using TCMine.Server.Infrastructure.Docker;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     Que eventos do Docker contam como "o estado deste servidor pode ter
///     mudado".
/// </summary>
public sealed class DockerContainerEventsTests
{
    private static readonly Guid Server = Guid.CreateVersion7();

    private static readonly Dictionary<string, string> Labels = new()
    {
        [DockerContainerEvents.ServerLabel] = Server.ToString(), ["name"] = $"tcmine-{Server}"
    };

    [Theory]
    [InlineData("start")]
    [InlineData("die")]
    [InlineData("stop")]
    [InlineData("restart")]
    [InlineData("destroy")]
    [InlineData("health_status: healthy")]
    [InlineData("health_status: unhealthy")]
    public void Mudancas_de_estado_apontam_o_servidor(string action)
    {
        DockerContainerEvents.ServerOf(action, Labels).ShouldBe(Server);
    }

    [Theory]
    [InlineData("exec_create: rcon-cli list")]
    [InlineData("exec_start: rcon-cli list")]
    [InlineData("exec_die")]
    [InlineData("attach")]
    public void Comandos_e_anexos_sao_ignorados(string action)
    {
        // O coletor de métricas faz um exec a cada quinze segundos por
        // servidor; tratá-los seria consultar o Docker sem parar à toa.
        DockerContainerEvents.ServerOf(action, Labels).ShouldBeNull();
    }

    [Fact]
    public void Container_que_nao_e_do_TCMine_e_ignorado()
    {
        DockerContainerEvents.ServerOf("die", new Dictionary<string, string> { ["name"] = "outro" })
            .ShouldBeNull();

        DockerContainerEvents.ServerOf("die", null).ShouldBeNull();
    }

    [Fact]
    public void Label_que_nao_e_um_id_e_ignorado()
    {
        DockerContainerEvents.ServerOf(
                "die", new Dictionary<string, string> { [DockerContainerEvents.ServerLabel] = "lixo" })
            .ShouldBeNull();
    }
}
