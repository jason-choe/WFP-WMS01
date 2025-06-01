// Views/Popups/SelectCheckoutRackPopupView.xaml.cs
using System.Windows;

namespace WPF_WMS01.Views.Popups
{
    /// <summary>
    /// SelectCheckoutRackPopupView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class SelectCheckoutRackPopupView : Window
    {
        public SelectCheckoutRackPopupView()
        {
            InitializeComponent();
            // 생성자에서 DataContextChanged 이벤트에 핸들러를 연결하는 것을 잊지 마세요.
            // XAML에서 DataContextChanged="Window_DataContextChanged" 로 연결했다면 필요 없습니다.
        }

        // 이 메서드를 추가하거나 기존 메서드를 확인합니다.
        private void Window_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (DataContext is ViewModels.Popups.SelectCheckoutRackPopupViewModel vm)
            {
                // 이전 DataContext의 PropertyChanged 이벤트 구독을 해제합니다.
                // (선택 사항이지만, 메모리 누수 방지 및 중복 구독 방지를 위해 좋은 습관입니다.)
                if (e.OldValue is ViewModels.Popups.SelectCheckoutRackPopupViewModel oldVm)
                {
                    oldVm.PropertyChanged -= Vm_PropertyChanged;
                }

                // 새 DataContext의 PropertyChanged 이벤트에 구독합니다.
                vm.PropertyChanged += Vm_PropertyChanged;
            }
        }

        private void Vm_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is ViewModels.Popups.SelectCheckoutRackPopupViewModel vm)
            {
                if (e.PropertyName == nameof(vm.DialogResult))
                {
                    // 뷰모델의 DialogResult가 변경되면 윈도우의 DialogResult를 설정하여 윈도우를 닫습니다.
                    this.DialogResult = vm.DialogResult;
                }
            }
        }
    }
}