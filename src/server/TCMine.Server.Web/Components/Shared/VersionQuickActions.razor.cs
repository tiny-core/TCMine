using Microsoft.AspNetCore.Components;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Web.Components.Features.Modpacks;

namespace TCMine.Server.Web.Components.Shared;

public partial class VersionQuickActions : ComponentBase
{
    private bool _busy;

    [Parameter] [EditorRequired] public Modpack Modpack { get; set; } = default!;

    [Parameter] public ModpackVersion? SelectedVersion { get; set; }

    /// <summary>Disparado depois de criar ou publicar, para quem hospeda recarregar.</summary>
    [Parameter] public EventCallback Changed { get; set; }

    [Inject] private VersionLifecycleActions Actions { get; set; } = default!;

    private async Task CreateVersionAsync()
    {
        if (_busy)
            return;

        _busy = true;
        try
        {
            if (await Actions.OpenCreateVersionAsync(Modpack, CancellationToken.None))
                await Changed.InvokeAsync();
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task PublishAsync()
    {
        if (SelectedVersion is null || _busy)
            return;

        _busy = true;
        try
        {
            if (await Actions.PublishWithConfirmAsync(SelectedVersion, CancellationToken.None))
                await Changed.InvokeAsync();
        }
        finally
        {
            _busy = false;
        }
    }
}
