// MainWindow.xaml.cs
using System.Windows;
using WPF_WMS01.ViewModels;

namespace WPF_WMS01
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // DataContext는 XAML에서 설정했으므로 여기서 따로 설정할 필요 없습니다.
            // 만약 XAML에서 설정하지 않았다면 아래와 같이 설정할 수 있습니다.
            //this.DataContext = new MainViewModel();
        }
    }
}