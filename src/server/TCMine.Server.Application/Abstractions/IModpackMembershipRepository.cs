using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Um editor do modpack, com o nome de quem é. Mesma razão do
///     <see cref="ServerMemberView" />: mora aqui, não ao lado do caso de uso
///     que a devolve, porque a regra de arquitetura exige que toda classe no
///     namespace dos casos de uso consulte o papel do usuário, e um record de
///     leitura não consulta nada.
/// </summary>
public sealed record ModpackMemberView(
    Guid MembershipId,
    Guid UserId,
    string DisplayName,
    ModpackRoleDto Role);

public interface IModpackMembershipRepository
{
    Task AddAsync(ModpackMembership membership, CancellationToken ct);

    Task<ModpackMembership?> GetAsync(Guid userId, Guid modpackId, CancellationToken ct);

    /// <summary>Editores de um modpack, com o nome de quem são, para a tela.</summary>
    Task<IReadOnlyList<ModpackMemberView>> ListWithUsersAsync(Guid modpackId, CancellationToken ct);

    /// <summary>
    ///     Vínculos de um usuário, em todos os modpacks — a mesma pergunta que
    ///     <see cref="IMembershipRepository.ListByUserAsync" /> responde do lado
    ///     dos servidores, para a tela "Usuários" mostrar os dois juntos.
    /// </summary>
    Task<IReadOnlyList<ModpackMembership>> ListByUserAsync(Guid userId, CancellationToken ct);

    /// <summary>
    ///     O dono do modpack, para a assinatura "criado por" — informação que
    ///     qualquer um que vê o modpack enxerga, ao contrário da lista de
    ///     editores (essa sim só para quem gerencia acesso). Sem porta de caso
    ///     de uso de propósito: mostrar quem é o dono não pede autorização, e
    ///     páginas já injetam repositório direto para leituras deste tipo (ver
    ///     IServerRepository em ModpackDetailPage).
    /// </summary>
    Task<ModpackMemberView?> GetOwnerAsync(Guid modpackId, CancellationToken ct);

    Task RemoveAsync(Guid id, CancellationToken ct);
}
