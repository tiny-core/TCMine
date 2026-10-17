using System.Text.Json;
using Microsoft.Extensions.Logging;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Infrastructure.Serialization;

namespace TCMine.Launcher.Infrastructure.Configuration;

/// <summary>
///     As escolhas da tela Definições, em <c>preferences.json</c> na raiz.
///     Arquivo à parte do <c>tcmine.json</c> de propósito: aquele é o
///     pareamento com o servidor, que o arranque reescreve sozinho, e misturar
///     as duas coisas faria uma escolha do jogador depender dessa escrita.
/// </summary>
public sealed partial class FileLauncherPreferences(
    LauncherPaths paths,
    ILogger<FileLauncherPreferences> logger) : ILauncherPreferences
{
    private readonly ILogger<FileLauncherPreferences> _logger = logger;

    private string Caminho => Path.Combine(paths.RootDirectory, "preferences.json");

    public async Task<LauncherPreferences> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(Caminho))
            return new LauncherPreferences();

        try
        {
            await using var stream = File.OpenRead(Caminho);

            return await JsonSerializer.DeserializeAsync(stream, LauncherJsonContext.Default.LauncherPreferences, ct)
                   ?? new LauncherPreferences();
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // Ilegível volta aos padrões: são escolhas de um clique, e recusar
            // abrir por causa delas seria trocar um incómodo por um impedimento.
            LogUnreadable(ex, Caminho);
            return new LauncherPreferences();
        }
    }

    public async Task WriteAsync(LauncherPreferences preferences, CancellationToken ct)
    {
        Directory.CreateDirectory(paths.RootDirectory);

        var temporary = Caminho + ".tmp";

        await using (var stream = File.Create(temporary))
        {
            await JsonSerializer.SerializeAsync(
                stream, preferences, LauncherJsonContext.Default.LauncherPreferences, ct);
        }

        File.Move(temporary, Caminho, true);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Preferências ilegíveis em {Path}; usando os padrões.")]
    private partial void LogUnreadable(Exception ex, string path);
}
