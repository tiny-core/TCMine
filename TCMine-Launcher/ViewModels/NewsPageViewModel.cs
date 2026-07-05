using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using TCMine_Application.Contracts;
using TCMine_Application.Launcher;

namespace TCMine_Launcher.ViewModels;

/// <summary>Página "Novidades": feed do servidor (globais + de modpacks), recarregável via SSE.</summary>
public sealed class NewsPageViewModel : ViewModelBase
{
    private readonly INewsFeed _feed;

    private bool _busy;
    private string? _error;

    public NewsPageViewModel(INewsFeed feed)
    {
        _feed = feed;
        Refresh = ReactiveCommand.CreateFromTask(LoadAsync);
        _ = LoadAsync();
    }

    public ObservableCollection<NewsItemDto> News { get; } = [];

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

    public bool IsEmpty => !Busy && News.Count == 0;

    /// <summary>Recarrega o feed (ex.: quando o servidor avisa que o conteúdo mudou).</summary>
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
            var items = await _feed.GetNewsAsync();
            News.Clear();
            foreach (var item in items) News.Add(item);
        }
        catch (Exception ex)
        {
            Error = $"Não foi possível carregar as novidades: {ex.Message}";
        }
        finally
        {
            Busy = false;
            this.RaisePropertyChanged(nameof(IsEmpty));
        }
    }
}