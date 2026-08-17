using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Application.Tests.Fakes;

/// <summary>
///     Identidade do chamador nos testes de caso de uso.
///     O padrão é <see cref="ServerRoleDto.Owner" /> porque a esmagadora maioria
///     dos testes existe para exercitar a REGRA, não a permissão: obrigá-los a
///     declarar o papel só encheria de ruído. Quem testa autorização passa o
///     papel — ou <c>null</c>, que é como o escopo representa tanto "servidor não
///     existe" quanto "não tenho vínculo".
/// </summary>
internal sealed class FakeUserScope(ServerRoleDto? role = ServerRoleDto.Owner) : ICurrentUserScope
{
    public Guid? UserId { get; init; } = Guid.CreateVersion7();

    public Guid OwnerId => UserId ?? Guid.Empty;

    public bool IsInstanceAdmin { get; init; }

    public Task<ServerRoleDto?> GetRoleAsync(Guid gameServerId, CancellationToken ct) =>
        Task.FromResult(role);
}
