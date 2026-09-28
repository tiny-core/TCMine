namespace TCMine.Server.Application.Public;

/// <summary>A versão mais nova publicada do launcher para o canal atual, pronta para exibir e baixar.</summary>
public sealed record LauncherReleaseInfo(string Version, string DownloadUrl);

/// <summary>
///     Lê o feed que o próprio Velopack já publica em
///     <c>/updates/launcher/{canal}/</c> (ver LauncherUpdateEndpoints) — o
///     TCMine não inventa outro formato, só interpreta o que o <c>vpk</c> já
///     escreveu, para achar o instalador (não os pacotes de update
///     incremental) e mostrar um link de download na página pública.
/// </summary>
public interface ILauncherReleaseSource
{
    /// <summary>Nulo quando não há nada publicado ainda para este canal — instalação nova, sem release.</summary>
    Task<LauncherReleaseInfo?> GetLatestAsync(CancellationToken ct);
}
