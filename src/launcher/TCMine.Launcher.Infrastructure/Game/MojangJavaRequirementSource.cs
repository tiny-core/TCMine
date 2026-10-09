using System.Text.Json;
using Microsoft.Extensions.Logging;
using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.Infrastructure.Game;

/// <summary>
///     Lê o <c>javaVersion</c> que a versão do Minecraft declara.
///     Disco primeiro, rede depois. A ordem importa: depois da primeira abertura
///     o JSON já está em <c>versions/</c> — o CmlLib grava-o —, e ler de lá faz
///     o jogo abrir sem ligação. Só uma versão nunca vista obriga a perguntar à
///     Mojang.
///     Nunca atira. Não saber qual Java é um caso previsto, e quem chama recorre
///     ao palpite; rebentar aqui transformaria "sem rede" em "não joga".
/// </summary>
public sealed partial class MojangJavaRequirementSource(
    HttpClient http,
    LauncherPaths paths,
    ILogger<MojangJavaRequirementSource> logger) : IJavaRequirementSource
{
    private const string ManifestUrl = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";

    private readonly ILogger<MojangJavaRequirementSource> _logger = logger;

    private string VersionsDirectory => Path.Combine(paths.RootDirectory, "minecraft", "versions");

    public async Task<int?> GetRequiredJavaAsync(string minecraftVersion, CancellationToken ct)
    {
        try
        {
            return ReadFromDisk(minecraftVersion) ?? await ReadFromMojangAsync(minecraftVersion, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogUnknownJava(ex, minecraftVersion);
            return null;
        }
    }

    private int? ReadFromDisk(string minecraftVersion)
    {
        var path = Path.Combine(VersionsDirectory, minecraftVersion, $"{minecraftVersion}.json");

        return File.Exists(path) ? Extract(File.ReadAllBytes(path)) : null;
    }

    private async Task<int?> ReadFromMojangAsync(string minecraftVersion, CancellationToken ct)
    {
        using var manifest = JsonDocument.Parse(await http.GetByteArrayAsync(ManifestUrl, ct));

        // ValueKind explícito, e não FirstOrDefault().TryGetProperty(): o default
        // de um JsonElement é Undefined, e perguntar-lhe por uma propriedade
        // ATIRA. Funcionaria — o catch lá fora devolveria nulo — mas registaria
        // "não deu para saber" quando a resposta certa é "a Mojang não conhece
        // esta versão", que é outra coisa e manda investigar noutro sítio.
        var version = manifest.RootElement
            .GetProperty("versions")
            .EnumerateArray()
            .FirstOrDefault(v => v.GetProperty("id").GetString() == minecraftVersion);

        if (version.ValueKind is not JsonValueKind.Object || !version.TryGetProperty("url", out var url))
        {
            // Acontece com snapshots e com um pack publicado com a versão escrita
            // à mão errada.
            LogUnknownVersion(minecraftVersion);
            return null;
        }

        return Extract(await http.GetByteArrayAsync(url.GetString()!, ct));
    }

    /// <summary>
    ///     O <c>javaVersion.majorVersion</c>, se existir.
    ///     Versões antigas do Minecraft não declaram este bloco — nasceram antes
    ///     de a Mojang gerir o Java —, e para elas não saber é a resposta honesta.
    /// </summary>
    private static int? Extract(byte[] json)
    {
        using var documento = JsonDocument.Parse(json);

        return documento.RootElement.TryGetProperty("javaVersion", out var java)
               && java.TryGetProperty("majorVersion", out var major)
            ? major.GetInt32()
            : null;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "A Mojang não conhece a versão {Version}; o Java virá do palpite.")]
    private partial void LogUnknownVersion(string version);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Não foi possível saber que Java a versão {Version} pede; usando o palpite.")]
    private partial void LogUnknownJava(Exception ex, string version);
}
