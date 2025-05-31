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
            // 이전 DataContext가 INotifyPropertyChanged를 구현했으면 구독 해지
            if (e.OldValue is INotifyPropertyChanged oldViewModel)
            {
                oldViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }
            // 새로운 DataContext가 RackTypeChangePopupViewModel이면 구독
            if (e.NewValue is RackTypeChangePopupViewModel newViewModel)
            {
                newViewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        // using System.ComponentModel; 이 상단에 있어야 합니다.
        // using WPF_WMS01.ViewModels.Popups; 이 상단에 있어야 합니다.
        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // DialogResult 속성이 변경되었는지 확인
            if (e.PropertyName == nameof(RackTypeChangePopupViewModel.DialogResult))
            {
                // 현재 DataContext가 RackTypeChangePopupViewModel이고, DialogResult에 값이 있는지 확인
                if (DataContext is RackTypeChangePopupViewModel viewModel && viewModel.DialogResult.HasValue)
                {
                    // 여기에서 System.InvalidOperationException이 발생한다고 하셨습니다.
                    // 이 라인이 실행될 때 Window의 상태가 DialogResult를 설정할 수 있는 상태인지가 중요합니다.
                    this.DialogResult = viewModel.DialogResult.Value; // 뷰의 DialogResult 설정
                    this.Close(); // 뷰 닫기
                }
            }
        }
    }
}
