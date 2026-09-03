using TCMine.Contracts;
using TCMine.Contracts.Handshake;

namespace TCMine.Launcher.Core.Connectivity;

/// <summary>
///     A que servidor este launcher pertence, e se dá para falar com ele agora.
///     São duas perguntas diferentes de propósito: um servidor fora do ar não
///     desfaz o pareamento. Tratar as duas como uma só mandaria o jogador
///     redigitar o endereço toda vez que a máquina dele ficasse sem rede.
/// </summary>
public sealed record PairingState
{
    public required PairingStatus Status { get; init; }

    /// <summary>Configuração conhecida. Sobrevive a uma falha de ligação.</summary>
    public LauncherConfig? Config { get; init; }

    /// <summary>O que o servidor respondeu no handshake, quando respondeu.</summary>
    public HandshakeResponse? Server { get; init; }

    /// <summary>Mensagem pronta para a tela. Nula quando não há nada a dizer.</summary>
    public string? Message { get; init; }

    /// <summary>Existe configuração — mesmo que o servidor esteja inacessível.</summary>
    public bool IsPaired => Config is not null;

    public bool IsOnline => Status is PairingStatus.Paired;

    public static PairingState NotPaired() => new() { Status = PairingStatus.NotPaired };

    public static PairingState Paired(LauncherConfig config, HandshakeResponse server) =>
        new() { Status = PairingStatus.Paired, Config = config, Server = server };

    public static PairingState Rejected(string message) =>
        new() { Status = PairingStatus.Invalid, Message = message };

    /// <summary>
    ///     Traduz a falha do handshake. O <paramref name="config" /> vem junto
    ///     quando já havia pareamento, porque perdê-lo aqui apagaria o endereço
    ///     por causa de uma queda de rede.
    /// </summary>
    public static PairingState FromHandshake(HandshakeResult result, LauncherConfig? config) => new()
    {
        Status = result.Outcome switch
        {
            HandshakeOutcome.Ok => PairingStatus.Paired,
            HandshakeOutcome.Unreachable => PairingStatus.Unreachable,
            HandshakeOutcome.LauncherTooOld or HandshakeOutcome.LauncherTooNew => PairingStatus.Incompatible,
            _ => PairingStatus.Invalid
        },
        Config = config,
        Server = result.Response,
        Message = result.Message
    };
}

public enum PairingStatus
{
    /// <summary>Nunca houve pareamento: não há tcmine.json legível.</summary>
    NotPaired,

    /// <summary>Pareado e o servidor respondeu.</summary>
    Paired,

    /// <summary>O endereço é conhecido, mas ninguém atendeu.</summary>
    Unreachable,

    /// <summary>
    ///     Atendeu, mas os protocolos não se cruzam. Distinto de
    ///     <see cref="Unreachable" /> porque tentar de novo não resolve — alguém
    ///     precisa atualizar alguma coisa.
    /// </summary>
    Incompatible,

    /// <summary>Endereço malformado, inseguro, ou resposta que não é do TCMine.</summary>
    Invalid
}
