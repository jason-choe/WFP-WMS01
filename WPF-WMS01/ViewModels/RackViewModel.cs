// ViewModels/RackViewModel.cs
using WPF_WMS01.Models; // YourAppName을 실제 프로젝트 이름으로 변경하세요.
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows;
using WPF_WMS01.Commands;

namespace WPF_WMS01.ViewModels
{
    public class RackViewModel : INotifyPropertyChanged
    {
        private readonly Rack _rack;
        public ICommand RackClickCommand { get; private set; }

        public RackViewModel(Rack rack)
        {
            _rack = rack;
            _rack.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName); // Model 변경 시 ViewModel도 업데이트
            RackClickCommand = new RelayCommand(OnRackClicked, CanClickRack);
        }

        public string Id => _rack.Id;
        public string Title => _rack.Title;

        public int ImageIndex
        {
            get => _rack.ImageIndex;
            set => _rack.ImageIndex = value; // Model의 ImageIndex를 통해 값을 변경
        }

        public bool IsVisible
        {
            get => _rack.IsVisible;
            set => _rack.IsVisible = value; // Model의 IsVisible을 통해 값을 변경
        }
        public bool IsLocked
        {
            get => _rack.IsLocked;
            set => _rack.IsLocked = value; // Model의 IsLocked 통해 값을 변경
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnRackClicked()
        {
            // 클릭 시 팝업 로직 호출
            // 예를 들어, MainViewModel에 팝업을 띄우라는 메시지를 보낼 수 있습니다.
            // 또는 간단한 Dialog를 직접 띄울 수도 있습니다.

            // 옵션 1: 간단한 정보 메시지 박스
            MessageBox.Show($"랙 '{Title}'이(가) 클릭되었습니다. 상태: {(IsLocked ? "잠김" : "잠금 해제")}", "랙 정보", MessageBoxButton.OK, MessageBoxImage.Information);
            // 옵션 2: 새로운 팝업 Window 띄우기 (별도의 팝업 View와 ViewModel 필요)
            // 이 방식은 RackViewModel이 View에 대한 직접적인 지식을 갖게 되므로 MVVM 원칙에 엄격하게 부합하지 않을 수 있습니다.
            // 더 나은 방법은 MainViewModel을 통해 팝업을 관리하는 것입니다.
            //var popupViewModel = new RackDetailPopupViewModel(this); // 현재 RackViewModel을 팝업 ViewModel에 전달
            //var popupView = new RackDetailPopupView { DataContext = popupViewModel };
            //popupView.ShowDialog(); // ShowDialog()는 모달로 띄웁니다.
        }

        private bool CanClickRack()
        {
            // 조건부 클릭 활성화 로직
            // 예시: 특정 상태일 때만 클릭 가능하도록
            return (!IsLocked && Title != "WRAP"); // 'locked' 상태가 아닐 때만 클릭 가능하도록 설정
        }
    }
}