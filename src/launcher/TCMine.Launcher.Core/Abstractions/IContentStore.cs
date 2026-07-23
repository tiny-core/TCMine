namespace TCMine.Launcher.Core.Abstractions;

/// <summary>
///     Store local endereçado por conteúdo.
///     No Windows, materializar usa hardlink NTFS: dez modpacks com o mesmo mod
///     ocupam um arquivo só no disco, e criar uma instância é instantâneo em vez
///     de copiar centenas de megabytes.
///     Hardlink exige mesmo volume. A implementação valida isso e cai para
///     cópia quando não der.
/// </summary>
public interface IContentStore
{
    Task<bool> ContainsAsync(string sha256, CancellationToken ct);

    /// <summary>
    ///     Adiciona ao store. A implementação recalcula o hash enquanto grava e
    ///     rejeita se não bater com o informado — o arquivo pode ter chegado
    ///     corrompido ou adulterado.
    /// </summary>
    Task AddAsync(string sha256, Stream content, CancellationToken ct);

    /// <summary>Coloca o arquivo na pasta da instância.</summary>
    Task MaterializeAsync(string sha256, string destinationPath, CancellationToken ct);

    Task<long> GetSizeOnDiskAsync(CancellationToken ct);
}