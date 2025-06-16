// Converters/LoginStatusToColorConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace WPF_WMS01.Converters
{
    public class LoginStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isLoggedIn)
            {
                return isLoggedIn ? Brushes.Green : Brushes.Red; // 로그인 성공 시 녹색, 실패 시 빨간색
            }
            return Brushes.Gray; // 기본값
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}