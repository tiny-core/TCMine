using System.Text.Json;
using Microsoft.Extensions.Logging;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Sync;
using TCMine.Launcher.Infrastructure.Serialization;

namespace TCMine.Launcher.Infrastructure.Game;

/// <summary>
///     O jogo aberto, em <c>running-game.json</c> na raiz. Um arquivo só porque
///     só há um jogo aberto de cada vez (ver <c>GameSession</c>).
/// </summary>
public sealed partial class FileRunningGameStore(
    LauncherPaths paths,
    ILogger<FileRunningGameStore> logger) : IRunningGameStore
{
    private readonly ILogger<FileRunningGameStore> _logger = logger;

    private string Caminho => Path.Combine(paths.RootDirectory, "running-game.json");

    public async Task<RunningGameRecord?> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(Caminho))
            return null;

        try
        {
            await using var stream = File.OpenRead(Caminho);

            var lido = await JsonSerializer.DeserializeAsync(
                stream, LauncherJsonContext.Default.RunningGameFile, ct);

            return lido is { InstanceId.Length: > 0, ProcessId: > 0 }
                ? new RunningGameRecord(
                    new InstanceKey(lido.InstanceId),
                    new GameProcessIdentity(lido.ProcessId, lido.StartedAt))
                : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // Ilegível vale como ausente: o pior que acontece é não reencontrar
            // um jogo — e o caso comum é nem haver jogo aberto.
            LogUnreadable(ex, Caminho);
            return null;
        }
    }

    public async Task WriteAsync(RunningGameRecord record, CancellationToken ct)
    {
        Directory.CreateDirectory(paths.RootDirectory);

        // Temporário e move, como os outros arquivos da raiz: um arquivo cortado
        // a meio por uma queda seria lido como ausente.
        var temporary = Caminho + ".tmp";

        await using (var stream = File.Create(temporary))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                new RunningGameFile
                {
                    InstanceId = record.Instance.Id,
                    ProcessId = record.Process.ProcessId,
                    StartedAt = record.Process.StartedAt
                },
                LauncherJsonContext.Default.RunningGameFile,
                ct);
        }

        File.Move(temporary, Caminho, true);
    }

    public Task ClearAsync(CancellationToken ct)
    {
        File.Delete(Caminho);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Registro do jogo aberto ilegível em {Path}; tratado como ausente.")]
    private partial void LogUnreadable(Exception ex, string path);
}

public sealed record RunningGameFile
{
    public string? InstanceId { get; init; }

    public int ProcessId { get; init; }

    public DateTimeOffset StartedAt { get; init; }
}
