using System.Text.Json;
using Microsoft.Extensions.Logging;
using TCMine.Launcher.Core.Abstractions;
using TCMine.Launcher.Core.Sync;
using TCMine.Launcher.Infrastructure.Serialization;

namespace TCMine.Launcher.Infrastructure.Instances;

/// <summary>
///     A escolha do jogador, num ficheiro ao lado do tcmine.json.
///     Separado dele porque são coisas de peso diferente: perder o pareamento
///     manda o jogador digitar o endereço outra vez, perder esta escolha custa um
///     clique. Guardá-las juntas daria à segunda o poder de destruir a primeira.
/// </summary>
public sealed partial class FileActiveInstanceStore(
    LauncherPaths paths,
    ILogger<FileActiveInstanceStore> logger) : IActiveInstanceStore
{
    private readonly ILogger<FileActiveInstanceStore> _logger = logger;

    private string Caminho => Path.Combine(paths.RootDirectory, "active.json");

    public async Task<InstanceKey?> ReadAsync(CancellationToken ct)
    {
        if (!File.Exists(Caminho))
            return null;

        try
        {
            await using var stream = File.OpenRead(Caminho);

            var lido = await JsonSerializer.DeserializeAsync(
                stream, LauncherJsonContext.Default.ActiveInstanceFile, ct);

            // Ficheiro da versão anterior guardava o par (modpack, versão) e não
            // traz InstanceId: sem id, vale como ausente. A consequência é um
            // clique — com uma instância só ela volta a ser a ativa sozinha.
            return lido?.InstanceId is { Length: > 0 } id ? new InstanceKey(id) : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // Ilegível é tratado como ausente, pela mesma razão do manifesto: a
            // tela de jogar continua a abrir, e a escolha reconstrói-se sozinha
            // (ou com um clique). Estourar aqui deixaria o launcher inútil por
            // causa de um ficheiro que existe só para poupar um clique.
            LogUnreadable(ex, Caminho);
            return null;
        }
    }

    public async Task WriteAsync(InstanceKey key, CancellationToken ct)
    {
        Directory.CreateDirectory(paths.RootDirectory);

        // Temporário e move, como no tcmine.json e no manifesto: um ficheiro
        // truncado por uma queda a meio da escrita seria lido como ausente, e o
        // jogador perderia a escolha sem nunca saber porquê.
        var temporary = Caminho + ".tmp";

        await using (var stream = File.Create(temporary))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                new ActiveInstanceFile { InstanceId = key.Id },
                LauncherJsonContext.Default.ActiveInstanceFile,
                ct);
        }

        File.Move(temporary, Caminho, true);
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Escolha de instância ilegível em {Path}; tratada como ausente.")]
    private partial void LogUnreadable(Exception ex, string path);
}

/// <summary>
///     O formato em disco. É um tipo próprio, e não o <see cref="InstanceKey" />,
///     porque aquele é um <c>record struct</c> posicional: serializá-lo
///     directamente prenderia o formato do ficheiro à ordem dos parâmetros do
///     construtor, e trocar essa ordem um dia passaria despercebido.
/// </summary>
public sealed record ActiveInstanceFile
{
    /// <summary>
    ///     Nulo nos ficheiros gravados antes de a instância ter identidade
    ///     própria. Opcional de propósito: recusar o ficheiro inteiro por causa
    ///     de um campo em falta custaria a escolha do jogador sem necessidade.
    /// </summary>
    public string? InstanceId { get; init; }
}
