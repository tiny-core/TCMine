namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Guarda e devolve instantâneos do mundo.
///     Um arquivo por snapshot (zip), não blobs por arquivo. A dedup por conteúdo
///     seria melhor em disco — entre dois backups a maioria das regiões não muda
///     —, mas produziria dezenas de milhares de blobs por mundo e uma restauração
///     que depende do banco estar íntegro. Um .zip o admin copia para outra
///     máquina, abre no explorador e restaura na unha se tudo mais falhar. Num
///     painel de homelab isso vale mais que o espaço economizado.
/// </summary>
public interface IWorldBackupStore
{
    /// <summary>
    ///     Compacta o mundo da instância. Devolve o nome do arquivo e o tamanho,
    ///     ou null se não havia mundo a salvar.
    ///     <paramref name="onProgress" /> recebe (arquivos processados, total).
    /// </summary>
    Task<StoredWorldBackup?> CreateAsync(
        Guid gameServerId, Action<int, int>? onProgress, CancellationToken ct);

    /// <summary>
    ///     Repõe o mundo a partir do snapshot, substituindo o que estiver lá.
    ///     Devolve false se o arquivo já não existe no disco.
    /// </summary>
    Task<bool> RestoreAsync(
        Guid gameServerId, string fileName, Action<int, int>? onProgress, CancellationToken ct);

    Task<bool> DeleteAsync(Guid gameServerId, string fileName, CancellationToken ct);

    /// <summary>Abre o arquivo para download. Null se não existe.</summary>
    Task<Stream?> OpenAsync(Guid gameServerId, string fileName, CancellationToken ct);
}

public sealed record StoredWorldBackup(string FileName, long SizeBytes);
