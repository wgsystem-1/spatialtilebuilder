namespace SpatialTileBuilder.App.Converters;

using System.Windows.Data;
using System;
using System.Globalization;

public class StepToButtonTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is int step && step == 4) // Last step
        {
            return "Save";
        }
        return "Next";
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
