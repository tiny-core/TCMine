using TCMine.Server.Domain.Common;

namespace TCMine.Server.Domain.Modpacks;

/// <summary>
///     Post de novidade de um modpack, exibido no launcher. Filho do Modpack, como
///     a versão — sem OwnerId próprio.
/// </summary>
public sealed class News : Entity
{
    public required Guid ModpackId { get; set; }
    public required string Title { get; set; }
    public required string Body { get; set; }

    /// <summary>Visível no launcher? Permite rascunhar antes de divulgar.</summary>
    public bool IsPublished { get; set; }
}
