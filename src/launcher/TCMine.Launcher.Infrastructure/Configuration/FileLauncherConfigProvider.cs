using System.Text.Json;
using Microsoft.Extensions.Logging;
using TCMine.Contracts;
using TCMine.Contracts.Serialization;
using TCMine.Launcher.Core.Connectivity;

namespace TCMine.Launcher.Infrastructure.Configuration;

/// <summary>
///     Lê e grava o tcmine.json.
///     O arquivo mora na RAIZ da instalação, um nível acima da pasta que o
///     Velopack substitui a cada update. Se ficasse dentro dela, sumiria no
///     primeiro autoupdate e o launcher esqueceria a qual servidor pertence.
/// </summary>
public sealed partial class FileLauncherConfigProvider(
    LauncherPaths paths,
    ILogger<FileLauncherConfigProvider> logger) : ILauncherConfigProvider
{
    private readonly ILogger<FileLauncherConfigProvider> _logger = logger;

    private string ConfigPath => Path.Combine(paths.RootDirectory, "tcmine.json");

    public async Task<LauncherConfig?> TryLoadAsync(CancellationToken ct)
    {
        if (!File.Exists(ConfigPath))
        {
            LogConfigNotFound(ConfigPath);
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(ConfigPath);

            var config = await JsonSerializer.DeserializeAsync(
                stream, TcMineJsonContext.Default.LauncherConfig, ct);

            if (config is null)
                return null;

            var erros = config.Validate();

            if (erros.Count > 0)
            {
                // Configuração inválida é tratada como ausente: a UI cai na
                // tela de pareamento manual em vez de tentar conectar num
                // endereço que sabemos estar errado.
                LogConfigInvalid(string.Join("; ", erros));
                return null;
            }

            return config;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // Arquivo corrompido ou em quarentena pelo antivírus. Não é
            // motivo para o launcher travar — a tela manual resolve.
            LogConfigUnreadable(ex, ConfigPath);
            return null;
        }
    }

    public async Task SaveAsync(LauncherConfig config, CancellationToken ct)
    {
        var erros = config.Validate();

        if (erros.Count > 0)
            throw new ArgumentException($"Configuração inválida: {string.Join("; ", erros)}");

        Directory.CreateDirectory(paths.RootDirectory);

        // Grava em temporário e move: uma queda no meio da escrita deixaria
        // um JSON truncado, e aí nem a próxima inicialização funcionaria.
        var temp = ConfigPath + ".tmp";

        await using (var stream = File.Create(temp))
        {
            await JsonSerializer.SerializeAsync(
                stream, config, TcMineJsonContext.Default.LauncherConfig, ct);
        }

        File.Move(temp, ConfigPath, true);

        LogConfigSaved(config.ServerUrl);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Configuração não encontrada em {Path}.")]
    private partial void LogConfigNotFound(string path);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Configuração inválida: {Erros}")]
    private partial void LogConfigInvalid(string erros);

    [LoggerMessage(Level = LogLevel.Error, Message = "Não foi possível ler a configuração em {Path}.")]
    private partial void LogConfigUnreadable(Exception ex, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Configuração salva apontando para {ServerUrl}.")]
    private partial void LogConfigSaved(Uri serverUrl);
}