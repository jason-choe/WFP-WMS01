// ViewModels/Popups/ConfirmTransferPopupViewModel.cs
using System.Windows.Input;
using WPF_WMS01.Commands;
using System.Windows; // System.Windows.Window를 사용하기 위해 추가

namespace WPF_WMS01.ViewModels.Popups
{
    public class ConfirmTransferPopupViewModel : ViewModelBase
    {
        private string _message;
        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        public ICommand ConfirmCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }

        // 이 속성은 이제 ShowDialog()의 반환 값에 직접 영향을 주지 않으므로 내부에서만 사용됩니다.
        // public bool DialogResult { get; private set; }

        // Design-time 또는 기본 사용을 위한 매개 변수 없는 public 생성자 추가
        public ConfirmTransferPopupViewModel() : this("알 수 없는 랙", "알 수 없는 Lot", "알 수 없는 목적지")
        {
            // 이 생성자는 주로 디자인 타임 뷰에서 ViewModel을 인스턴스화할 때 사용됩니다.
            // 런타임에서는 아래의 매개 변수 있는 생성자가 사용될 것입니다.
        }

        public ConfirmTransferPopupViewModel(string sourceRackTitle, string sourceLotNumber, string destinationRackTitle)
        {
            Message = $"랙 [{sourceRackTitle}]의 제품 (Lot: {sourceLotNumber})을/를 랙 [{destinationRackTitle}](으)로 이동하시겠습니까?";
            ConfirmCommand = new RelayCommand(ExecuteConfirm);
            CancelCommand = new RelayCommand(ExecuteCancel);
            // DialogResult = false; // 이제 Window.DialogResult를 직접 설정합니다.
        }

        private void ExecuteConfirm(object parameter)
        {
            if (parameter is Window window)
            {
                window.DialogResult = true; // 확인 시 Window.DialogResult를 true로 설정
                window.Close();
            }
        }

        private void ExecuteCancel(object parameter)
        {
            if (parameter is Window window)
            {
                window.DialogResult = false; // 취소 시 Window.DialogResult를 false로 설정
                window.Close();
            }
        }
    }
}
