namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Quantos jogadores estão num servidor de jogo agora.
///     Separado do <see cref="IRconClient" /> de propósito: contar jogadores não
///     precisa de canal de controle. A contagem pelo RCON abria e fechava uma
///     conexão a cada 15 segundos, e o jogo registrava cada uma no console
///     ("RCON Client started / shutting down") — o admin via o RCON a cair e
///     voltar sem parar, e o console ficava ilegível.
/// </summary>
public interface IPlayerCountProbe
{
    /// <summary>
    ///     O número de jogadores online, ou nulo quando o servidor não respondeu
    ///     (ainda a subir, parado ou travado). Nunca lança por isso.
    /// </summary>
    Task<int?> GetOnlineAsync(Guid gameServerId, CancellationToken ct);
}
