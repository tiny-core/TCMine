using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Modpacks;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class CreateVersionDialog : ComponentBase
{
    private MudForm _form = null!;
    private bool _isSaving;
    private ModLoader _loader = ModLoader.NeoForge;
    private string _loaderVersion = "";
    private int? _memoryMb;
    private string _minecraftVersion = "";
    private string _version = "";

    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = null!;

    [Parameter] public Guid ModpackId { get; set; }

    [Parameter] public string? DefaultMinecraftVersion { get; set; }
    [Parameter] public ModLoader? DefaultLoader { get; set; }
    [Parameter] public string? DefaultLoaderVersion { get; set; }
    [Parameter] public int? DefaultMemoryMb { get; set; }
    [Parameter] public string? DefaultVersion { get; set; }

    protected override void OnInitialized()
    {
        // Versão sugerida: patch da última + 1, marcada alpha. Só o número muda
        // entre versões; MC/loader/RAM herdam da última publicação.
        _version = PackVersion.SuggestNext(DefaultVersion);

        // Pré-preenche com a última versão: MC, loader e RAM raramente mudam
        // entre versões do mesmo pack. Só o número da versão fica em branco.
        if (DefaultMinecraftVersion is not null) _minecraftVersion = DefaultMinecraftVersion;
        if (DefaultLoader is not null) _loader = DefaultLoader.Value;
        if (DefaultLoaderVersion is not null) _loaderVersion = DefaultLoaderVersion;

        _memoryMb = DefaultMemoryMb;
    }

    private void Cancel()
    {
        Dialog.Cancel();
    }

    private async Task SubmitAsync()
    {
        await _form.ValidateAsync();
        if (!_form.IsValid)
            return;

        _isSaving = true;

        var command = new CreateModpackVersionCommand(
            ModpackId,
            _version,
            _minecraftVersion,
            _loader,
            _loaderVersion,
            _memoryMb);

        var result = await CreateVersionUseCase.HandleAsync(command, CancellationToken.None);

        _isSaving = false;

        if (result.Succeeded)
        {
            Snackbar.Add("Versão criada como rascunho.", Severity.Success);
            Dialog.Close(DialogResult.Ok(result.Value));
        }
        else
        {
            Snackbar.Add(result.Error!, Severity.Error);
        }
    }
}