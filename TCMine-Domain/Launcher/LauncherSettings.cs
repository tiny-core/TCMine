namespace TCMine_Domain.Launcher;

/// <summary>Definições globais do launcher (RAM, Java, modpack selecionado). Valor puro.</summary>
public sealed class LauncherSettings
{
    /// <summary>RAM padrão alocada ao jogo (MB), quando a instância não tem override.</summary>
    public int AllocatedRamMb { get; set; } = 4096;

    /// <summary>Caminho do executável Java; null/vazio = auto (o launcher instala/detecta).</summary>
    public string? JavaPath { get; set; }

    /// <summary>Modpack selecionado por último (restaurado como ativo no arranque).</summary>
    public string? SelectedModpackId { get; set; }
}