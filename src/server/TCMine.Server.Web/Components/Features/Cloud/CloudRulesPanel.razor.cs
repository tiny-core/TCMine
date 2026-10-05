using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Cloud;
using TCMine.Server.Application.Common;
using TCMine.Server.Domain.Cloud;

namespace TCMine.Server.Web.Components.Features.Cloud;

/// <summary>Aba "Regras": regras de item da nuvem e a fila de suspeitos.</summary>
public partial class CloudRulesPanel : ComponentBase
{
    private List<CloudItemRule>? _rules;
    private List<CloudSuspectItem>? _suspects;
    private CloudRuleScope _scope = CloudRuleScope.Item;
    private CloudRuleAction _action = CloudRuleAction.Block;
    private string _pattern = "";
    private string? _note;
    private bool _isBusy;

    [Parameter] [EditorRequired] public Guid VaultId { get; set; }
    [Parameter] public EventCallback Changed { get; set; }

    [Inject] private ListCloudRules ListRules { get; set; } = default!;
    [Inject] private AddCloudRule AddRule { get; set; } = default!;
    [Inject] private RemoveCloudRule RemoveRule { get; set; } = default!;
    [Inject] private ListCloudSuspects ListSuspects { get; set; } = default!;
    [Inject] private ResolveCloudSuspect ResolveSuspect { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private string PatternHint => _scope switch
    {
        CloudRuleScope.Item => "Item (ex.: minecraft:shulker_box)",
        CloudRuleScope.Tag => "Tag (ex.: c:shulker_boxes)",
        _ => "Mod (ex.: refinedstorage)"
    };

    private static string Label(CloudRuleScope scope) => scope switch
    {
        CloudRuleScope.Item => "Item",
        CloudRuleScope.Tag => "Tag",
        _ => "Mod"
    };

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        var rules = await ListRules.HandleAsync(VaultId, CancellationToken.None);
        var suspects = await ListSuspects.HandleAsync(VaultId, pendingOnly: true, CancellationToken.None);
        _rules = rules.Succeeded ? [.. rules.Value!] : [];
        _suspects = suspects.Succeeded ? [.. suspects.Value!] : [];
    }

    private Task AddAsync() => RunAsync(async () =>
    {
        var result = await AddRule.HandleAsync(VaultId, _scope, _pattern, _action, _note, CancellationToken.None);
        if (result.Succeeded)
        {
            _pattern = "";
            _note = null;
        }
        return result;
    }, "Regra criada.");

    private Task RemoveAsync(CloudItemRule rule) =>
        RunAsync(() => RemoveRule.HandleAsync(VaultId, rule.Id, CancellationToken.None), "Regra apagada.");

    private Task ResolveAsync(CloudSuspectItem suspect, bool allow) =>
        RunAsync(() => ResolveSuspect.HandleAsync(VaultId, suspect.Id, allow, CancellationToken.None),
            allow ? $"{suspect.ItemId} permitido." : $"{suspect.ItemId} bloqueado.");

    private async Task RunAsync(Func<Task<Result>> action, string success)
    {
        if (_isBusy) return;
        _isBusy = true;
        try
        {
            var result = await action();
            Snackbar.Add(result.Succeeded ? success : result.Error!, result.Succeeded ? Severity.Success : Severity.Error);
            await LoadAsync();
            await Changed.InvokeAsync();
        }
        finally
        {
            _isBusy = false;
        }
    }
}
