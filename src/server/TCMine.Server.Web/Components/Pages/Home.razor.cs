using Microsoft.AspNetCore.Components;
using TCMine.Contracts;
using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Web.Mapping;

namespace TCMine.Server.Web.Components.Pages;

/// <summary>
///     Painel inicial: visão geral com contagens (modpacks, servidores, quantos
///     rodando) e um atalho para os modpacks recentes.
/// </summary>
public partial class Home : ComponentBase
{
    private bool _isLoading = true;
    private IReadOnlyList<ModpackDto> _modpacks = [];
    private IReadOnlyList<GameServer> _servers = [];

    [Inject] private IModpackRepository ModpackRepository { get; set; } = default!;
    [Inject] private IServerRepository ServerRepository { get; set; } = default!;

    private static int ProtocolVersion => Protocol.Current;

    private int RunningServers =>
        _servers.Count(s => s.Status is GameServerStatus.Running or GameServerStatus.Starting);

    protected override async Task OnInitializedAsync()
    {
        var entities = await ModpackRepository.ListAsync(CancellationToken.None);
        _modpacks = [.. entities.Select(m => m.ToDto())];
        _servers = await ServerRepository.ListAllAsync(CancellationToken.None);
        _isLoading = false;
    }
}
