// Converters/BooleanToVisibilityConverterForHidden.cs
using System;
using System.Globalization;
using System.Windows; // Visibility 열거형을 위해 필요
using System.Windows.Data; // IValueConverter 인터페이스를 위해 필요

namespace WPF_WMS01.Converters
{
    public class BooleanToVisibilityConverterForHidden : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool booleanValue)
            {
                // booleanValue가 true이면 Visible, false이면 Hidden 반환
                return booleanValue ? Visibility.Visible : Visibility.Hidden;
            }
            // bool 타입이 아니면 기본적으로 Hidden 반환 (오류 방지)
            return Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 단방향 변환이므로 구현하지 않아도 됩니다.
            throw new NotImplementedException();
        }
    }
}