namespace SpatialTileBuilder.App.Converters;

using System.Windows.Data;
using System;
using System.Globalization;

public class ConverterParameterToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value == null || parameter == null) return System.Windows.Visibility.Collapsed;
        
        // Check if value (int) matches parameter (string parsed to int)
        if (int.TryParse(parameter.ToString(), out int targetStep) && value is int currentStep)
        {
            return currentStep == targetStep ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }
        
        return System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
