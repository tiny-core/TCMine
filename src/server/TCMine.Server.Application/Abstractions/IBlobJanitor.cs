namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Inspeção e remoção de blobs — operações de manutenção, separadas do
///     <see cref="IBlobStore" /> de propósito.
///     O caminho quente (gravar, ler, servir ao launcher) não deve nem enxergar
///     um método de apagar: é a operação mais perigosa do sistema, porque um
///     blob apagado por engano quebra silenciosamente todas as versões que
///     apontam para ele. Manter em porta própria deixa explícito quem pode
///     chamar. Nem todo backend sabe enumerar barato — um object storage cobra
///     por listagem —, o que é outra razão para não estar na porta principal.
/// </summary>
public interface IBlobJanitor
{
    /// <summary>Varre o store e devolve o que existe fisicamente.</summary>
    IAsyncEnumerable<StoredBlob> EnumerateAsync(CancellationToken ct);

    /// <summary>Apaga um blob. Devolve false se ele já não estava lá.</summary>
    Task<bool> DeleteAsync(string sha256, CancellationToken ct);
}

/// <summary>
///     Um blob como está no disco. <paramref name="CreatedAt" /> vem do sistema
///     de arquivos — é o que permite a guarda de idade mínima, sem depender de
///     nenhum registro que o sistema não mantém.
/// </summary>
public sealed record StoredBlob(string Sha256, long SizeBytes, DateTimeOffset CreatedAt);
