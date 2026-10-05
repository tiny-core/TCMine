using Microsoft.AspNetCore.Components;
using TCMine.Server.Application.Cloud;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Web.Components.Pages.Cloud;

/// <summary>Uma nuvem: servidores ligados e chaves, jogadores e canais, configurações.</summary>
public partial class CloudVaultPage : ComponentBase
{
    private CloudVault? _vault;
    private string? _error;

    /// <summary>Nome dos servidores do dono, para a aba de jogadores dizer quem segura cada lease.</summary>
    private IReadOnlyDictionary<Guid, string> _serverNames = new Dictionary<Guid, string>();

    [Parameter] public Guid VaultId { get; set; }

    [Inject] private GetCloudVault GetUseCase { get; set; } = default!;
    [Inject] private ListCloudVaultServers ServersUseCase { get; set; } = default!;

    protected override Task OnParametersSetAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        var result = await GetUseCase.HandleAsync(VaultId, CancellationToken.None);
        if (!result.Succeeded)
        {
            _error = result.Error;
            return;
        }

        _vault = result.Value;
        await ReloadServersAsync();
    }

    private async Task ReloadServersAsync()
    {
        var servers = await ServersUseCase.HandleAsync(VaultId, CancellationToken.None);
        if (servers.Succeeded)
            _serverNames = servers.Value!.ToDictionary(s => s.ServerId, s => s.Name);
    }
}
