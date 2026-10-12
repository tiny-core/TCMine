using TCMine.Contracts.Servers;
using TCMine.Server.Infrastructure.Docker;
using TCMine.Server.Infrastructure.Instances;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     Container de pé não é jogo pronto. O launcher só libera "Entrar" em
///     Running, então Running precisa querer dizer "aceita jogadores".
/// </summary>
public sealed class ContainerStatusTests
{
    private static ContainerState Rodando(string? saude) => new()
    {
        Status = "running",
        Running = true,
        Health = saude is null ? null : new ContainerHealth { Status = saude }
    };

    [Fact]
    public void Container_de_pe_com_o_jogo_ainda_carregando_esta_iniciando() =>
        DockerServerOrchestrator.StatusOf(Rodando("starting")).ShouldBe(GameServerStatus.Starting);

    [Fact]
    public void Jogo_que_respondeu_a_verificacao_esta_no_ar() =>
        DockerServerOrchestrator.StatusOf(Rodando("healthy")).ShouldBe(GameServerStatus.Running);

    [Fact]
    public void Jogo_que_parou_de_responder_continua_no_ar_para_o_admin_ver_o_console() =>
        // Esconder como "Iniciando" faria o admin esperar por algo que não vem.
        DockerServerOrchestrator.StatusOf(Rodando("unhealthy")).ShouldBe(GameServerStatus.Running);

    [Fact]
    public void Container_antigo_sem_verificacao_de_saude_vale_o_que_valia() =>
        DockerServerOrchestrator.StatusOf(Rodando(null)).ShouldBe(GameServerStatus.Running);

    [Theory]
    [InlineData("restarting", 1, GameServerStatus.Starting)]
    [InlineData("created", 0, GameServerStatus.Stopped)]
    [InlineData("exited", 137, GameServerStatus.Crashed)]
    [InlineData("exited", 0, GameServerStatus.Stopped)]
    public void Container_parado_segue_o_mapeamento_de_antes(string status, int exitCode, GameServerStatus esperado) =>
        DockerServerOrchestrator.StatusOf(new ContainerState { Status = status, ExitCode = exitCode })
            .ShouldBe(esperado);
}
