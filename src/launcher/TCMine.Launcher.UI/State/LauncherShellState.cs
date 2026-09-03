using TCMine.Contracts.Identity;
using TCMine.Launcher.Core.Connectivity;
using TCMine.Launcher.Core.Identity;

namespace TCMine.Launcher.UI.State;

/// <summary>
///     Estado vivo do launcher, compartilhado por todas as telas.
///     Ocupa o lugar que num app WPF clássico seria o ViewModel da janela, com
///     uma diferença que importa: ele não é dono de nada. Reflete o que as
///     portas do Core reportam — pareamento, sessão, conexão do hub — e avisa
///     quem estiver na tela. A lógica que merece teste continua em classes puras
///     no Core, como o ServerPairing e o ManifestDiffer.
///     Singleton de propósito: em Blazor Hybrid existe um circuito só, e o
///     estado precisa sobreviver à navegação entre páginas.
/// </summary>
public sealed class LauncherShellState
{
    private bool _checking;

    public PairingState? Pairing { get; private set; }

    public SignInState? Account { get; private set; }

    /// <summary>
    ///     Ainda não sabemos nada — nem se há pareamento, nem se há sessão. É
    ///     diferente de "não pareado" e de "deslogado": a moldura espera aqui em
    ///     vez de piscar a tela errada antes de redirecionar.
    ///     Explícito, e não derivado de Pairing ser nulo, porque o arranque tem
    ///     dois passos: assim que o primeiro resolve, o derivado diria que
    ///     acabou.
    /// </summary>
    public bool IsStartingUp { get; private set; } = true;

    /// <summary>Existe servidor conhecido, mesmo que ele não esteja atendendo.</summary>
    public bool IsPaired => Pairing?.IsPaired is true;

    public ConnectionState Connection =>
        _checking ? ConnectionState.Connecting
        : Pairing?.IsOnline is true ? ConnectionState.Connected
        : ConnectionState.Disconnected;

    /// <summary>O servidor respondeu. Sem isto não há como emitir sessão.</summary>
    public bool IsOnline => Pairing?.IsOnline is true;

    public bool IsSignedIn => Account?.IsSignedIn is true;

    public LauncherSessionDto? Player => Account?.Session;

    /// <summary>
    ///     Uma letra para o círculo do avatar, enquanto não buscamos a cabeça do
    ///     jogador pelo UUID.
    /// </summary>
    public string AvatarInitial =>
        Player?.DisplayName is { Length: > 0 } nome ? nome[..1].ToUpperInvariant() : "?";

    /// <summary>Nome do servidor, quando pareado. Vai na barra de título.</summary>
    public string? ServerName => Pairing?.Server?.ServerName ?? Pairing?.Config?.DisplayName;

    /// <summary>
    ///     O que dizer quando há servidor mas a ligação falhou. Nulo quando não
    ///     há nada a explicar — inclusive no caso de nem haver pareamento, que a
    ///     tela de pareamento já cobre.
    /// </summary>
    public string? ConnectionNotice =>
        IsPaired && Pairing?.IsOnline is false ? Pairing.Message : null;

    /// <summary>
    ///     Notifica as telas. Um evento só, sem granularidade por propriedade: o
    ///     custo de um re-render em Blazor é baixo, e uma trilha de eventos
    ///     específicos vira dívida na primeira propriedade nova.
    /// </summary>
    public event Action? Changed;

    public void BeginCheck()
    {
        _checking = true;
        Changed?.Invoke();
    }

    public void Apply(PairingState state)
    {
        _checking = false;
        Pairing = state;
        Changed?.Invoke();
    }

    public void Apply(SignInState state)
    {
        Account = state;
        Changed?.Invoke();
    }

    /// <summary>Chamado quando o arranque termina, com ou sem sucesso.</summary>
    public void FinishStartup()
    {
        IsStartingUp = false;
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
