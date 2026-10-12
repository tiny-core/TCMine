using TCMine.Contracts;
using TCMine.Contracts.Hubs;
using TCMine.Contracts.Identity;
using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Connectivity;
using TCMine.Launcher.Core.Identity;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.Core.Tests.Fakes;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.Core.Tests.UI;

/// <summary>
///     O catálogo guardado: o que a tela mostra muda quando o servidor muda, sem
///     o jogador ter de navegar para outra página e voltar.
/// </summary>
public sealed class CatalogStoreTests : IAsyncDisposable
{
    private static readonly Uri Server = new("https://modpacks.exemplo/");

    private readonly FakeServerConnection _connection = new();
    private readonly LauncherShellState _shell = new();

    public CatalogStoreTests()
    {
        _shell.Apply(new PairingState
        {
            Status = PairingStatus.Paired,
            Config = new LauncherConfig { Schema = 1, ServerUrl = Server, AzureClientId = "client" }
        });

        LogIn();
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();

    [Fact]
    public async Task Busca_o_catalogo_do_servidor()
    {
        _connection.Modpacks = [Modpack("Skyblock")];
        using var store = NewStore();

        await store.RefreshAsync();

        store.Current.ShouldNotBeNull().Entries.ShouldHaveSingleItem();
        store.IsLoading.ShouldBeFalse();
    }

    [Fact]
    public async Task Canal_que_volta_atualiza_o_catalogo_sem_navegar()
    {
        // Regressão: com o servidor de volta, a coluna de servidores da tela
        // Jogar ficava vazia até se trocar de página.
        _connection.Modpacks = [Modpack("Skyblock")];
        using var store = NewStore();
        await store.RefreshAsync();

        _connection.Modpacks = [Modpack("Skyblock"), Modpack("Tecnologia")];
        _shell.Apply(LinkStatus.Reconnecting);
        _shell.Apply(LinkStatus.Up);
        await store.WhenIdle();

        store.Current!.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Aviso_do_servidor_atualiza_o_catalogo_sem_navegar()
    {
        // Meta da fase: mudar um modpack no painel aparece no launcher sem o
        // jogador fazer nada.
        _connection.Modpacks = [Modpack("Skyblock")];
        using var store = NewStore();
        await store.RefreshAsync();

        _connection.Modpacks = [Modpack("Skyblock"), Modpack("Tecnologia")];
        _connection.RaiseInvalidated(ResourceKind.Modpacks, Guid.CreateVersion7());
        await store.WhenIdle();

        store.Current!.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Aviso_de_novidade_nao_rebusca_o_catalogo()
    {
        _connection.Modpacks = [Modpack("Skyblock")];
        using var store = NewStore();
        await store.RefreshAsync();

        _connection.Modpacks = [];
        _connection.RaiseInvalidated(ResourceKind.News, Guid.CreateVersion7());
        await store.WhenIdle();

        store.Current!.Entries.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Falha_numa_rebusca_mantem_o_ultimo_catalogo()
    {
        // Um catálogo de há um minuto serve melhor do que uma tela vazia.
        _connection.Modpacks = [Modpack("Skyblock")];
        using var store = NewStore();
        await store.RefreshAsync();

        _connection.Throws = new InvalidOperationException("canal em baixo");
        await store.RefreshAsync();

        store.Current!.Entries.ShouldHaveSingleItem();
        store.Error.ShouldNotBeNull();
        store.View.ShouldBeSameAs(store.Current);
    }

    [Fact]
    public async Task Falha_sem_nada_carregado_mostra_o_erro()
    {
        _connection.Throws = new InvalidOperationException("canal em baixo");
        using var store = NewStore();

        await store.RefreshAsync();

        store.Current.ShouldBeNull();
        store.View.ShouldNotBeNull().Failed.ShouldBeTrue();
    }

    [Fact]
    public async Task Sair_da_conta_esquece_o_catalogo()
    {
        // Quem entrar a seguir não pode ver, nem por um instante, servidores a
        // que só a conta anterior tinha acesso.
        _connection.Modpacks = [Modpack("Skyblock")];
        using var store = NewStore();
        await store.RefreshAsync();

        _shell.Apply(SignInState.SignedOut());

        store.Current.ShouldBeNull();
    }

    [Fact]
    public async Task Sem_sessao_nao_busca()
    {
        _shell.Apply(SignInState.SignedOut());
        using var store = NewStore();

        await store.RefreshAsync();

        store.View.ShouldBeNull();
        _connection.Connected.ShouldBeEmpty();
    }

    [Fact]
    public async Task Ja_carregado_nao_busca_de_novo_ao_abrir_a_tela()
    {
        _connection.Modpacks = [Modpack("Skyblock")];
        using var store = NewStore();
        await store.RefreshAsync();

        _connection.Modpacks = [];
        await store.EnsureLoadedAsync();

        store.Current!.Entries.ShouldHaveSingleItem();
    }

    private CatalogStore NewStore() => new(new LoadCatalog(_connection), _shell, _connection);

    private void LogIn() =>
        _shell.Apply(SignInState.SignedIn(new LauncherSessionDto
        {
            UserId = Guid.CreateVersion7(), DisplayName = "Jogador", MinecraftUuid = "0123456789abcdef0123456789abcdef"
        }));

    private static ModpackDto Modpack(string name) => new()
    {
        Id = Guid.CreateVersion7(),
        Slug = name.ToLowerInvariant(),
        Name = name,
        MinecraftVersion = "1.21.1",
        Loader = ModLoader.NeoForge
    };
}
