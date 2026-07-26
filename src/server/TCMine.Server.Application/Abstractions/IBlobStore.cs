namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Armazenamento endereçado por conteúdo.
///     Teremos duas implementações (disco local e object storage) mais uma
///     composta que escolhe entre elas por política. A Application não sabe
///     qual está em uso.
/// </summary>
public interface IBlobStore
{
    Task<bool> ExistsAsync(string sha256, CancellationToken ct);

    /// <summary>
    ///     Grava e devolve o hash real do conteúdo.
    ///     Se expectedSha256 vier preenchido e não bater, a implementação deve
    ///     lançar. Confiar no hash que a origem informou é como não verificar
    ///     nada: se o CDN devolveu outro arquivo, você acabou de gravar o
    ///     arquivo errado com o nome certo.
    /// </summary>
    Task<string> PutAsync(Stream content, string? expectedSha256, string contentType, CancellationToken ct);

    Task<Stream> OpenAsync(string sha256, CancellationToken ct);

    /// <summary>
    ///     URL pré-assinada, quando o backend suporta. Nulo significa "sirva pela
    ///     aplicação".
    ///     É a diferença entre o Kestrel segurar uma thread streamando 400 MB e
    ///     o cliente baixar direto do storage, sem passar pelo seu processo.
    /// </summary>
    Task<Uri?> TryGetDirectUrlAsync(string sha256, TimeSpan lifetime, CancellationToken ct);

    /// <summary>
    ///     Caminho físico do blob no host, quando o backend é disco local e
    ///     permite hardlink. Null em backends sem caminho local (object storage),
    ///     onde o materializador copia via stream.
    /// </summary>
    Task<string?> TryGetLocalPathAsync(string sha256, CancellationToken ct);
}