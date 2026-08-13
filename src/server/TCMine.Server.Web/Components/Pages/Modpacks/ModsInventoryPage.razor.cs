using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Modpacks;

namespace TCMine.Server.Web.Components.Pages.Modpacks;

public partial class ModsInventoryPage : ComponentBase
{
    private bool _onlyOrphans;
    private ModFileOrigin? _origin;
    private string _search = "";

    /// <summary>Referência à tabela para forçar recarga quando um filtro muda.</summary>
    private MudTable<ModInventoryEntry> _table = default!;

    /// <summary>Total da consulta corrente — o que a tabela usa para saber quantas páginas há.</summary>
    private int _total;

    [Inject] private IModpackRepository Repository { get; set; } = default!;

    /// <summary>
    ///     Carrega SÓ a página pedida, com filtros aplicados em SQL.
    ///     Antes isto trazia o inventário inteiro e filtrava em memória: funciona
    ///     com dez mods e afunda com um pack importado, que sozinho traz
    ///     centenas.
    /// </summary>
    private async Task<TableData<ModInventoryEntry>> LoadAsync(TableState state, CancellationToken ct)
    {
        var query = new ModInventoryQuery(
            new PageRequest(state.Page, state.PageSize),
            string.IsNullOrWhiteSpace(_search) ? null : _search.Trim(),
            _origin,
            _onlyOrphans);

        var result = await Repository.ListModInventoryAsync(query, ct);
        _total = result.TotalCount;

        return new TableData<ModInventoryEntry> { Items = result.Items, TotalItems = result.TotalCount };
    }

    // Qualquer filtro volta para a primeira página: manter a página 7 depois de
    // filtrar mostraria uma tabela vazia sem explicação.
    private Task OnFilterChanged() => _table.ReloadServerData();

    private Task OnSearchChanged(string value)
    {
        _search = value;
        return _table.ReloadServerData();
    }

    private Task OnOriginChanged(ModFileOrigin? value)
    {
        _origin = value;
        return _table.ReloadServerData();
    }

    private Task OnOrphanToggled(bool value)
    {
        _onlyOrphans = value;
        return _table.ReloadServerData();
    }

    private static Color OriginColor(ModFileOrigin origin) => origin switch
    {
        ModFileOrigin.Modrinth => Color.Success,
        ModFileOrigin.CurseForge => Color.Warning,
        _ => Color.Default
    };
}
