using Microsoft.AspNetCore.Components;
using TCMine.Server.Application.Modpacks;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class EditVersionDialog
{
    private int? _memoryMb;
    private string _version = "";

    [Parameter] public Guid VersionId { get; set; }
    [Parameter] public string Version { get; set; } = "";
    [Parameter] public int? MemoryMb { get; set; }

    [Inject] private UpdateModpackVersion UpdateUseCase { get; set; } = default!;

    protected override void OnInitialized()
    {
        _version = Version;
        _memoryMb = MemoryMb;
    }

    private Task Save()
    {
        return SubmitAsync(
            () => UpdateUseCase.HandleAsync(VersionId, _version, _memoryMb, CancellationToken.None));
    }
}
