using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace RobustFiler.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool bValue = value is bool b && b;
        if (Invert) bValue = !bValue;
        return bValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
