using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using TCMine_Application.Contracts;
using TCMine_Application.Launcher;

namespace TCMine_Launcher.ViewModels;

/// <summary>
///     Catálogo de modpacks publicados pelo servidor (<see cref="IModpackCatalog" />). Clicar num item só o
///     **seleciona** e abre a Home — a instalação/launch é o botão grande da Home (não instala daqui).
/// </summary>
public sealed class ModpacksPageViewModel : ViewModelBase
{
    private readonly IModpackCatalog _catalog;
    private readonly MainWindowViewModel _shell;

    private bool _busy;
    private string? _error;

    public ModpacksPageViewModel(MainWindowViewModel shell, IModpackCatalog catalog)
    {
        _shell = shell;
        _catalog = catalog;
        Refresh = ReactiveCommand.CreateFromTask(LoadAsync);
        _ = LoadAsync();
    }

    public ObservableCollection<ModpackListItem> Modpacks { get; } = [];

    public ReactiveCommand<Unit, Unit> Refresh { get; }

    private bool Busy
    {
        get => _busy;
        set => this.RaiseAndSetIfChanged(ref _busy, value);
    }

    public string? Error
    {
        get => _error;
        private set => this.RaiseAndSetIfChanged(ref _error, value);
    }

    public bool IsEmpty => !Busy && Modpacks.Count == 0;

    /// <summary>Recarrega o catálogo (ex.: quando o servidor avisa que o conteúdo mudou).</summary>
    public void Reload()
    {
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        Busy = true;
        Error = null;
        try
        {
            var packs = await _catalog.GetModpacksAsync();
            Modpacks.Clear();
            foreach (var p in packs) Modpacks.Add(new ModpackListItem(p, _shell));
        }
        catch (Exception ex)
        {
            Error = $"Não foi possível carregar o catálogo: {ex.Message}";
        }
        finally
        {
            Busy = false;
            this.RaisePropertyChanged(nameof(IsEmpty));
        }
    }
}

/// <summary>Um modpack do catálogo + a ação de abrir (selecionar) na Home.</summary>
public sealed class ModpackListItem : ViewModelBase
{
    private readonly MainWindowViewModel _shell;

    public ModpackListItem(ModpackSummaryDto summary, MainWindowViewModel shell)
    {
        Summary = summary;
        _shell = shell;
        Open = ReactiveCommand.CreateFromTask(() => _shell.SelectModpackAsync(summary.Id));
    }

    public ModpackSummaryDto Summary { get; }

    /// <summary>Rótulo informativo do estado local (não instala daqui — abre a Home).</summary>
    public string ActionLabel
    {
        get
        {
            var installed = _shell.GetInstalled(Summary.Id.ToString());
            if (installed is null || !installed.Installed) return "Instalar";
            return installed.ManifestVersion != Summary.Version ? "Atualizar" : "Jogar";
        }
    }

    public ReactiveCommand<Unit, Unit> Open { get; }
}