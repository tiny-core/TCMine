using System.Text.Json;
using Microsoft.Extensions.Logging;
using TCMine.Launcher.Core.Connectivity;

namespace TCMine.Launcher.Infrastructure.Configuration;

/// <summary>
///     Lê o <c>server.json</c> que o TCMine Server põe ao lado do executável
///     quando empacota o launcher (<c>{"url": "https://..."}</c>).
///     Mora na pasta do APP, e não na raiz como o tcmine.json, de propósito: o
///     Velopack substitui essa pasta a cada atualização com o pacote que veio do
///     mesmo servidor, então o arquivo acompanha a instalação sem ninguém
///     gravá-lo. Lido uma vez — o conteúdo não muda com o processo rodando.
/// </summary>
public sealed partial class BundledServerAddress(ILogger<BundledServerAddress> logger) : IBundledServerAddress
{
    public const string FileName = "server.json";

    private readonly ILogger<BundledServerAddress> _logger = logger;
    private readonly Lazy<string?> _address = new(() => Read(AppContext.BaseDirectory, logger));

    public string? Get() => _address.Value;

    /// <summary>O endereço do arquivo em <paramref name="directory" />, ou nulo.</summary>
    public static string? Read(string directory, ILogger? logger = null)
    {
        var path = Path.Combine(directory, FileName);
        if (!File.Exists(path))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));

            return doc.RootElement.TryGetProperty("url", out var url)
                   && url.GetString() is { Length: > 0 } value
                ? value
                : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException
                                       or InvalidOperationException)
        {
            // Ilegível = launcher genérico: o jogador digita o endereço, como
            // sempre foi. Nunca motivo para não abrir.
            if (logger is not null)
                LogIlegivel(logger, ex, path);
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "server.json ilegível em {Path}; o pareamento será manual.")]
    private static partial void LogIlegivel(ILogger logger, Exception ex, string path);
}
