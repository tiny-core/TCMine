using Microsoft.AspNetCore.Components;
using TCMine_Server.Infrastructure.ServerInstances;
using TCMine_Server.Components.Shared;

namespace TCMine_Server.Components.Pages.Admin.Servers;

/// <summary>
/// Página de configurações de uma instância (Fase de UX): árvore de arquivos + Monaco via o componente
/// compartilhado <see cref="FileTreeEditor"/>, com um <see cref="ServerConfigTreeSource"/> amarrado à instância.
/// </summary>
public partial class ServerConfigPage : ComponentBase
{
    [Parameter] public Guid Id { get; set; }

    [Inject] private ServerInstanceService Service { get; set; } = null!;

    private ServerConfigTreeSource? _source;

    protected override void OnParametersSet()
    {
        _source = new ServerConfigTreeSource(Service, Id);
    }
}
