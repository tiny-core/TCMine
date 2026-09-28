using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using TCMine.Contracts;
using TCMine.Server.Application.Public;

namespace TCMine.Server.Infrastructure.Versions;

/// <summary>
///     Lê o RELEASES do canal atual da mesma pasta que LauncherUpdateEndpoints
///     já serve. O nome do arquivo (RELEASES-{canal}) e o formato JSON dentro
///     dele (VelopackAssetFeed) são do Velopack — não referenciamos o pacote
///     aqui, só o bastante do formato para achar o instalador, para não puxar
///     uma dependência de empacotamento de app desktop para o servidor.
/// </summary>
public sealed class FileSystemLauncherReleaseSource(IConfiguration configuration) : ILauncherReleaseSource
{
    /// <summary>
    ///     4 é Velopack.VelopackAssetType.Installer, confirmado serializando um
    ///     VelopackAssetFeed de verdade — o JSON grava o inteiro do enum, não o
    ///     nome. Full/Delta/Portable/Msi (os outros valores) são pacotes de
    ///     atualização incremental para quem já tem o launcher instalado, não
    ///     para um primeiro download.
    /// </summary>
    private const int InstallerAssetType = 4;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    ///     Mesma chave de configuração que LauncherUpdateEndpoints usa (Web,
    ///     que esta camada não pode referenciar — Infrastructure não conhece
    ///     Web). Repetida como literal de propósito, não importada de lá.
    /// </summary>
    private const string RootKey = "LauncherUpdates:RootPath";

    public Task<LauncherReleaseInfo?> GetLatestAsync(CancellationToken ct)
    {
        var raiz = configuration[RootKey];
        if (string.IsNullOrWhiteSpace(raiz))
            return Task.FromResult<LauncherReleaseInfo?>(null);

        var channel = $"win-x64-p{Protocol.Current}";
        var pastaCanal = Path.Combine(raiz, channel);

        // "RELEASES-{canal}" é o nome que o Velopack usa para qualquer canal
        // que não seja o padrão (o padrão, sem nome, seria só "RELEASES") — o
        // TCMine sempre publica com canal, então é sempre a forma com sufixo.
        var manifestPath = Path.Combine(pastaCanal, $"RELEASES-{channel}");
        if (!File.Exists(manifestPath))
            return Task.FromResult<LauncherReleaseInfo?>(null);

        try
        {
            var json = File.ReadAllText(manifestPath);
            var feed = JsonSerializer.Deserialize<RawFeed>(json, JsonOptions);

            var instalador = feed?.Assets?.FirstOrDefault(a => a.Type == InstallerAssetType);
            if (instalador?.FileName is not { Length: > 0 } fileName || instalador.Version is not { } v)
                return Task.FromResult<LauncherReleaseInfo?>(null);

            var versionText = v.Release is { Length: > 0 }
                ? $"{v.Major}.{v.Minor}.{v.Patch}-{v.Release}"
                : $"{v.Major}.{v.Minor}.{v.Patch}";

            var downloadUrl = $"/updates/launcher/{channel}/{fileName}";

            return Task.FromResult<LauncherReleaseInfo?>(new LauncherReleaseInfo(versionText, downloadUrl));
        }
        catch (JsonException)
        {
            // Manifesto ilegível (publicação no meio, formato mudou): sem
            // download em vez de derrubar a página pública inteira por isso.
            return Task.FromResult<LauncherReleaseInfo?>(null);
        }
    }

    private sealed record RawVersion(
        [property: JsonPropertyName("Major")] int Major,
        [property: JsonPropertyName("Minor")] int Minor,
        [property: JsonPropertyName("Patch")] int Patch,
        [property: JsonPropertyName("Release")] string? Release);

    private sealed record RawAsset(
        [property: JsonPropertyName("FileName")] string FileName,
        [property: JsonPropertyName("Type")] int Type,
        [property: JsonPropertyName("Version")] RawVersion? Version);

    private sealed record RawFeed(
        [property: JsonPropertyName("Assets")] RawAsset[]? Assets);
}
