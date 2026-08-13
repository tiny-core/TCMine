using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Storage;
using TCMine.Server.Web.Background;
using TCMine.UI.Shared.Formatting;

namespace TCMine.Server.Web.Components.Pages.Storage;

public partial class StoragePage : ComponentBase, IDisposable
{
    private readonly HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);

    private bool _hasActiveJobs;
    private bool _isDeleting;
    private bool _isScanning;

    /// <summary>Id do trabalho em curso desta página — é por ele que o progresso chega.</summary>
    private Guid _jobId;

    private StorageReport? _report;

    [Inject] private ScanStorage ScanUseCase { get; set; } = default!;
    [Inject] private DeleteOrphanBlobs DeleteUseCase { get; set; } = default!;
    [Inject] private JobProgressRegistry Jobs { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    /// <summary>Progresso do trabalho desta página, empurrado pelo caso de uso.</summary>
    private JobProgress? Progress => _jobId == Guid.Empty ? null : Jobs.Get(_jobId);

    public void Dispose()
    {
        Jobs.Changed -= OnJobChanged;
        GC.SuppressFinalize(this);
    }

    protected override void OnInitialized() => Jobs.Changed += OnJobChanged;

    private void OnJobChanged() => _ = InvokeAsync(StateHasChanged);

    private double UsedPercent =>
        _report is null or { TotalBytes: 0 } ? 0 : _report.ReferencedBytes * 100d / _report.TotalBytes;

    private double OrphanPercent =>
        _report is null or { TotalBytes: 0 } ? 0 : _report.OrphanBytes * 100d / _report.TotalBytes;

    /// <summary>
    ///     Varre sob demanda, não ao abrir a página. Percorrer dezenas de
    ///     milhares de arquivos é caro, e ninguém abre esta tela por engano —
    ///     quem chega aqui veio para limpar.
    /// </summary>
    private async Task ScanAsync()
    {
        _isScanning = true;
        _selected.Clear();
        _jobId = Guid.CreateVersion7();

        try
        {
            var result = await ScanUseCase.HandleAsync(CancellationToken.None, _jobId);

            if (result.Succeeded)
                _report = result.Value;
            else
                Snackbar.Add(result.Error!, Severity.Error);

            // A limpeza fica bloqueada com trabalho em curso: uma ingestão pode
            // estar gravando blobs neste exato momento. O trabalho desta página
            // já terminou aqui, então não conta contra ela mesma.
            _hasActiveJobs = Jobs.Active.Any(j => j.Key != _jobId);
        }
        finally
        {
            _isScanning = false;
            _jobId = Guid.Empty;
        }
    }

    private void Toggle(string sha256, bool selected)
    {
        if (selected)
            _selected.Add(sha256);
        else
            _selected.Remove(sha256);
    }

    private void SelectAllSafe()
    {
        _selected.Clear();

        foreach (var orphan in _report?.Orphans ?? [])
        {
            if (orphan.Safe)
                _selected.Add(orphan.Sha256);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_selected.Count is 0 || _report is null)
            return;

        var bytes = _report.Orphans
            .Where(o => _selected.Contains(o.Sha256))
            .Sum(o => o.SizeBytes);

        var confirm = await DialogService.ShowMessageBoxAsync(
            "Apagar do disco",
            $"Apagar {_selected.Count} arquivo(s) e liberar {HumanSize.Bytes(bytes)}? "
            + "Os arquivos saem do disco de vez. Se algum voltar a ser preciso, o TCMine o rebaixa da origem "
            + "— exceto os enviados manualmente, que não têm de onde voltar.",
            "Apagar", cancelText: "Cancelar");

        if (confirm is not true)
            return;

        _isDeleting = true;
        _jobId = Guid.CreateVersion7();

        try
        {
            var result = await DeleteUseCase.HandleAsync([.. _selected], CancellationToken.None, _jobId);

            if (!result.Succeeded)
            {
                Snackbar.Add(result.Error!, Severity.Error);
                return;
            }

            var summary = result.Value!;
            Snackbar.Add(
                $"{summary.Deleted} apagado(s), {HumanSize.Bytes(summary.FreedBytes)} liberados."
                + (summary.Skipped > 0 ? $" {summary.Skipped} pulado(s) por segurança." : ""),
                Severity.Success);

            await ScanAsync();
        }
        finally
        {
            _isDeleting = false;
            _jobId = Guid.Empty;
        }
    }
}
