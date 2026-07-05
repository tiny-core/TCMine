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
    private LauncherBuildView? _build;
    private string? _feedVersion; // versão publicada no feed /updates
    private bool _needsBuild; // há launcher-v* mais nova que o feed?

    private List<ReleaseEntity>? _releases;
    private bool _scrollPending;

    // Compilar exige URL pública + Azure Client Id (ambos embutidos no launcher em build-time)
    private bool _settingsReady;
    private ElementReference _stepsEl;

    // Faixas de release do GitHub (server-v* e launcher-v*) + estado derivado
    private GitHubTracks? _tracks;
    [Inject] private LauncherBuildService Build { get; set; } = null!;
    [Inject] private ReleaseService ReleaseSvc { get; set; } = null!;
    [Inject] private LauncherFeedService Feed { get; set; } = null!;
    [Inject] private ServerSettingsService Settings { get; set; } = null!;
    [Inject] private GitHubReleaseService GitHub { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private BusyService Busy { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    private string? LauncherTarget => _tracks?.Launcher.LatestVersion;
    private string? LauncherTag => _tracks?.Launcher.Tag;

    // Versão a empacotar: a da release (build normal) ou X.Y.(Z+1)-local.N quando é um rebuild por config
    // por cima de uma versão já publicada — fica entre a atual e a próxima release do GitHub.
    private string? BuildVersion => LauncherTarget is { } t ? AppVersion.BuildVersion(t, _feedVersion) : null;

    // Pode compilar agora? Sempre permitido quando há uma release de launcher + settings prontas + nada
    // rodando — o admin pode querer recompilar para reaplicar uma config alterada (URL/Azure), mesmo com o
    // feed já na última versão. (O _needsBuild controla só o destaque de "desatualizado", não o botão.)
    private bool _canBuild => LauncherTarget is not null && _settingsReady && !Build.IsRunning;

    public void Dispose()
    {
        Build.Changed -= OnBuildChanged;
    }

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

        _tracks = await GitHub.GetAsync();
        RecomputeLauncherState();
    }

    // Feed publicado vs. última launcher-v* → precisa recompilar?
    private void RecomputeLauncherState()
    {
        _feedVersion = Feed.LatestVersion();
        _needsBuild = LauncherTarget is { } target &&
                      (_feedVersion is null || AppVersion.IsNewer(target, _feedVersion));
    }

    // Botão "Verificar atualizações": ignora o cache de 6h e consulta o GitHub na hora.
    private async Task CheckUpdatesAsync()
    {
        try
        {
            await Busy.RunAsync("Verificando novas versões…", async () =>
            {
                _tracks = await GitHub.GetAsync(true);
                RecomputeLauncherState();
            });

            if (_tracks?.Server is { UpdateAvailable: true } su)
                Snackbar.Add($"Atualização do servidor disponível: v{su.LatestVersion}.", Severity.Info);
            else
                Snackbar.Add("Servidor já está na versão mais recente.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha ao verificar atualizações: {ex.Message}", Severity.Error);
        }
    }

    // Abre o diálogo (só notas; a versão é computada — release ou -local.N) e dispara a compilação de fundo
    private async Task StartBuildAsync()
    {
        if (BuildVersion is not { } version || LauncherTag is not { } tag) return;

        var parameters = new DialogParameters<LauncherBuildDialog>
        {
            { x => x.Version, version },
            { x => x.InitialNotes, _tracks?.Launcher.Notes ?? string.Empty }
        };
        var dialog = await DialogService.ShowAsync<LauncherBuildDialog>(
            "Compilar launcher", parameters, new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });
        var result = await dialog.Result;
        if (result is null || result.Canceled ||
            result.Data is not LauncherBuildDialog.LauncherBuildRequest req)
            return;

        Build.Start(req.Version, tag, req.Notes);
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
        try
        {
            await JS.InvokeVoidAsync("tcmineScrollToBottom", _stepsEl);
        }
        catch
        {
            /* painel não renderizado — ignora */
        }
    }
}