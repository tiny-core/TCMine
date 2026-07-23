using TCMine.Contracts.Handshake;

namespace TCMine.Launcher.Core.Connectivity;

/// <summary>
///     Primeira coisa que acontece ao conectar, antes de qualquer outra chamada.
///     Sem interseção de protocolo, a interface mostra uma mensagem clara
///     dizendo o que fazer — nunca uma exceção de desserialização, que é o que
///     aconteceria se fôssemos direto para a API.
/// </summary>
public interface IHandshakeClient
{
    Task<HandshakeResult> PerformAsync(Uri serverUrl, CancellationToken ct);
}

public sealed record HandshakeResult(
    HandshakeOutcome Outcome,
    HandshakeResponse? Response,
    string? Message)
{
    /// <summary>
    ///     Este servidor sabe fazer tal coisa?
    ///     É assim que a UI decide mostrar ou esconder um botão. Comparar versão
    ///     no lugar disso impediria publicar uma funcionalidade no launcher antes
    ///     do servidor.
    /// </summary>
    public bool HasCapability(string capability)
    {
        return Response?.Capabilities.Contains(capability) is true;
    }
}