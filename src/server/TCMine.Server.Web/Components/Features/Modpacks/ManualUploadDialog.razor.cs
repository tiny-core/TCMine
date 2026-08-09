using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Modpacks;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class ManualUploadDialog
{
    private FileSide _side = FileSide.Both;
    private string _targetFolder = "mods";

    protected override void OnInitialized()
    {
        // Resolver pendência é sempre um .jar: já abre na pasta certa.
        if (ProjectSlug is { Length: > 0 })
            _targetFolder = "mods";
    }

    [Parameter] public Guid VersionId { get; set; }

    /// <summary>
    ///     Preenchido quando o upload existe para fechar uma pendência: o slug
    ///     amarra o .jar ao mod que faltava, e é por ele que a pendência some.
    /// </summary>
    [Parameter] public string? ProjectSlug { get; set; }

    /// <summary>Nome do mod pendente, só para o texto do diálogo.</summary>
    [Parameter] public string? PendingName { get; set; }

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
                    VersionId, path, stream, file.ContentType, _side, false, ProjectSlug);
                return await AddManualFileUseCase.HandleAsync(command, CancellationToken.None);
            },
            $"'{path}' adicionado.");
    }
}
