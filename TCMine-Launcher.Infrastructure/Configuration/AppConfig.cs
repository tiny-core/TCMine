using System.Reflection;

namespace TCMine_Launcher.Infrastructure.Configuration;

/// <summary>
///     Config embutida no binário em build-time (não hardcoded, não editável): URL do servidor e Azure
///     client id. Em produção o servidor injeta via <c>-p:TcmineServerUrl=… -p:MicrosoftClientId=…</c>; em
///     dev vêm de <c>Client.props</c>. Viram <see cref="AssemblyMetadataAttribute" /> (no .csproj do launcher).
/// </summary>
public static class AppConfig
{
    public static string? ServerUrl { get; } = Read("TcmineServerUrl");
    public static string? MicrosoftClientId { get; } = Read("MicrosoftClientId");

    private static string? Read(string key)
    {
        // Lê do assembly de ENTRADA (o launcher), onde os atributos são injetados.
        return (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly())
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == key)?.Value is { Length: > 0 } value
                ? value.Trim()
                : null;
    }
}