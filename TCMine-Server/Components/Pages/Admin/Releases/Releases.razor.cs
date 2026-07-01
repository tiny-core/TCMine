using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using TCMine_Domain.Entities;
using TCMine_Server.Components.Pages.Admin.Releases.Dialogs;
using TCMine_Server.Infrastructure.Launcher;
using TCMine_Server.Infrastructure.Server;
using TCMine_Server.Services;

namespace TCMine_Server.Components.Pages.Admin.Releases;

/// <summary>
///     Página de releases do launcher: mostra o estado do feed, dispara a compilação no
///     <see cref="LauncherBuildService" /> (progresso reconectável — a página se inscreve no evento e
///     sobrevive a um refresh) e lista o histórico via <see cref="ReleaseService" />.
/// </summary>
public partial class Releases : ComponentBase, IDisposable
{
    [Inject] private LauncherBuildService Build { get; set; } = null!;
    [Inject] private ReleaseService ReleaseSvc { get; set; } = null!;
    [Inject] private LauncherFeedService Feed { get; set; } = null!;
    [Inject] private ServerSettingsService Settings { get; set; } = null!;
    [Inject] private GitHubReleaseService GitHub { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private BusyService Busy { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    private List<ReleaseEntity>? _releases;

    // Compilar exige URL pública + Azure Client Id (ambos embutidos no launcher em build-time)
    private bool _settingsReady;

    // Estado de atualização do servidor (GitHub releases vs versão corrente)
    private ServerUpdate? _serverUpdate;

    // Pode compilar agora? (precisa recompilar + settings prontas + nada rodando)
    private bool _canBuild => Build.NeedsBuild() && _settingsReady && !Build.IsRunning;

    private LauncherBuildView? _build;
    private ElementReference _stepsEl;
    private bool _scrollPending;

    protected override async Task OnInitializedAsync()
    {
        await Busy.RunAsync("Carregando releases…", LoadAsync);

        // Reconecta a uma compilação em andamento e passa a ouvir o serviço
        _build = Build.Current;
        Build.Changed += OnBuildChanged;
    }

    private async Task LoadAsync()
    {
        _releases = await ReleaseSvc.ListAsync();

        var url = await Settings.GetPublicBaseUrlAsync();
        var clientId = await Settings.GetAzureClientIdAsync();
        _settingsReady = !string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(clientId);

        _serverUpdate = await GitHub.GetAsync();
    }

    // Abre o diálogo (só notas — a versão é a do servidor) e dispara a compilação no serviço de fundo
    private async Task StartBuildAsync()
    {
        var parameters = new DialogParameters<LauncherBuildDialog>
        {
            { x => x.Version, Build.TargetVersion }
        };
        var dialog = await DialogService.ShowAsync<LauncherBuildDialog>(
            "Compilar launcher", parameters, new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });
        var result = await dialog.Result;
        if (result is null || result.Canceled ||
            result.Data is not LauncherBuildDialog.LauncherBuildRequest req)
            return;

        Build.Start(req.Version, req.Notes);
        _build = Build.Current;
        _scrollPending = true;
    }

    // Serviço avisou que o job mudou (roda fora do circuito → InvokeAsync)
    private void OnBuildChanged()
    {
        _ = InvokeAsync(async () =>
        {
            var wasRunning = _build is { State: LauncherBuildState.Running };
            _build = Build.Current;
            _scrollPending = true;

            // Ao concluir, recarrega o histórico e dá o feedback
            if (wasRunning && _build is { State: not LauncherBuildState.Running })
            {
                if (_build.State == LauncherBuildState.Succeeded)
                {
                    await LoadAsync();
                    Snackbar.Add($"Launcher {_build.Version} publicado.", Severity.Success);
                }
                else
                {
                    Snackbar.Add("Falha ao compilar o launcher — veja os detalhes no painel.", Severity.Error);
                }
            }

            StateHasChanged();
        });
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_scrollPending) return;
        _scrollPending = false;
        try { await JS.InvokeVoidAsync("tcmineScrollToBottom", _stepsEl); }
        catch { /* painel não renderizado — ignora */ }
    }

    public void Dispose()
    {
        Build.Changed -= OnBuildChanged;
    }
}
