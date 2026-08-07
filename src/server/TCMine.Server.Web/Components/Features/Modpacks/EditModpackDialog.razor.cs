using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Modpacks;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class EditModpackDialog
{
    private MudForm _form = null!;
    private IBrowserFile? _icon;
    private string? _iconName;
    private string _name = "";
    private string _summary = "";

    [Parameter] public Guid ModpackId { get; set; }
    [Parameter] public string Name { get; set; } = "";
    [Parameter] public string? Summary { get; set; }
    [Parameter] public string Slug { get; set; } = "";
    [Parameter] public string MinecraftVersion { get; set; } = "";
    [Parameter] public ModLoader Loader { get; set; }

    /// <summary>URL da capa atual, para preview (null = ainda sem capa).</summary>
    [Parameter]
    public string? IconUrl { get; set; }

    [Inject] private UpdateModpack UpdateUseCase { get; set; } = default!;
    [Inject] private SetModpackIcon IconUseCase { get; set; } = default!;

    protected override void OnInitialized()
    {
        _name = Name;
        _summary = Summary ?? "";
    }

    private void OnIconPicked(IBrowserFile file)
    {
        _icon = file;
        _iconName = file.Name;
    }

    private Task Submit()
    {
        return RunAsync(async () =>
        {
            await _form.ValidateAsync();
            if (!_form.IsValid)
                return;

            var result = await UpdateUseCase.HandleAsync(
                ModpackId,
                _name,
                string.IsNullOrWhiteSpace(_summary) ? null : _summary,
                CancellationToken.None);
            if (!result.Succeeded)
            {
                Snackbar.Add(result.Error!, Severity.Error);
                return;
            }

            if (_icon is not null)
            {
                await using var stream = _icon.OpenReadStream(5 * 1024 * 1024);
                var iconResult = await IconUseCase.HandleAsync(
                    ModpackId, stream, _icon.ContentType, CancellationToken.None);
                if (!iconResult.Succeeded)
                    Snackbar.Add($"Salvo, mas a capa falhou: {iconResult.Error}", Severity.Warning);
            }

            Snackbar.Add("Modpack atualizado.", Severity.Success);
            Dialog.Close(DialogResult.Ok(true));
        });
    }
}
