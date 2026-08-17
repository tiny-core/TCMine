using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCMine.Contracts.Hubs;
using TCMine.Contracts.Identity;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Identity;
using TCMine.Server.Infrastructure.Persistence;
using TCMine.Server.Web.Tests.Infrastructure;

namespace TCMine.Server.Web.Tests.Endpoints;

/// <summary>
///     O ciclo completo, com o pipeline real: o jogador entra pelo launcher, não
///     enxerga nada, resgata um convite e passa a enxergar.
///     Existe porque as peças são testadas em separado e mesmo assim o caminho
///     pode não fechar — a sessão emitida pelo login do launcher precisa ser a
///     mesma que o hub reconhece, e o vínculo criado pelo resgate precisa ser o
///     mesmo que o ICurrentUserScope consulta. Um teste por peça não prova isso.
/// </summary>
public sealed class InviteFlowTests
{
    [Fact]
    public async Task Convite_resgatado_abre_o_acesso_ao_servidor_no_hub()
    {
        await using var factory = new TcMineAppFactory
        {
            Servicos = services => services.AddSingleton<IMinecraftProfileSource>(
                new FakeProfiles(new MinecraftProfile("abc123", "ana")))
        };

        var client = factory.CreateClient();
        var servidorId = Guid.CreateVersion7();

        // O jogador entra: conta criada, sessão válida, acesso a nada.
        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/minecraft",
            new MinecraftLoginRequest { AccessToken = "token-bom" },
            TestContext.Current.CancellationToken);

        login.StatusCode.ShouldBe(HttpStatusCode.OK);

        var cookie = login.Headers.GetValues("Set-Cookie")
            .First(c => c.StartsWith("tcmine.auth=", StringComparison.Ordinal))
            .Split(';')[0];

        await using (var antes = Conectar(factory, cookie))
        {
            await antes.StartAsync(TestContext.Current.CancellationToken);

            var semConvite = async () => await antes.InvokeAsync(
                nameof(IServerHub.SubscribeServerAsync), servidorId);

            await semConvite.ShouldThrowAsync<HubException>();
        }

        var codigo = await SemearConviteAsync(factory, servidorId);

        var resgate = await client.PostAsJsonAsync(
            "/api/v1/invites/redeem",
            new RedeemInviteRequest { Code = codigo },
            TestContext.Current.CancellationToken);

        resgate.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Conexão nova: o papel é consultado ao vivo, então nem seria preciso —
        // mas reconectar prova que o vínculo ficou gravado, e não só em memória.
        await using var depois = Conectar(factory, cookie);
        await depois.StartAsync(TestContext.Current.CancellationToken);

        var comConvite = async () => await depois.InvokeAsync(
            nameof(IServerHub.SubscribeServerAsync), servidorId);

        await comConvite.ShouldNotThrowAsync();
    }

    [Fact]
    public async Task Resgate_sem_sessao_e_recusado()
    {
        await using var factory = new TcMineAppFactory();
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var resposta = await client.PostAsJsonAsync(
            "/api/v1/invites/redeem",
            new RedeemInviteRequest { Code = "AAAA-BBBB-CCCC-DDDD" },
            TestContext.Current.CancellationToken);

        resposta.StatusCode.ShouldNotBe(HttpStatusCode.NoContent);
    }

    /// <summary>
    ///     Grava um convite direto na base. Criar pelo caso de uso exigiria um
    ///     servidor real e um Owner para convidar — o que este teste não está
    ///     verificando.
    /// </summary>
    private static async Task<string> SemearConviteAsync(TcMineAppFactory factory, Guid servidorId)
    {
        var codigo = Server.Application.Security.SecureToken.GenerateCode();

        using var escopo = factory.Services.CreateScope();
        var db = await escopo.ServiceProvider
            .GetRequiredService<IDbContextFactory<TcMineDbContext>>()
            .CreateDbContextAsync(TestContext.Current.CancellationToken);

        db.Invites.Add(new Invite
        {
            CodeHash = Server.Application.Security.SecureToken.Hash(
                Server.Application.Security.SecureToken.NormalizeCode(codigo)),
            GameServerId = servidorId,
            Role = ServerRole.Moderator,
            CreatedByUserId = Guid.CreateVersion7(),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        });

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return codigo;
    }

    private static HubConnection Conectar(TcMineAppFactory factory, string cookie) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, HubRoutes.Main), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.Headers["Cookie"] = cookie;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

    private sealed class FakeProfiles(MinecraftProfile? profile) : IMinecraftProfileSource
    {
        public Task<MinecraftProfile?> GetProfileAsync(string accessToken, CancellationToken ct) =>
            Task.FromResult(profile);
    }
}
