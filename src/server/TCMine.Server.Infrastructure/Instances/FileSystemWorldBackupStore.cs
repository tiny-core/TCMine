using System.Globalization;
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Infrastructure.Instances;

/// <summary>
///     Snapshots do mundo como .zip, num diretório por servidor ao lado das
///     instâncias.
///     Fora da pasta da instância de propósito: o materializador reescreve a
///     instância a cada troca de versão, e um backup guardado lá dentro estaria
///     no caminho de quem ele deveria proteger.
/// </summary>
public sealed partial class FileSystemWorldBackupStore(
    IInstanceMaterializer materializer,
    ILogger<FileSystemWorldBackupStore> logger) : IWorldBackupStore
{
    /// <summary>
    ///     O que entra no snapshot. Não é só world/: nível, dados de jogador e
    ///     as listas de permissão fazem parte do "estado do servidor" que o
    ///     admin espera recuperar. Mods e configs não entram — esses o TCMine
    ///     remonta do content store.
    /// </summary>
    private static readonly string[] BackedUp =
    [
        "world", "world_nether", "world_the_end",
        "playerdata", "stats", "advancements",
        "ops.json", "whitelist.json", "banned-players.json", "banned-ips.json"
    ];

    private readonly ILogger<FileSystemWorldBackupStore> _logger = logger;

    public async Task<StoredWorldBackup?> CreateAsync(
        Guid gameServerId, Action<int, int>? onProgress, CancellationToken ct)
    {
        var instancePath = materializer.GetInstancePath(gameServerId);
        var itens = Collect(instancePath);

        if (itens.Count is 0)
            return null; // servidor nunca gerou mundo

        var backupDir = BackupDirectory(gameServerId);
        Directory.CreateDirectory(backupDir);

        var fileName = $"{DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.zip";
        var destination = Path.Combine(backupDir, fileName);

        try
        {
            await Task.Run(() => Compress(instancePath, itens, destination, onProgress, ct), ct);
        }
        catch (Exception)
        {
            // Zip parcial é pior que nenhum: parece backup e não restaura.
            TryDelete(destination);
            throw;
        }

        var size = new FileInfo(destination).Length;
        LogCreated(gameServerId, fileName, size);

        return new StoredWorldBackup(fileName, size);
    }

    public async Task<bool> RestoreAsync(
        Guid gameServerId, string fileName, Action<int, int>? onProgress, CancellationToken ct)
    {
        var source = PathOf(gameServerId, fileName);
        if (source is null || !File.Exists(source))
            return false;

        var instancePath = materializer.GetInstancePath(gameServerId);
        await Task.Run(() => Extract(source, instancePath, onProgress, ct), ct);

        LogRestored(gameServerId, fileName);
        return true;
    }

    public Task<bool> DeleteAsync(Guid gameServerId, string fileName, CancellationToken ct)
    {
        var path = PathOf(gameServerId, fileName);
        if (path is null || !File.Exists(path))
            return Task.FromResult(false);

        File.Delete(path);
        return Task.FromResult(true);
    }

    public Task<Stream?> OpenAsync(Guid gameServerId, string fileName, CancellationToken ct)
    {
        var path = PathOf(gameServerId, fileName);
        if (path is null || !File.Exists(path))
            return Task.FromResult<Stream?>(null);

        return Task.FromResult<Stream?>(
            new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read));
    }

    private static void Compress(
        string instancePath, List<string> itens, string destination,
        Action<int, int>? onProgress, CancellationToken ct)
    {
        var arquivos = new List<string>();
        foreach (var item in itens)
        {
            var full = Path.Combine(instancePath, item);

            if (Directory.Exists(full))
                arquivos.AddRange(Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories));
            else if (File.Exists(full))
                arquivos.Add(full);
        }

        using var zip = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        using var archive = new ZipArchive(zip, ZipArchiveMode.Create);

        var done = 0;
        foreach (var arquivo in arquivos)
        {
            ct.ThrowIfCancellationRequested();

            var relative = Path.GetRelativePath(instancePath, arquivo).Replace('\\', '/');

            // Fastest, não Optimal: os .mca já são comprimidos por dentro, então
            // o esforço extra rende quase nada e dobra o tempo num mundo grande.
            archive.CreateEntryFromFile(arquivo, relative, CompressionLevel.Fastest);

            done++;
            if (done % 200 is 0)
                onProgress?.Invoke(done, arquivos.Count);
        }

        onProgress?.Invoke(arquivos.Count, arquivos.Count);
    }

    private static void Extract(
        string source, string instancePath, Action<int, int>? onProgress, CancellationToken ct)
    {
        using var archive = ZipFile.OpenRead(source);

        var total = archive.Entries.Count;
        var done = 0;

        // A raiz canônica para conferir escape: um entry com ".." escreveria
        // fora da instância — no host que roda o painel.
        var root = Path.GetFullPath(instancePath + Path.DirectorySeparatorChar);

        foreach (var entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();

            if (entry.FullName.EndsWith('/'))
                continue;

            var target = Path.GetFullPath(Path.Combine(instancePath, entry.FullName));
            if (!target.StartsWith(root, StringComparison.Ordinal))
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, true);

            done++;
            if (done % 200 is 0)
                onProgress?.Invoke(done, total);
        }

        onProgress?.Invoke(total, total);
    }

    /// <summary>O que existe na instância, dentre o que interessa salvar.</summary>
    private static List<string> Collect(string instancePath)
    {
        if (!Directory.Exists(instancePath))
            return [];

        return
        [
            .. BackedUp.Where(item =>
            {
                var full = Path.Combine(instancePath, item);
                return Directory.Exists(full) || File.Exists(full);
            })
        ];
    }

    private string BackupDirectory(Guid gameServerId)
    {
        // Irmão da pasta da instância: {root}/../backups/{serverId}.
        var instance = materializer.GetInstancePath(gameServerId);
        var root = Directory.GetParent(instance)?.FullName ?? instance;

        return Path.Combine(root, "backups", gameServerId.ToString());
    }

    /// <summary>
    ///     Caminho do arquivo, ou null se o nome tentar escapar do diretório.
    ///     O nome chega de requisição HTTP (download, restauração) — sem esta
    ///     conferência viraria leitura ou remoção de arquivo arbitrário.
    /// </summary>
    private string? PathOf(Guid gameServerId, string fileName)
    {
        if (fileName.Contains("..", StringComparison.Ordinal)
            || fileName.Contains('/', StringComparison.Ordinal)
            || fileName.Contains('\\', StringComparison.Ordinal))
            return null;

        return Path.Combine(BackupDirectory(gameServerId), fileName);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // Nada a fazer: o arquivo parcial fica e aparece como backup órfão.
        }
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Backup do mundo do servidor {ServerId} criado: {FileName} ({SizeBytes} bytes).")]
    private partial void LogCreated(Guid serverId, string fileName, long sizeBytes);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Mundo do servidor {ServerId} restaurado a partir de {FileName}.")]
    private partial void LogRestored(Guid serverId, string fileName);
}
