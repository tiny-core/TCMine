using TCMine.Contracts.Servers;

namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Quem está fazendo a requisição.
///     Toda query sobre entidade IOwnedEntity passa por aqui. Hoje, com dono
///     único, o filtro por OwnerId é efetivamente no-op — mas a chamada já
///     existe, e no dia em que houver dois donos nada precisa ser reescrito.
/// </summary>
public interface ICurrentUserScope
{
    /// <summary>Nulo em contexto anônimo (handshake, download público).</summary>
    Guid? UserId { get; }

    Guid OwnerId { get; }

    bool IsInstanceAdmin { get; }

    /// <summary>
    ///     Papel neste servidor. Nulo significa sem vínculo nenhum.
    ///     Assíncrono de propósito: vai ao banco. Resistir à tentação de cachear
    ///     para sempre — se o admin rebaixa alguém, a mudança precisa valer na
    ///     próxima chamada, não na próxima sessão.
    /// </summary>
    Task<ServerRoleDto?> GetRoleAsync(Guid gameServerId, CancellationToken ct);
}
