using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using TCMine.Contracts.Hubs;
using TCMine.Contracts.Identity;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Web.Tests.Infrastructure;

namespace TCMine.Server.Web.Tests.Endpoints;

/// <summary>
///     O caminho que o launcher percorre para instalar uma versão: entrar, pedir
///     o manifesto e baixar os arquivos.
///     Existe porque é a interface contra a qual o launcher será escrito, e ela
///     atravessa três mecanismos diferentes — cookie de sessão emitido por um
///     endpoint HTTP, invocação num hub SignalR, e download por um endpoint que
///     serve bytes do content store. Cada peça tem teste; o que faltava era a
///     prova de que elas encaixam.
///     O manifesto é o contrato do modelo declarativo: ele descreve o estado
///     final desejado, e é sobre ele que o <c>ManifestDiffer</c> decide o que
///     baixar e o que apagar. Um campo que não chegue aqui vira arquivo faltando
///     na máquina do jogador.
/// </summary>
public sealed class LauncherSyncContractTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Launcher_obtem_o_manifesto_e_baixa_o_arquivo()
    {
        await using var factory = new TcMineAppFactory
        {
            Servicos = services => services.AddSingleton<IMinecraftProfileSource>(
                new PerfilFixo(new MinecraftProfile("abc123", "ana")))
        };

        var conteudo = "conteudo do jar"u8.ToArray();
        var (versionId, sha) = await SemearVersaoPublicadaAsync(factory, conteudo);

        var client = factory.CreateClient();

        // 1. O jogador entra pelo launcher.
        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/minecraft",
            new MinecraftLoginRequest { AccessToken = "token-bom" },
            Ct);

        login.StatusCode.ShouldBe(HttpStatusCode.OK);

        var cookie = login.Headers.GetValues("Set-Cookie")
            .First(c => c.StartsWith("tcmine.auth=", StringComparison.Ordinal))
            .Split(';')[0];

        // 2. Pede o manifesto pelo hub.
        await using var hub = Conectar(factory, cookie);
        await hub.StartAsync(Ct);

        var manifesto = await hub.InvokeAsync<ModpackVersionDto>(
            nameof(IServerHub.GetModpackVersionAsync), versionId, Ct);

        manifesto.ShouldNotBeNull();

        var arquivo = manifesto.Files.ShouldHaveSingleItem();
        arquivo.Path.ShouldBe("mods/jei.jar");
        arquivo.Sha256.ShouldBe(sha);

        // O tamanho vai no manifesto porque o launcher mostra progresso antes de
        // começar: sem ele, a barra só existiria depois do download.
        arquivo.SizeBytes.ShouldBe(conteudo.Length);

        // 3. Baixa o que o manifesto mandou, pelo hash.
        var download = await client.GetAsync($"/api/v1/blobs/{arquivo.Sha256}", Ct);

        download.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await download.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(conteudo);
    }

    [Fact]
    public async Task Manifesto_de_versao_inexistente_nao_derruba_a_conexao()
    {
        // O launcher pode pedir uma versão que foi apagada entre o catálogo e a
        // instalação. A resposta tem de ser um erro tratável, e não a queda da
        // conexão — que levaria junto o acompanhamento de qualquer download em
        // curso.
        await using var factory = new TcMineAppFactory
        {
            Servicos = services => services.AddSingleton<IMinecraftProfileSource>(
                new PerfilFixo(new MinecraftProfile("abc123", "ana")))
        };

        var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/minecraft",
            new MinecraftLoginRequest { AccessToken = "token-bom" },
            Ct);

        var cookie = login.Headers.GetValues("Set-Cookie")
            .First(c => c.StartsWith("tcmine.auth=", StringComparison.Ordinal))
            .Split(';')[0];

        await using var hub = Conectar(factory, cookie);
        await hub.StartAsync(Ct);

        var pedir = async () => await hub.InvokeAsync<ModpackVersionDto>(
            nameof(IServerHub.GetModpackVersionAsync), Guid.CreateVersion7(), Ct);

        await pedir.ShouldThrowAsync<Exception>();

        hub.State.ShouldBe(HubConnectionState.Connected, "a conexão sobrevive ao erro");
    }

    private static async Task<(Guid VersionId, string Sha)> SemearVersaoPublicadaAsync(
        TcMineAppFactory factory, byte[] conteudo)
    {
        using var escopo = factory.Services.CreateScope();
        var repo = escopo.ServiceProvider.GetRequiredService<IModpackRepository>();
        var blobs = escopo.ServiceProvider.GetRequiredService<IBlobStore>();

        // Pelo store de verdade: é o hash que ele calcula que o manifesto
        // publica e o download resolve. Inventar um sha aqui testaria a nossa
        // aritmética, não o caminho.
        using var stream = new MemoryStream(conteudo);
        var sha = await blobs.PutAsync(stream, null, "application/java-archive", Ct);

        var modpack = new Modpack
        {
            Slug = $"pack-{Guid.CreateVersion7():N}"[..18],
            Name = "Pack",
            MinecraftVersion = "1.21.1",
            Loader = ModLoader.NeoForge
        };

        var version = new ModpackVersion
        {
            ModpackId = modpack.Id, Version = "1.0.0", LoaderVersion = "21.1.100"
        };

        version.UpsertFile(new ModpackFile
        {
            ModpackVersionId = version.Id,
            Path = "mods/jei.jar",
            Sha256 = sha,
            SizeBytes = conteudo.Length,
            Side = FileSide.Both,
            Origin = ModFileOrigin.CurseForge,
            ProjectSlug = "jei"
        });

        version.MarkResolving();
        version.MarkReady();

        await repo.CreateAsync(modpack, Ct);
        await repo.AddVersionAsync(version, Ct);

        return (version.Id, sha);
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

    private sealed class PerfilFixo(MinecraftProfile? profile) : IMinecraftProfileSource
    {
        public Task<MinecraftProfile?> GetProfileAsync(string accessToken, CancellationToken ct) =>
            Task.FromResult(profile);
    }
}
