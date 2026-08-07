using TCMine.Server.Domain.Settings;

namespace TCMine.Server.Application.Abstractions;

/// <summary>
///     Acesso à configuração da instalação (linha única).
///     Os segredos (chave do CurseForge, senha de SMTP) trafegam aqui em claro e
///     são cifrados pela implementação ao gravar — proteger em repouso é
///     responsabilidade da persistência, não de quem usa o valor.
/// </summary>
public interface ISettingsRepository
{
    /// <summary>Devolve a configuração, criando a linha padrão se ainda não existir.</summary>
    Task<InstallationSettings> GetAsync(CancellationToken ct);

    Task SaveAsync(InstallationSettings settings, CancellationToken ct);

    /// <summary>Chave do CurseForge em claro, ou nulo se não configurada.</summary>
    Task<string?> GetCurseForgeApiKeyAsync(CancellationToken ct);

    /// <summary>Senha de SMTP em claro, ou nulo se não configurada.</summary>
    Task<string?> GetSmtpPasswordAsync(CancellationToken ct);
}
