// ViewModels/RackViewModel.cs
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using WPF_WMS01.Models;
using WPF_WMS01.Commands;
using WPF_WMS01.Services;
using WPF_WMS01.Views.Popups; // SelectStorageRackPopupView 추가
using WPF_WMS01.ViewModels.Popups; // SelectStorageRackPopupViewModel 추가
using System.Configuration;

namespace WPF_WMS01.ViewModels
{
    public class RackViewModel : INotifyPropertyChanged
    {
        //private readonly Rack _rack;
        public ICommand RackClickCommand { get; private set; }

        private Rack _rackModel; // Rack 모델의 백킹 필드
        public Rack RackModel // RackModel 속성 (private set 유지)
        {
            get => _rackModel;
            private set // private set 유지
            {
                if (_rackModel != value)
                {
                    // 이전 모델의 PropertyChanged 이벤트 구독 해제
                    if (_rackModel != null)
                    {
                        _rackModel.PropertyChanged -= OnRackModelPropertyChanged;
                    }

                    _rackModel = value; // 새 모델 인스턴스 할당

                    // 새 모델의 PropertyChanged 이벤트 구독
                    if (_rackModel != null)
                    {
                        _rackModel.PropertyChanged += OnRackModelPropertyChanged;
                    }

                    // RackModel 객체 자체가 바뀌었으므로, 모든 래퍼 속성에 대해 PropertyChanged 알림
                    //OnPropertyChanged(nameof(Id));
                    OnPropertyChanged(nameof(Title));
                    OnPropertyChanged(nameof(IsLocked));
                    OnPropertyChanged(nameof(IsVisible));
                    OnPropertyChanged(nameof(RackType));
                    OnPropertyChanged(nameof(BulletType));
                    OnPropertyChanged(nameof(ImageIndex));
                }
            }
        }

        private readonly DatabaseService _databaseService; // DatabaseService 추가
        private readonly MainViewModel _mainViewModel; // MainViewModel 참조 추가

        // 생성자: 최초 RackViewModel 생성 시 호출
        public RackViewModel(Rack rack, DatabaseService databaseService, MainViewModel mainViewModel)
        {
            // 생성자에서는 SetRackModel을 호출하여 _rackModel에 할당하고 구독 로직을 실행
            SetRackModel(rack); // RackModel의 set 접근자 로직이 여기서 실행됨

            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _mainViewModel = mainViewModel; // MainViewModel 참조 저장
            RackClickCommand = new RelayCommand(OnRackClicked, CanClickRack);
        }

        // RackModel의 PropertyChanged 이벤트를 처리하는 핸들러 (이전과 동일하게 유지)
        private void OnRackModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // RackModel 내부의 속성 변경 시, ViewModel의 해당 래퍼 속성에 대한 알림
            if (e.PropertyName == nameof(Models.Rack.ImageIndex))
            {
                OnPropertyChanged(nameof(ImageIndex));
            }
            else if (e.PropertyName == nameof(Models.Rack.IsLocked))
            {
                OnPropertyChanged(nameof(IsLocked));
            }
            else if (e.PropertyName == nameof(Models.Rack.IsVisible))
            {
                OnPropertyChanged(nameof(IsVisible));
            }
            else if (e.PropertyName == nameof(Models.Rack.Title))
            {
                OnPropertyChanged(nameof(Title));
            }
            else if (e.PropertyName == nameof(Models.Rack.RackType))
            {
                OnPropertyChanged(nameof(RackType));
            }
            else if (e.PropertyName == nameof(Models.Rack.BulletType))
            {
                OnPropertyChanged(nameof(BulletType));
            }
        }

        // MainViewModel에서 호출할 공용 메서드: RackModel 참조를 교체
        public void SetRackModel(Rack newRack)
        {
            RackModel = newRack; // 이 호출이 위에서 정의한 RackModel의 set 접근자를 호출
        }   

