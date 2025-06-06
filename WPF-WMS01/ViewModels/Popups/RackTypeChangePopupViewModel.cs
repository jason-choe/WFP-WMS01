﻿using System;
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

        public ICommand ConfirmCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }

        public RackTypeChangePopupViewModel(int currentRackType, int newRackType)
        {
            string currentTypeDesc = currentRackType == 0 ? "포장 전 팔레트" : "포장 후 팔레트";
            string newTypeDesc = newRackType == 0 ? "포장 전 팔레트" : "포장 후 팔레트";

            Message = $"'{newTypeDesc}' 보관용 랙으로 변경하시겠습니까?";

            ConfirmCommand = new RelayCommand(ExecuteConfirm); // <Window> 제거
            CancelCommand = new RelayCommand(ExecuteCancel); // <Window> 제거
        }

        private void ExecuteConfirm(object parameter) // 매개변수 object로 변경
        {
                DialogResult = true;
        }

        private void ExecuteCancel(object parameter) // 매개변수 object로 변경
        {
                DialogResult = false;
        }
    }
}
