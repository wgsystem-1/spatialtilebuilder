namespace SpatialTileBuilder.App.Converters;

using System.Windows.Data;
using System;
using System.Globalization;

public class StringFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value == null) return string.Empty;
        if (parameter == null) return value;
        
        return string.Format(parameter.ToString() ?? "{0}", value);
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
         throw new NotImplementedException();
    }
}
