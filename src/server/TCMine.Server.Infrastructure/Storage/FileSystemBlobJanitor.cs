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
    private readonly BlobStorageOptions _options = options.Value;

    public async IAsyncEnumerable<StoredBlob> EnumerateAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (!Directory.Exists(_options.RootPath))
            yield break;

        // EnumerateFiles em vez de GetFiles: devolve conforme encontra, sem
        // montar um array com dezenas de milhares de caminhos antes de começar.
        foreach (var path in Directory.EnumerateFiles(_options.RootPath, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();

            var name = Path.GetFileName(path);

            // Ignora o que não parece blob: a pasta .tmp guarda escritas em
            // andamento, e apagá-las abortaria um download em curso.
            if (!IsHash(name))
                continue;

            FileInfo info;
            try
            {
                info = new FileInfo(path);
            }
            catch (IOException)
            {
                continue;
            }

            yield return new StoredBlob(name, info.Length, info.CreationTimeUtc);
            await Task.Yield();
        }
    }

    public Task<bool> DeleteAsync(string sha256, CancellationToken ct)
    {
        if (!IsHash(sha256))
            return Task.FromResult(false);

        var normalized = sha256.ToLowerInvariant();
        var path = Path.Combine(_options.RootPath, normalized[..2], normalized[2..4], normalized);

        try
        {
            if (!File.Exists(path))
                return Task.FromResult(false);

            File.Delete(path);
            LogDeleted(normalized);
            return Task.FromResult(true);
        }
        catch (IOException ex)
        {
            // Arquivo em uso (hardlink de instância rodando, por exemplo):
            // preferimos não apagar a arriscar o servidor de jogo.
            LogDeleteFailed(ex, normalized);
            return Task.FromResult(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            LogDeleteFailed(ex, normalized);
            return Task.FromResult(false);
        }
    }

    private static bool IsHash(string value) => value.Length is 64 && value.All(char.IsAsciiHexDigit);

    [LoggerMessage(Level = LogLevel.Information, Message = "Blob órfão {Sha256} apagado.")]
    private partial void LogDeleted(string sha256);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Não foi possível apagar o blob {Sha256}.")]
    private partial void LogDeleteFailed(Exception ex, string sha256);
}
