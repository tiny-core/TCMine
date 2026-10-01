using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Web.Configuration;

/// <summary>
///     De onde sai o client id do Azure: painel primeiro, appsettings depois.
///     A ordem não é arbitrária. Registrar a app no Azure acontece DEPOIS do
///     deploy, então o lugar natural do valor é a tela de configurações — e
///     lido daqui, a mudança vale na próxima tentativa de login, sem reiniciar
///     o processo com jogadores conectados. O appsettings continua valendo
///     como semente para que instalação já configurada por arquivo (ou por
///     variável de ambiente, no Docker) siga de pé sem ninguém tocar em nada.
///     Compartilhado entre o handshake (launcher) e o login do painel — os
///     dois precisam do mesmo valor, e divergir seria dois apps Azure
///     diferentes atendendo a mesma instalação.
/// </summary>
public static class AzureClientIdResolver
{
    public static async Task<string> ResolveAsync(ISettingsRepository settings, string fromFile, CancellationToken ct)
    {
        var stored = (await settings.GetAsync(ct)).AzureClientId;

        return string.IsNullOrWhiteSpace(stored) ? fromFile : stored;
    }
}
