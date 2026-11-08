using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace HljuML.App.Core;

public sealed class UniformThicknessConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is double spacing ? new Thickness(spacing) : new Thickness();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}