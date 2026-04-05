using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DungeonSiegeLab.Views;

/// <summary>
/// Konvertuje bool na farbu.
/// ConverterParameter = "FarbaKeďTrue|FarbaKeďFalse" (hex).
/// </summary>
public class BoolToBrushConverter : IValueConverter
{
    public static readonly BoolToBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var parts = (parameter as string ?? "#ffffff|#888888").Split('|');
        var hex = (value is true) ? parts[0] : (parts.Length > 1 ? parts[1] : "#888888");
        return SolidColorBrush.Parse(hex);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Konvertuje status text na farbu (OK = zelená, inak červená).
/// </summary>
public class StatusToBrushConverter : IValueConverter
{
    public static readonly StatusToBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value as string ?? "";
        return text.StartsWith("OK", StringComparison.OrdinalIgnoreCase)
            ? SolidColorBrush.Parse("#a6e3a1")
            : SolidColorBrush.Parse("#f38ba8");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
