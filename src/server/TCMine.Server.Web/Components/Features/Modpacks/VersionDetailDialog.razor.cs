using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Web.Components.Features.Modpacks;

// Mantém dois estados de "ocupado" distintos (upload e publicação) em vez do
// IsBusy único da base, porque são botões separados na mesma tela.
public partial class VersionDetailDialog
{
    private bool _changed;
    private bool _isPublishing;
    private bool _isUploading;
    private Timer? _pollTimer;
    private string _targetFolder = "mods";

    private ModpackVersion? _version;

    [Parameter] public Guid VersionId { get; set; }

    [Inject] private IModpackRepository Repository { get; set; } = default!;
    [Inject] private AddManualFile AddManualFileUseCase { get; set; } = default!;
    [Inject] private PublishModpackVersion PublishUseCase { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    public void Dispose()
    {
        _pollTimer?.Dispose();
        GC.SuppressFinalize(this);
    }

    protected override async Task OnInitializedAsync()
    {
        await ReloadAsync();
        StartPollingIfNeeded();
    }

    private async Task ReloadAsync()
    {
        _version = await Repository.GetVersionAsync(VersionId, CancellationToken.None);
    }

    private async Task OnFileSelected(IBrowserFile file)
    {
        _isUploading = true;
        try
        {
            // A pasta vem da escolha do admin; o nome, do arquivo. Assim um
            // config.json vai para config/ e um mod para mods/, em vez de tudo
            // cair em mods/ como antes.
            var path = $"{_targetFolder}/{file.Name}";

            // OpenReadStream tem limite padrão baixo (512 KB). Mods passam disso
            // com folga, então elevamos — 200 MB cobre até os maiores.
            await using var stream = file.OpenReadStream(200 * 1024 * 1024);

            var command = new AddManualFileCommand(
                VersionId,
                path,
                stream,
                file.ContentType,
                FileSide.Both,
                false);

            var result = await AddManualFileUseCase.HandleAsync(command, CancellationToken.None);

            if (result.Succeeded)
            {
                Snackbar.Add($"Arquivo '{path}' adicionado.", Severity.Success);
                _changed = true;
                await ReloadAsync();
            }
            else
            {
                Snackbar.Add(result.Error!, Severity.Error);
            }
        }
        finally
        {
            _isUploading = false;
        }
    }

    private async Task OpenIngestDialog()
    {
        var parameters = new DialogParameters { ["VersionId"] = VersionId };
        var dialog = await DialogService.ShowAsync<IngestModsDialog>("Buscar mods", parameters);
        var result = await dialog.Result;

        // Recarrega para pegar o estado Resolving assim que a ingestão começa.
        if (result is { Canceled: false })
        {
            _changed = true;
            await ReloadAsync();
        }
    }

    private async Task PublishAsync()
    {
        _isPublishing = true;
        try
        {
            var result = await PublishUseCase.HandleAsync(VersionId, CancellationToken.None);

            if (result.Succeeded)
            {
                Snackbar.Add("Versão publicada.", Severity.Success);
                _changed = true;
                await ReloadAsync();
            }
            else
            {
                Snackbar.Add(result.Error!, Severity.Error);
            }
        }
        finally
        {
            _isPublishing = false;
        }
    }

    private void Close()
    {
        // Devolve se algo mudou, para a página de trás decidir se recarrega.
        Dialog.Close(DialogResult.Ok(_changed));
    }

    // Enquanto a versão estiver processando, recarrega a cada 2s para a UI
    // acompanhar a transição para Ready ou Failed sem o admin precisar atualizar
    // a página. Para de sondar assim que sai de Resolving.
    private void StartPollingIfNeeded()
    {
        if (_version?.State is not ModpackVersionState.Resolving)
            return;

        _pollTimer ??= new Timer(async _ =>
        {
            await ReloadAsync();

            // InvokeAsync porque o callback do Timer roda fora do contexto de
            // renderização do Blazor — sem ele, StateHasChanged lança.
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
}
