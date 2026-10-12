namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Onde o TCMine Server descobre que saiu uma versão nova de si mesmo.
///     A implementação consulta as releases do repositório; a Application só
///     precisa de "qual é a mais nova estável" e de saber se a consulta deu
///     certo — uma consulta de cortesia nunca pode derrubar o painel.
/// </summary>
public interface IServerReleaseFeed
{
    /// <summary>
    ///     A estável mais nova. Sem <paramref name="refresh" /> a resposta pode vir
    ///     do cache; com ele, consulta agora (o botão "Verificar agora").
    /// </summary>
    Task<ServerReleaseLookup> LookupAsync(bool refresh, CancellationToken ct);
}

/// <summary>Uma release publicada do servidor (ex.: "0.5.0").</summary>
public sealed record ServerRelease(string Version, Uri Page, DateTimeOffset PublishedAt);

/// <summary>
///     O resultado de uma consulta. <see cref="Latest" /> nulo com
///     <see cref="ReleaseFeedState.Ok" /> quer dizer que não há release estável
///     nenhuma; com <see cref="ReleaseFeedState.Unreachable" />, que não deu
///     para saber. Os dois casos pedem frases diferentes na tela.
/// </summary>
public sealed record ServerReleaseLookup(ReleaseFeedState State, ServerRelease? Latest, DateTimeOffset CheckedAt);

public enum ReleaseFeedState
{
    Ok,

    /// <summary>Rede, GitHub fora do ar ou limite de consultas.</summary>
    Unreachable,

    /// <summary>Desligado na configuração (Updates__Enabled=false).</summary>
    Disabled
}
