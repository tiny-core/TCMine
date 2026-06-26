using Microsoft.AspNetCore.Components;
using TCMine_Application.Contracts;
using TCMine_Domain.Entities;
using TCMine_Domain.Modpack;
using TCMine_Infrastructure.Minecraft;
using TCMine_Server.Services;

namespace TCMine_Server.Components.Pages.Admin.Modpacks;

/// <summary>
/// Aba "Detalhes" do <see cref="ModpackEditor"/>: edita os metadados do <see cref="Draft"/> (por
/// referência) e oferece os seletores de versão. As listas de versão são carregadas aqui (o painel
/// monta ao abrir a aba), então sempre refletem o loader/Minecraft atuais do rascunho.
/// </summary>
public partial class DetailsPanel : ComponentBase
{
    [Parameter] [EditorRequired] public ModpackEntity Draft { get; set; } = null!;

    [Inject] private MinecraftVersionService Versions { get; set; } = null!;
    [Inject] private BusyService Busy { get; set; } = null!;

    // Seletores de versão (canais oficiais; podem vir vazios → texto livre)
    private IReadOnlyList<VersionOptionDto> _mcVersions = [];
    private IReadOnlyList<VersionOptionDto> _loaderVersions = [];

    // Mostrar só lançamentos estáveis (oculta snapshots/beta/rc) — ligado por padrão
    private bool _mcReleasesOnly = true;
    private bool _loaderReleasesOnly = true;

    protected override async Task OnInitializedAsync()
    {
        _mcVersions = await Versions.GetMinecraftVersionsAsync();
        await ReloadLoaderVersionsAsync();
    }

    private async Task OnMinecraftChanged(string value)
    {
        Draft.Minecraft = value;
        await Busy.RunAsync("Carregando versões…", ReloadLoaderVersionsAsync);
    }

    private async Task OnLoaderChanged(ModLoader value)
    {
        Draft.Loader = value;
        await Busy.RunAsync("Carregando versões…", ReloadLoaderVersionsAsync);
    }

    private async Task ReloadLoaderVersionsAsync()
    {
        _loaderVersions = await Versions.GetLoaderVersionsAsync(Draft.Loader, Draft.Minecraft);
    }

    // SearchFunc dos MudAutocomplete: filtra as versões e devolve texto livre quando não há lista
    private Task<IEnumerable<string>> SearchMcAsync(string value, CancellationToken ct)
    {
        return Task.FromResult(Filter(_mcVersions, value, _mcReleasesOnly));
    }

    private Task<IEnumerable<string>> SearchLoaderAsync(string value, CancellationToken ct)
    {
        return Task.FromResult(Filter(_loaderVersions, value, _loaderReleasesOnly));
    }

    private static IEnumerable<string> Filter(IReadOnlyList<VersionOptionDto> opts, string? value, bool releasesOnly)
    {
        IEnumerable<VersionOptionDto> q = opts;
        if (releasesOnly)
            q = q.Where(o => o.IsRelease);
        if (!string.IsNullOrWhiteSpace(value))
            q = q.Where(o => o.Version.Contains(value, StringComparison.OrdinalIgnoreCase));
        return q.Take(100).Select(o => o.Version);
    }
}
