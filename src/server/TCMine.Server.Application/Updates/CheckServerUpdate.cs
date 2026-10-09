using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Application.Updates;

/// <summary>
///     Diz ao admin da instalação que há versão nova do TCMine Server.
///     Existe porque publicar a imagem não basta: o container continua rodando
///     a antiga até alguém fazer o pull, e pelo painel não havia como perceber.
///     Só o admin da instalação vê — é ele quem pode atualizar, e para os outros
///     o aviso seria ruído sem ação possível.
/// </summary>
public sealed class CheckServerUpdate(IServerReleaseFeed feed, ICurrentUserScope scope)
{
    /// <param name="currentVersion">A versão desta build ("dev" fora de uma imagem publicada).</param>
    /// <returns>A release mais nova quando ela é MAIOR que a atual; senão, nulo.</returns>
    public async Task<ServerRelease?> HandleAsync(string currentVersion, CancellationToken ct)
    {
        if (!scope.IsInstanceAdmin)
            return null;

        // Build local não tem número que se compare: avisar "há a 0.5.0" a quem
        // está depurando o código-fonte seria mentira nos dois sentidos.
        if (!SemanticVersion.TryParse(currentVersion, out var current))
            return null;

        var latest = await feed.GetLatestStableAsync(ct);
        if (latest is null || !SemanticVersion.TryParse(latest.Version, out var available))
            return null;

        return available > current ? latest : null;
    }
}

/// <summary>
///     O bastante de SemVer para ordenar versões do TCMine: núcleo numérico e
///     pré-lançamento (que vem ANTES da estável de mesmo núcleo: 0.5.0-beta.1 &lt; 0.5.0).
///     Metadado de build (+sha) é ignorado, como manda a especificação.
/// </summary>
public readonly record struct SemanticVersion(int Major, int Minor, int Patch, string? PreRelease)
    : IComparable<SemanticVersion>
{
    public int CompareTo(SemanticVersion other)
    {
        var byCore = (Major, Minor, Patch).CompareTo((other.Major, other.Minor, other.Patch));
        if (byCore != 0)
            return byCore;

        return (PreRelease, other.PreRelease) switch
        {
            (null, null) => 0,
            (null, _) => 1, // estável ganha do pré-lançamento de mesmo núcleo
            (_, null) => -1,
            _ => string.CompareOrdinal(PreRelease, other.PreRelease)
        };
    }

    public static bool TryParse(string? text, out SemanticVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var value = text.Trim().TrimStart('v', 'V').Split('+')[0];
        var dash = value.IndexOf('-', StringComparison.Ordinal);
        var core = dash < 0 ? value : value[..dash];
        var pre = dash < 0 ? null : value[(dash + 1)..];

        var parts = core.Split('.');
        if (parts.Length is < 2 or > 3
            || !int.TryParse(parts[0], out var major)
            || !int.TryParse(parts[1], out var minor))
            return false;

        var patch = 0;
        if (parts.Length == 3 && !int.TryParse(parts[2], out patch))
            return false;

        version = new SemanticVersion(major, minor, patch, string.IsNullOrEmpty(pre) ? null : pre);
        return true;
    }

    public static bool operator <(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) < 0;
    public static bool operator <=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) > 0;
    public static bool operator >=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) >= 0;
}
