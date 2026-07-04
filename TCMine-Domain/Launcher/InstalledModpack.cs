using System.ComponentModel;
using System.Text.Json.Serialization;

namespace TCMine_Domain.Launcher;

/// <summary>
/// Instância instalada localmente, derivada de um modpack oficial. O launcher é só-oficial: há **uma
/// instância por modpack**, chaveada pelo <see cref="ModpackId"/>. Persistido em JSON.
/// Implementa <see cref="INotifyPropertyChanged"/> só para as <b>flags de disponibilidade em runtime</b>
/// (não persistidas): assim os badges de "modpack/servidor indisponível" — bindados direto no XAML —
/// reagem quando o SSE avisa que o conteúdo do servidor mudou.
/// </summary>
public sealed class InstalledModpack : INotifyPropertyChanged
{
    /// <summary>Id do modpack no servidor (Guid em texto) — também é o nome da pasta da instância.</summary>
    public string ModpackId { get; set; } = "";

    public string Name { get; set; } = "";
    public string Minecraft { get; set; } = "";
    public string NeoForgeVersion { get; set; } = "";
    public string ManifestVersion { get; set; } = "";
    public string Description { get; set; } = "";

    public bool HasOverrides { get; set; }

    /// <summary>Versão do manifesto cujos overrides já foram aplicados (evita reaplicar).</summary>
    public string? OverridesVersion { get; set; }

    public List<ModpackServer> Servers { get; set; } = [];

    /// <summary>Servidor onde entrar automaticamente ao iniciar; null = menu principal.</summary>
    public string? AutoJoinServerName { get; set; }

    /// <summary>RAM recomendada pelo modpack (MB); default se não houver override.</summary>
    public int? RecommendedRamMb { get; set; }

    /// <summary>RAM específica desta instância (MB); null = default global.</summary>
    public int? RamOverrideMb { get; set; }

    /// <summary>Ficou true após o 1º prepare bem-sucedido (define Jogar vs Instalar).</summary>
    public bool Installed { get; set; }

    public DateTimeOffset? LastPlayedAt { get; set; }

    /// <summary>
    /// <c>UpdatedAt</c> do servidor correspondente às configs do jogador (keybinds/opções) que estão
    /// aplicadas nesta instância. Usado no sync last-write-wins: no prepare, só baixa do servidor se este
    /// valor divergir do <c>UpdatedAt</c> remoto (ex.: o jogador jogou noutro PC). Null = nunca sincronizou.
    /// </summary>
    public DateTimeOffset? ConfigSyncedAt { get; set; }

    // ── Derivados (não persistir) ────────────────────────────────────────────────────────────────
    [JsonIgnore]
    public string VersionSummary => $"v{ManifestVersion} · MC {Minecraft} · NeoForge {NeoForgeVersion}";

    [JsonIgnore]
    public bool HasServer => Servers.Count > 0;

    // ── Disponibilidade em runtime (setada pela shell a partir do catálogo/manifesto; não persistir) ──
    private bool _modpackMissing;

    /// <summary>O modpack já não existe no servidor (removido ou despublicado). Setado pela shell.</summary>
    [JsonIgnore]
    public bool ModpackMissing
    {
        get => _modpackMissing;
        set
        {
            if (_modpackMissing == value) return;
            _modpackMissing = value;
            RaiseAvailability();
        }
    }

    /// <summary>
    /// O servidor de auto-join configurado desapareceu da lista atual de servidores do modpack.
    /// Só faz sentido enquanto o modpack existe (se o modpack sumiu, o aviso é o outro).
    /// </summary>
    [JsonIgnore]
    public bool AutoJoinServerMissing =>
        !ModpackMissing && AutoJoinServerName is { } name && Servers.All(s => s.Name != name);

    /// <summary>Há algum problema de disponibilidade a sinalizar com badge.</summary>
    [JsonIgnore]
    public bool HasAvailabilityWarning => ModpackMissing || AutoJoinServerMissing;

    /// <summary>Texto do badge (null quando não há aviso).</summary>
    [JsonIgnore]
    public string? AvailabilityMessage => ModpackMissing
        ? "Modpack indisponível no servidor"
        : AutoJoinServerMissing
            ? $"Servidor \"{AutoJoinServerName}\" já não existe"
            : null;

    /// <summary>
    /// Reavaliar os badges após a lista de servidores / auto-join mudar (ex.: depois de aplicar um
    /// manifesto fresco). <see cref="ModpackMissing"/> já notifica sozinho no seu setter.
    /// </summary>
    public void NotifyAvailabilityChanged() => RaiseAvailability();

    private void RaiseAvailability()
    {
        OnPropertyChanged(nameof(ModpackMissing));
        OnPropertyChanged(nameof(AutoJoinServerMissing));
        OnPropertyChanged(nameof(HasAvailabilityWarning));
        OnPropertyChanged(nameof(AvailabilityMessage));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
