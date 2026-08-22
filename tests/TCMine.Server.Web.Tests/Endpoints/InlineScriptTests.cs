using System.Text.RegularExpressions;
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
public sealed class InlineScriptTests
{
    // Uma anônima e as autenticadas: as duas famílias renderizam por caminhos
    // diferentes (SSR estático e circuito interativo).
    public static TheoryData<string> Rotas => new() { "/setup", "/", "/modpacks", "/servers", "/mods", "/settings", "/storage" };

    [Theory]
    [MemberData(nameof(Rotas))]
    public async Task Nenhuma_pagina_serve_script_inline(string rota)
    {
        await using var factory = new TcMineAppFactory();
        var cookie = await factory.EntrarComoAdminAsync();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", cookie);

        var html = await client.GetStringAsync(rota, TestContext.Current.CancellationToken);

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
