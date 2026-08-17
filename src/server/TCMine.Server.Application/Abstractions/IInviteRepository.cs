using TCMine.Contracts.Servers;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Um membro do servidor, do jeito que a tela precisa: o vínculo mais o
///     nome de quem é.
/// </summary>
public sealed record ServerMemberView(
    Guid MembershipId,
    Guid UserId,
    string DisplayName,
    string? MinecraftUuid,
    ServerRoleDto Role,
    DateTimeOffset? LastSeenAt);

/// <summary>
///     Tudo o que a tela de acesso mostra de um servidor.
///     Mora aqui, e não ao lado do caso de uso que a devolve, porque a regra de
///     arquitetura exige que toda classe no namespace dos casos de uso consulte
///     o papel do usuário — e um record de leitura não consulta nada. Deixá-lo
///     lá obrigaria a afrouxar a regra que protege os casos de uso de verdade.
/// </summary>
public sealed record ServerAccessView(
    IReadOnlyList<ServerMemberView> Members,
    IReadOnlyList<Invite> PendingInvites);

public interface IInviteRepository
{
    Task AddAsync(Invite invite, CancellationToken ct);

    Task<Invite?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>
    ///     Busca pelo hash do código apresentado. É a única forma de encontrar um
    ///     convite pelo que o usuário digitou — o valor em claro não existe no
    ///     banco.
    /// </summary>
    Task<Invite?> GetByCodeHashAsync(string codeHash, CancellationToken ct);

    /// <summary>Convites de um servidor, mais recentes primeiro (ordem de Id).</summary>
    Task<IReadOnlyList<Invite>> ListByServerAsync(Guid gameServerId, CancellationToken ct);

    Task UpdateAsync(Invite invite, CancellationToken ct);
}

public interface IMembershipRepository
{
    Task AddAsync(Membership membership, CancellationToken ct);

    Task<Membership?> GetAsync(Guid userId, Guid gameServerId, CancellationToken ct);

    Task<IReadOnlyList<Membership>> ListByServerAsync(Guid gameServerId, CancellationToken ct);

    /// <summary>
    ///     Membros com o nome de quem são, para a tela.
    ///     Projeção no repositório e não duas consultas no caso de uso: o join
    ///     mora onde há banco, e buscar os usuários um a um depois seria N+1
    ///     numa tela que lista dezenas de pessoas.
    /// </summary>
    Task<IReadOnlyList<ServerMemberView>> ListWithUsersAsync(Guid gameServerId, CancellationToken ct);

    Task UpdateAsync(Membership membership, CancellationToken ct);

    Task RemoveAsync(Guid id, CancellationToken ct);
}
