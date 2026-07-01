using TCMine_Application.Contracts;

namespace TCMine_Application.Launcher;

/// <summary>Porta de leitura do catálogo de modpacks do servidor (consumida pelo launcher).</summary>
public interface IModpackCatalog
{
    Task<IReadOnlyList<ModpackSummaryDto>> GetModpacksAsync(CancellationToken ct = default);
    Task<ModpackManifestDto?> GetManifestAsync(Guid modpackId, CancellationToken ct = default);

    /// <summary>O servidor está alcançável? (indicador da barra de estado).</summary>
    Task<bool> PingAsync(CancellationToken ct = default);
}
