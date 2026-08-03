using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Web.Components.Features.Modpacks;

namespace TCMine.Server.Web.Components.Pages.Modpacks;

public partial class ModpackDetailPage : ComponentBase, IDisposable
{
    private bool _isLoading = true;
    private bool _isPublishing;
    private Modpack? _modpack;
    private Timer? _pollTimer;
    private ModpackVersion? _selectedVersion;
    private Guid _selectedVersionId;
    private int _serverCount;

    [Parameter] public Guid ModpackId { get; set; }

    // Mods e overrides moram na mesma coleção Files, separados pela Origin.
    private int ModCount =>
        _selectedVersion?.Files.Count(f => f.Origin != ModFileOrigin.Override) ?? 0;

    private int OverrideCount =>
        _selectedVersion?.Files.Count(f => f.Origin == ModFileOrigin.Override) ?? 0;

    private long TotalSizeBytes => _selectedVersion?.Files.Sum(f => f.SizeBytes) ?? 0;

    [Inject] private PublishModpackVersion PublishUseCase { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private DeleteModpackVersion DeleteVersionUseCase { get; set; } = default!;
    [Inject] private ArchiveModpackVersion ArchiveUseCase { get; set; } = default!;
    [Inject] private RestoreModpackVersion RestoreUseCase { get; set; } = default!;
    [Inject] private IServerRepository ServerRepository { get; set; } = default!;

    public void Dispose()
    {
        _pollTimer?.Dispose();
        GC.SuppressFinalize(this);
    }

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _isLoading = true;

        _modpack = await Repository.GetWithVersionsAsync(ModpackId, CancellationToken.None);

        if (_modpack is not null)
        {
            // Preserva a versão selecionada entre recargas (poll, publish); se ela
            // sumiu ou não havia seleção, cai na mais recente (lista por Id desc).
            var selected = _selectedVersionId != Guid.Empty
                ? _modpack.Versions.FirstOrDefault(v => v.Id == _selectedVersionId)
                : null;
            selected ??= _modpack.Versions.OrderByDescending(v => v.Id).FirstOrDefault();

            if (selected is not null)
            {
                _selectedVersionId = selected.Id;
                _selectedVersion = selected;
            }

            _serverCount = (await ServerRepository.ListByModpackAsync(ModpackId, CancellationToken.None)).Count;
        }

        _isLoading = false;
        StartPollingIfResolving();
    }

