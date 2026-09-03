using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.Core.Tests.Fakes;

namespace TCMine.Launcher.Core.Tests.Modpacks;

/// <summary>
///     O catálogo que o jogador vê.
///     São duas listas do servidor — modpacks e servidores — e a tela precisa
///     delas casadas. O que estes testes trancam é o casamento e o que fazer
///     quando ele não bate: um servidor apontando para um modpack que já não
///     está no catálogo é caso real, e não pode virar um card sem nome.
/// </summary>
public class LoadCatalogTests
{
    private static readonly Uri Servidor = new("https://servidor.exemplo/");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void Cada_modpack_recebe_os_servidores_que_o_usam()
    {
        var a = Modpack("Skyblock");
        var b = Modpack("Tecnologia");

        var entradas = LoadCatalog.Join(
            [a, b],
            [Server(a.Id, "survival"), Server(a.Id, "creative"), Server(b.Id, "tech")]);

        entradas.Single(e => e.Modpack.Id == a.Id).Servers.Count.ShouldBe(2);
        entradas.Single(e => e.Modpack.Id == b.Id).Servers.Count.ShouldBe(1);
    }

    [Fact]
    public void Modpack_sem_servidor_continua_no_catalogo()
    {
        // Ele se instala e se joga sozinho. Sumir da lista esconderia metade do
        // catálogo de quem ainda não foi convidado para servidor nenhum.
        var pack = Modpack("Solo");

        var entradas = LoadCatalog.Join([pack], []);

        entradas.ShouldHaveSingleItem().HasServer.ShouldBeFalse();
    }

    [Fact]
    public void Servidor_de_modpack_fora_do_catalogo_e_ignorado()
    {
        // Acontece quando o jogador tem acesso a um servidor de um pack que foi
        // removido. Inventar uma entrada mostraria um card sem nome nem versão.
        var pack = Modpack("Skyblock");

        var entradas = LoadCatalog.Join([pack], [Server(Guid.CreateVersion7(), "orfao")]);

        entradas.ShouldHaveSingleItem().HasServer.ShouldBeFalse();
    }

    [Fact]
    public void A_ordem_e_alfabetica_e_nao_depende_de_servidor_no_ar()
    {
        // A posição de um card não pode mudar sozinha porque um servidor subiu
        // ou caiu: o jogador perderia de vista o que estava prestes a clicar.
        var entradas = LoadCatalog.Join(
            [Modpack("Zumbis"), Modpack("aventura"), Modpack("Magia")],
            []);

        entradas.Select(e => e.Modpack.Name).ShouldBe(["aventura", "Magia", "Zumbis"]);
    }

    [Fact]
    public void Contagem_de_jogadores_ignora_servidor_parado()
    {
        // Um servidor parado reporta zero, e somar isso faria "0 online" parecer
        // um servidor vazio em vez de desligado.
        var pack = Modpack("Skyblock");

        var entradas = LoadCatalog.Join(
            [pack],
            [
                Server(pack.Id, "no-ar", GameServerStatus.Running, online: 7),
                Server(pack.Id, "parado", GameServerStatus.Stopped, online: 0)
            ]);

        var entrada = entradas.ShouldHaveSingleItem();
        entrada.IsAnyServerRunning.ShouldBeTrue();
        entrada.OnlinePlayers.ShouldBe(7);
    }

    [Fact]
    public async Task O_canal_e_aberto_sob_demanda()
    {
        // Ligar no login deixaria a tela sem saída se a conexão caísse depois: o
        // catálogo falharia para sempre até o jogador sair e entrar de novo.
        var canal = new FakeServerConnection();

        await new LoadCatalog(canal).HandleAsync(Servidor, Ct);

        canal.Connected.ShouldBe([Servidor]);
    }

    [Fact]
    public async Task Canal_ja_aberto_nao_e_reaberto()
    {
        var canal = new FakeServerConnection { IsConnected = true };

        await new LoadCatalog(canal).HandleAsync(Servidor, Ct);

        canal.Connected.ShouldBeEmpty();
    }

    [Fact]
    public async Task Falha_no_canal_vira_mensagem_e_nao_excecao()
    {
        // Uma exceção subindo daqui deixaria o spinner girando para sempre.
        var canal = new FakeServerConnection { Throws = new InvalidOperationException("canal fechado") };

        var visao = await new LoadCatalog(canal).HandleAsync(Servidor, Ct);

        visao.Failed.ShouldBeTrue();
        visao.Error!.ShouldContain("canal fechado");
        visao.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task Catalogo_vazio_e_resposta_e_nao_falha()
    {
        // A tela mostra textos diferentes: "ninguém publicou nada" pede espera,
        // "não deu para carregar" pede nova tentativa.
        var visao = await new LoadCatalog(new FakeServerConnection()).HandleAsync(Servidor, Ct);

        visao.Failed.ShouldBeFalse();
        visao.IsEmpty.ShouldBeTrue();
    }

    // ---------- apoio ----------

    private static ModpackDto Modpack(string nome) => new()
    {
        Id = Guid.CreateVersion7(),
        Slug = nome.ToLowerInvariant(),
        Name = nome,
        MinecraftVersion = "1.21.1",
        Loader = ModLoader.NeoForge
    };

    private static GameServerDto Server(
        Guid modpackId,
        string nome,
        GameServerStatus status = GameServerStatus.Running,
        int online = 0) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = nome,
        ModpackId = modpackId,
        ModpackVersionId = Guid.CreateVersion7(),
        ConnectAddress = "jogo.exemplo:25565",
        Status = status,
        OnlinePlayers = online,
        MaxPlayers = 20,
        Role = ServerRoleDto.Member
    };
}
