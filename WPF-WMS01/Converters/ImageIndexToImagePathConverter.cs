// Converters/ImageIndexToImagePathConverter.cs
using System;
using System.Globalization;
using System.Windows.Data; // 이 using 문이 중요합니다.
using System.Windows.Media.Imaging;

namespace WPF_WMS01.Converters // 네임스페이스 확인
{
    public class ImageIndexToImagePathConverter : IValueConverter // IValueConverter 구현 확인
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int imageIndex)
            {
                // 어셈블리 이름을 정확하게 명시합니다. (WPF_WMS01)
                // 프로젝트의 Images 폴더에 이미지가 있다고 가정합니다.
                return new BitmapImage(new Uri($"pack://application:,,,/WPF-WMS01;component/images/rack_state_{imageIndex}.png"));
            }
            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}