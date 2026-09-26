using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Identity;
using TCMine.Launcher.Core.Connectivity;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.Infrastructure;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Web.Tests.Infrastructure;

namespace TCMine.Server.Web.Tests.Endpoints;

/// <summary>
///     O launcher entra e vê o catálogo — com o cliente de verdade, contra o
///     servidor de verdade.
///     Existe porque a sessão atravessa dois transportes: ela nasce como cookie
///     numa resposta HTTP e precisa ser reapresentada numa conexão SignalR. Cada
///     metade tinha teste; o que faltava era a prova de que o cookie emitido no
///     login é o mesmo que o hub aceita — e essa prova exige o cliente montando a
///     própria conexão, e não um HubConnection escrito no teste com o cabeçalho
///     na mão.
///     Por isso o Kestrel numa porta real: com o TestServer não haveria onde o
///     CookieContainer do cliente agir.
/// </summary>
public sealed class LauncherCatalogContractTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Jogador_entra_e_o_catalogo_chega_pelo_hub()
    {
        await using var server = new RealPortAppFactory
        {
            Servicos = services => services.AddSingleton<IMinecraftProfileSource>(
                new PerfilFixo(new MinecraftProfile("abc123", "ana")))
        };

        var name = $"Pack {Guid.CreateVersion7():N}"[..12];
        await SemearModpackAsync(server, name);

        await using var launcher = MontarLauncher();

        var config = new TCMine.Contracts.LauncherConfig
        {
            Schema = 1, ServerUrl = server.Address, AzureClientId = "client-id-de-teste"
        };

        // 1. Entra: o token vira cookie de sessão, guardado pelo cliente.
        var entrada = await launcher.GetRequiredService<SignIn>().InteractiveAsync(config, Ct);

        entrada.IsSignedIn.ShouldBeTrue(entrada.Message ?? "sem mensagem");
        entrada.Session!.DisplayName.ShouldBe("ana");

        // 2. Pede o catálogo pelo hub. Ninguém passou credencial aqui: se o
        //    cookie não tivesse acompanhado, a negociação levaria 401.
        var catalogo = await launcher.GetRequiredService<LoadCatalog>().HandleAsync(server.Address, Ct);

        catalogo.Failed.ShouldBeFalse(catalogo.Error ?? "sem erro");
        catalogo.Entries.ShouldContain(e => e.Modpack.Name == name);
    }

    [Fact]
    public async Task Sem_entrar_o_hub_recusa_a_conexao()
    {
        // O outro lado da mesma moeda: a proteção do hub não pode depender de a
        // interface esconder a tela.
        await using var server = new RealPortAppFactory();
        await using var launcher = MontarLauncher();

        var catalogo = await launcher.GetRequiredService<LoadCatalog>()
            .HandleAsync(server.Address, Ct);

        catalogo.Failed.ShouldBeTrue("um anônimo não pode listar o catálogo");
    }

    /// <summary>
    ///     O contêiner do launcher, montado como no aplicativo: mesma
    ///     infraestrutura, mesmos casos de uso. Só o autenticador é trocado, pela
    ///     mesma razão que o resto da suíte troca o perfil da Mojang — depender
    ///     da Microsoft de verdade tornaria o teste refém dela.
    /// </summary>
    private static ServiceProvider MontarLauncher()
    {
        var raiz = Path.Combine(Path.GetTempPath(), $"tcmine-launcher-{Guid.CreateVersion7():N}");

        var services = new ServiceCollection();

        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddLauncherInfrastructure(raiz);
        services.AddLauncherCore();
        services.AddSingleton<IMinecraftAuthenticator, AutenticadorFixo>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task O_historico_de_versoes_atravessa_o_hub()
    {
        // Atravessar É o teste. Um tipo novo neste caminho é exatamente onde o
        // MessagePack já mordeu uma vez — a chamada morre em runtime e derruba a
        // conexão, e nenhum teste que fale JSON vê isso acontecer.
        await using var server = new RealPortAppFactory
        {
            Servicos = services => services.AddSingleton<IMinecraftProfileSource>(
                new PerfilFixo(new MinecraftProfile("abc123", "ana")))
        };

        var modpackId = await SemearComVersoesAsync(server);

        await using var launcher = MontarLauncher();

        var config = new TCMine.Contracts.LauncherConfig
        {
            Schema = 1, ServerUrl = server.Address, AzureClientId = "client-id-de-teste"
        };

        await launcher.GetRequiredService<SignIn>().InteractiveAsync(config, Ct);

        var connection = launcher.GetRequiredService<IServerConnection>();
        await connection.ConnectAsync(server.Address, Ct);

        var versions = await connection.GetVersionsAsync(modpackId, ReleaseChannel.Release, Ct);

        // Da mais nova para a mais velha, e sem a Draft nem a pré-lançamento:
        // o canal estável não vê o alpha.
        versions.Select(v => v.Version).ShouldBe(["1.1.0", "1.0.0"]);

        // Sem os arquivos, por desenho — o resumo existe para a lista não
        // carregar o manifesto inteiro de cada versão.
        versions[0].LoaderVersion.ShouldBe("21.1.0");

        // E o alpha vê só a sua, que é o que faz dele um canal. A Draft continua
        // de fora dos dois: não está publicada em canal nenhum.
        var alphas = await connection.GetVersionsAsync(modpackId, ReleaseChannel.Alpha, Ct);

        alphas.Select(v => v.Version).ShouldBe(["1.2.0-beta"]);
    }

    [Fact]
    public async Task As_novidades_publicadas_atravessam_o_hub()
    {
        await using var server = new RealPortAppFactory
        {
            Servicos = services => services.AddSingleton<IMinecraftProfileSource>(
                new PerfilFixo(new MinecraftProfile("abc123", "ana")))
        };

        var modpackId = await SemearComNovidadesAsync(server);

        await using var launcher = MontarLauncher();

        var config = new TCMine.Contracts.LauncherConfig
        {
            Schema = 1, ServerUrl = server.Address, AzureClientId = "client-id-de-teste"
        };

        await launcher.GetRequiredService<SignIn>().InteractiveAsync(config, Ct);

        var connection = launcher.GetRequiredService<IServerConnection>();
        await connection.ConnectAsync(server.Address, Ct);

        var news = await connection.GetNewsAsync(modpackId, Ct);

        // Da mais recente para a mais antiga, e SEM o rascunho: o filtro vive no
        // hub e não na tela, porque quem tem a URL chama o método diretamente.
        news.Select(n => n.Title).ShouldBe(["Segunda", "Primeira"]);
        news[0].Body.ShouldBe("corpo da segunda");
    }

    private static async Task<Guid> SemearComNovidadesAsync(RealPortAppFactory factory)
    {
        using var escopo = factory.Services.CreateScope();
        var modpacks = escopo.ServiceProvider.GetRequiredService<IModpackRepository>();
        var news = escopo.ServiceProvider.GetRequiredService<INewsRepository>();

        var modpack = new Modpack
        {
            Slug = $"pack-{Guid.CreateVersion7():N}"[..18],
            Name = "Pack com novidades",
            MinecraftVersion = "1.21.1",
            Loader = ModLoader.NeoForge
        };

        await modpacks.CreateAsync(modpack, Ct);

        foreach (var (titulo, publicado) in
                 new[] { ("Primeira", true), ("Segunda", true), ("Rascunho", false) })
        {
            await news.AddAsync(
                new News
                {
                    ModpackId = modpack.Id,
                    Title = titulo,
                    Body = $"corpo da {titulo.ToLowerInvariant()}",
                    IsPublished = publicado
                },
                Ct);
        }

        return modpack.Id;
    }

    /// <summary>
    ///     Um pack com duas versões publicadas, uma pré-lançamento e uma Draft.
    ///     As três últimas existem para provar o filtro: oferecer no seletor o
    ///     que o GetLatestVersionAsync esconde seria dar pela porta do lado o que
    ///     a porta da frente recusa.
    /// </summary>
    private static async Task<Guid> SemearComVersoesAsync(RealPortAppFactory factory)
    {
        using var escopo = factory.Services.CreateScope();
        var repo = escopo.ServiceProvider.GetRequiredService<IModpackRepository>();

        var modpack = new Modpack
        {
            Slug = $"pack-{Guid.CreateVersion7():N}"[..18],
            Name = "Pack com histórico",
            MinecraftVersion = "1.21.1",
            Loader = ModLoader.NeoForge
        };

        await repo.CreateAsync(modpack, Ct);

        foreach (var (numero, publicar) in
                 new[] { ("1.0.0", true), ("1.1.0", true), ("1.2.0-beta", true), ("1.3.0", false) })
        {
            var version = new ModpackVersion
            {
                ModpackId = modpack.Id,
                Version = numero,
                LoaderVersion = "21.1.0"
            };

            if (publicar)
            {
                // Uma versão sem arquivos não publica — regra do domínio, e este
                // teste não a está a testar. Um mod chega.
                version.UpsertFile(new ModpackFile
                {
                    ModpackVersionId = version.Id,
                    Path = "mods/jei.jar",
                    Sha256 = new string('a', 64),
                    SizeBytes = 1,
                    Side = FileSide.Both,
                    Origin = ModFileOrigin.Modrinth,
                    ProjectSlug = "jei"
                });

                version.MarkResolving();
                version.MarkReady();
            }

            await repo.AddVersionAsync(version, Ct);
        }

        return modpack.Id;
    }

    private static async Task SemearModpackAsync(RealPortAppFactory factory, string name)
    {
        using var escopo = factory.Services.CreateScope();
        var repo = escopo.ServiceProvider.GetRequiredService<IModpackRepository>();

        await repo.CreateAsync(
            new Modpack
            {
                Slug = $"pack-{Guid.CreateVersion7():N}"[..18],
                Name = name,
                MinecraftVersion = "1.21.1",
                Loader = ModLoader.NeoForge
            },
            Ct);
    }

    private sealed class PerfilFixo(MinecraftProfile? profile) : IMinecraftProfileSource
    {
        public Task<MinecraftProfile?> GetProfileAsync(string accessToken, CancellationToken ct) =>
            Task.FromResult(profile);
    }

    private sealed class AutenticadorFixo : IMinecraftAuthenticator
    {
        public Task<AuthResult> TrySilentAsync(string azureClientId, CancellationToken ct) =>
            Task.FromResult(AuthResult.NoStoredCredentials());

        public Task<AuthResult> SignInAsync(string azureClientId, CancellationToken ct) =>
            Task.FromResult(AuthResult.Success("token-bom"));

        public Task SignOutAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
