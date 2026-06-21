using Microsoft.AspNetCore.Components;
using MudBlazor;
using TCMine_Server.Theme;

namespace TCMine_Server.Components.Layout;

public partial class RootLayout : LayoutComponentBase
{
    private readonly MudTheme _theme = MudThemeFactory.Create();
    private bool _isDarkMode = true;
}