using System.Text.Json;
using Microsoft.Extensions.Logging;
using TCMine.Launcher.Infrastructure.Serialization;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Sync;

namespace TCMine.Launcher.Infrastructure.Instances;

/// <summary>
///     As pastas de instância no disco do jogador.
///     Uma pasta por (modpack, versão), sob <c>{raiz}/instances/</c>. O nome vem
///     de <see cref="InstanceKey.ToDirectoryName" /> e não do nome do pack:
///     acento e barra quebram em algum sistema de arquivos, e renomear o pack
///     renomearia a pasta, forçando download completo de novo.
/// </summary>
public sealed partial class FileSystemInstanceStore(
    LauncherPaths paths,
    ILogger<FileSystemInstanceStore> logger) : IInstanceStore
{
    private readonly ILogger<FileSystemInstanceStore> _logger = logger;

    public string PathFor(InstanceKey key) =>
        Path.Combine(paths.RootDirectory, "instances", key.ToDirectoryName());

    public async Task<InstanceManifest?> ReadManifestAsync(InstanceKey key, CancellationToken ct)
    {
        var caminho = ManifestPath(key);

        if (!File.Exists(caminho))
            return null;

        try
        {
            await using var stream = File.OpenRead(caminho);

            return await JsonSerializer.DeserializeAsync(
                stream, LauncherJsonContext.Default.InstanceManifest, ct);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // Manifesto ilegível é tratado como ausente, e a consequência é
            // deliberada: o diff seguinte vê uma instância vazia, baixa tudo de
            // novo e NÃO apaga nada — porque sem conjunto gerenciado não há o que
            // apagar. Perder disco é aceitável; perder o mundo do jogador não.
            LogManifestoIlegivel(ex, caminho);
            return null;
        }
    }

    public async Task WriteManifestAsync(InstanceKey key, InstanceManifest manifest, CancellationToken ct)
    {
        var caminho = ManifestPath(key);

        Directory.CreateDirectory(Path.GetDirectoryName(caminho)!);

        // Temporário e move, como no tcmine.json: um manifesto truncado por uma
        // queda no meio da escrita seria lido como ausente na próxima abertura,
        // e a instalação inteira se repetiria.
        var temporario = caminho + ".tmp";

        await using (var stream = File.Create(temporario))
        {
            await JsonSerializer.SerializeAsync(
                stream, manifest, LauncherJsonContext.Default.InstanceManifest, ct);
        }

        File.Move(temporario, caminho, true);
    }

    public async Task<IReadOnlyList<InstalledInstance>> ListAsync(CancellationToken ct)
    {
        var raiz = Path.Combine(paths.RootDirectory, "instances");

        if (!Directory.Exists(raiz))
            return [];

        var instaladas = new List<InstalledInstance>();

        foreach (var pasta in Directory.EnumerateDirectories(raiz))
        {
            var manifesto = await LerAsync(Path.Combine(pasta, InstanceManifest.FileName), ct);

            // Pasta sem manifesto não é instância nossa — pode ser sobra de uma
            // instalação interrompida. Listá-la ofereceria ao jogador um card
            // sem nome nem versão.
            if (manifesto is null)
                continue;

            instaladas.Add(new InstalledInstance(
                new InstanceKey(manifesto.ModpackId, manifesto.ModpackVersionId),
                manifesto,
                TamanhoDe(pasta),
                pasta));
        }

        return [.. instaladas.OrderBy(i => i.Manifest.ModpackName, StringComparer.CurrentCultureIgnoreCase)];
    }

    public Task DeleteFilesAsync(InstanceKey key, IEnumerable<string> relativePaths, CancellationToken ct)
    {
        var raiz = PathFor(key);

        foreach (var relativo in relativePaths)
        {
            var caminho = Path.Combine(raiz, relativo);

            // Confinamento: um caminho vindo do diff nunca deveria escapar da
            // pasta, mas "nunca deveria" não é garantia — e um ".." aqui apagaria
            // arquivos fora da instância.
            if (!EstaDentro(raiz, caminho))
            {
                LogCaminhoForaDaInstancia(relativo);
                continue;
            }

            if (File.Exists(caminho))
                File.Delete(caminho);

            LimparPastasVaziasAte(raiz, Path.GetDirectoryName(caminho));
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(InstanceKey key, CancellationToken ct)
    {
        var raiz = PathFor(key);

        if (Directory.Exists(raiz))
            Directory.Delete(raiz, true);

        return Task.CompletedTask;
    }

    private string ManifestPath(InstanceKey key) => Path.Combine(PathFor(key), InstanceManifest.FileName);

    private async Task<InstanceManifest?> LerAsync(string caminho, CancellationToken ct)
    {
        if (!File.Exists(caminho))
            return null;

        try
        {
            await using var stream = File.OpenRead(caminho);

            return await JsonSerializer.DeserializeAsync(
                stream, LauncherJsonContext.Default.InstanceManifest, ct);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            LogManifestoIlegivel(ex, caminho);
            return null;
        }
    }

    private static bool EstaDentro(string raiz, string caminho)
    {
        var raizCompleta = Path.GetFullPath(raiz + Path.DirectorySeparatorChar);

        return Path.GetFullPath(caminho).StartsWith(raizCompleta, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Uma pasta que ficou vazia depois de remover o último mod não é
    ///     conteúdo: deixá-la faria a instância acumular esqueletos de versões
    ///     antigas para sempre. Sobe até a raiz da instância e para lá.
    /// </summary>
    private static void LimparPastasVaziasAte(string raiz, string? pasta)
    {
        var limite = Path.GetFullPath(raiz);

        while (pasta is not null
               && Path.GetFullPath(pasta) != limite
               && Directory.Exists(pasta)
               && !Directory.EnumerateFileSystemEntries(pasta).Any())
        {
            Directory.Delete(pasta);
            pasta = Path.GetDirectoryName(pasta);
        }
    }

    private static long TamanhoDe(string pasta) =>
        Directory.EnumerateFiles(pasta, "*", SearchOption.AllDirectories)
            .Sum(caminho => new FileInfo(caminho).Length);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Manifesto ilegível em {Path}; tratado como ausente.")]
    private partial void LogManifestoIlegivel(Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Caminho fora da instância ignorado: {Path}")]
    private partial void LogCaminhoForaDaInstancia(string path);
}
