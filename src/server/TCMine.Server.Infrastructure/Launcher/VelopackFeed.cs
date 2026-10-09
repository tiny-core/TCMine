using System.Text.Json;
using System.Text.Json.Serialization;
using TCMine.Server.Application.Updates;

namespace TCMine.Server.Infrastructure.Launcher;

/// <summary>
///     O bastante do formato do feed do Velopack para o servidor responder duas
///     perguntas: qual a versão mais nova publicada num canal, e qual arquivo é
///     o instalador. Não referenciamos o pacote Velopack aqui — empacotamento de
///     app desktop não é dependência que o servidor deva carregar.
///     Os nomes e formatos são os do <c>vpk</c> 1.2 (conferidos na saída real):
///     <c>releases.{canal}.json</c> lista os pacotes Full/Delta com a versão, e
///     <c>assets.{canal}.json</c> lista os arquivos com o tipo POR EXTENSO
///     ("Installer"). O <c>RELEASES-{canal}</c> é o formato texto antigo do
///     Squirrel, mantido só para clientes velhos — lê-lo como JSON (o que o
///     código antigo fazia) nunca achava o instalador.
/// </summary>
public static class VelopackFeed
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static string ChannelDirectory(string root, string channel) => Path.Combine(root, channel);

    /// <summary>A maior versão com pacote Full no canal, ou nulo quando o canal está vazio/ilegível.</summary>
    public static string? LatestVersion(string channelDir, string channel)
    {
        var path = Path.Combine(channelDir, $"releases.{channel}.json");
        if (!File.Exists(path))
            return null;

        try
        {
            var feed = JsonSerializer.Deserialize<ReleasesFile>(File.ReadAllText(path), Json);

            string? best = null;
            SemanticVersion bestVersion = default;

            foreach (var asset in feed?.Assets ?? [])
            {
                if (!string.Equals(asset.Type, "Full", StringComparison.OrdinalIgnoreCase)
                    || !SemanticVersion.TryParse(asset.Version, out var version))
                    continue;

                if (best is null || version > bestVersion)
                {
                    best = asset.Version;
                    bestVersion = version;
                }
            }

            return best;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>O nome do instalador (Setup.exe) do canal, ou nulo.</summary>
    public static string? InstallerFileName(string channelDir, string channel)
    {
        var path = Path.Combine(channelDir, $"assets.{channel}.json");
        if (!File.Exists(path))
            return null;

        try
        {
            var assets = JsonSerializer.Deserialize<AssetEntry[]>(File.ReadAllText(path), Json);

            var installer = assets?.FirstOrDefault(a =>
                string.Equals(a.Type, "Installer", StringComparison.OrdinalIgnoreCase));

            return installer?.RelativeFileName is { Length: > 0 } name
                   && File.Exists(Path.Combine(channelDir, name))
                ? name
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    ///     O primeiro caminho do canal em que este processo não consegue
    ///     escrever (a própria pasta ou um arquivo dela), ou <c>null</c>. O vpk
    ///     sobrescreve o instalador e os índices, e apaga o canal ao refazê-lo.
    /// </summary>
    public static string? FindUnwritable(string channelDir)
    {
        var probe = Path.Combine(channelDir, $".tcmine-probe-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            return channelDir;
        }

        foreach (var file in Directory.EnumerateFiles(channelDir))
        {
            try
            {
                // Abrir para escrita sem truncar: confere a permissão sem tocar no conteúdo.
                using var _ = File.OpenHandle(file, FileMode.Open, FileAccess.Write);
            }
            catch (Exception e) when (e is UnauthorizedAccessException or IOException)
            {
                return file;
            }
        }

        return null;
    }

    private sealed record ReleasesFile([property: JsonPropertyName("Assets")] ReleaseAsset[]? Assets);

    private sealed record ReleaseAsset(
        [property: JsonPropertyName("Version")]
        string? Version,
        [property: JsonPropertyName("Type")] string? Type);

    private sealed record AssetEntry(
        [property: JsonPropertyName("RelativeFileName")]
        string? RelativeFileName,
        [property: JsonPropertyName("Type")] string? Type);
}
