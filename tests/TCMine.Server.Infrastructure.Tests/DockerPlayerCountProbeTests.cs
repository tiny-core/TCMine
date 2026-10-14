using TCMine.Server.Infrastructure.Instances;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     A contagem de jogadores sai do <c>mc-monitor</c>, que escreve o erro no
///     mesmo fluxo do número quando o servidor não responde.
/// </summary>
public sealed class DockerPlayerCountProbeTests
{
    [Theory]
    [InlineData("0", 0)]
    [InlineData("3\r\n", 3)]
    [InlineData("  12 ", 12)]
    public void Numero_sozinho_e_a_contagem(string output, int expected) =>
        DockerPlayerCountProbe.ParseCount(output).ShouldBe(expected);

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("failed to ping localhost:25565 : dial tcp 127.0.0.1:25565: connect: connection refused")]
    [InlineData("-1")]
    public void Erro_ou_texto_nao_vira_contagem(string? output) =>
        DockerPlayerCountProbe.ParseCount(output).ShouldBeNull();

    [Fact]
    public void Pergunta_sem_RCON_e_so_pelo_numero()
    {
        // Voltar ao rcon-cli traria de volta o "RCON Client started / shutting
        // down" no console a cada quinze segundos.
        DockerPlayerCountProbe.Command.ShouldNotContain("rcon-cli");
        DockerPlayerCountProbe.Command.ShouldContain("--show-player-count");
    }
}
