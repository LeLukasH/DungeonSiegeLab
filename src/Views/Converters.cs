using System.Globalization;
using Avalonia.Controls;
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
/// Konvertuje bool na FontStyle: true = Italic, false = Normal.
/// </summary>
public class BoolToFontStyleConverter : IValueConverter
{
    public static readonly BoolToFontStyleConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? FontStyle.Italic : FontStyle.Normal;

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
/// <summary>
/// Konvertuje bool na GridLength: true = Length, false = 0
/// </summary>
public class BoolToGridLengthConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isVisible = value is true;

        if (!isVisible)
            return new GridLength(0);

        if (parameter == null)
            return new GridLength(1, GridUnitType.Star);

        if (double.TryParse(parameter.ToString(), out var width))
            return new GridLength(width);

        return new GridLength(0);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Konvertuje cislo riadku na y-ovu suradnicu
/// </summary>
public class LineToYConverter : IMultiValueConverter
{
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count == 0 || values[0] is not int line)
            return 0;

        const double lineHeight = 18;
        return (line - 1) * lineHeight + 4;
    }
}