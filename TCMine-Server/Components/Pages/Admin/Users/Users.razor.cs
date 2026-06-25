using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using System.Security.Claims;
using TCMine_Domain.Entities;
using TCMine_Domain.Identity;
using TCMine_Infrastructure.Identity;
using TCMine_Server.Components.Pages.Admin.Users.Dialogs;
using TCMine_Server.Services;

namespace TCMine_Server.Components.Pages.Admin.Users;

/// <summary>
/// Gestão de usuários do painel (rota só para Owner). Lista todos, abre o <see cref="UserEditDialog"/>
/// para criar/editar e delega a persistência ao <see cref="UserService"/> — que centraliza hash de
/// senha, unicidade do login e a proteção do último Owner ativo. A lista é recarregada após cada
/// alteração para refletir o estado real do banco.
/// </summary>
public partial class Users : ComponentBase
{
    [Inject] private UserService Service { get; set; } = null!;
    [Inject] private IDialogService DialogService { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;
    [Inject] private BusyService Busy { get; set; } = null!;
    [CascadingParameter] private Task<AuthenticationState> AuthState { get; set; } = null!;

    // null = carregando (mostra skeletons); lista vazia = estado vazio
    private List<UserEntity>? _rows;

    // Id do usuário logado — para marcar "você" e impedir a auto-remoção
    private Guid _currentUserId;

    protected override async Task OnInitializedAsync()
    {
        var state = await AuthState;
        if (Guid.TryParse(state.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            _currentUserId = id;

        await Busy.RunAsync("Carregando usuários…", ReloadAsync);
    }

    // Recarrega a lista sem overlay próprio — quem chama decide o envelope de feedback
    private async Task ReloadAsync()
    {
        _rows = await Service.GetAllAsync();
    }

    private async Task CreateAsync()
    {
        var dialog = await DialogService.ShowAsync<UserEditDialog>("Novo usuário");
        var result = await dialog.Result;
        if (result is null || result.Canceled || result.Data is not UserEditDialog.UserFormResult form) return;

        try
        {
            await Busy.RunAsync("Criando usuário…", async () =>
            {
                // Senha obrigatória na criação — o diálogo já valida, fallback defensivo aqui
                await Service.CreateAsync(form.Username, form.NewPassword ?? string.Empty, form.Role);
                await ReloadAsync();
            });
            Snackbar.Add($"Usuário \"{form.Username}\" criado.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private async Task EditAsync(UserEntity user)
    {
        var parameters = new DialogParameters<UserEditDialog> { { x => x.User, user } };
        var dialog = await DialogService.ShowAsync<UserEditDialog>($"Editar {user.Username}", parameters);
        var result = await dialog.Result;
        if (result is null || result.Canceled || result.Data is not UserEditDialog.UserFormResult form) return;

        try
        {
            await Busy.RunAsync("Salvando usuário…", async () =>
            {
                await Service.UpdateAsync(user.Id, form.Username, form.Role, form.IsActive, form.NewPassword);
                await ReloadAsync();
            });
            Snackbar.Add("Usuário atualizado.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private async Task ToggleActiveAsync(UserEntity user)
    {
        try
        {
            await Busy.RunAsync(user.IsActive ? "Desativando usuário…" : "Ativando usuário…", async () =>
            {
                await Service.SetActiveAsync(user.Id, !user.IsActive);
                await ReloadAsync();
            });
            Snackbar.Add(user.IsActive ? "Usuário desativado." : "Usuário ativado.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private async Task DeleteAsync(UserEntity user)
    {
        var ok = await DialogService.ShowMessageBoxAsync(
            "Remover usuário",
            $"Remover \"{user.Username}\" definitivamente? Esta ação não pode ser desfeita.",
            "Remover", cancelText: "Cancelar");

        if (ok != true) return;

        try
        {
            await Busy.RunAsync("Removendo usuário…", async () =>
            {
                await Service.DeleteAsync(user.Id);
                await ReloadAsync();
            });
            Snackbar.Add("Usuário removido.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, Severity.Error);
        }
    }

    // Cor do chip por papel — Owner em destaque, decrescendo o privilégio
    private static Color RoleColor(UserRole role)
    {
        return role switch
        {
            UserRole.Owner => Color.Primary,
            UserRole.Admin => Color.Secondary,
            UserRole.Operator => Color.Info,
            _ => Color.Default
        };
    }
}