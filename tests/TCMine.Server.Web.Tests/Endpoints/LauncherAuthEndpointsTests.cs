using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using TCMine.Contracts.Hubs;
using TCMine.Contracts.Identity;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Web.Tests.Infrastructure;

namespace TCMine.Server.Web.Tests.Endpoints;

/// <summary>
///     Porta de entrada do launcher.
///     O teste mais importante daqui não é o do login que funciona: é o último,
///     que fixa o limite desta fatia — quem entra pelo launcher ganha uma sessão
///     válida e, ainda assim, nenhum acesso a servidor. Sem esse limite escrito,
///     o fluxo de convite poderia ser construído por cima de uma porta já aberta.
/// </summary>
public sealed class LauncherAuthEndpointsTests
{
    [Fact]
    public async Task Login_valido_abre_sessao_e_devolve_o_jogador()
    {
        await using var factory = ComPerfil(new MinecraftProfile("abc123", "ana"));
        var client = factory.CreateClient();

        var resposta = await client.PostAsJsonAsync(
            "/api/v1/auth/minecraft",
            new MinecraftLoginRequest { AccessToken = "token-bom" },
            TestContext.Current.CancellationToken);

        resposta.StatusCode.ShouldBe(HttpStatusCode.OK);

        var sessao = await resposta.Content.ReadFromJsonAsync<LauncherSessionDto>(
            TestContext.Current.CancellationToken);

        sessao.ShouldNotBeNull();
        sessao.MinecraftUuid.ShouldBe("abc123");
        sessao.DisplayName.ShouldBe("ana");

        resposta.Headers.GetValues("Set-Cookie")
            .ShouldContain(c => c.StartsWith("tcmine.auth=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Token_recusado_pela_mojang_devolve_401_sem_cookie()
    {
        await using var factory = ComPerfil(null);
        var client = factory.CreateClient();

        var resposta = await client.PostAsJsonAsync(
            "/api/v1/auth/minecraft",
            new MinecraftLoginRequest { AccessToken = "token-ruim" },
            TestContext.Current.CancellationToken);

        // 401 e não 400: o pedido estava correto, a credencial é que não serve.
        resposta.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        resposta.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    [Fact]
    public async Task Jogador_autenticado_ainda_nao_enxerga_servidor_nenhum()
    {
        await using var factory = ComPerfil(new MinecraftProfile("abc123", "ana"));
        var client = factory.CreateClient();

        var resposta = await client.PostAsJsonAsync(
            "/api/v1/auth/minecraft",
            new MinecraftLoginRequest { AccessToken = "token-bom" },
            TestContext.Current.CancellationToken);

        var cookie = resposta.Headers.GetValues("Set-Cookie")
            .First(c => c.StartsWith("tcmine.auth=", StringComparison.Ordinal))
            .Split(';')[0];

        await using var conexao = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, HubRoutes.Main), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.Headers["Cookie"] = cookie;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

        await conexao.StartAsync(TestContext.Current.CancellationToken);

        // A conexão é aceita — a sessão é legítima. O que falta é vínculo, e é
        // o Membership (fatia do convite) que vai concedê-lo.
        var acao = async () => await conexao.InvokeAsync(
            nameof(IServerHub.SubscribeServerAsync),
            Guid.CreateVersion7());

        await acao.ShouldThrowAsync<HubException>();
    }

    private static TcMineAppFactory ComPerfil(MinecraftProfile? profile) =>
        new()
        {
            Servicos = services =>
                services.AddSingleton<IMinecraftProfileSource>(new FakeProfiles(profile))
        };

    private sealed class FakeProfiles(MinecraftProfile? profile) : IMinecraftProfileSource
    {
        public Task<MinecraftProfile?> GetProfileAsync(string accessToken, CancellationToken ct) =>
            Task.FromResult(profile);
    }
}
