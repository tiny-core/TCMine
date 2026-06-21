using System.ComponentModel.DataAnnotations;

namespace TCMine_Data.Entities;

public class NewsEntity
{
    public int Id { get; set; }

    [MaxLength(40)] public string Tag { get; set; } = string.Empty;

    [MaxLength(200)] public string Title { get; set; } = string.Empty;

    [MaxLength(1000)] public string Summary { get; set; } = string.Empty;

    // Data de publicação (ordena a lista e formata o campo "date" público)
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

    // Só as publicadas aparecem no endpoint público
    public bool IsPublished { get; set; } = true;
}