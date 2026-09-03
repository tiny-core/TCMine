using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Identity;
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
        await using var servidor = new RealPortAppFactory
        {
            Servicos = services => services.AddSingleton<IMinecraftProfileSource>(
                new PerfilFixo(new MinecraftProfile("abc123", "ana")))
        };

        var nome = $"Pack {Guid.CreateVersion7():N}"[..12];
        await SemearModpackAsync(servidor, nome);

        await using var launcher = MontarLauncher();

        var config = new TCMine.Contracts.LauncherConfig
        {
            Schema = 1, ServerUrl = servidor.Address, AzureClientId = "client-id-de-teste"
        };

        // 1. Entra: o token vira cookie de sessão, guardado pelo cliente.
        var entrada = await launcher.GetRequiredService<SignIn>().InteractiveAsync(config, Ct);

        entrada.IsSignedIn.ShouldBeTrue(entrada.Message ?? "sem mensagem");
        entrada.Session!.DisplayName.ShouldBe("ana");

        // 2. Pede o catálogo pelo hub. Ninguém passou credencial aqui: se o
        //    cookie não tivesse acompanhado, a negociação levaria 401.
        var catalogo = await launcher.GetRequiredService<LoadCatalog>().HandleAsync(servidor.Address, Ct);

        catalogo.Failed.ShouldBeFalse(catalogo.Error ?? "sem erro");
        catalogo.Entries.ShouldContain(e => e.Modpack.Name == nome);
    }

    [Fact]
    public async Task Sem_entrar_o_hub_recusa_a_conexao()
    {
        // O outro lado da mesma moeda: a proteção do hub não pode depender de a
        // interface esconder a tela.
        await using var servidor = new RealPortAppFactory();
        await using var launcher = MontarLauncher();

        var catalogo = await launcher.GetRequiredService<LoadCatalog>()
            .HandleAsync(servidor.Address, Ct);

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

    private static async Task SemearModpackAsync(RealPortAppFactory factory, string nome)
    {
        using var escopo = factory.Services.CreateScope();
        var repo = escopo.ServiceProvider.GetRequiredService<IModpackRepository>();

        await repo.CreateAsync(
            new Modpack
            {
                Slug = $"pack-{Guid.CreateVersion7():N}"[..18],
                Name = nome,
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
        public Task<MinecraftAuthResult> TrySilentAsync(string azureClientId, CancellationToken ct) =>
            Task.FromResult(MinecraftAuthResult.NoStoredCredentials());

        public Task<MinecraftAuthResult> SignInAsync(string azureClientId, CancellationToken ct) =>
            Task.FromResult(MinecraftAuthResult.Success("token-bom"));

        public Task SignOutAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
