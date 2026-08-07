using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Web.Components.Features.Modpacks;

namespace TCMine.Server.Web.Components.Pages.Modpacks;

public partial class ModpackModsPage : ComponentBase, IDisposable
{
    private bool _isIngesting;
    private bool _isLoading = true;
    private Modpack? _modpack;
    private Timer? _pollTimer;
    private string _searchString = "";
    private ModpackVersion? _version;

    [Parameter] public Guid ModpackId { get; set; }
    [Parameter] public Guid VersionId { get; set; }

    // Filtro em memória: a lista de mods de uma versão cabe tranquilamente,
    // então não vale ida ao banco a cada tecla.
    private IEnumerable<ModpackFile> FilteredFiles
    {
        get
        {
            // Overrides (config/extras) têm a sua própria aba; fora daqui.
            var files = (_version?.Files ?? []).Where(f => f.Origin != ModFileOrigin.Override);
            return string.IsNullOrWhiteSpace(_searchString)
                ? files
                : files.Where(f => f.Path.Contains(_searchString, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void Dispose()
    {
        _pollTimer?.Dispose();
        GC.SuppressFinalize(this);
    }

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _isLoading = true;

        // Carrega o modpack com todas as versões (para o seletor do workspace) e
        // pega a versão da rota entre elas — uma consulta só.
        _modpack = await Repository.GetWithVersionsAsync(ModpackId, CancellationToken.None);
        _version = _modpack?.Versions.FirstOrDefault(v => v.Id == VersionId);

        _isLoading = false;
        StartPollingIfResolving();
    }

    private void OnVersionChanged(Guid versionId)
    {
        // Troca de versão numa aba por versão = navega para a mesma aba da nova.
        Navigation.NavigateTo($"/modpacks/{ModpackId}/versions/{versionId}/mods");
    }

    // Enquanto a ingestão roda, a versão fica em Resolving. Recarrega até sair
    // desse estado, para os mods aparecerem sem o admin atualizar a página.
    private void StartPollingIfResolving()
    {
        if (_version?.State is not ModpackVersionState.Resolving)
            return;

        _pollTimer ??= new Timer(async _ =>
        {
            await LoadAsync();
            await InvokeAsync(() =>
            {
                StateHasChanged();
                if (_version?.State is not ModpackVersionState.Resolving)
                {
                    _pollTimer?.Dispose();
                    _pollTimer = null;
                }
            });
        }, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    private async Task OpenIngest()
    {
        var parameters = new DialogParameters
        {
            ["VersionId"] = VersionId,
            ["MinecraftVersion"] = _modpack!.MinecraftVersion,
            ["Loader"] = _modpack.Loader
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true };

        var dialog = await DialogService.ShowAsync<ModSearchDialog>(
            "Buscar mods", parameters, options);

        if (await dialog.Result is { Canceled: false })
            await WatchIngestionAsync();
    }

    // Acompanha a versão após enfileirar a ingestão. Cobre a corrida
    // Draft → Resolving → Draft: recarrega a grade a cada tick e para quando a
    // versão assenta (voltou a Draft, entrou em Failed, ou estourou o tempo).
    private async Task WatchIngestionAsync()
    {
        _isIngesting = true;
        StateHasChanged();

        var deadline = DateTime.UtcNow.AddMinutes(3);
        var sawResolving = false;

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(1500);
            await LoadAsync();

            if (_version?.State is ModpackVersionState.Resolving)
                sawResolving = true;

            var settled = _version?.State is ModpackVersionState.Failed
                          || (sawResolving && _version?.State is not ModpackVersionState.Resolving);

            StateHasChanged();

            if (settled)
                break;
        }

        _isIngesting = false;
        StateHasChanged();
    }

    private async Task OpenManualUpload()
    {
        var parameters = new DialogParameters { ["VersionId"] = VersionId };
        var dialog = await DialogService.ShowAsync<ManualUploadDialog>("Enviar arquivo", parameters);

        if (await dialog.Result is { Canceled: false })
            await LoadAsync();
    }

    private async Task RemoveFile(ModpackFile file)
    {
        var confirm = await DialogService.ShowMessageBoxAsync(
            "Remover mod",
            $"Remover '{file.Path}' desta versão?",
            "Remover", cancelText: "Cancelar");

        if (confirm is not true)
            return;

        var result = await RemoveFileUseCase.HandleAsync(VersionId, file.Id, CancellationToken.None);

        if (result.Succeeded)
        {
            Snackbar.Add("Mod removido.", Severity.Success);
            await LoadAsync();
        }
        else
            Snackbar.Add(result.Error!, Severity.Error);
    }

    private static string OriginIcon(ModFileOrigin origin)
    {
        return origin switch
        {
            ModFileOrigin.Modrinth or ModFileOrigin.CurseForge => Icons.Material.Filled.Cloud,
            ModFileOrigin.ManualUpload => Icons.Material.Filled.Upload,
            ModFileOrigin.Override => Icons.Material.Filled.Folder,
            _ => Icons.Material.Filled.HelpOutline
        };
    }

    private static Color OriginColor(ModFileOrigin origin)
    {
        return origin switch
        {
            ModFileOrigin.Modrinth => Color.Success,
            ModFileOrigin.CurseForge => Color.Warning,
            _ => Color.Default
        };
    }

    private async Task OpenCheckUpdates()
    {
        var parameters = new DialogParameters
        {
            ["SourceVersionId"] = _version!.Id, ["SourceVersion"] = _version.Version
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };

        var dialog = await DialogService.ShowAsync<CheckUpdatesDialog>("Verificar atualizações", parameters, options);

        if (await dialog.Result is { Canceled: false, Data: Guid newVersionId })
            Navigation.NavigateTo($"/modpacks/{ModpackId}/versions/{newVersionId}/mods");
    }
}
