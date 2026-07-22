namespace TCMine.Contracts;

/// <summary>
///     Features opcionais anunciadas pelo server no handshake.
///     Serve para publicar uma feature no launcher ANTES do server: se a
///     capability não vier na lista, o launcher simplesmente esconde a opção.
///     Nunca troque isto por comparação de versão ("if serverVersion ≥ 1.6.0").
///     Versão diz o que o server É; capability diz o que ele SABE FAZER — e é
///     isso que você precisa saber para decidir se mostra um botão.
/// </summary>
public static class Capabilities
{
    public const string ModpackDelta = "modpack.delta";
    public const string ConsoleStream = "console.stream";
    public const string ConsoleCommands = "console.commands";
    public const string BackupSchedule = "backup.schedule";
    public const string ManualModUpload = "modpack.manual-upload";
}