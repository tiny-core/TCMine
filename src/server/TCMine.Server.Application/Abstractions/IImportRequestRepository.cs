using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Rastro das importações em curso.
///     Porta separada da IModpackRepository de propósito: não é parte do catálogo,
///     é estado operacional de curta vida — a linha nasce quando o admin pede e
///     morre quando a importação termina, de um jeito ou de outro.
/// </summary>
public interface IImportRequestRepository
{
    Task AddAsync(ImportRequest request, CancellationToken ct);

    Task UpdateAsync(ImportRequest request, CancellationToken ct);

    /// <summary>Idempotente: terminar duas vezes não pode explodir.</summary>
    Task RemoveAsync(Guid requestId, CancellationToken ct);

    /// <summary>
    ///     Tudo o que sobrou. No arranque, cada linha aqui é uma importação que o
    ///     processo anterior não terminou.
    /// </summary>
    Task<IReadOnlyList<ImportRequest>> ListAllAsync(CancellationToken ct);

    /// <summary>Já existe importação em curso para este pack?</summary>
    Task<bool> ExistsForAsync(ModFileOrigin origin, string projectId, CancellationToken ct);
}
