using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using TCMine.Server.Application.Cloud;
using TCMine.Server.Web.Components.Features.Modpacks;

namespace TCMine.Server.Web.Components.Features.Cloud;

public partial class CloudServerKeyDialog : DialogComponentBase
{
    private string? _key;

    [Parameter] [EditorRequired] public Guid ServerId { get; set; }
    [Parameter] [EditorRequired] public string ServerName { get; set; } = "";

    [Inject] private IssueCloudServerKey UseCase { get; set; } = default!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    /// <summary>Endereço que o servidor de jogo usa para falar com este TCMine (o mesmo do painel).</summary>
    private string BaseUrl => Navigation.BaseUri.TrimEnd('/');

    /// <summary>Não fecha no sucesso: é a única chance de ver a chave (mesmo motivo do InviteDialog).</summary>
    private Task GenerateAsync() => RunAsync(async () =>
    {
        var result = await UseCase.HandleAsync(ServerId, CancellationToken.None);
        if (!result.Succeeded)
        {
            Snackbar.Add(result.Error!, Severity.Error);
            return;
        }

        _key = result.Value;
    });

    private async Task CopyAsync()
    {
        await JsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", _key);
        Snackbar.Add("Chave copiada.", Severity.Success);
    }

    private void Close() => Dialog.Close(DialogResult.Ok(true));
}
