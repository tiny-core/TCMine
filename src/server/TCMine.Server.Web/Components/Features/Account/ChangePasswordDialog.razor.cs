using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Security;
using TCMine.Server.Web.Components.Features.Modpacks;

namespace TCMine.Server.Web.Components.Features.Account;

public partial class ChangePasswordDialog : DialogComponentBase
{
    private string _confirm = "";
    private string _current = "";
    private MudForm _form = null!;
    private string _new = "";

    [Inject] private ChangePassword ChangeUseCase { get; set; } = default!;
    [Inject] private ICurrentUserScope Scope { get; set; } = default!;

    private static string? ValidateNew(string value) =>
        value.Length < CreateFirstAdmin.MinPasswordLength
            ? $"Mínimo de {CreateFirstAdmin.MinPasswordLength} caracteres."
            : null;

    private string? ValidateConfirm(string value) =>
        value == _new ? null : "As senhas não conferem.";

    private async Task Submit()
    {
        await _form.ValidateAsync();
        if (!_form.IsValid)
            return;

        if (Scope.UserId is not { } userId)
        {
            Snackbar.Add("Sessão expirada. Entre de novo.", Severity.Error);
            return;
        }

        await SubmitAsync(
            () => ChangeUseCase.HandleAsync(userId, _current, _new, CancellationToken.None),
            "Senha alterada.");
    }
}
