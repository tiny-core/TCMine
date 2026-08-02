using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Web.Security;

/// <summary>
///     ATENÇÃO: implementação temporária, apenas para desenvolvimento.
///     Trata todos como Owner de tudo. Serve para exercitar o Hub
///     enquanto a autenticação real (MSAL → validação de id_token → JWT
///     próprio) não existe.
///     O registro no DI é condicionado a ambiente de desenvolvimento, e a
///     aplicação recusa subir em produção com esta classe ativa — um stub de
///     autorização esquecido em produção é o tipo de falha que só aparece
///     quando alguém já entrou onde não devia.
/// </summary>
public sealed class DevelopmentUserScope : ICurrentUserScope
{
    private static readonly Guid DevUserId =
        Guid.Parse("00000000-0000-0000-0000-0000000000de");

    public Guid? UserId => DevUserId;

    public Guid OwnerId => DevUserId;

    public bool IsInstanceAdmin => true;

    public Task<ServerRoleDto?> GetRoleAsync(Guid gameServerId, CancellationToken ct) =>
        Task.FromResult<ServerRoleDto?>(ServerRoleDto.Owner);
}
