using System.ComponentModel.DataAnnotations;

namespace TCMine_Domain.Entities;

/// <summary>Um servidor anunciado por um modpack (escrito no servers.dat pelo launcher).</summary>
public class ServerEntryEntity
{
    public int Id { get; set; }

    [MaxLength(120)] public string Name { get; set; } = string.Empty;

    [MaxLength(200)] public string Address { get; set; } = string.Empty;

    public int Port { get; set; } = 25565;

    public Guid ModpackId { get; set; }
    public ModpackEntity? Modpack { get; set; }

    /// <summary>
    /// Quando preenchido, esta entrada foi <b>auto-gerada</b> por uma instância gerenciada
    /// (<see cref="ServerInstanceEntity"/>) e é mantida em sync com ela — não deve ser editada à mão.
    /// <c>null</c> = entrada manual (servidor externo cadastrado pelo admin).
    /// </summary>
    public Guid? ServerInstanceId { get; set; }
}