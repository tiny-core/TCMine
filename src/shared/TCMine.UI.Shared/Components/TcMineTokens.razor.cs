using Microsoft.AspNetCore.Components;
using TCMine.UI.Shared.Theming;

namespace TCMine.UI.Shared.Components;

public partial class TcMineTokens : ComponentBase
{
    // Estático: o resultado nunca muda durante a execução, e construir uma
    // vez por requisição seria desperdício em página com muitos acessos.
    private static readonly string Css = TokenCssBuilder.Build();
}