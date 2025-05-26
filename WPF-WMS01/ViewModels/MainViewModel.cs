// ViewModels/MainViewModel.cs
using System.Collections.ObjectModel;
using System.Windows.Input;
using WPF_WMS01.Commands; // ICommand 구현 클래스를 필요로 합니다.
using WPF_WMS01.Services;
using WPF_WMS01.Models;
using System.Threading.Tasks; // 비동기 작업용
using System;

namespace WPF_WMS01.ViewModels
{
    public class MainViewModel : ViewModelBase // INotifyPropertyChanged를 구현하는 ViewModelBase 사용
    {
        private readonly DatabaseService _databaseService;
        private ObservableCollection<RackViewModel> _rackList;

        public MainViewModel()
        {
            _databaseService = new DatabaseService(); // 실제 서비스 인스턴스화
            RackList = new ObservableCollection<RackViewModel>();
            LoadRacksCommand = new AsyncCommand(LoadRacks); // AsyncCommand는 비동기 ICommand 구현체입니다.

            // 애플리케이션 시작 시 자동으로 랙 데이터를 로드합니다.
            // Dispatcher를 사용하거나, Task.Run을 사용하여 UI 스레드를 블록하지 않도록 주의합니다.
            Task.Run(async () => await LoadRacks());
        }

        public ObservableCollection<RackViewModel> RackList
        {
            get => _rackList;
            set => SetProperty(ref _rackList, value); // ViewModelBase의 SetProperty 사용
        }

        public ICommand LoadRacksCommand { get; }

        private async Task LoadRacks()
        {
            try
            {
                // 실제 데이터 로딩 로직
                var racks = await _databaseService.GetRackStatesAsync();
                RackList.Clear();
                foreach (var rack in racks)
                {
                    RackList.Add(new RackViewModel(rack));
                }

                // 또는 더 간단하게:
                // RackList = new ObservableCollection<RackViewModel>(loadedRacks.Select(r => new RackViewModel(r)));
                // 하지만 이 방식은 Set 속성을 private으로 한 경우 UI가 업데이트되지 않을 수 있습니다.
                // PropertyChanged를 구현하고, set 접근자에 OnPropertyChanged()를 호출하는 것이 좋습니다.
                // 또는 Clear() 후 Add() 루프를 사용하는 것이 더 안전합니다.

                // 만약 위처럼 Add/Clear 루프를 사용한다면, RackList 속성은 아래처럼 Public set을 가져야 합니다.
                // public ObservableCollection<RackViewModel> RackList { get; set; }
                // 또는 INotifyPropertyChanged를 구현해야 합니다.
                // 현재는 LoadRacks()에서 RackList.Clear() 후 RackList.Add() 루프를 사용하는 것이 적절합니다.
            }
            catch (Exception ex)
            {
                // 오류 로깅 또는 메시지 박스 표시
                System.Diagnostics.Debug.WriteLine($"Error loading racks: {ex.Message}");
                // 사용자에게도 알림
                // MessageBox.Show($"데이터 로드 중 오류 발생: {ex.Message}");
            }
        }

        // 예시: 랙 상태를 업데이트하는 명령 (버튼 등에 바인딩 가능)
        public ICommand UpdateRackStateCommand => new RelayCommand<RackViewModel>(async (rackViewModel) =>
        {
            if (rackViewModel != null)
            {
                // 예시: 이미지 인덱스를 1씩 증가시키는 로직
                // 실제로는 사용자 입력이나 다른 비즈니스 로직에 따라 변경됩니다.
                int newImageIndex = (rackViewModel.ImageIndex + 1) % 6; // 0-5 사이 순환
                rackViewModel.ImageIndex = newImageIndex;

                // 데이터베이스에 변경 사항을 저장 (필요시)
                // await _databaseService.UpdateRackStateAsync(rackViewModel.Id, newImageIndex);
            }
        });
    }
}