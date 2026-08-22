using Microsoft.AspNetCore.Components;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class LoaderVersionPicker : ComponentBase
{
    private bool _releasesOnly = true;
    private IReadOnlyList<string> _versions = [];

    [Parameter] public string Value { get; set; } = "";
    [Parameter] public EventCallback<string> ValueChanged { get; set; }

    /// <summary>Loader e versão do Minecraft do modpack — definem a lista.</summary>
    [Parameter] public ModLoader Loader { get; set; }

    [Parameter] public string MinecraftVersion { get; set; } = "";

    [Parameter] public string Label { get; set; } = "Versão do loader";
    [Parameter] public bool Required { get; set; } = true;
    [Parameter] public string? Class { get; set; }

    [Inject] private IVersionCatalog Catalog { get; set; } = default!;

    protected override Task OnInitializedAsync() => ReloadAsync();

    private async Task ReloadAsync()
    {
        // Sem versão do Minecraft não há lista possível; o campo continua
        // aceitando texto digitado (CoerceValue), que é a saída para um loader
        // que o catálogo ainda não conhece.
        if (string.IsNullOrWhiteSpace(MinecraftVersion))
        {
            _versions = [];
            return;
        }

        _versions = await Catalog.GetLoaderVersionsAsync(
            Loader, MinecraftVersion, _releasesOnly, CancellationToken.None);
    }

    private Task OnChanged(string value) => ValueChanged.InvokeAsync(value);

    private Task<IEnumerable<string>> Search(string value, CancellationToken ct) =>
        Task.FromResult(string.IsNullOrWhiteSpace(value)
            ? _versions.AsEnumerable()
            : _versions.Where(v => v.Contains(value, StringComparison.OrdinalIgnoreCase)));
}
