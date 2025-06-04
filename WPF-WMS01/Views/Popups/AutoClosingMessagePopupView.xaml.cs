using System.Windows;
using System.Windows.Threading; // DispatcherTimer 사용을 위해 추가
using System;
using System.Windows.Input; // 마우스 드래그를 위해 추가

namespace WPF_WMS01.Views.Popups
{
    /// <summary>
    /// AutoClosingMessagePopupView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class AutoClosingMessagePopupView : Window
    {
        private DispatcherTimer _timer;

        public AutoClosingMessagePopupView()
        {
            InitializeComponent();
            SetupTimer();
        }

        private void SetupTimer()
        {
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(2); // 2초 설정
            _timer.Tick += (sender, e) =>
            {
                _timer.Stop();
                this.Close(); // 팝업 닫기
            };
            _timer.Start();
        }

        // 팝업을 드래그하여 이동할 수 있도록 (WindowStyle="None" 일 때 유용)
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        // DataContext 변경 시 ViewModel의 DialogResult를 윈도우에 연결 (선택 사항)
        // AutoClosingMessagePopupViewModel 에서는 DialogResult를 직접 사용하지 않아도 되지만,
        // 일관성을 위해 유지하거나 필요에 따라 제거할 수 있습니다.
        private void Window_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (DataContext is ViewModels.Popups.AutoClosingMessagePopupViewModel vm)
            {
                // 이전 DataContext의 PropertyChanged 이벤트 구독을 해제합니다. (메모리 누수 방지)
                if (e.OldValue is ViewModels.Popups.AutoClosingMessagePopupViewModel oldVm)
                {
                    oldVm.PropertyChanged -= Vm_PropertyChanged;
                }

                // 새 DataContext의 PropertyChanged 이벤트에 구독합니다.
                vm.PropertyChanged += Vm_PropertyChanged;
            }
        }

        private void Vm_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is ViewModels.Popups.AutoClosingMessagePopupViewModel vm)
            {
                if (e.PropertyName == nameof(vm.DialogResult))
                {
                    // 뷰모델의 DialogResult가 변경되면 윈도우의 DialogResult를 설정하여 윈도우를 닫습니다.
                    // 이 팝업은 타이머로 닫히므로 이 부분은 거의 사용되지 않을 수 있습니다.
                    this.DialogResult = vm.DialogResult;
                }
            }
        }
    }
}