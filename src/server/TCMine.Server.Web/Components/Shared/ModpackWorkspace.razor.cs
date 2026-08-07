using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Web.Components.Shared;

public partial class ModpackWorkspace : ComponentBase
{
    // As quatro etapas visíveis do ciclo de vida. Failed é tratado como falha na
    // etapa "Resolvendo", não como uma coluna própria.
    private static readonly (int Index, string Label)[] Steps =
    [
        (0, "Rascunho"),
        (1, "Resolvendo"),
        (2, "Publicado"),
        (3, "Arquivado")
    ];

    [Parameter] [EditorRequired] public Modpack Modpack { get; set; } = default!;

    [Parameter] public ModpackTab Active { get; set; }

    [Parameter] public Guid SelectedVersionId { get; set; }

    [Parameter] public ModpackVersion? SelectedVersion { get; set; }

    /// <summary>Seletor de versão só faz sentido em abas por versão (não em Servidores).</summary>
    [Parameter]
    public bool ShowVersionSelector { get; set; } = true;

    /// <summary>Disparado quando o admin troca a versão no seletor.</summary>
    [Parameter]
    public EventCallback<Guid> SelectedVersionIdChanged { get; set; }

    /// <summary>Ações à direita do cabeçalho (ex.: "Nova versão").</summary>
    [Parameter]
    public RenderFragment? HeaderActions { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }

    private List<BreadcrumbItem> Breadcrumbs =>
    [
        new("Modpacks", "/modpacks"),
        new(Modpack.Name, $"/modpacks/{Modpack.Id}", Active == ModpackTab.Overview)
    ];

    private Task OnVersionChanged(Guid versionId) => SelectedVersionIdChanged.InvokeAsync(versionId);

    private string Cls(ModpackTab tab) => Active == tab ? "active" : "";

    // Índice da etapa "atual" para cada estado. Failed pinta a etapa 1 como falha.
    private string StepClass(int index)
    {
        var state = SelectedVersion!.State;

        if (state is ModpackVersionState.Failed)
            return index switch { 0 => "done", 1 => "failed", _ => "" };

        var current = state switch
        {
            ModpackVersionState.Draft => 0,
            ModpackVersionState.Resolving => 1,
            ModpackVersionState.Ready => 2,
            ModpackVersionState.Archived => 3,
            _ => 0
        };

        if (index < current) return "done";
        return index == current ? "current" : "";
    }

    private string StepGlyph(int index)
    {
        var cls = StepClass(index);
        return cls switch
        {
            "done" => "✓",
            "failed" => "!",
            "current" => "●",
            _ => (index + 1).ToString()
        };
    }
}
