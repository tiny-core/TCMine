using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Modpacks;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class CreateVersionDialog
{
    private MudForm _form = null!;
    private bool _inheritFiles = true;
    private string _loaderVersion = "";
    private int? _memoryMb;
    private string _version = "";
    private Guid? _sourceVersionId;

    [Parameter] public string MinecraftVersion { get; set; } = "";
    [Parameter] public ModLoader Loader { get; set; }
    [Parameter] public Guid ModpackId { get; set; }
    [Parameter] public string? DefaultLoaderVersion { get; set; }
    [Parameter] public int? DefaultMemoryMb { get; set; }
    [Parameter] public string? DefaultVersion { get; set; }

    /// <summary>Versões publicadas, mais recente primeiro: de onde copiar.</summary>
    [Parameter] public IReadOnlyList<SourceVersionOption> SourceVersions { get; set; } = [];

    [Parameter] public Guid? DefaultSourceVersionId { get; set; }

    [Inject] private CreateModpackVersion CreateVersionUseCase { get; set; } = default!;

    protected override void OnInitialized()
    {
        // Versão sugerida: patch da última + 1, marcada alpha. Só o número muda
        // entre versões; MC/loader/RAM herdam da última publicação.
        _version = PackVersion.SuggestNext(DefaultVersion);

        if (DefaultLoaderVersion is not null) _loaderVersion = DefaultLoaderVersion;

        _memoryMb = DefaultMemoryMb;
        _sourceVersionId = DefaultSourceVersionId;

        // Sem nenhuma publicada não há de onde herdar: o interruptor começaria
        // ligado prometendo uma cópia que não acontece.
        if (SourceVersions.Count is 0)
            _inheritFiles = false;
    }

    private string SourceLabel =>
        SourceVersions.FirstOrDefault(v => v.Id == _sourceVersionId)?.Label ?? "";

    private async Task Submit()
    {
        await _form.ValidateAsync();
        if (!_form.IsValid)
            return;

        var command = new CreateModpackVersionCommand(
            ModpackId,
            _version,
            _loaderVersion,
            _memoryMb,
            _inheritFiles,
            _inheritFiles ? _sourceVersionId : null);

        await SubmitAsync(
            () => CreateVersionUseCase.HandleAsync(command, CancellationToken.None),
            _inheritFiles
                ? $"Versão criada como rascunho, com os mods da {SourceLabel}."
                : "Versão criada como rascunho vazio — adicione os mods na grade.");
    }
}
