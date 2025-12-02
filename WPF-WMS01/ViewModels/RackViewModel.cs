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
using System.Diagnostics;
using System.Windows.Controls;

namespace WPF_WMS01.ViewModels
{
    public class RackViewModel : INotifyPropertyChanged
    {
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
                    OnPropertyChanged(nameof(Id)); // Id도 래퍼 속성이므로 필요
                    OnPropertyChanged(nameof(Title));
                    OnPropertyChanged(nameof(IsLocked));
                    OnPropertyChanged(nameof(IsVisible));
                    OnPropertyChanged(nameof(RackType));
                    OnPropertyChanged(nameof(BulletType));
                    OnPropertyChanged(nameof(ImageIndex));
                    OnPropertyChanged(nameof(LotNumber));
                    OnPropertyChanged(nameof(RackedAt));
                    OnPropertyChanged(nameof(LocationArea)); // LocationArea 속성 변경 알림 추가
                    ((RelayCommand)RackClickCommand)?.RaiseCanExecuteChanged(); // CanExecute 상태 갱신
                }
            }
        }

        private readonly DatabaseService _databaseService; // DatabaseService 추가
        private readonly MainViewModel _mainViewModel; // MainViewModel 참조 추가
        // AMR Payload 필드는 MainViewModel에서 관리하므로 여기서는 제거합니다.
        //private readonly string _warehousePayload;
        //private readonly string _productionLinePayload;

        // 생성자: 최초 RackViewModel 생성 시 호출
        public RackViewModel(Rack rack, DatabaseService databaseService, MainViewModel mainViewModel)
        {
            // 생성자에서는 SetRackModel을 호출하여 _rackModel에 할당하고 구독 로직을 실행
            SetRackModel(rack); // RackModel의 set 접근자 로직이 여기서 실행됨

            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _mainViewModel = mainViewModel; // MainViewModel 참조 저장

            // AMR Payload 값은 MainViewModel에서 가져오도록 변경
            //_warehousePayload = ConfigurationManager.AppSettings["WarehouseAMR"] ?? "AMR_1";
            // = ConfigurationManager.AppSettings["ProductionLineAMR"] ?? "AMR_2";

            RackClickCommand = new RelayCommand(OnRackClicked, CanClickRack);
        }

        // RackModel의 PropertyChanged 이벤트를 처리하는 핸들러 (이전과 동일하게 유지)
        private void OnRackModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // RackModel 내부의 속성 변경 시, ViewModel의 해당 래퍼 속성에 대한 알림
            switch (e.PropertyName)
            {
                case nameof(Models.Rack.ImageIndex):
                    OnPropertyChanged(nameof(ImageIndex));
                    break;
                case nameof(Models.Rack.IsLocked):
                    OnPropertyChanged(nameof(IsLocked));
                    ((RelayCommand)RackClickCommand)?.RaiseCanExecuteChanged(); // 잠금 상태 변경 시 버튼 활성화/비활성화 갱신
                    break;
                case nameof(Models.Rack.IsVisible):
                    OnPropertyChanged(nameof(IsVisible));
                    break;
                case nameof(Models.Rack.Title):
                    OnPropertyChanged(nameof(Title));
                    break;
                case nameof(Models.Rack.RackType):
                    OnPropertyChanged(nameof(RackType));
                    break;
                case nameof(Models.Rack.BulletType):
                    OnPropertyChanged(nameof(BulletType));
                    break;
                case nameof(Models.Rack.LotNumber):
                    OnPropertyChanged(nameof(LotNumber));
                    break;
                case nameof(Models.Rack.RackedAt):
                    OnPropertyChanged(nameof(RackedAt));
                    break;
                case nameof(Models.Rack.LocationArea): // LocationArea 변경 시 알림 추가
                    OnPropertyChanged(nameof(LocationArea));
                    break;
            }
        }

        // MainViewModel에서 호출할 공용 메서드: RackModel 참조를 교체
        public void SetRackModel(Rack newRack)
        {
            RackModel = newRack; // 이 호출이 위에서 정의한 RackModel의 set 접근자를 호출
        }

        // 기존 RackModel의 속성을 업데이트 (이전과 동일하게 유지)
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
            if (RackModel.LotNumber != newRackData.LotNumber) // LotNumber 업데이트 로직 추가
            {
                RackModel.LotNumber = newRackData.LotNumber;
            }
            if (RackModel.RackedAt != newRackData.RackedAt) // RackedAt 업데이트 로직 추가
            {
                RackModel.RackedAt = newRackData.RackedAt;
            }
            if (RackModel.LocationArea != newRackData.LocationArea) // LocationArea 업데이트 로직 추가
            {
                RackModel.LocationArea = newRackData.LocationArea;
            }
            // Id는 Primary Key이므로 변경하지 않습니다.
            // ImageIndex는 RackModel 내부에서 계산되므로 여기서 설정할 필요 없음.
        }

        // 기존 래퍼 속성들 (Id, Title, ImageIndex, RackType, BulletType, IsVisible, IsLocked)
        public int Id => _rackModel.Id;
        public string Title
        {
            get => _rackModel.Title;
            set
            {
                if (_rackModel.Title != value)
                {
                    _rackModel.Title = value;
                    // OnPropertyChanged()는 RackModel에서 이미 알림을 보내므로 여기서는 명시적으로 호출할 필요 없음.
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
                    // OnPropertyChanged()는 RackModel에서 이미 알림을 보내므로 여기서는 명시적으로 호출할 필요 없음.
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
                    // OnPropertyChanged()는 RackModel에서 이미 알림을 보내므로 여기서는 명시적으로 호출할 필요 없음.
                }
            }
        }

        public int ImageIndex => _rackModel.ImageIndex;

        public int RackType
        {
            get => _rackModel.RackType;
            set
            {
                if (_rackModel.RackType != value)
                {
                    _rackModel.RackType = value;
                    // OnPropertyChanged()는 RackModel에서 이미 알림을 보내므로 여기서는 명시적으로 호출할 필요 없음
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
                    // OnPropertyChanged()는 RackModel에서 이미 알림을 보내므로 여기서는 명시적으로 호출할 필요 없음.
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
                    // OnPropertyChanged()는 RackModel에서 이미 알림을 보내므로 여기서는 명시적으로 호출할 필요 없음.
                }
            }
        }

        public int BoxCount
        {
            get => _rackModel.BoxCount;
            set
            {
                if (_rackModel.BoxCount != value)
                {
                    _rackModel.BoxCount = value;
                    // OnPropertyChanged()는 RackModel에서 이미 알림을 보내므로 여기서는 명시적으로 호출할 필요 없음.
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
                    // OnPropertyChanged()는 RackModel에서 이미 알림을 보내므로 여기서는 명시적으로 호출할 필요 없음.
                }
            }
        }

        // LocationArea 래퍼 속성 (모델의 LocationArea와 동기화)
        public int LocationArea
        {
            get => _rackModel.LocationArea;
            set
            {
                if (_rackModel.LocationArea != value)
                {
                    _rackModel.LocationArea = value;
                    // OnPropertyChanged()는 RackModel에서 이미 알림을 보내므로 여기서는 명시적으로 호출할 필요 없음.
                }
            }
        }

        public int? InsertedIn
        {
            get => _rackModel.InsertedIn;
            set
            {
                if (_rackModel.InsertedIn != value)
                {
                    _rackModel.InsertedIn = value;
                    // OnPropertyChanged()는 RackModel에서 이미 알림을 보내므로 여기서는 명시적으로 호출할 필요 없음.
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
            var clickedRackViewModel = parameter as RackViewModel;
            if (clickedRackViewModel == null) return;

            // AMR 랙 버튼 클릭 시 처리
            if (clickedRackViewModel.Title.Equals("AMR"))
            {
                _mainViewModel.ShowAmrMissionStatusCommand?.Execute(null);
                //ShowAutoClosingMessage("AMR을 클릭했습니다.");
                return; // 다른 랙 클릭 로직을 실행하지 않고 종료
            }

            // 랙이 잠겨있으면 작업을 수행할 수 없음
            if (IsLocked)
            {
                ShowAutoClosingMessage("랙이 잠겨있어 작업을 수행할 수 없습니다.");
                return;
            }

            // ImageIndex 값에 따라 다른 팝업 창 띄우기
            switch (ImageIndex)
            {
                case 0:
                case 13:
                    // 랙 타입 변경 팝업
                    int currentRackType = clickedRackViewModel.RackModel.RackType; // 현재 모델의 타입 읽기
                    int newRackType = (currentRackType == 0) ? 1 : 0; // 0과 1 사이 토글

                    var popupViewModel = new RackTypeChangePopupViewModel(0, currentRackType, newRackType);
                    var popupView = new RackTypeChangePopupView { DataContext = popupViewModel };
                    popupView.Title = $"랙 {clickedRackViewModel.Title} 용도 변경";
                    bool? result = popupView.ShowDialog();

                    if (result == true) // 사용자가 '확인'을 눌렀을 경우
                    {
                        try
                        {
                            // DB 업데이트
                            await _databaseService.UpdateRackTypeAsync(clickedRackViewModel.Id, newRackType);
                            // 모델 업데이트 (UI 반영을 위해)
                            clickedRackViewModel.RackModel.RackType = newRackType;
                            ShowAutoClosingMessage($"랙 {Title}의 타입이 {currentRackType}에서 {newRackType}으로 변경되었습니다.");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"랙 타입 변경 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        ShowAutoClosingMessage("랙 타입 변경이 취소되었습니다.");
                    }
                    break;
                case 140:
                case int i when i >= 1 && i <= 12: // ImageIndex가 1에서 12 사이인 경우
                    // 포장 대기 랙의 팔레트를 포장기로 옮기는 작업
                    await HandleTransferToWrapRack(clickedRackViewModel);
                    break;
                case int i when i >= 14 && i <= 25:
                    // 제품 팔레트를 출고하는 작업
                    await HandleRackShipout(clickedRackViewModel);
                    break;
                case 26:
                    break;
                case int i when i >= 27 && i <= 38: // ImageIndex가 27에서 38 사이인 경우, WRAP rack click
                    // 포장된 팔레트를 입고 랙으로 옮기는 작업
                    await HandleRackTransfer(clickedRackViewModel);
                    break;
                case int i when i >= 40 && i <= 51:
                    if(clickedRackViewModel.Title.Equals("1-1"))
                        await HandleHalfPalletExport(clickedRackViewModel); // 반제품 팔레트를 패키징 라인으로 반출하는 작업
                    else
                        await HandleHalfPalletMove(clickedRackViewModel); // 반제품 팔레트를 반출 대기 장소로 옮기는 작업
                    break;
                case 166:
                    MessageBox.Show($"{Title} 입고 불가 : 탄종이 입력되지 않았습니다.", "입고 불가", MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;
                case 39:
                    break;
                default:
                    MessageBox.Show($"랙 {Title} (ImageIndex: {ImageIndex}): 기타 유형의 팝업!", "랙 상세", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
            }
        }

        // 새로운 메서드: WRAP 랙으로 제품 이동 처리
        private async Task HandleTransferToWrapRack(RackViewModel sourceRackViewModel)
        {
            // "WRAP" 랙 찾기
            // MainViewModel의 RackList에 접근하여 "WRAP" 랙을 찾습니다.
            // MainViewModel의 RackList 속성이 public 이어야 합니다.
            var wrapRackViewModel = _mainViewModel.RackList?.FirstOrDefault(r => r.Title.Equals("WRAP"));

            if (wrapRackViewModel == null)
            {
                MessageBox.Show("이동할 'WRAP' 장소을 찾을 수 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // "WRAP" 랙의 상태 확인
            if (wrapRackViewModel.BulletType != 0 || wrapRackViewModel.IsLocked)
            {

                MessageBox.Show("포장 장소가 이미 사용 중이거나 잠겨있어 이동할 수 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 사용자에게 이동 여부 확인 팝업
            var confirmViewModel = new ConfirmTransferPopupViewModel(
                sourceRackViewModel.Title,
                sourceRackViewModel.LotNumber,
                "WRAP", sourceRackViewModel.BulletType
            );
            var confirmView = new ConfirmTransferPopupView { DataContext = confirmViewModel };
            bool? confirmResult = confirmView.ShowDialog();

            // ViewModel의 DialogResult 속성 대신 Window.ShowDialog()의 반환 값만 확인
            if (confirmResult == true)
            {
                List<int> lockedRackIds = new List<int>();
                // 1) 원본 랙과 WRAP 랙 잠금
                ShowAutoClosingMessage($"랙 {sourceRackViewModel.Title} 에서 'WRAP' 랙으로 이동을 시작합니다. 잠금 중...");
                try
                {
                    await _databaseService.UpdateIsLockedAsync(sourceRackViewModel.Id, true);
                    Application.Current.Dispatcher.Invoke(() => sourceRackViewModel.IsLocked = true);
                    lockedRackIds.Add(sourceRackViewModel.Id);

                    await _databaseService.UpdateIsLockedAsync(wrapRackViewModel.Id, true);
                    Application.Current.Dispatcher.Invoke(() => wrapRackViewModel.IsLocked = true);
                    lockedRackIds.Add(wrapRackViewModel.Id);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"랙 잠금 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    Debug.WriteLine($"[RackViewModel] Error locking racks: {ex.Message}");
                    // 오류 발생 시 작업 취소 및 잠금 해제 시도
                    foreach (var id in lockedRackIds)
                    {
                        await _databaseService.UpdateIsLockedAsync(id, false);
                        Application.Current.Dispatcher.Invoke(() => (_mainViewModel.RackList?.FirstOrDefault(r => r.Id == id)).IsLocked = false);
                    }
                    return; // 더 이상 진행하지 않음
                }

                var amrRackViewModel = _mainViewModel.RackList?.FirstOrDefault(r => r.Title.Equals("AMR"));
                // Case 1: WAIT 랙 -> WRAP 랙 (실제 로봇 미션)
                ShowAutoClosingMessage($"로봇 미션: 랙({sourceRackViewModel.Title})에서 WRAP 랙으로 이동 시작. 명령 전송 중...");
                List<MissionStepDefinition> missionSteps;
                string shelf = $"{int.Parse(sourceRackViewModel.Title.Split('-')[0]):D2}_{sourceRackViewModel.Title.Split('-')[1]}";

                missionSteps = new List<MissionStepDefinition>
                {
                    // 1. Move, Turn
                    new MissionStepDefinition {
                        ProcessStepDescription = $"랙 {sourceRackViewModel.Title}(으)로 이동하여, 미포장 팔레트 픽업",
                        MissionType = "8",
                        ToNode = $"Rack_{shelf}_PickUP",
                        Payload = _mainViewModel.WarehousePayload,
                        IsLinkable = true,
                        LinkWaitTimeout = 3600,
                        PostMissionOperations = new List<MissionSubOperation> {
                            new MissionSubOperation { Type = SubOperationType.DbUpdateRackState, Description = "랙 상태 업데이트", SourceRackIdForDbUpdate = sourceRackViewModel.Id, DestRackIdForDbUpdate = amrRackViewModel.Id }
                        }
                    },
                    new MissionStepDefinition {
                        ProcessStepDescription = "래핑기로 이동하여, 미포장 팔레트 드롭",
                        MissionType = "8",
                        ToNode = "Wrapping_Drop",
                        Payload = _mainViewModel.WarehousePayload,
                        IsLinkable = true,
                        LinkWaitTimeout = 3600,
                        PostMissionOperations = new List<MissionSubOperation> {
                            new MissionSubOperation { Type = SubOperationType.DbUpdateRackState, Description = "랙 상태 업데이트", SourceRackIdForDbUpdate = amrRackViewModel.Id, DestRackIdForDbUpdate =wrapRackViewModel.Id }
                        }
                    },
                    new MissionStepDefinition {
                        ProcessStepDescription = $"대기 장소로 이동",
                        MissionType = "8",
                        ToNode = "AMR1_WAIT",  //"Wrapping_Turn1",
                        Payload = _mainViewModel.WarehousePayload,
                        IsLinkable = false,
                        LinkWaitTimeout = 3600
                    }
                };

                try
                {
                    // MainViewModel을 통해 로봇 미션 프로세스 시작 (이제 MainViewModel이 RobotMissionService로 위임)
                    string processId = await _mainViewModel.InitiateRobotMissionProcess(
                        "포장 준비 작업", // 미션 프로세스 유형
                        missionSteps,
                        lockedRackIds,
                        null, // racksToProcess
                        null, // initiatingCoilAddress
                        true // isWarehouseMission = true로 전달
                    );
                    Debug.WriteLine($"[RackViewModel] Robot mission process '{processId}' initiated for transfer from {sourceRackViewModel.Title} to WRAP.");
                    ShowAutoClosingMessage($"로봇 미션 프로세스 시작됨: {processId}");
                    // **중요**: 로봇 미션이 시작되었으므로, 이 시점에서는 랙의 잠금 상태만 유지하고,
                    // 실제 DB 업데이트 (비우기, 채우기)는 RobotMissionService의 폴링 로직에서
                    // 미션 완료 시점(`HandleRobotMissionCompletion`)에 이루어지도록 위임합니다.
                    // 따라서 여기에 있던 10초 딜레이 및 직접적인 DB 업데이트 로직은 삭제합니다.
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"포장기로 이동 로봇 미션 시작 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    Debug.WriteLine($"[RackViewModel] Error initiating robot mission: {ex.Message}");
                    // 미션 시작 실패 시 랙 잠금 해제
                    foreach (var id in lockedRackIds)
                    {
                        await _databaseService.UpdateIsLockedAsync(id, false);
                        Application.Current.Dispatcher.Invoke(() => (_mainViewModel.RackList?.FirstOrDefault(r => r.Id == id)).IsLocked = false);
                    }
                    return; // 더 이상 진행하지 않음
                }
            }
            else
            {
                ShowAutoClosingMessage("랙 이동 작업이 취소되었습니다.");
            }
        }

        private async Task HandleHalfPalletMove(RackViewModel sourceRackViewModel)
        {
            // 라이트 팔레트 반출 전용 랙 찾기
            var outRackViewModel = _mainViewModel.RackList?.FirstOrDefault(r => r.Title.Equals("1-1"));

            if (outRackViewModel == null)
            {
                MessageBox.Show("반출 장소 'Rack 1-1'을 찾을 수 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (outRackViewModel.BulletType != 0 || outRackViewModel.IsLocked)
            {
                MessageBox.Show("반출 장소 'Rack 1-1'이 이미 사용 중이거나 잠겨있어 이동할 수 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var popupViewModel = new RackTypeChangePopupViewModel(2, sourceRackViewModel.Id);
            var popupView = new RackTypeChangePopupView
            {
                DataContext = popupViewModel
            };

            popupView.Title = $"랙 {sourceRackViewModel.Title} 라이트 팔레트를 반출용 랙에 이동 적치";
            bool? result = popupView.ShowDialog();

            if (result == true) // 사용자가 '확인'을 눌렀을 경우
            {
                ShowAutoClosingMessage($"랙 {sourceRackViewModel.Title}의 라이트 팔레트를 반출 대기 장소로 옮깁니다. 잠금 중...");
                List<int> lockedRackIds = new List<int>();
                try
                {
                    // 1) 기존 랙 (sourceRack)을 DB에서 잠금
                    await _databaseService.UpdateIsLockedAsync(sourceRackViewModel.Id, true);
                    Application.Current.Dispatcher.Invoke(() => sourceRackViewModel.IsLocked = true);
                    lockedRackIds.Add(sourceRackViewModel.Id);

                    await _databaseService.UpdateIsLockedAsync(outRackViewModel.Id, true);
                    Application.Current.Dispatcher.Invoke(() => outRackViewModel.IsLocked = true);
                    lockedRackIds.Add(outRackViewModel.Id);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"랙 잠금 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    // 오류 발생 시 작업 취소 및 잠금 해제 시도
                    foreach (var id in lockedRackIds)
                    {
                        await _databaseService.UpdateIsLockedAsync(id, false);
                        Application.Current.Dispatcher.Invoke(() => (_mainViewModel.RackList?.FirstOrDefault(r => r.Id == id)).IsLocked = false);
                    }
                    return; // 더 이상 진행하지 않음
                }

                ShowAutoClosingMessage($"로봇 미션: 랙 {sourceRackViewModel.Title}의  라이트 팔레트를 반출 대기 장소로 이동 시작. 명령 전송 중...");

                var amrRackViewModel = _mainViewModel.RackList?.FirstOrDefault(r => r.Title.Equals("AMR"));

                List<MissionStepDefinition> missionSteps = new List<MissionStepDefinition>();

                string shelf = $"{int.Parse(sourceRackViewModel.Title.Split('-')[0]):D2}_{sourceRackViewModel.Title.Split('-')[1]}";
                string targetShelf = $"{int.Parse(outRackViewModel.Title.Split('-')[0]):D2}_{outRackViewModel.Title.Split('-')[1]}";

                // 로봇 미션 단계 정의 (사용자 요청에 따라 4단계로 복원 및 IsLinkable, LinkedMission 조정)

                // 1. 라이트 팔레트 픽업
                missionSteps.Add(new MissionStepDefinition
                {
                    ProcessStepDescription = $"랙 {sourceRackViewModel.Title}(으)로 이동하여, 라이트 팔레트 픽업",
                    MissionType = "8",
                    ToNode = $"Rack_{shelf}_PickUP",
                    Payload = _mainViewModel.WarehousePayload,
                    IsLinkable = true,
                    LinkWaitTimeout = 3600,
                    PostMissionOperations = new List<MissionSubOperation> {
                        new MissionSubOperation { Type = SubOperationType.DbUpdateRackState, Description = "랙 상태 업데이트", SourceRackIdForDbUpdate = sourceRackViewModel.Id, DestRackIdForDbUpdate = amrRackViewModel.Id }
                    }
                });

                // 2. 라이트 팔레트 드롭
                if (outRackViewModel.LocationArea == 2 || outRackViewModel.LocationArea == 4) // 랙 2 ~ 8번 1단 드롭 만 적용
                {
                    missionSteps.Add(new MissionStepDefinition
                    {
                        ProcessStepDescription = "팔레트 픽업을 위한 이동 및 회전",
                        MissionType = "7",
                        FromNode = $"RACK_{targetShelf}_STEP1",
                        ToNode = $"RACK_{targetShelf}_STEP2",
                        Payload = _mainViewModel.WarehousePayload,
                        IsLinkable = true,
                        LinkWaitTimeout = 3600
                    });
                }
                missionSteps.Add(new MissionStepDefinition
                {
                    ProcessStepDescription = $"랙 {outRackViewModel.Title}(으)로 이동하여, 라이트 팔레트 드롭",
                    MissionType = "8",
                    ToNode = $"Rack_{targetShelf}_Drop",
                    Payload = _mainViewModel.WarehousePayload,
                    IsLinkable = true,
                    LinkWaitTimeout = 3600,
                    PostMissionOperations = new List<MissionSubOperation> {
                        new MissionSubOperation { Type = SubOperationType.DbUpdateRackState, Description = "랙 상태 업데이트", SourceRackIdForDbUpdate = amrRackViewModel.Id, DestRackIdForDbUpdate = outRackViewModel.Id }
                    }
                });

                // 3. Move, Charge
                missionSteps.Add(new MissionStepDefinition
                {
                    ProcessStepDescription = $"대기 장소로 복귀",
                    MissionType = "8",
                    ToNode = "AMR1_WAIT",
                    Payload = _mainViewModel.WarehousePayload,
                    IsLinkable = false,
                    LinkWaitTimeout = 3600
                });

                try
                {
                    // 로봇 미션 프로세스 시작
                    string processId = await _mainViewModel.InitiateRobotMissionProcess(
                        "라이트 반출 준비", //"HandleHalfPalletMove", // 미션 프로세스 유형
                        missionSteps,
                        lockedRackIds, // 잠긴 랙 ID 목록 전달
                        null, // racksToProcess
                        null, // initiatingCoilAddress
                        true // isWarehouseMission = true로 전달
                    );
                    ShowAutoClosingMessage($"로봇 미션 프로세스 시작됨: {processId}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"반제품 반출 로봇 미션 시작 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    foreach (var id in lockedRackIds)
                    {
                        await _databaseService.UpdateIsLockedAsync(id, false);
                        Application.Current.Dispatcher.Invoke(() => (_mainViewModel.RackList?.FirstOrDefault(r => r.Id == id)).IsLocked = false);
                    }
                }
            }
            else
            {
                _mainViewModel.PlcStatusIsPaused = false; // 콜버튼 액션 허용
                ShowAutoClosingMessage("라이트 반출작업이 취소되었습니다.");
            }
        }

        // 라이트 반출을 처리하는 비동기 메서드 (이전과 동일하게 유지)
        private async Task HandleHalfPalletExport(RackViewModel outRackVm)
        {
            var popupViewModel = new SelectProductionLinePopupViewModel(_rackModel.LotNumber);
            var popupView = new SelectProductionLinePopupView
            {
                DataContext = popupViewModel
            };

            if (popupView.ShowDialog() == true && popupViewModel.DialogResult == true)
            {
                var selectedLine = popupViewModel.SelectedLocation;
                if (selectedLine != null)
                {
                    ShowAutoClosingMessage($"랙 {outRackVm.Title}의 라이트 팔레트를 '{selectedLine.Name}'(으)로 반출합니다. 잠금 중...");
                    List<int> lockedRackIds = new List<int>();
                    try
                    {
                        // 1) 기존 랙 (sourceRack)을 DB에서 잠금
                        await _databaseService.UpdateIsLockedAsync(outRackVm.Id, true);
                        Application.Current.Dispatcher.Invoke(() => outRackVm.IsLocked = true);
                        lockedRackIds.Add(outRackVm.Id);

                        if (selectedLine.Id == 9)
                        {
                            await _databaseService.UpdateIsLockedAsync(outRackVm.Id, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"랙 잠금 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        // 오류 발생 시 작업 취소 및 잠금 해제 시도
                        foreach (var id in lockedRackIds)
                        {
                            await _databaseService.UpdateIsLockedAsync(id, false);
                            Application.Current.Dispatcher.Invoke(() => (_mainViewModel.RackList?.FirstOrDefault(r => r.Id == id)).IsLocked = false);
                        }
                        return; // 더 이상 진행하지 않음
                    }

                    ShowAutoClosingMessage($"로봇 미션: 랙 {outRackVm.Title}의  라이트 팔레트를 '{selectedLine.Name}'(으)로 반출 시작. 명령 전송 중...");

                    var amrRackViewModel = _mainViewModel.RackList?.FirstOrDefault(r => r.Title.Equals("AMR"));

                    List<MissionStepDefinition> missionSteps = new List<MissionStepDefinition>();
                    //List<int> racksToLock = new List<int>(); // No racks locked for simple supply missions initially

                    string workPoint;
                    string swapPoint;
                    // Determine MC Protocol IP address based on button content
                    string? mcProtocolIpAddress = null;
                    ushort? lotInfoReadAddress = null;
                    ushort? lightAddress = null;
                    string? readStringValue = null;
                    ushort? readIntvalue = null;
                    ushort? coilAddress = null;

                    if (selectedLine.Id == 2)
                    {
                        workPoint = "223A1";
                        swapPoint = "223A"; // "Turn_223A1_Direct";
                        mcProtocolIpAddress = ConfigurationManager.AppSettings["McProtocolIpAddress556mm1"] ?? "192.168.200.101";
                        lotInfoReadAddress = 0x1520; // Lot 정보 위치
                        lightAddress = 0x102D;  // 경광등 On/Off
                        coilAddress = 1;
                    }
                    else if (selectedLine.Id == 1)
                    {
                        workPoint = "223A2";
                        swapPoint = "223A";
                        mcProtocolIpAddress = ConfigurationManager.AppSettings["McProtocolIpAddress556mm1"] ?? "192.168.200.101";
                        lotInfoReadAddress = 0x1510;
                        lightAddress = 0x101D;
                        coilAddress = 0;
                    }
                    else if (selectedLine.Id == 4)
                    {
                        workPoint = "223B1";
                        swapPoint = "223B";
                        mcProtocolIpAddress = ConfigurationManager.AppSettings["McProtocolIpAddress556mm2"] ?? "192.168.200.102";
                        lotInfoReadAddress = 0x1520;
                        lightAddress = 0x102D;
                        coilAddress = 4;
                    }
                    else if (selectedLine.Id == 3)
                    {
                        workPoint = "223B2";
                        swapPoint = "223B";
                        mcProtocolIpAddress = ConfigurationManager.AppSettings["McProtocolIpAddress556mm2"] ?? "192.168.200.102";
                        lotInfoReadAddress = 0x1510;
                        lightAddress = 0x101D;
                        coilAddress = 3;
                    }
                    else if (selectedLine.Id == 5)
                    {
                        workPoint = "308";
                        swapPoint = "308";
                        mcProtocolIpAddress = ConfigurationManager.AppSettings["McProtocolIpAddress762mm"] ?? "127.168.200.103";
                        lotInfoReadAddress = 0x1510;
                        lightAddress = 0x101D;
                        coilAddress = 6;
                    }
                    else if (selectedLine.Id == 6)
                    {
                        workPoint = "Manual_1";
                        swapPoint = "Manual";
                        mcProtocolIpAddress = ConfigurationManager.AppSettings["McProtocolIpAddressQatar"] ?? "127.168.200.120";
                        lotInfoReadAddress = 0x1520;
                        lightAddress = 0x102D;
                        coilAddress = 9;
                    }
                    else if (selectedLine.Id == 7)
                    {
                        workPoint = "Manual_2";
                        swapPoint = "Manual";
                        mcProtocolIpAddress = ConfigurationManager.AppSettings["McProtocolIpAddressQatar"] ?? "127.168.200.120";
                        lotInfoReadAddress = 0x1510;
                        lightAddress = 0x101D;
                        coilAddress = 10;
                    }
                    else if (selectedLine.Id == 8)
                    {
                        workPoint = "Etc_1"; // or "Etc_2"
                        swapPoint = "Etc";
                        mcProtocolIpAddress = null;
                        coilAddress = 11;
                    }
                    else if (selectedLine.Id == 9) // 라이트 팔레트를 랩핑기에 이송하는 특수 케이스, 2025/11/24 풍산 추가 요구 사항 
                    {
                        var wrapRackViewModel = _mainViewModel.RackList?.FirstOrDefault(r => r.Title.Equals("WRAP"));

                        if (wrapRackViewModel == null)
                        {
                            MessageBox.Show("이동할 'WRAP' 장소을 찾을 수 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                            foreach (var id in lockedRackIds)
                            {
                                await _databaseService.UpdateIsLockedAsync(id, false);
                                Application.Current.Dispatcher.Invoke(() => (_mainViewModel.RackList?.FirstOrDefault(r => r.Id == id)).IsLocked = false);
                            }
                            return;
                        }

                        // "WRAP" 랙의 상태 확인
                        if (wrapRackViewModel.BulletType != 0 || wrapRackViewModel.IsLocked)
                        {
                            MessageBox.Show("포장 장소가 이미 사용 중이거나 잠겨있어 이동할 수 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                            foreach (var id in lockedRackIds)
                            {
                                await _databaseService.UpdateIsLockedAsync(id, false);
                                Application.Current.Dispatcher.Invoke(() => (_mainViewModel.RackList?.FirstOrDefault(r => r.Id == id)).IsLocked = false);
                            }
                            return;
                        }

                        try
                        {
                            await _databaseService.UpdateIsLockedAsync(wrapRackViewModel.Id, true);
                            Application.Current.Dispatcher.Invoke(() => wrapRackViewModel.IsLocked = true);
                            lockedRackIds.Add(wrapRackViewModel.Id);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"랙 잠금 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                            // 오류 발생 시 작업 취소 및 잠금 해제 시도
                            foreach (var id in lockedRackIds)
                            {
                                await _databaseService.UpdateIsLockedAsync(id, false);
                                Application.Current.Dispatcher.Invoke(() => (_mainViewModel.RackList?.FirstOrDefault(r => r.Id == id)).IsLocked = false);
                            }
                            return; // 더 이상 진행하지 않음
                        }

                        missionSteps.Add(new MissionStepDefinition
                        {
                            ProcessStepDescription = $"랙 1-1로 이동하여, 라이트 팔레트 픽업",
                            MissionType = "8",
                            ToNode = $"Rack_01_1_PickUP",
                            Payload = _mainViewModel.WarehousePayload,
                            IsLinkable = true,
                            LinkWaitTimeout = 3600,
                            PostMissionOperations = new List<MissionSubOperation> {
                                new MissionSubOperation { Type = SubOperationType.DbUpdateRackState, Description = "랙 상태 업데이트", SourceRackIdForDbUpdate = outRackVm.Id, DestRackIdForDbUpdate = amrRackViewModel.Id }
                            }
                        });
                        missionSteps.Add(new MissionStepDefinition
                        {
                            ProcessStepDescription = "래핑기로 이동하여, 라이트 팔레트 드롭",
                            MissionType = "8",
                            ToNode = "Wrapping_Drop",
                            Payload = _mainViewModel.WarehousePayload,
                            IsLinkable = true,
                            LinkWaitTimeout = 3600,
                            PostMissionOperations = new List<MissionSubOperation> {
                                new MissionSubOperation { Type = SubOperationType.DbUpdateRackState, Description = "랙 상태 업데이트", SourceRackIdForDbUpdate = amrRackViewModel.Id, DestRackIdForDbUpdate =wrapRackViewModel.Id }
                            }
                        });
                        missionSteps.Add(new MissionStepDefinition
                        {
                            ProcessStepDescription = $"대기 장소로 이동",
                            MissionType = "8",
                            ToNode = "AMR1_WAIT",  //"Wrapping_Turn1",
                            Payload = _mainViewModel.WarehousePayload,
                            IsLinkable = false,
                            LinkWaitTimeout = 3600
                        });

                        try
                        {
                            // 로봇 미션 프로세스 시작
                            string processId = await _mainViewModel.InitiateRobotMissionProcess(
                                "라이트 반출 작업", // 미션 프로세스 유형
                                missionSteps,
                                lockedRackIds, // 잠긴 랙 ID 목록 전달
                                null, // racksToProcess
                                null, // initiatingCoilAddress
                                true // isWarehouseMission = true로 전달
                            );
                            ShowAutoClosingMessage($"로봇 미션 프로세스 시작됨: {processId}");
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"반제품 반출 로봇 미션 시작 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                            foreach (var id in lockedRackIds)
                            {
                                await _databaseService.UpdateIsLockedAsync(id, false);
                                Application.Current.Dispatcher.Invoke(() => (_mainViewModel.RackList?.FirstOrDefault(r => r.Id == id)).IsLocked = false);
                            }
                            return; // 더 이상 진행하지 않음
                        }
                        return;
                    }
                    else
                    {
                        return;
                    }
   
                    var inRackVm = await _mainViewModel.GetRackViewModelForInboundTemporary();    // 라인 입고 팔레트를 적치할 Rack
                    if (inRackVm == null)
                    {
                        MessageBox.Show("적치 가능한 랙이 없습니다.", "경고", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
                        return;
                    }
                    await _databaseService.UpdateIsLockedAsync(inRackVm.Id, true);
                    Application.Current.Dispatcher.Invoke(() => (_mainViewModel.RackList?.FirstOrDefault(r => r.Id == inRackVm.Id)).IsLocked = true);
                    lockedRackIds.Add(inRackVm.Id);

                    await _databaseService.UpdateIsLockedAsync(outRackVm.Id, true);
                    Application.Current.Dispatcher.Invoke(() => (_mainViewModel.RackList?.FirstOrDefault(r => r.Id == outRackVm.Id)).IsLocked = true);
                    lockedRackIds.Add(outRackVm.Id);

                    var inShelf = $"{int.Parse(inRackVm.Title.Split('-')[0]):D2}_{inRackVm.Title.Split('-')[1]}";
                    var outShelf = $"{int.Parse(outRackVm.Title.Split('-')[0]):D2}_{outRackVm.Title.Split('-')[1]}";

                    switch (selectedLine.Id)
                    {
                        case 1: // 223#1 A
                        case 2: // 223#1 B
                        case 3: // 223#2 A
                        case 4: // 223#2 B
                            // 1. Move from Charger, Turn
                            missionSteps.Add(new MissionStepDefinition {
                                ProcessStepDescription = "대기 장소로 이동, 경광등 켜기",
                                MissionType = "8",
                                ToNode = "Pallet_BWD_Pos",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkWaitTimeout = 3600,
                                PostMissionOperations = new List<MissionSubOperation> // 경광등 켜기
                                {
                                    new MissionSubOperation { Type = SubOperationType.McWriteSingleWord, Description = "경광등 켜기", WordDeviceCode = "W", McWordAddress = lightAddress, McWriteValueInt = 1, McProtocolIpAddress = mcProtocolIpAddress }
                                }
                            });
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = $"{selectedLine.Name} 근처로 이동, 안전 센서 끄기",
                                MissionType = "8",
                                ToNode = $"Turn_{workPoint}_Direct",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkWaitTimeout = 3600,
                                PostMissionOperations = new List<MissionSubOperation> {
                                    new MissionSubOperation { Type = SubOperationType.McWaitSensorOff, Description = "안전 센서 끄기", WordDeviceCode = "W", McWordAddress = 0x1008, McWriteValueInt = 1, McProtocolIpAddress = mcProtocolIpAddress }
                                }
                            });
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = $"{selectedLine.Name} 제품 팔레트 픽업, Lot 정보 읽기",
                                MissionType = "8",
                                ToNode = $"Work_{workPoint}_PickUP",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkWaitTimeout = 3600,
                                PostMissionOperations = new List<MissionSubOperation> // Read LotNo. BoxCount 
                                {
                                    new MissionSubOperation { Type = SubOperationType.McReadLotNoBoxCount, Description = "LotNo., BoxCount 읽기", WordDeviceCode = "W", McWordAddress = lotInfoReadAddress, McStringLengthWords = 8, McProtocolIpAddress = mcProtocolIpAddress }
                                }
                            });
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = $"{selectedLine.Name}(으)로부터 나와서 안전 센서 켜기",
                                MissionType = "8",
                                ToNode = $"{swapPoint}_SENSOR",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkWaitTimeout = 3600,
                                PreMissionOperations = new List<MissionSubOperation> // 안전 센서 ON
                                {
                                    new MissionSubOperation { Type = SubOperationType.McWaitSensorOn, Description = "안전 센서 켜기", WordDeviceCode = "W", McWordAddress = 0x1008, McWriteValueInt = 0, McProtocolIpAddress = mcProtocolIpAddress }
                                }
                            });
                            // 제품 팔레트 임시 입고 랙에 적치
                            if (inRackVm.LocationArea == 2 || inRackVm.LocationArea == 4) // 랙 2 - 8, 1단 드롭 만 적용
                            {
                                missionSteps.Add(new MissionStepDefinition
                                {
                                    ProcessStepDescription = "팔레트 적재를 위한 이동 및 회전 2",
                                    MissionType = "7",
                                    FromNode = $"RACK_{inShelf}_STEP1",
                                    ToNode = $"RACK_{inShelf}_STEP2",
                                    Payload = _mainViewModel.ProductionLinePayload,
                                    IsLinkable = true,
                                    LinkWaitTimeout = 3600
                                });
                            }
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = $"{inRackVm.Title}(으)로 이동 & 팔레트 드롭",
                                MissionType = "8",
                                ToNode = $"Rack_{inShelf}_Drop",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkWaitTimeout = 3600,
                                PostMissionOperations = new List<MissionSubOperation> {
                                    //new MissionSubOperation { Type = SubOperationType.CheckModbusDiscreteInput, Description = "팔레트 랙에 안착 여부 확인", McDiscreteInputAddress = _mainViewModel._checkModbusDescreteInputAddr },
                                    new MissionSubOperation { Type = SubOperationType.DbWriteRackData, Description = "입고 팔레트 정보 업데이트", DestRackIdForDbUpdate = inRackVm.Id },
                                    new MissionSubOperation { Type = SubOperationType.ClearLotInformation, Description = "Lot 정보 표시 지우기" }
                                }
                            });

                            // 반출할 라이트 팔레트 픽업
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = $"라이트 반출 장소({outRackVm.Title})로 이동하여, 라이트 팔레트 픽업",
                                MissionType = "8",
                                ToNode = $"Rack_{outShelf}_PickUP",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkWaitTimeout = 3600,
                                PostMissionOperations = new List<MissionSubOperation> {
                                    new MissionSubOperation { Type = SubOperationType.DbReadRackData, Description = "랙 데이터 읽어 오기", TargetRackId = outRackVm.Id },
                                    new MissionSubOperation { Type = SubOperationType.DbUpdateRackState, Description = "랙 상태 업데이트", SourceRackIdForDbUpdate = outRackVm.Id, DestRackIdForDbUpdate = null },
                                    //new MissionSubOperation { Type = SubOperationType.SetPlcStatusIsPaused, Description = "콜버튼 액션 허용", PauseButtonCallPlcStatus = false }
                                }
                            });

                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = $"{selectedLine.Name}(으) 근처로 이동, 안전 센서 끄기",
                                MissionType = "8",
                                ToNode = $"Turn_{workPoint}_Direct",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkWaitTimeout = 3600,
                                PostMissionOperations = new List<MissionSubOperation> {
                                    new MissionSubOperation { Type = SubOperationType.McWaitSensorOff, Description = "안전 센서 끄기", WordDeviceCode = "W", McWordAddress = 0x1008, McWriteValueInt = 1, McProtocolIpAddress = mcProtocolIpAddress }
                                }
                            });
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = $"{selectedLine.Name}에 라이트 팔레트 드롭 후 LotNo., BoxCount 쓰기",
                                MissionType = "8",
                                ToNode = $"Work_{workPoint}_Drop",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkWaitTimeout = 3600,
                                PostMissionOperations = new List<MissionSubOperation> {
                                    new MissionSubOperation { Type = SubOperationType.McWriteLotNoBoxCount, Description = "LotNo., BoxCount 쓰기", WordDeviceCode = "W", McWordAddress = 0x1010, McStringLengthWords = 8, McProtocolIpAddress = mcProtocolIpAddress }
                                }
                            });
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = $"{selectedLine.Name}(으)로부터 나와서 안전센서 켜기",
                                MissionType = "8",
                                ToNode = $"{swapPoint}_SENSOR",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkWaitTimeout = 3600,
                                PostMissionOperations = new List<MissionSubOperation> { // LotNo., BoxCount refresh를 위해 경광등 끄기를 먼저 한다,
                                    new MissionSubOperation { Type = SubOperationType.McWaitSensorOn, Description = "안전 센서 켜기", WordDeviceCode = "W", McWordAddress = 0x1008, McWriteValueInt = 0, McProtocolIpAddress = mcProtocolIpAddress },
                                    new MissionSubOperation { Type = SubOperationType.McWriteSingleWord, Description = "경광등 끄기", WordDeviceCode = "W", McWordAddress = lightAddress, McWriteValueInt = 0, McProtocolIpAddress = mcProtocolIpAddress }
                                }
                            });
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = "작업 대기 장소로 이동",
                                MissionType = "8",
                                ToNode = "Pallet_BWD_Pos",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = false,
                                LinkWaitTimeout = 3600,
                            });
                            break;

                        case 5: // 308
                            // 1. Move from Charger, Turn
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = "대기 장소로 이동, 경광등 켜기",
                                MissionType = "8",
                                ToNode = "Pallet_BWD_Pos",
                                Payload = _mainViewModel.ProductionLinePayload,
                                LinkWaitTimeout = 3600,
                                PostMissionOperations = new List<MissionSubOperation> // 경광등 켜기
                                {
                                    new MissionSubOperation { Type = SubOperationType.McWriteSingleWord, Description = "경광등 켜기", WordDeviceCode = "W", McWordAddress = lightAddress, McWriteValueInt = 1, McProtocolIpAddress = mcProtocolIpAddress }
                                }
                            });
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = $"{selectedLine.Name} 근처로 이동, 안전 센서 끄기",
                                MissionType = "8",
                                ToNode = $"Work_308_Turn",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkWaitTimeout = 3600,
                                PostMissionOperations = new List<MissionSubOperation> {
                                    new MissionSubOperation { Type = SubOperationType.McWaitSensorOff, Description = "안전 센서 끄기", WordDeviceCode = "W", McWordAddress = 0x1008, McWriteValueInt = 1, McProtocolIpAddress = mcProtocolIpAddress }
                                }
                            });
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = $"{selectedLine.Name} 제품 팔레트 픽업 후 Lot 정보 읽기",
                                MissionType = "8",
                                ToNode = $"Work_308_PickUP",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkWaitTimeout = 3600,
                                PostMissionOperations = new List<MissionSubOperation> // Read LotNo. BoxCount 
                                {
                                    new MissionSubOperation { Type = SubOperationType.McReadLotNoBoxCount, Description = "LotNo., BoxCount 읽기", WordDeviceCode = "W", McWordAddress = lotInfoReadAddress, McStringLengthWords = 8, McProtocolIpAddress = mcProtocolIpAddress }
                                }
                            });
                            // Rack에 적치하기 위한 이동 및 적치
                            if (inRackVm.LocationArea == 2 || inRackVm.LocationArea == 4) // 랙 2 ~ 8 번 1단 드롭 만 적용
                            {
                                missionSteps.Add(new MissionStepDefinition
                                {
                                    ProcessStepDescription = "팔레트 적재를 위한 이동 및 회전",
                                    MissionType = "7",
                                    FromNode = $"RACK_{inShelf}_STEP1",
                                    ToNode = $"RACK_{inShelf}_STEP2",
                                    Payload = _mainViewModel.ProductionLinePayload,
                                    IsLinkable = true,
                                    LinkWaitTimeout = 3600
                                });
                            }
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = $"{inRackVm.Title}(으)로 이동 & 팔레트 드롭",
                                MissionType = "8",
                                ToNode = $"Rack_{inShelf}_Drop",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkWaitTimeout = 3600,
                                PostMissionOperations = new List<MissionSubOperation> {
                                    //new MissionSubOperation { Type = SubOperationType.CheckModbusDiscreteInput, Description = "팔레트 랙에 안착 여부 확인", McDiscreteInputAddress = _mainViewModel._checkModbusDescreteInputAddr },
                                    new MissionSubOperation { Type = SubOperationType.DbWriteRackData, Description = "입고 팔레트 정보 업데이트", DestRackIdForDbUpdate = inRackVm.Id },
                                    new MissionSubOperation { Type = SubOperationType.ClearLotInformation, Description = "Lot 정보 표시 지우기" }
                                }
                            });

                            // 반출할 라이트 팔레트 픽업
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = $"라이트 반출 장소({outRackVm.Title})로 이동하여, 라이트 팔레트 픽업",
                                MissionType = "8",
                                ToNode = $"Rack_{outShelf}_PickUP",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkWaitTimeout = 3600,
                                PostMissionOperations = new List<MissionSubOperation> {
                                    new MissionSubOperation { Type = SubOperationType.DbReadRackData, Description = "랙 데이터 읽어 오기", TargetRackId = outRackVm.Id },
                                    new MissionSubOperation { Type = SubOperationType.DbUpdateRackState, Description = "랙 상태 업데이트", SourceRackIdForDbUpdate = outRackVm.Id, DestRackIdForDbUpdate = null },
                                }
                            });

                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = $"{selectedLine.Name}에 라이트 팔레트 드롭 후, LotNo., BoxCount 쓰기",
                                MissionType = "7",
                                FromNode = $"Work_308_Turn",
                                ToNode = $"Work_308_Drop",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkWaitTimeout = 3600,
                                PostMissionOperations = new List<MissionSubOperation> {
                                    new MissionSubOperation { Type = SubOperationType.McWriteLotNoBoxCount, Description = "LotNo., BoxCount 쓰기", WordDeviceCode = "W", McWordAddress = 0x1010, McStringLengthWords = 8, McProtocolIpAddress = mcProtocolIpAddress }
                                }
                            });
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = $"{selectedLine.Name}(으)로부터 나와서 안전센서 켜고, 경광등 끄기",
                                MissionType = "8",
                                ToNode = "Turn_308_Direct",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkWaitTimeout = 3600,
                                PostMissionOperations = new List<MissionSubOperation> { // LotNo., BoxCount refresh를 위해 경광등 끄기를 먼저 한다,
                                    new MissionSubOperation { Type = SubOperationType.McWaitSensorOn, Description = "안전 센서 켜기", WordDeviceCode = "W", McWordAddress = 0x1008, McWriteValueInt = 0, McProtocolIpAddress = mcProtocolIpAddress },
                                    new MissionSubOperation { Type = SubOperationType.McWriteSingleWord, Description = "경광등 끄기", WordDeviceCode = "W", McWordAddress = lightAddress, McWriteValueInt = 0, McProtocolIpAddress = mcProtocolIpAddress }
                                }
                            });
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = "작업 대기 장소로 이동",
                                MissionType = "8",
                                ToNode = "Pallet_BWD_Pos",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = false,
                                LinkWaitTimeout = 3600,
                            });
                            break;

                        case 6: // 카타르 A
                        case 7: // 카타르 B
                            // 1. Move from Charger, Turn
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = "대기 장소로 이동, 경광등 켜기",
                                MissionType = "7",
                                FromNode = "Pallet_BWD_Pos",
                                ToNode = "AMR2_Wait_Turn",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkWaitTimeout = 3600,
                                PostMissionOperations = new List<MissionSubOperation> // 경광등 켜기
                                {
                                    new MissionSubOperation { Type = SubOperationType.McWriteSingleWord, Description = "경광등 켜기", WordDeviceCode = "W", McWordAddress = lightAddress, McWriteValueInt = 1, McProtocolIpAddress = mcProtocolIpAddress }
                                }
                            });
                            // Step 2 : Move from chatger, Turn
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = $"{selectedLine.Name} 근처로 이동, 안전 센서 끄기",
                                MissionType = "8",
                                ToNode = $"Turn_{swapPoint}_Direct",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkWaitTimeout = 3600,
                                PostMissionOperations = new List<MissionSubOperation> // 안전 센서 OFF (1=OFF, 0=ON, 2=Quit)
                                {
                                    new MissionSubOperation { Type = SubOperationType.McWaitSensorOff, Description = "안전 센서 끄기", WordDeviceCode = "W", McWordAddress = 0x1008, McWriteValueInt = 1, McProtocolIpAddress = mcProtocolIpAddress }
                                }
                            });
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = $"{selectedLine.Name} 제품 팔레트 픽업, Lot 정보 읽기",
                                MissionType = "8",
                                ToNode = $"Work_{workPoint}_PickUP",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkWaitTimeout = 3600,
                                PostMissionOperations = new List<MissionSubOperation> // Lot 정보 읽기
                                {
                                    new MissionSubOperation { Type = SubOperationType.McReadLotNoBoxCount, Description = "LotNo., BoxCount 읽기", WordDeviceCode = "W", McWordAddress = lotInfoReadAddress, McStringLengthWords = 6, McProtocolIpAddress = mcProtocolIpAddress }
                                }
                            });
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = $"{selectedLine.Name}(으)로부터 나와서 안전 센서 켜기",
                                MissionType = "8",
                                ToNode = $"{swapPoint}_SENSOR",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkWaitTimeout = 3600,
                                PostMissionOperations = new List<MissionSubOperation> // 안전 센서 ON (1=OFF, 0=ON, 2=Quit)
                                {
                                    new MissionSubOperation { Type = SubOperationType.McWaitSensorOn, Description = "안전 센서 켜기", WordDeviceCode = "W", McWordAddress = 0x1008, McWriteValueInt = 0, McProtocolIpAddress = mcProtocolIpAddress },
                                }
                            });
                            // Rack에 적치하기 위한 이동 및 적치
                            if (inRackVm.LocationArea == 2 || inRackVm.LocationArea == 4) // 랙 2 ~ 8 번 1단 드롭 만 적용
                            {
                                missionSteps.Add(new MissionStepDefinition
                                {
                                    ProcessStepDescription = "팔레트 적재를 위한 이동 및 회전",
                                    MissionType = "7",
                                    FromNode = $"RACK_{inShelf}_STEP1",
                                    ToNode = $"RACK_{inShelf}_STEP2",
                                    Payload = _mainViewModel.ProductionLinePayload,
                                    IsLinkable = true,
                                    LinkWaitTimeout = 3600
                                });
                            }
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = $"{inRackVm.Title}(으)로 이동 & 팔레트 드롭",
                                MissionType = "8",
                                ToNode = $"Rack_{inShelf}_Drop",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkWaitTimeout = 3600,
                                PostMissionOperations = new List<MissionSubOperation> {
                                    //new MissionSubOperation { Type = SubOperationType.CheckModbusDiscreteInput, Description = "팔레트 랙에 안착 여부 확인", McDiscreteInputAddress = _mainViewModel._checkModbusDescreteInputAddr },
                                    new MissionSubOperation { Type = SubOperationType.DbWriteRackData, Description = "입고 팔레트 정보 업데이트", DestRackIdForDbUpdate = inRackVm.Id },
                                    new MissionSubOperation { Type = SubOperationType.ClearLotInformation, Description = "Lot 정보 표시 지우기" }
                                }
                            });
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = $"라이트 반출 장소({outRackVm.Title})로 이동하여, 라이트 팔레트 픽업",
                                MissionType = "8",
                                ToNode = $"Rack_{outShelf}_PickUP",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkWaitTimeout = 3600,
                                PostMissionOperations = new List<MissionSubOperation> {
                                    new MissionSubOperation { Type = SubOperationType.DbReadRackData, Description = "랙 데이터 읽어 오기", TargetRackId = outRackVm.Id },
                                    new MissionSubOperation { Type = SubOperationType.DbUpdateRackState, Description = "랙 상태 업데이트", SourceRackIdForDbUpdate = outRackVm.Id, DestRackIdForDbUpdate = null },
                                }
                            });
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = "라이트 팔레트 드롭 준비, 안전 센서 끄기",
                                MissionType = "8",
                                ToNode = $"Turn_{swapPoint}_Direct",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkWaitTimeout = 3600,
                                PostMissionOperations = new List<MissionSubOperation> // 공 파렛트 배출 완료 신호, 안전 센서 OFF (1=OFF, 0=ON, 2=Quit)
                            {
                                new MissionSubOperation { Type = SubOperationType.McWaitSensorOff, Description = "안전 센서 끄기", WordDeviceCode = "W", McWordAddress = 0x1008, McWriteValueInt = 1, McProtocolIpAddress = mcProtocolIpAddress }
                            }
                            });
                            // Step 9 : Move, Drop
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = $"{selectedLine.Name}에 라이트 팔레트 드롭 후, LotNo., BoxCount 쓰기",
                                MissionType = "8",
                                ToNode = $"Work_{workPoint}_Drop",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkWaitTimeout = 3600,
                                PostMissionOperations = new List<MissionSubOperation> {
                                    new MissionSubOperation { Type = SubOperationType.McWriteLotNoBoxCount, Description = "LotNo., BoxCount 쓰기", WordDeviceCode = "W", McWordAddress = 0x1010, McStringLengthWords = 8, McProtocolIpAddress = mcProtocolIpAddress }
                                }
                            });
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = $"{selectedLine.Name}(으)로부터 나와서 안전 센서 켜기",
                                MissionType = "8",
                                ToNode = $"{swapPoint}_SENSOR",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkWaitTimeout = 3600,
                                PostMissionOperations = new List<MissionSubOperation> // 안전 센서 Quit (1=OFF, 0=ON, 2=Quit)
                                {
                                    new MissionSubOperation { Type = SubOperationType.McWaitSensorOn, Description = "안전 센서 켜기", WordDeviceCode = "W", McWordAddress = 0x1008, McWriteValueInt = 0, McProtocolIpAddress = mcProtocolIpAddress }
                                }
                            });
                            // Step 11 : Move, Charge
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = "작업 대기 장소로 이동, 작업완료 요청",
                                MissionType = "8",
                                ToNode = "Pallet_BWD_Pos",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = false,
                                LinkWaitTimeout = 3600,
                                PostMissionOperations = new List<MissionSubOperation> // 안전 센서 Quit (1=OFF, 0=ON, 2=Quit), 경광등 끄기
                                {
                                    new MissionSubOperation { Type = SubOperationType.McWaitSensorOn, Description = "안전 센서 종료", WordDeviceCode = "W", McWordAddress = 0x1008, McWriteValueInt = 2, McProtocolIpAddress = mcProtocolIpAddress },
                                    new MissionSubOperation { Type = SubOperationType.McWriteSingleWord, Description = "경광등 끄기", WordDeviceCode = "W", McWordAddress = lightAddress, McProtocolIpAddress = mcProtocolIpAddress, McWriteValueInt = 2 }
                                }
                            });
                            break;

                        case 8: // 특수포장
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = "대기 장소로 이동",
                                MissionType = "7",
                                FromNode = "Pallet_BWD_Pos",
                                ToNode = "AMR2_Wait_Turn",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkWaitTimeout = 3600
                            });
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = $"{selectedLine.Name}(으)로 이동하여, 제품 팔레트 픽업",
                                MissionType = "8",
                                ToNode = $"Work_{workPoint}_PickUP",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkedMission = null,
                                LinkWaitTimeout = 3600
                            });
                            // Rack에 적치하기 위한 이동 및 적치
                            if (inRackVm.LocationArea == 2 || inRackVm.LocationArea == 4) // 랙 2 ~ 8 번 1단 드롭 만 적용
                            {
                                missionSteps.Add(new MissionStepDefinition
                                {
                                    ProcessStepDescription = "팔레트 적재를 위한 이동 및 회전",
                                    MissionType = "7",
                                    FromNode = $"RACK_{inShelf}_STEP1",
                                    ToNode = $"RACK_{inShelf}_STEP2",
                                    Payload = _mainViewModel.ProductionLinePayload,
                                    IsLinkable = true,
                                    LinkWaitTimeout = 3600
                                });
                            }
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = $"{inRackVm.Title}(으)로 이동 & 팔레트 드롭",
                                MissionType = "8",
                                ToNode = $"Rack_{inShelf}_Drop",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkWaitTimeout = 3600,
                                PostMissionOperations = new List<MissionSubOperation> {
                                    //new MissionSubOperation { Type = SubOperationType.CheckModbusDiscreteInput, Description = "팔레트 랙에 안착 여부 확인", McDiscreteInputAddress = _mainViewModel._checkModbusDescreteInputAddr },
                                    new MissionSubOperation { Type = SubOperationType.DbWriteRackData, Description = "입고 팔레트 정보 업데이트", DestRackIdForDbUpdate = inRackVm.Id }
                                }
                            });

                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = $"라이트 반출 장소({outRackVm.Title})로 이동하여, 라이트 팔레트 픽업",
                                MissionType = "8",
                                ToNode = $"Rack_{outShelf}_PickUP",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkWaitTimeout = 3600,
                                PostMissionOperations = new List<MissionSubOperation> {
                                    //new MissionSubOperation { Type = SubOperationType.DbReadRackData, Description = "랙 데이터 읽어 오기", TargetRackId = outRackVm.Id },
                                    new MissionSubOperation { Type = SubOperationType.DbUpdateRackState, Description = "랙 상태 업데이트", SourceRackIdForDbUpdate = outRackVm.Id, DestRackIdForDbUpdate = null },
                                    //new MissionSubOperation { Type = SubOperationType.SetPlcStatusIsPaused, Description = "콜버튼 액션 허용", PauseButtonCallPlcStatus = false }
                                }
                            });

                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = $"{selectedLine.Name}(으)로 이동하여, 라이트 팔레트 드롭",
                                MissionType = "8",
                                //FromNode = "AMR2_Wait_Turn",
                                ToNode = $"Work_{workPoint}_Drop",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = true,
                                LinkedMission = null,
                                LinkWaitTimeout = 3600
                            });
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = "작업 대기 장소로 이동",
                                MissionType = "8",
                                ToNode = "Pallet_BWD_Pos",
                                Payload = _mainViewModel.ProductionLinePayload,
                                IsLinkable = false,
                                LinkWaitTimeout = 3600,
                            });
                            break;

                        default:
                            break;
                    }

                    try
                    {
                        // 로봇 미션 프로세스 시작
                        string processId = await _mainViewModel.InitiateRobotMissionProcess(
                            "라이트 반출 작업", // 미션 프로세스 유형
                            missionSteps,
                            lockedRackIds, // 잠긴 랙 ID 목록 전달
                            null, // racksToProcess
                            coilAddress, // initiatingCoilAddress
                            false // isWarehouseMission = true로 전달
                        );
                        ShowAutoClosingMessage($"로봇 미션 프로세스 시작됨: {processId}");

                        var modbuttonVm = _mainViewModel.ModbusButtons.FirstOrDefault(b => b.CoilOutputAddress == coilAddress);
                        modbuttonVm.IsProcessing = true;
                        await _mainViewModel.ExecuteModbusButtonCommand(modbuttonVm);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"반제품 반출 로봇 미션 시작 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        foreach (var id in lockedRackIds)
                        {
                            await _databaseService.UpdateIsLockedAsync(id, false);
                            Application.Current.Dispatcher.Invoke(() => (_mainViewModel.RackList?.FirstOrDefault(r => r.Id == id)).IsLocked = false);
                        }
                    }
                }
                else
                {
                    ShowAutoClosingMessage("라이트 반출장소가 선택되지 않았습니다."); // It never happens
                }
            }
            else
            {
                ShowAutoClosingMessage("라이트 팔레트 반출작업이 취소되었습니다.");
            }
        }

        private async Task HandleRackTransfer(RackViewModel sourceRackViewModel) // 기존 HandleRackTransfer (ImageIndex 27-38용)
        {
            bool inputContainsValidLotNumber = !string.IsNullOrWhiteSpace(_mainViewModel.InputStringForButton) &&
                                             (_mainViewModel.InputStringForButton.Contains("223A")
                                             || _mainViewModel.InputStringForButton.Contains("223SP")
                                              || _mainViewModel.InputStringForButton.Contains("223XM")
                                               || _mainViewModel.InputStringForButton.Contains("5.56X")
                                                || _mainViewModel.InputStringForButton.Contains("5.56K")
                                                 || _mainViewModel.InputStringForButton.Contains("PSD")
                                                  || _mainViewModel.InputStringForButton.Contains("308B")
                                                   || _mainViewModel.InputStringForButton.Contains("308SP")
                                                    || _mainViewModel.InputStringForButton.Contains("308XM")
                                                     || _mainViewModel.InputStringForButton.Contains("7.62X")
                                             );
            if(!inputContainsValidLotNumber)
            {
                MessageBox.Show("WRAP 입고 불가 : Lot 번호를 확인해 주세요.", "입고 불가", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            List<Rack> allRacks = await _databaseService.GetRackStatesAsync();
            // 🚨 수정할 부분: IsLocked가 false이면서 ImageIndex가 3인 랙만 필터링
            List<Rack> targetRacks = allRacks
                .Where(r => r.Id != sourceRackViewModel.Id // 자기 자신 제외
                            && r.IsVisible
                            && !r.IsLocked                 // 잠겨있지 않은 랙만
                            && r.ImageIndex == 13          // ImageIndex가 13인 랙만 (RackType 1, BulletType 0)
                            && !r.Title.Equals("AMR"))
                .ToList();
            if (!targetRacks.Any())
            {
                MessageBox.Show("완제품을 보관할 랙이 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 랙 선택 팝업 표시 (이름 변경)
            var selectPopupViewModel = new SelectStorageRackPopupViewModel(targetRacks,
                    _rackModel.Title.Equals("WRAP") ? _mainViewModel.InputStringForButton.TrimStart() ?? "" : _rackModel.LotNumber);
            var selectPopupView = new SelectStorageRackPopupView { DataContext = selectPopupViewModel };
            selectPopupView.Title = $"랙 {sourceRackViewModel.Title} 의 제품 이동";

            if (selectPopupView.ShowDialog() == true && selectPopupViewModel.SelectedRack != null)
            {
                Rack destinationRack = selectPopupViewModel.SelectedRack;
                List<int> lockedRackIds = new List<int>();

                // 1) 기존 랙 (sourceRack)과 대상 랙 (destinationRack)을 DB에서 잠금
                ShowAutoClosingMessage($"랙 {sourceRackViewModel.Title} 에서 랙 {destinationRack.Title} 로 이동을 시작합니다. 잠금 중...");
                try
                {
                    await _databaseService.UpdateIsLockedAsync(sourceRackViewModel.Id, true); // source 랙 잠금
                    Application.Current.Dispatcher.Invoke(() => sourceRackViewModel.IsLocked = true);
                    lockedRackIds.Add(sourceRackViewModel.Id);

                    await _databaseService.UpdateIsLockedAsync(destinationRack.Id, true); // destination 랙 잠금
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var destRackVm = _mainViewModel.RackList?.FirstOrDefault(r => r.Id == destinationRack.Id);
                        if (destRackVm != null)
                        {
                            destRackVm.IsLocked = true;
                            lockedRackIds.Add(destinationRack.Id);
                        }
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"랙 잠금 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    Debug.WriteLine($"[RackViewModel] Error locking racks: {ex.Message}");
                    // 오류 발생 시 작업 취소 및 잠금 해제 시도
                    foreach (var id in lockedRackIds)
                    {
                        await _databaseService.UpdateIsLockedAsync(id, false);
                        Application.Current.Dispatcher.Invoke(() => (_mainViewModel.RackList?.FirstOrDefault(r => r.Id == id)).IsLocked = false);
                    }
                    return; // 더 이상 진행하지 않음
                }

                var amrRackViewModel = _mainViewModel.RackList?.FirstOrDefault(r => r.Title.Equals("AMR"));

                ShowAutoClosingMessage($"로봇 미션: 랙 {sourceRackViewModel.Title} 에서 랙 {destinationRack.Title}(으)로 이동 시작. 명령 전송 중...");
                // 이 값은 원본 랙의 BulletType이 0으로 변경되기 전에 가져와야 합니다.
                int originalSourceBulletType = sourceRackViewModel.RackModel.BulletType;
                string originalSourceLotNumber = sourceRackViewModel.LotNumber; // LotNumber도 미리 저장

                List<MissionStepDefinition> missionSteps;
                string shelf = $"{int.Parse(destinationRack.Title.Split('-')[0]):D2}_{destinationRack.Title.Split('-')[1]}";

                // 랩핑기에서 포장괸 팔레트 픽업
                missionSteps = new List<MissionStepDefinition>
                {
                    // 1. Move, Pickup
                    new MissionStepDefinition {
                        ProcessStepDescription = "래핑기로 이동하여, 포장된 팔레트 픽업",
                        MissionType = "8",
                        ToNode = $"Wrapping_PickUP",
                        Payload = _mainViewModel.WarehousePayload,
                        IsLinkable = true,
                        LinkWaitTimeout = 3600,
                        PostMissionOperations = new List<MissionSubOperation> {
                            new MissionSubOperation { Type = SubOperationType.DbUpdateRackState, Description = "랙 상태 업데이트", SourceRackIdForDbUpdate = sourceRackViewModel.Id, DestRackIdForDbUpdate = amrRackViewModel.Id }
                        }
                    }
                };

                // 포장된 팔레트를 선택된 랙에 드롭
                if (destinationRack.LocationArea == 4 || destinationRack.LocationArea == 2) // 랙 2 ~ 8번 1단 드롭 만 적용
                {
                    missionSteps.Add(new MissionStepDefinition
                    {
                        ProcessStepDescription = "포장된 팔레트 적재를 위한 이동 및 회전",
                        MissionType = "7",
                        FromNode = $"RACK_{shelf}_STEP1",
                        ToNode = $"RACK_{shelf}_STEP2",
                        Payload = _mainViewModel.WarehousePayload,
                        IsLinkable = true,
                        LinkWaitTimeout = 3600
                    });
                }
                missionSteps.Add(new MissionStepDefinition
                {
                    ProcessStepDescription = $"{destinationRack.Title}(으)로 이동 & 포장된 팔레트 드롭",
                    MissionType = "8",
                    ToNode = $"Rack_{shelf}_Drop",
                    Payload = _mainViewModel.WarehousePayload,
                    IsLinkable = true,
                    LinkWaitTimeout = 3600,
                    PostMissionOperations = new List<MissionSubOperation> {
                        //new MissionSubOperation { Type = SubOperationType.CheckModbusDiscreteInput, Description = "팔레트 랙에 안착 여부 확인", McDiscreteInputAddress = _mainViewModel._checkModbusDescreteInputAddr },
                        new MissionSubOperation { Type = SubOperationType.DbUpdateRackState, Description = "랙 상태 업데이트", SourceRackIdForDbUpdate = amrRackViewModel.Id, DestRackIdForDbUpdate = destinationRack.Id },
                        new MissionSubOperation { Type = SubOperationType.DbInsertInboundData, Description = "입고 장부 기입", DestRackIdForDbUpdate = destinationRack.Id }
                    }
                });

                missionSteps.Add(new MissionStepDefinition
                {
                    ProcessStepDescription = $"대기 장소로 복귀",
                    MissionType = "8",
                    ToNode = "AMR1_WAIT",
                    Payload = _mainViewModel.WarehousePayload,
                    IsLinkable = false,
                    LinkWaitTimeout = 3600
                });

                try
                {
                    // MainViewModel을 통해 로봇 미션 프로세스 시작
                    // LinkedMission은 MainViewModel의 SendNextRobotMissionInProcess에서 처리될 것입니다.
                    string processId = await _mainViewModel.InitiateRobotMissionProcess(
                        "완제품 입고 작업", // 미션 프로세스 유형
                        missionSteps,
                        lockedRackIds, // 잠긴 랙 ID 목록 전달
                        null, // racksToProcess
                        null, // initiatingCoilAddress
                        true // isWarehouseMission = true로 전달
                    );
                    Debug.WriteLine($"[RackViewModel] Robot mission process '{processId}' initiated for transfer from {sourceRackViewModel.Title} to WRAP.");
                    ShowAutoClosingMessage($"로봇 미션 프로세스 시작됨: {processId}");
                    // **중요**: 로봇 미션이 시작되었으므로, 이 시점에서는 랙의 잠금 상태만 유지하고,
                    // 실제 DB 업데이트 (비우기, 채우기)는 MainViewModel의 폴링 로직 (RobotMissionPollingTimer_Tick)에서
                    // 미션 완료 시점(`HandleRobotMissionCompletion`)에 이루어지도록 위임합니다.
                    // 따라서 여기에 있던 10초 딜레이 및 직접적인 DB 업데이트 로직은 삭제합니다.
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"포장제품 입고 로봇 미션 시작 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    Debug.WriteLine($"[RackViewModel] Error initiating robot mission: {ex.Message}");
                    foreach (var id in lockedRackIds)
                    {
                        await _databaseService.UpdateIsLockedAsync(id, false);
                        Application.Current.Dispatcher.Invoke(() => (_mainViewModel.RackList?.FirstOrDefault(r => r.Id == id)).IsLocked = false);
                    }
                }
            }
            else
            {
                ShowAutoClosingMessage("포장된 팔레트 적재 작업이 취소되었습니다.");
            }
        }

        // 새롭게 추가할 출고 로직 (이전과 동일하게 유지)
        private async Task HandleRackShipout(RackViewModel targetRackViewModel)
        {
            var confirmPopupViewModel = new ConfirmShipoutPopupViewModel(targetRackViewModel.Title, targetRackViewModel.BulletType, targetRackViewModel.LotNumber);
            var confirmPopupView = new ConfirmShipoutPopupView { DataContext = confirmPopupViewModel };

            if (confirmPopupView.ShowDialog() == true && confirmPopupViewModel.DialogResult == true)
            {
                ShowAutoClosingMessage($"랙 {targetRackViewModel.Title} 출고 작업을 시작합니다. 잠금 중...");
                List<int> lockedRackIds = new List<int>();
                try
                {
                    // 랙 잠금 및 비동기 작업 시작
                    await _databaseService.UpdateIsLockedAsync(targetRackViewModel.Id, true);
                    Application.Current.Dispatcher.Invoke(() => targetRackViewModel.IsLocked = true);
                    lockedRackIds.Add(targetRackViewModel.Id);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"랙 잠금 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    // 오류 발생 시 작업 취소 및 잠금 해제 시도
                    foreach (var id in lockedRackIds)
                    {
                        await _databaseService.UpdateIsLockedAsync(id, false);
                        Application.Current.Dispatcher.Invoke(() => (_mainViewModel.RackList?.FirstOrDefault(r => r.Id == id)).IsLocked = false);
                    }
                    return; // 더 이상 진행하지 않음
                }

                var amrRackViewModel = _mainViewModel.RackList?.FirstOrDefault(r => r.Title.Equals("AMR"));

                ShowAutoClosingMessage($"로봇 미션: 랙({targetRackViewModel.Title})에서 출고 작업 시작. 명령 전송 중...");
                List<MissionStepDefinition> missionSteps = new List<MissionStepDefinition>();
                string shelf = $"{int.Parse(targetRackViewModel.Title.Split('-')[0]):D2}_{targetRackViewModel.Title.Split('-')[1]}";
                int? insertedInID = targetRackViewModel.InsertedIn;

                // 로봇 미션 단계 정의 (사용자 요청에 따라 4단계로 복원 및 IsLinkable, LinkedMission 조정)
                missionSteps.Add(new MissionStepDefinition
                {
                    ProcessStepDescription = $"랙 {targetRackViewModel.Title}로 이동(으)로 이동하여, 제품 팔레트 픽업",
                    MissionType = "8",
                    ToNode = $"Rack_{shelf}_PickUP",
                    Payload = _mainViewModel.WarehousePayload,
                    IsLinkable = true,
                    LinkedMission = null,
                    LinkWaitTimeout = 3600,
                    PostMissionOperations = new List<MissionSubOperation> {
                        new MissionSubOperation { Type = SubOperationType.DbUpdateRackState, Description = "랙 상태 업데이트", SourceRackIdForDbUpdate = targetRackViewModel.Id, DestRackIdForDbUpdate = amrRackViewModel.Id }
                    }
                });
                // 3. Move, Drop, Check, Update DB

                missionSteps.Add(new MissionStepDefinition
                {
                    ProcessStepDescription = $"출고 위치 {_mainViewModel.outletPosition / 5 + 1}_{_mainViewModel.outletPosition % 5 + 1} 로 이동하여, 팔레트 드롭",
                    MissionType = "8",
                    //ToNode = $"WaitProduct_{_mainViewModel.outletPosition + 1}_Drop",
                    ToNode = $"test_drop_Rack_{_mainViewModel.outletPosition / 5 + 1}_{_mainViewModel.outletPosition % 5 + 1}",
                    Payload = _mainViewModel.WarehousePayload,
                    IsLinkable = true,
                    LinkedMission = null,
                    LinkWaitTimeout = 3600,
                    PostMissionOperations = new List<MissionSubOperation> {
                        new MissionSubOperation { Type = SubOperationType.DbUpdateRackState, Description = "랙 상태 업데이트", SourceRackIdForDbUpdate = amrRackViewModel.Id, DestRackIdForDbUpdate = null },
                        new MissionSubOperation { Type = SubOperationType.DbUpdateOutboundData, Description = "출고 장부 기입", SourceRackIdForDbUpdate = insertedInID} // SourceRackIdForDbUpdate를 int 전달을 위해 차용
                    }
                });
                /*missionSteps.Add(new MissionStepDefinition
                {
                    ProcessStepDescription = "창고 진입을 위한 이동, 회전",
                    MissionType = "7",
                    FromNode = "WAIT_TURN",
                    ToNode = "AMR1_RACK_Turn",
                    Payload = _mainViewModel.WarehousePayload,
                    IsLinkable = true,
                    LinkedMission = null,
                    LinkWaitTimeout = 3600
                });*/
                missionSteps.Add(new MissionStepDefinition {
                        ProcessStepDescription = $"대기 장소로 복귀",
                        MissionType = "8",
                        ToNode = "AMR1_WAIT",
                        Payload = _mainViewModel.WarehousePayload,
                        IsLinkable = false,
                        LinkedMission = null,
                        LinkWaitTimeout = 3600
                });

                _mainViewModel.outletPosition++;
                if (_mainViewModel.outletPosition >= _mainViewModel.MAXOUTLETS)
                    _mainViewModel.outletPosition = 0;

                try
                {
                    // MainViewModel을 통해 로봇 미션 프로세스 시작 (이제 MainViewModel이 RobotMissionService로 위임)
                    string processId = await _mainViewModel.InitiateRobotMissionProcess(
                        "단일 출고 작업", // 미션 프로세스 유형
                        missionSteps,
                        lockedRackIds, // 잠긴 랙 ID 목록 전달 
                        null, // racksToProcess
                        null, // initiatingCoilAddress
                        true // isWarehouseMission = true로 전달
                    );
                    Debug.WriteLine($"[RackViewModel] Robot mission process '{processId}' initiated for transfer from {targetRackViewModel.Title} to WRAP.");
                    ShowAutoClosingMessage($"로봇 미션 프로세스 시작됨: {processId}");
                    // **중요**: 로봇 미션이 시작되었으므로, 이 시점에서는 랙의 잠금 상태만 유지하고,
                    // 실제 DB 업데이트 (비우기, 채우기)는 RobotMissionService의 폴링 로직에서
                    // 미션 완료 시점(`HandleRobotMissionCompletion`)에 이루어지도록 위임합니다.
                    // 따라서 여기에 있던 10초 딜레이 및 직접적인 DB 업데이트 로직은 삭제합니다.
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"제품 출고 로봇 미션 시작 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    Debug.WriteLine($"[RackViewModel] Error initiating robot mission: {ex.Message}");
                    // 미션 시작 실패 시 랙 잠금 해제
                    foreach (var id in lockedRackIds)
                    {
                        await _databaseService.UpdateIsLockedAsync(id, false);
                        Application.Current.Dispatcher.Invoke(() => (_mainViewModel.RackList?.FirstOrDefault(r => r.Id == id)).IsLocked = false);
                    }
                }
            }
            else
            {
                ShowAutoClosingMessage("랙 출고 작업이 취소되었습니다.");
            }
        }

        private bool CanClickRack(object parameter)
        {
            return (!IsLocked); // 'locked' 상태가 아닐 때만 클릭 가능하도록 설정
        }

        private void ShowAutoClosingMessage(string message)
        {
            _mainViewModel.ShowAutoClosingMessage(message);
/*            Application.Current.Dispatcher.Invoke(() =>
            {
                var viewModel = new AutoClosingMessagePopupViewModel(message);
                var view = new AutoClosingMessagePopupView { DataContext = viewModel };
                view.Show();
            });*/
        }
    }
}
