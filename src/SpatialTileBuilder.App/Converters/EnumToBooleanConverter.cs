namespace SpatialTileBuilder.App.Converters;

using System.Windows.Data;
using System;
using System.Globalization;
using SpatialTileBuilder.Core.Enums;

public class EnumToBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value == null || parameter == null) return false;
        
        string checkValue = value.ToString() ?? "";
        string targetValue = parameter.ToString() ?? "";
        
        return checkValue.Equals(targetValue, StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool b && b && parameter is string targetStr)
        {
            if (Enum.TryParse(typeof(RegionType), targetStr, true, out var result))
            {
                return result;
            }
        }
        return System.Windows.DependencyProperty.UnsetValue;
    }
}
