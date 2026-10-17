using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Cloud;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Web.Components.Features.Cloud;

/// <summary>
///     Aba "Auditoria": quem decidiu o quê no painel e os movimentos de itens (o
///     ledger). As duas listas carregam uma página por vez, com "Carregar mais";
///     os filtros dos movimentos vão para o banco.
/// </summary>
public partial class CloudAuditPanel : ComponentBase
{
    private static readonly CloudLedgerSource[] Sources = Enum.GetValues<CloudLedgerSource>();

    private readonly List<CloudAdminAuditEntry> _actions = [];
    private readonly List<CloudLedgerView> _ledger = [];
    private bool _actionsLoaded;
    private Guid? _actionsNext;
    private bool _actionsBusy;

    /// <summary>Último jogador que a página pediu, para não reaplicar o mesmo a cada render do pai.</summary>
    private string? _appliedInitialPlayer;

    private string? _item;
    private bool _ledgerLoaded;
    private Guid? _ledgerNext;
    private bool _ledgerBusy;
    private DateRange? _period;
    private string? _player;
    private CloudLedgerSource? _source;

    /// <summary>Filtro que a página do cofre manda ao abrir esta aba pelo "Ver histórico" de um jogador.</summary>
    [Parameter] public string? InitialPlayer { get; set; }

    [Parameter] [EditorRequired] public Guid VaultId { get; set; }

    [Inject] private ListCloudAdminActions ActionsUseCase { get; set; } = default!;
    [Inject] private ListCloudLedger LedgerUseCase { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private bool HasFilter =>
        !string.IsNullOrWhiteSpace(_player) || !string.IsNullOrWhiteSpace(_item) || _source is not null ||
        _period?.Start is not null || _period?.End is not null;

    protected override Task OnInitializedAsync() => LoadMoreActionsAsync();

    protected override Task OnParametersSetAsync()
    {
        // Primeira carga, ou "Ver histórico" de outro jogador com a aba já aberta.
        if (_ledgerLoaded && InitialPlayer == _appliedInitialPlayer)
            return Task.CompletedTask;

        _appliedInitialPlayer = InitialPlayer;
        if (!string.IsNullOrWhiteSpace(InitialPlayer))
        {
            _player = InitialPlayer;
            _item = null;
            _source = null;
            _period = null;
        }

        return ReloadLedgerAsync();
    }

    private async Task LoadMoreActionsAsync()
    {
        _actionsBusy = true;
        try
        {
            var result = await ActionsUseCase.HandleAsync(VaultId, _actionsNext, CancellationToken.None);
            if (!result.Succeeded)
            {
                Snackbar.Add(result.Error!, Severity.Error);
                return;
            }

            _actions.AddRange(result.Value!.Items);
            _actionsNext = result.Value.Next;
            _actionsLoaded = true;
        }
        finally
        {
            _actionsBusy = false;
        }
    }

    // Filtro novo começa do topo: continuar do cursor antigo misturaria linhas
    // de dois filtros na mesma tabela.
    private Task ReloadLedgerAsync()
    {
        _ledger.Clear();
        _ledgerNext = null;
        return LoadLedgerPageAsync();
    }

    private Task ClearFiltersAsync()
    {
        _player = null;
        _item = null;
        _source = null;
        _period = null;
        return ReloadLedgerAsync();
    }

    private async Task LoadLedgerPageAsync()
    {
        _ledgerBusy = true;
        try
        {
            var result = await LedgerUseCase.HandleAsync(VaultId, BuildFilter(), _ledgerNext,
                CancellationToken.None);
            if (!result.Succeeded)
            {
                Snackbar.Add(result.Error!, Severity.Error);
                return;
            }

            _ledger.AddRange(result.Value!.Items);
            _ledgerNext = result.Value.Next;
            _ledgerLoaded = true;
        }
        finally
        {
            _ledgerBusy = false;
        }
    }

    /// <summary>
    ///     O seletor devolve dias inteiros no fuso do servidor (o mesmo das datas
    ///     da tabela): o período vai do início do primeiro dia ao início do dia
    ///     seguinte ao último, então o último dia entra inteiro.
    /// </summary>
    private CloudLedgerFilter BuildFilter() => new(
        _player,
        _item,
        _source,
        _period?.Start is { } start ? new DateTimeOffset(start.Date) : null,
        _period?.End is { } end ? new DateTimeOffset(end.Date.AddDays(1)) : null);

    private Task OnFilterKeyDown(KeyboardEventArgs e) =>
        e.Key == "Enter" ? ReloadLedgerAsync() : Task.CompletedTask;

    private static string SourceLabel(CloudLedgerSource source) => source switch
    {
        CloudLedgerSource.Game => "Jogo",
        CloudLedgerSource.Admin => "Painel",
        CloudLedgerSource.Revert => "Estorno",
        _ => "Quarentena"
    };
}
