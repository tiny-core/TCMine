using System.Text.Json;
using TCMine.Contracts;
using TCMine.Contracts.Handshake;
using TCMine.Contracts.Serialization;

namespace TCMine.Launcher.Core.Tests.Connectivity;

/// <summary>
///     O launcher consegue LER o que o servidor realmente escreve.
///     Este é o único contrato congelado do sistema, e era o único sem teste do
///     lado da leitura: os dois lados usam o mesmo tipo, então tudo compila e
///     tudo parece certo até a resposta de verdade chegar na máquina de alguém.
///     O JSON abaixo é uma cópia literal de uma resposta do servidor, não uma
///     serialização feita aqui — serializar e desserializar com o mesmo contexto
///     passa mesmo quando o formato do outro lado é outro.
/// </summary>
public class HandshakeWireFormatTests
{
    private const string RespostaReal =
        """
        {"protocolMin":1,"protocolMax":1,"serverVersion":"1.0.0","serverName":"TCMine Server",
        "launcherChannel":"win-x64-p1","launcherFeedUrl":"https://localhost:7001/updates/launcher/win-x64-p1/",
        "minLauncherVersion":null,"updatesFrozen":false,
        "capabilities":["console.commands","console.stream","modpack.manual-upload"],
        "azureClientId":"demo-client-id"}
        """;

    [Fact]
    public void A_resposta_do_servidor_e_desserializavel_pelo_launcher()
    {
        var resposta = JsonSerializer.Deserialize(
            RespostaReal, TcMineJsonContext.Default.HandshakeResponse);

        resposta.ShouldNotBeNull();
        resposta.ServerName.ShouldBe("TCMine Server");
        resposta.ProtocolMin.ShouldBe(1);
        resposta.ProtocolMax.ShouldBe(1);
        resposta.LauncherChannel.ShouldBe("win-x64-p1");
        resposta.AzureClientId.ShouldBe("demo-client-id");
        resposta.Capabilities.ShouldContain("console.stream");
        resposta.MinLauncherVersion.ShouldBeNull();
        resposta.UpdatesFrozen.ShouldBeFalse();
    }

    [Fact]
    public void O_tcmine_json_sobrevive_a_uma_ida_e_volta()
    {
        // O outro consumidor do mesmo contexto. Quebrou junto e pelo mesmo
        // motivo: com o resolver nulo, gravar a configuração estourava — o
        // launcher pareava e esquecia o servidor no fecho.
        var original = new LauncherConfig
        {
            Schema = 1,
            ServerUrl = new Uri("https://modpacks.exemplo/"),
            AzureClientId = "client",
            DisplayName = "Servidor de Teste"
        };

        var json = JsonSerializer.Serialize(original, TcMineJsonContext.Default.LauncherConfig);
        var lido = JsonSerializer.Deserialize(json, TcMineJsonContext.Default.LauncherConfig);

        lido.ShouldBe(original);
    }
}
