using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WPF_WMS01.Commands;

namespace WPF_WMS01.ViewModels.Popups
{
    public class ConfirmShipoutPopupViewModel : INotifyPropertyChanged // 클래스 이름 변경
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private string _confirmationMessage;
        public string ConfirmationMessage
        {
            get => _confirmationMessage;
            set
            {
                _confirmationMessage = value;
                OnPropertyChanged();
            }
        }

        private string _lotNoMessage;
        public string LotNoMessage
        {
            get => _lotNoMessage;
            set
            {
                _lotNoMessage = value;
                OnPropertyChanged();
            }
        }

        private bool? _dialogResult;
        public bool? DialogResult
        {
            get => _dialogResult;
            set
            {
                if (_dialogResult != value)
                {
                    _dialogResult = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand ConfirmCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }

        public ConfirmShipoutPopupViewModel(string rackTitle, int bulletType, string LotNo)
        {
            string product = bulletType == 1 ? "223A"
                : bulletType == 2 ? "223SP"
                : bulletType == 3 ? "223XM"
                : bulletType == 4 ? "5.56X"
                : bulletType == 5 ? "5.56K"
                : bulletType == 6 ? "M855T"
                : bulletType == 7 ? "M193"
                : bulletType == 8 ? "308B"
                : bulletType == 9 ? "308SP"
                : bulletType == 10 ? "308XM"
                : bulletType == 11 ? "7.62X" : "M855T";
            LotNoMessage = $"Lot No. :  {LotNo}";
            ConfirmationMessage = $"랙 {rackTitle}의 {product} 제품을 출고할까요?";
            ConfirmCommand = new RelayCommand(ExecuteConfirm);
            CancelCommand = new RelayCommand(ExecuteCancel);
        }

        private void ExecuteConfirm(object parameter)
        {
            DialogResult = true;
        }

        private void ExecuteCancel(object parameter)
        {
            DialogResult = false;
        }
    }
}