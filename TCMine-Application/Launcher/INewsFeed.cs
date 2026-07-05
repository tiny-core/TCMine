using TCMine_Application.Contracts;

namespace TCMine_Application.Launcher;

/// <summary>Feed de novidades do servidor (globais + de modpacks), consumido pelo launcher.</summary>
public interface INewsFeed
{
    Task<IReadOnlyList<NewsItemDto>> GetNewsAsync(CancellationToken ct = default);
}