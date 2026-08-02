using System.Text.Json;
using Microsoft.Extensions.Options;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Infrastructure.Instances;

public sealed class FileSystemInstanceMaterializer(
    IBlobStore blobStore,
    IOptions<InstanceOptions> options) : IInstanceMaterializer
{
    // world/ e dados do jogador nunca entram
    // aqui, então nunca são removidos numa re-materialização.
    private const string ManifestFileName = ".tcmine-manifest.json";

    private readonly InstanceOptions _options = options.Value;

    // Resolve relativo→absoluto já aqui. O bind mount do Docker exige caminho
    // absoluto; e o CWD do processo pode não ser o que esperamos, então fixamos
    // uma vez em vez de depender do diretório atual a cada chamada.
    private readonly string _rootPath = Path.GetFullPath(options.Value.RootPath);

    public string GetInstancePath(Guid gameServerId) => Path.Combine(_rootPath, gameServerId.ToString());

    public Task DeleteInstanceAsync(Guid gameServerId, CancellationToken ct)
    {
        var path = GetInstancePath(gameServerId);

        // Guarda de sanidade: só apagamos algo que está mesmo debaixo da raiz
        // de instâncias. Se o caminho resolvido escapar da raiz (id estranho,
        // config errada), recusamos — apagar recursivamente a pasta errada é
        // catastrófico.
        var fullRoot = Path.GetFullPath(_rootPath + Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(fullRoot, StringComparison.Ordinal))
            throw new InvalidOperationException($"Caminho de instância fora da raiz: {path}");

        if (Directory.Exists(fullPath))
            Directory.Delete(fullPath, true);

        return Task.CompletedTask;
    }

    public async Task MaterializeAsync(Guid gameServerId, ModpackVersion version, CancellationToken ct)
    {
        var instancePath = GetInstancePath(gameServerId);
        Directory.CreateDirectory(instancePath);

        // Servidor leva Both + ServerOnly. ClientOnly (shaders, minimap) fica de fora.
        var serverFiles = version.Files
            .Where(f => f.Side is FileSide.Both or FileSide.ServerOnly)
            .ToList();

        var desired = serverFiles
            .Select(f => Normalize(f.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Remove o que saiu da versão desde a última materialização (mods que
        // deixaram de existir). Só toca no que está no manifesto — world/ intacto.
        var manifestPath = Path.Combine(instancePath, ManifestFileName);
        foreach (var stale in (await ReadManifestAsync(manifestPath, ct)).Where(p => !desired.Contains(p)))
        {
            var full = Path.Combine(instancePath, stale);
            if (File.Exists(full))
                File.Delete(full);
        }

        foreach (var file in serverFiles)
        {
            ct.ThrowIfCancellationRequested();

            var target = Path.Combine(instancePath, Normalize(file.Path));
            GuardInside(instancePath, target); // anti path traversal
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);

            if (File.Exists(target))
                File.Delete(target);

            await PlaceAsync(file, target, ct);
        }

        await WriteManifestAsync(manifestPath, desired, ct);
    }

    // mods/ → hardlink (jars read-only, onde estão os bytes). Resto → cópia,
    // porque o servidor pode reescrevê-los e um hardlink corromperia o blob.
    private async Task PlaceAsync(ModpackFile file, string target, CancellationToken ct)
    {
        var isMod = Normalize(file.Path).StartsWith("mods/", StringComparison.OrdinalIgnoreCase);

        if (isMod)
        {
            var source = await blobStore.TryGetLocalPathAsync(file.Sha256, ct);
            if (source is not null && HardLink.TryCreate(source, target))
                return;
        }

        await using var blob = await blobStore.OpenAsync(file.Sha256, ct);
        await using var dest = File.Create(target);
        await blob.CopyToAsync(dest, ct);
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');

    private static void GuardInside(string root, string target)
    {
        var fullRoot = Path.GetFullPath(root + Path.DirectorySeparatorChar);
        if (!Path.GetFullPath(target).StartsWith(fullRoot, StringComparison.Ordinal))
            throw new InvalidOperationException($"Caminho fora da instância: {target}");
    }

    private static async Task<HashSet<string>> ReadManifestAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        await using var s = File.OpenRead(path);
        var list = await JsonSerializer.DeserializeAsync<List<string>>(s, cancellationToken: ct);
        return new HashSet<string>(list ?? [], StringComparer.OrdinalIgnoreCase);
    }

    private static async Task WriteManifestAsync(string path, IEnumerable<string> paths, CancellationToken ct)
    {
        await using var s = File.Create(path);
        await JsonSerializer.SerializeAsync(s, paths.ToList(), cancellationToken: ct);
    }
}
