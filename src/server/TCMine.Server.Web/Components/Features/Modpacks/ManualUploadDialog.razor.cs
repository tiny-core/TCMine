using TCMine.Server.Domain.Modpacks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Modpacks;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class ManualUploadDialog
{
    /// <summary>Bytes já enviados e total, para a barra do upload.</summary>
    private long _sent;

    private FileSide _side = FileSide.Both;
    private long _total;

    private double SentPercent => _total > 0 ? Math.Clamp(_sent * 100d / _total, 0, 100) : 0;
    private string _targetFolder = "mods";

    protected override void OnInitialized()
    {
        // Resolver pendência é sempre um .jar: já abre na pasta certa.
        if (ProjectSlug is { Length: > 0 })
            _targetFolder = InstanceFolders.Mods;

        if (DefaultFolder is { Length: > 0 } pasta)
            _targetFolder = pasta;

        if (DefaultSide is { } lado)
            _side = lado;
    }

    [Parameter] public Guid VersionId { get; set; }

    /// <summary>
    ///     Preenchido quando o upload existe para fechar uma pendência: o slug
    ///     amarra o .jar ao mod que faltava, e é por ele que a pendência some.
    /// </summary>
    [Parameter] public string? ProjectSlug { get; set; }

    /// <summary>Nome do mod pendente, só para o texto do diálogo.</summary>
    [Parameter] public string? PendingName { get; set; }

    /// <summary>
    ///     Pasta e lado sugeridos por quem abriu. A aba de recursos já sabe que
    ///     um shaderpack vai para shaderpacks/ e é de cliente; obrigar o admin a
    ///     repetir isso a cada envio seria pedir que ele acerte de cabeça o que
    ///     a tela já sabe.
    /// </summary>
    [Parameter] public string? DefaultFolder { get; set; }

    [Parameter] public FileSide? DefaultSide { get; set; }

    [Inject] private AddManualFile AddManualFileUseCase { get; set; } = default!;

    private Task OnFileSelected(IBrowserFile file)
    {
        // Pasta escolhida + nome do arquivo. Assim um config.json vai para
        // config/ e um mod para mods/.
        var path = $"{_targetFolder}/{file.Name}";

        return SubmitAsync(
            async () =>
            {
                _sent = 0;
                _total = file.Size;

                // Envolve o stream do navegador para contar o que passa: um .jar
                // de 200 MB leva minutos, e sem isto a janela fica muda.
                await using var browserStream = file.OpenReadStream(200 * 1024 * 1024);
                await using var stream = new ProgressStream(browserStream, file.Size, (sent, total) =>
                {
                    _sent = sent;
                    _total = total;
                    _ = InvokeAsync(StateHasChanged);
                });

                var command = new AddManualFileCommand(
                    VersionId, path, stream, file.ContentType, _side, false, ProjectSlug);
                return await AddManualFileUseCase.HandleAsync(command, CancellationToken.None);
            },
            $"'{path}' adicionado.");
    }
}