    // Enquanto a versão selecionada está resolvendo, recarrega a cada 2s para o
    // stepper e os contadores acompanharem a transição (→ Draft/Ready/Failed) sem
    // o admin atualizar a página. Para de sondar quando sai de Resolving.
    private void StartPollingIfResolving()
    {
        if (_selectedVersion?.State is not ModpackVersionState.Resolving)
            return;

        _pollTimer ??= new Timer(async _ =>
        {
            await LoadAsync();
            await InvokeAsync(() =>
            {
                StateHasChanged();
                if (_selectedVersion?.State is not ModpackVersionState.Resolving)
                {
                    _pollTimer?.Dispose();
                    _pollTimer = null;
                }
            });
        }, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    private void OnVersionChanged(Guid versionId)
    {
        // Troca em memória: a Visão geral já tem todas as versões carregadas.
        _selectedVersionId = versionId;
        _selectedVersion = _modpack?.Versions.FirstOrDefault(v => v.Id == versionId);
    }

    private async Task OpenCreateVersion()
    {
        var latest = _modpack?.Versions.OrderByDescending(v => v.Id).FirstOrDefault();

        var parameters = new DialogParameters
        {
            ["ModpackId"] = ModpackId,
            ["DefaultVersion"] = latest?.Version,
            ["MinecraftVersion"] = _modpack?.MinecraftVersion,
            ["Loader"] = _modpack?.Loader,
            ["DefaultLoaderVersion"] = latest?.LoaderVersion,
            ["DefaultMemoryMb"] = latest?.RecommendedMemoryMb
        };

        var dialog = await DialogService.ShowAsync<CreateVersionDialog>("Nova versão", parameters);
        if (await dialog.Result is { Canceled: false })
            await LoadAsync();
    }

    private async Task DeleteVersion()
    {
        if (_selectedVersion is null)
            return;

        var confirm = await DialogService.ShowMessageBoxAsync(
            "Apagar rascunho",
            $"Apagar a versão {_selectedVersion.Version}? Os mods e overrides desta versão são removidos. Irreversível.",
            "Apagar", cancelText: "Cancelar");
        if (confirm is not true)
            return;

        var result = await DeleteVersionUseCase.HandleAsync(_selectedVersion.Id, CancellationToken.None);
        if (result.Succeeded)
        {
            Snackbar.Add("Rascunho apagado.", Severity.Success);
            _selectedVersionId = Guid.Empty;
            await LoadAsync();
        }
        else
            Snackbar.Add(result.Error!, Severity.Error);
    }

    private async Task Publish()
    {
        if (_selectedVersion is null)
            return;

        var confirm = await DialogService.ShowMessageBoxAsync(
            "Publicar versão",
            $"Publicar a versão {_selectedVersion.Version}? A partir daqui ela fica imutável — "
            + "para mudanças, cria uma nova versão.",
            "Publicar", cancelText: "Cancelar");
        if (confirm is not true)
            return;

        _isPublishing = true;
        try
        {
            var result = await PublishUseCase.HandleAsync(_selectedVersion.Id, CancellationToken.None);
            if (result.Succeeded)
            {
                Snackbar.Add("Versão publicada.", Severity.Success);
                await LoadAsync();
            }
            else
                Snackbar.Add(result.Error!, Severity.Error);
        }
        finally
        {
            _isPublishing = false;
        }
    }

    private async Task OpenEditVersion()
    {
        var p = new DialogParameters
        {
            ["VersionId"] = _selectedVersion!.Id,
            ["Version"] = _selectedVersion.Version,
            ["MemoryMb"] = _selectedVersion.RecommendedMemoryMb
        };
        var dialog = await DialogService.ShowAsync<EditVersionDialog>("Editar versão", p);
        if (await dialog.Result is { Canceled: false })
            await LoadAsync();
    }

    private async Task OpenCheckUpdates()
    {
        if (_selectedVersion is null)
            return;

        var parameters = new DialogParameters
        {
            ["SourceVersionId"] = _selectedVersion.Id,
            ["SourceVersion"] = _selectedVersion.Version
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };

        var dialog = await DialogService.ShowAsync<CheckUpdatesDialog>("Verificar atualizações", parameters, options);

        // Ok devolve o Id do Draft novo — leva o admin direto para os mods dele.
        if (await dialog.Result is { Canceled: false, Data: Guid newVersionId })
            Navigation.NavigateTo($"/modpacks/{ModpackId}/versions/{newVersionId}/mods");
    }

    private async Task Archive()
    {
        if (_selectedVersion is null)
            return;

        var confirm = await DialogService.ShowMessageBoxAsync(
            "Arquivar versão",
            $"Arquivar a versão {_selectedVersion.Version}? Ela some de novas instalações, mas quem "
            + "já a usa continua rodando. Dá para restaurar depois.",
            "Arquivar", cancelText: "Cancelar");
        if (confirm is not true)
            return;

        var result = await ArchiveUseCase.HandleAsync(_selectedVersion.Id, CancellationToken.None);
        if (result.Succeeded)
        {
            Snackbar.Add("Versão arquivada.", Severity.Success);
            await LoadAsync();
        }
        else
            Snackbar.Add(result.Error!, Severity.Error);
    }

    private async Task Restore()
    {
        if (_selectedVersion is null)
            return;

        var result = await RestoreUseCase.HandleAsync(_selectedVersion.Id, CancellationToken.None);
        if (result.Succeeded)
        {
            Snackbar.Add("Versão restaurada.", Severity.Success);
            await LoadAsync();
        }
        else
            Snackbar.Add(result.Error!, Severity.Error);
    }

    // ---- Card "próximo passo": severidade e textos conforme o estado ----

    private static Severity NextSeverity(ModpackVersionState state) => state switch
    {
        ModpackVersionState.Ready => Severity.Success,
        ModpackVersionState.Failed => Severity.Error,
        ModpackVersionState.Resolving => Severity.Info,
        ModpackVersionState.Archived => Severity.Normal,
        _ => Severity.Info
    };

    private static string NextTitle(ModpackVersionState state) => state switch
    {
        ModpackVersionState.Draft => "Rascunho editável",
        ModpackVersionState.Resolving => "Processando mods…",
        ModpackVersionState.Ready => "Versão publicada e imutável",
        ModpackVersionState.Failed => "A resolução falhou",
        ModpackVersionState.Archived => "Versão arquivada",
        _ => ""
    };

    private static string NextHint(ModpackVersionState state) => state switch
    {
        ModpackVersionState.Draft => "Adicione mods e overrides; publique quando estiver pronto.",
        ModpackVersionState.Resolving => "Baixando e verificando os arquivos. Isto atualiza sozinho.",
        ModpackVersionState.Ready => "Pronta para uso. Crie um servidor ou verifique se há mods mais novos.",
        ModpackVersionState.Failed => "Revise os mods e tente novamente numa nova versão.",
        ModpackVersionState.Archived => "Some de novas instalações, mas quem já a fixou continua rodando.",
        _ => ""
    };
}
