namespace TCMine.Launcher.Core.Connectivity;

/// <summary>
///     Como está o canal com o servidor AGORA.
///     Existe porque o rodapé dizia "Ligado" com base no handshake do arranque —
///     uma foto do passado. O servidor podia ter caído há uma hora e a interface
///     continuava verde.
///     São quatro estados, e não um booleano, porque "caiu e estou a tentar" e
///     "caiu e não há nada a correr" pedem coisas diferentes do jogador: no
///     primeiro basta esperar.
/// </summary>
public enum LinkStatus
{
    /// <summary>Sem canal, e ninguém a tentar abri-lo neste instante.</summary>
    Down,

    /// <summary>A abrir o canal pela primeira vez (ou depois de uma queda definitiva).</summary>
    Connecting,

    Up,

    /// <summary>O canal caiu e está a ser reaberto sozinho.</summary>
    Reconnecting
}
