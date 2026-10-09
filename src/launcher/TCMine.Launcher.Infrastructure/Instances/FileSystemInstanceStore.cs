using System.Text.Json;
using Microsoft.Extensions.Logging;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Sync;
using TCMine.Launcher.Infrastructure.Serialization;

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
        var path = ManifestPath(key);

        if (!File.Exists(path))
            return null;

        try
        {
            await using var stream = File.OpenRead(path);

            return await JsonSerializer.DeserializeAsync(
                stream, LauncherJsonContext.Default.InstanceManifest, ct);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // Manifesto ilegível é tratado como ausente, e a consequência é
            // deliberada: o diff seguinte vê uma instância vazia, baixa tudo de
            // novo e NÃO apaga nada — porque sem conjunto gerenciado não há o que
            // apagar. Perder disco é aceitável; perder o mundo do jogador não.
            LogUnreadableManifest(ex, path);
            return null;
        }
    }

    public async Task WriteManifestAsync(InstanceKey key, InstanceManifest manifest, CancellationToken ct)
    {
        var path = ManifestPath(key);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        // Temporário e move, como no tcmine.json: um manifesto truncado por uma
        // queda no meio da escrita seria lido como ausente na próxima abertura,
        // e a instalação inteira se repetiria.
        var temporary = path + ".tmp";

        await using (var stream = File.Create(temporary))
        {
            await JsonSerializer.SerializeAsync(
                stream, manifest, LauncherJsonContext.Default.InstanceManifest, ct);
        }

        File.Move(temporary, path, true);
    }

    public async Task<IReadOnlyList<InstalledInstance>> ListAsync(CancellationToken ct)
    {
        var raiz = Path.Combine(paths.RootDirectory, "instances");

        if (!Directory.Exists(raiz))
            return [];

        // Ler o manifesto e somar o tamanho da pasta são independentes entre
        // instâncias, e o segundo é caro: um mundo jogado tem milhares de
        // arquivos (regiões, playerdata, logs). Somando uma instância de cada
        // vez, de forma síncrona, a tela de instâncias travava por vários
        // segundos a cada abertura — travava a UI inteira, porque corria na
        // mesma thread do circuito Blazor. Task.Run tira o enumerate/stat
        // dessa thread e o WhenAll deixa as instâncias correrem em paralelo.
        var carregamentos = Directory.EnumerateDirectories(raiz)
            .Select(folder => CarregarAsync(folder, ct));

        var instaladas = await Task.WhenAll(carregamentos);

        return
        [
            .. instaladas
                .OfType<InstalledInstance>()
                .OrderBy(i => i.Manifest.ModpackName, StringComparer.CurrentCultureIgnoreCase)
        ];
    }

    public async Task<IReadOnlyList<InstanceManifest>> ListManifestsAsync(CancellationToken ct)
    {
        var raiz = Path.Combine(paths.RootDirectory, "instances");

        if (!Directory.Exists(raiz))
            return [];

        var manifestos = await Task.WhenAll(
            Directory.EnumerateDirectories(raiz)
                .Select(folder => LerAsync(Path.Combine(folder, InstanceManifest.FileName), ct)));

        return [.. manifestos.OfType<InstanceManifest>()];
    }

    public Task DeleteFilesAsync(InstanceKey key, IEnumerable<string> relativePaths, CancellationToken ct)
    {
        var raiz = PathFor(key);

        foreach (var relativo in relativePaths)
        {
            var path = Path.Combine(raiz, relativo);

            // Confinamento: um caminho vindo do diff nunca deveria escapar da
            // pasta, mas "nunca deveria" não é garantia — e um ".." aqui apagaria
            // arquivos fora da instância.
            if (!IsInside(raiz, path))
            {
                LogCaminhoForaDaInstancia(relativo);
                continue;
            }

            if (File.Exists(path))
                File.Delete(path);

            LimparPastasVaziasAte(raiz, Path.GetDirectoryName(path));
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

    /// <summary>
    ///     Uma instância, ou null se a pasta não tiver manifesto — pode ser
    ///     sobra de uma instalação interrompida, e listá-la ofereceria ao
    ///     jogador um card sem nome nem versão.
    /// </summary>
    private async Task<InstalledInstance?> CarregarAsync(string folder, CancellationToken ct)
    {
        var manifest = await LerAsync(Path.Combine(folder, InstanceManifest.FileName), ct);

        if (manifest is null)
            return null;

        var tamanho = await Task.Run(() => SizeOf(folder), ct);

        // A chave vem do NOME DA PASTA, e não do manifesto. É o que faz as
        // instalações antigas — nomeadas pela regra do par (modpack, versão) —
        // continuarem a ser encontradas sem renomear nada, e o que permite
        // duas instâncias do mesmo pack coexistirem.
        return new InstalledInstance(new InstanceKey(Path.GetFileName(folder)), manifest, tamanho, folder);
    }

    private string ManifestPath(InstanceKey key) => Path.Combine(PathFor(key), InstanceManifest.FileName);

    private async Task<InstanceManifest?> LerAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            await using var stream = File.OpenRead(path);

            return await JsonSerializer.DeserializeAsync(
                stream, LauncherJsonContext.Default.InstanceManifest, ct);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            LogUnreadableManifest(ex, path);
            return null;
        }
    }

    private static bool IsInside(string raiz, string path)
    {
        var raizCompleta = Path.GetFullPath(raiz + Path.DirectorySeparatorChar);

        return Path.GetFullPath(path).StartsWith(raizCompleta, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Uma pasta que ficou vazia depois de remover o último mod não é
    ///     conteúdo: deixá-la faria a instância acumular esqueletos de versões
    ///     antigas para sempre. Sobe até a raiz da instância e para lá.
    /// </summary>
    private static void LimparPastasVaziasAte(string raiz, string? folder)
    {
        var limite = Path.GetFullPath(raiz);

        while (folder is not null
               && Path.GetFullPath(folder) != limite
               && Directory.Exists(folder)
               && !Directory.EnumerateFileSystemEntries(folder).Any())
        {
            Directory.Delete(folder);
            folder = Path.GetDirectoryName(folder);
        }
    }

    private static long SizeOf(string folder) =>
        Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Sum(path => new FileInfo(path).Length);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Manifesto ilegível em {Path}; tratado como ausente.")]
    private partial void LogUnreadableManifest(Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Error, Message = "Caminho fora da instância ignorado: {Path}")]
    private partial void LogCaminhoForaDaInstancia(string path);
}
