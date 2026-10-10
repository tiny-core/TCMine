using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Settings;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Web.Mapping;

namespace TCMine.Server.Web.Tests.Mapping;

/// <summary>
///     O endereço que o launcher recebe.
///     É a borda onde o defeito aparecia: o launcher passa este texto ao jogo
///     como está, então o que sai daqui é, letra por letra, onde o botão Entrar
///     vai bater.
/// </summary>
public sealed class ServerMappingsTests
{
    private static readonly IReadOnlyDictionary<Guid, string> NoLabels = new Dictionary<Guid, string>();

    [Fact]
    public void Servidor_fora_da_porta_padrao_sai_com_a_porta()
    {
        // A regressão: endereço sem ":porta", servidor na 25566. O launcher
        // recebia "94.63.98.255" e o jogo tentava a 25565.
        var dto = Granted(Servidor("94.63.98.255", 25566)).ToDto(new NoPlayers(), NoLabels, Address(null));

        dto.ConnectAddress.ShouldBe("94.63.98.255:25566");
    }

    [Fact]
    public void Endereco_automatico_usa_o_host_publico()
    {
        var dto = Granted(Servidor("", 25566)).ToDto(new NoPlayers(), NoLabels, Address("jogar.exemplo.com"));

        dto.ConnectAddress.ShouldBe("jogar.exemplo.com:25566");
    }

    [Fact]
    public void Automatico_sem_host_publico_sai_nulo_e_nao_vazio()
    {
        // Nulo é o que o launcher já entende como "não há por onde entrar".
        var dto = Granted(Servidor("", 25566)).ToDto(new NoPlayers(), NoLabels, Address(null));

        dto.ConnectAddress.ShouldBeNull();
    }

    [Fact]
    public void Sem_acesso_concedido_o_endereco_nao_sai()
    {
        var pending = new AccessibleServer(
            Servidor("94.63.98.255", 25566), ServerRoleDto.Member, ServerAccessState.Pending);

        pending.ToDto(new NoPlayers(), NoLabels, Address("jogar.exemplo.com")).ConnectAddress.ShouldBeNull();
    }

    [Fact]
    public void Servidor_com_subdominio_sai_pelo_nome_sem_porta()
    {
        // O jogo só consulta o SRV quando o endereço vem sem porta.
        var server = Servidor("", 25566);
        server.Subdomain = "sobrevivencia";

        var dto = Granted(server).ToDto(
            new NoPlayers(), NoLabels, new AddressSettings("1.2.3.4", null, "exemplo.com"));

        dto.ConnectAddress.ShouldBe("sobrevivencia.exemplo.com");
    }

    private static AddressSettings Address(string? publicHost) => new(publicHost, null, null);

    private static AccessibleServer Granted(GameServer server) =>
        new(server, ServerRoleDto.Member, ServerAccessState.Granted);

    private static GameServer Servidor(string address, int port) => new()
    {
        Name = "test-server",
        ModpackId = Guid.CreateVersion7(),
        ModpackVersionId = Guid.CreateVersion7(),
        ConnectAddress = address,
        GamePort = port,
        RconSecret = "segredo"
    };

    private sealed class NoPlayers : IPlayerCountSource
    {
        public int? TryGet(Guid gameServerId) => null;

        public int? PeakToday(Guid gameServerId) => null;
    }
}
