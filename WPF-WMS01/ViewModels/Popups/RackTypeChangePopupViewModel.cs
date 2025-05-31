using System;
using System.Windows.Input;
using System.Windows;
using WPF_WMS01.Commands; // RelayCommand 사용
using WPF_WMS01.Models; // Rack 모델 사용
using System.ComponentModel; // INotifyPropertyChanged

namespace WPF_WMS01.ViewModels.Popups
{
    public class RackTypeChangePopupViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _message;
        public string Message
        {
            get { return _message; }
            set
            {
                if (_message != value)
                {
                    _message = value;
                    OnPropertyChanged(nameof(Message));
                }
            }
        }

        private bool? _dialogResult;
        public bool? DialogResult
        {
            get { return _dialogResult; }
            set
            {
                if (_dialogResult != value)
                {
                    _dialogResult = value;
                    OnPropertyChanged(nameof(DialogResult));
                }
            }
        }

        public ICommand OkCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }

        public RackTypeChangePopupViewModel(int currentRackType, int newRackType)
        {
            string currentTypeDesc = currentRackType == 0 ? "wrapping 되지 않은 팔레트" : "wrapping 된 팔레트";
            string newTypeDesc = newRackType == 0 ? "wrapping 되지 않은 팔레트" : "wrapping 된 팔레트";

            Message = $"현재 랙의 용도는 '{currentTypeDesc}' 용입니다. 랙 용도를 '{newTypeDesc}' 용으로 변경하시겠습니까?";

            OkCommand = new RelayCommand(ExecuteConfirm); // <Window> 제거
            CancelCommand = new RelayCommand(ExecuteCancel); // <Window> 제거
        }

        private void ExecuteConfirm(object parameter) // 매개변수 object로 변경
        {
            if (parameter is Window window)
            {
                window.DialogResult = true;
                window.Close();
            }
        }

        private void ExecuteCancel(object parameter) // 매개변수 object로 변경
        {
            if (parameter is Window window)
            {
                window.DialogResult = false;
                window.Close();
            }
        }
    }
}
