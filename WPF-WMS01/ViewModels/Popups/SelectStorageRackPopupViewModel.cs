using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using WPF_WMS01.Commands;
using WPF_WMS01.Models; // Rack 모델 사용

namespace WPF_WMS01.ViewModels.Popups
{
    public class SelectStorageRackPopupViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private ObservableCollection<Rack> _availableRacks;
        public ObservableCollection<Rack> AvailableRacks
        {
            get => _availableRacks;
            set
            {
                _availableRacks = value;
                OnPropertyChanged();
            }
        }

        private Rack _selectedRack;
        public Rack SelectedRack
        {
            get => _selectedRack;
            set
            {
                _selectedRack = value;
                OnPropertyChanged();
                ((RelayCommand)SelectCommand).RaiseCanExecuteChanged();
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

        public ICommand SelectCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }

        public bool DialogResult { get; private set; }

        public SelectStorageRackPopupViewModel(IEnumerable<Rack> racks, string lotNo)
        {
            // 🚨 수정할 부분: AvailableRacks를 설정하기 전에 Title(랙 번호) 기준으로 정렬
            AvailableRacks = new ObservableCollection<Rack>(
                racks.OrderBy(rack =>
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

            LotNo = lotNo;
            // 🚨 수정할 부분: Title을 숫자로 파싱하여 정렬
            //AvailableRacks = new ObservableCollection<Rack>(
            //    racks.OrderBy(r => int.TryParse(r.Title, out int number) ? number : int.MaxValue) // 숫자로 파싱하여 정렬
            //);
            SelectCommand = new RelayCommand(ExecuteSelect, CanExecuteSelect);
            CancelCommand = new RelayCommand(ExecuteCancel);
        }

        private void ExecuteSelect(object parameter)
        {
            if (parameter is Window window) // parameter가 Window 객체인지 확인
            {
                DialogResult = true; // 뷰모델의 논리적 결과 설정
                window.DialogResult = true; // 팝업 윈도우의 DialogResult 속성 설정 (이것이 ShowDialog()의 반환 값 결정)
                window.Close(); // 팝업 윈도우 닫기
            }
        }

        private bool CanExecuteSelect(object parameter)
        {
            return SelectedRack != null;    // 랙이 선택되어야만 확인 버튼 활성화
        }

        private void ExecuteCancel(object parameter)
        {
            if (parameter is Window window) // parameter가 Window 객체인지 확인
            {
                DialogResult = false; // 뷰모델의 논리적 결과 설정
                window.DialogResult = false; // 팝업 윈도우의 DialogResult 속성 설정
                window.Close(); // 팝업 윈도우 닫기
            }
        }
    }
}