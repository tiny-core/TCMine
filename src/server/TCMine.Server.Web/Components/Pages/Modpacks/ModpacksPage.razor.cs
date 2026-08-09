using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Web.Components.Features.Modpacks;
using TCMine.Server.Web.Background;
using TCMine.Server.Web.Mapping;

namespace TCMine.Server.Web.Components.Pages.Modpacks;

public partial class ModpacksPage : ComponentBase, IDisposable
{
    private bool _isLoading = true;
    private IReadOnlyList<ModpackDto> _modpacks = [];

    /// <summary>Quantos modpacks havia na última carga — detecta o pack novo.</summary>
    private int _lastCount;

    /// <summary>Havia trabalho em curso no último aviso — detecta o "acabou".</summary>
    private bool _hadJobs;

    [Inject] private JobProgressRegistry Jobs { get; set; } = default!;

    public void Dispose()
    {
        Jobs.Changed -= OnJobChanged;
        GC.SuppressFinalize(this);
    }

    protected override async Task OnInitializedAsync()
    {
        // A importação roda em background e cria o modpack no meio do caminho —
        // sem ouvir o registro, o card só aparecia com F5.
        Jobs.Changed += OnJobChanged;
        await LoadAsync();
    }

    private void OnJobChanged() => _ = InvokeAsync(async () =>
    {
        // Recarrega a lista e só re-renderiza se algo de fato mudou: o registro
        // dispara a cada mod baixado, e redesenhar a grade centenas de vezes por
        // minuto seria desperdício.
        var beforeCount = _lastCount;
        var hadJobs = _hadJobs;
        _hadJobs = Jobs.Active.Count > 0;

        await LoadAsync();

        // Redesenha quando surgiu um modpack novo (importação criou o card) ou
        // quando o último trabalho terminou (contadores do card mudaram). A cada
        // mod baixado não: seriam centenas de re-renders por minuto à toa.
        if (_lastCount != beforeCount || (hadJobs && !_hadJobs))
            StateHasChanged();
    });

    private async Task LoadAsync()
    {
        _isLoading = true;

        var entities = await Repository.ListAsync(CancellationToken.None);
        _modpacks = [.. entities.Select(m => m.ToDto())];
        _lastCount = _modpacks.Count;

        _isLoading = false;
    }

    private async Task Delete(ModpackDto pack)
    {
        var confirm = await DialogService.ShowMessageBoxAsync(
            "Apagar modpack",
            $"Apagar \"{pack.Name}\"? Todas as versões e arquivos serão removidos. Isto é irreversível.",
            "Apagar", cancelText: "Cancelar");
        if (confirm is not true)
            return;

        var result = await DeleteUseCase.HandleAsync(pack.Id, CancellationToken.None);
        if (result.Succeeded)
        {
            Snackbar.Add("Modpack apagado.", Severity.Success);
            await LoadAsync(); // ou o método que recarrega _modpacks
        }
        else
        {
            // A barreira dos servidores volta como mensagem clara aqui.
            Snackbar.Add(result.Error!, Severity.Error);
        }
    }

    private async Task OpenCreateDialog()
    {
        var dialog = await DialogService.ShowAsync<CreateModpackDialog>("Novo modpack");
        var result = await dialog.Result;

        // Recarrega só se o diálogo confirmou a criação. Cancelar não deve
        // custar uma ida ao banco.
        if (result is { Canceled: false })
            await LoadAsync();
    }

    private async Task OpenImportDialog()
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<ImportPackDialog>("Importar modpack", options);

        if (await dialog.Result is { Canceled: false })
            await LoadAsync();
    }

    private async Task OpenEditDialog(ModpackDto pack)
    {
        var parameters = new DialogParameters
        {
            ["ModpackId"] = pack.Id,
            ["Name"] = pack.Name,
            ["Summary"] = pack.Summary,
            ["Slug"] = pack.Slug,
            ["MinecraftVersion"] = pack.MinecraftVersion,
            ["Loader"] = pack.Loader,
            ["IconUrl"] = pack.IconUrl?.ToString()
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true };

        var dialog = await DialogService.ShowAsync<EditModpackDialog>("Editar modpack", parameters, options);
        if (await dialog.Result is { Canceled: false })
            await LoadAsync();
    }
}
