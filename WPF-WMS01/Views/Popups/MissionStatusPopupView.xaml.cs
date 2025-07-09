// Views/Popups/MissionStatusPopupView.xaml.cs
using System.Windows;
using WPF_WMS01.ViewModels.Popups;

namespace WPF_WMS01.Views.Popups
{
    /// <summary>
    /// MissionStatusPopupView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MissionStatusPopupView : Window
    {
        public MissionStatusPopupView()
        {
            InitializeComponent();
            // DataContext가 설정될 때 CloseAction을 연결합니다.
            this.DataContextChanged += MissionStatusPopupView_DataContextChanged;
        }

        private void MissionStatusPopupView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is MissionStatusPopupViewModel viewModel)
            {
                viewModel.CloseAction = () => this.Close();
            }
        }
    }
}
