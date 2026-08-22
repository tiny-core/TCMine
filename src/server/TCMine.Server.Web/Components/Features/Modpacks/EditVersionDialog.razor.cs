using Microsoft.AspNetCore.Components;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Modpacks;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class EditVersionDialog
{
    private string _loaderVersion = "";
    private int? _memoryMb;
    private string _version = "";

    [Parameter] public Guid VersionId { get; set; }
    [Parameter] public string Version { get; set; } = "";
    [Parameter] public string LoaderVersion { get; set; } = "";
    [Parameter] public int? MemoryMb { get; set; }

    /// <summary>Do modpack, para o picker montar a lista certa.</summary>
    [Parameter] public ModLoader Loader { get; set; }

    [Parameter] public string MinecraftVersion { get; set; } = "";

    [Inject] private UpdateModpackVersion UpdateUseCase { get; set; } = default!;

    protected override void OnInitialized()
    {
        _version = Version;
        _loaderVersion = LoaderVersion;
        _memoryMb = MemoryMb;
    }

    private Task Save() =>
        SubmitAsync(() => UpdateUseCase.HandleAsync(
            VersionId, _version, _loaderVersion, _memoryMb, CancellationToken.None));
}
