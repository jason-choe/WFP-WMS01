// ViewModels/MainViewModel.cs
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Threading.Tasks; // 비동기 작업용
using System.Windows.Threading; // DispatcherTimer 사용을 위해 추가
using System;
using WPF_WMS01.Commands; // ICommand 구현 클래스를 필요로 합니다.
using WPF_WMS01.Services;
using WPF_WMS01.Models;
using System.Windows;
using System.Collections.Generic;
using System.Linq;

namespace WPF_WMS01.ViewModels
{
    public class MainViewModel : ViewModelBase // INotifyPropertyChanged를 구현하는 ViewModelBase 사용
    {
        private readonly DatabaseService _databaseService;
        private ObservableCollection<RackViewModel> _rackList;
        private DispatcherTimer _refreshTimer; // 타이머 선언

        public MainViewModel()
        {
            _databaseService = new DatabaseService(); // 실제 서비스 인스턴스화
            RackList = new ObservableCollection<RackViewModel>();
            LoadRacksCommand = new AsyncCommand(LoadRacks); // AsyncCommand는 비동기 ICommand 구현체입니다.

            // 애플리케이션 시작 시 자동으로 랙 데이터를 로드합니다.
            // Dispatcher를 사용하거나, Task.Run을 사용하여 UI 스레드를 블록하지 않도록 주의합니다.
            //Task.Run(async () => await LoadRacks());

            // 애플리케이션 시작 시 데이터 로드
            _ = LoadRacks(); // 비동기 메서드를 호출하지만, 결과를 기다리지 않음

            // 타이머 설정 및 시작
            SetupRefreshTimer();
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

                //RackList.Clear();
                //foreach (var rack in racks)
                //{
                //    RackList.Add(new RackViewModel(rack));
                //}

                // 또는 더 간단하게:
                // RackList = new ObservableCollection<RackViewModel>(loadedRacks.Select(r => new RackViewModel(r)));
                // 하지만 이 방식은 Set 속성을 private으로 한 경우 UI가 업데이트되지 않을 수 있습니다.
                // PropertyChanged를 구현하고, set 접근자에 OnPropertyChanged()를 호출하는 것이 좋습니다.
                // 또는 Clear() 후 Add() 루프를 사용하는 것이 더 안전합니다.

                // 만약 위처럼 Add/Clear 루프를 사용한다면, RackList 속성은 아래처럼 Public set을 가져야 합니다.
                // public ObservableCollection<RackViewModel> RackList { get; set; }
                // 또는 INotifyPropertyChanged를 구현해야 합니다.
                // 현재는 LoadRacks()에서 RackList.Clear() 후 RackList.Add() 루프를 사용하는 것이 적절합니다.
                 // UI 스레드에서 ObservableCollection 업데이트 (DispatcherTimer 사용 시 불필요하지만,
                // 다른 스레드 타이머 사용 시 필요함)

                App.Current.Dispatcher.Invoke(() =>
                {
                    // 기존 데이터와 새 데이터를 비교하여 변경된 부분만 업데이트
                    // 불필요한 UI 깜빡임을 줄일 수 있습니다.
                    UpdateRackList(racks);
                });
           }
            catch (Exception ex)
            {
                // 오류 로깅 또는 메시지 박스 표시
                System.Diagnostics.Debug.WriteLine($"Error loading racks: {ex.Message}");
                // 사용자에게도 알림
                MessageBox.Show($"데이터 로드 중 오류 발생: {ex.Message}");
            }
        }

