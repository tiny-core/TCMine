using System.ComponentModel.DataAnnotations;

namespace TCMine_Domain.Entities;

public class NewsEntity
{
    public int Id { get; set; }

    [MaxLength(40)] public string Tag { get; set; } = string.Empty;
    [MaxLength(200)] public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Fornece uma visão geral concisa ou um resumo do conteúdo da entidade de notícias.
    /// </summary>
    [MaxLength(1000)]
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Especifica a data e hora em que a entidade de notícias foi publicada.icada.
    /// </summary>
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Indica se a entidade de notícias está publicada ou não.
    /// </summary>
    public bool IsPublished { get; set; } = true;
}