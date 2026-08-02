using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class IngestModsDialog
{
    private string _rawIds = "";

    [Parameter] public Guid VersionId { get; set; }

    [Inject] private QueueIngestion QueueIngestionUseCase { get; set; } = default!;

    private Task Submit()
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
            return Task.CompletedTask;
        }

        var command = new QueueIngestionCommand(VersionId, items);
        return SubmitAsync(
            () => QueueIngestionUseCase.HandleAsync(command, CancellationToken.None),
            $"Ingestão de {items.Count} mod(s) iniciada.");
    }
}
