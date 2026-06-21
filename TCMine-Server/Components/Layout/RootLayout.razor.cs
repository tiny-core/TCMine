using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine_Core.Design;

namespace TCMine_Server.Components.Layout;

public partial class RootLayout : LayoutComponentBase
{
    private readonly MudTheme _theme = MudThemeFactory.Create();
    private bool _isDarkMode = true;
}