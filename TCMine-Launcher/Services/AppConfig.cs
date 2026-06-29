using System.Reflection;

namespace TCMine_Launcher.Services;

/// <summary>
/// Configuração embutida no binário em tempo de compilação (NÃO editável pelo utilizador, NÃO
/// hardcoded no código): a URL do TCMine Server.
///
/// Origem do valor (vira um <see cref="AssemblyMetadataAttribute"/> — ver o .csproj):
/// <list type="bullet">
///   <item>PRODUÇÃO: o servidor compila o launcher e injeta a sua URL/IP no publish
///         (<c>-p:TcmineServerUrl=https://meu-servidor</c>).</item>
///   <item>DEV: vem de <c>Client.props</c> (fora do git; ver <c>Client.props.example</c>), ou cai no
///         fallback de dev do <see cref="ServerConfig"/> quando ausente.</item>
/// </list>
/// O URL não é segredo (é público). Trocá-lo apontaria o launcher a outro servidor — por isso é
/// fixado no build, não configurável em runtime.
/// </summary>
public static class AppConfig
{
    /// <summary>URL base do servidor TCMine injetada no build, ou null se não configurada.</summary>
    public static string? ServerUrl { get; } = Read("TcmineServerUrl");

    /// <summary>Azure client id (login MSAL) injetado no build, ou null se não configurado.</summary>
    public static string? MicrosoftClientId { get; } = Read("MicrosoftClientId");

    private static string? Read(string key)
    {
        return Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == key)?.Value is { Length: > 0 } value
            ? value.Trim()
            : null;
    }
}
