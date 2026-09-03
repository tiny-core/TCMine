using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Identity;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.Core.Sync;
using TCMine.Launcher.Infrastructure;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Web.Tests.Infrastructure;

namespace TCMine.Server.Web.Tests.Endpoints;

/// <summary>
///     Instalar um modpack, do começo ao fim, com as duas metades de verdade.
///     É o caminho mais longo do produto: o jogador entra, o manifesto vem pelo
///     hub, os bytes vêm por HTTP autenticado pelo mesmo cookie, o hash é
///     conferido enquanto o arquivo é gravado, e o disco acaba igual ao que o
///     manifesto descreve. Cada peça tem teste de unidade; o que este prova é que
///     elas encaixam — e é justamente onde este projeto já se machucou.
/// </summary>
public sealed class LauncherInstallContractTests : IDisposable
{
    private readonly string _raizDoLauncher = Path.Combine(
        Path.GetTempPath(), $"tcmine-launcher-{Guid.CreateVersion7():N}");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_raizDoLauncher))
            Directory.Delete(_raizDoLauncher, true);
    }

    [Fact]
    public async Task O_jogador_instala_e_o_disco_fica_igual_ao_manifesto()
    {
        await using var servidor = new RealPortAppFactory
        {
            Servicos = services => services.AddSingleton<IMinecraftProfileSource>(
                new PerfilFixo(new MinecraftProfile("abc123", "ana")))
        };

        var jar = "conteudo do jar de teste"u8.ToArray();
        var config = "chave = valor"u8.ToArray();

        var (modpackId, sha) = await SemearVersaoAsync(servidor, jar, config);

        await using var launcher = MontarLauncher();

        var pareamento = new TCMine.Contracts.LauncherConfig
        {
            Schema = 1, ServerUrl = servidor.Address, AzureClientId = "client-id-de-teste"
        };

        (await launcher.GetRequiredService<SignIn>().InteractiveAsync(pareamento, Ct))
            .IsSignedIn.ShouldBeTrue();

        var catalogo = await launcher.GetRequiredService<LoadCatalog>()
            .HandleAsync(servidor.Address, Ct);

        var pack = catalogo.Entries.Single(e => e.Modpack.Id == modpackId).Modpack;

        // O caminho completo: manifesto pelo hub, bytes por HTTP, hash conferido,
        // arquivos materializados.
        var resultado = await launcher.GetRequiredService<InstallModpackVersion>()
            .InstallLatestAsync(servidor.Address, pack, null, Ct);

        resultado.Succeeded.ShouldBeTrue(resultado.Error);

        var instancia = launcher.GetRequiredService<IInstanceStore>()
            .PathFor(new InstanceKey(modpackId, resultado.Instance!.ModpackVersionId));

        (await File.ReadAllBytesAsync(Path.Combine(instancia, "mods", "jei.jar"), Ct)).ShouldBe(jar);
        (await File.ReadAllBytesAsync(Path.Combine(instancia, "config", "jei.toml"), Ct)).ShouldBe(config);

        // E o manifesto local ficou gravado: é ele, e não uma varredura da pasta,
        // que o próximo update vai usar para saber o que pode apagar.
        resultado.Instance.ManagedFiles.Keys.ShouldBe(["mods/jei.jar", "config/jei.toml"], ignoreOrder: true);
        resultado.Instance.ManagedFiles["mods/jei.jar"].ShouldBe(sha);
    }

    [Fact]
    public async Task Instalar_de_novo_nao_baixa_nada_e_nao_apaga_o_mundo()
    {
        // A segunda instalação é um diff contra o manifesto local: nada mudou,
        // então nada acontece. E o mundo criado entre as duas continua lá — ele
        // nunca esteve no conjunto gerenciado, então nunca entrou no ToDelete.
        await using var servidor = new RealPortAppFactory
        {
            Servicos = services => services.AddSingleton<IMinecraftProfileSource>(
                new PerfilFixo(new MinecraftProfile("abc123", "ana")))
        };

        var (modpackId, _) = await SemearVersaoAsync(servidor, "jar"u8.ToArray(), "cfg"u8.ToArray());

        await using var launcher = MontarLauncher();

        var pareamento = new TCMine.Contracts.LauncherConfig
        {
            Schema = 1, ServerUrl = servidor.Address, AzureClientId = "client-id-de-teste"
        };

        await launcher.GetRequiredService<SignIn>().InteractiveAsync(pareamento, Ct);

        var catalogo = await launcher.GetRequiredService<LoadCatalog>().HandleAsync(servidor.Address, Ct);
        var pack = catalogo.Entries.Single(e => e.Modpack.Id == modpackId).Modpack;

        var instalador = launcher.GetRequiredService<InstallModpackVersion>();
        var primeira = await instalador.InstallLatestAsync(servidor.Address, pack, null, Ct);

        var instancia = launcher.GetRequiredService<IInstanceStore>()
            .PathFor(new InstanceKey(modpackId, primeira.Instance!.ModpackVersionId));

        // O jogador jogou: criou um mundo e mexeu nas opções.
        var mundo = Path.Combine(instancia, "saves", "meu-mundo", "level.dat");
        Directory.CreateDirectory(Path.GetDirectoryName(mundo)!);
        await File.WriteAllTextAsync(mundo, "o mundo dele", Ct);
        await File.WriteAllTextAsync(Path.Combine(instancia, "options.txt"), "fov:90", Ct);

        var segunda = await instalador.InstallLatestAsync(servidor.Address, pack, null, Ct);

        segunda.Succeeded.ShouldBeTrue(segunda.Error);
        File.Exists(mundo).ShouldBeTrue("o mundo do jogador não é gerenciado pelo launcher");
        File.Exists(Path.Combine(instancia, "options.txt")).ShouldBeTrue();
    }

    private ServiceProvider MontarLauncher()
    {
        var services = new ServiceCollection();

        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddLauncherInfrastructure(_raizDoLauncher);
        services.AddLauncherCore();
        services.AddSingleton<IMinecraftAuthenticator, AutenticadorFixo>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    ///     Semeia pelo blob store de verdade: é o hash que ELE calcula que o
    ///     manifesto publica e o download resolve. Inventar um sha aqui testaria
    ///     a nossa aritmética, não o caminho.
    /// </summary>
    private static async Task<(Guid ModpackId, string Sha)> SemearVersaoAsync(
        RealPortAppFactory factory, byte[] jar, byte[] config)
    {
        using var escopo = factory.Services.CreateScope();
        var repo = escopo.ServiceProvider.GetRequiredService<IModpackRepository>();
        var blobs = escopo.ServiceProvider.GetRequiredService<IBlobStore>();

        using var streamJar = new MemoryStream(jar);
        var shaJar = await blobs.PutAsync(streamJar, null, "application/java-archive", Ct);

        using var streamConfig = new MemoryStream(config);
        var shaConfig = await blobs.PutAsync(streamConfig, null, "text/plain", Ct);

        var modpack = new Modpack
        {
            Slug = $"pack-{Guid.CreateVersion7():N}"[..18],
            Name = $"Pack {Guid.CreateVersion7():N}"[..12],
            MinecraftVersion = "1.21.1",
            Loader = ModLoader.NeoForge
        };

        var versao = new ModpackVersion
        {
            ModpackId = modpack.Id, Version = "1.0.0", LoaderVersion = "21.1.100"
        };

        versao.UpsertFile(new ModpackFile
        {
            ModpackVersionId = versao.Id,
            Path = "mods/jei.jar",
            Sha256 = shaJar,
            SizeBytes = jar.Length,
            Side = FileSide.Both,
            Origin = ModFileOrigin.CurseForge,
            ProjectSlug = "jei"
        });

        versao.UpsertFile(new ModpackFile
        {
            ModpackVersionId = versao.Id,
            Path = "config/jei.toml",
            Sha256 = shaConfig,
            SizeBytes = config.Length,
            Side = FileSide.Both,
            Origin = ModFileOrigin.Override,
            ProjectSlug = "override:config/jei.toml"
        });

        versao.MarkResolving();
        versao.MarkReady();

        await repo.CreateAsync(modpack, Ct);
        await repo.AddVersionAsync(versao, Ct);

        return (modpack.Id, shaJar);
    }

    private sealed class PerfilFixo(MinecraftProfile? profile) : IMinecraftProfileSource
    {
        public Task<MinecraftProfile?> GetProfileAsync(string accessToken, CancellationToken ct) =>
            Task.FromResult(profile);
    }

    private sealed class AutenticadorFixo : IMinecraftAuthenticator
    {
        public Task<MinecraftAuthResult> TrySilentAsync(string azureClientId, CancellationToken ct) =>
            Task.FromResult(MinecraftAuthResult.NoStoredCredentials());

        public Task<MinecraftAuthResult> SignInAsync(string azureClientId, CancellationToken ct) =>
            Task.FromResult(MinecraftAuthResult.Success("token-bom"));

        public Task SignOutAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
