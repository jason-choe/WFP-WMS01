using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media; // Brush, SolidColorBrush 등을 위해 필요

namespace WPF_WMS01.Converters // 실제 프로젝트의 네임스페이스로 변경하세요.
{
    public class BackgroundColorConverter : IValueConverter
    {
        // Source -> Target (View)
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 예시: 불리언 값에 따라 색상 변경
            // ViewModel의 프로퍼티가 bool 타입이라고 가정합니다.
            if (value is bool booleanValue)
            {
                // booleanValue가 true이면 Red Color, false이면 White Color 반환
                return new SolidColorBrush(booleanValue ? Colors.Red : Colors.White);
            }
            return new SolidColorBrush(Colors.White); // 기본 배경색
        }

        // Target (View) -> Source (ViewModel)
        // 일반적으로 BackgroundColorConverter에서는 사용되지 않습니다.
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}