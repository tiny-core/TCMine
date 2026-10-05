using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Cloud;
using TCMine.Server.Application.Common;

namespace TCMine.Server.Web.Components.Features.Cloud;

/// <summary>
///     Aba "Servidores": liga e desliga servidores do dono e cuida da chave de
///     cada um. Toda troca de nuvem revoga a chave (ver SetServerCloudVault),
///     então a tela avisa antes.
/// </summary>
public partial class CloudServersPanel : ComponentBase
{
    private List<CloudVaultServerView>? _servers;
    private bool _isBusy;

    [Parameter] [EditorRequired] public Guid VaultId { get; set; }

    /// <summary>Avisa a página (os nomes dos servidores mudam o que a aba de jogadores mostra).</summary>
    [Parameter] public EventCallback ServersChanged { get; set; }

    [Inject] private ListCloudVaultServers ListUseCase { get; set; } = default!;
    [Inject] private SetServerCloudVault SetVaultUseCase { get; set; } = default!;
    [Inject] private RevokeCloudServerKey RevokeUseCase { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    protected override Task OnParametersSetAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        var result = await ListUseCase.HandleAsync(VaultId, CancellationToken.None);
        if (result.Succeeded)
            _servers = [.. result.Value!];
        else
            Snackbar.Add(result.Error!, Severity.Error);
    }

    private async Task AttachAsync(CloudVaultServerView server)
    {
        if (server.AttachedElsewhere && !await ConfirmAsync("Trocar de nuvem",
                $"{server.Name} está em outra nuvem. Os jogadores dele passam a usar os canais desta, e a chave atual deixa de valer."))
            return;
        await RunAsync(() => SetVaultUseCase.HandleAsync(server.ServerId, VaultId, CancellationToken.None),
            $"{server.Name} ligado à nuvem. Reinicie o servidor para ele começar a usar.");
    }

    private async Task DetachAsync(CloudVaultServerView server)
    {
        if (!await ConfirmAsync("Desligar da nuvem",
                $"{server.Name} perde o acesso à nuvem na próxima requisição e a chave é revogada. Os itens continuam na nuvem."))
            return;
        await RunAsync(() => SetVaultUseCase.HandleAsync(server.ServerId, null, CancellationToken.None),
            $"{server.Name} desligado da nuvem.");
    }

    private async Task RevokeAsync(CloudVaultServerView server)
    {
        if (!await ConfirmAsync("Revogar chave",
                $"{server.Name} perde o acesso à nuvem agora. Uma chave nova é gerada no próximo início do servidor."))
            return;
        await RunAsync(() => RevokeUseCase.HandleAsync(server.ServerId, CancellationToken.None), "Chave revogada.");
    }

    private async Task<bool> ConfirmAsync(string title, string message) =>
        await DialogService.ShowMessageBoxAsync(title, message, yesText: "Confirmar", cancelText: "Cancelar") == true;

    private async Task RunAsync(Func<Task<Result>> action, string success)
    {
        if (_isBusy)
            return;
        _isBusy = true;
        try
        {
            var result = await action();
            Snackbar.Add(result.Succeeded ? success : result.Error!, result.Succeeded ? Severity.Success : Severity.Error);
            await LoadAsync();
            await ServersChanged.InvokeAsync();
        }
        finally
        {
            _isBusy = false;
        }
    }
}
