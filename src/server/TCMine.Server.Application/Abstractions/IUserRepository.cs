using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Abstractions;

public interface IUserRepository
{
    /// <summary>Existe algum usuário? Falso significa instalação nova (setup inicial).</summary>
    Task<bool> AnyAsync(CancellationToken ct);

    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>Busca por e-mail (login local). Comparação sem distinção de caixa.</summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);

    /// <summary>Busca pelo Object ID da Microsoft (login federado).</summary>
    Task<User?> GetByMicrosoftObjectIdAsync(string objectId, CancellationToken ct);

    /// <summary>
    ///     Busca pelo UUID da conta Minecraft (login do launcher). É por ele, e
    ///     não pelo nome de jogador, que reconhecemos quem voltou: o nome muda.
    /// </summary>
    Task<User?> GetByMinecraftUuidAsync(string uuid, CancellationToken ct);

    Task AddAsync(User user, CancellationToken ct);

    Task UpdateAsync(User user, CancellationToken ct);
}
