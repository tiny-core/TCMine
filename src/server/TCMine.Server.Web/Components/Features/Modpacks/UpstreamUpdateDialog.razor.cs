using Microsoft.AspNetCore.Components;
using TCMine.Server.Application.Modpacks;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class UpstreamUpdateDialog : DialogComponentBase
{
    private string? _error;
    private string _latestLabel = "";
    private bool _loading = true;
    private string _newVersion = "";
    private UpstreamMergePlan? _plan;

    [Parameter] public Guid VersionId { get; set; }

    /// <summary>Rótulo do autor na versão atual, só para o texto "4.2 → 4.3".</summary>
    [Parameter] public string CurrentLabel { get; set; } = "";

    /// <summary>Nossa numeração atual, para sugerir a próxima.</summary>
    [Parameter] public string CurrentVersion { get; set; } = "";

    [Inject] private UpdateFromUpstream UpdateUseCase { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        _newVersion = NextVersion(CurrentVersion);

        // dryRun: calcula e mostra o plano sem gravar nada. O admin precisa ver
        // o que vai acontecer antes de aceitar — atualizar às cegas um pack com
        // configs customizados é como aplicar patch sem ler o diff.
        var result = await UpdateUseCase.HandleAsync(VersionId, "", CancellationToken.None, true);

        if (result.Succeeded)
        {
            _plan = result.Value!.Plan;
            _latestLabel = result.Value.LatestLabel;

            if (_plan.IsEmpty)
                _error = "Nada mudou em relação à origem. Você já está na versão do autor.";
        }
        else
            _error = result.Error;

        _loading = false;
    }

    private Task Apply() => SubmitAsync(
        async () =>
        {
            var result = await UpdateUseCase.HandleAsync(VersionId, _newVersion, CancellationToken.None);
            return result.Succeeded
                ? TCMine.Server.Application.Common.Result.Success()
                : TCMine.Server.Application.Common.Result.Fail(result.Error!);
        },
        "Rascunho criado. Os mods novos estão sendo baixados em segundo plano.");

    /// <summary>
    ///     Sugere o próximo minor ("1.0.0" → "1.1.0"). É palpite: o admin edita.
    ///     Atualização de pack costuma trazer mods novos, o que é mudança de
    ///     comportamento — patch subestimaria.
    /// </summary>
    private static string NextVersion(string current)
    {
        var parts = current.Split('.');
        if (parts.Length < 2 || !int.TryParse(parts[1], out var minor))
            return "";

        return $"{parts[0]}.{minor + 1}.0";
    }

    private static string Explain(UpstreamModConflict conflict) => conflict.Kind switch
    {
        UpstreamConflictKind.BothChanged =>
            "Você trocou a versão deste mod e o autor também. A sua fica.",
        UpstreamConflictKind.RemovedHereKeptThere =>
            "Você removeu este mod; o autor continua incluindo. Segue removido.",
        UpstreamConflictKind.ChangedHereRemovedThere =>
            "Você trocou a versão deste mod e o autor o removeu do pack. O seu fica.",
        _ => ""
    };
}
