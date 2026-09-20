using Microsoft.AspNetCore.Components;
using TCMine.Contracts.Modpacks;

namespace TCMine.Launcher.UI.Components;

/// <summary>
///     As novidades publicadas do pack ativo.
///     O corpo é renderizado como TEXTO, nunca como marcação. Os posts são
///     escritos no painel do servidor, e interpretar HTML aqui daria a quem
///     escreve um post o poder de injetar conteúdo na janela do jogador.
/// </summary>
public partial class HomeNews : ComponentBase
{
    [Parameter] [EditorRequired] public IReadOnlyList<ModpackNewsDto> Posts { get; set; } = [];
}
