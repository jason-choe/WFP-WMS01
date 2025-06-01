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
using System.Configuration; // App.config 읽기를 위해 추가
using System.Threading.Tasks; // Task.Run, Task.Delay 사용을 위해 추가

namespace WPF_WMS01.ViewModels
{
    public class MainViewModel : ViewModelBase // INotifyPropertyChanged를 구현하는 ViewModelBase 사용
    {
        private readonly DatabaseService _databaseService;
        private ObservableCollection<RackViewModel> _rackList;
        private DispatcherTimer _refreshTimer; // 타이머 선언
        public readonly string _waitRackTitle; // App.config에서 읽어올 WAIT 랙 타이틀


        public MainViewModel()
        {
            _databaseService = new DatabaseService(); // 실제 서비스 인스턴스화
            // App.config에서 WAIT 랙 타이틀 읽기
            _waitRackTitle = ConfigurationManager.AppSettings["WaitRackTitle"] ?? "WAIT";

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
                    RackList.Add(new RackViewModel(newRackData, _databaseService, this));
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
                    var targetRackVm = RackList?.FirstOrDefault(r => r.Id == selectedRack.Id);
                    var waitRackVm = RackList?.FirstOrDefault(r => r.Title == _waitRackTitle);

                    if (targetRackVm == null) return;
                    // WAIT 랙이 없으면 잠금 처리할 대상이 없으므로 null 체크
                    // 만약 WAIT 랙이 필수라면 여기서 오류 메시지 표시
                    MessageBox.Show($"랙 {selectedRack.Title} 에 {InputStringForButton} 제품 입고 작업을 시작합니다. 10초 대기...", "입고 작업 시작", MessageBoxButton.OK, MessageBoxImage.Information);

                    // **타겟 랙과 WAIT 랙 잠금**
                    await _databaseService.UpdateRackStateAsync(targetRackVm.Id, targetRackVm.RackType, targetRackVm.BulletType, true);
                    Application.Current.Dispatcher.Invoke(() => targetRackVm.IsLocked = true);

                    if (waitRackVm != null)
                    {
                        await _databaseService.UpdateRackStateAsync(waitRackVm.Id, waitRackVm.RackType, waitRackVm.BulletType, true);
                        Application.Current.Dispatcher.Invoke(() => waitRackVm.IsLocked = true);
                    }

                    await Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10)); // 10초 지연 시뮬레이션

