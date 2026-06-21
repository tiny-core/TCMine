using System.ComponentModel.DataAnnotations;

namespace TCMine_Data.Entities;

/// <summary>
/// Configurações do jogador (keybinds, shader/texturas selecionados, minimapa)
/// guardadas por <c>(Uuid, ModpackId)</c> como um zip. Permite repor as configs
/// quando o jogador entra noutro PC. A chave é o UUID do Minecraft — são apenas
/// settings de jogo (sem segredos). Last-write-wins por <see cref="UpdatedAt"/>.
/// </summary>
public class PlayerConfigEntity
{
    // UUID do Minecraft do jogador (parte da chave composta)
    [MaxLength(40)] public string Uuid { get; set; } = string.Empty;

    // Slug do modpack oficial a que estas configs pertencem (parte da chave)
    [MaxLength(80)] public string ModpackId { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; }
}