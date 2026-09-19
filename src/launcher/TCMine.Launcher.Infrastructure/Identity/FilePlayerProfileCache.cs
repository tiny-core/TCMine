using System.Text.Json;
using Microsoft.Extensions.Logging;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Infrastructure.Serialization;

namespace TCMine.Launcher.Infrastructure.Identity;

/// <summary>
///     O último perfil conhecido, num ficheiro ao lado do tcmine.json.
///     Existe para um caso só: abrir o jogo sem rede nenhuma. Com internet o
///     perfil vem do Minecraft e este ficheiro é apenas reescrito.
///     Ilegível é tratado como ausente, como em todo o resto do launcher: sem
///     ele o jogo não abre offline, o que é um incómodo — rebentar aqui seria um
///     impedimento, e por uma coisa que se reconstrói ao entrar uma vez.
/// </summary>
public sealed partial class FilePlayerProfileCache(
    LauncherPaths paths,
    ILogger<FilePlayerProfileCache> logger) : IPlayerProfileCache
{
    private readonly ILogger<FilePlayerProfileCache> _logger = logger;

    private string Caminho => Path.Combine(paths.RootDirectory, "profile.json");

    public async Task<PlayerProfile?> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(Caminho))
            return null;

        try
        {
            await using var stream = File.OpenRead(Caminho);

            return await JsonSerializer.DeserializeAsync(
                stream, LauncherJsonContext.Default.PlayerProfile, ct);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            LogIlegivel(ex, Caminho);
            return null;
        }
    }

    public async Task WriteAsync(PlayerProfile profile, CancellationToken ct)
    {
        Directory.CreateDirectory(paths.RootDirectory);

        // Temporário e move, como o resto: um ficheiro truncado por uma queda
        // seria lido como ausente, e o jogador perderia o modo offline sem saber.
        var temporario = Caminho + ".tmp";

        await using (var stream = File.Create(temporario))
        {
            await JsonSerializer.SerializeAsync(
                stream, profile, LauncherJsonContext.Default.PlayerProfile, ct);
        }

        File.Move(temporario, Caminho, true);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Perfil guardado ilegível em {Caminho}; tratado como ausente.")]
    private partial void LogIlegivel(Exception ex, string caminho);
}
