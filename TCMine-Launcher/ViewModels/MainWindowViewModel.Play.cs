using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using Avalonia.Threading;
using ReactiveUI;
using TCMine_Application.Contracts;
using TCMine_Application.Launcher;
using TCMine_Domain.Launcher;
using TCMine_Domain.Modpack;

namespace TCMine_Launcher.ViewModels;

/// <summary>
/// Parte da shell dedicada às <b>instâncias instaladas</b> e ao <b>pipeline de install/launch</b>. Só
/// coordena as portas (TCMine-Application); o download dos mods (do cache do servidor), o NeoForge e os
/// overrides vivem na infraestrutura. Clicar num modpack apenas o <b>seleciona</b> — instalar/jogar é o
/// botão grande da Home.
/// </summary>
public sealed partial class MainWindowViewModel
{
    private readonly IInstanceStore _instanceStore;
    private readonly ISettingsStore _settingsStore;
    private readonly IGameRunStateStore _runState;
    private readonly ILaunchOrchestrator _orchestrator;
    private readonly ISystemInfo _systemInfo;
    private LauncherSettings _settings = new();

    private InstalledModpack? _active;
    private bool _isGameRunning;
    private bool _isLaunching;
    private double _launchPercent;
    private string _launchStatus = "Pronto";
    private CancellationTokenSource? _launchCts;
    private bool _acceptProgress;

    // Versão mais recente de cada modpack no servidor (do manifesto/catálogo). Comparada com a versão
    // instalada (InstalledModpack.ManifestVersion) para detetar "atualizar". Atualizada via SSE.
    private readonly Dictionary<string, string> _latestVersions = [];

    public ObservableCollection<InstalledModpack> Installed { get; } = [];

    public ReactiveCommand<Unit, Unit> Play { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> CancelLaunch { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> OpenInstanceFolder { get; private set; } = null!;

    public InstalledModpack? Active
    {
        get => _active;
        private set
        {
            this.RaiseAndSetIfChanged(ref _active, value);
            this.RaisePropertyChanged(nameof(HasActive));
            this.RaisePropertyChanged(nameof(InstanceIdShort));
            this.RaisePropertyChanged(nameof(ActiveRamMb));
            this.RaisePropertyChanged(nameof(ActiveRamLabel));
            RaisePlayState();
        }
    }

    // ── RAM do modpack ativo (botão da barra de estado) ──────────────────────────────────────────
    public double ActiveRamMin => 1024;
    public double ActiveRamMax => Math.Max(2048, _systemInfo.TotalPhysicalRamMb / 1024 * 1024);

    public double ActiveRamMb
    {
        get => Active is { } a ? EffectiveRam(a) : _settings.AllocatedRamMb;
        set
        {
            if (Active is not { } a) return;
            a.RamOverrideMb = (int)Math.Clamp(value, ActiveRamMin, ActiveRamMax);
            _instanceStore.Save(a);
            this.RaisePropertyChanged(nameof(ActiveRamLabel));
        }
    }

    public string ActiveRamLabel => $"{(int)ActiveRamMb} MB";

    /// <summary>Teto de RAM (RAM física), independente da instância — usado pelos editores de memória.</summary>
    public double RamHardMax => Math.Max(2048, _systemInfo.TotalPhysicalRamMb / 1024 * 1024);

    // ── Gestão de instâncias (aba Instâncias) ────────────────────────────────────────────────────
    public void SaveInstance(InstalledModpack instance) => _instanceStore.Save(instance);

    public void DeleteInstance(InstalledModpack instance)
    {
        _instanceStore.Delete(instance.ModpackId);
        Installed.Remove(instance);

        if (Active != instance) return;
        Active = Installed.FirstOrDefault();
        _settings.SelectedModpackId = Active?.ModpackId;
        _settingsStore.Save(_settings);
        Home.NotifyActiveChanged();
    }

    public Task ExportInstanceAsync(InstalledModpack instance, string zipPath) =>
        Task.Run(() => _instanceStore.Export(instance.ModpackId, zipPath));

    public async Task<InstalledModpack?> ImportInstanceAsync(string zipPath)
    {
        var imported = await Task.Run(() => _instanceStore.Import(zipPath));
        if (imported is null) return null;

        if (GetInstalled(imported.ModpackId) is { } existing) Installed.Remove(existing);
        Installed.Insert(0, imported);
        return imported;
    }

    /// <summary>Abre uma subpasta do jogo da instância (ex.: "shaderpacks", "resourcepacks").</summary>
    public void OpenInstanceSubfolder(InstalledModpack instance, string sub)
    {
        var dir = Path.Combine(_instanceStore.GameDir(instance.ModpackId), sub);
        Directory.CreateDirectory(dir);
        try { Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true }); }
        catch { /* ignora falhas a abrir o explorador */ }
    }

