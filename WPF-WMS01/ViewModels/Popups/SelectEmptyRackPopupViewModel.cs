using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WPF_WMS01.Commands;
using WPF_WMS01.Models; // Rack 모델 사용

namespace WPF_WMS01.ViewModels.Popups
{
    public class SelectEmptyRackPopupViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private ObservableCollection<Rack> _racks;
        public ObservableCollection<Rack> Racks
        {
            get => _racks;
            set
            {
                _racks = value;
                OnPropertyChanged();
            }
        }

        private Rack _selectedRack;
        public Rack SelectedRack
        {
            get => _selectedRack;
            set
            {
                if (_selectedRack != value)
                {
                    _selectedRack = value;
                    OnPropertyChanged();
                    ((RelayCommand)ConfirmCommand).RaiseCanExecuteChanged(); // 선택에 따라 확인 버튼 활성화/비활성화
                }
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

        private string _lotNo; // LotNo 속성
        public string LotNo
        {
            get => _lotNo;
            set
            {
                _lotNo = value;
                OnPropertyChanged();
            }
        }

        private string _firstMessage;
        public string FirstMessage
        {
            get => _firstMessage;
            set
            {
                _firstMessage = value;
                OnPropertyChanged();
            }
        }

        private string _secondMessage;
        public string SecondMessage
        {
            get => _secondMessage;
            set
            {
                _secondMessage = value;
                OnPropertyChanged();
            }
        }
        public ICommand ConfirmCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }

        public SelectEmptyRackPopupViewModel(List<Rack> emptyRacks, string lotNo, string sentenceOne, string sentenceTwo)
        {
            Racks = new ObservableCollection<Rack>(
                emptyRacks.OrderBy(rack =>
                {
                    // 첫 번째 숫자 부분 추출
                    string[] parts = rack.Title.Split('-');
                    if (parts.Length > 0 && int.TryParse(parts[0], out int num))
                    {
                        return num;
                    }
                    return int.MaxValue; // 파싱 실패 시 가장 뒤로 보내기
                })
                .ThenBy(rack =>
                {
                    // 두 번째 숫자 부분 추출 (있을 경우)
                    string[] parts = rack.Title.Split('-');
                    if (parts.Length > 1 && int.TryParse(parts[1], out int num))
                    {
                        return num;
                    }
                    return int.MaxValue; // 파싱 실패 시 가장 뒤로 보내기
                })
            );

            ConfirmCommand = new RelayCommand(ExecuteConfirm, CanExecuteConfirm);
            CancelCommand = new RelayCommand(ExecuteCancel);
            FirstMessage = $" / {sentenceOne}";
            SecondMessage = $"'{sentenceTwo}'";
            LotNo = lotNo;
        }

        private void ExecuteConfirm(object parameter)
        {
            if (SelectedRack != null)
            {
                DialogResult = true;
            }
        }

        private bool CanExecuteConfirm(object parameter)
        {
            return SelectedRack != null; // 랙이 선택되었을 때만 확인 버튼 활성화
        }

        private void ExecuteCancel(object parameter)
        {
            DialogResult = false;
        }
    }
}