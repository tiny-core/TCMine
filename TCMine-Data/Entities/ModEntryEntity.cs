using System.ComponentModel.DataAnnotations;
using TCMine_Core.modpack;

namespace TCMine_Data.Entities;

/// <summary>Um mod (CurseForge) pertencente a um modpack.</summary>
public abstract class ModEntryEntity
{
    public int Id { get; set; }

    // ID do mod no CurseForge (serializado como "modId" no manifesto público)
    public long CurseModId { get; set; }

    public long FileId { get; set; }

    [MaxLength(200)] public string Name { get; set; } = string.Empty;

    // Versão legível do arquivo (CurseForge DisplayName)
    [MaxLength(80)] public string? Version { get; set; }

    [MaxLength(260)] public string FileName { get; set; } = string.Empty;

    // URL de origem no CurseForge — usada pelo servidor para baixar o jar uma vez.
    // O launcher NÃO usa esta URL: baixa o jar do próprio servidor (ver project-modpack-mods-locais).
    [MaxLength(500)] public string DownloadUrl { get; set; } = string.Empty;

    // SHA-1 e tamanho do jar baixado — para o launcher verificar integridade e para dedup do cache.
    // Preenchidos quando o servidor baixa o arquivo (o CF não fornece hash no CfFileRefDto).
    [MaxLength(40)] public string? Sha1 { get; set; }
    public long FileLength { get; set; }

    // Destino no cliente: "mod", "resourcepack" ou "shaderpack"
    [MaxLength(20)] public string Target { get; set; } = "mod";

    // Lado em que o mod roda — filtra o que vai pro cliente (launcher) vs servidor.
    // Regra compartilhada em TCMine.Core.modpack.ModSideRules.
    public ModSide Side { get; set; } = ModSide.Both;

    public string ModpackId { get; set; } = string.Empty;
    public ModpackEntity? Modpack { get; set; }
}