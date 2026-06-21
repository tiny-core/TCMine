using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace TCMine_Server.Components.Shared;

public partial class ErrorScreen : ComponentBase
{
    // Código grande em destaque (ex.: "404", "500")
    [Parameter] [EditorRequired] public string Code { get; set; } = null!;

    // Título curto da situação (ex.: "Página não encontrada")
    [Parameter] [EditorRequired] public string Title { get; set; } = null!;

    // Mensagem explicativa abaixo do título
    [Parameter] [EditorRequired] public string Message { get; set; } = null!;

    // Ícone do Material exibido no topo; padrão é um ícone genérico de erro
    [Parameter] public string Icon { get; set; } = Icons.Material.Filled.ErrorOutline;

    // Cor do ícone e do código — acento do tema por padrão
    [Parameter] public Color Color { get; set; } = Color.Primary;

    // Conteúdo de ação (botões, alerta) renderizado abaixo da mensagem
    [Parameter] public RenderFragment? ChildContent { get; set; }
}