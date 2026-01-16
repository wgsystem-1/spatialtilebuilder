namespace SpatialTileBuilder.App.Converters;

using System.Windows;
using System.Windows.Data;
using System;
using System.Globalization;
using SpatialTileBuilder.Core.Enums;

public class EnumToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value == null || parameter == null) return Visibility.Collapsed;
        
        string checkValue = value.ToString() ?? "";
        string targetValue = parameter.ToString() ?? "";
        
        return checkValue.Equals(targetValue, StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
