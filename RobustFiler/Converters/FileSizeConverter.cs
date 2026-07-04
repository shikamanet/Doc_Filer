using System;
using Microsoft.UI.Xaml.Data;

namespace RobustFiler.Converters;

public class FileSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is long size)
        {
            if (size < 0) return ""; // Folder or unavailable
            if (size < 1024) return $"{size} B";
            if (size < 1024 * 1024) return $"{size / 1024.0:N0} KB";
            if (size < 1024 * 1024 * 1024) return $"{size / (1024.0 * 1024.0):N1} MB";
            return $"{size / (1024.0 * 1024.0 * 1024.0):N2} GB";
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
