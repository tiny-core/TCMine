using TCMine.Server.Infrastructure.Docker;

namespace TCMine.Server.Infrastructure.Tests;

/// <summary>
///     O que o admin lê quando o Docker recusa. Antes era "Response status code
///     does not indicate success: 500" para tudo.
/// </summary>
public sealed class DockerErrorTests
{
    [Theory]
    [InlineData(
        "driver failed programming external connectivity on endpoint tcmine-x: Bind for 0.0.0.0:25565 failed: port is already allocated",
        "porta já está em uso por outro container")]
    [InlineData(
        "failed to listen on TCP socket: address already in use",
        "porta já está em uso por outro programa")]
    [InlineData("write /var/lib/docker/tmp/x: no space left on device", "disco está cheio")]
    [InlineData("manifest for itzg/minecraft-server:java99 not found: manifest unknown", "imagem não existe")]
    [InlineData("No such image: itzg/minecraft-server:java21", "não está nesta máquina")]
    [InlineData("toomanyrequests: You have reached your pull rate limit.", "limitou os downloads")]
    public void Causa_conhecida_vira_frase_que_o_admin_resolve(string dockerMessage, string expected)
    {
        var message = DockerError.Describe("iniciar o container", dockerMessage);

        message.ShouldStartWith("Docker não conseguiu iniciar o container: ");
        message.ShouldContain(expected);
        // O texto original fica: é o que se procura quando a frase não basta.
        message.ShouldContain(dockerMessage);
    }

    [Fact]
    public void Causa_desconhecida_passa_com_o_texto_do_Docker()
    {
        DockerError.Describe("criar o container", "invalid reference format")
            .ShouldBe("Docker não conseguiu criar o container: invalid reference format");
    }

    [Fact]
    public void Sem_detalhe_ainda_diz_o_que_estava_fazendo()
    {
        DockerError.Describe("parar o container", " ")
            .ShouldBe("Docker não conseguiu parar o container, e não disse por quê.");
    }

    [Theory]
    // Rotas comuns da Engine API.
    [InlineData("""{"message":"No such container: abc"}""", "No such container: abc")]
    // Linha de erro no meio do stream do pull.
    [InlineData("""{"errorDetail":{"message":"pull access denied"},"error":"pull access denied"}""",
        "pull access denied")]
    // Não é JSON (proxy no caminho): o texto serve.
    [InlineData("Bad Gateway", "Bad Gateway")]
    public void Mensagem_sai_do_corpo_nos_formatos_que_o_daemon_usa(string body, string expected) =>
        DockerError.Extract(body).ShouldBe(expected);

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    // Linha de progresso do pull: não é erro.
    [InlineData("""{"status":"Downloading","id":"abc"}""")]
    public void Corpo_sem_erro_nao_inventa_mensagem(string? body) =>
        DockerError.Extract(body).ShouldBeNull();
}
