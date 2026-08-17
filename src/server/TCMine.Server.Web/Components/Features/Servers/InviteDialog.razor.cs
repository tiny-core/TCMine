using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using TCMine.Contracts.Servers;
using TCMine.Server.Application.Servers;
using TCMine.Server.Web.Components.Features.Modpacks;

namespace TCMine.Server.Web.Components.Features.Servers;

public partial class InviteDialog : DialogComponentBase
{
    private string? _code;
    private ServerRoleDto _createdRole;
    private ServerRoleDto _role = ServerRoleDto.Member;

    [Parameter] [EditorRequired] public Guid ServerId { get; set; }
    [Parameter] [EditorRequired] public string ServerName { get; set; } = "";

    [Inject] private CreateInvite CreateUseCase { get; set; } = default!;
    [Inject] private IJSRuntime JsRuntime { get; set; } = default!;

    private static int Dias => (int)CreateInvite.DefaultLifetime.TotalDays;

    private static string Rotulo(ServerRoleDto role) => role switch
    {
        ServerRoleDto.Member => "Membro",
        ServerRoleDto.Moderator => "Moderador",
        ServerRoleDto.Admin => "Admin",
        _ => "Dono"
    };

    /// <summary>
    ///     Não usa o SubmitAsync da base de propósito: o padrão fecha o diálogo
    ///     no sucesso, e aqui o sucesso é justamente quando ele precisa continuar
    ///     aberto — é a única oportunidade de o admin ver o código.
    /// </summary>
    private Task CreateAsync() => RunAsync(async () =>
    {
        var result = await CreateUseCase.HandleAsync(ServerId, _role, CancellationToken.None);

        if (!result.Succeeded)
        {
            Snackbar.Add(result.Error!, Severity.Error);
            return;
        }

        _createdRole = _role;
        _code = result.Value;
    });

    private async Task CopyAsync()
    {
        await JsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", _code);
        Snackbar.Add("Código copiado.", Severity.Success);
    }

    // Fecha devolvendo true: a lista de convites da tela de trás mudou.
    private void Close() => Dialog.Close(DialogResult.Ok(true));
}
