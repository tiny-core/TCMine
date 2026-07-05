using TCMine_Launcher.Infrastructure.Updates;

namespace TCMine_Launcher.Infrastructure.Configuration;

/// <summary>
/// Aponta o launcher para o TCMine Server (catálogo, login, jars). A URL vem injetada no build
/// (<see cref="AppConfig.ServerUrl"/>); sem valor (dev), cai no servidor local.
///
/// <b>Normalização defensiva:</b> a URL injetada é validada no arranque — sem esquema assume-se
/// <c>https://</c>; se ainda assim for inválida, cai no fallback de dev. Antes, uma URL malformada
/// (ex.: sem <c>http(s)://</c>) fazia <c>new Uri(BaseUrl)</c> lançar <see cref="System.UriFormatException"/>
/// no construtor do <see cref="UpdateService"/> e o launcher <b>fechava no boot, sem UI</b>.
/// </summary>
public sealed class ServerConfig
{
    private const string DevFallback = "https://localhost:7002";

    private readonly Uri _base;

    public ServerConfig() => _base = TryParseBase(AppConfig.ServerUrl) ?? new Uri(DevFallback);

    /// <summary>URL base absoluta do servidor, já normalizada.</summary>
    public string BaseUrl => _base.ToString();

    /// <summary>Resolve um caminho relativo contra a base (ex.: <c>/updates</c>).</summary>
    public Uri Resolve(string path) => new(_base, path);

    /// <summary>Absoluta http(s)? Sem esquema, assume https. Inválida → null (caller usa o fallback).</summary>
    private static Uri? TryParseBase(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        var s = url.Trim();
        if (!s.Contains("://", StringComparison.Ordinal)) s = "https://" + s;

        return Uri.TryCreate(s, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? uri
            : null;
    }
}
