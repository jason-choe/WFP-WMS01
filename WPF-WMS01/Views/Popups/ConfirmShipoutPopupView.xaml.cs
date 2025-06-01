using System.Windows;
using System.ComponentModel;
using WPF_WMS01.ViewModels.Popups; // 뷰모델 네임스페이스는 그대로 유지

namespace WPF_WMS01.Views.Popups
{
    public partial class ConfirmShipoutPopupView : Window // 클래스 이름 변경
    {
        public ConfirmShipoutPopupView()
        {
            InitializeComponent();
            this.DataContextChanged += ConfirmShipoutPopupView_DataContextChanged; // 메서드 이름도 변경하는 것이 좋지만, 기존 이름 유지해도 동작
        }

        private void ConfirmShipoutPopupView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) // 메서드 이름 변경 (권장)
        {
            if (e.OldValue is INotifyPropertyChanged oldViewModel)
            {
                oldViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }
            if (e.NewValue is ConfirmShipoutPopupViewModel newViewModel) // 뷰모델 캐스팅 타입 변경
            {
                newViewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ConfirmShipoutPopupViewModel.DialogResult))
            {
                if (DataContext is ConfirmShipoutPopupViewModel viewModel && viewModel.DialogResult.HasValue) // 뷰모델 캐스팅 타입 변경
                {
                    this.DialogResult = viewModel.DialogResult.Value;
                    this.Close();
                }
            }
        }
    }
}