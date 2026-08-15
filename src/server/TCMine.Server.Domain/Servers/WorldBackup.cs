using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Servers;

/// <summary>
///     Um instantâneo do mundo de um servidor.
///     Existe para destravar a troca de versão: mexer nos mods de um mundo já
///     gerado pode corromper o save (mod removido = registry faltando no
///     carregamento; downgrade = formato de dados que a versão antiga não sabe
///     ler). Sem poder voltar atrás, a operação é irreversível — com o snapshot,
///     é só demorada.
/// </summary>
public sealed class WorldBackup : Entity
{
    public required Guid GameServerId { get; set; }

    /// <summary>Nome do arquivo no diretório de backups. Não é caminho — o store resolve.</summary>
    public required string FileName { get; set; }

    public required long SizeBytes { get; set; }

    public required WorldBackupReason Reason { get; set; }

    /// <summary>
    ///     Versão do modpack que estava fixada quando o snapshot foi tirado.
    ///     É o que diz para qual versão restaurar: um mundo salvo na 1.2 pode não
    ///     abrir na 1.5.
    /// </summary>
    public Guid? ModpackVersionId { get; set; }

    /// <summary>Rótulo da versão, para exibir sem precisar de join.</summary>
    public string? ModpackVersionLabel { get; set; }

    /// <summary>Anotação do admin, quando manual.</summary>
    public string? Note { get; set; }

    /// <summary>
    ///     Tirado com o servidor no ar (autosave pausado por RCON).
    ///     Vale registrar: mesmo com save-off + save-all, um snapshot a quente
    ///     tem uma janela menor de risco que um a frio, e é justo o admin saber
    ///     qual dos dois tem em mãos na hora de restaurar.
    /// </summary>
    public bool TakenHot { get; set; }
}

public enum WorldBackupReason
{
    /// <summary>Pedido pelo admin.</summary>
    Manual,

    /// <summary>Automático, imediatamente antes de trocar a versão fixada.</summary>
    BeforeVersionChange
}
