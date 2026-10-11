using Microsoft.AspNetCore.Components;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class LoaderVersionPicker : ComponentBase
{
    /// <summary>A fonte não respondeu: a lista vazia não quer dizer "não existe".</summary>
    private bool _failed;

    private bool _isLoading;
    private bool _releasesOnly = true;
    private IReadOnlyList<string> _versions = [];

    [Parameter] public string Value { get; set; } = "";
    [Parameter] public EventCallback<string> ValueChanged { get; set; }

    /// <summary>Loader e versão do Minecraft do modpack — definem a lista.</summary>
    [Parameter]
    public ModLoader Loader { get; set; }

    [Parameter] public string MinecraftVersion { get; set; } = "";

    [Parameter] public string Label { get; set; } = "Versão do loader";
    [Parameter] public bool Required { get; set; } = true;
    [Parameter] public string? Class { get; set; }

    [Inject] private IVersionCatalog Catalog { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        await ReloadAsync();

        // Campo vazio numa versão nova: já vem com a build mais recente que
        // serve a este Minecraft (a lista chega da mais nova para a mais
        // antiga, e com "só releases" ligado é a estável mais recente). Quem
        // edita uma versão que já tem build não é tocado.
        if (string.IsNullOrWhiteSpace(Value) && _versions.Count > 0)
            await ValueChanged.InvokeAsync(_versions[0]);
    }

    private async Task ReloadAsync()
    {
        // Sem versão do Minecraft não há lista possível; o campo continua
        // aceitando texto digitado (CoerceValue), que é a saída para um loader
        // que o catálogo ainda não conhece.
        if (string.IsNullOrWhiteSpace(MinecraftVersion))
        {
            _versions = [];
            _failed = false;
            return;
        }

        _isLoading = true;
        try
        {
            var lookup = await Catalog.GetLoaderVersionsAsync(
                Loader, MinecraftVersion, _releasesOnly, CancellationToken.None);

            _versions = lookup.Versions;
            _failed = lookup.Failed;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private Task OnChanged(string value) => ValueChanged.InvokeAsync(value);

    private Task<IEnumerable<string>> Search(string value, CancellationToken ct) =>
        Task.FromResult(string.IsNullOrWhiteSpace(value)
            ? _versions.AsEnumerable()
            : _versions.Where(v => v.Contains(value, StringComparison.OrdinalIgnoreCase)));
}