        // 예시: 랙 상태를 업데이트하는 명령 (버튼 등에 바인딩 가능)
        public ICommand UpdateRackStateCommand => new RelayCommand<RackViewModel>(async (rackViewModel) =>
        {
            if (rackViewModel != null)
            {
                // RackViewModel의 ImageIndex는 읽기 전용이므로,
                // 내부 RackModel의 RackType과 BulletType을 변경해야 합니다.
                // 여기서는 예시로 ImageIndex를 통해 RackType과 BulletType을 역산하여 업데이트합니다.
                // 실제로는 새로운 RackType과 BulletType 값을 직접 설정해야 합니다.

                // 예시: 이미지 인덱스를 1씩 증가시키는 로직 (ImageIndex를 기준으로 RackType, BulletType 변경)
                int newImageIndex = (rackViewModel.ImageIndex + 1) % 6; // 0-5 사이 순환

                // 새 ImageIndex로부터 RackType과 BulletType을 역산하여 Model에 업데이트합니다.
                // 예를 들어, BulletType을 (newImageIndex % 3)으로, RackType을 (newImageIndex / 3)으로 가정
                rackViewModel.RackModel.BulletType = newImageIndex % 3; // BulletType은 0, 1, 2
                rackViewModel.RackModel.RackType = newImageIndex / 3;   // RackType은 0, 1

                // 데이터베이스에 변경 사항을 저장 (필요시, RackType과 BulletType 저장)
                // await _databaseService.UpdateRackStateAsync(rackViewModel.Id, rackViewModel.RackModel.RackType, rackViewModel.RackModel.BulletType);
            }
        });

        // RackList 업데이트 로직
        private void UpdateRackList(List<Rack> newRacks)
        {
            // 간단한 방법: 모두 지우고 다시 추가 (UI가 깜빡일 수 있음)
            // RackList.Clear();
            // foreach (var rack in newRacks)
            // {
            //     RackList.Add(new RackViewModel(rack));
            // }

            // 더 효율적인 방법: 변경된 항목만 업데이트
            // 1. 기존 RackList에 없는 새 랙 추가
            foreach (var newRack in newRacks)
            {
                var existingRackVm = RackList.FirstOrDefault(r => r.Id.Equals(newRack.Id));
                if (existingRackVm == null)
                {
                    RackList.Add(new RackViewModel(newRack));
                }
                else
                {
                    // 이미지 인덱스나 가시성 등 속성이 변경되었는지 확인하고 업데이트

                    // 여기에서 RackViewModel의 속성을 직접 할당하는 대신,
                    // RackViewModel 내부의 RackModel 속성을 업데이트해야 합니다.
                    if (existingRackVm.RackModel.RackType != newRack.RackType)
                    {
                        existingRackVm.RackModel.RackType = newRack.RackType;
                    }
                    if (existingRackVm.RackModel.BulletType != newRack.BulletType)
                    {
                        existingRackVm.RackModel.BulletType = newRack.BulletType;
                    }
                    // ImageIndex는 RackModel의 RackType/BulletType 변경 시 자동으로 업데이트됩니다.

                    if (existingRackVm.RackModel.IsVisible != newRack.IsVisible)
                    {
                        existingRackVm.RackModel.IsVisible = newRack.IsVisible;
                    }
                    if (existingRackVm.RackModel.IsLocked != newRack.IsLocked)
                    {
                        existingRackVm.RackModel.IsLocked = newRack.IsLocked;
                    }
                    if (existingRackVm.RackModel.Title != newRack.Title)
                    {
                        existingRackVm.RackModel.Title = newRack.Title;
                    }
                    // Title 등 다른 속성도 필요하면 업데이트
                }
            }

            // 2. 새 랙 목록에 없는 기존 랙 제거 (데이터베이스에서 삭제된 경우)
            for (int i = RackList.Count - 1; i >= 0; i--)
            {
                var rackVm = RackList[i];
                if (!newRacks.Any(r => r.Id.Equals(rackVm.Id)))
                {
                    RackList.RemoveAt(i);
                }
            }
        }

        private void SetupRefreshTimer()
        {
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(1); // 1초마다 업데이트 (원하는 간격으로 설정)
            _refreshTimer.Tick += RefreshTimer_Tick;
            _refreshTimer.Start();
        }

        private async void RefreshTimer_Tick(object sender, EventArgs e)
        {
            await LoadRacks(); // 타이머 틱마다 데이터를 다시 로드
        }

        // ViewModel이 소멸될 때 타이머를 멈추는 것이 좋습니다. (Window.Closed 이벤트 등에서 호출)
        public void Dispose()
        {
            _refreshTimer?.Stop();
            _refreshTimer.Tick -= RefreshTimer_Tick;
        }

    }
}