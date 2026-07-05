using Microsoft.AspNetCore.Components;
using TCMine_Server.Components.Shared;
using TCMine_Server.Infrastructure.ServerInstances;

namespace TCMine_Server.Components.Pages.Admin.Servers;

/// <summary>
///     Página de configurações de uma instância (Fase de UX): árvore de arquivos + Monaco via o componente
///     compartilhado <see cref="FileTreeEditor" />, com um <see cref="ServerConfigTreeSource" /> amarrado à instância.
/// </summary>
public partial class ServerConfigPage : ComponentBase
{
    private ServerConfigTreeSource? _source;
    [Parameter] public Guid Id { get; set; }

    [Inject] private ServerInstanceService Service { get; set; } = null!;

    protected override void OnParametersSet()
    {
        _source = new ServerConfigTreeSource(Service, Id);
    }
}