                        try
                        {
                            int newBulletType = 0;
                            if (InputStringForButton.Contains("223"))
                            {
                                newBulletType = 1;
                            }
                            else if (InputStringForButton.Contains("308"))
                            {
                                newBulletType = 2;
                            }
                            else
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    MessageBox.Show("입력된 문자열에서 유효한 제품 유형을 찾을 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                                });
                                // 오류 발생 시 타겟 랙과 WAIT 랙 모두 잠금 해제
                                await _databaseService.UpdateRackStateAsync(targetRackVm.Id, targetRackVm.RackType, targetRackVm.BulletType, false);
                                Application.Current.Dispatcher.Invoke(() => targetRackVm.IsLocked = false);
                                if (waitRackVm != null)
                                {
                                    await _databaseService.UpdateRackStateAsync(waitRackVm.Id, waitRackVm.RackType, waitRackVm.BulletType, false);
                                    Application.Current.Dispatcher.Invoke(() => waitRackVm.IsLocked = false);
                                }
                                return;
                            }

                            // **입고: 타겟 랙만 잠금 해제 (IsLocked = false) 및 BulletType 변경**
                            await _databaseService.UpdateRackStateAsync(
                                selectedRack.Id,
                                selectedRack.RackType,
                                newBulletType,
                                false // 입고 후 타겟 랙만 잠금 해제
                            );

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                targetRackVm.BulletType = newBulletType;
                                targetRackVm.IsLocked = false; // UI 업데이트 (잠금 해제)

                                if (waitRackVm != null)
                                {
                                    // WAIT 랙 잠금 해제 (BulletType은 CanExecute에서 이미 관리됨)
                                    waitRackVm.IsLocked = false;
                                    // WAIT 랙의 BulletType은 CanExecuteInboundProduct에서 이미 관리되므로
                                    // 여기서 BulletType을 다시 설정할 필요는 없습니다.
                                }

                                // WAIT 랙은 계속 잠금 상태를 유지 (CanExecute에서 제어됨)
                                MessageBox.Show($"랙 {selectedRack.Title} 에 제품 입고 완료.", "입고 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                                InputStringForButton = string.Empty;
                            });
                        }
                        catch (Exception ex)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"입고 작업 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                            });
                            // 예외 발생 시 타겟 랙과 WAIT 랙 잠금 해제
                            await _databaseService.UpdateRackStateAsync(targetRackVm.Id, targetRackVm.RackType, targetRackVm.BulletType, false);
                            Application.Current.Dispatcher.Invoke(() => targetRackVm.IsLocked = false);
                            if (waitRackVm != null)
                            {
                                await _databaseService.UpdateRackStateAsync(waitRackVm.Id, waitRackVm.RackType, waitRackVm.BulletType, false);
                                Application.Current.Dispatcher.Invoke(() => waitRackVm.IsLocked = false);
                            }
                        }
                    });
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

            // 2) ImageIndex가 0 (빈 랙)이고 IsVisible이 True인 랙이 존재하는지 확인
            bool emptyAndVisibleRackExists = RackList?.Any(r => (r.ImageIndex == 0 && r.IsVisible)) == true;

            // 특정 Title을 갖는 WAIT 랙을 찾습니다.
            var waitRackVm = RackList?.FirstOrDefault(r => r.Title == _waitRackTitle);

            // 추가된 조건: WAIT 랙이 잠겨 있지 않아야 함
            bool waitRackNotLocked = (waitRackVm?.IsLocked == false) || (waitRackVm == null); // WAIT 랙이 없거나 잠겨있지 않아야 함

            // 중요: CanExecute에서 데이터 변경은 MVVM 패턴에 위배될 수 있습니다.
            // 하지만 요청에 따라 여기에 로직을 추가합니다.
            if (waitRackVm != null)
            {
                int newBulletTypeForWaitRack = 0; // 기본은 0 (비활성화 상태)

                if (inputContainsValidProduct && emptyAndVisibleRackExists)
                {
                    // 활성화 조건 충족 시
                    if (_inputStringForButton.Contains("223"))
                    {
                        newBulletTypeForWaitRack = 1;
                    }
                    else if (_inputStringForButton.Contains("308"))
                    {
                        newBulletTypeForWaitRack = 2;
                    }
                }

                // WAIT 랙의 BulletType을 업데이트 (비동기 처리)
                // UI 스레드에서 직접 데이터베이스 호출을 피하기 위해 Task.Run 사용
                Task.Run(async () =>
                {
                    // 실제 DB 업데이트는 비동기로 이루어지므로,
                    // CanExecute가 반환되기 전에 완료되지 않을 수 있습니다.
                    // UI 스레드에 RackViewModel의 속성을 업데이트하도록 Dispatcher.Invoke 사용
                    await _databaseService.UpdateRackStateAsync(
                        waitRackVm.Id,
                        waitRackVm.RackType,
                        newBulletTypeForWaitRack,
                        waitRackVm.IsLocked // IsLocked는 변경하지 않음
                    );

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        // UI 업데이트를 위해 RackViewModel의 속성도 수동으로 업데이트합니다.
                        // 이 부분은 _databaseService.UpdateRackStateAsync가 자동으로 갱신하는 로직이 없다고 가정할 때 필요합니다.
                        // 만약 DB 업데이트 후 LoadRacks를 다시 호출하는 등의 로직이 있다면 이 부분은 생략 가능합니다.
                        waitRackVm.BulletType = newBulletTypeForWaitRack;
                    });
                });
            }

            // 두 조건을 모두 만족할 때만 true 반환
            return inputContainsValidProduct && emptyAndVisibleRackExists && waitRackNotLocked;

        }

        // '223 제품 출고' 버튼 수정
        private async void ExecuteCheckout223Product(object parameter)
        {
            // 출고 가능한 223 제품 랙 목록 가져오기 (잠겨있지 않은 랙만)
            var availableRacksForCheckout = RackList?.Where(r => r.RackType == 1 && r.BulletType == 1 && !r.IsLocked).Select(rvm => rvm.RackModel).ToList();

            if (availableRacksForCheckout == null || !availableRacksForCheckout.Any())
            {
                MessageBox.Show("출고할 223 제품이 있는 랙이 없습니다.", "223 출고 불가", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 팝업 ViewModel 인스턴스 생성 및 랙 리스트 전달
            var selectCheckoutRackViewModel = new SelectCheckoutRackPopupViewModel(availableRacksForCheckout);
            var selectCheckoutRackView = new SelectCheckoutRackPopupView { DataContext = selectCheckoutRackViewModel };
            selectCheckoutRackView.Title = "223 제품 출고할 랙 선택";

            if (selectCheckoutRackView.ShowDialog() == true && selectCheckoutRackViewModel.DialogResult == true)
            {
                // 사용자가 선택한 랙 목록 가져오기
                var selectedRacksForCheckout = selectCheckoutRackViewModel.GetSelectedRacks();

                if (selectedRacksForCheckout == null || !selectedRacksForCheckout.Any())
                {
                    MessageBox.Show("선택된 랙이 없습니다.", "출고 취소", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                MessageBox.Show($"{selectedRacksForCheckout.Count}개의 223 제품 랙 출고 작업을 시작합니다.", "출고 작업 시작", MessageBoxButton.OK, MessageBoxImage.Information);

                // **출고: 모든 선택된 랙을 동시에 잠금 (IsLocked = true)**
                var targetRackVmsToLock = selectedRacksForCheckout.Select(r => RackList?.FirstOrDefault(rvm => rvm.Id == r.Id))
                                                                   .Where(rvm => rvm != null)
                                                                   .ToList();
                foreach (var rvm in targetRackVmsToLock)
                {
                    await _databaseService.UpdateRackStateAsync(rvm.Id, rvm.RackType, rvm.BulletType, true);
                    Application.Current.Dispatcher.Invoke(() => rvm.IsLocked = true);
                }

                // 새로운 스레드에서 순차적으로 각 랙 출고 처리
                await Task.Run(async () =>
                {
                    foreach (var rackModelToCheckout in selectedRacksForCheckout)
                    {
                        var targetRackVm = RackList?.FirstOrDefault(r => r.Id == rackModelToCheckout.Id);
                        if (targetRackVm == null) continue; // 뷰모델이 없으면 다음 랙으로

                        try
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"랙 {targetRackVm.Title} 출고 처리 중... (10초 대기)", "출고 진행", MessageBoxButton.OK, MessageBoxImage.Information);
                            });

                            await Task.Delay(TimeSpan.FromSeconds(10)); // 10초 지연

                            // **출고: 각 랙이 개별적으로 잠금 해제 (IsLocked = false) 및 BulletType 변경**
                            await _databaseService.UpdateRackStateAsync(targetRackVm.Id, targetRackVm.RackType, 0, false);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                targetRackVm.BulletType = 0; // UI 업데이트
                                targetRackVm.IsLocked = false; // UI 업데이트 (잠금 해제)
                                MessageBox.Show($"랙 {targetRackVm.Title} 출고 완료.", "출고 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                            });
                        }
                        catch (Exception ex)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"랙 {targetRackVm.Title} 출고 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                            });
                            // 오류 발생 시 해당 랙만 잠금 해제
                            await _databaseService.UpdateRackStateAsync(targetRackVm.Id, targetRackVm.RackType, targetRackVm.BulletType, false);
                            Application.Current.Dispatcher.Invoke(() => targetRackVm.IsLocked = false);
                        }
                    } // foreach 끝

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("모든 223 제품 출고 작업이 완료되었습니다.", "모든 출고 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                });
            }
            else
            {
                MessageBox.Show("223 제품 출고 작업이 취소되었습니다.", "취소", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private bool CanExecuteCheckout223Product(object parameter)
        {
            // 잠겨있지 않은 223 제품 랙이 하나라도 있으면 활성화
            return RackList?.Any(r => r.RackType == 1 && r.BulletType == 1 && !r.IsLocked) == true;
        }

        // '308 제품 출고' 버튼 수정 (223과 동일한 로직)
        private async void ExecuteCheckout308Product(object parameter)
        {
            // 출고 가능한 308 제품 랙 목록 가져오기 (잠겨있지 않은 랙만)
            var availableRacksForCheckout = RackList?.Where(r => r.RackType == 1 && r.BulletType == 2 && !r.IsLocked).Select(rvm => rvm.RackModel).ToList();

            if (availableRacksForCheckout == null || !availableRacksForCheckout.Any())
            {
                MessageBox.Show("출고할 308 제품이 있는 랙이 없습니다.", "308 출고 불가", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 팝업 ViewModel 인스턴스 생성 및 랙 리스트 전달
            var selectCheckoutRackViewModel = new SelectCheckoutRackPopupViewModel(availableRacksForCheckout);
            var selectCheckoutRackView = new SelectCheckoutRackPopupView { DataContext = selectCheckoutRackViewModel };
            selectCheckoutRackView.Title = "308 제품 출고할 랙 선택";

            if (selectCheckoutRackView.ShowDialog() == true && selectCheckoutRackViewModel.DialogResult == true)
            {
                // 사용자가 선택한 랙 목록 가져오기
                var selectedRacksForCheckout = selectCheckoutRackViewModel.GetSelectedRacks();

                if (selectedRacksForCheckout == null || !selectedRacksForCheckout.Any())
                {
                    MessageBox.Show("선택된 랙이 없습니다.", "출고 취소", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                MessageBox.Show($"{selectedRacksForCheckout.Count}개의 308 제품 랙 출고 작업을 시작합니다.", "출고 작업 시작", MessageBoxButton.OK, MessageBoxImage.Information);

                // **출고: 모든 선택된 랙을 동시에 잠금 (IsLocked = true)**
                var targetRackVmsToLock = selectedRacksForCheckout.Select(r => RackList?.FirstOrDefault(rvm => rvm.Id == r.Id))
                                                                   .Where(rvm => rvm != null)
                                                                   .ToList();
                foreach (var rvm in targetRackVmsToLock)
                {
                    await _databaseService.UpdateRackStateAsync(rvm.Id, rvm.RackType, rvm.BulletType, true);
                    Application.Current.Dispatcher.Invoke(() => rvm.IsLocked = true);
                }

                // 새로운 스레드에서 순차적으로 각 랙 출고 처리
                await Task.Run(async () =>
                {
                    foreach (var rackModelToCheckout in selectedRacksForCheckout)
                    {
                        var targetRackVm = RackList?.FirstOrDefault(r => r.Id == rackModelToCheckout.Id);
                        if (targetRackVm == null) continue; // 뷰모델이 없으면 다음 랙으로

                        try
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"랙 {targetRackVm.Title} 출고 처리 중... (10초 대기)", "출고 진행", MessageBoxButton.OK, MessageBoxImage.Information);
                            });

                            await Task.Delay(TimeSpan.FromSeconds(10)); // 10초 지연

                            // **출고: 각 랙이 개별적으로 잠금 해제 (IsLocked = false) 및 BulletType 변경**
                            await _databaseService.UpdateRackStateAsync(targetRackVm.Id, targetRackVm.RackType, 0, false);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                targetRackVm.BulletType = 0; // UI 업데이트
                                targetRackVm.IsLocked = false; // UI 업데이트 (잠금 해제)
                                MessageBox.Show($"랙 {targetRackVm.Title} 출고 완료.", "출고 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                            });
                        }
                        catch (Exception ex)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"랙 {targetRackVm.Title} 출고 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                            });
                            // 오류 발생 시 해당 랙만 잠금 해제
                            await _databaseService.UpdateRackStateAsync(targetRackVm.Id, targetRackVm.RackType, targetRackVm.BulletType, false);
                            Application.Current.Dispatcher.Invoke(() => targetRackVm.IsLocked = false);
                        }
                    } // foreach 끝

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("모든 308 제품 출고 작업이 완료되었습니다.", "모든 출고 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                });
            }
            else
            {
                MessageBox.Show("308 제품 출고 작업이 취소되었습니다.", "취소", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private bool CanExecuteCheckout308Product(object parameter)
        {
            // 잠겨있지 않은 308 제품 랙이 하나라도 있으면 활성화
            return RackList?.Any(r => r.RackType == 1 && r.BulletType == 2 && !r.IsLocked) == true;
        }

        // 모든 출고 관련 버튼의 CanExecute 상태를 갱신
        private void RaiseAllCheckoutCanExecuteChanged()
        {
            ((RelayCommand)InboundProductCommand).RaiseCanExecuteChanged(); // 입고 버튼도 갱신하도록 추가
            ((RelayCommand)Checkout223ProductCommand).RaiseCanExecuteChanged();
            ((RelayCommand)Checkout308ProductCommand).RaiseCanExecuteChanged();
        }

    }
}