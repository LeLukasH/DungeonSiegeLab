using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;

namespace DungeonSiegeLab.Converters;

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