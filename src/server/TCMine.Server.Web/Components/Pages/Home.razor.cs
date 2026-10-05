using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Servers;
using TCMine.Server.Domain.Common;
using TCMine.Server.Domain.Modpacks;
using TCMine.Server.Domain.Servers;
using TCMine.Server.Web.Background;

namespace TCMine.Server.Web.Components.Pages;

/// <summary>
///     Visão geral: o que está no ar e o que espera pelo admin.
///     Só números que existem — jogadores vêm da contagem por RCON, CPU e RAM da
///     amostragem do Docker, disco da do host. Onde não há medida mostra-se um
///     traço: um zero inventado diria que o servidor está vazio, e não se sabe.
/// </summary>
public partial class Home : ComponentBase
{
    private bool _isLoading = true;
    private IReadOnlyList<GameServer> _servers = [];
    private List<PackRow> _packs = [];
    private Dictionary<Guid, string> _versionNumbers = [];
    private List<AttentionItem> _attention = [];
    private IReadOnlyList<ActivityEvent> _recentActivity = [];
    private DiskUsage? _disk;

    [Inject] private IModpackRepository ModpackRepository { get; set; } = default!;
    [Inject] private IServerRepository ServerRepository { get; set; } = default!;
    [Inject] private IPlayerCountSource Players { get; set; } = default!;
    [Inject] private MetricsHistory Metrics { get; set; } = default!;
    [Inject] private ListAccessRequests AccessRequests { get; set; } = default!;
    [Inject] private IActivityLogRepository Activity { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    /// <summary>Volta do /auth/microsoft/callback depois de vincular o Minecraft.</summary>
    [SupplyParameterFromQuery(Name = "linked")]
    private bool? Linked { get; set; }

    /// <summary>Mesma volta, quando vincular (ou o login) deu errado.</summary>
    [SupplyParameterFromQuery(Name = "error")]
    private string? Error { get; set; }

    private List<GameServer> RunningServers =>
        [.. _servers.Where(s => s.Status is GameServerStatus.Running)];

    private List<GameServer> CrashedServers =>
        [.. _servers.Where(s => s.Status is GameServerStatus.Crashed)];

    /// <summary>Soma do que se sabe; nulo quando nenhum servidor no ar foi amostrado.</summary>
    private int? PlayersOnline
    {
        get
        {
            var counts = RunningServers.Select(s => Players.TryGet(s.Id)).Where(c => c is not null).ToList();
            return counts.Count is 0 ? null : counts.Sum();
        }
    }

    /// <summary>
    ///     Soma dos picos de hoje, por TODOS os servidores (não só os no ar — um
    ///     servidor que já parou hoje continua contando para o pico do dia).
    ///     Nulo quando nenhum teve amostra hoje.
    /// </summary>
    private int? PeakToday
    {
        get
        {
            var peaks = _servers.Select(s => Players.PeakToday(s.Id)).Where(p => p is not null).ToList();
            return peaks.Count is 0 ? null : peaks.Sum();
        }
    }

    protected override async Task OnInitializedAsync()
    {
        _servers = await ServerRepository.ListAllAsync(CancellationToken.None);

        // Os resumos de versão (sem arquivos) bastam para o estado e o número —
        // carregar as versões inteiras arrastaria milhares de linhas de um pack
        // importado só para escrever "v2.3.0".
        foreach (var modpack in await ModpackRepository.ListAsync(CancellationToken.None))
        {
            var versions = await ModpackRepository.ListVersionSummariesAsync(modpack.Id, CancellationToken.None);

            foreach (var v in versions)
                _versionNumbers[v.Id] = v.Version;

            _packs.Add(new PackRow(
                modpack,
                versions.Where(v => v.State is ModpackVersionState.Ready).MaxBy(v => v.Id),
                versions.FirstOrDefault(v => v.State is ModpackVersionState.Draft
                    or ModpackVersionState.Resolving or ModpackVersionState.Failed)));
        }

        if (Metrics.Host() is [.., var last] && last.DiskTotalBytes > 0)
            _disk = new DiskUsage(last.DiskTotalBytes - last.DiskFreeBytes, last.DiskTotalBytes);

        var pending = await AccessRequests.HandleAsync(CancellationToken.None);
        _attention = BuildAttention(pending.Count, CrashedServers, _packs, _disk);

        _recentActivity = await Activity.ListRecentAsync(8, CancellationToken.None);

        _isLoading = false;

        if (Linked == true)
            Snackbar.Add("Conta Minecraft vinculada.", Severity.Success);
        else if (!string.IsNullOrWhiteSpace(Error))
            Snackbar.Add(Error, Severity.Error);
    }

    /// <summary>
    ///     O que pede uma decisão do admin, do mais urgente para o resto. Pura,
    ///     para a ordem e o texto não dependerem de quem chama.
    /// </summary>
    internal static List<AttentionItem> BuildAttention(
        int pendingRequests, IReadOnlyList<GameServer> crashed, IReadOnlyList<PackRow> packs, DiskUsage? disk)
    {
        var items = new List<AttentionItem>();

        foreach (var server in crashed)
        {
            items.Add(new AttentionItem(
                Icons.Material.Filled.ErrorOutline, $"{server.Name} caiu",
                "Veja o console antes de reiniciar: o motivo costuma estar nas últimas linhas.",
                ServerHref(server), "Abrir", Danger: true));
        }

        if (disk is { UsedPercent: >= 85 })
        {
            items.Add(new AttentionItem(
                Icons.Material.Filled.Storage, $"Disco {disk.UsedPercent:0}% cheio",
                "Servidor de jogo sem disco corrompe o mundo aberto. Libere espaço em Armazenamento.",
                "/admin/storage", "Liberar", Danger: true));
        }

        if (pendingRequests > 0)
        {
            items.Add(new AttentionItem(
                Icons.Material.Filled.PersonAdd,
                pendingRequests == 1 ? "1 pedido de acesso" : $"{pendingRequests} pedidos de acesso",
                "Jogadores esperando para entrar num servidor.",
                "/admin/access-requests", "Revisar"));
        }

        foreach (var pack in packs.Where(p => p.Draft is { State: ModpackVersionState.Failed }))
        {
            items.Add(new AttentionItem(
                Icons.Material.Filled.ExtensionOff, $"{pack.Modpack.Name} v{pack.Draft!.Version} falhou",
                "A resolução dos mods parou. Abra a versão para ver o que faltou.",
                $"/admin/modpacks/{pack.Modpack.Id}", "Resolver"));
        }

        return items;
    }

    private MetricPoint? LastMetric(Guid serverId) =>
        Metrics.Server(serverId) is [.., var last] ? last : null;

    private static double MemoryPercent(MetricPoint m) =>
        m.MemoryLimitBytes is 0 ? 0 : Math.Min(100, m.MemoryUsedBytes * 100d / m.MemoryLimitBytes);

    private string PackName(Guid modpackId) =>
        _packs.FirstOrDefault(p => p.Modpack.Id == modpackId)?.Modpack.Name ?? "?";

    private string VersionNumber(Guid versionId) => _versionNumbers.GetValueOrDefault(versionId, "?");

    /// <summary>A tela onde o console e as ações de um servidor moram.</summary>
    private static string ServerHref(GameServer server) => $"/admin/modpacks/{server.ModpackId}/servers";

    private static string CrystalClass(GameServerStatus status) => status switch
    {
        GameServerStatus.Running => "tc-crystal-on",
        GameServerStatus.Crashed => "tc-crystal-bad",
        GameServerStatus.Starting or GameServerStatus.Stopping or GameServerStatus.Updating => "tc-crystal-wait",
        _ => "tc-crystal-off"
    };

    private static string StatusHint(GameServerStatus status) => status switch
    {
        GameServerStatus.Crashed => "Caiu. O console mostra o motivo.",
        GameServerStatus.Starting => "Iniciando…",
        GameServerStatus.Stopping => "Parando…",
        GameServerStatus.Updating => "Atualizando…",
        _ => "Parado."
    };

    private static string ActivityIcon(ActivityEventKind kind) => kind switch
    {
        ActivityEventKind.UserLoggedIn => Icons.Material.Filled.Login,
        ActivityEventKind.VersionPublished => Icons.Material.Filled.Publish,
        ActivityEventKind.ServerCrashed => Icons.Material.Filled.ErrorOutline,
        ActivityEventKind.WorldBackupCreated => Icons.Material.Filled.Backup,
        _ => Icons.Material.Filled.Circle
    };

    private static Color ActivityColor(ActivityEventKind kind) => kind switch
    {
        ActivityEventKind.VersionPublished => Color.Success,
        ActivityEventKind.ServerCrashed => Color.Error,
        ActivityEventKind.WorldBackupCreated => Color.Info,
        _ => Color.Default
    };

    private static string StateLabel(ModpackVersionState state) => state switch
    {
        ModpackVersionState.Draft => "rascunho",
        ModpackVersionState.Resolving => "resolvendo",
        ModpackVersionState.Failed => "falhou",
        _ => ""
    };

    internal sealed record PackRow(Modpack Modpack, ModpackVersion? Published, ModpackVersion? Draft);

    internal sealed record AttentionItem(
        string Icon, string Title, string Detail, string Href, string Action, bool Danger = false);

    internal sealed record DiskUsage(long UsedBytes, long TotalBytes)
    {
        public double UsedPercent => TotalBytes is 0 ? 0 : UsedBytes * 100d / TotalBytes;
    }
}
