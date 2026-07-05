using TCMine_Domain.Entities;

namespace TCMine_Application.Abstractions;

/// <summary>
///     Acesso à linha única de settings de runtime (<see cref="ServerSettingEntity" />). Só persiste/
///     consulta a linha — a cifra dos segredos (Data Protection) e o cache ficam no serviço de aplicação.
/// </summary>
public interface IServerSettingsStore
{
    /// <summary>Lê a linha de settings, ou null se ainda não existe.</summary>
    /// <param name="tracking">true para rastrear a entidade (fluxo de escrita); false para leitura.</param>
    /// <param name="ct">Token de cançelamento</param>
    Task<ServerSettingEntity?> GetAsync(bool tracking, CancellationToken ct = default);

    /// <summary>Marca uma nova linha de settings para inserção.</summary>
    void Add(ServerSettingEntity row);

    /// <summary>Persiste as alterações pendentes.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}