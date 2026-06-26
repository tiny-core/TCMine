using Microsoft.AspNetCore.Components;
using TCMine_Domain.Entities;

namespace TCMine_Server.Components.Pages.Admin.Modpacks;

/// <summary>
/// Aba "Servidores" do <see cref="ModpackEditor"/>: edita a lista de servidores anunciados (por
/// referência). Add/remove e paginação são locais; a persistência acontece no Guardar do editor.
/// </summary>
public partial class ServersPanel : ComponentBase
{
    [Parameter] [EditorRequired] public List<ServerEntryEntity> Servers { get; set; } = null!;

    private const int PageSize = 5;
    private int _page = 1;

    private int PageCount => Math.Max(1, (Servers.Count + PageSize - 1) / PageSize);
    private IEnumerable<ServerEntryEntity> Paged => Servers.Skip((_page - 1) * PageSize).Take(PageSize);

    private void Add()
    {
        Servers.Add(new ServerEntryEntity { Name = "Novo servidor", Address = "", Port = 25565 });
        _page = PageCount; // pula para a última página para mostrar o recém-adicionado
    }

    private void Remove(ServerEntryEntity server)
    {
        Servers.Remove(server);
        if (_page > PageCount) _page = PageCount; // não deixa a página passar do fim
    }
}
