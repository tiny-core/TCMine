using TCMine.Contracts;
using TCMine.Contracts.Hubs;
using TCMine.Contracts.Identity;
using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Connectivity;
using TCMine.Launcher.Core.Identity;
using TCMine.Launcher.Core.Tests.Fakes;
using TCMine.Launcher.UI.State;

namespace TCMine.Launcher.Core.Tests.UI;

public sealed class NewsStoreTests : IAsyncDisposable
{
    private static readonly Guid Pack = Guid.CreateVersion7();

    private readonly FakeServerConnection _connection = new();
    private readonly LauncherShellState _shell = new();

    public NewsStoreTests()
    {
        _shell.Apply(new PairingState
        {
            Status = PairingStatus.Paired,
            Config = new LauncherConfig
            {
                Schema = 1, ServerUrl = new Uri("https://modpacks.exemplo/"), AzureClientId = "client"
            }
        });

        _shell.Apply(SignInState.SignedIn(new LauncherSessionDto
        {
            UserId = Guid.CreateVersion7(), DisplayName = "Jogador", MinecraftUuid = "0123456789abcdef0123456789abcdef"
        }));
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();

    [Fact]
    public async Task Guarda_as_novidades_do_pack_pedido()
    {
        _connection.News[Pack] = [Post("Versão 1.2")];
        using var store = new NewsStore(_connection, _shell);

        await store.RefreshAsync(Pack);

        store.For(Pack).ShouldHaveSingleItem().Title.ShouldBe("Versão 1.2");
        store.For(Guid.CreateVersion7()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Falha_ao_buscar_e_silenciosa_e_mantem_o_que_havia()
    {
        _connection.News[Pack] = [Post("Versão 1.2")];
        using var store = new NewsStore(_connection, _shell);
        await store.RefreshAsync(Pack);

        _connection.Throws = new InvalidOperationException("canal em baixo");
        await store.RefreshAsync(Pack);

        store.For(Pack).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Aviso_do_servidor_rebusca_as_novidades_do_pack()
    {
        _connection.News[Pack] = [Post("Versão 1.2")];
        using var store = new NewsStore(_connection, _shell);
        await store.RefreshAsync(Pack);

        var avisos = 0;
        store.Changed += () => avisos++;

        _connection.News[Pack] = [Post("Versão 1.3"), Post("Versão 1.2")];
        _connection.RaiseInvalidated(ResourceKind.News, Pack);

        // O fake responde na hora, então a rebusca já terminou aqui.
        avisos.ShouldBe(1);
        store.For(Pack).Count.ShouldBe(2);
    }

    [Fact]
    public void Aviso_de_um_pack_que_ninguem_pediu_e_ignorado()
    {
        using var store = new NewsStore(_connection, _shell);

        _connection.RaiseInvalidated(ResourceKind.News, Pack);

        store.For(Pack).ShouldBeEmpty();
    }

    [Fact]
    public async Task Sair_da_conta_esquece_as_novidades()
    {
        _connection.News[Pack] = [Post("Versão 1.2")];
        using var store = new NewsStore(_connection, _shell);
        await store.RefreshAsync(Pack);

        _shell.Apply(SignInState.SignedOut());

        store.For(Pack).ShouldBeEmpty();
    }

    private static ModpackNewsDto Post(string title) => new()
    {
        Id = Guid.CreateVersion7(), Title = title, Body = "", PostedAt = DateTimeOffset.UtcNow
    };
}
