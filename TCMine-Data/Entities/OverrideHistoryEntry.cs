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

    /// <summary>
    /// Identificador único do modpack ao qual o mod pertence (referência para <see cref="ModpackEntity"/>).
    /// </summary>
    public Guid ModpackId { get; set; }

    /// <summary>
    /// Tipo de operação registrada para esta entrada no histórico de alterações de overrides.
    /// Refere-se à ação realizada, como edição, movimentação ou exclusão,
    /// que determinou a necessidade de registro para auditoria ou suporte à funcionalidade de desfazer.
    /// </summary>
    public OverrideOp Operation { get; set; }

    /// <summary>
    /// Caminho de origem (edição/exclusão = caminho do arquivo; mover = caminho antes de mover)
    /// </summary>
    [MaxLength(400)] public string? PathBefore { get; set; }

    /// <summary>
    /// Caminho de destino (só para mover)
    /// </summary>
    [MaxLength(400)] public string? PathAfter { get; set; }

    /// <summary>
    /// Conteúdo de texto anterior (edição/exclusão) — sem MaxLength: coluna de texto longo
    /// </summary>
    // ReSharper disable once EntityFramework.ModelValidation.UnlimitedStringLength
    public string? ContentBefore { get; set; }

    /// <summary>
    /// Quem fez a alteração (username), quando disponível
    /// </summary>
    [MaxLength(80)] public string? Actor { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}