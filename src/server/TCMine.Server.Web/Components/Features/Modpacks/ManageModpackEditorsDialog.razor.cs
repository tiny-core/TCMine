using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Contracts.Modpacks;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Application.Modpacks;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Web.Components.Features.Modpacks;

public partial class ManageModpackEditorsDialog : DialogComponentBase
{
    private List<ModpackMemberView> _members = [];
    private List<User> _candidatos = [];
    private Guid? _selecionado;
    private bool _loaded;

    [Parameter] [EditorRequired] public Guid ModpackId { get; set; }

    [Inject] private ListModpackAccess ListUseCase { get; set; } = default!;
    [Inject] private AddModpackEditor AddUseCase { get; set; } = default!;
    [Inject] private RemoveModpackEditor RemoveUseCase { get; set; } = default!;
    [Inject] private IUserRepository Users { get; set; } = default!;

    protected override Task OnInitializedAsync() => LoadAsync();

    private async Task LoadAsync()
    {
        var result = await ListUseCase.HandleAsync(ModpackId, CancellationToken.None);
        _loaded = true;

        if (!result.Succeeded)
        {
            Snackbar.Add(result.Error!, Severity.Error);
            return;
        }

        _members = [.. result.Value!];

        // Só quem ainda não tem vínculo entra na lista de "conceder acesso a" —
        // já é membro não deveria aparecer duas vezes no seletor.
        var jaTemAcesso = _members.Select(m => m.UserId).ToHashSet();
        var todos = await Users.ListAsync(CancellationToken.None);
        _candidatos = [.. todos.Where(u => !jaTemAcesso.Contains(u.Id))];
        _selecionado = _candidatos.Count > 0 ? _candidatos[0].Id : null;
    }

    private Task AddAsync() => RunAsync(async () =>
    {
        if (_selecionado is not { } userId)
            return;

        var result = await AddUseCase.HandleAsync(ModpackId, userId, CancellationToken.None);

        if (result.Succeeded)
            Snackbar.Add("Editor adicionado.", Severity.Success);
        else
            Snackbar.Add(result.Error!, Severity.Error);

        await LoadAsync();
    });

    private Task RemoveAsync(ModpackMemberView member) => RunAsync(async () =>
    {
        var result = await RemoveUseCase.HandleAsync(ModpackId, member.UserId, CancellationToken.None);

        if (result.Succeeded)
            Snackbar.Add($"{member.DisplayName} não edita mais este modpack.", Severity.Success);
        else
            Snackbar.Add(result.Error!, Severity.Error);

        await LoadAsync();
    });

    private void Close() => Dialog.Close();
}
