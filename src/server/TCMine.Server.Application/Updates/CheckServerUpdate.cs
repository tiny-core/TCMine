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
    /// <summary>Para o aviso no topo do painel: pode responder do cache.</summary>
    /// <param name="currentVersion">A versão desta build ("dev" fora de uma imagem publicada).</param>
    /// <returns>A release mais nova quando ela é MAIOR que a atual; senão, nulo.</returns>
    public async Task<ServerRelease?> HandleAsync(string currentVersion, CancellationToken ct)
    {
        var status = await CheckAsync(currentVersion, refresh: false, ct);
        return status is { State: ServerUpdateState.Available } ? status.Latest : null;
    }

    /// <summary>
    ///     O estado completo, para a tela de Configurações. Com
    ///     <paramref name="refresh" />, ignora o cache: é o "Verificar agora" de
    ///     quem acabou de publicar e não quer esperar a próxima consulta.
    /// </summary>
    /// <returns>Nulo para quem não é admin da instalação.</returns>
    public async Task<ServerUpdateStatus?> CheckAsync(string currentVersion, bool refresh, CancellationToken ct)
    {
        if (!scope.IsInstanceAdmin)
            return null;

        var lookup = await feed.LookupAsync(refresh, ct);

        if (lookup.State is ReleaseFeedState.Disabled)
            return new ServerUpdateStatus(ServerUpdateState.Disabled, currentVersion, null, null);

        if (lookup.State is ReleaseFeedState.Unreachable)
            return new ServerUpdateStatus(ServerUpdateState.Unreachable, currentVersion, null, lookup.CheckedAt);

        // Build local não tem número que se compare: avisar "há a 0.5.0" a quem
        // está depurando o código-fonte seria mentira nos dois sentidos. A tela
        // ainda mostra qual é a publicada.
        if (!SemanticVersion.TryParse(currentVersion, out var current))
            return new ServerUpdateStatus(ServerUpdateState.LocalBuild, currentVersion, lookup.Latest, lookup.CheckedAt);

        var newer = lookup.Latest is { } latest
                    && SemanticVersion.TryParse(latest.Version, out var available)
                    && available > current;

        return new ServerUpdateStatus(
            newer ? ServerUpdateState.Available : ServerUpdateState.UpToDate,
            currentVersion, lookup.Latest, lookup.CheckedAt);
    }
}

/// <summary>O que a tela diz sobre a versão desta instalação.</summary>
/// <param name="CheckedAt">Quando o GitHub respondeu (pode ser do cache); nulo se a consulta está desligada.</param>
public sealed record ServerUpdateStatus(
    ServerUpdateState State,
    string CurrentVersion,
    ServerRelease? Latest,
    DateTimeOffset? CheckedAt);

public enum ServerUpdateState
{
    UpToDate,
    Available,
    Unreachable,
    Disabled,
    LocalBuild
}

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
