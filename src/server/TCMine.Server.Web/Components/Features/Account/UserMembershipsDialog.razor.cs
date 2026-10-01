using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine.Server.Application.Abstractions;
using TCMine.Server.Domain.Identity;

namespace TCMine.Server.Web.Components.Features.Account;

/// <summary>
///     Em que servidores e modpacks esta conta tem algum papel.
///     Injeta os repositórios direto, sem caso de uso: mostrar vínculo de quem
///     já está vendo a tela de Usuários (admin-only) não pede autorização
///     própria — mesma regra que IModpackMembershipRepository já documenta
///     para leituras deste tipo.
/// </summary>
public partial class UserMembershipsDialog : ComponentBase
{
    private bool _loading = true;
    private List<(string ServerName, string Role)> _servers = [];
    private List<(string ModpackName, string Role)> _modpacks = [];

    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;

    [Parameter] [EditorRequired] public Guid UserId { get; set; }

    [Parameter] [EditorRequired] public string DisplayName { get; set; } = "";

    [Inject] private IMembershipRepository Memberships { get; set; } = default!;
    [Inject] private IModpackMembershipRepository ModpackMemberships { get; set; } = default!;
    [Inject] private IServerRepository Servers { get; set; } = default!;
    [Inject] private IModpackRepository Modpacks { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        var vinculosServidor = await Memberships.ListByUserAsync(UserId, CancellationToken.None);
        var vinculosModpack = await ModpackMemberships.ListByUserAsync(UserId, CancellationToken.None);

        // Duas idas extra ao banco, em vez de um join: a lista inteira de
        // servidores/modpacks já é pequena (mesma premissa do resto do
        // painel), e isto evita portas novas só para um nome por vínculo.
        var todosServidores = await Servers.ListAllAsync(CancellationToken.None);
        var todosModpacks = await Modpacks.ListAsync(CancellationToken.None);

        _servers =
        [
            .. vinculosServidor.Select(m => (
                todosServidores.FirstOrDefault(s => s.Id == m.GameServerId)?.Name ?? "(servidor removido)",
                Rotulo(m.Role)))
        ];

        _modpacks =
        [
            .. vinculosModpack.Select(m => (
                todosModpacks.FirstOrDefault(p => p.Id == m.ModpackId)?.Name ?? "(modpack removido)",
                m.Role == ModpackRole.Owner ? "Dono" : "Editor"))
        ];

        _loading = false;
    }

    private static string Rotulo(ServerRole role) => role switch
    {
        ServerRole.Member => "Membro",
        ServerRole.Moderator => "Moderador",
        ServerRole.Admin => "Admin",
        _ => "Dono"
    };

    private void Close() => Dialog.Close();
}