    public bool HasActive => Active is not null;

    /// <summary>Id (abreviado) da instância ativa — mostrado no painel da Home.</summary>
    public string InstanceIdShort => Active?.ModpackId is { } id
        ? id.Length > 30 ? id[..30] + "…" : id
        : "—";

    public bool IsGameRunning
    {
        get => _isGameRunning;
        set
        {
            this.RaiseAndSetIfChanged(ref _isGameRunning, value);
            RaisePlayState();
        }
    }

    public bool IsLaunching
    {
        get => _isLaunching;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isLaunching, value);
            RaisePlayState();
        }
    }

    public double LaunchPercent
    {
        get => _launchPercent;
        private set => this.RaiseAndSetIfChanged(ref _launchPercent, value);
    }

    public string LaunchStatus
    {
        get => _launchStatus;
        private set => this.RaiseAndSetIfChanged(ref _launchStatus, value);
    }

    public ObservableCollection<string> LaunchLog { get; } = [];

    public bool CanPlay => !IsLaunching && !IsGameRunning && HasActive;

    /// <summary>Há uma versão mais recente do modpack ativo no servidor do que a instalada.</summary>
    public bool HasActiveUpdate =>
        Active is { Installed: true } a
        && _latestVersions.TryGetValue(a.ModpackId, out var latest)
        && latest != a.ManifestVersion;

    public string PlayLabel => IsGameRunning
        ? "EM EXECUÇÃO"
        : IsLaunching
            ? "A PREPARAR…"
            : (Active?.Installed ?? false)
                ? HasActiveUpdate ? "ATUALIZAR" : "JOGAR"
                : "INSTALAR";

    internal LauncherSettings Prefs => _settings;

    private void RaisePlayState()
    {
        this.RaisePropertyChanged(nameof(CanPlay));
        this.RaisePropertyChanged(nameof(HasActiveUpdate));
        this.RaisePropertyChanged(nameof(PlayLabel));
    }

    private void InitPlay()
    {
        _settings = _settingsStore.Load();

        var canPlay = this.WhenAnyValue(x => x.IsLaunching, x => x.IsGameRunning, (l, r) => !l && !r);
        Play = ReactiveCommand.CreateFromTask(PlayActiveAsync, canPlay);
        CancelLaunch = ReactiveCommand.Create(() => _launchCts?.Cancel());
        OpenInstanceFolder = ReactiveCommand.Create(OpenInstanceFolderImpl);

        LoadInstalled();
        DetectRunningGame();
    }

    private void LoadInstalled()
    {
        Installed.Clear();
        foreach (var instance in _instanceStore.LoadAll().OrderByDescending(i => i.LastPlayedAt))
            Installed.Add(instance);

        Active = Installed.FirstOrDefault(i => i.ModpackId == _settings.SelectedModpackId)
                 ?? Installed.FirstOrDefault();
    }

    public InstalledModpack? GetInstalled(string modpackId) =>
        Installed.FirstOrDefault(i => i.ModpackId == modpackId);

    /// <summary>Atualiza os metadados do modpack ativo a partir do manifesto (servidores, descrição…).</summary>
    public async Task RefreshActiveAsync()
    {
        if (Active is null) return;
        try
        {
            var manifest = await _catalog.GetManifestAsync(Guid.Parse(Active.ModpackId));
            if (manifest is null)
            {
                // Manifesto 404: o modpack foi removido/despublicado no servidor. Sinaliza (badge) e
                // rebuild da lista de servidores para refletir o estado — sem apagar nada em disco.
                Active.ModpackMissing = true;
                Home.NotifyActiveChanged();
                return;
            }

            RegisterFromManifest(manifest); // atualiza o mesmo objeto (Active está em Installed)
            this.RaisePropertyChanged(nameof(InstanceIdShort));
            this.RaisePropertyChanged(nameof(ActiveRamMb));
            this.RaisePropertyChanged(nameof(ActiveRamLabel));
            RaisePlayState();
            Home.NotifyActiveChanged(); // recarrega a lista de servidores
        }
        catch
        {
            // servidor offline — mantém o que está em disco (NÃO marca como removido)
        }
    }

    /// <summary>
    /// Reconcilia todas as instâncias instaladas com o catálogo público: marca como
    /// <see cref="InstalledModpack.ModpackMissing"/> as que já não constam do servidor (removidas ou
    /// despublicadas). Cobre a lista de Instâncias inteira; a lista de servidores do ativo continua a
    /// vir do manifesto (<see cref="RefreshActiveAsync"/>). Silenciosa se o servidor estiver offline.
    /// </summary>
    public async Task ReconcileAvailabilityAsync()
    {
        List<InstalledModpack> snapshot = [.. Installed];
        if (snapshot.Count == 0) return;

        IReadOnlyList<ModpackSummaryDto> catalog;
        try
        {
            catalog = await _catalog.GetModpacksAsync();
        }
        catch
        {
            return; // offline — não dá para saber o que foi removido; mantém como está
        }

        var liveIds = catalog.Select(c => c.Id.ToString()).ToHashSet();
        foreach (var instance in snapshot)
            instance.ModpackMissing = !liveIds.Contains(instance.ModpackId);
    }

    public void SelectActive(InstalledModpack instance)
    {
        Active = instance;
        _settings.SelectedModpackId = instance.ModpackId;
        _settingsStore.Save(_settings);
        Home.NotifyActiveChanged();
    }

    public void SaveSettings() => _settingsStore.Save(_settings);

    /// <summary>
    /// Regista o modpack (se preciso) e atualiza os **metadados de exibição** (nome, descrição,
    /// servidores). Para uma instância **já instalada**, NÃO mexe na versão/loader/overrides instalados —
    /// só regista a versão mais recente do servidor (em <see cref="_latestVersions"/>) para o botão
    /// poder virar "ATUALIZAR". Os campos de instalação só mudam ao Jogar (<see cref="ApplyInstallFromManifest"/>).
    /// </summary>
    public InstalledModpack RegisterFromManifest(ModpackManifestDto m)
    {
        var id = m.Id.ToString();
        _latestVersions[id] = m.Version;
        var servers = m.Servers.Select(s => new ModpackServer(s.Name, s.Address, s.Port)).ToList();

        var existing = Installed.FirstOrDefault(i => i.ModpackId == id);
        if (existing is not null)
        {
            existing.Name = m.Name;
            existing.Description = m.Description;
            existing.Servers = servers; // metadados de exibição: sempre frescos (auto-join continua válido)
            existing.RecommendedRamMb = m.RecommendedRamMb;

            // Manifesto veio → o modpack existe; reavalia os badges com os servidores frescos
            existing.ModpackMissing = false;
            existing.NotifyAvailabilityChanged();

            if (!existing.Installed)
            {
                // Ainda não instalado: a versão/loader/overrides refletem o que SERÁ instalado.
                existing.ManifestVersion = m.Version;
                existing.Minecraft = m.Minecraft;
                existing.NeoForgeVersion = m.LoaderVersion;
                existing.HasOverrides = m.HasOverrides;
            }

            _instanceStore.Save(existing);
            if (existing == Active) RaisePlayState();
            return existing;
        }

        var instance = new InstalledModpack
        {
            ModpackId = id,
            Name = m.Name,
            Minecraft = m.Minecraft,
            NeoForgeVersion = m.LoaderVersion,
            ManifestVersion = m.Version,
            Description = m.Description,
            HasOverrides = m.HasOverrides,
            Servers = servers,
            RecommendedRamMb = m.RecommendedRamMb,
            AutoJoinServerName = servers.FirstOrDefault()?.Name
        };
        _instanceStore.Save(instance);
        Installed.Insert(0, instance);
        return instance;
    }

    /// <summary>Aplica os campos de instalação do manifesto (chamado ao Jogar — instala/atualiza a versão atual).</summary>
    private static void ApplyInstallFromManifest(InstalledModpack instance, ModpackManifestDto m)
    {
        instance.ManifestVersion = m.Version;
        instance.Minecraft = m.Minecraft;
        instance.NeoForgeVersion = m.LoaderVersion;
        instance.HasOverrides = m.HasOverrides;
        instance.Servers = m.Servers.Select(s => new ModpackServer(s.Name, s.Address, s.Port)).ToList();
        instance.RecommendedRamMb = m.RecommendedRamMb;
    }

    public int EffectiveRam(InstalledModpack instance)
    {
        var max = Math.Max(2048, _systemInfo.TotalPhysicalRamMb / 1024 * 1024);
        return Math.Clamp(instance.RamOverrideMb ?? instance.RecommendedRamMb ?? _settings.AllocatedRamMb, 1024, max);
    }

    /// <summary>Botão do card de Modpacks: só **seleciona** (regista metadados) e abre a Home — NÃO instala.</summary>
    public async Task SelectModpackAsync(Guid modpackId)
    {
        var idStr = modpackId.ToString();
        var instance = GetInstalled(idStr);
        if (instance is null)
        {
            try
            {
                var manifest = await _catalog.GetManifestAsync(modpackId);
                if (manifest is null) return;
                instance = RegisterFromManifest(manifest);
            }
            catch
            {
                return;
            }
        }

        SelectActive(instance);
        SelectedTab = AppTab.Home;
    }

    /// <summary>Comando da Home: instala (se preciso) e lança a instância ativa.</summary>
    private async Task PlayActiveAsync()
    {
        var instance = Active;
        if (instance is null)
        {
            LaunchStatus = "Nenhuma instância selecionada.";
            return;
        }

        if (_auth.Current is null)
        {
            LaunchStatus = "Sessão inválida — faz login novamente.";
            return;
        }

        ModpackManifestDto? manifest;
        try
        {
            manifest = await _catalog.GetManifestAsync(Guid.Parse(instance.ModpackId));
        }
        catch
        {
            LaunchStatus = "Servidor indisponível.";
            return;
        }

        if (manifest is null)
        {
            LaunchStatus = "Modpack indisponível no servidor.";
            return;
        }

        if (manifest.Loader != ModLoader.NeoForge)
        {
            LaunchStatus = $"Loader {manifest.Loader} ainda não suportado (só NeoForge).";
            return;
        }

        instance = RegisterFromManifest(manifest);   // garante na lista + metadados frescos + latest
        ApplyInstallFromManifest(instance, manifest); // vamos instalar/atualizar para a versão atual
        await LaunchAsync(instance, manifest);
    }

    private async Task LaunchAsync(InstalledModpack instance, ModpackManifestDto manifest)
    {
        IsLaunching = true;
        LaunchPercent = 0;
        LaunchLog.Clear();
        LaunchLog.Add($"Instância: {instance.Name} ({instance.VersionSummary})");
        _launchCts = new CancellationTokenSource();
        _acceptProgress = true;

        var progress = new Progress<LaunchProgress>(p =>
        {
            if (!_acceptProgress) return;
            LaunchPercent = p.Percent;
            LaunchStatus = p.Message;
            LaunchLog.Add($"[{p.Percent,3:0}%] {p.Message}");
        });

        try
        {
            var ram = EffectiveRam(instance);
            var java = string.IsNullOrWhiteSpace(_settings.JavaPath) ? null : _settings.JavaPath;

            var process = await _orchestrator.PrepareAsync(instance, manifest, ram, java, progress, _launchCts.Token);
            _acceptProgress = false;

            instance.Installed = true;
            instance.LastPlayedAt = DateTimeOffset.Now;
            _instanceStore.Save(instance);

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            IsGameRunning = true;
            _runState.Save(instance.ModpackId, process.Id);
            LaunchStatus = "Minecraft em execução";
            LaunchLog.Add("Minecraft iniciado.");

            _ = MonitorGameAsync(process);
        }
        catch (OperationCanceledException)
        {
            LaunchStatus = "Launch cancelado";
            LaunchLog.Add("Launch cancelado pelo utilizador.");
        }
        catch (Exception ex)
        {
            LaunchStatus = "Falha no launch";
            LaunchLog.Add("ERRO: " + ex.Message);
        }
        finally
        {
            _launchCts?.Dispose();
            _launchCts = null;
            LaunchPercent = 0;
            IsLaunching = false;
            Home.NotifyActiveChanged();
        }
    }

    private async Task MonitorGameAsync(Process process)
    {
        try { await process.WaitForExitAsync(); }
        catch { /* o importante é reativar a UI a seguir */ }

        _runState.Clear();
        try { process.Dispose(); }
        catch { /* noop */ }

        Dispatcher.UIThread.Post(() =>
        {
            IsGameRunning = false;
            LaunchStatus = "Pronto";
            Home.NotifyActiveChanged();
        });
    }

    private void DetectRunningGame()
    {
        var state = _runState.Load();
        if (state is null) return;

        try
        {
            var proc = Process.GetProcessById(state.Pid);
            if (proc.HasExited)
            {
                _runState.Clear();
                return;
            }

            _isGameRunning = true; // direto: ainda no construtor, sem bindings ligados
            _ = MonitorGameAsync(proc);
        }
        catch (ArgumentException)
        {
            _runState.Clear();
        }
    }

    private void OpenInstanceFolderImpl()
    {
        if (Active is null) return;
        var dir = _instanceStore.InstanceDir(Active.ModpackId);
        Directory.CreateDirectory(dir);
        try { Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true }); }
        catch { /* ignora falhas a abrir o explorador */ }
    }
}
