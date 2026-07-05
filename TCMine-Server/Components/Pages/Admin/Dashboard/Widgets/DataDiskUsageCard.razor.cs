using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine_Server.Infrastructure.FileSystem;

namespace TCMine_Server.Components.Pages.Admin.Dashboard.Widgets;

/// <summary>
///     Quebra do uso de disco da pasta de dados (<c>tcmine-data</c>) por área: cache de mods,
///     modpacks, instâncias, cache de servidor, configs de jogador e feed do launcher. Ajuda no
///     planejamento de capacidade (o cache de mods e o de servidor crescem com o tempo). A varredura
///     é feita <b>uma vez</b> no init, fora do thread do circuito (Task.Run), porque somar tamanhos
///     recursivamente pode ser caro com muitos arquivos.
/// </summary>
public partial class DataDiskUsageCard : ComponentBase
{
    // null = ainda a medir (mostra skeleton); preenchido = pronto, ordenado do maior para o menor
    private List<DirUsage>? _dirs;
    private long _total;

    [Inject] private IWebHostEnvironment Env { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        var root = Env.ContentRootPath;

        // Varredura off-thread: não bloqueia o render do dashboard
        _dirs = await Task.Run(() =>
        {
            var items = new List<DirUsage>
            {
                new("Cache de mods", Icons.Material.Filled.Extension, DirSize(ServerPaths.Mods(root))),
                new("Modpacks", Icons.Material.Filled.Inventory2, DirSize(ServerPaths.Modpacks(root))),
                new("Instâncias de servidor", Icons.Material.Filled.Dns, DirSize(ServerPaths.Servers(root))),
                new("Cache de servidor", Icons.Material.Filled.Cached, DirSize(ServerPaths.ServerCache(root))),
                new("Configs de jogador", Icons.Material.Filled.ManageAccounts,
                    DirSize(ServerPaths.PlayerConfigs(root))),
                new("Feed do launcher", Icons.Material.Filled.SystemUpdate, DirSize(ServerPaths.Updates(root)))
            };
            return items.OrderByDescending(d => d.Bytes).ToList();
        });

        _total = _dirs.Sum(d => d.Bytes);
    }

    // Fração (0–100) desta área sobre a MAIOR — dá a barra uma escala relativa comparável entre linhas
    private double BarPercent(DirUsage d)
    {
        var max = _dirs is null || _dirs.Count == 0 ? 0 : _dirs[0].Bytes; // já ordenado desc
        return max > 0 ? d.Bytes / (double)max * 100d : 0;
    }

    // Soma recursiva dos tamanhos dos arquivos de um diretório (0 se não existir/erro). Cada f.Length é
    // protegido porque um arquivo pode sumir/bloquear no meio da enumeração.
    private static long DirSize(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return 0;
            return new DirectoryInfo(path)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(f =>
                {
                    try
                    {
                        return f.Length;
                    }
                    catch
                    {
                        return 0;
                    }
                });
        }
        catch
        {
            return 0;
        }
    }

    // Tamanho em unidade adaptativa (GB/MB/KB); "0" para vazio
    private static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "0";
        return bytes >= 1L << 30
            ? $"{bytes / 1024d / 1024d / 1024d:0.0} GB"
            : bytes >= 1L << 20
                ? $"{bytes / 1024d / 1024d:0} MB"
                : $"{bytes / 1024d:0} KB";
    }

    // Uma área de disco medida (rótulo + ícone + bytes)
    private sealed record DirUsage(string Label, string Icon, long Bytes);
}