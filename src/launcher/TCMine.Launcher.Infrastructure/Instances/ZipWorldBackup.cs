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
///     Guarda as últimas <see cref="WorldBackupRetention.Keep" /> e deixa cair as
///     mais antigas. A decisão de quantas é do Core; aqui só se apaga ficheiro.
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

        var folder = Path.Combine(paths.RootDirectory, "backups", key.Id);

        Directory.CreateDirectory(folder);

        // Data e hora no nome, em UTC e ordenável: o jogador vai olhar para uma
        // lista destes ficheiros no dia em que precisar, e precisa de perceber
        // qual é o mais recente sem abrir nenhum.
        var target = Path.Combine(folder, $"saves-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.zip");

        // Para um temporário e só depois move: uma queda a meio deixaria um .zip
        // truncado com nome de backup bom, que é pior do que não ter backup —
        // descobre-se no dia em que se tenta restaurar.
        var temporary = target + ".tmp";

        try
        {
            await Task.Run(
                () => ZipFile.CreateFromDirectory(saves, temporary, CompressionLevel.Fastest, false),
                ct);

            File.Move(temporary, target, true);
        }
        catch
        {
            if (File.Exists(temporary))
                File.Delete(temporary);

            throw;
        }

        LogCreated(target);

        // Depois de a nova estar no lugar, nunca antes: podar primeiro e falhar
        // a criar deixaria o jogador com menos cópias do que tinha, por causa de
        // uma atualização que nem aconteceu.
        Prune(folder);

        return target;
    }

    /// <summary>
    ///     Apaga as cópias que sobram.
    ///     Falhar aqui não derruba nada: a cópia que interessa já está gravada, e
    ///     recusar a atualização porque não se conseguiu apagar um ficheiro
    ///     ANTIGO seria trocar um problema de disco por um impedimento.
    /// </summary>
    private void Prune(string folder)
    {
        try
        {
            var names = Directory.EnumerateFiles(folder, "saves-*.zip").Select(Path.GetFileName).OfType<string>();

            foreach (var velho in WorldBackupRetention.Expired(names))
            {
                File.Delete(Path.Combine(folder, velho));
                LogPruned(velho);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogPruneFailed(ex);
        }
    }

    private string SavesDirectory(InstanceKey key) => Path.Combine(instances.PathFor(key), "saves");

    [LoggerMessage(Level = LogLevel.Information, Message = "Cópia do mundo criada em {Path}.")]
    private partial void LogCreated(string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Cópia antiga removida: {Name}.")]
    private partial void LogPruned(string name);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Não foi possível remover cópias antigas; a nova está gravada.")]
    private partial void LogPruneFailed(Exception ex);
}
