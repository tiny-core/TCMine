using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine_Domain.Entities;
using TCMine_Domain.Identity;

namespace TCMine_Server.Components.Pages.Admin.Users.Dialogs;

/// <summary>
///     Diálogo de criar/editar um usuário do painel. Só coleta e valida o formulário e devolve um
///     <see cref="UserFormResult" />; a persistência (hash, unicidade do login, proteção do último Owner)
///     fica com o <c>UserService</c>, chamado pela página. Na criação a senha é obrigatória; na edição
///     é opcional (preencher = redefinir).
/// </summary>
public partial class UserEditDialog : ComponentBase
{
    private string _confirm = string.Empty;

    private MudForm _form = null!;
    private bool _isActive = true;
    private string _password = string.Empty;
    private UserRole _role = UserRole.Viewer;
    private string _username = string.Empty;
    [CascadingParameter] private IMudDialogInstance MudDialog { get; set; } = null!;

    // null = criar; preenchido = editar (não mutamos o original — devolvemos um resultado)
    [Parameter] public UserEntity? User { get; set; }

    private bool IsEdit => User is not null;

    protected override void OnInitialized()
    {
        if (User is not null)
        {
            _username = User.Username;
            _role = User.Role;
            _isActive = User.IsActive;
        }
    }

    // Senha: obrigatória só na criação; quando informada, mínimo de 8 caracteres
    private string? ValidatePassword(string value)
    {
        if (string.IsNullOrEmpty(value))
            return IsEdit ? null : "A senha é necessária";
        return value.Length < 8 ? "Mínimo de 8 caracteres" : null;
    }

    // Confirmação só importa quando há senha digitada
    private string? ValidateConfirm(string value)
    {
        if (string.IsNullOrEmpty(_password)) return null;
        return value == _password ? null : "As senhas não coincidem";
    }

    private async Task Confirm()
    {
        await _form.ValidateAsync();
        if (!_form.IsValid) return;

        // Senha vazia na edição = manter a atual (null sinaliza isso ao serviço)
        var newPassword = string.IsNullOrEmpty(_password) ? null : _password;
        MudDialog.Close(DialogResult.Ok(new UserFormResult(_username.Trim(), _role, _isActive, newPassword)));
    }

    private void Cancel()
    {
        MudDialog.Cancel();
    }

    // Rótulo legível de cada papel
    private static string RoleLabel(UserRole role)
    {
        return role switch
        {
            UserRole.Owner => "Owner — controle total (usuários e secrets)",
            UserRole.Admin => "Admin — gerencia conteúdo",
            UserRole.Operator => "Operator — opera servidores",
            UserRole.Viewer => "Viewer — somente leitura",
            _ => role.ToString()
        };
    }

    /// <summary>Resultado do formulário entregue à página para persistir via UserService.</summary>
    public sealed record UserFormResult(string Username, UserRole Role, bool IsActive, string? NewPassword);
}