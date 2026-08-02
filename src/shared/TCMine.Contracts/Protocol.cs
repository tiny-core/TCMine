namespace TCMine.Contracts;

/// <summary>
///     Versão do protocolo de comunicação entre server e launcher.
///     Só incrementa em MUDANÇA QUEBRADA de contrato. Alteração de UI, campo
///     opcional novo ou correção de bug NÃO mexem aqui — é justamente isso que
///     permite publicar launcher 1.6.0, 1.7.0, 1.8.0 sem exigir release do server.
/// </summary>
public class Protocol
{
    /// <summary>Protocolo que esta build fala.</summary>
    public const int Current = 1;

    /// <summary>
    ///     Protocolo mais antigo ainda aceito. Manter N e N-1 dá ao admin uma
    ///     janela para atualizar sem derrubar os jogadores dele.
    /// </summary>
    public const int MinimumSupported = 1;

    /// <summary>
    ///     Rota do handshake. CONGELADA PARA SEMPRE: é o único endpoint que nunca
    ///     pode mudar de formato, porque é por ele que os dois lados descobrem se
    ///     conseguem conversar. O resto vive sob /api/v{n}/.
    /// </summary>
    public const string HandshakeRoute = "/api/handshake";

    /// <summary>
    ///     Há interseção entre o intervalo do outro lado e o nosso?
    /// </summary>
    public static bool IsCompatible(int otherMin, int otherMax) => otherMax >= MinimumSupported && otherMin <= Current;
}
