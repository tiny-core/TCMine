using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Modpacks;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class EditModpackDialog
{
    private MudForm _form = null!;
    private string _name = "";
    private string _summary = "";

    [Parameter] public Guid ModpackId { get; set; }
    [Parameter] public string Name { get; set; } = "";
    [Parameter] public string? Summary { get; set; }
    [Parameter] public string Slug { get; set; } = "";
    [Parameter] public string MinecraftVersion { get; set; } = "";
    [Parameter] public ModLoader Loader { get; set; }

    [Inject] private UpdateModpack UpdateUseCase { get; set; } = default!;

    protected override void OnInitialized()
    {
        _name = Name;
        _summary = Summary ?? "";
    }

    private async Task Submit()
    {
        await _form.ValidateAsync();
        if (!_form.IsValid)
            return;

        await SubmitAsync(
            () => UpdateUseCase.HandleAsync(
                ModpackId,
                _name,
                string.IsNullOrWhiteSpace(_summary) ? null : _summary,
                CancellationToken.None),
            "Modpack atualizado.");
    }
}