        // 새롭게 추가할 메서드: 기존 RackModel의 속성을 업데이트
        public void UpdateProperties(Rack newRackData)
        {
            // 각 속성을 개별적으로 비교하고 업데이트합니다.
            // 이렇게 하면 RackModel 인스턴스 자체는 변경되지 않습니다.
            if (RackModel.Title != newRackData.Title)
            {
                RackModel.Title = newRackData.Title;
            }
            if (RackModel.RackType != newRackData.RackType)
            {
                RackModel.RackType = newRackData.RackType;
            }
            if (RackModel.BulletType != newRackData.BulletType)
            {
                RackModel.BulletType = newRackData.BulletType;  
            }
            if (RackModel.IsVisible != newRackData.IsVisible)
            {
                RackModel.IsVisible = newRackData.IsVisible;
            }
            if (RackModel.IsLocked != newRackData.IsLocked)
            {
                RackModel.IsLocked = newRackData.IsLocked;
            }
            // Id는 Primary Key이므로 변경하지 않습니다.
            // ImageIndex는 RackModel 내부에서 계산되므로 여기서 설정할 필요 없음.
        }

        // 기존 래퍼 속성들 (Id, Title, ImageIndex, RackType, BulletType, IsVisible, IsLocked)
        // 이 속성들은 이제 모두 백킹 필드인 _rackModel을 참조하도록 수정해야 합니다.
        // 그리고 setter가 있는 경우, _rackModel의 해당 속성 setter를 호출해야 합니다.
        // 모델 속성들을 ViewModel에서 노출
        //public int Id => RackModel.Id;
        public int Id => _rackModel.Id; // _rackModel 필드 직접 참조
        public string Title
        {
            get => _rackModel.Title;
            set
            {
                if (_rackModel.Title != value)
                {
                    _rackModel.Title = value;
                    // OnPropertyChanged()는 RackModel에서 이미 알림을 보내므로 여기서는 필요 없음.
                    // 단, OnRackModelPropertyChanged가 Title 변경을 처리하도록 되어 있어야 함.
                }
            }
        }
        public bool IsLocked
        {
            get => _rackModel.IsLocked;
            set
            {
                if (_rackModel.IsLocked != value)
                {
                    _rackModel.IsLocked = value;
                    // OnPropertyChanged()는 RackModel에서 이미 알림을 보내므로 여기서는 필요 없음.
                }
            }
        }   
        public bool IsVisible
        {
            get => _rackModel.IsVisible;
            set
            {
                if (_rackModel.IsVisible != value)
                {
                    _rackModel.IsVisible = value;
                    // OnPropertyChanged()는 RackModel에서 이미 알림을 보내므로 여기서는 필요 없음.
                }
            }
        }

        // ImageIndex는 모델에서 계산된 값을 직접 가져옴
        public int ImageIndex => _rackModel.ImageIndex; // _rackModel 필드 직접 참조 (계산된 속성이므로 setter 없음)

        public int RackType
        {
            get => _rackModel.RackType;     
            set
            {
                if (_rackModel.RackType != value)
                {
                    _rackModel.RackType = value;
                    // OnPropertyChanged()는 RackModel에서 이미 알림을 보내므로 여기서는 필요 없음.
                }
            }
        }
        public int BulletType
        {
            get => _rackModel.BulletType;
            set
            {
                if (_rackModel.BulletType != value)
                {
                    _rackModel.BulletType = value;
                    // OnPropertyChanged()는 RackModel에서 이미 알림을 보내므로 여기서는 필요 없음.
                }
            }
        }

        // 새로운 속성 추가 (모델의 속성을 래핑)
        public string LotNumber
        {
            get => _rackModel.LotNumber;
            set
            {
                if (_rackModel.LotNumber != value)
                {
                    _rackModel.LotNumber = value;
                    OnPropertyChanged();
                    // 데이터베이스 업데이트 로직이 필요한 경우 여기에 추가 (또는 상위 ViewModel에서 처리)
                }
            }
        }

