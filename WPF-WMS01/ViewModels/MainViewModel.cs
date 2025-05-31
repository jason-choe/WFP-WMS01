// ViewModels/MainViewModel.cs
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Threading.Tasks; // 비동기 작업용
using System.Windows.Threading; // DispatcherTimer 사용을 위해 추가
using System;
using WPF_WMS01.Commands; // ICommand 구현 클래스를 필요로 합니다.
using WPF_WMS01.Services;
using WPF_WMS01.Models;
using WPF_WMS01.ViewModels.Popups;
using WPF_WMS01.Views.Popups;
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

            // 애플리케이션 시작 시 데이터 로드
            _ = LoadRacks(); // 비동기 메서드를 호출하지만, 결과를 기다리지 않음

            // --- Grid>Row="1"에 새로 추가된 명령 초기화 ---
            InboundProductCommand = new RelayCommand(ExecuteInboundProduct, CanExecuteInboundProduct);
            Checkout223ProductCommand = new RelayCommand(ExecuteCheckout223Product, CanExecuteCheckout223Product);
            Checkout308ProductCommand = new RelayCommand(ExecuteCheckout308Product, CanExecuteCheckout308Product);

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
                MessageBox.Show($"데이터 로드 중 오류 발생: {ex.Message}");
            }
        }

        // 예시: 랙 상태를 업데이트하는 명령 (버튼 등에 바인딩 가능)
        public ICommand UpdateRackStateCommand => new RelayCommand(async (parameter) => // 👈 <RackViewModel> 제거
        {
            // parameter를 RackViewModel로 캐스팅해야 합니다.
            if (parameter is RackViewModel rackViewModel)
            {
                // RackViewModel의 ImageIndex는 읽기 전용이므로,
                // 내부 RackModel의 RackType과 BulletType을 변경해야 합니다.
                // 여기서는 예시로 ImageIndex를 통해 RackType과 BulletType을 역산하여 업데이트합니다.
                // 실제로는 새로운 RackType과 BulletType 값을 직접 설정해야 합니다.

                // 예시: 이미지 인덱스를 1씩 증가시키는 로직 (ImageIndex를 기준으로 RackType, BulletType 변경)
                int newImageIndex = (rackViewModel.ImageIndex + 1) % 6; // 0-5 사이 순환

                // RackViewModel의 RackModel에 직접 접근하여 RackType과 BulletType을 변경
                // ImageIndex = RackType * 3 + BulletType;
                // BulletType = ImageIndex % 3;
                // RackType = ImageIndex / 3;
                rackViewModel.RackModel.BulletType = newImageIndex % 3; // BulletType은 0, 1, 2
                rackViewModel.RackModel.RackType = newImageIndex / 3;    // RackType은 0, 1

                // 데이터베이스에 변경 사항을 저장 (필요시, RackType과 BulletType 저장)
                // 현재는 이 UpdateRackStateCommand가 RackViewModel의 OnRackClicked와는 별개로 존재합니다.
                // 따라서 여기에 데이터베이스 업데이트 로직을 추가하려면 _databaseService 인스턴스가 필요합니다.
                // MainViewModel이 DatabaseService를 가지고 있다면 (아마 가지고 있을 것), 사용 가능합니다.
                await _databaseService.UpdateRackStateAsync(
                    rackViewModel.Id,
                    rackViewModel.RackModel.RackType,
                    rackViewModel.RackModel.BulletType,
                    rackViewModel.RackModel.IsLocked // IsLocked도 함께 전달
                );
            }
        });

        // RackList 업데이트 로직
        private void UpdateRackList(List<Rack> newRacks)
        {
            // 최적화된 ObservableCollection 업데이트 (제거 -> 추가 대신 갱신)
            // 기존 RackList에 있는 항목 중 newRacks에 없는 항목을 제거하고,
            // newRacks에 있는 항목 중 RackList에 없는 항목을 추가하며,
            // 둘 다 있는 항목은 UpdateProperties를 호출합니다.

            // 제거할 항목을 먼저 찾습니다.
            var rvmIdsToRemove = RackList.Select(rvm => rvm.Id)
                                         .Except(newRacks.Select(nr => nr.Id))
                                         .ToList();

            foreach (var rvmId in rvmIdsToRemove)
            {
                var rvmToRemove = RackList.FirstOrDefault(rvm => rvm.Id == rvmId);
                if (rvmToRemove != null)
                {
                    RackList.Remove(rvmToRemove);
                }
            }

            // 추가 또는 업데이트할 항목 처리
            foreach (var newRackData in newRacks)
            {
                var existingRackVm = RackList.FirstOrDefault(rvm => rvm.Id == newRackData.Id);

                if (existingRackVm == null)
                {
                    // 새 랙을 추가
                    RackList.Add(new RackViewModel(newRackData, _databaseService));
                }
                else
                {
                    // 기존 RackViewModel을 찾았으면, SetRackModel 메서드를 호출하여 RackModel 객체를 교체합니다.
                    // 이 메서드는 RackViewModel 내부에서 이전 구독 해제 및 새 구독을 처리합니다.
                    existingRackVm.SetRackModel(newRackData); // <-- 이 부분이 핵심 변경!
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

        // Grid>Row="1"에 새로 추가된 속성 및 명령 ---

        private string _inputStringForButton;
        public string InputStringForButton
        {
            get => _inputStringForButton;
            set
            {
                _inputStringForButton = value;
                OnPropertyChanged();
                // TextBlock 내용이 변경될 때마다 버튼의 활성화 여부를 다시 평가
                ((RelayCommand)InboundProductCommand).RaiseCanExecuteChanged();
            }
        }

        public ICommand InboundProductCommand { get; private set; } // '입고' 버튼 명령
        public ICommand Checkout223ProductCommand { get; private set; } // '232 출고' 버튼 명령
        public ICommand Checkout308ProductCommand { get; private set; } // '308 출고' 버튼 명령

        // Grid>Row="1"에 새로 추가된 명령 구현 ---

        // '입고' 버튼
        private async void ExecuteInboundProduct(object parameter)
        {
            // 이 시점에서는 CanExecute에서 이미 빈 랙 존재 여부를 확인했으나, 한 번 더 확인하여 안전성을 높입니다.
            var emptyRacks = RackList?.Where(r => r.ImageIndex == 0 && r.IsVisible).ToList();

            if (emptyRacks == null || !emptyRacks.Any())
            {
                MessageBox.Show("현재 입고 가능한 빈 랙이 없습니다.", "입고 불가", MessageBoxButton.OK, MessageBoxImage.Warning);
                // CanExecute에서 이미 막았지만, 혹시 모를 상황 대비 (경쟁 조건 등)
                return;
            }

            var selectEmptyRackViewModel = new SelectEmptyRackPopupViewModel(emptyRacks.Select(r => r.RackModel).ToList());
            var selectEmptyRackView = new SelectEmptyRackPopupView { DataContext = selectEmptyRackViewModel };
            selectEmptyRackView.Title = $"{InputStringForButton} 제품 입고할 랙 선택";

            if (selectEmptyRackView.ShowDialog() == true && selectEmptyRackViewModel.DialogResult == true)
            {
                var selectedRack = selectEmptyRackViewModel.SelectedRack;
                if (selectedRack != null)
                {
                    MessageBox.Show($"랙 {selectedRack.Title} 에 {InputStringForButton} 제품을 입고합니다.", "입고 확인", MessageBoxButton.OK, MessageBoxImage.Information);

                    int newBulletType = 0;
                    if (InputStringForButton.Contains("223"))
                    {
                        newBulletType = 1; // '223' 포함 시 BulletType 1
                    }
                    else if (InputStringForButton.Contains("308"))
                    {
                        newBulletType = 2; // '308' 포함 시 BulletType 2
                    }
                    else
                    {
                        // 예외 처리 또는 기본값 설정 (이 경우는 CanExecute에서 걸러지므로 거의 발생하지 않음)
                        MessageBox.Show("입력된 문자열에서 유효한 제품 유형을 찾을 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // RackType은 1 (제품 보관 랙)로 설정하고 BulletType을 입력된 문자열에 따라 설정
                    await _databaseService.UpdateRackStateAsync(
                        selectedRack.Id,
                        selectedRack.RackType, // RackType을 기존 값(0)으로 유지
                        newBulletType,
                        false // 입고 후 잠금 해제 상태로
                    );
                    MessageBox.Show($"랙 {selectedRack.Title} 에 제품 입고 완료.", "입고 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                    InputStringForButton = string.Empty; // 입고 후 TextBox 내용 초기화
                }
            }
            else
            {
                MessageBox.Show("입고 작업이 취소되었습니다.", "취소", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private bool CanExecuteInboundProduct(object parameter)
        {
            // 1) InputStringForButton이 '223' 또는 '308'을 포함하는지 확인
            bool inputContainsValidProduct = !string.IsNullOrWhiteSpace(_inputStringForButton) &&
                                             (_inputStringForButton.Contains("223") || _inputStringForButton.Contains("308"));

            // 2) ImageIndex가 0인 랙 (빈 랙)이 존재하는지 확인
            bool emptyAndVisibleRackExists = RackList?.Any(r => (r.ImageIndex == 0 && r.IsVisible)) == true;

            // 두 조건을 모두 만족할 때만 true 반환
            return inputContainsValidProduct && emptyAndVisibleRackExists;
        }

        // '223 제품 출고' 버튼
        private void ExecuteCheckout223Product(object parameter)
        {
            // RackList를 사용하여 랙 찾기
            var targetRack = RackList?.FirstOrDefault(r => r.RackType == 1 && r.BulletType == 1 && !r.IsLocked);

            if (targetRack != null)
            {
                MessageBox.Show($"223 제품이 있는 랙 {targetRack.Title} 에서 출고를 시작합니다.", "223 출고", MessageBoxButton.OK, MessageBoxImage.Information);
                // 여기에 실제 223 제품 출고 로직 (예: RackViewModel.HandleRackCheckout 호출 등)을 추가
                // 현재 MainViewModel에서는 RackViewModel의 내부 메서드를 직접 호출할 수 없으므로,
                // MainViewModel에서 RackViewModel의 상태를 변경하는 메서드를 호출하거나,
                // 공통 출고 로직을 MainViewModel에 구현해야 합니다.
                // 여기서는 간단히 MessageBox로 대체합니다.

                // 실제 구현 시, 해당 랙의 OnRackClicked 로직을 재사용하는 것이 좋습니다.
                // 예를 들어, RackViewModel의 '출고' 기능을 트리거하는 새로운 Public 메서드를 RackViewModel에 추가하고 MainViewModel에서 호출
                // 또는 RackViewModel의 HandleRackCheckout이 직접 실행될 수 있도록 구조 변경.
                // 편의상 현재는 단순히 메시지 박스만 띄우겠습니다.
                // 예: targetRack.PerformCheckout(); // RackViewModel에 새로운 메서드 필요
            }
            else
            {
                MessageBox.Show("출고할 223 제품이 있는 랙이 없습니다.", "223 출고 불가", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private bool CanExecuteCheckout223Product(object parameter)
        {
            return RackList?.Any(r => r.RackType == 1 && r.BulletType == 1 && !r.IsLocked) == true;
        }

        // '308 제품 출고' 버튼
        private void ExecuteCheckout308Product(object parameter)
        {
            // RackList를 사용하여 랙 찾기
            var targetRack = RackList?.FirstOrDefault(r => r.RackType == 1 && r.BulletType == 2 && !r.IsLocked);

            if (targetRack != null)
            {
                MessageBox.Show($"308 제품이 있는 랙 {targetRack.Title} 에서 출고를 시작합니다.", "308 출고", MessageBoxButton.OK, MessageBoxImage.Information);
                // 여기에 실제 308 제품 출고 로직 추가
            }
            else
            {
                MessageBox.Show("출고할 308 제품이 있는 랙이 없습니다.", "308 출고 불가", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private bool CanExecuteCheckout308Product(object parameter)
        {
            // RackList를 사용하여 랙 확인
            return RackList?.Any(r => r.RackType == 1 && r.BulletType == 2 && !r.IsLocked) == true;
        }

        // 모든 출고 관련 버튼의 CanExecute 상태를 갱신
        private void RaiseAllCheckoutCanExecuteChanged()
        {
            ((RelayCommand)Checkout223ProductCommand).RaiseCanExecuteChanged();
            ((RelayCommand)Checkout308ProductCommand).RaiseCanExecuteChanged();
        }

    }
}