using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Um pedido de acesso, do jeito que a tela do admin precisa: quem pediu,
///     para qual servidor, e quando — sem join nenhum do lado do caso de uso.
/// </summary>
public sealed record AccessRequestView(
    Guid Id,
    Guid UserId,
    string DisplayName,
    Guid GameServerId,
    string ServerName,
    DateTimeOffset RequestedAt);

public interface IAccessRequestRepository
{
    Task AddAsync(AccessRequest request, CancellationToken ct);

    Task<AccessRequest?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>
    ///     O pedido PENDENTE deste jogador para este servidor, se houver. É por
    ///     ele que <c>RequestServerAccess</c> decide entre criar um novo e
    ///     devolver o que já existe — um segundo clique não pode abrir um
    ///     segundo pedido.
    /// </summary>
    Task<AccessRequest?> GetPendingAsync(Guid userId, Guid gameServerId, CancellationToken ct);

    /// <summary>
    ///     Para marcar o estado de cada servidor na lista do jogador (Pending
    ///     vs. nada) sem uma consulta por servidor.
    /// </summary>
    Task<IReadOnlyList<AccessRequest>> ListPendingByUserAsync(Guid userId, CancellationToken ct);

    /// <summary>
    ///     Pedidos pendentes dos servidores informados, com nome de quem pediu e
    ///     do servidor — é a tela "Pedidos" do admin. Quem decide QUAIS
    ///     servidores entram aqui é o caso de uso (pelo papel de quem está
    ///     pedindo), não o repositório.
    /// </summary>
    Task<IReadOnlyList<AccessRequestView>> ListPendingForServersAsync(
        IReadOnlyList<Guid> gameServerIds, CancellationToken ct);

    Task UpdateAsync(AccessRequest request, CancellationToken ct);
}
