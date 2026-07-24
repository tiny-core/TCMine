using TCMine.Contracts.Handshake;
using TCMine.Launcher.Core.Connectivity;

namespace TCMine.Launcher.Core.Tests.Connectivity;

public class HandshakeResultTests
{
    private static HandshakeResponse Response(params string[] capabilities)
    {
        return new HandshakeResponse
        {
            ProtocolMin = 1,
            ProtocolMax = 1,
            ServerVersion = "1.0.0",
            ServerName = "Teste",
            LauncherChannel = "win-x64-p1",
            LauncherFeedUrl = new Uri("https://exemplo.com/updates"),
            Capabilities = capabilities,
            AzureClientId = "abc"
        };
    }

    [Fact]
    public void Capability_presente_e_reconhecida()
    {
        var result = new HandshakeResult(HandshakeOutcome.Ok, Response("console.stream"), null);

        result.HasCapability("console.stream").ShouldBeTrue();
    }

    [Fact]
    public void Capability_ausente_faz_a_ui_esconder_o_recurso()
    {
        var result = new HandshakeResult(HandshakeOutcome.Ok, Response(), null);

        result.HasCapability("backup.schedule").ShouldBeFalse();
    }

    [Fact]
    public void Sem_resposta_nenhuma_capability_existe()
    {
        // Caso de servidor inalcançável: a UI não deve oferecer nada.
        var result = new HandshakeResult(HandshakeOutcome.Unreachable, null, "sem rede");

        result.HasCapability("console.stream").ShouldBeFalse();
    }
}