using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Modpacks;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class CreateVersionDialog : ComponentBase
{
    private MudForm _form = null!;
    private bool _inheritFiles = true;
    private bool _isSaving;
    private bool _loaderReleasesOnly = true;
    private string _loaderVersion = "";
    private IReadOnlyList<string> _loaderVersions = [];
    private int? _memoryMb;
    private string _version = "";

    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = null!;

    [Parameter] public string MinecraftVersion { get; set; } = "";
    [Parameter] public ModLoader Loader { get; set; }
    [Parameter] public Guid ModpackId { get; set; }
    [Parameter] public string? DefaultLoaderVersion { get; set; }
    [Parameter] public int? DefaultMemoryMb { get; set; }
    [Parameter] public string? DefaultVersion { get; set; }

    protected override async void OnInitialized()
    {
        // Versão sugerida: patch da última + 1, marcada alpha. Só o número muda
        // entre versões; MC/loader/RAM herdam da última publicação.
        _version = PackVersion.SuggestNext(DefaultVersion);

        if (DefaultLoaderVersion is not null) _loaderVersion = DefaultLoaderVersion;

        _memoryMb = DefaultMemoryMb;

        await ReloadLoaderVersions();
    }

    private async Task ReloadLoaderVersions()
    {
        if (string.IsNullOrWhiteSpace(MinecraftVersion))
        {
            _loaderVersions = [];
            return;
        }

        _loaderVersions = await Catalog.GetLoaderVersionsAsync(
            Loader, MinecraftVersion, _loaderReleasesOnly, CancellationToken.None);
    }

    private Task<IEnumerable<string>> SearchLoader(string value, CancellationToken ct) =>
        Task.FromResult(Filter(_loaderVersions, value));

    private static IEnumerable<string> Filter(IReadOnlyList<string> all, string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? all
            : all.Where(v => v.Contains(value, StringComparison.OrdinalIgnoreCase));
    }

    private void Cancel() => Dialog.Cancel();

    private async Task SubmitAsync()
    {
        await _form.ValidateAsync();
        if (!_form.IsValid)
            return;

        _isSaving = true;

        var command = new CreateModpackVersionCommand(
            ModpackId,
            _version,
            _loaderVersion,
            _memoryMb,
            _inheritFiles);

        var result = await CreateVersionUseCase.HandleAsync(command, CancellationToken.None);

        _isSaving = false;

        if (result.Succeeded)
        {
            Snackbar.Add("Versão criada como rascunho.", Severity.Success);
            Dialog.Close(DialogResult.Ok(result.Value));
        }
        else
            Snackbar.Add(result.Error!, Severity.Error);
    }
}
