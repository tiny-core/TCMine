using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Application.Abstractions;

public interface IPlayerSettingsRepository
{
    Task<PlayerModpackSettings?> GetAsync(Guid userId, Guid modpackId, CancellationToken ct);

    [NotVisibleToLauncher("Só o próprio jogador grava, e o launcher lê antes de abrir o jogo — não há tela a avisar.")]
    Task AddAsync(PlayerModpackSettings settings, CancellationToken ct);

    [NotVisibleToLauncher("Só o próprio jogador grava, e o launcher lê antes de abrir o jogo — não há tela a avisar.")]
    Task UpdateAsync(PlayerModpackSettings settings, CancellationToken ct);
}