        public DateTime? RackedAt
        {
            get => _rackModel.RackedAt;
            set
            {
                if (_rackModel.RackedAt != value)
                {
                    _rackModel.RackedAt = value;
                    OnPropertyChanged();
                    // 데이터베이스 업데이트 로직이 필요한 경우 여기에 추가
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async void OnRackClicked(object parameter) // async로 변경 (DB 작업 때문)
        {
            // CommandParameter로 넘어온 RackViewModel을 사용 (만약 필요하다면)
            var clickedRackViewModel = parameter as RackViewModel;
            if (clickedRackViewModel == null) return;

            // ImageIndex 값에 따라 다른 팝업 창 띄우기
            switch (ImageIndex)
            {
                case 0:
                case 7:
                    // 랙 타입 변경 팝업
                    //int currentRackType = clickedRackViewModel.RackType;
                    //int newRackType = (currentRackType == 0) ? 1 : 0; // 0이면 1로, 1이면 0으로 변경
                    int currentRackType = clickedRackViewModel.RackModel.RackType; // 현재 모델의 타입 읽기
                    int newRackType = (currentRackType == 0) ? 1 : 0; // 0과 1 사이 토글

                    var popupViewModel = new RackTypeChangePopupViewModel(currentRackType, newRackType);
                    var popupView = new RackTypeChangePopupView { DataContext = popupViewModel };
                    // 팝업 윈도우의 제목 설정
                    popupView.Title = $"랙 {clickedRackViewModel.Title} 용도 변경"; // <--- 이 부분이 수정되었습니다.
                    bool? result = popupView.ShowDialog(); // 모달로 팝업 띄우고 결과 기다림

                    if (result == true) // 사용자가 '확인'을 눌렀을 경우
                    {
                        try
                        {
                            // DB 업데이트
                            await _databaseService.UpdateRackTypeAsync(clickedRackViewModel.Id, newRackType);
                            // 모델 업데이트 (UI 반영을 위해)
                            clickedRackViewModel.RackModel.RackType = newRackType;
                            //MessageBox.Show($"랙 {Title}의 타입이 {currentRackType}에서 {newRackType}으로 변경되었습니다.", "변경 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                            ShowAutoClosingMessage($"랙 {Title}의 타입이 {currentRackType}에서 {newRackType}으로 변경되었습니다.");
                        }
                        catch (Exception ex)
                        {   
                            MessageBox.Show($"랙 타입   변경 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        //MessageBox.Show("랙 타입 변경이 취소되었습니다.", "변경 취소", MessageBoxButton.OK, MessageBoxImage.Information);
                        ShowAutoClosingMessage("랙 타입 변경이 취소되었습니다.");
                    }
                    break;
                case 1:
                case 2:
                case 3:
                case 4:
                case 5:
                case 6:
                case 15:    // WAIT rack click
                case 16:    // WAIT rack click
                case 17:    // WAIT rack click
                case 18:    // WAIT rack click
                case 19:    // WAIT rack click
                case 20:    // WAIT rack click
                    // ImageIndex가 1~6 또는 15~20일 때 띄울 팝업 - 이동/복사 로직
                    await HandleRackTransfer(clickedRackViewModel); // 새로운 비동기 처리 메서드 호출
                    break;
                case 8:
                case 9:
                case 10:
                case 11:
                case 12:
                case 13:
                    // 3) ImageIndex가 4 또는 5일 때 띄울 팝업
                    await HandleRackShipout(clickedRackViewModel);
                    break;
                default:
                    // 그 외의 경우
                    MessageBox.Show($"랙 {Title} (ImageIndex: {ImageIndex}): 기타 유형의 팝업!", "랙 상세", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
            }
        }

        // 랙 이동/복사 로직을 처리하는 새로운 비동기 메서드
        private async Task HandleRackTransfer(RackViewModel sourceRackViewModel)
        {
            List<Rack> allRacks = await _databaseService.GetRackStatesAsync();
            // 🚨 수정할 부분: IsLocked가 false이면서 ImageIndex가 3인 랙만 필터링
            List<Rack> targetRacks = allRacks
                .Where(r => r.Id != sourceRackViewModel.Id && // 자기 자신 제외
                            !r.IsLocked &&                     // 잠겨있지 않은 랙만
                            r.ImageIndex == 7)                 // ImageIndex가 7인 랙만 (RackType 1, BulletType 0)
                .ToList();
            if (!targetRacks.Any())
            {
                MessageBox.Show("이동할 (비어있는 보관)랙이 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 랙 선택 팝업 표시 (이름 변경)
            var selectPopupViewModel = new SelectStorageRackPopupViewModel(targetRacks, _rackModel.LotNumber);
            var selectPopupView = new SelectStorageRackPopupView { DataContext = selectPopupViewModel };
            selectPopupView.Title = $"랙 {sourceRackViewModel.Title} 의 제품 이동";

            if (selectPopupView.ShowDialog() == true && selectPopupViewModel.SelectedRack != null)
            {
                Rack destinationRack = selectPopupViewModel.SelectedRack;

                // 1) 기존 랙 (sourceRack)과 대상 랙 (destinationRack)을 DB에서 잠금
                await _databaseService.UpdateRackStateAsync(sourceRackViewModel.Id, sourceRackViewModel.RackModel.RackType, sourceRackViewModel.RackModel.BulletType, true); // source 랙 잠금
                await _databaseService.UpdateRackStateAsync(destinationRack.Id, destinationRack.RackType, destinationRack.BulletType, true); // destination 랙 잠금

                //MessageBox.Show($"랙 {sourceRackViewModel.Title} 에서 랙 {destinationRack.Title} 로 이동 중입니다. 10초 대기...", "이동 시작", MessageBoxButton.OK, MessageBoxImage.Information);
                ShowAutoClosingMessage($"랙 {sourceRackViewModel.Title} 에서 랙 {destinationRack.Title} 로 이동 중입니다. 10초 대기...");
                // 이 값은 원본 랙의 BulletType이 0으로 변경되기 전에 가져와야 합니다.
                int originalSourceBulletType = sourceRackViewModel.RackModel.BulletType;

                // 2) 별도 스레드에서 지연 및 데이터 업데이트 (시뮬레이션)
                await Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(10)); // 10초 지연

                    try
                    {
                        // 3) 대상 랙 (destinationRack)의 BulletType을 원본 랙의 기존 BulletType으로 복사
                        // (대상 랙은 RackType을 유지하면서 원본 랙의 BulletType을 복사)
                        await _databaseService.UpdateRackStateAsync(
                            destinationRack.Id,
                            destinationRack.RackType,               // 대상 랙의 RackType은 유지
                            originalSourceBulletType,               // <-- 미리 저장해둔 원본 랙의 BulletType을 사용
                            false                                   // IsLocked 해제
                        );
                        if (sourceRackViewModel.Title.Equals(_mainViewModel._waitRackTitle))
                            await _databaseService.UpdateLotNumberAsync(destinationRack.Id, _mainViewModel.InputStringForButton.TrimStart());
                        else
                            await _databaseService.UpdateLotNumberAsync(destinationRack.Id, sourceRackViewModel.LotNumber);

                        // 4) 원본 랙 (sourceRack)의 BulletType을 0으로 설정하여 '비움'
                        // (원래 랙은 RackType을 유지하면서 BulletType만 0으로)
                        await _databaseService.UpdateRackStateAsync(
                            sourceRackViewModel.Id,
                            sourceRackViewModel.RackModel.RackType, // 원본 랙의 RackType은 유지
                            0,                                      // 원본 랙의 BulletType을 0으로 설정
                            false                                   // IsLocked 해제
                        );
                        await _databaseService.UpdateLotNumberAsync(sourceRackViewModel.Id, String.Empty);

                        if (sourceRackViewModel.Title.Equals(ConfigurationManager.AppSettings["WaitRackTitle"] ?? "WAIT"))
                            _mainViewModel.InputStringForButton = string.Empty; // 입고 후 TextBox 내용 초기화;

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            //MessageBox.Show($"랙 {sourceRackViewModel.Title} 에서 랙 {destinationRack.Title} 로의 이동이 완료되었습니다.", "이동 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                            ShowAutoClosingMessage($"랙 {sourceRackViewModel.Title} 에서 랙 {destinationRack.Title} 로의 이동이 완료되었습니다.");
                        });
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"랙 작업 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                });
            }
            else
            {
                //MessageBox.Show("랙 이동/복사 작업이 취소되었습니다.", "취소", MessageBoxButton.OK, MessageBoxImage.Information);
                ShowAutoClosingMessage("랙 이동/복사 작업이 취소되었습니다.");
                // 팝업이 닫히거나, 선택된 랙이 없으면 취소.
                // 잠갔던 sourceRackViewModel.IsLocked = true; 를 다시 false로 되돌려야 합니다.
                // 이 역시 DatabaseService를 통해 다시 업데이트
                await _databaseService.UpdateRackStateAsync(sourceRackViewModel.Id, sourceRackViewModel.RackModel.RackType, sourceRackViewModel.RackModel.BulletType, false);
            }
        }

        // 새롭게 추가할 출고 로직
        private async Task HandleRackShipout(RackViewModel targetRackViewModel)
        {
            var confirmPopupViewModel = new ConfirmShipoutPopupViewModel(targetRackViewModel.Title, targetRackViewModel.BulletType, targetRackViewModel.LotNumber);
            var confirmPopupView = new ConfirmShipoutPopupView { DataContext = confirmPopupViewModel };

            if (confirmPopupView.ShowDialog() == true && confirmPopupViewModel.DialogResult == true)
            {
                // UI 잠금 메시지
                //MessageBox.Show($"랙 {targetRackViewModel.Title} 출고 작업을 시작합니다. 10초 대기...", "작업 시작", MessageBoxButton.OK, MessageBoxImage.Information);
                ShowAutoClosingMessage($"랙 {targetRackViewModel.Title} 출고 작업을 시작합니다. 10초 대기...");

                // 랙 잠금 및 비동기 작업 시작
                await _databaseService.UpdateRackStateAsync(targetRackViewModel.Id, targetRackViewModel.RackType, targetRackViewModel.BulletType, true); // 랙 잠금

                await Task.Run(async () =>
                {
                    // === 향후 REST API 및 폴링 로직이 추가될 부분 ===
                    // 예: var commandResult = await _restApiClient.SendCommandAsync(targetRackViewModel.Id, "checkout");
                    //     while (true)
                    //     {
                    //         var status = await _restApiClient.GetStatusCommandAsync(commandResult.OperationId);
                    //         if (status.IsCompleted) break;
                    //         await Task.Delay(TimeSpan.FromSeconds(5)); // 5초마다 폴링
                    //     }
                    // ===============================================

                    await Task.Delay(TimeSpan.FromSeconds(10)); // 10초 지연 시뮬레이션

                    try
                    {
                        // BulletType을 0으로 설정하고 잠금 해제
                        await _databaseService.UpdateRackStateAsync(
                            targetRackViewModel.Id,
                            targetRackViewModel.RackType, // RackType은 유지
                            0,                            // BulletType을 0으로 설정 (출고)
                            false                         // IsLocked 해제
                        );
                        await _databaseService.UpdateLotNumberAsync(targetRackViewModel.Id, String.Empty);

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            //MessageBox.Show($"랙 {targetRackViewModel.Title} 출고 작업이 완료되었습니다.", "작업 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                            ShowAutoClosingMessage($"랙 {targetRackViewModel.Title} 출고 작업이 완료되었습니다.");
                        });
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"출고 작업 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                });
            }
            else
            {
                //MessageBox.Show("랙 출고 작업이 취소되었습니다.", "취소", MessageBoxButton.OK, MessageBoxImage.Information);
                ShowAutoClosingMessage("랙 출고 작업이 취소되었습니다.");
                // 취소 시 랙 잠금 해제 (작업 시작 전에 잠겼다면)
                // await _databaseService.UpdateRackStateAsync(targetRackViewModel.Id, targetRackViewModel.RackType, targetRackViewModel.BulletType, false);
            }
        }

        private bool CanClickRack(object parameter)
        {
            // Title 이 "WRAP"이 아니고
            // Rack이 잠겨있지 않을 때만 클릭 가능
            // IsLocked는 RackModel.IsLocked에서 가져옴
            return (!IsLocked && Title != "WRAP"); // 'locked' 상태가 아닐 때만 클릭 가능하도록 설정
        }
        // 자동 닫힘 메시지 팝업을 표시하는 헬퍼 메서드
        private void ShowAutoClosingMessage(string message)
        {
            // UI 스레드에서 팝업을 띄웁니다.
            Application.Current.Dispatcher.Invoke(() =>
            {
                var viewModel = new AutoClosingMessagePopupViewModel(message);
                var view = new AutoClosingMessagePopupView { DataContext = viewModel };
                view.Show(); // ShowDialog() 대신 Show()를 사용하여 비모달로 띄웁니다.
            });
        }
    }

}