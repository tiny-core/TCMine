using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace TCMine.Server.Web.Components;

public partial class App : ComponentBase
{
    [CascadingParameter] private HttpContext HttpContext { get; set; } = default!;

    /// <summary>
    ///     InteractiveServer para o app inteiro, exceto nas páginas marcadas com
    ///     [ExcludeFromInteractiveRouting] — nelas devolve null (SSR estático).
    /// </summary>
    private IComponentRenderMode? RenderModeForPage =>
        HttpContext.GetEndpoint()?.Metadata.GetMetadata<ExcludeFromInteractiveRoutingAttribute>() is null
            ? RenderMode.InteractiveServer
            : null;
}
