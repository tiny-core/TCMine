using TCMine.Contracts.Modpacks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Web.Components.Features.Servers;

namespace TCMine.Server.Web.Components.Pages.Servers;

public partial class ServersPage : ComponentBase
{
    private readonly HashSet<Guid> _busy = [];
    private bool _isLoading = true;
    private bool _onlyRunning;
    private List<ServerRow> _rows = [];
    private int _running;
    private string _search = "";

    [Inject] private IServerRepository ServerRepository { get; set; } = default!;
    [Inject] private IModpackRepository ModpackRepository { get; set; } = default!;
    [Inject] private ServerActions Actions { get; set; } = default!;
    [Inject] private IPlayerCountSource Players { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;

    /// <summary>
    ///     Filtro em memória: servidores são poucos por natureza (um homelab tem
    ///     unidades, não milhares), então paginar no banco aqui seria cerimônia.
    ///     A tabela ainda pagina para a tela não crescer sem fim.
    /// </summary>
    private IEnumerable<ServerRow> Filtered =>
        _rows.Where(r =>
            (!_onlyRunning || r.Server.Status is GameServerStatus.Running)
            && (_search.Length is 0
                || r.Server.Name.Contains(_search, StringComparison.OrdinalIgnoreCase)
                || r.ModpackName.Contains(_search, StringComparison.OrdinalIgnoreCase)));

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _isLoading = true;

        var servers = await ServerRepository.ListAllAsync(CancellationToken.None);

        // O container é a fonte da verdade do status, não a coluna.
        await Actions.SyncStatusesAsync(servers, CancellationToken.None);

        // Nomes de modpack e rótulos de versão numa passada só: sem isto seria
        // uma consulta por servidor para preencher duas colunas.
        var modpacks = await ModpackRepository.ListAsync(CancellationToken.None);
        var namesById = modpacks.ToDictionary(m => m.Id, m => m.Name);

        var rows = new List<ServerRow>();
        foreach (var group in servers.GroupBy(s => s.ModpackId))
        {
            var todas = await ModpackRepository.ListVersionsAsync(group.Key, CancellationToken.None);
            var versions = todas.ToDictionary(v => v.Id, v => v.Version);

            // A mais recente que um servidor PODE receber: publicada e de canal
            // release. Alpha fica de fora — é a mesma regra que vale ao criar o
            // servidor, e sugerir uma pré-lançamento seria oferecer o que o
            // sistema recusaria depois.
            var maisRecente = todas
                .Where(v => v.State is ModpackVersionState.Ready && !v.IsPreRelease)
                .OrderByDescending(v => v.Id) // GUID v7: mais recente primeiro
                .FirstOrDefault();

            rows.AddRange(group.Select(s => new ServerRow(
                s,
                namesById.GetValueOrDefault(s.ModpackId, "—"),
                versions.GetValueOrDefault(s.ModpackVersionId, "—"),

                // Comparar identidade, e não ordenar dois GUIDs: se a mais
                // recente não é a fixada, há para onde ir. O servidor fixa a
                // versão de propósito (é o que permite atualizar um de cada vez
                // e voltar atrás), então ficar para trás é escolha — mas nada
                // dizia que havia ficado.
                maisRecente is not null && maisRecente.Id != s.ModpackVersionId
                    ? maisRecente.Version
                    : null)));
        }

        _rows = [.. rows.OrderBy(r => r.ModpackName).ThenBy(r => r.Server.Name)];
        _running = _rows.Count(r => r.Server.Status is GameServerStatus.Running);

        _isLoading = false;
    }

    private Task Start(GameServer server) =>
        WithBusyAsync(server.Id, () => Actions.StartAsync(server.Id, CancellationToken.None));

    private Task Stop(GameServer server) =>
        WithBusyAsync(server.Id, () => Actions.StopAsync(server.Id, CancellationToken.None));

    private async Task WithBusyAsync(Guid serverId, Func<Task<bool>> action)
    {
        _busy.Add(serverId);
        try
        {
            await action();

            // Recarrega sempre: mesmo em falha o status pode ter mudado no Docker.
            await LoadAsync();
        }
        finally
        {
            _busy.Remove(serverId);
        }
    }

    private async Task CopyAddress(string address)
    {
        await JsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", address);
        Snackbar.Add("Endereço copiado.", Severity.Info);
    }

    /// <summary>Servidor mais o que a tabela precisa mostrar ao lado dele.</summary>
    private sealed record ServerRow(
        GameServer Server,
        string ModpackName,
        string VersionLabel,
        string? NewerVersion);
}
