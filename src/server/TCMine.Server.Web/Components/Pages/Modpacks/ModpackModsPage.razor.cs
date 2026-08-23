using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Web.Background;
using TCMine.Server.Web.Components.Features.Modpacks;

namespace TCMine.Server.Web.Components.Pages.Modpacks;

public partial class ModpackModsPage : ComponentBase, IDisposable
{
    private MudDataGrid<ModpackFile> _grid = default!;
    private bool _isLoading = true;

    /// <summary>Arquivo cujo lado está a ser gravado — trava só a linha dele.</summary>
    private Guid _savingSide;
    private Modpack? _modpack;
    private string _searchString = "";
    private ModpackVersion? _version;

    [Parameter] public Guid ModpackId { get; set; }
    [Parameter] public Guid VersionId { get; set; }

    [Inject] private JobProgressRegistry Jobs { get; set; } = default!;
    [Inject] private ChangeFileSide ChangeSideUseCase { get; set; } = default!;

    /// <summary>
    ///     Carrega só a página pedida, com a busca aplicada em SQL.
    ///     Overrides ficam de fora — têm aba própria, e num pack importado são
    ///     milhares.
    /// </summary>
    private async Task<GridData<ModpackFile>> LoadPageAsync(
        GridState<ModpackFile> state, CancellationToken ct)
    {
        var result = await Repository.ListVersionModsAsync(
            VersionId,
            string.IsNullOrWhiteSpace(_searchString) ? null : _searchString.Trim(),
            new PageRequest(state.Page, state.PageSize),
            ct);

        return new GridData<ModpackFile> { Items = result.Items, TotalItems = result.TotalCount };
    }

    private Task OnSearchChanged(string value)
    {
        _searchString = value;
        return _grid.ReloadServerData();
    }

    /// <summary>
    ///     Ingestão em curso para ESTA versão, vinda do registro de progresso.
    ///     Antes isto era uma sondagem que só parava depois de ver o estado
    ///     "Resolvendo" — e com poucos mods a ingestão terminava antes do
    ///     primeiro tique, então a barra girava os três minutos inteiros com o
    ///     trabalho já feito.
    /// </summary>
    private JobProgress? Progress => Jobs.Get(VersionId);

    private bool IsIngesting => Progress is not null;

    public void Dispose()
    {
        Jobs.Changed -= OnJobChanged;
        GC.SuppressFinalize(this);
    }

    protected override async Task OnInitializedAsync()
    {
        Jobs.Changed += OnJobChanged;
        await LoadAsync();
    }

    private void OnJobChanged() => _ = InvokeAsync(async () =>
    {
        // Terminou: os arquivos novos já estão no banco, então vale reler.
        if (Jobs.TryConsumeCompletion(VersionId, out var erro))
        {
            await LoadAsync();

            if (erro is { Length: > 0 })
                Snackbar.Add(erro, Severity.Error);
        }

        StateHasChanged();
    });

    /// <summary>
    ///     Troca o lado de um arquivo. Sem recarregar a grade inteira: são
    ///     milhares de linhas num pack importado, e o que mudou foi uma célula.
    /// </summary>
    private async Task ChangeSide(ModpackFile file, FileSide side)
    {
        if (_savingSide != Guid.Empty || file.Side == side)
            return;

        _savingSide = file.Id;
        try
        {
            var result = await ChangeSideUseCase.HandleAsync(
                VersionId, file.Id, side, CancellationToken.None);

            if (result.Succeeded)
                file.Side = side;
            else
                Snackbar.Add(result.Error!, Severity.Error);
        }
        finally
        {
            _savingSide = Guid.Empty;
        }
    }

    private async Task LoadAsync()
    {
        _isLoading = true;

        // Carrega o modpack com todas as versões (para o seletor do workspace) e
        // pega a versão da rota entre elas — uma consulta só.
        _modpack = await Repository.GetWithVersionsAsync(ModpackId, CancellationToken.None);
        _version = _modpack?.Versions.FirstOrDefault(v => v.Id == VersionId);

        _isLoading = false;

        // A grade tem os próprios dados; depois de uma recarga (ingestão que
        // terminou, mod removido) ela precisa reler a página corrente.
        if (_grid is not null)
            await _grid.ReloadServerData();
    }

    private void OnVersionChanged(Guid versionId)
    {
        // Troca de versão numa aba por versão = navega para a mesma aba da nova.
        Navigation.NavigateTo($"/modpacks/{ModpackId}/versions/{versionId}/mods");
    }

    // Enquanto a ingestão roda, a versão fica em Resolving. Recarrega até sair
    // desse estado, para os mods aparecerem sem o admin atualizar a página.
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

        // Nada de esperar aqui: o registro empurra o progresso e o fim.
        if (await dialog.Result is { Canceled: false })
            await LoadAsync();
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
