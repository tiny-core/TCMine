using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Security;
using TCMine.Server.Application.Servers;
using TCMine.Server.Domain.Identity;
using TCMine.Server.Domain.Servers;

namespace TCMine.Server.Web.Components.Features.Servers;

public partial class ServerMembersPanel : ComponentBase
{
    private bool _canManage;
    private List<Invite> _invites = [];
    private bool _isBusy;
    private bool _loaded;
    private List<ServerMemberView> _members = [];

    [Parameter] [EditorRequired] public GameServer Server { get; set; } = default!;

    [Inject] private ListServerAccess ListUseCase { get; set; } = default!;
    [Inject] private RevokeInvite RevokeUseCase { get; set; } = default!;
    [Inject] private RemoveMember RemoveUseCase { get; set; } = default!;
    [Inject] private ChangeMemberRole ChangeRoleUseCase { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private static string Rotulo(ServerRoleDto role) => role switch
    {
        ServerRoleDto.Member => "Membro",
        ServerRoleDto.Moderator => "Moderador",
        ServerRoleDto.Admin => "Admin",
        _ => "Dono"
    };

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        var result = await ListUseCase.HandleAsync(Server.Id, CancellationToken.None);

        // A recusa do caso de uso é a fonte da verdade sobre poder gerenciar:
        // perguntar o papel aqui, à parte, criaria uma segunda regra para
        // divergir da primeira.
        _canManage = result.Succeeded;
        _loaded = true;

        if (!result.Succeeded)
            return;

        _members = [.. result.Value!.Members];
        _invites = [.. result.Value.PendingInvites];
    }

    private Task InviteAsync() => RunAsync(async () =>
    {
        var parameters = new DialogParameters<InviteDialog>
        {
            { x => x.ServerId, Server.Id },
            { x => x.ServerName, Server.Name }
        };

        var dialog = await DialogService.ShowAsync<InviteDialog>("Convidar", parameters);
        await dialog.Result;
    });

    private async Task RevokeAsync(Invite invite)
    {
        var confirm = await DialogService.ShowMessageBoxAsync(
            "Revogar convite",
            $"O código de {Rotulo(invite.Role.ToDto())} deixa de funcionar. "
            + "Quem já o usou continua membro.",
            "Revogar", cancelText: "Cancelar");

        if (confirm is not true)
            return;

        await RunAsync(async () =>
        {
            var result = await RevokeUseCase.HandleAsync(invite.Id, CancellationToken.None);

            if (result.Succeeded)
                Snackbar.Add("Convite revogado.", Severity.Success);
            else
                Snackbar.Add(result.Error!, Severity.Error);
        });
    }

    private async Task RemoveAsync(ServerMemberView member)
    {
        var confirm = await DialogService.ShowMessageBoxAsync(
            "Tirar o acesso",
            $"{member.DisplayName} deixa de enxergar este servidor no launcher. "
            + "O mundo e o que a pessoa construiu não são afetados.",
            "Remover", cancelText: "Cancelar");

        if (confirm is not true)
            return;

        await RunAsync(async () =>
        {
            var result = await RemoveUseCase.HandleAsync(
                Server.Id, member.UserId, CancellationToken.None);

            if (result.Succeeded)
                Snackbar.Add($"{member.DisplayName} não é mais membro.", Severity.Success);
            else
                Snackbar.Add(result.Error!, Severity.Error);
        });
    }

    private Task ChangeRoleAsync(ServerMemberView member, ServerRoleDto papel) => RunAsync(async () =>
    {
        if (papel == member.Role)
            return;

        var result = await ChangeRoleUseCase.HandleAsync(
            Server.Id, member.UserId, papel, CancellationToken.None);

        if (result.Succeeded)
            Snackbar.Add($"{member.DisplayName} agora é {Rotulo(papel)}.", Severity.Success);
        else
            Snackbar.Add(result.Error!, Severity.Error);
    });

    /// <summary>
    ///     Recarrega sempre ao final: papel e vínculo mudam em conjunto (remover
    ///     um membro tira uma linha, revogar um convite tira outra), e recarregar
    ///     é mais barato de acertar do que remendar as duas listas em memória.
    /// </summary>
    private async Task RunAsync(Func<Task> action)
    {
        if (_isBusy)
            return;

        _isBusy = true;

        try
        {
            await action();
            await LoadAsync();
        }
        finally
        {
            _isBusy = false;
        }
    }
}
