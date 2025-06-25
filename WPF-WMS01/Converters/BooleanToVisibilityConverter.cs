using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WPF_WMS01.Converters
{
    /// <summary>
    /// Boolean 값을 Visibility 값으로 변환합니다. True는 Visible, False는 Collapsed입니다.
    /// Converts a Boolean value to a Visibility value. True is Visible, False is Collapsed.
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool booleanValue)
            {
                return booleanValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed; // 기본값
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }
            return false; // 기본값
        }
    }
}
