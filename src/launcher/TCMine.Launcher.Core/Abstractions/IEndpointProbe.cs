using TCMine.Launcher.Core.Modpacks;

namespace TCMine.Launcher.Core.Abstractions;

/// <summary>
///     Responde se um endereço aceita conexão AGORA, deste computador.
///     É o que deixa o launcher escolher sozinho por onde entrar num servidor,
///     em vez de perguntar ao jogador "você está em casa?" — uma pergunta que ele
///     não sabe responder em termos de rede, e que erraria no dia em que levasse
///     o portátil para outro lugar.
/// </summary>
public interface IEndpointProbe
{
    /// <summary>
    ///     Verdadeiro quando uma conexão TCP abre dentro do prazo da
    ///     implementação. Falso para recusa, prazo esgotado ou endereço que não
    ///     resolve — nunca lança por causa da rede.
    /// </summary>
    Task<bool> IsReachableAsync(ServerAddress address, CancellationToken ct);
}
