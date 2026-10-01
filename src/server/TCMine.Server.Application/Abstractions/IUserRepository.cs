using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Abstractions;

public interface IUserRepository
{
    /// <summary>Existe algum usuário? Falso significa instalação nova (setup inicial).</summary>
    Task<bool> AnyAsync(CancellationToken ct);

    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Busca pelo Object ID da Microsoft — a identidade de toda conta.</summary>
    Task<User?> GetByMicrosoftObjectIdAsync(string objectId, CancellationToken ct);

    /// <summary>
    ///     Busca pelo UUID da conta Minecraft (login do launcher). É por ele, e
    ///     não pelo nome de jogador, que reconhecemos quem voltou: o nome muda.
    /// </summary>
    Task<User?> GetByMinecraftUuidAsync(string uuid, CancellationToken ct);

    Task AddAsync(User user, CancellationToken ct);

    Task UpdateAsync(User user, CancellationToken ct);

    /// <summary>
    ///     Todos os usuários, para o seletor de "conceder acesso a" — convite de
    ///     servidor e editor de modpack. Poucas dezenas de contas numa instalação
    ///     típica; pagina no dia em que isso deixar de ser verdade.
    /// </summary>
    Task<IReadOnlyList<User>> ListAsync(CancellationToken ct);
}
