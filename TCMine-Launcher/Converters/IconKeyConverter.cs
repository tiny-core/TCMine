using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace TCMine_Launcher.Converters;

/// <summary>
/// Resolve uma chave de ícone (ex.: "IconInstances") para a <see cref="Geometry"/> registada em
/// <c>Themes/Icons.axaml</c> — permite escolher o ícone por dados (binding) em vez de fixo no XAML.
/// </summary>
public sealed class IconKeyConverter : IValueConverter
{
    public static readonly IconKeyConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string key && Application.Current is { } app &&
            app.TryGetResource(key, app.ActualThemeVariant, out var res) && res is Geometry geometry)
            return geometry;

        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
