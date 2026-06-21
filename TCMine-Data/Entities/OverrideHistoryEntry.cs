using System.ComponentModel.DataAnnotations;

namespace TCMine_Data.Entities;

/// <summary>Tipo de operação registrada no histórico de overrides (base do "desfazer").</summary>
public enum OverrideOp
{
    Edit, // edição de conteúdo de um arquivo de texto
    MoveFile, // arquivo movido de pasta
    MoveFolder, // pasta inteira movida
    DeleteFile // arquivo de texto excluído
}

/// <summary>
/// Uma entrada no histórico de alterações de overrides de um modpack — serve de trilha de auditoria
/// e de base para o "desfazer". Cada operação guarda o necessário para a sua inversa:
/// edição/exclusão guardam o conteúdo anterior; movimentações guardam o caminho de origem.
/// </summary>
public class OverrideHistoryEntry
{
    public int Id { get; set; }

    [MaxLength(80)] public string ModpackId { get; set; } = string.Empty;

    public OverrideOp Operation { get; set; }

    // Caminho de origem (edição/exclusão = caminho do arquivo; mover = caminho antes de mover)
    [MaxLength(400)] public string? PathBefore { get; set; }

    // Caminho de destino (só para mover)
    [MaxLength(400)] public string? PathAfter { get; set; }

    // Conteúdo de texto anterior (edição/exclusão) — sem MaxLength: coluna de texto longo
    public string? ContentBefore { get; set; }

    // Quem fez a alteração (username), quando disponível
    [MaxLength(80)] public string? Actor { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}