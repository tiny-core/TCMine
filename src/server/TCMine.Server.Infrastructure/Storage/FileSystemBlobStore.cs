using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Infrastructure.Storage;

/// <summary>
///     Armazenamento endereçado por conteúdo, em disco local.
///     Layout: {raiz}/a3/f9/a3f9c1...
///     Os dois níveis de subpasta vêm dos primeiros quatro caracteres do hash.
///     Com 50 mil arquivos numa pasta só, listar diretório fica lento em
///     qualquer filesystem e insuportável no NTFS. Divididos em 256 x 256
///     pastas, nenhuma passa de algumas dezenas de arquivos.
///     A classe é partial porque o source generator do LoggerMessage escreve a
///     outra metade — os métodos de log declarados no final.
/// </summary>
public sealed partial class FileSystemBlobStore : IBlobStore
{
    private readonly ILogger<FileSystemBlobStore> _logger;
    private readonly BlobStorageOptions _options;

    public FileSystemBlobStore(
        IOptions<BlobStorageOptions> options,
        ILogger<FileSystemBlobStore> logger)
    {
        _options = options.Value;
        _logger = logger;

        Directory.CreateDirectory(_options.RootPath);
        Directory.CreateDirectory(TempDirectory);
    }

    private string TempDirectory => Path.Combine(_options.RootPath, ".tmp");

    public Task<bool> ExistsAsync(string sha256, CancellationToken ct)
    {
        return Task.FromResult(File.Exists(ResolvePath(sha256)));
    }

    public async Task<string> PutAsync(
        Stream content,
        string? expectedSha256,
        string contentType,
        CancellationToken ct)
    {
        // Grava primeiro num arquivo temporário com nome aleatório. O
        // arquivo só ganha o nome definitivo depois que o conteúdo inteiro
        // chegou e o hash foi conferido — assim uma queda no meio da
        // gravação deixa lixo em .tmp, nunca um blob corrompido em uso.
        var tempPath = Path.Combine(TempDirectory, Path.GetRandomFileName());

        try
        {
            var (hash, bytes) = await WriteAndHashAsync(content, tempPath, ct);

            if (expectedSha256 is not null &&
                !hash.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
                // Confiar no hash informado pela origem equivale a não
                // verificar nada: se o CDN devolveu outro arquivo, teríamos
                // gravado conteúdo errado com o nome certo — e todo cliente
                // que baixasse depois receberia o arquivo errado achando que
                // está correto.
                throw new InvalidDataException(
                    $"Hash divergente. Esperado {expectedSha256}, obtido {hash}.");

            var finalPath = ResolvePath(hash);

            if (File.Exists(finalPath))
            {
                // Conteúdo já existe: é o caso normal de deduplicação, não
                // um erro. Basta descartar o temporário.
                LogBlobAlreadyExists(hash);
                File.Delete(tempPath);
                return hash;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);

            try
            {
                // Move dentro do mesmo volume é atômico: ou o arquivo aparece
                // completo, ou não aparece. Não existe estado intermediário
                // visível para quem lê.
                File.Move(tempPath, finalPath);
            }
            catch (IOException) when (File.Exists(finalPath))
            {
                // Duas requisições gravaram o mesmo conteúdo ao mesmo tempo e
                // a outra chegou primeiro. O resultado é idêntico, então não
                // há nada a corrigir.
                File.Delete(tempPath);
            }

            LogBlobStored(hash, bytes);

            return hash;
        }
        catch
        {
            // Limpeza best-effort: se falhar, sobra um arquivo em .tmp, que
            // é inofensivo e pode ser removido por uma rotina de manutenção.
            TryDelete(tempPath);
            throw;
        }
    }

    public Task<Stream> OpenAsync(string sha256, CancellationToken ct)
    {
        var path = ResolvePath(sha256);

        if (!File.Exists(path))
            throw new FileNotFoundException($"Blob {sha256} não encontrado.");

        // SequentialScan avisa o sistema operacional que a leitura é do
        // começo ao fim, o que melhora o read-ahead. Async porque o Kestrel
        // não pode ficar com uma thread bloqueada streamando 400 MB.
        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            _options.CopyBufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return Task.FromResult(stream);
    }

    /// <summary>
    ///     Disco local não tem URL pré-assinada — quem serve é a aplicação.
    ///     A implementação de object storage devolve URL aqui, e aí os bytes nem
    ///     passam pelo processo do TCMine.
    /// </summary>
    public Task<Uri?> TryGetDirectUrlAsync(string sha256, TimeSpan lifetime, CancellationToken ct)
    {
        return Task.FromResult<Uri?>(null);
    }

    // ---------- Internos ----------

    private async Task<(string Hash, long Bytes)> WriteAndHashAsync(
        Stream source,
        string destination,
        CancellationToken ct)
    {
        // O hash é calculado durante a gravação, não numa segunda leitura.
        // Ler o arquivo de novo só para hashear dobraria a I/O de cada
        // upload — com packs de centenas de megabytes, isso pesa.
        using var sha = SHA256.Create();

        await using var file = new FileStream(
            destination,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            _options.CopyBufferSize,
            FileOptions.Asynchronous);

        var buffer = new byte[_options.CopyBufferSize];
        long total = 0;
        int lidos;

        while ((lidos = await source.ReadAsync(buffer, ct)) > 0)
        {
            sha.TransformBlock(buffer, 0, lidos, null, 0);
            await file.WriteAsync(buffer.AsMemory(0, lidos), ct);
            total += lidos;
        }

        sha.TransformFinalBlock([], 0, 0);

        return (Convert.ToHexStringLower(sha.Hash!), total);
    }

    private string ResolvePath(string sha256)
    {
        // Validação obrigatória: este valor pode vir de requisição HTTP, e um
        // hash com "…" ou barra viraria path traversal — leitura de arquivo
        // arbitrário no servidor.
        if (!IsValidHash(sha256))
            throw new ArgumentException($"Hash inválido: {sha256}", nameof(sha256));

        var normalizado = sha256.ToLowerInvariant();

        return Path.Combine(
            _options.RootPath,
            normalizado[..2],
            normalizado[2..4],
            normalizado);
    }

    private static bool IsValidHash(string value)
    {
        return value.Length is 64 && value.All(char.IsAsciiHexDigit);
    }

    private void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException ex)
        {
            LogTemporaryFileDeleteFailed(ex, path);
        }
    }

    // ---------- Log ----------
    // O source generator escreve a implementação destes métodos em tempo de
    // compilação: sem boxing dos argumentos, sem array de params, e a
    // checagem de nível acontece antes de qualquer formatação.

    [LoggerMessage(Level = LogLevel.Debug, Message = "Blob {Hash} já existia; conteúdo descartado.")]
    private partial void LogBlobAlreadyExists(string hash);

    [LoggerMessage(Level = LogLevel.Information, Message = "Blob {Hash} armazenado ({Bytes} bytes).")]
    private partial void LogBlobStored(string hash, long bytes);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Não foi possível remover o temporário {Path}.")]
    private partial void LogTemporaryFileDeleteFailed(Exception ex, string path);
}