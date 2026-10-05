namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Onde o TCMine Server descobre que saiu uma versão nova de si mesmo.
///     A implementação consulta as releases do repositório; a Application só
///     precisa de "qual é a mais nova estável", e de nulo quando não deu para
///     saber — uma consulta de cortesia nunca pode derrubar o painel.
/// </summary>
public interface IServerReleaseFeed
{
    Task<ServerRelease?> GetLatestStableAsync(CancellationToken ct);
}

/// <summary>Uma release publicada do servidor (ex.: "0.5.0").</summary>
public sealed record ServerRelease(string Version, Uri Page, DateTimeOffset PublishedAt);
