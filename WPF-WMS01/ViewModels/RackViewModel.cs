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
            else if (e.PropertyName == nameof(Models.Rack.LotNumber)) // LotNumber 변경 시 알림 추가
            {
                OnPropertyChanged(nameof(LotNumber));
            }
            else if (e.PropertyName == nameof(Models.Rack.RackedAt)) // RackedAt 변경 시 알림 추가
            {
                OnPropertyChanged(nameof(RackedAt));
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

        public int ImageIndex => _rackModel.ImageIndex;

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
            var clickedRackViewModel = parameter as RackViewModel;
            if (clickedRackViewModel == null) return;

            // ImageIndex 값에 따라 다른 팝업 창 띄우기
            switch (ImageIndex)
            {
                case 0:
                case 13:
                    if (clickedRackViewModel.Title.Equals("WAIT"))
                        break;
                    // 랙 타입 변경 팝업
                    int currentRackType = clickedRackViewModel.RackModel.RackType; // 현재 모델의 타입 읽기
                    int newRackType = (currentRackType == 0) ? 1 : 0; // 0과 1 사이 토글

                    var popupViewModel = new RackTypeChangePopupViewModel(currentRackType, newRackType);
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
                case int i when i >= 1 && i <= 12: // ImageIndex가 1에서 12 사이인 경우
                    await HandleTransferToWrapRack(clickedRackViewModel); // WRAP 랙으로 이동 로직 호출
                    break;
                case 26:
                    break;
                case int i when i >= 27 && i <= 38: // ImageIndex가 27에서 38 사이인 경우, WRAP rack click
                    await HandleRackTransfer(clickedRackViewModel); // 기존 이동/복사 로직 호출
                    break;
                case int i when i >= 14 && i <= 25:
                    await HandleRackShipout(clickedRackViewModel);
                    break;
                case int i when i >= 40 && i <= 51:
                    await HandleHalfPalletExport(clickedRackViewModel);
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
                ShowAutoClosingMessage("이동할 'WRAP' 랙을 찾을 수 없습니다.");
                return;
            }

            // "WRAP" 랙의 상태 확인
            if (wrapRackViewModel.BulletType != 0 || wrapRackViewModel.IsLocked)
            {
                ShowAutoClosingMessage("'WRAP' 랙이 이미 사용 중이거나 잠겨있어 이동할 수 없습니다.");
                return;
            }

            // 사용자에게 이동 여부 확인 팝업
            var confirmViewModel = new ConfirmTransferPopupViewModel(
                sourceRackViewModel.Title, sourceRackViewModel.LotNumber, "WRAP"
            );
            var confirmView = new ConfirmTransferPopupView { DataContext = confirmViewModel };
            bool? confirmResult = confirmView.ShowDialog();

            // ViewModel의 DialogResult 속성 대신 Window.ShowDialog()의 반환 값만 확인
            if (confirmResult == true)
            {
                // 1) 원본 랙과 WRAP 랙 잠금
                ShowAutoClosingMessage($"랙 {sourceRackViewModel.Title} 에서 'WRAP' 랙으로 이동을 시작합니다. 잠금 중...");
                await _databaseService.UpdateRackStateAsync(sourceRackViewModel.Id, sourceRackViewModel.RackModel.RackType, sourceRackViewModel.RackModel.BulletType, true);
                await _databaseService.UpdateRackStateAsync(wrapRackViewModel.Id, wrapRackViewModel.RackModel.RackType, wrapRackViewModel.RackModel.BulletType, true);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    sourceRackViewModel.IsLocked = true;
                    wrapRackViewModel.IsLocked = true;
                });

                ShowAutoClosingMessage($"랙 {sourceRackViewModel.Title} 에서 'WRAP' 랙으로 이동 중입니다. 10초 대기...");

                await Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(10)); // 10초 지연 시뮬레이션

                    try
                    {
                        // 2) WRAP 랙으로 제품 정보 이동
                        await _databaseService.UpdateRackStateAsync(
                            wrapRackViewModel.Id,
                            wrapRackViewModel.RackType,
                            sourceRackViewModel.BulletType, // 원본 랙의 제품 타입 복사
                            false // 잠금 해제
                        );
                        await _databaseService.UpdateLotNumberAsync(
                            wrapRackViewModel.Id,
                            sourceRackViewModel.LotNumber // 원본 랙의 LotNumber 복사
                        );

                        // 3) 원본 랙 비우기
                        await _databaseService.UpdateRackStateAsync(
                            sourceRackViewModel.Id,
                            sourceRackViewModel.RackType,
                            0, // BulletType을 0으로 설정 (비움)
                            false // 잠금 해제
                        );
                        await _databaseService.UpdateLotNumberAsync(
                            sourceRackViewModel.Id,
                            String.Empty // LotNumber 비움
                        );

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ShowAutoClosingMessage($"랙 {sourceRackViewModel.Title} 에서 'WRAP' 랙으로의 이동이 완료되었습니다.");
                            // UI ViewModel 업데이트는 DB 업데이트 시 MainViewModel에서 RefreshTimer_Tick을 통해 자동으로 이루어질 것입니다.
                            // 하지만 즉각적인 반영을 위해 명시적으로 업데이트할 수도 있습니다.
                            // sourceRackViewModel.BulletType = 0;
                            // sourceRackViewModel.LotNumber = String.Empty;
                            // wrapRackViewModel.BulletType = originalSourceBulletType;
                            // wrapRackViewModel.LotNumber = originalSourceLotNumber;
                        });
                    }
                    catch (Exception ex)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"랙 이동 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                    finally
                    {
                        // 오류 발생 시에도 잠금 해제 (최종적으로 보장)
                        Application.Current.Dispatcher.Invoke(async () =>
                        {
                            await _databaseService.UpdateRackStateAsync(sourceRackViewModel.Id, sourceRackViewModel.RackModel.RackType, sourceRackViewModel.RackModel.BulletType, false);
                            await _databaseService.UpdateRackStateAsync(wrapRackViewModel.Id, wrapRackViewModel.RackModel.RackType, wrapRackViewModel.RackModel.BulletType, false);
                            sourceRackViewModel.IsLocked = false;
                            wrapRackViewModel.IsLocked = false;
                        });
                    }
                });
            }
            else
            {
                ShowAutoClosingMessage("랙 이동 작업이 취소되었습니다.");
                // 취소 시 원본 랙 잠금 해제 (만약 작업 시작 전에 잠겼다면)
                await _databaseService.UpdateRackStateAsync(sourceRackViewModel.Id, sourceRackViewModel.RackModel.RackType, sourceRackViewModel.RackModel.BulletType, false);
                Application.Current.Dispatcher.Invoke(() => sourceRackViewModel.IsLocked = false);
            }
        }


        // 반 팔레트 반출을 처리하는 비동기 메서드 (이전과 동일하게 유지)
        private async Task HandleHalfPalletExport(RackViewModel sourceRackViewModel)
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
                    ShowAutoClosingMessage($"랙 {sourceRackViewModel.Title}의 재공품을 '{selectedLine.Name}'(으)로 반출합니다. 잠금 중...");

                    // 1) 기존 랙 (sourceRack)을 DB에서 잠금
                    await _databaseService.UpdateRackStateAsync(sourceRackViewModel.Id, sourceRackViewModel.RackModel.RackType, sourceRackViewModel.RackModel.BulletType, true); // source 랙 잠금
                    Application.Current.Dispatcher.Invoke(() => sourceRackViewModel.IsLocked = true);


                    ShowAutoClosingMessage($"랙 {sourceRackViewModel.Title} 재공품을 '{selectedLine.Name}'(으)로 반출 중입니다. 10초 대기...");

                    // 2) 별도 스레드에서 지연 및 데이터 업데이트 (시뮬레이션)
                    await Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10)); // 10초 지연

                        try
                        {
                            // 4) 원본 랙 (sourceRack)의 BulletType을 0으로 설정하여 '비움'
                            // (원래 랙은 RackType을 유지하면서 BulletType만 0으로)
                            await _databaseService.UpdateRackStateAsync(
                                sourceRackViewModel.Id,
                                sourceRackViewModel.RackModel.RackType, // 원본 랙의 RackType은 유지
                                0,                                      // 원본 랙의 BulletType을 0으로 설정
                                false                                   // IsLocked 해제
                            );
                            await _databaseService.UpdateLotNumberAsync(sourceRackViewModel.Id, String.Empty);

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                ShowAutoClosingMessage($"랙 {sourceRackViewModel.Title} 에서 '{selectedLine.Name}'(으)로 반출이 완료되었습니다.");
                            });
                        }
                        catch (Exception ex)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"반출 작업 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                            });
                        }
                        finally
                        {
                            // 오류 발생 시에도 잠금 해제 (최종적으로 보장)
                            Application.Current.Dispatcher.Invoke(async () =>
                            {
                                await _databaseService.UpdateRackStateAsync(sourceRackViewModel.Id, sourceRackViewModel.RackModel.RackType, sourceRackViewModel.RackModel.BulletType, false);
                                sourceRackViewModel.IsLocked = false;
                            });
                        }
                    });
                }
                else
                {
                    ShowAutoClosingMessage("재공품 반출작업이 취소되었습니다.");
                    // 팝업이 닫히거나, 선택된 랙이 없으면 취소.
                    // 잠갔던 sourceRackViewModel.IsLocked = true; 를 다시 false로 되돌려야 합니다.
                    // 이 역시 DatabaseService를 통해 다시 업데이트
                    await _databaseService.UpdateRackStateAsync(sourceRackViewModel.Id, sourceRackViewModel.RackModel.RackType, sourceRackViewModel.RackModel.BulletType, false);
                    Application.Current.Dispatcher.Invoke(() => sourceRackViewModel.IsLocked = false);
                }
            }
        }

        private async Task HandleRackTransfer(RackViewModel sourceRackViewModel) // 기존 HandleRackTransfer (ImageIndex 27-38용)
        {
            List<Rack> allRacks = await _databaseService.GetRackStatesAsync();
            // 🚨 수정할 부분: IsLocked가 false이면서 ImageIndex가 3인 랙만 필터링
            List<Rack> targetRacks = allRacks
                .Where(r => r.Id != sourceRackViewModel.Id && // 자기 자신 제외
                            !r.IsLocked &&                     // 잠겨있지 않은 랙만
                            r.ImageIndex == 13)                 // ImageIndex가 13인 랙만 (RackType 1, BulletType 0)
                .ToList();
            if (!targetRacks.Any())
            {
                MessageBox.Show("이동할 (비어있는 보관)랙이 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // 랙 선택 팝업 표시 (이름 변경)
            var selectPopupViewModel = new SelectStorageRackPopupViewModel(targetRacks,
                    _rackModel.Title.Equals("WAIT") ? _mainViewModel.InputStringForButton.TrimStart().TrimEnd(_mainViewModel._militaryCharacter) : _rackModel.LotNumber);
            var selectPopupView = new SelectStorageRackPopupView { DataContext = selectPopupViewModel };
            selectPopupView.Title = $"랙 {sourceRackViewModel.Title} 의 제품 이동";

            if (selectPopupView.ShowDialog() == true && selectPopupViewModel.SelectedRack != null)
            {
                Rack destinationRack = selectPopupViewModel.SelectedRack;

                // 1) 기존 랙 (sourceRack)과 대상 랙 (destinationRack)을 DB에서 잠금
                ShowAutoClosingMessage($"랙 {sourceRackViewModel.Title} 에서 랙 {destinationRack.Title} 로 이동을 시작합니다. 잠금 중...");
                await _databaseService.UpdateRackStateAsync(sourceRackViewModel.Id, sourceRackViewModel.RackModel.RackType, sourceRackViewModel.RackModel.BulletType, true); // source 랙 잠금
                await _databaseService.UpdateRackStateAsync(destinationRack.Id, destinationRack.RackType, destinationRack.BulletType, true); // destination 랙 잠금

                Application.Current.Dispatcher.Invoke(() =>
                {
                    sourceRackViewModel.IsLocked = true;
                    // destinationRack은 RackViewModel이 아니므로 직접 IsLocked 속성을 가지고 있지 않습니다.
                    // MainViewModel에서 RackList를 업데이트하면 UI가 자동으로 반영되거나,
                    // destinationRack에 해당하는 RackViewModel을 찾아 IsLocked를 업데이트해야 합니다.
                    // 현재는 MainViewModel의 RefreshTimer_Tick이 알아서 갱신할 것으로 가정합니다.
                    // 그러나 즉각적인 UI 반영을 위해 해당 RackViewModel을 찾아 잠금 설정하는 것이 좋습니다.
                    var destRackVm = _mainViewModel.RackList?.FirstOrDefault(r => r.Id == destinationRack.Id);
                    if (destRackVm != null)
                    {
                        destRackVm.IsLocked = true;
                    }
                });


                ShowAutoClosingMessage($"랙 {sourceRackViewModel.Title} 에서 랙 {destinationRack.Title} 로 이동 중입니다. 10초 대기...");
                // 이 값은 원본 랙의 BulletType이 0으로 변경되기 전에 가져와야 합니다.
                int originalSourceBulletType = sourceRackViewModel.RackModel.BulletType;
                string originalSourceLotNumber = sourceRackViewModel.LotNumber; // LotNumber도 미리 저장

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
                            await _databaseService.UpdateLotNumberAsync(destinationRack.Id, _mainViewModel.InputStringForButton.TrimStart().TrimEnd(_mainViewModel._militaryCharacter));
                        else
                            await _databaseService.UpdateLotNumberAsync(destinationRack.Id, originalSourceLotNumber); // 미리 저장해둔 LotNumber 사용

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
                    finally
                    {
                        // 오류 발생 시에도 잠금 해제 (최종적으로 보장)
                        Application.Current.Dispatcher.Invoke(async () =>
                        {
                            await _databaseService.UpdateRackStateAsync(sourceRackViewModel.Id, sourceRackViewModel.RackModel.RackType, sourceRackViewModel.RackModel.BulletType, false);
                            sourceRackViewModel.IsLocked = false;

                            var destRackVm = _mainViewModel.RackList?.FirstOrDefault(r => r.Id == destinationRack.Id);
                            if (destRackVm != null)
                            {
                                await _databaseService.UpdateRackStateAsync(destRackVm.Id, destRackVm.RackModel.RackType, destRackVm.RackModel.BulletType, false);
                                destRackVm.IsLocked = false;
                            }
                        });
                    }
                });
            }
            else
            {
                ShowAutoClosingMessage("랙 이동/복사 작업이 취소되었습니다.");
                // 팝업이 닫히거나, 선택된 랙이 없으면 취소.
                // 잠갔던 sourceRackViewModel.IsLocked = true; 를 다시 false로 되돌려야 합니다.
                // 이 역시 DatabaseService를 통해 다시 업데이트
                await _databaseService.UpdateRackStateAsync(sourceRackViewModel.Id, sourceRackViewModel.RackModel.RackType, sourceRackViewModel.RackModel.BulletType, false);
                Application.Current.Dispatcher.Invoke(() => sourceRackViewModel.IsLocked = false);

                // destinationRack이 선택되지 않아 null일 수 있으므로 null 체크 후 잠금 해제
                if (selectPopupViewModel.SelectedRack != null)
                {
                    var destRackVm = _mainViewModel.RackList?.FirstOrDefault(r => r.Id == selectPopupViewModel.SelectedRack.Id);
                    if (destRackVm != null)
                    {
                        await _databaseService.UpdateRackStateAsync(destRackVm.Id, destRackVm.RackModel.RackType, destRackVm.RackModel.BulletType, false);
                        Application.Current.Dispatcher.Invoke(() => destRackVm.IsLocked = false);
                    }
                }
            }
        }

        // 새롭게 추가할 출고 로직 (이전과 동일하게 유지)
        private async Task HandleRackShipout(RackViewModel targetRackViewModel)
        {
            var confirmPopupViewModel = new ConfirmShipoutPopupViewModel(targetRackViewModel.Title, targetRackViewModel.BulletType, targetRackViewModel.LotNumber);
            var confirmPopupView = new ConfirmShipoutPopupView { DataContext = confirmPopupViewModel };

            if (confirmPopupView.ShowDialog() == true && confirmPopupViewModel.DialogResult == true)
            {
                ShowAutoClosingMessage($"랙 {targetRackViewModel.Title} 출고 작업을 시작합니다. 10초 대기...");

                // 랙 잠금 및 비동기 작업 시작
                await _databaseService.UpdateRackStateAsync(targetRackViewModel.Id, targetRackViewModel.RackType, targetRackViewModel.BulletType, true); // 랙 잠금
                Application.Current.Dispatcher.Invoke(() => targetRackViewModel.IsLocked = true);


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
                    finally
                    {
                        // 오류 발생 시에도 잠금 해제 (최종적으로 보장)
                        Application.Current.Dispatcher.Invoke(async () =>
                        {
                            await _databaseService.UpdateRackStateAsync(targetRackViewModel.Id, targetRackViewModel.RackModel.RackType, targetRackViewModel.RackModel.BulletType, false);
                            targetRackViewModel.IsLocked = false;
                        });
                    }
                });
            }
            else
            {
                ShowAutoClosingMessage("랙 출고 작업이 취소되었습니다.");
                // 취소 시 랙 잠금 해제 (작업 시작 전에 잠겼다면)
                await _databaseService.UpdateRackStateAsync(targetRackViewModel.Id, targetRackViewModel.RackModel.RackType, targetRackViewModel.RackModel.BulletType, false);
                Application.Current.Dispatcher.Invoke(() => targetRackViewModel.IsLocked = false);
            }
        }

        private bool CanClickRack(object parameter)
        {
            return (!IsLocked); // 'locked' 상태가 아닐 때만 클릭 가능하도록 설정
        }

        private void ShowAutoClosingMessage(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var viewModel = new AutoClosingMessagePopupViewModel(message);
                var view = new AutoClosingMessagePopupView { DataContext = viewModel };
                view.Show();
            });
        }
    }
}
