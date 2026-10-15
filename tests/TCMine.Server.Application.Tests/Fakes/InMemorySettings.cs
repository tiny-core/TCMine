using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Settings;

namespace TCMine.Server.Application.Tests.Fakes;

/// <summary>Configurações em memória: devolve sempre a mesma instância, e o que se grava fica nela.</summary>
public sealed class InMemorySettings(InstallationSettings? initial = null) : ISettingsRepository
{
    public InstallationSettings Current { get; private set; } = initial ?? new InstallationSettings();

    public Task<InstallationSettings> GetAsync(CancellationToken ct) => Task.FromResult(Current);

    public Task SaveAsync(InstallationSettings settings, CancellationToken ct)
    {
        Current = settings;
        return Task.CompletedTask;
    }

    public Task<string?> GetCurseForgeApiKeyAsync(CancellationToken ct) =>
        Task.FromResult(Current.CurseForgeApiKeyEncrypted);
}
