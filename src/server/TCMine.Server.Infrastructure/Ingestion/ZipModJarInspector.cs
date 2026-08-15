using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Infrastructure.Ingestion;

/// <summary>
///     Lê a exigência de loader de dentro do .jar.
///     Um jar é um zip; os três formatos que interessam são arquivos de texto na
///     raiz ou em META-INF. O TOML é lido por expressão regular em vez de um
///     parser completo — a única coisa que se quer dali é o
///     intervalo declarado no bloco de dependência cujo modId é o loader, e
///     trazer uma dependência de TOML para o projeto por duas linhas não se paga.
///     Se a leitura falhar por qualquer motivo, devolve null: quem chama trata
///     ausência de informação como "pode instalar".
/// </summary>
public sealed partial class ZipModJarInspector : IModJarInspector
{
    public async Task<ModJarInfo?> InspectAsync(Stream jar, CancellationToken ct)
    {
        try
        {
            // O ZipArchive precisa de seek; um stream de rede não tem.
            var buffer = jar;
            if (!jar.CanSeek)
            {
                buffer = new MemoryStream();
                await jar.CopyToAsync(buffer, ct);
                buffer.Position = 0;
            }

            using var archive = new ZipArchive(buffer, ZipArchiveMode.Read, true);

            if (Entry(archive, "fabric.mod.json") is { } fabric)
                return await ReadFabricAsync(fabric, ct);

            var toml = Entry(archive, "META-INF/neoforge.mods.toml")
                       ?? Entry(archive, "META-INF/mods.toml");

            return toml is null ? null : await ReadTomlAsync(toml, ct);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or JsonException)
        {
            // Jar corrompido, comprimido de forma exótica, ou nem jar é.
            return null;
        }
    }

    private static ZipArchiveEntry? Entry(ZipArchive archive, string name) =>
        archive.Entries.FirstOrDefault(e =>
            e.FullName.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static async Task<ModJarInfo?> ReadFabricAsync(ZipArchiveEntry entry, CancellationToken ct)
    {
        await using var stream = entry.Open();
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var raiz = doc.RootElement;
        var modId = raiz.TryGetProperty("id", out var id) ? id.GetString() : null;

        if (!raiz.TryGetProperty("depends", out var depends)
            || depends.ValueKind is not JsonValueKind.Object
            || !depends.TryGetProperty("fabricloader", out var loader))
            return new ModJarInfo(modId, null);

        // Pode ser string ("&gt;=0.15.0") ou array de alternativas; com array,
        // qualquer uma serve, e verificar "alguma passa" daria falso negativo —
        // então não checamos.
        return new ModJarInfo(modId, loader.ValueKind is JsonValueKind.String ? loader.GetString() : null);
    }

    private static async Task<ModJarInfo?> ReadTomlAsync(ZipArchiveEntry entry, CancellationToken ct)
    {
        await using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        var texto = await reader.ReadToEndAsync(ct);

        var modId = ModIdPattern().Match(texto) is { Success: true } m ? m.Groups["id"].Value : null;

        // Procura o bloco de dependência do loader e o versionRange que vem
        // logo depois dele. A ordem das chaves dentro do bloco é convenção
        // firme nos dois loaders.
        var loader = LoaderDependencyPattern().Match(texto);

        return new ModJarInfo(modId, loader.Success ? loader.Groups["range"].Value : null);
    }

    [GeneratedRegex("""modId\s*=\s*"(?<id>[^"]+)"\s*\r?\n\s*version""", RegexOptions.IgnoreCase)]
    private static partial Regex ModIdPattern();

    [GeneratedRegex(
        """modId\s*=\s*"(?:neoforge|forge)"(?:[^\[]*?)versionRange\s*=\s*"(?<range>[^"]+)""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex LoaderDependencyPattern();
}
