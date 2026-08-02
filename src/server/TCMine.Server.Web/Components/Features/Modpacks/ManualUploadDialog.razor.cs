using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Modpacks;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class ManualUploadDialog
{
    private FileSide _side = FileSide.Both;
    private string _targetFolder = "mods";

    [Parameter] public Guid VersionId { get; set; }

    [Inject] private AddManualFile AddManualFileUseCase { get; set; } = default!;

    private Task OnFileSelected(IBrowserFile file)
    {
        // Pasta escolhida + nome do arquivo. Assim um config.json vai para
        // config/ e um mod para mods/.
        var path = $"{_targetFolder}/{file.Name}";

        return SubmitAsync(
            async () =>
            {
                await using var stream = file.OpenReadStream(200 * 1024 * 1024);
                var command = new AddManualFileCommand(
                    VersionId, path, stream, file.ContentType, _side, false);
                return await AddManualFileUseCase.HandleAsync(command, CancellationToken.None);
            },
            $"'{path}' adicionado.");
    }
}
