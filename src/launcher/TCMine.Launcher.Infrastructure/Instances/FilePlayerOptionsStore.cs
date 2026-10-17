using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Sync;
using TCMine.Launcher.Infrastructure.Serialization;

namespace TCMine.Launcher.Infrastructure.Instances;

/// <summary>
///     O <c>options.txt</c> na pasta da instância (o game dir do jogo) e, ao lado,
///     <c>.tcmine-options-sync.json</c> com o estado da última sincronização —
///     pontuado como o manifesto, para o jogador não o confundir com o jogo.
/// </summary>
public sealed partial class FilePlayerOptionsStore(
    IInstanceStore instances,
    ILogger<FilePlayerOptionsStore> logger) : IPlayerOptionsStore
{
    private const string OptionsName = "options.txt";
    private const string StateName = ".tcmine-options-sync.json";

    /// <summary>
    ///     Sem BOM: o Minecraft lê o arquivo como UTF-8 puro, e um BOM viraria
    ///     parte do nome da primeira opção.
    /// </summary>
    private static readonly UTF8Encoding Utf8 = new(false);

    private readonly ILogger<FilePlayerOptionsStore> _logger = logger;

    public async Task<LocalOptions?> ReadAsync(InstanceKey key, CancellationToken ct)
    {
        var path = Path.Combine(instances.PathFor(key), OptionsName);

        if (!File.Exists(path))
            return null;

        var text = await File.ReadAllTextAsync(path, Utf8, ct);
        return new LocalOptions(text, new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero));
    }

    public async Task WriteAsync(InstanceKey key, string text, CancellationToken ct)
    {
        var folder = instances.PathFor(key);
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, OptionsName);

        // Temporário e move: um options.txt cortado a meio faria o jogo voltar
        // TODAS as opções ao padrão, que é pior do que não sincronizar.
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, text, Utf8, ct);
        File.Move(temporary, path, true);
    }

    public async Task<OptionsSyncState?> ReadStateAsync(InstanceKey key, CancellationToken ct)
    {
        var path = Path.Combine(instances.PathFor(key), StateName);

        if (!File.Exists(path))
            return null;

        try
        {
            await using var stream = File.OpenRead(path);

            var lido = await JsonSerializer.DeserializeAsync(stream, LauncherJsonContext.Default.OptionsSyncFile, ct);

            return lido is { Fingerprint.Length: > 0 }
                ? new OptionsSyncState(lido.Fingerprint, lido.RemoteSavedAt)
                : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // Ilegível vale como "nunca sincronizado": a próxima abertura compara
            // pela hora e segue. Não se perde nada que esteja no options.txt.
            LogUnreadable(ex, path);
            return null;
        }
    }

    public async Task WriteStateAsync(InstanceKey key, OptionsSyncState state, CancellationToken ct)
    {
        var folder = instances.PathFor(key);
        Directory.CreateDirectory(folder);

        var path = Path.Combine(folder, StateName);
        var temporary = path + ".tmp";

        await using (var stream = File.Create(temporary))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                new OptionsSyncFile { Fingerprint = state.Fingerprint, RemoteSavedAt = state.RemoteSavedAt },
                LauncherJsonContext.Default.OptionsSyncFile,
                ct);
        }

        File.Move(temporary, path, true);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Estado da sincronização de teclas ilegível em {Path}; tratado como ausente.")]
    private partial void LogUnreadable(Exception ex, string path);
}

public sealed record OptionsSyncFile
{
    public string? Fingerprint { get; init; }

    public DateTimeOffset RemoteSavedAt { get; init; }
}
