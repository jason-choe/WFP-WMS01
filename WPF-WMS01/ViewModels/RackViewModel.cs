// ViewModels/RackViewModel.cs
using WPF_WMS01.Models; // YourAppName을 실제 프로젝트 이름으로 변경하세요.
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows;
using WPF_WMS01.Commands;
// 필요에 따라 팝업 View/ViewModel 네임스페이스 추가:
// using WPF_WMS01.Views.Popups; 
// using WPF_WMS01.ViewModels.Popups;

namespace WPF_WMS01.ViewModels
{
    public class RackViewModel : INotifyPropertyChanged
    {
        private readonly Rack _rack;
        public ICommand RackClickCommand { get; private set; }

        public Rack RackModel { get; private set; } // 모델 인스턴스

        public RackViewModel(Rack rack)
        {
            RackModel = rack;
            // 모델의 PropertyChanged 이벤트를 구독하여 ViewModel 속성 업데이트
            RackModel.PropertyChanged += (sender, e) =>
            {
                // ImageIndex가 모델에서 계산되므로, ViewModel에서 별도로 계산할 필요 없음
                // ImageIndex 변경 시 UI가 업데이트되도록 다시 OnPropertyChanged 호출
                if (e.PropertyName == nameof(RackModel.ImageIndex))
                {
                    OnPropertyChanged(nameof(ImageIndex)); // ViewModel의 ImageIndex 속성 업데이트
                }
                // 다른 속성도 필요하면 추가
                if (e.PropertyName == nameof(RackModel.IsLocked))
                {
                    OnPropertyChanged(nameof(IsLocked));
                }
                if (e.PropertyName == nameof(RackModel.IsVisible))
                {
                    OnPropertyChanged(nameof(IsVisible));
                }
                if (e.PropertyName == nameof(RackModel.Title))
                {
                    OnPropertyChanged(nameof(Title));
                }
            };

            // RackClickCommand 초기화
            // CommandParameter로 RackViewModel 자신을 넘기기 때문에 RelayCommand<object> 사용
            RackClickCommand = new RelayCommand<object>(OnRackClicked, CanClickRack);
        }

        // 모델 속성들을 ViewModel에서 노출
        public int Id => RackModel.Id;
        public string Title => RackModel.Title;
        public bool IsLocked => RackModel.IsLocked;
        public bool IsVisible => RackModel.IsVisible;
        // ImageIndex는 모델에서 계산된 값을 직접 가져옴
        public int ImageIndex => RackModel.ImageIndex;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnRackClicked(object parameter)
        {
            // CommandParameter로 넘어온 RackViewModel을 사용 (만약 필요하다면)
            var clickedRackViewModel = parameter as RackViewModel;
            if (clickedRackViewModel == null) return;

            // ImageIndex 값에 따라 다른 팝업 창 띄우기
            switch (ImageIndex)
            {
                case 0:
                case 3:
                    // 1) ImageIndex가 0 또는 3일 때 띄울 팝업
                    MessageBox.Show($"랙 {Title} (ImageIndex: {ImageIndex}): 첫 번째 유형의 팝업!", "랙 상세", MessageBoxButton.OK, MessageBoxImage.Information);
                    // 실제 구현: new Type1PopupView { DataContext = new Type1PopupViewModel(clickedRackViewModel) }.ShowDialog();
                    break;
                case 1:
                case 2:
                    // 2) ImageIndex가 1 또는 2일 때 띄울 팝업
                    MessageBox.Show($"랙 {Title} (ImageIndex: {ImageIndex}): 두 번째 유형의 팝업!", "랙 상세", MessageBoxButton.OK, MessageBoxImage.Warning);
                    // 실제 구현: new Type2PopupView { DataContext = new Type2PopupViewModel(clickedRackViewModel) }.ShowDialog();
                    break;
                case 4:
                case 5:
                    // 3) ImageIndex가 4 또는 5일 때 띄울 팝업
                    MessageBox.Show($"랙 {Title} (ImageIndex: {ImageIndex}): 세 번째 유형의 팝업!", "랙 상세", MessageBoxButton.OK, MessageBoxImage.Error);
                    // 실제 구현: new Type3PopupView { DataContext = new Type3PopupViewModel(clickedRackViewModel) }.ShowDialog();
                    break;
                default:
                    // 그 외의 경우
                    MessageBox.Show($"랙 {Title} (ImageIndex: {ImageIndex}): 기타 유형의 팝업!", "랙 상세", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
            }
        }

        private bool CanClickRack(object parameter)
        {
            // Title 이 "WRAP"이 아니고
            // Rack이 잠겨있지 않을 때만 클릭 가능
            // IsLocked는 RackModel.IsLocked에서 가져옴
            return (!IsLocked && Title != "WRAP"); // 'locked' 상태가 아닐 때만 클릭 가능하도록 설정
        }

        private bool CanClickRack()
        {
            // 조건부 클릭 활성화 로직
            // 예시: 특정 상태일 때만 클릭 가능하도록
            return (!IsLocked && Title != "WRAP"); // 'locked' 상태가 아닐 때만 클릭 가능하도록 설정
        }
    }
}