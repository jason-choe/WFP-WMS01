using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Controls; // ProgressBar를 참조하기 위함

namespace WPF_WMS01.Converters
{
    public class ProgressToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double progress && parameter is ProgressBar progressBar)
            {
                return progressBar.ActualWidth * (progress / progressBar.Maximum);
            }
            if (value is int intProgress && parameter is ProgressBar intProgressBar)
            {
                return intProgressBar.ActualWidth * (intProgress / intProgressBar.Maximum);
            }
            return 0.0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}