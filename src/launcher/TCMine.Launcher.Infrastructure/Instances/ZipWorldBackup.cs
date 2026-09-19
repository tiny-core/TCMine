using System.IO.Compression;
using Microsoft.Extensions.Logging;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Sync;

namespace TCMine.Launcher.Infrastructure.Instances;

/// <summary>
///     Os mundos de uma instância, num .zip datado.
///     Fora da pasta da instância, em <c>{raiz}/backups/{instância}/</c>: é o
///     instalador que reescreve aquela pasta, e guardar a cópia lá dentro seria
///     pô-la no único sítio que a operação seguinte pode mexer. É a mesma razão
///     pela qual o servidor guarda os snapshots dele fora da pasta que o
///     materializador reescreve.
///     Nada é apagado aqui. Uma política de retenção precisa de decidir o que
///     perder, e essa decisão não se toma em silêncio no meio de uma atualização.
/// </summary>
public sealed partial class ZipWorldBackup(
    IInstanceStore instances,
    LauncherPaths paths,
    ILogger<ZipWorldBackup> logger) : IWorldBackup
{
    private readonly ILogger<ZipWorldBackup> _logger = logger;

    public bool HasWorld(InstanceKey key)
    {
        var saves = SavesDirectory(key);

        // Pasta vazia não conta: o Minecraft cria saves/ no primeiro arranque,
        // mesmo que o jogador saia do menu sem criar mundo nenhum. Oferecer
        // backup disso ensinaria a ignorar o aviso.
        return Directory.Exists(saves) && Directory.EnumerateFileSystemEntries(saves).Any();
    }

    public async Task<string> CreateAsync(InstanceKey key, CancellationToken ct)
    {
        var saves = SavesDirectory(key);

        if (!Directory.Exists(saves))
            throw new InvalidOperationException("Esta instância não tem mundos para copiar.");

        var pasta = Path.Combine(paths.RootDirectory, "backups", key.Id);

        Directory.CreateDirectory(pasta);

        // Data e hora no nome, em UTC e ordenável: o jogador vai olhar para uma
        // lista destes ficheiros no dia em que precisar, e precisa de perceber
        // qual é o mais recente sem abrir nenhum.
        var destino = Path.Combine(pasta, $"saves-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.zip");

        // Para um temporário e só depois move: uma queda a meio deixaria um .zip
        // truncado com nome de backup bom, que é pior do que não ter backup —
        // descobre-se no dia em que se tenta restaurar.
        var temporario = destino + ".tmp";

        try
        {
            await Task.Run(
                () => ZipFile.CreateFromDirectory(saves, temporario, CompressionLevel.Fastest, false),
                ct);

            File.Move(temporario, destino, true);
        }
        catch
        {
            if (File.Exists(temporario))
                File.Delete(temporario);

            throw;
        }

        LogCriado(destino);

        return destino;
    }

    private string SavesDirectory(InstanceKey key) => Path.Combine(instances.PathFor(key), "saves");

    [LoggerMessage(Level = LogLevel.Information, Message = "Cópia do mundo criada em {Caminho}.")]
    private partial void LogCriado(string caminho);
}
