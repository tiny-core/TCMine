using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine_Application.Contracts;
using TCMine_Server.Infrastructure.PlayerConfigs;
using TCMine_Server.Services;

namespace TCMine_Server.Components.Pages.Admin.Players;

/// <summary>
///     Gestão das configs player-owned em disco (ver <see cref="PlayerConfigAdminService" />). Lista os
///     conjuntos <c>(uuid, modpackId)</c> agrupados por jogador, com tamanho/último sync, e permite apagar
///     um conjunto ou tudo de um jogador para recuperar disco. UI fina: só conhece o serviço e os DTOs.
/// </summary>
public partial class PlayerConfigs : ComponentBase
{
    private string _search = string.Empty;

    // null = carregando (BusyOverlay cobre a tela); Sets vazio = estado vazio
    private PlayerConfigOverviewDto? _overview;

    [Inject] private PlayerConfigAdminService Service { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private BusyService Busy { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        await Busy.RunAsync("Carregando configs dos jogadores…", ReloadAsync);
    }

    private async Task ReloadAsync()
    {
        _overview = await Service.ListAsync();
    }

    private bool Filter(PlayerConfigSetDto s)
    {
        if (string.IsNullOrWhiteSpace(_search)) return true;
        return s.Uuid.Contains(_search, StringComparison.OrdinalIgnoreCase)
               || (s.ModpackName?.Contains(_search, StringComparison.OrdinalIgnoreCase) ?? false)
               || s.ModpackId.Contains(_search, StringComparison.OrdinalIgnoreCase);
    }

    // ── Remoção ─────────────────────────────────────────────────────────────────────────────────────────

    private async Task DeleteSetAsync(PlayerConfigSetDto s)
    {
        var label = s.ModpackName ?? s.ModpackId;
        var ok = await DialogService.ShowMessageBoxAsync(
            "Apagar configs do modpack",
            $"Apagar as configs de \"{label}\" deste jogador ({FormatSize(s.SizeBytes)})? " +
            "O jogador re-sincroniza as locais no próximo launch. Esta ação não pode ser desfeita.",
            "Apagar", cancelText: "Cancelar");
        if (ok != true) return;

        try
        {
            await Busy.RunAsync("Apagando configs…", async () =>
            {
                await Service.DeleteSetAsync(s.Uuid, s.ModpackId);
                await ReloadAsync();
            });
            Snackbar.Add("Configs apagadas.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha ao apagar: {ex.Message}", Severity.Error);
        }
    }

    private async Task DeletePlayerAsync(string uuid, int setCount)
    {
        var ok = await DialogService.ShowMessageBoxAsync(
            "Apagar todas as configs do jogador",
            $"Apagar TODAS as configs deste jogador ({setCount} modpack(s))? " +
            "Ele re-sincroniza as locais no próximo launch. Esta ação não pode ser desfeita.",
            "Apagar tudo", cancelText: "Cancelar");
        if (ok != true) return;

        try
        {
            await Busy.RunAsync("Apagando configs do jogador…", async () =>
            {
                await Service.DeletePlayerAsync(uuid);
                await ReloadAsync();
            });
            Snackbar.Add("Configs do jogador apagadas.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Falha ao apagar: {ex.Message}", Severity.Error);
        }
    }

    // ── Apresentação ──────────────────────────────────────────────────────────────────────────────────────

    // Tamanho em unidade adaptativa (GB/MB/KB) — configs vão de poucos kB (só keybinds) a centenas de MB
    // (cache de mapa)
    private static string FormatSize(long bytes)
    {
        return bytes switch
        {
            >= 1L << 30 => $"{bytes / 1024d / 1024d / 1024d:0.0} GB",
            >= 1L << 20 => $"{bytes / 1024d / 1024d:0.0} MB",
            _ => $"{bytes / 1024d:0} KB"
        };
    }

    private static string Updated(DateTimeOffset? dt)
    {
        return dt?.LocalDateTime.ToString("g") ?? "—";
    }
}
