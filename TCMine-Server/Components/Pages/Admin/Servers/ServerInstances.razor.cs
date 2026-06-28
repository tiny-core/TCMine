using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine_Application.Contracts;
using TCMine_Domain.Entities;
using TCMine_Server.Components.Pages.Admin.Servers.Dialogs;
using TCMine_Server.Services;
using TCMine_Infrastructure.ServerInstances;

namespace TCMine_Server.Components.Pages.Admin.Servers;

/// <summary>
/// Lista das instâncias de servidor com as ações de ciclo de vida (provisionar, iniciar, parar) e
/// acesso ao detalhe/console. Cria via <see cref="ServerInstanceEditDialog"/> e delega tudo ao
/// <see cref="ServerInstanceService"/>. Operações pesadas usam o <see cref="BusyService"/>; a lista é
/// recarregada após cada mudança para refletir o estado real.
/// </summary>
public partial class ServerInstances : ComponentBase
{
    [Inject] private ServerInstanceService Service { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private BusyService Busy { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    // null = carregando (BusyOverlay cobre a tela); lista vazia = estado vazio
    private List<ServerInstanceRowDto>? _rows;
    private string _search = string.Empty;

    // Jogadores online por instância (Server List Ping, feito em paralelo após o load — não bloqueia)
    private readonly Dictionary<Guid, ServerPing?> _pings = new();

    protected override async Task OnInitializedAsync()
    {
        await Busy.RunAsync("Carregando servidores…", ReloadAsync);
    }

    private async Task ReloadAsync()
    {
        _rows = await Service.ListAsync();
        // Dispara os pings em background; a UI atualiza conforme cada um responde (não segura o load)
        _ = PingRunningAsync();
    }

    // Faz o ping de cada instância em execução em paralelo, atualizando a coluna conforme respondem
    private async Task PingRunningAsync()
    {
        var running = _rows?.Where(r => r.Status == ServerInstanceStatus.Running).ToList() ?? [];
        if (running.Count == 0) return;

        await Parallel.ForEachAsync(running, async (row, ct) =>
        {
            var ping = await Service.PingAsync(row.PublicAddress, row.Port, ct);
            _pings[row.Id] = ping;
            await InvokeAsync(StateHasChanged);
        });
    }

    private bool Filter(ServerInstanceRowDto row)
    {
        if (string.IsNullOrWhiteSpace(_search)) return true;
        return row.Name.Contains(_search, StringComparison.OrdinalIgnoreCase)
               || row.ModpackName.Contains(_search, StringComparison.OrdinalIgnoreCase);
    }

    private async Task CreateAsync()
    {
        var dialog = await DialogService.ShowAsync<ServerInstanceEditDialog>("Nova instância", EditOptions());
        var result = await dialog.Result;
        if (result is null || result.Canceled || result.Data is not ServerInstanceEditDto dto) return;

        try
        {
            await Busy.RunAsync("Criando instância…", async () =>
            {
                await Service.CreateAsync(dto);
                await ReloadAsync();
            });
            Snackbar.Add($"Instância \"{dto.Name}\" criada. Provisione-a para montar o diretório.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private async Task StopAsync(ServerInstanceRowDto row)
    {
        try
        {
            await Busy.RunAsync("Parando servidor…", async () =>
            {
                await Service.StopAsync(row.Id);
                await ReloadAsync();
            });
            Snackbar.Add("Servidor parado.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha ao parar: {ex.Message}", Severity.Error);
        }
    }

    private void Open(ServerInstanceRowDto row)
    {
        Nav.NavigateTo($"/admin/servers/{row.Id}");
    }

    // Diálogo de instância em largura média (form seccionado)
    internal static DialogOptions EditOptions()
    {
        return new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true };
    }

    // ── Apresentação do status ────────────────────────────────────────────────────────────────────────

    private static string StatusLabel(ServerInstanceStatus s)
    {
        return s switch
        {
            ServerInstanceStatus.Running => "Em execução",
            ServerInstanceStatus.Starting => "Iniciando",
            ServerInstanceStatus.Stopping => "Parando",
            ServerInstanceStatus.Crashed => "Falhou",
            _ => "Parado"
        };
    }

    private static Color StatusColor(ServerInstanceStatus s)
    {
        return s switch
        {
            ServerInstanceStatus.Running => Color.Success,
            ServerInstanceStatus.Starting or ServerInstanceStatus.Stopping => Color.Info,
            ServerInstanceStatus.Crashed => Color.Error,
            _ => Color.Default
        };
    }

    private static string StatusIcon(ServerInstanceStatus s)
    {
        return s switch
        {
            ServerInstanceStatus.Running => Icons.Material.Filled.CheckCircle,
            ServerInstanceStatus.Starting or ServerInstanceStatus.Stopping => Icons.Material.Filled.Sync,
            ServerInstanceStatus.Crashed => Icons.Material.Filled.Error,
            _ => Icons.Material.Filled.StopCircle
        };
    }
}
