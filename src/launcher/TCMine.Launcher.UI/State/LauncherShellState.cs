namespace TCMine.Launcher.UI.State;

/// <summary>
///     Estado vivo do launcher, compartilhado por todas as telas.
///     Ocupa o lugar que num app WPF clássico seria o ViewModel da janela, com
///     uma diferença que importa: ele não é dono de nada. Reflete o que as
///     portas do Core reportam — configuração, sessão, conexão do hub — e avisa
///     quem estiver na tela. A lógica que merece teste continua em classes puras
///     no Core, como o ManifestDiffer.
///     Singleton de propósito: em Blazor Hybrid existe um circuito só, e o
///     estado precisa sobreviver à navegação entre páginas.
/// </summary>
public sealed class LauncherShellState
{
    public ConnectionState Connection { get; private set; } = ConnectionState.Disconnected;

    /// <summary>
    ///     Notifica as telas. Um evento só, sem granularidade por propriedade: o
    ///     custo de um re-render em Blazor é baixo, e uma trilha de eventos
    ///     específicos vira dívida na primeira propriedade nova.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    ///     Método em vez de setter público de propósito: mudar estado aqui
    ///     dispara redesenho de tela, e uma atribuição solta esconderia esse
    ///     efeito no meio de uma expressão.
    /// </summary>
    public void SetConnection(ConnectionState state)
    {
        if (Connection == state)
            return;

        Connection = state;
        Changed?.Invoke();
    }
}

/// <summary>
///     Ligação com o TCMine Server.
///     Não confundir com "o jogador está autenticado": dá para estar ligado ao
///     servidor e não ter sessão ainda, que é justamente o estado da tela de
///     login.
/// </summary>
public enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected
}
