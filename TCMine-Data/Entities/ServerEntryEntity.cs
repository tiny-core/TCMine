using System.ComponentModel.DataAnnotations;

namespace TCMine_Data.Entities;

/// <summary>Um servidor anunciado por um modpack (escrito no servers.dat pelo launcher).</summary>
public class ServerEntryEntity
{
    public int Id { get; set; }

    [MaxLength(120)] public string Name { get; set; } = string.Empty;

    [MaxLength(200)] public string Address { get; set; } = string.Empty;

    public int Port { get; set; } = 25565;

    public Guid ModpackId { get; set; }
    public ModpackEntity? Modpack { get; set; }
}