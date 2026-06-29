using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using TCMine_Application.Contracts;
using TCMine_Launcher.Services;

namespace TCMine_Launcher.ViewModels;

/// <summary>
/// Catálogo de modpacks publicados pelo servidor (<c>GET /api/modpacks</c>). Só leitura neste
/// incremento — instalar/lançar virá depois.
/// </summary>
public sealed class ModpacksPageViewModel : ViewModelBase
{
    private readonly ApiClient _api;

    private bool _busy;
    private string? _error;

    public ModpacksPageViewModel(ApiClient api)
    {
        _api = api;
        Refresh = ReactiveCommand.CreateFromTask(LoadAsync);
        _ = LoadAsync();
    }

    public ObservableCollection<ModpackSummaryDto> Modpacks { get; } = [];

    public ReactiveCommand<Unit, Unit> Refresh { get; }

    public bool Busy
    {
        get => _busy;
        private set => this.RaiseAndSetIfChanged(ref _busy, value);
    }

    public string? Error
    {
        get => _error;
        private set => this.RaiseAndSetIfChanged(ref _error, value);
    }

    /// <summary>Empty-state: sem modpacks e sem carregamento em curso.</summary>
    public bool IsEmpty => !Busy && Modpacks.Count == 0;

    private async Task LoadAsync()
    {
        Busy = true;
        Error = null;
        try
        {
            var packs = await _api.GetModpacksAsync();
            Modpacks.Clear();
            foreach (var p in packs) Modpacks.Add(p);
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
