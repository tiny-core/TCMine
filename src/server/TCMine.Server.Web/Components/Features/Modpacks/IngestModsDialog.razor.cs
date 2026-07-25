using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class IngestModsDialog : ComponentBase
{
    private bool _isSubmitting;

    private string _rawIds = "";

    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = null!;

    [Parameter] public Guid VersionId { get; set; }

    private void Cancel()
    {
        Dialog.Cancel();
    }

    private async Task SubmitAsync()
    {
        // Uma linha por projeto, ignorando linhas em branco e espaços. O
        // Modrinth é a única origem por ora, então todos os itens vêm dele.
        var items = _rawIds
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(id => new ModIngestionItem(
                ModFileOrigin.Modrinth,
                id,
                null,
                FileSide.Both))
            .ToList();

        if (items.Count is 0)
        {
            Snackbar.Add("Informe ao menos um projeto.", Severity.Warning);
            return;
        }

        _isSubmitting = true;

        var command = new QueueIngestionCommand(VersionId, items);
        var result = await QueueIngestionUseCase.HandleAsync(command, CancellationToken.None);

        _isSubmitting = false;

        if (result.Succeeded)
        {
            Snackbar.Add($"Ingestão de {items.Count} mod(s) iniciada.", Severity.Success);
            Dialog.Close(DialogResult.Ok(true));
        }
        else
        {
            Snackbar.Add(result.Error!, Severity.Error);
        }
    }
}