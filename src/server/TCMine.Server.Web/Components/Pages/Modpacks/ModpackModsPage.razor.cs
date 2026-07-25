using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Web.Components.Features.Modpacks;

namespace TCMine.Server.Web.Components.Pages.Modpacks;

public partial class ModpackModsPage : ComponentBase, IDisposable
{
    private List<BreadcrumbItem> _breadcrumbs = [];
    private bool _isIngesting;
    private bool _isLoading = true;

    private bool _isPublishing;
    private Timer? _pollTimer;
    private string _searchString = "";
    private ModpackVersion? _version;
    [Parameter] public Guid ModpackId { get; set; }
    [Parameter] public Guid VersionId { get; set; }

    // Filtro em memória: a lista de mods de uma versão cabe tranquilamente,
    // então não vale ida ao banco a cada tecla.
    private IEnumerable<ModpackFile> FilteredFiles =>
        string.IsNullOrWhiteSpace(_searchString)
            ? _version?.Files ?? []
            : (_version?.Files ?? []).Where(f =>
                f.Path.Contains(_searchString, StringComparison.OrdinalIgnoreCase));

    public void Dispose()
    {
        _pollTimer?.Dispose();
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _version = await Repository.GetVersionAsync(VersionId, CancellationToken.None);

        _breadcrumbs =
        [
            new BreadcrumbItem("Modpacks", "/modpacks"),
            new BreadcrumbItem("Modpack", $"/modpacks/{ModpackId}"),
            new BreadcrumbItem("Mods", null, true)
        ];

        _isLoading = false;
        StartPollingIfResolving();
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
            ["MinecraftVersion"] = _version!.MinecraftVersion,
            ["Loader"] = _version.Loader
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true };

        var dialog = await DialogService.ShowAsync<ModrinthSearchDialog>(
            "Buscar mods no Modrinth", parameters, options);

        // Se o diálogo enfileirou uma ingestão, acompanhamos até terminar.
        if (await dialog.Result is { Canceled: false })
            await WatchIngestionAsync();
    }

    // Acompanha a versão após enfileirar a ingestão. Cobre a corrida
    // Draft → Resolving → Draft: em vez de sondar só "enquanto Resolving",
    // recarrega a grade a cada tick e para quando a versão assenta (voltou a
    // Draft depois de processar, entrou em Failed, ou estourou o tempo).
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

    private async Task Publish()
    {
        if (_version is null)
            return;

        var confirm = await DialogService.ShowMessageBoxAsync(
            "Publicar versão",
            $"Publicar a versão {_version.Version}? A partir daqui ela fica imutável — não dá " +
            "para adicionar, remover ou trocar mods. Para mudanças futuras, cria uma nova versão.",
            "Publicar", cancelText: "Cancelar");

        if (confirm is not true)
            return;

        _isPublishing = true;
        StateHasChanged();
        try
        {
            var result = await PublishUseCase.HandleAsync(VersionId, CancellationToken.None);

            if (result.Succeeded)
            {
                Snackbar.Add("Versão publicada.", Severity.Success);
                await LoadAsync(); // recarrega: chip vira Publicado, ações de edição somem
            }
            else
            {
                Snackbar.Add(result.Error!, Severity.Error);
            }
        }
        finally
        {
            _isPublishing = false;
            StateHasChanged();
        }
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
        {
            Snackbar.Add(result.Error!, Severity.Error);
        }
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
}