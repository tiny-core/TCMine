using Microsoft.AspNetCore.Components;
using TCMine.Contracts.Modpacks;
using TCMine.Launcher.Core.Abstractions;

namespace TCMine.Launcher.UI.Components;

public partial class HomeOverview : ComponentBase
{
    [Parameter] [EditorRequired] public InstalledInstance Instance { get; set; } = default!;

    /// <summary>O pack no catálogo, para a descrição. Nulo sem rede.</summary>
    [Parameter]
    public ModpackDto? Modpack { get; set; }

    /// <summary>Calculado pela tela, que já tem a instância em mãos.</summary>
    [Parameter]
    public int ModCount { get; set; }

    private string? Summary => Modpack?.Summary is { } summary && !string.IsNullOrWhiteSpace(summary) ? summary : null;

    private string LoaderText =>
        Instance.Manifest.Loader is not { } loader ? "?"
        : Instance.Manifest.LoaderVersion is { Length: > 0 } version ? $"{loader} {version}"
        : loader.ToString();

    /// <summary>
    ///     A RAM é do jogador (Instâncias → memória), e não do pack. Gravada na
    ///     instalação a partir da recomendada; nula só nas instâncias muito
    ///     antigas, que usam a recomendada na hora de abrir.
    /// </summary>
    private string MemoryText =>
        Instance.Manifest.MemoryMb is { } mb ? $"{mb / 1024.0:0.#} GB" : "a recomendada do pack";
}
