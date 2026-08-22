using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Web.Tests.Infrastructure;

namespace TCMine.Server.Web.Tests.Endpoints;

/// <summary>
///     Nenhuma página pode servir JavaScript inline.
///     A CSP do painel declara <c>script-src 'self'</c>, sem 'unsafe-inline',
///     sem nonce e sem hash. Isso é uma escolha — é o que fecha a porta de XSS
///     mais comum —, mas ela só se sustenta enquanto TODO script for arquivo
///     servido por nós. Um &lt;script&gt; embutido numa página passa despercebido
///     em desenvolvimento (a página renderiza, e o que quebra é só o trecho
///     bloqueado) e só aparece como erro no console de quem usa.
///     Este teste existe porque o comentário no SecurityHeaders afirmava essa
///     regra sem ninguém verificá-la.
/// </summary>
public sealed class InlineScriptTests(PainelAutenticado painel) : IClassFixture<PainelAutenticado>
{
    // Uma anônima e as autenticadas: as duas famílias renderizam por caminhos
    // diferentes (SSR estático e circuito interativo).
    public static TheoryData<string> Rotas =>
        new() { "/setup", "/", "/modpacks", "/servers", "/mods", "/settings", "/storage" };

    [Theory]
    [MemberData(nameof(Rotas))]
    public async Task Nenhuma_pagina_serve_script_inline(string rota)
    {
        var html = await painel.BuscarAsync(rota, TestContext.Current.CancellationToken);

        var inline = Regex
            .Matches(html, @"<script([^>]*)>(.*?)</script>", RegexOptions.Singleline)
            .Where(m => !m.Groups[1].Value.Contains("src=", StringComparison.OrdinalIgnoreCase))
            .Where(m => m.Groups[2].Value.Trim().Length > 0)
            .Select(m => m.Groups[2].Value.Trim())
            .ToList();

        inline.ShouldBeEmpty(
            $"{rota} serve script inline, que a CSP 'script-src self' bloqueia no navegador");
    }
}

/// <summary>
///     Uma aplicação de pé, com admin logado, compartilhada pelas rotas.
///     Subir uma por rota custava sete arranques com migrations — e o cálculo
///     não muda de rota para rota.
///     Os dois orquestradores são substituídos porque falam com o DOCKER: a tela
///     de configurações consulta o estado do servidor de e-mail ao renderizar, e
///     a de servidores sincroniza o status dos containers. No CI isso pendurou o
///     pedido até o teste ser cancelado — uma falha que não tem nada a ver com o
///     que este teste afirma. É exatamente para isto que a fábrica expõe o
///     ponto de troca de serviços.
/// </summary>
public sealed class PainelAutenticado : IAsyncLifetime
{
    private TcMineAppFactory _factory = default!;
    private HttpClient _client = default!;

    public async ValueTask InitializeAsync()
    {
        _factory = new TcMineAppFactory
        {
            Servicos = services =>
            {
                services.AddSingleton<IMailServerOrchestrator>(new MailParado());
                services.AddSingleton<IServerOrchestrator>(new ContainersParados());
            }
        };

        var cookie = await _factory.EntrarComoAdminAsync();

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("Cookie", cookie);
    }

    public Task<string> BuscarAsync(string rota, CancellationToken ct) =>
        _client.GetStringAsync(rota, ct);

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class MailParado : IMailServerOrchestrator
    {
        public Task<MailServerState> GetStateAsync(CancellationToken ct) =>
            Task.FromResult(MailServerState.NotCreated);

        public Task StartAsync(string domain, CancellationToken ct) => Task.CompletedTask;
        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
        public Task RemoveAsync(CancellationToken ct) => Task.CompletedTask;
        public Task<string?> GetDkimRecordAsync(string domain, CancellationToken ct) =>
            Task.FromResult<string?>(null);

        public Task EnsureSenderAccountAsync(string address, string password, CancellationToken ct) =>
            Task.CompletedTask;
    }

    private sealed class ContainersParados : IServerOrchestrator
    {
        public Task<string> EnsureCreatedAsync(Guid gameServerId, CancellationToken ct) =>
            Task.FromResult("container-de-teste");

        public Task StartAsync(Guid gameServerId, CancellationToken ct) => Task.CompletedTask;
        public Task StopAsync(Guid gameServerId, TimeSpan timeout, CancellationToken ct) => Task.CompletedTask;

        public Task<GameServerStatus> GetStatusAsync(Guid gameServerId, CancellationToken ct) =>
            Task.FromResult(GameServerStatus.Stopped);

        public Task RemoveAsync(Guid gameServerId, CancellationToken ct) => Task.CompletedTask;

        public async IAsyncEnumerable<ConsoleLine> StreamLogsAsync(
            Guid gameServerId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            // Nenhuma linha: o console não faz parte do que este teste afirma.
            await Task.CompletedTask;
            yield break;
        }
    }
}
