using System.Windows;
using WPF_WMS01.ViewModels.Popups;
using System.ComponentModel; // INotifyPropertyChanged를 위해 추가

namespace WPF_WMS01.Views.Popups
{
    public partial class RackTypeChangePopupView : Window
    {
        public RackTypeChangePopupView()
        {
            InitializeComponent();

            // DataContext가 설정될 때 이벤트를 구독하여 ViewModel의 DialogResult 변경을 감지합니다.
            this.DataContextChanged += RackTypeChangePopupView_DataContextChanged;
        }

        private void RackTypeChangePopupView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is RackTypeChangePopupViewModel oldViewModel)
            {
                oldViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            if (e.NewValue is RackTypeChangePopupViewModel newViewModel)
            {
                newViewModel.PropertyChanged += ViewModel_PropertyChanged;
                // ViewModel의 DialogResult가 이미 설정되어 있을 경우 처리 (예: 디자인 타임)
                if (newViewModel.DialogResult.HasValue)
                {
                    this.DialogResult = newViewModel.DialogResult;
                    this.Close();
                }
            }
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RackTypeChangePopupViewModel.DialogResult))
            {
                if (sender is RackTypeChangePopupViewModel viewModel && viewModel.DialogResult.HasValue)
                {
                    this.DialogResult = viewModel.DialogResult;
                    this.Close(); // ViewModel에서 DialogResult가 설정되면 윈도우를 닫습니다.
                }
            }
        }
    }
}
