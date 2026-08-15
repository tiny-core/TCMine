using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Infrastructure.Storage;

/// <summary>
///     Varre e apaga blobs no store em disco.
///     O layout é shard por hash (<c>{sha[0:2]}/{sha[2:4]}/{sha}</c>), então
///     enumerar é percorrer os diretórios. O nome do arquivo É o hash — não há
///     índice a consultar nem risco de o nome discordar do conteúdo.
/// </summary>
public sealed partial class FileSystemBlobJanitor(
    IOptions<BlobStorageOptions> options,
    ILogger<FileSystemBlobJanitor> logger) : IBlobJanitor
{
    private readonly ILogger<FileSystemBlobJanitor> _logger = logger;
    /// <summary>
    ///     Quantos arquivos por ida à thread do pool. Grande o bastante para o
    ///     custo do salto se diluir, pequeno o bastante para a interface
    ///     respirar entre os lotes.
    /// </summary>
    private const int BatchSize = 500;

    private readonly BlobStorageOptions _options = options.Value;

    public async IAsyncEnumerable<StoredBlob> EnumerateAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (!Directory.Exists(_options.RootPath))
            yield break;

        // EnumerateFiles em vez de GetFiles: devolve conforme encontra, sem
        // montar um array com dezenas de milhares de caminhos antes de começar.
        using var caminhos = Directory
            .EnumerateFiles(_options.RootPath, "*", SearchOption.AllDirectories)
            .GetEnumerator();

        while (true)
        {
            // Cada lote é lido numa thread do pool. Isto NÃO é otimização: sem
            // sair da thread chamadora, a varredura roda no dispatcher do
            // circuito Blazor — a mesma que desenha a tela e trata os cliques —
            // e a aba do admin congela até o fim. O await entre lotes é o que
            // devolve o controle à interface.
            var lote = await Task.Run(() => NextBatch(caminhos, BatchSize), ct).ConfigureAwait(false);
            if (lote.Count is 0)
                yield break;

            foreach (var blob in lote)
                yield return blob;
        }
    }

    /// <summary>Lê até <paramref name="tamanho" /> arquivos, pulando o que não é blob.</summary>
    private static List<StoredBlob> NextBatch(IEnumerator<string> caminhos, int tamanho)
    {
        var lote = new List<StoredBlob>(tamanho);

        while (lote.Count < tamanho && caminhos.MoveNext())
        {
            var path = caminhos.Current;
            var name = Path.GetFileName(path);

            // Ignora o que não parece blob: a pasta .tmp guarda escritas em
            // andamento, e apagá-las abortaria um download em curso.
            if (!IsHash(name))
                continue;

            try
            {
                var info = new FileInfo(path);
                lote.Add(new StoredBlob(name, info.Length, info.CreationTimeUtc));
            }
            catch (IOException)
            {
                // Arquivo sumiu entre listar e medir: some da conta, sem drama.
            }
        }

        return lote;
    }

    public async Task<bool> DeleteAsync(string sha256, CancellationToken ct)
    {
        if (!IsHash(sha256))
            return false;

        var normalized = sha256.ToLowerInvariant();
        var path = Path.Combine(_options.RootPath, normalized[..2], normalized[2..4], normalized);

        // Fora da thread chamadora pelo mesmo motivo da varredura: apagar
        // centenas de arquivos no dispatcher do circuito congela a aba.
        return await Task.Run(() =>
        {
            try
            {
                if (!File.Exists(path))
                    return false;

                File.Delete(path);
                LogDeleted(normalized);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Arquivo em uso (hardlink de instância rodando, por exemplo):
                // preferimos não apagar a arriscar o servidor de jogo.
                LogDeleteFailed(ex, normalized);
                return false;
            }
        }, ct).ConfigureAwait(false);
    }

    private static bool IsHash(string value) => value.Length is 64 && value.All(char.IsAsciiHexDigit);

    [LoggerMessage(Level = LogLevel.Information, Message = "Blob órfão {Sha256} apagado.")]
    private partial void LogDeleted(string sha256);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Não foi possível apagar o blob {Sha256}.")]
    private partial void LogDeleteFailed(Exception ex, string sha256);
}
