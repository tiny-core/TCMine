using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Infrastructure.Content;

/// <summary>
///     Store local endereçado por conteúdo, em disco.
///     Layout <c>{store}/{sha[0:2]}/{sha[2:4]}/{sha}</c>: dois níveis de shard
///     porque um diretório único com dezenas de milhares de arquivos degrada a
///     listagem em NTFS e incomoda qualquer explorador de arquivos.
///     A lógica aqui é portável de propósito. A única parte específica do sistema
///     — o hardlink — entra pela porta <see cref="IFileLinker" />, e sem ela o
///     store simplesmente copia.
/// </summary>
public sealed partial class FileSystemContentStore(
    LauncherPaths paths,
    IFileLinker linker,
    ILogger<FileSystemContentStore> logger) : IContentStore
{
    private readonly ILogger<FileSystemContentStore> _logger = logger;

    public Task<bool> ContainsAsync(string sha256, CancellationToken ct) =>
        Task.FromResult(File.Exists(PathFor(sha256)));

    public async Task AddAsync(string sha256, Stream content, CancellationToken ct)
    {
        var destino = PathFor(sha256);

        if (File.Exists(destino))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);

        // Grava em temporário no MESMO diretório: o move final é atômico dentro
        // do volume, então ninguém nunca vê um blob pela metade — e uma queda no
        // meio do download deixa lixo temporário, não um arquivo corrompido que
        // o store passaria a servir como bom.
        var temporario = destino + ".tmp";

        try
        {
            var calculado = await GravarCalculandoAsync(content, temporario, ct);

            if (!string.Equals(calculado, sha256, StringComparison.OrdinalIgnoreCase))
            {
                // O arquivo chegou corrompido ou adulterado. Aceitar aqui
                // significaria servir o conteúdo errado para sempre, porque
                // daqui em diante ninguém mais confere.
                LogHashDivergente(sha256, calculado);

                throw new InvalidOperationException(
                    $"O conteúdo baixado não confere: esperado {sha256}, obtido {calculado}.");
            }

            File.Move(temporario, destino, true);
        }
        finally
        {
            if (File.Exists(temporario))
                File.Delete(temporario);
        }
    }

    public Task<IReadOnlySet<string>> ListHashesAsync(CancellationToken ct)
    {
        if (!Directory.Exists(paths.StoreDirectory))
            return Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var hashes = Directory
            .EnumerateFiles(paths.StoreDirectory, "*", SearchOption.AllDirectories)
            .Select(caminho => Path.GetFileName(caminho))
            // Ignora os .tmp de downloads interrompidos: eles não são conteúdo
            // válido, e contá-los faria o diff pular um download necessário.
            .Where(nome => nome.Length is 64)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return Task.FromResult<IReadOnlySet<string>>(hashes);
    }

    public async Task MaterializeAsync(
        string sha256, string destinationPath, bool allowHardLink, CancellationToken ct)
    {
        var origem = PathFor(sha256);

        if (!File.Exists(origem))
            throw new FileNotFoundException($"O conteúdo {sha256} não está no store.", origem);

        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);

        // Apagar antes: sobrescrever um hardlink existente escreveria NO BLOB,
        // e a corrupção viajaria para todas as instâncias que o compartilham.
        if (File.Exists(destinationPath))
            File.Delete(destinationPath);

        if (allowHardLink && linker.TryCreateHardLink(origem, destinationPath))
            return;

        // Volume diferente, sistema sem suporte, ou arquivo que o jogo reescreve.
        // Copiar é o caminho correto, não uma degradação.
        await using var entrada = File.OpenRead(origem);
        await using var saida = File.Create(destinationPath);

        await entrada.CopyToAsync(saida, ct);
    }

    public Task<long> GetSizeOnDiskAsync(CancellationToken ct)
    {
        if (!Directory.Exists(paths.StoreDirectory))
            return Task.FromResult(0L);

        var total = Directory
            .EnumerateFiles(paths.StoreDirectory, "*", SearchOption.AllDirectories)
            .Sum(caminho => new FileInfo(caminho).Length);

        return Task.FromResult(total);
    }

    /// <summary>
    ///     Grava e calcula o hash na MESMA passada. Ler o arquivo de novo para
    ///     conferir dobraria a E/S de cada download.
    /// </summary>
    private static async Task<string> GravarCalculandoAsync(Stream origem, string destino, CancellationToken ct)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        await using (var saida = File.Create(destino))
        {
            var buffer = new byte[81920];
            int lidos;

            while ((lidos = await origem.ReadAsync(buffer, ct)) > 0)
            {
                hash.AppendData(buffer, 0, lidos);
                await saida.WriteAsync(buffer.AsMemory(0, lidos), ct);
            }
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    private string PathFor(string sha256) =>
        Path.Combine(paths.StoreDirectory, sha256[..2], sha256[2..4], sha256);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Conteúdo baixado não confere: esperado {Esperado}, obtido {Obtido}.")]
    private partial void LogHashDivergente(string esperado, string obtido);
}
