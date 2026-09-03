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

    /// <summary>
    ///     Todos os hashes já presentes. É o que o diff usa para decidir o que
    ///     precisa vir da rede: um mod compartilhado com outro pack instalado não
    ///     é baixado de novo.
    /// </summary>
    Task<IReadOnlySet<string>> ListHashesAsync(CancellationToken ct);

    /// <summary>
    ///     Coloca o arquivo na pasta da instância.
    ///     <paramref name="allowHardLink" /> decide entre ligar e copiar, e a
    ///     decisão é de quem chama porque depende do CAMINHO, não do conteúdo:
    ///     ver <see cref="Sync.InstanceLayout.CanHardLink" />. Ligar um arquivo
    ///     que o jogo reescreve corromperia o blob compartilhado por todas as
    ///     instâncias.
    /// </summary>
    Task MaterializeAsync(string sha256, string destinationPath, bool allowHardLink, CancellationToken ct);

    Task<long> GetSizeOnDiskAsync(CancellationToken ct);
}
