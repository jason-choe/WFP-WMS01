using System.Threading.Tasks;
using System.Windows.Input; // ICommand를 위해
using WPF_WMS01.Commands; // RelayCommand 사용을 위해

namespace WPF_WMS01.ViewModels.Popups
{
    public class AutoClosingMessagePopupViewModel : ViewModelBase
    {
        private string _message;
        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        private bool? _dialogResult;
        public bool? DialogResult
        {
            get => _dialogResult;
            set => SetProperty(ref _dialogResult, value);
        }

        // 팝업을 닫는 명령 (선택 사항, 그러나 유용할 수 있음)
        public ICommand CloseCommand { get; private set; }

        public AutoClosingMessagePopupViewModel(string message)
        {
            Message = message;
            CloseCommand = new RelayCommand(ExecuteClose);
        }

        private void ExecuteClose(object parameter)
        {
            DialogResult = true; // 팝업 닫기
        }
    }
}
