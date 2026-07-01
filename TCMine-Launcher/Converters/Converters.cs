using Avalonia.Data.Converters;

namespace TCMine_Launcher.Converters;

/// <summary>Conversores de valor reutilizáveis nas views (referenciados via <c>{x:Static}</c>).</summary>
public static class Converters
{
    /// <summary>int &gt; 0 → true (ex.: mostrar o badge "Servidor" só quando há servidores).</summary>
    public static readonly IValueConverter IntIsPositive = new FuncValueConverter<int, bool>(n => n > 0);

    /// <summary>int == 0 → true (ex.: empty-state quando não há instâncias).</summary>
    public static readonly IValueConverter IntIsZero = new FuncValueConverter<int, bool>(n => n == 0);
}
