using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;
using TCMine.Launcher.Core.Modpacks;
using TCMine.Launcher.Core.Sync;

namespace TCMine.Launcher.Core.Tests.Modpacks;

/// <summary>
///     O botão de cada servidor na aba Servidores. Errar aqui é oferecer
///     "Jogar" a quem não tem a pasta, ou "Instalar" a quem já a tem.
/// </summary>
public class ServerEntryTests
{
    private static readonly Guid Pack = Guid.CreateVersion7();
    private static readonly Guid Version = Guid.CreateVersion7();

    [Fact]
    public void Sem_acesso_pede_acesso_mesmo_com_a_pasta_no_disco() =>
        ServerEntry.Of(Servidor(ServerAccessState.None), Manifesto(Pack, Version))
            .ShouldBe(ServerEntryAction.RequestAccess);

    [Fact]
    public void Pedido_feito_aguarda() =>
        ServerEntry.Of(Servidor(ServerAccessState.Pending), null).ShouldBe(ServerEntryAction.Pending);

    [Fact]
    public void Com_acesso_e_sem_pasta_instala() =>
        ServerEntry.Of(Servidor(), null).ShouldBe(ServerEntryAction.Install);

    [Fact]
    public void Pasta_na_versao_do_servidor_joga() =>
        ServerEntry.Of(Servidor(), Manifesto(Pack, Version)).ShouldBe(ServerEntryAction.Play);

    [Fact]
    public void Pasta_noutra_versao_atualiza_e_joga() =>
        ServerEntry.Of(Servidor(), Manifesto(Pack, Guid.CreateVersion7()))
            .ShouldBe(ServerEntryAction.UpdateAndPlay);

    [Fact]
    public void Pasta_de_outro_pack_conta_como_nenhuma()
    {
        // O servidor trocou de pack: a pasta antiga volta às pessoais e o
        // servidor ganha outra — o botão tem de dizer "Instalar".
        ServerEntry.Of(Servidor(), Manifesto(Guid.CreateVersion7(), Version))
            .ShouldBe(ServerEntryAction.Install);
    }

    [Theory]
    [InlineData(GameServerStatus.Running, false)]
    [InlineData(GameServerStatus.Starting, true)]
    [InlineData(GameServerStatus.Stopped, true)]
    [InlineData(GameServerStatus.Crashed, true)]
    public void So_entra_com_o_servidor_no_ar(GameServerStatus status, bool blocked) =>
        (ServerEntry.Unavailable(Servidor() with { Status = status }) is not null).ShouldBe(blocked);

    private static GameServerDto Servidor(ServerAccessState access = ServerAccessState.Granted) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = "Survival",
        ModpackId = Pack,
        ModpackVersionId = Version,
        Status = GameServerStatus.Running,
        Role = ServerRoleDto.Member,
        AccessState = access
    };

    private static InstanceManifest Manifesto(Guid pack, Guid version) => new()
    {
        Schema = 2,
        ModpackId = pack,
        ModpackVersionId = version,
        ModpackName = "Pack",
        Version = "1.0.0",
        InstalledAt = DateTimeOffset.UtcNow,
        ManagedFiles = new Dictionary<string, string>(),
        Loader = ModLoader.NeoForge,
        MinecraftVersion = "1.21.1"
    };
}
