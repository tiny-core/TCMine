using TCMine.Contracts.Hubs;
using TCMine.Server.Domain.Settings;

namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Acesso à configuração da instalação (linha única).
///     O segredo (chave do CurseForge) trafega aqui em claro e
///     é cifrado pela implementação ao gravar — proteger em repouso é
///     responsabilidade da persistência, não de quem usa o valor.
/// </summary>
public interface ISettingsRepository
{
    /// <summary>Devolve a configuração, criando a linha padrão se ainda não existir.</summary>
    Task<InstallationSettings> GetAsync(CancellationToken ct);

    [Invalidates(ResourceKind.Servers)] // o endereço público entra no endereço de cada servidor
    Task SaveAsync(InstallationSettings settings, CancellationToken ct);

    /// <summary>Chave do CurseForge em claro, ou nulo se não configurada.</summary>
    Task<string?> GetCurseForgeApiKeyAsync(CancellationToken ct);
}
