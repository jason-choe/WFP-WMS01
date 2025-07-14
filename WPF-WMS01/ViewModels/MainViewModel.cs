// ViewModels/MainViewModel.cs
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Threading.Tasks;
using System;
using WPF_WMS01.Commands;
using WPF_WMS01.Services;
using WPF_WMS01.Models;
using WPF_WMS01.ViewModels.Popups;
using WPF_WMS01.Views.Popups;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Configuration;
using System.Net.Http;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading; // DispatcherTimer 사용을 위해 추가
using Newtonsoft.Json;
using JsonException = Newtonsoft.Json.JsonException;
using Microsoft.Extensions.DependencyInjection;

namespace WPF_WMS01.ViewModels
{
    // Modbus 버튼의 상태를 나타내는 ViewModel (각 버튼에 바인딩될 개별 항목)
    public class ModbusButtonViewModel : ViewModelBase
    {
        private bool _isEnabled; // 버튼의 활성화 상태 (PLC RUN && Discrete Input 1)
        private string _content; // 버튼에 표시될 텍스트 (예: "팔레트 공급")
        private ushort _discreteInputAddress; // 해당 버튼이 관여하는 Modbus Discrete Input 주소
        private ushort _coilOutputAddress; // 해당 버튼에 대응하는 Modbus Coil Output 주소 (경광등)
        private bool _isProcessing; // 비동기 작업 진행 중 여부 (미션 시작부터 완료/실패까지)
        private int _currentProgress; // 진행률 (0-100)
        private bool _isTaskInitiatedByDiscreteInput; // Discrete Input에 의해 작업이 시작되었음을 나타내는 플래그 (중복 트리거 방지용)
        private bool _currentDiscreteInputState; // 현재 Discrete Input 상태를 저장 (이전 상태 비교용)

        // 각 ModbusButtonViewModel 인스턴스에 고유한 팝업 ViewModel 및 View를 연결
        public MissionStatusPopupViewModel MissionStatusPopupVm { get; set; }
        public MissionStatusPopupView MissionStatusPopupViewInstance { get; set; }


        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (SetProperty(ref _isEnabled, value))
                {
                    // IsEnabled가 변경되면 Command의 CanExecute를 재평가
                    // CanExecuteModbusButtonCommand는 이제 IsEnabled에만 의존하므로 이 호출이 중요합니다.
                    ((RelayCommand)ExecuteButtonCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public string Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }

        public ushort DiscreteInputAddress // Call Button 입력 주소
        {
            get => _discreteInputAddress;
            set => SetProperty(ref _discreteInputAddress, value);
        }

        public ushort CoilOutputAddress // 경광등 제어 Coil 주소
        {
            get => _coilOutputAddress;
            set => SetProperty(ref _coilOutputAddress, value);
        }

        public bool IsProcessing // 작업 진행 중 여부
        {
            get => _isProcessing;
            set
            {
                // IsProcessing 변경 시 CanExecute를 다시 평가할 필요가 없습니다.
                // CanExecute는 이제 IsEnabled에만 의존하며, IsProcessing은 UI의 시각적 변화(DataTrigger)에만 사용됩니다.
                SetProperty(ref _isProcessing, value);
            }
        }

        public int CurrentProgress // 현재 진행률 (0-100)
        {
            get => _currentProgress;
            set => SetProperty(ref _currentProgress, value);
        }

        public bool IsTaskInitiatedByDiscreteInput // Discrete Input에 의해 작업이 스케줄링/시작되었는지 여부 (중복 트리거 방지용)
        {
            get => _isTaskInitiatedByDiscreteInput;
            set
            {
                // IsTaskInitiatedByDiscreteInput 변경 시 CanExecute를 다시 평가할 필요가 없습니다.
                // CanExecute는 이제 IsEnabled에만 의존하며, IsTaskInitiatedByDiscreteInput은 내부 로직 및 UI의 시각적 변화에 사용됩니다.
                SetProperty(ref _isTaskInitiatedByDiscreteInput, value);
            }
        }

        public bool CurrentDiscreteInputState // 현재 Discrete Input 상태 (for 0->1 transition detection)
        {
            get => _currentDiscreteInputState;
            set => SetProperty(ref _currentDiscreteInputState, value);
        }

        // 이 Command는 MainViewModel에서 초기화될 것입니다.
        public ICommand ExecuteButtonCommand { get; set; }

        public ModbusButtonViewModel(string content, ushort discreteInputAddress, ushort coilOutputAddress)
        {
            Content = content;
            DiscreteInputAddress = discreteInputAddress;
            CoilOutputAddress = coilOutputAddress;
            IsEnabled = false; // 초기에는 비활성화
            IsProcessing = false; // 초기에는 작업 중 아님
            CurrentProgress = 0; // 초기 진행률 0
            IsTaskInitiatedByDiscreteInput = false; // 초기 상태
            CurrentDiscreteInputState = false; // 초기 Discrete Input 상태

            // 팝업 관련 속성은 미션 시작 시점에 초기화될 예정
            MissionStatusPopupVm = null;
            MissionStatusPopupViewInstance = null;
        }
    }


    public class CheckoutRequest
    {
        public int BulletType { get; set; }
        public string ProductName { get; set; }
    }

    public class MainViewModel : ViewModelBase
    {
        private readonly DatabaseService _databaseService;
        private readonly HttpService _httpService;
        private IRobotMissionService _robotMissionServiceInternal; // 필드 이름을 변경하여 인터페이스 구현체임을 명확히 함
        private readonly string _apiUsername;
        private readonly string _apiPassword;
        private readonly ModbusClientService _modbusService; // ModbusClientService 인스턴스 추가
        // private readonly IMcProtocolService _mcProtocolService; // MC Protocol Service 인스턴스 추가 (현재 사용 안함)

        // AMR Payload 값들을 저장할 필드 추가
        private readonly string _warehousePayload;
        private readonly string _productionLinePayload;

        private ObservableCollection<RackViewModel> _rackList;
        private DispatcherTimer _refreshTimer;
        private DispatcherTimer _modbusReadTimer; // Modbus Coil 상태 읽기용 타이머

        public readonly string _waitRackTitle;
        public readonly char[] _militaryCharacter = { 'a', 'b', 'c', ' ' };

        // Modbus Discrete Input/Coil 상태를 저장할 ObservableCollection
        public ObservableCollection<ModbusButtonViewModel> ModbusButtons { get; set; }

        private bool _plcStatusIsRun; // PLC 구동 상태 (Discrete Input 100012)
        public bool PlcStatusIsRun
        {
            get => _plcStatusIsRun;
            set
            {
                if (SetProperty(ref _plcStatusIsRun, value))
                {
                    // PLC 상태가 변경되면 모든 Modbus 버튼의 활성화 상태를 갱신
                    foreach (var buttonVm in ModbusButtons)
                    {
                        // IsEnabled는 이제 PLC 상태와 Discrete Input 상태에만 의존합니다.
                        buttonVm.IsEnabled = value && buttonVm.CurrentDiscreteInputState;
                        // ModbusButtonViewModel의 IsEnabled setter에서 RaiseCanExecuteChanged가 호출됩니다.
                    }
                }
            }
        }

        private string _loginStatusMessage;
        public string LoginStatusMessage
        {
            get => _loginStatusMessage;
            set => SetProperty(ref _loginStatusMessage, value);
        }

        private bool _isLoggedIn;
        public bool IsLoggedIn
        {
            get => _isLoggedIn;
            set
            {
                if (SetProperty(ref _isLoggedIn, value))
                {
                    ((AsyncRelayCommand)LoginCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private string _authToken;
        public string AuthToken
        {
            get => _authToken;
            set => SetProperty(ref _authToken, value);
        }

        private DateTime? _tokenExpiryTime;
        public DateTime? TokenExpiryTime
        {
            get => _tokenExpiryTime;
            set => SetProperty(ref _tokenExpiryTime, value);
        }

        private bool _isLoginAttempting;
        public bool IsLoginAttempting
        {
            get => _isLoginAttempting;
            set
            {
                if (SetProperty(ref _isLoginAttempting, value))
                {
                    ((AsyncRelayCommand)LoginCommand).RaiseCanExecuteChanged();
                }
            }
        }

        private bool _isMenuOpen;
        public bool IsMenuOpen
        {
            get => _isMenuOpen;
            set
            {
                if (SetProperty(ref _isMenuOpen, value))
                {
                    Debug.WriteLine($"IsMenuOpen toggled to: {value}");
                }
            }
        }

        // AutoClosingMessage 팝업을 위한 속성 추가
        private AutoClosingMessagePopupViewModel _currentMessagePopupViewModel;
        public AutoClosingMessagePopupViewModel CurrentMessagePopupViewModel
        {
            get => _currentMessagePopupViewModel;
            set => SetProperty(ref _currentMessagePopupViewModel, value);
        }

        private bool _isMessagePopupVisible;
        public bool IsMessagePopupVisible
        {
            get => _isMessagePopupVisible;
            set => SetProperty(ref _isMessagePopupVisible, value);
        }

        private DispatcherTimer _messagePopupTimer; // 메시지 팝업 자동 닫힘 타이머

        // 미션 상태 팝업 관리를 위한 필드 (이제 ModbusButtonViewModel 내부에 개별적으로 존재)
        // private MissionStatusPopupViewModel _activeMissionStatusPopupViewModel; // 제거
        // private MissionStatusPopupView _activeMissionStatusPopupView; // 제거

        public ICommand LoginCommand { get; private set; }
        public ICommand OpenMenuCommand { get; }
        public ICommand CloseMenuCommand { get; }
        public ICommand MenuItem1Command { get; }
        public ICommand MenuItem2Command { get; }
        public ICommand MenuItem3Command { get; }

        private string _popupDebugMessage;
        public string PopupDebugMessage
        {
            get => _popupDebugMessage;
            set => SetProperty(ref _popupDebugMessage, value);
        }

        // Constructor: IMcProtocolService parameter removed as per user's request to add it later
        public MainViewModel(DatabaseService databaseService, HttpService httpService, ModbusClientService modbusService,
                             string warehousePayload, string productionLinePayload /*, IMcProtocolService mcProtocolService */)
        {
            Debug.WriteLine("[MainViewModel] Constructor called.");
            _databaseService = databaseService;
            _waitRackTitle = ConfigurationManager.AppSettings["WaitRackTitle"] ?? "WAIT";

            // App.config의 설정이 없는 경우를 대비하여 기본값 추가
            _httpService = httpService;
            _apiUsername = ConfigurationManager.AppSettings["AntApiUsername"] ?? "admin";
            _apiPassword = ConfigurationManager.AppSettings["AntApiPassword"] ?? "123456";

            // ModbusClientService 초기화 (TCP 모드 예시)
            // 실제 PLC의 IP 주소와 포트, 슬레이브 ID로 변경하세요.
            // RTU 모드를 사용하려면 ModbusClientService("COM1", 9600, Parity.None, StopBits.One, 8, 1) 와 같이 변경
            // App.config에서 IP/Port를 읽어오도록 변경 가능
            _modbusService = modbusService;
            // _mcProtocolService = mcProtocolService; // MC Protocol 서비스 주입 (현재 사용 안함)

            // Initialize AMR payload fields
            _warehousePayload = warehousePayload;
            _productionLinePayload = productionLinePayload;

            // ModbusButtons 컬렉션 초기화 (XAML의 버튼 순서 및 내용에 맞춰)
            // Discrete Input Address와 Coil Output Address를 스펙에 맞춰 매핑
            ModbusButtons = new ObservableCollection<ModbusButtonViewModel>
            {
                new ModbusButtonViewModel("5.56mm[1]", 0, 0),    // Discrete Input 100000 -> 0x02 Read 0 / Coil Output 0x05 Write 0
                new ModbusButtonViewModel("5.56mm[2]", 1, 1),    // Discrete Input 100001 -> 0x02 Read 1 / Coil Output 0x05 Write 1
                new ModbusButtonViewModel("5.56mm[3]", 2, 2),        // Discrete Input 100002 -> 0x02 Read 2 / Coil Output 0x05 Write 2
                new ModbusButtonViewModel("5.56mm[4]", 3, 3),     // Discrete Input 100003 -> 0x02 Read 3 / Coil Output 0x05 Write 3
                new ModbusButtonViewModel("5.56mm[5]", 4, 4),     // Discrete Input 100004 -> 0x02 Read 4 / Coil Output 0x05 Write 4
                new ModbusButtonViewModel("5.56mm[6]", 5, 5),     // Discrete Input 100005 -> 0x02 Read 5 / Coil Output 0x05 Write 5
                new ModbusButtonViewModel("7.62mm", 6, 6),     // Discrete Input 100006 -> 0x02 Read 6 / Coil Output 0x05 Write 6
                new ModbusButtonViewModel("팔레트 공급", 7, 7),     // Discrete Input 100007 -> 0x02 Read 7 / Coil Output 0x05 Write 7
                new ModbusButtonViewModel("단프라 공급", 8, 8),     // Discrete Input 100008 -> 0x02 Read 8 / Coil Output 0x05 Write 8
                new ModbusButtonViewModel("카타르[1]", 9, 9),     // Discrete Input 100009 -> 0x02 Read 9 / Coil Output 0x05 Write 9
                new ModbusButtonViewModel("카타르[2]", 10, 10),   // Discrete Input 100010 -> 0x02 Read 10 / Coil Output 0x05 Write 10
                new ModbusButtonViewModel("특수 포장", 11, 11)    // Discrete Input 100011 -> 0x02 Read 11 / Coil Output 0x05 Write 11
            };

            // 각 ModbusButtonViewModel에 Command 할당
            foreach (var buttonVm in ModbusButtons)
            {
                buttonVm.ExecuteButtonCommand = new RelayCommand(
                    async p => await ExecuteModbusButtonCommand(p as ModbusButtonViewModel),
                    p => CanExecuteModbusButtonCommand(p as ModbusButtonViewModel)
                );
            }

            // 일반 Command 초기화 (기존 코드)
            OpenMenuCommand = new RelayCommand(p => ExecuteOpenMenuCommand());
            CloseMenuCommand = new RelayCommand(p => ExecuteCloseMenuCommand());
            MenuItem1Command = new RelayCommand(p => OnMenuItem1Executed(p));
            MenuItem2Command = new RelayCommand(p => OnMenuItem2Executed(p));
            MenuItem3Command = new RelayCommand(p => OnMenuItem3Executed(p));

            IsMenuOpen = false;
            IsLoggedIn = false;
            IsLoginAttempting = false;
            LoginStatusMessage = "로그인 필요";

            // 팝업 메시지 초기화
            // CurrentMessagePopupViewModel을 생성자에서 초기화하도록 보장
            CurrentMessagePopupViewModel = new AutoClosingMessagePopupViewModel(string.Empty);
            IsMessagePopupVisible = false;
            SetupMessagePopupTimer(); // 메시지 팝업 타이머 설정

            InitializeCommands(); // 기존의 다른 명령 초기화

            SetupRefreshTimer(); // RackList 갱신 타이머
            SetupModbusReadTimer(); // Modbus Coil 상태 읽기 타이머 설정
            _ = LoadRacksAsync();
            _ = AutoLoginOnStartup();
            Debug.WriteLine("[MainViewModel] Constructor finished.");
        }

        /// <summary>
        /// RobotMissionService 인스턴스를 설정하는 메서드 (App.xaml.cs에서 호출)
        /// </summary>
        /// <param name="service">주입할 IRobotMissionService 인스턴스</param>
        public void SetRobotMissionService(IRobotMissionService service)
        {
            Debug.WriteLine("[MainViewModel] SetRobotMissionService called.");
            _robotMissionServiceInternal = service;
            // 서비스가 설정된 후에 이벤트 구독
            SetupRobotMissionServiceEvents();
            Debug.WriteLine("[MainViewModel] RobotMissionService injected and events setup.");
        }

        /// <summary>
        /// RobotMissionService 이벤트 구독을 위한 헬퍼 메서드
        /// </summary>
        private void SetupRobotMissionServiceEvents()
        {
            if (_robotMissionServiceInternal != null)
            {
                // 기존 구독 해제 (중복 구독 방지)
                _robotMissionServiceInternal.OnShowAutoClosingMessage -= ShowAutoClosingMessage;
                _robotMissionServiceInternal.OnRackLockStateChanged -= OnRobotMissionRackLockStateChanged;
                _robotMissionServiceInternal.OnInputStringForButtonCleared -= () => InputStringForButton = string.Empty;
                _robotMissionServiceInternal.OnTurnOffAlarmLightRequest -= HandleTurnOffAlarmLightRequest;
                _robotMissionServiceInternal.OnMissionProcessUpdated -= HandleMissionProcessUpdate;

                // 새로 구독
                _robotMissionServiceInternal.OnShowAutoClosingMessage += ShowAutoClosingMessage;
                _robotMissionServiceInternal.OnRackLockStateChanged += OnRobotMissionRackLockStateChanged;
                _robotMissionServiceInternal.OnInputStringForButtonCleared += () => InputStringForButton = string.Empty;
                _robotMissionServiceInternal.OnTurnOffAlarmLightRequest += HandleTurnOffAlarmLightRequest;
                _robotMissionServiceInternal.OnMissionProcessUpdated += HandleMissionProcessUpdate;
                Debug.WriteLine("[MainViewModel] Subscribed to RobotMissionService events.");
            }
            else
            {
                Debug.WriteLine("[MainViewModel] Attempted to setup RobotMissionService events, but _robotMissionServiceInternal is null.");
            }
        }

        /// <summary>
        /// RobotMissionService에서 랙 잠금 상태 변경 이벤트가 발생했을 때 호출됩니다.
        /// MainViewModel의 RackList에서 해당 랙을 찾아 UI를 업데이트합니다.
        /// </summary>
        /// <param name="rackId">상태가 변경된 랙의 ID.</param>
        /// <param name="newIsLocked">새로운 잠금 상태.</param>
        private void OnRobotMissionRackLockStateChanged(int rackId, bool newIsLocked)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var rackVm = RackList?.FirstOrDefault(r => r.Id == rackId);
                if (rackVm != null)
                {
                    rackVm.IsLocked = newIsLocked;
                    Debug.WriteLine($"[MainViewModel] Rack {rackVm.Title} (ID: {rackId}) IsLocked updated to {newIsLocked} via event.");
                }
            });
        }

        private void ExecuteOpenMenuCommand()
        {
            IsMenuOpen = !IsMenuOpen;
            Debug.WriteLine($"Hamburger button clicked. IsMenuOpen: {IsMenuOpen}");
        }

        private void ExecuteCloseMenuCommand()
        {
            IsMenuOpen = false;
            Debug.WriteLine("Menu close button clicked. IsMenuOpen: False");
        }

        private void OnMenuItem1Executed(object parameter)
        {
            Debug.WriteLine($"Option 1 clicked. Parameter: {parameter}");
            IsMenuOpen = false;
        }

        private void OnMenuItem2Executed(object parameter)
        {
            Debug.WriteLine($"Option 2 clicked. Parameter: {parameter}");
            IsMenuOpen = false;
        }

        private void OnMenuItem3Executed(object parameter)
        {
            Debug.WriteLine($"Option 3 clicked. Parameter: {parameter}");
            IsMenuOpen = false;
        }

        private void InitializeCommands()
        {
            InboundProductCommand = new RelayCommand(ExecuteInboundProduct, CanExecuteInboundProduct);  // 미포장 입고
            FakeInboundProductCommand = new RelayCommand(FakeExecuteInboundProduct, CanFakeExecuteInboundProduct); // 재공품 입고
            Checkout223aProductCommand = new RelayCommand(
                param => ExecuteCheckoutProduct(new CheckoutRequest { BulletType = 1, ProductName = "233A" }),
                param => CanExecuteCheckoutProduct(new CheckoutRequest { BulletType = 1, ProductName = "233A" }));
            Checkout223spProductCommand = new RelayCommand(
                param => ExecuteCheckoutProduct(new CheckoutRequest { BulletType = 2, ProductName = "223SP" }),
                param => CanExecuteCheckoutProduct(new CheckoutRequest { BulletType = 2, ProductName = "223SP" }));
            Checkout223xmProductCommand = new RelayCommand(
                param => ExecuteCheckoutProduct(new CheckoutRequest { BulletType = 3, ProductName = "223XM" }),
                param => CanExecuteCheckoutProduct(new CheckoutRequest { BulletType = 3, ProductName = "223XM" }));
            Checkout556xProductCommand = new RelayCommand(
                param => ExecuteCheckoutProduct(new CheckoutRequest { BulletType = 4, ProductName = "5.56X" }),
                param => CanExecuteCheckoutProduct(new CheckoutRequest { BulletType = 4, ProductName = "5.56X" }));
            Checkout556kProductCommand = new RelayCommand(
                param => ExecuteCheckoutProduct(new CheckoutRequest { BulletType = 5, ProductName = "5.56K" }),
                param => CanExecuteCheckoutProduct(new CheckoutRequest { BulletType = 5, ProductName = "5.56K" }));
            CheckoutM855tProductCommand = new RelayCommand(
                param => ExecuteCheckoutProduct(new CheckoutRequest { BulletType = 6, ProductName = "M855T" }),
                param => CanExecuteCheckoutProduct(new CheckoutRequest { BulletType = 6, ProductName = "M855T" }));
            CheckoutM193ProductCommand = new RelayCommand(
                param => ExecuteCheckoutProduct(new CheckoutRequest { BulletType = 7, ProductName = "M193" }),
                param => CanExecuteCheckoutProduct(new CheckoutRequest { BulletType = 7, ProductName = "M193" }));
            Checkout308bProductCommand = new RelayCommand(
                param => ExecuteCheckoutProduct(new CheckoutRequest { BulletType = 8, ProductName = "308B" }),
                param => CanExecuteCheckoutProduct(new CheckoutRequest { BulletType = 8, ProductName = "308B" }));
            Checkout308spProductCommand = new RelayCommand(
                param => ExecuteCheckoutProduct(new CheckoutRequest { BulletType = 9, ProductName = "308SP" }),
                param => CanExecuteCheckoutProduct(new CheckoutRequest { BulletType = 9, ProductName = "308SP" }));
            Checkout308xmProductCommand = new RelayCommand(
                param => ExecuteCheckoutProduct(new CheckoutRequest { BulletType = 10, ProductName = "308XM" }),
                param => CanExecuteCheckoutProduct(new CheckoutRequest { BulletType = 10, ProductName = "308XM" }));
            Checkout762xProductCommand = new RelayCommand(
                param => ExecuteCheckoutProduct(new CheckoutRequest { BulletType = 11, ProductName = "7.62X" }),
                param => CanExecuteCheckoutProduct(new CheckoutRequest { BulletType = 11, ProductName = "7.62X" }));
            CheckoutM80ProductCommand = new RelayCommand(
                param => ExecuteCheckoutProduct(new CheckoutRequest { BulletType = 12, ProductName = "M80" }),
                param => CanExecuteCheckoutProduct(new CheckoutRequest { BulletType = 12, ProductName = "M80" }));

            LoginCommand = new AsyncRelayCommand(ExecuteLogin, CanExecuteLogin);
        }

        public ObservableCollection<RackViewModel> RackList
        {
            get => _rackList;
            set => SetProperty(ref _rackList, value);
        }

        // RobotMissionService가 RackViewModel을 ID로 찾을 수 있도록 하는 헬퍼 메서드
        public RackViewModel GetRackViewModelById(int rackId)
        {
            return RackList?.FirstOrDefault(r => r.Id == rackId);
        }

        public ICommand LoadRacksCommand { get; }
        private async Task LoadRacksAsync()
        {
            try
            {
                var rackData = await _databaseService.GetRackStatesAsync();
                var rackViewModels = new ObservableCollection<RackViewModel>();
                foreach (var rack in rackData)
                {
                    // RackViewModel 생성 시 AMR Payload 값 전달 (X)
                    rackViewModels.Add(new RackViewModel(rack, _databaseService, this /* , _warehousePayload, _productionLinePayload */));
                }
                RackList = rackViewModels;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading rack data: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Debug.WriteLine($"[Database] Error loading rack data: {ex.GetType().Name} - {ex.Message}. StackTrace: {ex.StackTrace}");
            }
        }
        private async Task LoadRacks() // 이 메서드는 LoadRacksAsync와 중복되므로 향후 하나로 통합 고려
        {
            try
            {
                var racks = await _databaseService.GetRackStatesAsync();

                App.Current.Dispatcher.Invoke(() =>
                {
                    UpdateRackList(racks);
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data: {ex.Message}");
                Debug.WriteLine($"[Database] Error loading data in LoadRacks: {ex.GetType().Name} - {ex.Message}. StackTrace: {ex.StackTrace}");
            }
        }

        public ICommand UpdateRackStateCommand => new RelayCommand(async (parameter) =>
        {
            if (parameter is RackViewModel rackViewModel)
            {
                int newImageIndex = (rackViewModel.ImageIndex + 1) % 6;

                rackViewModel.RackModel.BulletType = newImageIndex % 7;
                rackViewModel.RackModel.RackType = newImageIndex / 7;

                try
                {
                    await _databaseService.UpdateRackStateAsync(
                        rackViewModel.Id,
                        rackViewModel.RackModel.RackType,
                        rackViewModel.RackModel.BulletType
                    );
                    await _databaseService.UpdateIsLockedAsync(rackViewModel.Id, rackViewModel.RackModel.IsLocked);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error updating rack state: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    Debug.WriteLine($"[Database] Error updating rack state: {ex.GetType().Name} - {ex.Message}. StackTrace: {ex.StackTrace}");
                }
            }
        });

        private void UpdateRackList(List<Rack> newRacks)
        {
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

            foreach (var newRackData in newRacks)
            {
                var existingRackVm = RackList.FirstOrDefault(rvm => rvm.Id == newRackData.Id);

                if (existingRackVm == null)
                {
                    // RackViewModel 생성 시 AMR Payload 값 전달 (X)
                    RackList.Add(new RackViewModel(newRackData, _databaseService, this /* , _warehousePayload, _productionLinePayload */));
                }
                else
                {
                    existingRackVm.SetRackModel(newRackData);
                }
            }
        }
        private void SetupRefreshTimer()
        {
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromSeconds(1);
            _refreshTimer.Tick += RefreshTimer_Tick;
            _refreshTimer.Start();
        }

        private async void RefreshTimer_Tick(object sender, EventArgs e)
        {
            await LoadRacksAsync();
        }

        private async Task AutoLoginOnStartup()
        {
            LoginStatusMessage = "로그인 시도 중...";
            IsLoggedIn = false;
            await ExecuteLogin(null);
        }

        private async Task ExecuteLogin(object parameter)
        {
            if (IsLoginAttempting) return;

            IsLoginAttempting = true;
            LoginStatusMessage = "로그인 중...";
            IsLoggedIn = false;
            AuthToken = null;

            try
            {
                LoginRequest loginReq = new LoginRequest
                {
                    Username = _apiUsername,
                    Password = _apiPassword,
                    ApiVersion = new ApiVersion { Major = 0, Minor = 0 }
                };

                Debug.WriteLine($"Login request: {_httpService.BaseApiUrl}wms/rest/login (User: {_apiUsername})");
                LoginResponse loginRes = await _httpService.PostAsync<LoginRequest, LoginResponse>("wms/rest/login", loginReq);

                if (!string.IsNullOrEmpty(loginRes?.Token))
                {
                    _httpService.SetAuthorizationHeader(loginRes.Token);
                    AuthToken = loginRes.Token;

                    if (!string.IsNullOrEmpty(loginRes.ApiVersionString))
                    {
                        string versionNumbers = loginRes.ApiVersionString.TrimStart('v');
                        string[] parts = versionNumbers.Split('.');

                        if (parts.Length == 2 && int.TryParse(parts[0], out int major) && int.TryParse(parts[1], out int minor))
                        {
                            _httpService.SetCurrentApiVersion(major, minor);
                            LoginStatusMessage = $"로그인 성공! (API v{major}.{minor})";
                        }
                        else
                        {
                            _httpService.SetCurrentApiVersion(0, 0);
                            LoginStatusMessage = $"로그인 성공! (API 버전 파싱 오류: {loginRes.ApiVersionString}, 기본값 v0.0 사용)";
                            Debug.WriteLine($"Warning: Login response API version '{loginRes.ApiVersionString}' parsing error. Using default v0.0.");
                        }
                    }
                    else
                    {
                        _httpService.SetCurrentApiVersion(0, 0);
                        LoginStatusMessage = $"로그인 성공! (API 버전 정보 없음, 기본값 v0.0 사용)";
                        Debug.WriteLine("Warning: Login response does not contain API version information. Using default v0.0.");
                    }

                    IsLoggedIn = true;
                    Debug.WriteLine("WMS server login successful!");
                }
                else
                {
                    IsLoggedIn = false;
                    LoginStatusMessage = "로그인 실패: 토큰 없음";
                    MessageBox.Show($"Login failed: No authentication token received. Server response might be incomplete or incorrect.", "ANT Login Error");
                    Debug.WriteLine("Login failed: No authentication token received.");
                }
            }
            catch (HttpRequestException httpEx)
            {
                IsLoggedIn = false;
                LoginStatusMessage = "로그인 실패";
                MessageBox.Show($"Login failed: Network error or no server response: {httpEx.Message}", "ANT Login Error");
                Debug.WriteLine($"Login HttpRequestException: {httpEx.Message}. Status Code: {httpEx.StatusCode}. StackTrace: {httpEx.StackTrace}");
                if (httpEx.InnerException != null)
                {
                    Debug.WriteLine($"Inner Exception: {httpEx.InnerException.GetType().Name} - {httpEx.InnerException.Message}");
                }
            }
            catch (JsonException jsonEx)
            {
                IsLoggedIn = false;
                LoginStatusMessage = "로그인 실패";
                MessageBox.Show($"Login failed: Response data format error. {jsonEx.Message}", "ANT Login Error");
                Debug.WriteLine($"Login JsonException: {jsonEx.Message}. StackTrace: {jsonEx.StackTrace}");
            }
            catch (Exception ex)
            {
                IsLoggedIn = false;
                LoginStatusMessage = "로그인 실패";
                MessageBox.Show($"Login failed: Unexpected error. {ex.Message}", "ANT Login Error");
                Debug.WriteLine($"Login General Exception: {ex.Message}. StackTrace: {ex.StackTrace}");
            }
            finally
            {
                IsLoginAttempting = false;
            }
        }

        private bool CanExecuteLogin(object parameter)
        {
            return !IsLoginAttempting;
        }

        /// <summary>
        /// 새로운 로봇 미션 프로세스를 시작합니다.
        /// 이 메서드는 RackViewModel과 같은 외부 ViewModel에서 호출될 수 있습니다.
        /// RobotMissionService로 호출을 위임합니다.
        /// </summary>
        /// <param name="processType">미션 프로세스의 유형 (예: "WaitToWrapTransfer", "RackTransfer").</param>
        /// <param name="missionSteps">이 프로세스를 구성하는 순차적인 미션 단계 목록.</param>
        /// <param name="sourceRack">원본 랙 ViewModel (더 이상 직접 사용되지 않음).</param>
        /// <param name="destinationRack">목적지 랙 ViewModel (더 이상 직접 사용되지 않음).</param>
        /// <param name="destinationLine">목적지 생산 라인 (선택 사항).</param>
        /// <param name="racksLockedAtStart">이 프로세스 시작 시 잠긴 모든 랙의 ID 목록.</param>
        /// <param name="racksToProcess">여러 랙을 처리할 경우 (예: 출고) 해당 랙들의 ViewModel 목록.</param>
        /// <param name="initiatingCoilAddress">이 미션을 시작한 Modbus Coil의 주소 (경광등 제어용).</param>
        /// <returns>시작된 미션 프로세스의 고유 ID.</returns>
        public async Task<string> InitiateRobotMissionProcess(
            string processType,
            List<MissionStepDefinition> missionSteps,
            RackViewModel sourceRack = null, // 이제 이 파라미터는 null로 전달됨
            RackViewModel destinationRack = null, // 이제 이 파라미터는 null로 전달됨
            Location destinationLine = null,
            List<int> racksLockedAtStart = null,
            List<RackViewModel> racksToProcess = null,
            ushort? initiatingCoilAddress = null) // 새로운 파라미터 추가
        {
            if (_robotMissionServiceInternal == null)
            {
                Debug.WriteLine("[MainViewModel] RobotMissionService is not initialized. Attempting to resolve from ServiceProvider.");
                // Fallback: If _robotMissionServiceInternal is null, try to get it from the ServiceProvider.
                // This might indicate an issue in DI setup if it's consistently null.
                // This block is primarily for robustness and debugging.
                if (App.ServiceProvider != null)
                {
                    try
                    {
                        _robotMissionServiceInternal = App.ServiceProvider.GetRequiredService<IRobotMissionService>();
                        Debug.WriteLine("[MainViewModel] RobotMissionService resolved from ServiceProvider as fallback.");
                        SetupRobotMissionServiceEvents(); // Ensure events are subscribed if resolved late
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[MainViewModel] Failed to resolve RobotMissionService from ServiceProvider: {ex.Message}");
                        MessageBox.Show("로봇 미션 서비스를 초기화할 수 없습니다. 관리자에게 문의하세요.", "경고", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return null;
                    }
                }
                else
                {
                    MessageBox.Show("로봇 미션 서비스를 초기화할 수 없습니다. 관리자에게 문의하세요.", "경고", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }
            }

            // MissionStatusPopupViewModel 초기화는 이제 HandleCallButtonActivatedTask에서 해당 버튼에 대해 수행됩니다.
            // 이 메서드에서는 RobotMissionService에 미션 시작을 요청합니다.

            return await _robotMissionServiceInternal.InitiateRobotMissionProcess(
                processType,
                missionSteps,
                sourceRack, // 여전히 인터페이스 호환을 위해 전달하지만, RobotMissionService에서 사용하지 않음
                destinationRack, // 여전히 인터페이스 호환을 위해 전달하지만, RobotMissionService에서 사용하지 않음
                destinationLine,
                () => InputStringForButton, // InputStringForButton 값을 가져오는 델리게이트 전달
                racksLockedAtStart,
                racksToProcess,
                initiatingCoilAddress // 새로운 파라미터 전달
            );
        }

        // Modbus Coil 상태 읽기 타이머 설정
        private void SetupModbusReadTimer()
        {
            _modbusReadTimer = new DispatcherTimer();
            _modbusReadTimer.Interval = TimeSpan.FromMilliseconds(1000); // 1초마다 읽기 (조정 가능)
            _modbusReadTimer.Tick += ModbusReadTimer_Tick;
            _modbusReadTimer.Start();
            Debug.WriteLine("[ModbusService] Modbus Read Timer Started.");
        }

        // Modbus Discrete Input/Coil 상태 주기적 읽기
        private async void ModbusReadTimer_Tick(object sender, EventArgs e)
        {
            // 연결이 끊겼다면 재연결 시도
            if (!_modbusService.IsConnected)
            {
                Debug.WriteLine("[ModbusService] Read Timer: Not Connected. Attempting to reconnect asynchronously...");
                try
                {
                    await _modbusService.ConnectAsync().ConfigureAwait(false); // ConfigureAwait(false) 사용
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ModbusService] Reconnect attempt failed: {ex.Message}.");
                    // 재연결 실패 시 메시지 박스 대신 자동 닫힘 메시지 사용
                    ShowAutoClosingMessage($"Modbus 연결 실패: {ex.Message.Substring(0, Math.Min(100, ex.Message.Length))}");
                    // 연결 실패 시 모든 버튼 상태를 비활성화 및 리셋
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        PlcStatusIsRun = false; // PLC 상태도 비활성으로
                        foreach (var buttonVm in ModbusButtons)
                        {
                            buttonVm.IsEnabled = false;
                            buttonVm.IsProcessing = false;
                            buttonVm.CurrentProgress = 0;
                            buttonVm.IsTaskInitiatedByDiscreteInput = false;
                            buttonVm.CurrentDiscreteInputState = false; // Discrete Input 상태 초기화
                            ((RelayCommand)buttonVm.ExecuteButtonCommand)?.RaiseCanExecuteChanged();
                        }
                    });
                    return; // 연결 실패 시 더 이상 진행하지 않음
                }

                if (!_modbusService.IsConnected) // 재연결 후에도 연결되지 않았다면
                {
                    Debug.WriteLine("[ModbusService] Read Timer: Connection failed after reconnect attempt. Skipping Modbus read.");
                    // PLC가 STOP 상태이면 모든 버튼을 비활성화하고 작업 플래그 리셋
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var buttonVm in ModbusButtons)
                        {
                            PlcStatusIsRun = false; // PLC 상태도 비활성으로
                            buttonVm.IsEnabled = false;
                            buttonVm.IsProcessing = false;
                            buttonVm.CurrentProgress = 0;
                            buttonVm.IsTaskInitiatedByDiscreteInput = false;
                            buttonVm.CurrentDiscreteInputState = false; // Discrete Input 상태 초기화
                            ((RelayCommand)buttonVm.ExecuteButtonCommand)?.RaiseCanExecuteChanged();
                        }
                    });
                    return;
                }
            }

            try
            {
                // 1. PLC 구동 상태 (Discrete Input 100012) 읽기
                ushort plcStatusAddress = 12; // 100012의 주소는 0x02 Read 12
                bool[] plcStatus = await _modbusService.ReadDiscreteInputStatesAsync(plcStatusAddress, 1).ConfigureAwait(false);

                // UI 스레드에서 PlcStatusIsRun 업데이트
                Application.Current.Dispatcher.Invoke(() =>
                {
                    PlcStatusIsRun = (plcStatus != null && plcStatus.Length > 0 && plcStatus[0]);
                });

                if (!PlcStatusIsRun)
                {
                    Debug.WriteLine("[ModbusService] PLC Status is STOP (0). Ignoring call button inputs.");
                    // PLC가 STOP 상태이면 모든 버튼을 비활성화하고 작업 플래그 리셋
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        foreach (var buttonVm in ModbusButtons)
                        {
                            buttonVm.IsEnabled = false;
                            buttonVm.IsProcessing = false;
                            buttonVm.CurrentProgress = 0;
                            buttonVm.IsTaskInitiatedByDiscreteInput = false;
                            buttonVm.CurrentDiscreteInputState = false; // Discrete Input 상태 초기화
                            ((RelayCommand)buttonVm.ExecuteButtonCommand)?.RaiseCanExecuteChanged();
                        }
                    });
                    return; // PLC가 정지 상태이므로 Call Button 입력은 처리하지 않음
                }

                // 2. Call Button Discrete Input (100000 ~ 100011) 상태 읽기
                ushort startDiscreteInputAddress = 0; // 100000의 주소는 0x02 Read 0
                ushort numberOfDiscreteInputs = (ushort)ModbusButtons.Count; // 12개
                bool[] discreteInputStates = await _modbusService.ReadDiscreteInputStatesAsync(startDiscreteInputAddress, numberOfDiscreteInputs).ConfigureAwait(false);

                // 3. 경광등 Coil Output (0 ~ 11) 상태 읽기 (필요시, 현재는 PLC에 Write만 하므로 생략 가능)
                // 현재 경광등 상태를 읽어와서 UI에 반영할 필요가 있다면 여기에 추가

                if (discreteInputStates != null && discreteInputStates.Length >= numberOfDiscreteInputs)
                {
                    // UI 업데이트는 UI 스레드에서 수행
                    Application.Current.Dispatcher.Invoke(async () =>
                    {
                        for (int i = 0; i < numberOfDiscreteInputs; i++)
                        {
                            var buttonVm = ModbusButtons[i];
                            bool currentDiscreteInputState = discreteInputStates[i];
                            bool previousDiscreteInputState = buttonVm.CurrentDiscreteInputState;

                            // Discrete Input 상태 업데이트 (다음 틱에서 이전 상태 비교를 위해)
                            buttonVm.CurrentDiscreteInputState = currentDiscreteInputState;

                            // 버튼의 IsEnabled 상태는 PLC가 Run 상태이고, 해당 Discrete Input이 1일 때 활성화
                            // IsProcessing 상태는 IsEnabled에 직접 영향을 주지 않음 (XAML DataTrigger에서 시각적 변화 처리)
                            buttonVm.IsEnabled = PlcStatusIsRun && currentDiscreteInputState;

                            // PLC 신호 (Discrete Input 0->1) 감지 및 PLC가 Run 상태일 때
                            // 그리고 해당 버튼에 대한 작업이 아직 시작되지 않았을 때만 트리거
                            if (currentDiscreteInputState && !previousDiscreteInputState && PlcStatusIsRun && !buttonVm.IsTaskInitiatedByDiscreteInput)
                            {
                                // 이 버튼에 대한 작업이 이미 시작되지 않았고, Discrete Input이 0에서 1로 전환되었다면
                                buttonVm.IsTaskInitiatedByDiscreteInput = true; // 작업 시작 플래그 설정
                                buttonVm.IsProcessing = true; // 작업 진행 중 상태로 변경 (UI에 ProgressBar 표시)
                                buttonVm.CurrentProgress = 0; // 진행률 초기화
                                Debug.WriteLine($"[Modbus] Call Button (Discrete Input {buttonVm.DiscreteInputAddress}) activated (0->1). Initiating task.");
                                ShowAutoClosingMessage($"[Modbus] {buttonVm.Content} Call Button 신호 감지! 작업 자동 시작됩니다.");

                                // 해당 경광등 Coil을 1로 켜기 (App -> PLC)
                                bool writeLampSuccess = await _modbusService.WriteSingleCoilAsync(buttonVm.CoilOutputAddress, true).ConfigureAwait(false);
                                if (writeLampSuccess)
                                {
                                    Debug.WriteLine($"[Modbus] Lamp Coil {buttonVm.CoilOutputAddress} set to ON (1).");
                                    // 10초 지연 후 메인 비동기 작업 시작
                                    _ = HandleCallButtonActivatedTask(buttonVm); // 백그라운드에서 실행 (fire-and-forget)
                                }
                                else
                                {
                                    Debug.WriteLine($"[Modbus] Failed to write Lamp Coil {buttonVm.CoilOutputAddress} to ON (1). Task not started.");
                                    ShowAutoClosingMessage($"[Modbus] {buttonVm.Content} 경광등 켜기 실패! 작업 시작 불가.");
                                    // 작업 시작 실패했으므로 플래그 리셋 (UI 스레드에서)
                                    buttonVm.IsTaskInitiatedByDiscreteInput = false;
                                    buttonVm.IsProcessing = false;
                                    buttonVm.CurrentProgress = 0;
                                }
                            }
                            // Command의 CanExecute 상태를 명시적으로 갱신하여 UI가 IsEnabled/IsProcessing 변화에 즉시 반응하도록 함
                            ((RelayCommand)buttonVm.ExecuteButtonCommand)?.RaiseCanExecuteChanged();
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModbusService] Error reading Modbus inputs/coils in timer tick: {ex.GetType().Name} - {ex.Message}. StackTrace: {ex.StackTrace}");
                _modbusService.Dispose(); // 통신 오류 발생 시 연결 끊고 재연결 준비
                ShowAutoClosingMessage($"Modbus 통신 오류: {ex.Message.Substring(0, Math.Min(100, ex.Message.Length))}");
                // 오류 발생 시 모든 버튼 상태를 비활성화 및 리셋
                Application.Current.Dispatcher.Invoke(() =>
                {
                    PlcStatusIsRun = false; // PLC 상태도 비활성으로
                    foreach (var buttonVm in ModbusButtons)
                    {
                        buttonVm.IsEnabled = false;
                        buttonVm.IsProcessing = false;
                        buttonVm.CurrentProgress = 0;
                        buttonVm.IsTaskInitiatedByDiscreteInput = false;
                        buttonVm.CurrentDiscreteInputState = false; // Discrete Input 상태 초기화
                        ((RelayCommand)buttonVm.ExecuteButtonCommand)?.RaiseCanExecuteChanged();
                    }
                });
            }
        }

        // Modbus 버튼 클릭 시 실행될 Command (HMI에서 작업 진행 상황 확인)
        private async Task ExecuteModbusButtonCommand(ModbusButtonViewModel buttonVm)
        {
            if (buttonVm == null) return;

            Debug.WriteLine($"[Modbus] UI Call Button clicked for {buttonVm.Content}. Displaying status.");

            string messageToShow;
            // 이 버튼에 대해 미션이 활성 상태이거나 대기 중인 경우
            if (buttonVm.IsProcessing || buttonVm.IsTaskInitiatedByDiscreteInput)
            {
                // 해당 버튼의 팝업 ViewModel이 초기화되어 있지 않다면 초기화 (예상치 못한 상황 대비)
                // 이 상황은 물리적 콜 버튼으로 미션이 시작되었으나, UI 버튼을 클릭하기 전에 팝업 ViewModel이 초기화되지 않은 경우입니다.
                if (buttonVm.MissionStatusPopupVm == null)
                {
                    // 이 시점에서는 MissionSteps를 알 수 없으므로, RobotMissionService에서 현재 활성화된 미션 정보를 조회하여 초기화해야 합니다.
                    // 임시로 빈 목록으로 초기화하고, HandleMissionProcessUpdate에서 실제 데이터로 채워질 것을 기대합니다.
                    buttonVm.MissionStatusPopupVm = new MissionStatusPopupViewModel(
                        "N/A", // ProcessId는 나중에 업데이트될 것
                        buttonVm.Content + " 미션", // 임시 ProcessType
                        new List<MissionStepDefinition>() // 임시 미션 단계 목록
                    );
                    Debug.WriteLine($"[MainViewModel] Lazily initialized MissionStatusPopupVm for {buttonVm.Content}.");
                }

                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 해당 버튼의 팝업 View가 존재하지 않거나 닫혀 있다면 새로 생성합니다.
                    if (buttonVm.MissionStatusPopupViewInstance == null || !buttonVm.MissionStatusPopupViewInstance.IsLoaded)
                    {
                        buttonVm.MissionStatusPopupViewInstance = new MissionStatusPopupView
                        {
                            DataContext = buttonVm.MissionStatusPopupVm,
                            Owner = Application.Current.MainWindow // 여기에 Owner 속성 추가
                        };
                        // 사용자가 팝업을 수동으로 닫을 때 View 참조를 null로 설정하여 다음 클릭 시 새로 생성되도록 합니다.
                        buttonVm.MissionStatusPopupViewInstance.Closed += (s, e) =>
                        {
                            buttonVm.MissionStatusPopupViewInstance = null;
                            buttonVm.MissionStatusPopupVm = null; // 팝업이 닫히면 ViewModel도 클리어
                            Debug.WriteLine($"[MainViewModel] Mission status popup view for {buttonVm.Content} manually closed by user. View and ViewModel references cleared.");
                        };
                    }

                    // 팝업을 표시합니다. 이미 표시되어 있다면 활성화합니다.
                    if (!buttonVm.MissionStatusPopupViewInstance.IsVisible)
                    {
                        buttonVm.MissionStatusPopupViewInstance.Show();
                        Debug.WriteLine($"[MainViewModel] Showing mission status popup for {buttonVm.Content}, Process ID: {buttonVm.MissionStatusPopupVm.ProcessId}");
                    }
                    else
                    {
                        buttonVm.MissionStatusPopupViewInstance.Activate(); // 이미 열려있다면 활성화
                        Debug.WriteLine($"[MainViewModel] Mission status popup for {buttonVm.Content}, Process ID: {buttonVm.MissionStatusPopupVm.ProcessId} is already visible. Activating.");
                    }
                });
                messageToShow = $"[Modbus] {buttonVm.Content} 작업 상태 표시됨.";
            }
            else if (buttonVm.IsEnabled && !buttonVm.IsProcessing && !buttonVm.IsTaskInitiatedByDiscreteInput)
            {
                // PLC Run && Discrete Input 1 이지만, 미션이 실행 중이거나 대기 중이 아닌 경우
                // (물리적 버튼이 눌렸다가 해제되었거나, 새 미션을 기다리는 상태)
                messageToShow = $"[Modbus] {buttonVm.Content} 작업 시작 준비 완료. 물리적 콜 버튼을 기다립니다. (주소: {buttonVm.DiscreteInputAddress}).";
                // 이 상태에서는 팝업을 띄우지 않습니다. 혹시 이전 미션의 팝업이 남아있다면 닫습니다.
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (buttonVm.MissionStatusPopupViewInstance != null)
                    {
                        buttonVm.MissionStatusPopupViewInstance.Close();
                        buttonVm.MissionStatusPopupViewInstance = null;
                        // ViewModel은 다음 미션을 위해 유지하거나, 필요시 null로 설정
                        // 여기서는 미션이 없으므로 ViewModel도 클리어
                        buttonVm.MissionStatusPopupVm = null;
                        Debug.WriteLine($"[MainViewModel] Old mission status popup for {buttonVm.Content} closed as no active mission found.");
                    }
                });
            }
            else // 버튼이 비활성화된 경우 (PLC Stop 또는 Discrete Input 0)
            {
                string status = PlcStatusIsRun ? $"Discrete Input {buttonVm.DiscreteInputAddress}이 0입니다." : "PLC가 정지 상태입니다.";
                messageToShow = $"[Modbus] {buttonVm.Content} 작업 비활성화됨. ({status}).";
                // 이 상태에서는 팝업을 띄우지 않습니다. 혹시 이전 미션의 팝업이 남아있다면 닫습니다.
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (buttonVm.MissionStatusPopupViewInstance != null)
                    {
                        buttonVm.MissionStatusPopupViewInstance.Close();
                        buttonVm.MissionStatusPopupViewInstance = null;
                        // ViewModel도 클리어
                        buttonVm.MissionStatusPopupVm = null;
                        Debug.WriteLine($"[MainViewModel] Old mission status popup for {buttonVm.Content} closed as button is disabled.");
                    }
                });
            }

            ShowAutoClosingMessage(messageToShow);
        }

        // Discrete Input이 0에서 1로 활성화될 때 수행될 비동기 작업
        private async Task HandleCallButtonActivatedTask(ModbusButtonViewModel buttonVm)
        {
            if (buttonVm == null) return;

            Debug.WriteLine($"[Modbus] Async task started for {buttonVm.Content} (Discrete Input: {buttonVm.DiscreteInputAddress}).");

            List<MissionStepDefinition> missionSteps = new List<MissionStepDefinition>();
            string processType = "CallButtonMission"; // Default process type
            List<int> racksToLock = new List<int>(); // No racks locked for simple supply missions initially

            try
            {
                switch (buttonVm.Content)
                {
                    case "팔레트 공급":
                        processType = "공 팔레트 더미 공급 작업";
                        missionSteps.Add(new MissionStepDefinition
                        {
                            ProcessStepDescription = "충전소에서 이동",
                            MissionType = "8", // Pick up
                            ToNode = "Turn_Charge2", // Hypothetical drop-off station
                            Payload = _productionLinePayload,
                            IsLinkable = true,
                            LinkedMission = null,
                            LinkWaitTimeout = 3600
                        });
                        missionSteps.Add(new MissionStepDefinition
                        {
                            ProcessStepDescription = "공 팔레트 더미 픽업, 매거진으로 이동 & 투입",
                            MissionType = "7", // Move/Drop
                            FromNode = "Empty_Palette_PickUP",
                            ToNode = "MZ_Empty_Palette_Drop", // Return to charge station after drop-off
                            Payload = _productionLinePayload,
                            IsLinkable = true, // This is the final step of this specific mission
                            LinkedMission = null,
                            LinkWaitTimeout = 3600
                        });
                        missionSteps.Add(new MissionStepDefinition
                        {
                            ProcessStepDescription = "충전소로 복귀",
                            MissionType = "8", // Move/Drop
                            ToNode = "Charge2", // Return to charge station after drop-off
                            Payload = _productionLinePayload,
                            IsLinkable = false, // This is the final step of this specific mission
                            LinkedMission = null,
                            LinkWaitTimeout = 3600,
                            SourceRackId = null, // No specific rack involved in DB update for this supply
                            DestinationRackId = null
                        });
                        break;

                    case "단프라 공급":
                        processType = "단프라 시트 더미 공급 작업";
                        missionSteps.Add(new MissionStepDefinition
                        {
                            ProcessStepDescription = "충전소에서 이동",
                            MissionType = "8", // Pick up
                            ToNode = "Turn_Charge2", // Hypothetical drop-off station
                            Payload = _productionLinePayload,
                            IsLinkable = true,
                            LinkedMission = null,
                            LinkWaitTimeout = 3600
                        });
                        missionSteps.Add(new MissionStepDefinition
                        {
                            ProcessStepDescription = "단프라 시트 더미 픽업, 매거진으로 이동 & 투입",
                            MissionType = "7", // Move/Drop
                            FromNode = "DanPra_Sheet_PickUP",
                            ToNode = "MZ_DanPra_Sheet_Drop", // Return to charge station after drop-off
                            Payload = _productionLinePayload,
                            IsLinkable = true, // This is the final step of this specific mission
                            LinkedMission = null,
                            LinkWaitTimeout = 3600
                        });
                        missionSteps.Add(new MissionStepDefinition
                        {
                            ProcessStepDescription = "충전소로 복귀",
                            MissionType = "8", // Move/Drop
                            ToNode = "Charge2", // Return to charge station after drop-off
                            Payload = _productionLinePayload,
                            IsLinkable = false, // This is the final step of this specific mission
                            LinkedMission = null,
                            LinkWaitTimeout = 3600,
                            SourceRackId = null, // No specific rack involved in DB update for this supply
                            DestinationRackId = null
                        });
                        break;

                    // For other product-specific call buttons, let's define a simple "robot moves to station" mission.
                    // This avoids complex product lookup and transfer logic for now.
                    case "특수 포장":
                        processType = "특수 포장 제품 입고 작업";
                        missionSteps.Add(new MissionStepDefinition
                        {
                            ProcessStepDescription = "충전소에서 이동",
                            MissionType = "8", // Pick up
                            ToNode = "Turn_Charge2", // Hypothetical drop-off station
                            Payload = _productionLinePayload,
                            IsLinkable = true,
                            LinkedMission = null,
                            LinkWaitTimeout = 3600
                        });
                        missionSteps.Add(new MissionStepDefinition
                        {
                            ProcessStepDescription = "특수 포장 제품 픽업,입고장으로 이동 & 입고",
                            MissionType = "7", // Move/Drop
                            FromNode = "Work_Etc_PickUP",
                            ToNode = "Palette_IN_Drop", // Return to charge station after drop-off
                            Payload = _productionLinePayload,
                            IsLinkable = true, // This is the final step of this specific mission
                            LinkedMission = null,
                            LinkWaitTimeout = 3600
                        });
                        missionSteps.Add(new MissionStepDefinition
                        {
                            ProcessStepDescription = "충전소로 복귀",
                            MissionType = "8", // Move/Drop
                            ToNode = "Charge2", // Return to charge station after drop-off
                            Payload = _productionLinePayload,
                            IsLinkable = false, // This is the final step of this specific mission
                            LinkedMission = null,
                            LinkWaitTimeout = 3600,
                            SourceRackId = null, // No specific rack involved in DB update for this supply
                            DestinationRackId = null
                        });
                        break;
                    case "5.56mm[3]":
                    case "5.56mm[6]":
                    case "5.56mm[1]":
                    case "5.56mm[2]":
                    case "5.56mm[4]":
                    case "5.56mm[5]":
                    case "7.62mm":
                    case "카타르[1]":
                    case "카타르[2]":
                        string workPoint;
                        string swapPoint;
                        if (buttonVm.Content.Equals("7.62mm"))
                        {
                            workPoint = "308";
                            swapPoint = "308";
                        }
                        else if (buttonVm.Content.Equals("5.56mm[5]"))
                        {
                            workPoint = "223_2_2";
                            swapPoint = "223_2";
                        }
                        else if (buttonVm.Content.Equals("5.56mm[4]"))
                        {
                            workPoint = "223_2_1";
                            swapPoint = "223_2";
                        }
                        else if (buttonVm.Content.Equals("5.56mm[2]"))
                        {
                            workPoint = "223_1_2";
                            swapPoint = "223_1";
                        }
                        else if (buttonVm.Content.Equals("5.56mm[1]"))
                        {
                            workPoint = "223_1_1";
                            swapPoint = "223_1";
                        }
                        else if (buttonVm.Content.Equals("카타르[1]"))
                        {
                            workPoint = "Manual_1";
                            swapPoint = "Manual";
                        }
                        else if (buttonVm.Content.Equals("카타르[2]"))
                        {
                            workPoint = "Manual_2";
                            swapPoint = "Manual";
                        }
                        else if (buttonVm.Content.Equals("5.56mm[3]"))
                        {
                            workPoint = "223_1_Bypass";
                            swapPoint = "223_1";
                        }
                        else //if (buttonVm.Content.Equals("5.56mm[6]"))
                        {
                            workPoint = "223_2_Bypass";
                            swapPoint = "223_2";
                        }
                        processType = $"{buttonVm.Content} 제품 입고 작업";
                        missionSteps.Add(new MissionStepDefinition { ProcessStepDescription = $"충전소에서 이동", MissionType = "8", ToNode = "Turn_Charge2", Payload = _productionLinePayload, IsLinkable = true, LinkedMission = null, LinkWaitTimeout = 3600 });
                        missionSteps.Add(new MissionStepDefinition { ProcessStepDescription = $"매거진에서 공 파레트 픽업, 교체 장소로 이동 & 드롭", MissionType = "7", FromNode = "MZ_DanPra_Palette_PickUP", ToNode = $"Empty_{swapPoint}_Drop", Payload = _productionLinePayload, IsLinkable = true, LinkedMission = null, LinkWaitTimeout = 3600 });
                        missionSteps.Add(new MissionStepDefinition { ProcessStepDescription = $"{buttonVm.Content} 제품 팔레트 픽업, 교체 장소로 이동 & 드롭", MissionType = "7", FromNode = $"Work_{workPoint}_PickUP", ToNode = $"Full_{swapPoint}_Drop", Payload = _productionLinePayload, IsLinkable = true, LinkedMission = null, LinkWaitTimeout = 3600 });
                        missionSteps.Add(new MissionStepDefinition { ProcessStepDescription = $"공 파레트 픽업, {buttonVm.Content}(으)로 이동 & 투입", MissionType = "7", FromNode = $"Empty_{swapPoint}_PickUP", ToNode = $"Work_{workPoint}_Drop", Payload = _productionLinePayload, IsLinkable = true, LinkedMission = null, LinkWaitTimeout = 3600 });
                        missionSteps.Add(new MissionStepDefinition { ProcessStepDescription = $"{buttonVm.Content} 제품 팔레트 픽업 & 이동", MissionType = "7", FromNode = $"Full_{swapPoint}_PickUP", ToNode = $"Return_{swapPoint}", Payload = _productionLinePayload, IsLinkable = true, LinkedMission = null, LinkWaitTimeout = 3600 });
                        missionSteps.Add(new MissionStepDefinition { ProcessStepDescription = $"{buttonVm.Content} 제품 팔레트 이동 & 입고", MissionType = "8", ToNode = "Palette_IN_Drop", Payload = _productionLinePayload, IsLinkable = true, LinkedMission = null, LinkWaitTimeout = 3600 });
                        missionSteps.Add(new MissionStepDefinition
                        {
                            ProcessStepDescription = $"충전소로 복귀",
                            MissionType = "8",
                            ToNode = "Charge2",
                            Payload = _productionLinePayload,
                            IsLinkable = false,
                            LinkWaitTimeout = 3600,
                            SourceRackId = null,
                            DestinationRackId = null
                        });
                        break;

                    default:
                        ShowAutoClosingMessage($"[Modbus] 알 수 없는 콜 버튼: {buttonVm.Content}. 미션 시작 불가.");
                        Debug.WriteLine($"[Modbus] Unknown call button content: {buttonVm.Content}. No mission initiated.");
                        return; // Exit if unknown button
                }

                // 해당 버튼의 MissionStatusPopupVm을 초기화합니다.
                // 이 시점에서 processId는 아직 모르므로 "N/A"로 초기화하고,
                // RobotMissionService에서 실제 processId를 받으면 업데이트됩니다.
                buttonVm.MissionStatusPopupVm = new MissionStatusPopupViewModel(
                    "N/A", // 초기 ProcessId는 "N/A"로 설정
                    processType,
                    missionSteps // 초기 미션 단계 목록 전달
                );
                Debug.WriteLine($"[MainViewModel] Initialized MissionStatusPopupVm for {buttonVm.Content} (Coil: {buttonVm.CoilOutputAddress}).");


                // Initiate the robot mission
                string processId = await InitiateRobotMissionProcess(
                    processType,
                    missionSteps,
                    null, // SourceRack (not directly used for these missions)
                    null, // DestinationRack (not directly used for these missions)
                    null, // DestinationLine
                    racksToLock, // Empty list for now, as no racks are directly locked for these supply missions
                    null, // racksToProcess
                    buttonVm.CoilOutputAddress // 새로 추가된 파라미터: 경광등 Coil 주소 전달
                );

                if (!string.IsNullOrEmpty(processId))
                {
                    ShowAutoClosingMessage($"[Modbus] {buttonVm.Content} 로봇 미션 시작됨: {processId}");
                    Debug.WriteLine($"[Modbus] Robot mission for {buttonVm.Content} initiated with Process ID: {processId}");
                    // 미션이 시작되었으므로, 해당 버튼의 팝업 ViewModel에 실제 ProcessId를 업데이트합니다.
                    if (buttonVm.MissionStatusPopupVm != null)
                    {
                        buttonVm.MissionStatusPopupVm.ProcessId = processId;
                    }
                }
                else
                {
                    ShowAutoClosingMessage($"[Modbus] {buttonVm.Content} 로봇 미션 시작 실패.");
                    Debug.WriteLine($"[Modbus] Robot mission for {buttonVm.Content} failed to initiate.");
                    // 미션 시작 실패 시 해당 버튼의 팝업 ViewModel도 클리어
                    buttonVm.MissionStatusPopupVm = null;
                }
            }
            catch (Exception ex)
            {
                ShowAutoClosingMessage($"[Modbus] {buttonVm.Content} 미션 시작 중 오류: {ex.Message}");
                Debug.WriteLine($"[Modbus] Error during {buttonVm.Content} mission initiation: {ex.GetType().Name} - {ex.Message}");
                // 오류 발생 시 해당 버튼의 팝업 ViewModel도 클리어
                buttonVm.MissionStatusPopupVm = null;
            }
            finally
            {
                // UI 상태를 리셋합니다. 경광등 끄는 로직은 RobotMissionService로 이관되었습니다.
                // 여기서는 IsProcessing 및 IsTaskInitiatedByDiscreteInput을 리셋하지 않습니다.
                // 이들은 RobotMissionService의 완료/실패 처리 로직에서 경광등이 꺼질 때 리셋됩니다.
                // ModbusReadTimer_Tick에서 Discrete Input이 0으로 돌아오는 것을 감지하여 IsProcessing을 false로 설정합니다.
                // buttonVm.IsProcessing = false; // 제거
                // buttonVm.CurrentProgress = 0; // 제거
                // buttonVm.IsTaskInitiatedByDiscreteInput = false; // 제거
            }
        }

        // Modbus 버튼 활성화 여부를 결정하는 CanExecute 로직
        private bool CanExecuteModbusButtonCommand(ModbusButtonViewModel buttonVm)
        {
            if (buttonVm == null) return false;
            // UI 버튼은 PLC가 Run 상태이고 해당 Discrete Input이 1일 때 (즉, buttonVm.IsEnabled가 true일 때) 클릭 가능합니다.
            // 이는 미션 진행 여부와 상관없이 상태를 확인하기 위해 클릭할 수 있도록 합니다.
            return buttonVm.IsEnabled;
        }

        public void Dispose()
        {
            _refreshTimer?.Stop();
            _refreshTimer.Tick -= RefreshTimer_Tick;
            _modbusReadTimer?.Stop(); // Modbus 타이머도 해제
            _modbusReadTimer.Tick -= ModbusReadTimer_Tick;
            _messagePopupTimer?.Stop(); // 메시지 팝업 타이머 해제
            _messagePopupTimer.Tick -= MessagePopupTimer_Tick;

            _modbusService?.Dispose(); // Modbus 서비스 자원 해제
                                       // _mcProtocolService?.Dispose(); // MC Protocol 서비스 자원 해제 (현재 사용 안함)

            // RobotMissionService 이벤트 구독 해제
            if (_robotMissionServiceInternal != null)
            {
                _robotMissionServiceInternal.OnShowAutoClosingMessage -= ShowAutoClosingMessage;
                _robotMissionServiceInternal.OnRackLockStateChanged -= OnRobotMissionRackLockStateChanged;
                _robotMissionServiceInternal.OnInputStringForButtonCleared -= () => InputStringForButton = string.Empty;
                _robotMissionServiceInternal.OnTurnOffAlarmLightRequest -= HandleTurnOffAlarmLightRequest;
                _robotMissionServiceInternal.OnMissionProcessUpdated -= HandleMissionProcessUpdate;
            }
            _robotMissionServiceInternal?.Dispose(); // 로봇 미션 서비스 자원 해제

            // 모든 활성 팝업 정리
            foreach (var buttonVm in ModbusButtons)
            {
                if (buttonVm.MissionStatusPopupViewInstance != null)
                {
                    buttonVm.MissionStatusPopupViewInstance.Close();
                    buttonVm.MissionStatusPopupViewInstance = null;
                    buttonVm.MissionStatusPopupVm = null;
                }
            }
        }

        // AutoClosingMessagePopupView를 표시하는 새로운 로직
        public void ShowAutoClosingMessage(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // CurrentMessagePopupViewModel이 null이면 새로 생성
                if (CurrentMessagePopupViewModel == null)
                {
                    CurrentMessagePopupViewModel = new AutoClosingMessagePopupViewModel(message);
                }
                else
                {
                    // 이미 인스턴스가 있다면 메시지만 업데이트
                    CurrentMessagePopupViewModel.Message = message;
                }
                IsMessagePopupVisible = true;
                _messagePopupTimer.Stop(); // 기존 타이머 중지
                _messagePopupTimer.Start(); // 새 메시지로 타이머 재시작
            });
        }

        // 메시지 팝업 자동 닫힘 타이머 설정
        private void SetupMessagePopupTimer()
        {
            _messagePopupTimer = new DispatcherTimer();
            _messagePopupTimer.Interval = TimeSpan.FromSeconds(3); // 3초 후 자동 닫힘 (조정 가능)
            _messagePopupTimer.Tick += MessagePopupTimer_Tick;
        }

        // 메시지 팝업 타이머 틱 이벤트 핸들러
        private void MessagePopupTimer_Tick(object sender, EventArgs e)
        {
            _messagePopupTimer.Stop();
            IsMessagePopupVisible = false;
            CurrentMessagePopupViewModel.Message = string.Empty; // 메시지 초기화
        }

        // RobotMissionService로부터 경광등을 끄라는 요청을 받았을 때 처리하는 메서드
        private async void HandleTurnOffAlarmLightRequest(ushort coilAddress)
        {
            try
            {
                // MainViewModel의 _modbusService를 사용하여 경광등을 끕니다.
                bool success = await _modbusService.WriteSingleCoilAsync(coilAddress, false);
                if (success)
                {
                    Debug.WriteLine($"[MainViewModel] Alarm light Coil {coilAddress} turned OFF by RobotMissionService request.");
                    // 경광등이 꺼졌으므로 해당 버튼의 IsProcessing 및 IsTaskInitiatedByDiscreteInput 플래그를 리셋합니다.
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var buttonVm = ModbusButtons.FirstOrDefault(b => b.CoilOutputAddress == coilAddress);
                        if (buttonVm != null)
                        {
                            buttonVm.IsProcessing = false;
                            buttonVm.CurrentProgress = 0;
                            buttonVm.IsTaskInitiatedByDiscreteInput = false;
                            // Discrete Input 상태는 다음 ModbusReadTimer_Tick에서 PLC로부터 읽어와 업데이트될 것입니다.
                            Debug.WriteLine($"[MainViewModel] Button {buttonVm.Content} state reset after alarm light OFF.");

                            // 미션이 완료/실패되었지만, 팝업은 자동으로 닫지 않습니다.
                            // 사용자가 '확인' 버튼을 눌러 닫도록 합니다.
                            // 따라서 buttonVm.MissionStatusPopupViewInstance.Close() 및 null 설정 로직은 여기서 제거합니다.
                            // 팝업이 닫힐 때 (사용자 클릭 시) Closed 이벤트 핸들러에서 ViewModel 및 View 참조를 클리어합니다.
                        }
                    });
                }
                else
                {
                    Debug.WriteLine($"[MainViewModel] Failed to turn OFF alarm light Coil {coilAddress} by RobotMissionService request.");
                    ShowAutoClosingMessage($"경광등 끄기 실패: Coil {coilAddress}!");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainViewModel] Error turning off alarm light Coil {coilAddress}: {ex.Message}");
                ShowAutoClosingMessage($"경광등 제어 중 오류: {ex.Message.Substring(0, Math.Min(100, ex.Message.Length))}");
            }
        }

        /// <summary>
        /// RobotMissionService로부터 미션 프로세스 업데이트를 받아 팝업을 갱신합니다.
        /// </summary>
        /// <param name="missionInfo">업데이트된 RobotMissionInfo 객체.</param>
        private void HandleMissionProcessUpdate(RobotMissionInfo missionInfo)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                // missionInfo의 InitiatingCoilAddress를 사용하여 해당 ModbusButtonViewModel을 찾습니다.
                var buttonVm = ModbusButtons.FirstOrDefault(b => b.CoilOutputAddress == missionInfo.InitiatingCoilAddress);

                if (buttonVm != null) // 버튼 ViewModel을 찾았을 경우
                {
                    // 해당 버튼의 팝업 ViewModel이 아직 초기화되지 않았다면 초기화합니다.
                    // 이 경우는 물리적 콜 버튼으로 미션이 시작되었고, 아직 UI 버튼을 클릭하지 않은 상태에서 업데이트가 먼저 도착한 경우입니다.
                    if (buttonVm.MissionStatusPopupVm == null)
                    {
                        buttonVm.MissionStatusPopupVm = new MissionStatusPopupViewModel(
                            missionInfo.ProcessId,
                            missionInfo.ProcessType,
                            missionInfo.MissionSteps.ToList()
                        );
                        Debug.WriteLine($"[MainViewModel] Lazily initialized MissionStatusPopupVm for {buttonVm.Content} (Coil: {missionInfo.InitiatingCoilAddress}) upon first update.");
                    }

                    // 해당 버튼의 팝업 ViewModel을 업데이트합니다.
                    buttonVm.MissionStatusPopupVm.UpdateStatus(missionInfo, missionInfo.MissionSteps.ToList());
                    Debug.WriteLine($"[MainViewModel] Mission status popup ViewModel for Coil {missionInfo.InitiatingCoilAddress} updated for Process ID: {missionInfo.ProcessId}");
                }
                else
                {
                    Debug.WriteLine($"[MainViewModel] Received mission process update for {missionInfo.ProcessId} (Coil: {missionInfo.InitiatingCoilAddress}), but no matching button ViewModel found. This might indicate an unhandled scenario or a late update for a non-button-initiated mission.");
                }

                // 미션이 완료되거나 실패하면 자동 닫힘 메시지를 표시합니다.
                if (missionInfo.IsFinished || missionInfo.IsFailed)
                {
                    ShowAutoClosingMessage($"미션 {missionInfo.ProcessId} ({missionInfo.ProcessType})이(가) {missionInfo.HmiStatus.Status} 상태로 종료되었습니다.");
                }
            });
        }

        private string _inputStringForButton;
        public string InputStringForButton
        {
            get => _inputStringForButton;
            set
            {
                _inputStringForButton = value;
                OnPropertyChanged();
                ((RelayCommand)InboundProductCommand).RaiseCanExecuteChanged();
            }
        }

        private string _inputStringForShipOut;
        public string InputStringForShipOut
        {
            get => _inputStringForShipOut;
            set
            {
                _inputStringForShipOut = value;
                OnPropertyChanged();
            }
        }

        public ICommand InboundProductCommand { get; private set; }
        public ICommand FakeInboundProductCommand { get; private set; }
        public ICommand Checkout223aProductCommand { get; private set; }
        public ICommand Checkout223spProductCommand { get; private set; }
        public ICommand Checkout223xmProductCommand { get; private set; }
        public ICommand Checkout556xProductCommand { get; private set; }
        public ICommand Checkout556kProductCommand { get; private set; }
        public ICommand CheckoutM855tProductCommand { get; private set; }
        public ICommand CheckoutM193ProductCommand { get; private set; }
        public ICommand Checkout308bProductCommand { get; private set; }
        public ICommand Checkout308spProductCommand { get; private set; }
        public ICommand Checkout308xmProductCommand { get; private set; }
        public ICommand Checkout762xProductCommand { get; private set; }
        public ICommand CheckoutM80ProductCommand { get; private set; }

        private async void ExecuteInboundProduct(object parameter)
        {
            var emptyRacks = RackList?.Where(r => r.ImageIndex == 0 && r.IsVisible).ToList();

            if (emptyRacks == null || !emptyRacks.Any())
            {
                MessageBox.Show("현재 미 포장 입고할 수 있는 빈 랙이 없습니다.", "경고", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectEmptyRackViewModel = new SelectEmptyRackPopupViewModel(emptyRacks.Select(r => r.RackModel).ToList(),
                _inputStringForButton.TrimStart().TrimEnd(_militaryCharacter), "포장 전 적재", "미포장 제품");
            var selectEmptyRackView = new SelectEmptyRackPopupView { DataContext = selectEmptyRackViewModel };
            selectEmptyRackView.Title = $"미포장 입고 랙 선택";

            if (selectEmptyRackView.ShowDialog() == true && selectEmptyRackViewModel.DialogResult == true)
            {
                var selectedRack = selectEmptyRackViewModel.SelectedRack;
                if (selectedRack != null)
                {
                    var targetRackVm = RackList?.FirstOrDefault(r => r.Id == selectedRack.Id);
                    var waitRackVm = RackList?.FirstOrDefault(r => r.Title == _waitRackTitle);

                    if (targetRackVm == null) return;
                    ShowAutoClosingMessage($"랙 {targetRackVm.Title}에 미포장 제품 {InputStringForButton.TrimStart().TrimEnd(_militaryCharacter)}의 입고 작업을 시작합니다.");

                    int newBulletType = GetBulletTypeFromInputString(_inputStringForButton); // Helper method
                    if (newBulletType == 0)
                    {
                        MessageBox.Show("입력된 Lot 번호에서 제품 정보를 알 수 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    ShowAutoClosingMessage($"랙 {waitRackVm.Title} 에서 랙 {targetRackVm.Title} 로 미포장 입고를 시작합니다. 잠금 중...");
                    List<int> lockedRackIds = new List<int>();
                    try
                    {
                        await _databaseService.UpdateIsLockedAsync(targetRackVm.Id, true);
                        Application.Current.Dispatcher.Invoke(() => targetRackVm.IsLocked = true);
                        lockedRackIds.Add(targetRackVm.Id);

                        await _databaseService.UpdateIsLockedAsync(waitRackVm.Id, true);
                        Application.Current.Dispatcher.Invoke(() => waitRackVm.IsLocked = true);
                        lockedRackIds.Add(waitRackVm.Id);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"랙 잠금 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        // 오류 발생 시 작업 취소 및 잠금 해제 시도
                        foreach (var id in lockedRackIds)
                        {
                            await _databaseService.UpdateIsLockedAsync(id, false);
                            Application.Current.Dispatcher.Invoke(() => (RackList?.FirstOrDefault(r => r.Id == id)).IsLocked = false);
                        }
                        return; // 더 이상 진행하지 않음
                    }

                    ShowAutoClosingMessage($"로봇 미션: 랙 {waitRackVm.Title} 에서 랙 {targetRackVm.Title}(으)로 이동 시작. 명령 전송 중...");

                    List<MissionStepDefinition> missionSteps;
                    string shelf = $"{int.Parse(targetRackVm.Title.Split('-')[1]):D2}_{targetRackVm.Title.Split('-')[0]}";
                    // 로봇 미션 단계 정의 (사용자 요청에 따라 4단계로 복원 및 IsLinkable, LinkedMission 조정)
                    if (targetRackVm.LocationArea == 3)
                    {
                        missionSteps = new List<MissionStepDefinition>
                        {
                            // 1. 턴 랙 (27-32) - 로봇이 랙을 회전하는 지점
                            new MissionStepDefinition { ProcessStepDescription = $"대기장소로 이동", MissionType = "8", ToNode = "Turn_Rack_27_32", Payload = _warehousePayload, IsLinkable = true, LinkWaitTimeout = 3600 },
                            // 2. 랩핑 드롭 (랩핑 스테이션으로 이동하여 드롭)
                            new MissionStepDefinition { ProcessStepDescription = $"{waitRackVm.Title} 제품 픽업 & 이동", MissionType = "7", FromNode = "Palette_OUT_PickUP", ToNode = "Turn_Rack_27_32", Payload = _warehousePayload, IsLinkable = true, LinkWaitTimeout = 3600 },
                            // 3. 다시 턴 랙 (27-32) - 아마도 WRAP 랙의 방향 정렬 또는 다음 작업을 위한 준비
                            new MissionStepDefinition { ProcessStepDescription = $"{targetRackVm.Title}(으)로 이동 & 제품 드롭", MissionType = "8", ToNode = $"Rack_{shelf}_Drop", Payload = _warehousePayload, IsLinkable = true, LinkWaitTimeout = 3600 },
                            // 4. 턴 랙 (27-32) - 로봇이 랙을 회전하는 지점
                            new MissionStepDefinition { ProcessStepDescription = $"지게차 회전을 위한 이동", MissionType = "8", ToNode = "Turn_Rack_29", Payload = _warehousePayload, IsLinkable = true, LinkWaitTimeout = 60 },
                            // 5.
                            new MissionStepDefinition {
                                ProcessStepDescription = $"충전소로 복귀", MissionType = "8", ToNode = "Charge1", Payload = _warehousePayload, IsLinkable = false, LinkWaitTimeout = 3600,
                                CheckModbusDiscreteInput = true, ModbusDiscreteInputAddressToCheck = 13, SourceRackId = waitRackVm.Id, DestinationRackId = targetRackVm.Id
                            }
                        };
                    }
                    else //if (destinationRack.LocationArea == 2 || sourceRackViewModel.LocationArea == 1)
                    {
                        missionSteps = new List<MissionStepDefinition>
                        {
                            // 1. 턴 랙 (27-32) - 로봇이 랙을 회전하는 지점
                            new MissionStepDefinition { ProcessStepDescription = $"대기장소로 이동", MissionType = "8", ToNode = "Turn_Rack_27_32", Payload = _warehousePayload, IsLinkable = true, LinkWaitTimeout = 3600 },
                            // 2. 턴 랙 (27-32) - 로봇이 랙을 회전하는 지점
                            new MissionStepDefinition { ProcessStepDescription = $"{waitRackVm.Title} 제품 픽업, {targetRackVm.Title}(을)로 이동 & 드롭", MissionType = "7", FromNode = "Palette_OUT_PickUP", ToNode = $"Rack_{shelf}_Drop", Payload = _warehousePayload, IsLinkable = true, LinkWaitTimeout = 60 },
                            // 3. 다시 턴 랙 (27-32) - 아마도 WRAP 랙의 방향 정렬 또는 다음 작업을 위한 준비
                            new MissionStepDefinition {
                                ProcessStepDescription = $"충전소로 복귀", MissionType = "8", ToNode = "Charge1", Payload = _warehousePayload, IsLinkable = false, LinkWaitTimeout = 3600,
                                CheckModbusDiscreteInput = true, ModbusDiscreteInputAddressToCheck = 13, SourceRackId = waitRackVm.Id, DestinationRackId = targetRackVm.Id
                            }
                        };
                    }

                    try
                    {
                        // 로봇 미션 프로세스 시작
                        string processId = await InitiateRobotMissionProcess(
                            "ExecuteInboundProduct", // 미션 프로세스 유형
                            missionSteps,
                            null, // SourceRack은 이제 MissionStepDefinition의 ID로 관리
                            null, // DestinationRack은 이제 MissionStepDefinition의 ID로 관리
                            null,
                            lockedRackIds // 잠긴 랙 ID 목록 전달
                        );
                        ShowAutoClosingMessage($"로봇 미션 프로세스 시작됨: {processId}");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"로봇 미션 시작 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        foreach (var id in lockedRackIds)
                        {
                            await _databaseService.UpdateIsLockedAsync(id, false);
                            Application.Current.Dispatcher.Invoke(() => (RackList?.FirstOrDefault(r => r.Id == id)).IsLocked = false);
                        }
                    }
                }
            }
            else
            {
                ShowAutoClosingMessage("미포장 입고가 취소되었습니다.");
            }
        }

        private bool CanExecuteInboundProduct(object parameter)
        {
            bool inputContainsValidProduct = !string.IsNullOrWhiteSpace(_inputStringForButton) &&
                                             (_inputStringForButton.Contains("223A")
                                             || _inputStringForButton.Contains("223SP")
                                              || _inputStringForButton.Contains("223XM")
                                               || _inputStringForButton.Contains("5.56X")
                                                || _inputStringForButton.Contains("5.56K")
                                                 || (_inputStringForButton.Contains("PSD") && _inputStringForButton.Contains(" a"))
                                                 || (_inputStringForButton.Contains("PSD") && _inputStringForButton.Contains(" b"))
                                                 || (_inputStringForButton.Contains("PSD") && _inputStringForButton.Contains(" c"))
                                                  || _inputStringForButton.Contains("308B")
                                                   || _inputStringForButton.Contains("308SP")
                                                    || _inputStringForButton.Contains("308XM")
                                                     || _inputStringForButton.Contains("7.62X")
                                             );

            bool emptyAndVisibleRackExists = RackList?.Any(r => (r.ImageIndex == 0 && r.IsVisible && !r.Title.Equals(_waitRackTitle))) == true;

            var waitRackVm = RackList?.FirstOrDefault(r => r.Title == _waitRackTitle);
            bool waitRackNotLocked = (waitRackVm?.IsLocked == false) || (waitRackVm == null);

            if (waitRackVm != null)
            {
                int newBulletTypeForWaitRack = 0;
                if (inputContainsValidProduct)
                {
                    newBulletTypeForWaitRack = GetBulletTypeFromInputString(_inputStringForButton); // Helper method
                }

                // 이 Task.Run은 CanExecute 호출 시마다 실행될 수 있으므로, 과도한 DB/UI 업데이트를 피하기 위해 주의해야 함.
                // 이 부분을 ExecuteInboundProduct 안으로 옮기는 것이 더 적절할 수 있음.
                Task.Run(async () =>
                {
                    await _databaseService.UpdateRackStateAsync(
                        waitRackVm.Id,
                        waitRackVm.RackType,
                        newBulletTypeForWaitRack
                    );
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        waitRackVm.BulletType = newBulletTypeForWaitRack;
                    });
                });
            }

            return inputContainsValidProduct && emptyAndVisibleRackExists && waitRackNotLocked;
        }

        private async void FakeExecuteInboundProduct(object parameter)
        {
            var emptyRacks = RackList?.Where(r => r.ImageIndex == 13 && r.IsVisible).ToList();

            if (emptyRacks == null || !emptyRacks.Any())
            {
                MessageBox.Show("현재 재공품을 적재할 빈 랙이 없습니다..", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectEmptyRackViewModel = new SelectEmptyRackPopupViewModel(emptyRacks.Select(r => r.RackModel).ToList(),
                _inputStringForButton.TrimStart().TrimEnd(_militaryCharacter), "재공품 적재", "재공품");
            var selectEmptyRackView = new SelectEmptyRackPopupView { DataContext = selectEmptyRackViewModel };
            selectEmptyRackView.Title = $"재공품 입고 랙 선택";

            if (selectEmptyRackView.ShowDialog() == true && selectEmptyRackViewModel.DialogResult == true)
            {
                var selectedRack = selectEmptyRackViewModel.SelectedRack;
                if (selectedRack != null)
                {
                    var targetRackVm = RackList?.FirstOrDefault(r => r.Id == selectedRack.Id);
                    var waitRackVm = RackList?.FirstOrDefault(r => r.Title == _waitRackTitle);

                    if (targetRackVm == null) return;
                    ShowAutoClosingMessage($"랙 {targetRackVm.Title}에 재공품 {InputStringForButton.TrimStart().TrimEnd(_militaryCharacter)}의 입고 작업을 시작합니다.");

                    // 🚨 ToDo : WAIT  Rack으로부터 이동 시에는 inputString의 입력을 disable해야 한다.아니면 이동 전에  Lot No.를 DB에 copy.
                    int newBulletType = GetBulletTypeFromInputString(_inputStringForButton); // Helper method
                    if (newBulletType == 0)
                    {
                        MessageBox.Show("입력된 Lot 번호에서 제품 정보를 알 수 없습니다.", "경고", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    ShowAutoClosingMessage($"랙 {waitRackVm.Title}에서 랙 {targetRackVm.Title} 로 미포장 입고를 시작합니다. 잠금 중...");
                    List<int> lockedRackIds = new List<int>();
                    try
                    {
                        await _databaseService.UpdateIsLockedAsync(targetRackVm.Id, true);
                        Application.Current.Dispatcher.Invoke(() => targetRackVm.IsLocked = true);
                        lockedRackIds.Add(targetRackVm.Id);

                        await _databaseService.UpdateIsLockedAsync(waitRackVm.Id, true);
                        Application.Current.Dispatcher.Invoke(() => waitRackVm.IsLocked = true);
                        lockedRackIds.Add(waitRackVm.Id);

                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"랙 잠금 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        // 오류 발생 시 작업 취소 및 잠금 해제 시도
                        foreach (var id in lockedRackIds)
                        {
                            await _databaseService.UpdateIsLockedAsync(id, false);
                            Application.Current.Dispatcher.Invoke(() => (RackList?.FirstOrDefault(r => r.Id == id)).IsLocked = false);
                        }
                        return; // 더 이상 진행하지 않음
                    }

                    ShowAutoClosingMessage($"로봇 미션: 랙 {waitRackVm.Title} 에서 랙 {targetRackVm.Title}(으)로 이동 시작. 명령 전송 중...");

                    List<MissionStepDefinition> missionSteps;
                    string shelf = $"{int.Parse(targetRackVm.Title.Split('-')[1]):D2}_{targetRackVm.Title.Split('-')[0]}";
                    // 로봇 미션 단계 정의 (사용자 요청에 따라 4단계로 복원 및 IsLinkable, LinkedMission 조정)
                    if (targetRackVm.LocationArea == 3)
                    {
                        missionSteps = new List<MissionStepDefinition>
                        {
                            // 1. 턴 랙 (27-32) - 로봇이 랙을 회전하는 지점
                            new MissionStepDefinition { ProcessStepDescription = $"대기장소로 이동", MissionType = "8", ToNode = "Turn_Rack_27_32", Payload = _warehousePayload, IsLinkable = true, LinkWaitTimeout = 3600 },
                            // 2. 랩핑 드롭 (랩핑 스테이션으로 이동하여 드롭)
                            new MissionStepDefinition { ProcessStepDescription = $"{waitRackVm.Title} 제품 픽업 & 이동", MissionType = "7", FromNode = "Palette_OUT_PickUP", ToNode = "Turn_Rack_27_32", Payload = _warehousePayload, IsLinkable = true, LinkWaitTimeout = 3600 },
                            // 3. 다시 턴 랙 (27-32) - 아마도 WRAP 랙의 방향 정렬 또는 다음 작업을 위한 준비
                            new MissionStepDefinition { ProcessStepDescription = $"{targetRackVm.Title}(으)로 이동 & 제품 드롭", MissionType = "8", ToNode = $"Rack_{shelf}_Drop", Payload = _warehousePayload, IsLinkable = true, LinkWaitTimeout = 3600 },
                            // 4. 턴 랙 (27-32) - 로봇이 랙을 회전하는 지점
                            new MissionStepDefinition { ProcessStepDescription = $"지게차 회전을 위한 이동", MissionType = "8", ToNode = "Turn_Rack_29", Payload = _warehousePayload, IsLinkable = true, LinkWaitTimeout = 60 },
                            // 5.
                            new MissionStepDefinition {
                                ProcessStepDescription = $"충전소로 복귀", MissionType = "8", ToNode = "Charge1", Payload = _warehousePayload, IsLinkable = false, LinkWaitTimeout = 3600,
                                CheckModbusDiscreteInput = true, ModbusDiscreteInputAddressToCheck = 13, SourceRackId = waitRackVm.Id, DestinationRackId = targetRackVm.Id
                            }
                        };
                    }
                    else //if (destinationRack.LocationArea == 2 || sourceRackViewModel.LocationArea == 1)
                    {
                        missionSteps = new List<MissionStepDefinition>
                        {
                            // 1. 턴 랙 (27-32) - 로봇이 랙을 회전하는 지점
                            new MissionStepDefinition { ProcessStepDescription = $"대기장소로 이동", MissionType = "8", ToNode = "Turn_Rack_27_32", Payload = _warehousePayload, IsLinkable = true, LinkWaitTimeout = 3600 },
                            // 2. 턴 랙 (27-32) - 로봇이 랙을 회전하는 지점
                            new MissionStepDefinition { ProcessStepDescription = $"{waitRackVm.Title} 제품 픽업, {targetRackVm.Title}(으)로 이동 & 드롭", MissionType = "7", FromNode = "Palette_OUT_PickUP", ToNode = $"Rack_{shelf}_Drop", Payload = _warehousePayload, IsLinkable = true, LinkWaitTimeout = 60 },
                            // 3. 다시 턴 랙 (27-32) - 아마도 WRAP 랙의 방향 정렬 또는 다음 작업을 위한 준비
                            new MissionStepDefinition {
                                ProcessStepDescription = $"충전소로 복귀", MissionType = "8", ToNode = "Charge1", Payload = _warehousePayload, IsLinkable = false, LinkWaitTimeout = 3600,
                                CheckModbusDiscreteInput = true, ModbusDiscreteInputAddressToCheck = 13, SourceRackId = waitRackVm.Id, DestinationRackId = targetRackVm.Id
                            }
                        };
                    }

                    try
                    {
                        // 로봇 미션 프로세스 시작
                        string processId = await InitiateRobotMissionProcess(
                            "FakeExecuteInboundProduct", // 미션 프로세스 유형
                            missionSteps,
                            null, // SourceRack은 이제 MissionStepDefinition의 ID로 관리
                            null, // DestinationRack은 이제 MissionStepDefinition의 ID로 관리
                            null,
                            lockedRackIds // 잠긴 랙 ID 목록 전달
                        );
                        ShowAutoClosingMessage($"로봇 미션 프로세스 시작됨: {processId}");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"로봇 미션 시작 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        foreach (var id in lockedRackIds)
                        {
                            await _databaseService.UpdateIsLockedAsync(id, false);
                            Application.Current.Dispatcher.Invoke(() => (RackList?.FirstOrDefault(r => r.Id == id)).IsLocked = false);
                        }
                    }
                }
            }
            else
            {
                ShowAutoClosingMessage("재공품 입고가 취소되었습니다.");
            }
        }

        private bool CanFakeExecuteInboundProduct(object parameter)
        {
            bool inputContainsValidProduct = !string.IsNullOrWhiteSpace(_inputStringForButton) &&
                                             (_inputStringForButton.Contains("223A")
                                             || _inputStringForButton.Contains("223SP")
                                              || _inputStringForButton.Contains("223XM")
                                               || _inputStringForButton.Contains("5.56X")
                                                || _inputStringForButton.Contains("5.56K")
                                                 || (_inputStringForButton.Contains("PSD") && _inputStringForButton.Contains(" a"))
                                                 || (_inputStringForButton.Contains("PSD") && _inputStringForButton.Contains(" b"))
                                                 || (_inputStringForButton.Contains("PSD") && _inputStringForButton.Contains(" c"))
                                                  || _inputStringForButton.Contains("308B")
                                                   || _inputStringForButton.Contains("308SP")
                                                    || _inputStringForButton.Contains("308XM")
                                                     || _inputStringForButton.Contains("7.62X")
                                             );

            bool emptyAndVisibleRackExists = RackList?.Any(r => (r.ImageIndex == 13 && r.IsVisible)) == true;

            var waitRackVm = RackList?.FirstOrDefault(r => r.Title == _waitRackTitle);

            bool waitRackNotLocked = (waitRackVm?.IsLocked == false) || (waitRackVm == null);

            return inputContainsValidProduct && emptyAndVisibleRackExists && waitRackNotLocked;
        }

        private async void ExecuteCheckoutProduct(object parameter)
        {
            if (parameter is CheckoutRequest request)
            {
                var availableRacksForCheckout = RackList?.Where(r => r.RackType == 1 && r.BulletType == request.BulletType && r.LotNumber.Contains((InputStringForShipOut == null || InputStringForShipOut == "") ? "" : "-" + InputStringForShipOut) && !r.IsLocked).Select(rvm => rvm.RackModel).ToList();
                var productName = request.ProductName;

                if (availableRacksForCheckout == null || !availableRacksForCheckout.Any())
                {
                    MessageBox.Show($"출고할 {productName} 제품이 있는 랙이 없습니다..", $"{productName} 제품 출고 불가능", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectCheckoutRackViewModel = new SelectCheckoutRackPopupViewModel(availableRacksForCheckout);
                var selectCheckoutRackView = new SelectCheckoutRackPopupView { DataContext = selectCheckoutRackViewModel };
                selectCheckoutRackView.Title = $"출고할 {productName} 제품 선택";

                if (selectCheckoutRackView.ShowDialog() == true && selectCheckoutRackViewModel.DialogResult == true)
                {
                    var selectedRacksForCheckout = selectCheckoutRackViewModel.GetSelectedRacks();

                    if (selectedRacksForCheckout == null || !selectedRacksForCheckout.Any())
                    {
                        MessageBox.Show("선택된 랙이 없습니다.", "출고 취소", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    ShowAutoClosingMessage($"{selectedRacksForCheckout.Count} 개 랙의 {productName} 제품 출고가 시작됩니다.");

                    //var targetRackVmsToLock = selectedRacksForCheckout.Select(r => RackList?.FirstOrDefault(rvm => rvm.Id == r.Id))
                    //                                                   .Where(rvm => rvm != null)
                    //                                                   .ToList();
                    List<int> lockedRackIds = new List<int>();
                    List<RackViewModel> racksToProcess = new List<RackViewModel>(); // RobotMissionInfo에 전달할 ViewModel 목록
                    try
                    {
                        foreach (var rackModel in selectedRacksForCheckout)
                        {
                            var rvm = RackList?.FirstOrDefault(r => r.Id == rackModel.Id);
                            if (rvm != null)
                            {
                                await _databaseService.UpdateIsLockedAsync(rvm.Id, true);
                                Application.Current.Dispatcher.Invoke(() => rvm.IsLocked = true);
                                lockedRackIds.Add(rvm.Id);
                                racksToProcess.Add(rvm); // ViewModel 추가
                            }
                        }
                        //foreach (var rvm in targetRackVmsToLock)
                        //{
                        //    await _databaseService.UpdateIsLockedAsync(rvm.Id, true);
                        //    Application.Current.Dispatcher.Invoke(() => rvm.IsLocked = true);
                        //}
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"랙 잠금 중 오류 발생: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                        Debug.WriteLine($"[Checkout] Error locking racks for checkout: {ex.Message}");
                        foreach (var id in lockedRackIds)
                        {
                            await _databaseService.UpdateIsLockedAsync(id, false);
                            Application.Current.Dispatcher.Invoke(() => (RackList?.FirstOrDefault(r => r.Id == id)).IsLocked = false);
                        }
                        return;
                    }

                    // 여러 랙 출고 시나리오를 위한 missionSteps 구성
                    List<MissionStepDefinition> missionSteps = new List<MissionStepDefinition>();
                    foreach (var rackModelToCheckout in selectedRacksForCheckout)
                    {
                        var targetRackVm = RackList?.FirstOrDefault(r => r.Id == rackModelToCheckout.Id);
                        if (targetRackVm == null) continue;

                        string shelf = $"{int.Parse(targetRackVm.Title.Split('-')[1]):D2}_{targetRackVm.Title.Split('-')[0]}";

                        // 각 랙에 대한 픽업 및 드롭 미션 스텝 추가
                        // MissionStepDefinition에서는 더 이상 DB 업데이트 정보를 포함하지 않습니다.
                        // 이 정보는 RobotMissionService의 RacksToProcess와 ProcessType을 통해 HandleRobotMissionCompletion에서 처리됩니다.
                        if (targetRackVm.LocationArea == 3)
                        {
                            missionSteps.Add(new MissionStepDefinition { ProcessStepDescription = $"{targetRackVm.Title} 제품 픽업 & 이동", MissionType = "7", FromNode = $"Rack_{shelf}_PickUP", ToNode = "Turn_Rack_29", Payload = _warehousePayload, IsLinkable = true, LinkedMission = null, LinkWaitTimeout = 3600 });
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = $"출고 장소로 이동 & 제품 드롭",
                                MissionType = "8",
                                ToNode = "WaitProduct_1_Drop",
                                Payload = _warehousePayload,
                                IsLinkable = false,
                                LinkedMission = null,
                                LinkWaitTimeout = 3600,
                                SourceRackId = targetRackVm.Id,
                                DestinationRackId = null // 출고는 소스 랙만 비우므로 SourceRackId만 설정
                            });
                        }
                        else if (targetRackVm.LocationArea == 2)
                        {
                            missionSteps.Add(new MissionStepDefinition { ProcessStepDescription = $"대기장소로 이동", MissionType = "8", ToNode = "Turn_Rack_27_32", Payload = _warehousePayload, IsLinkable = true, LinkedMission = null, LinkWaitTimeout = 3600 });
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = $"{targetRackVm.Title} 제품 픽업, 출고 장소로 이동 & 드롭",
                                MissionType = "7",
                                FromNode = $"Rack_{shelf}_PickUP",
                                ToNode = "WaitProduct_1_Drop",
                                Payload = _warehousePayload,
                                IsLinkable = false,
                                LinkedMission = null,
                                LinkWaitTimeout = 3600,
                                SourceRackId = targetRackVm.Id,
                                DestinationRackId = null // 출고는 소스 랙만 비우므로 SourceRackId만 설정
                            });

                        }
                        else //if (targetRackVm.LocationArea == 1)
                        {
                            missionSteps.Add(new MissionStepDefinition { ProcessStepDescription = $"{targetRackVm.Title} 제품 픽업 & 이동", MissionType = "7", FromNode = $"Rack_{shelf}_PickUP", ToNode = "Turn_Rack_27_32", Payload = _warehousePayload, IsLinkable = true, LinkedMission = null, LinkWaitTimeout = 3600 });
                            missionSteps.Add(new MissionStepDefinition
                            {
                                ProcessStepDescription = $"출고 장소로 이동 & 제품 드롭",
                                MissionType = "8",
                                ToNode = "WaitProduct_1_Drop",
                                Payload = _warehousePayload,
                                IsLinkable = false,
                                LinkedMission = null,
                                LinkWaitTimeout = 3600,
                                SourceRackId = targetRackVm.Id,
                                DestinationRackId = null // 충전소 복귀는 랙 상태 변화가 없음
                            });
                        }
                    }
                    missionSteps.Add(new MissionStepDefinition
                    {
                        ProcessStepDescription = "충전소로 복귀",
                        MissionType = "8",
                        ToNode = "Charge1",
                        Payload = _warehousePayload,
                        IsLinkable = false,
                        LinkedMission = null,
                        LinkWaitTimeout = 3600,
                        SourceRackId = null,
                        DestinationRackId = null // 충전소 복귀는 랙 상태 변화가 없음
                    });

                    try
                    {
                        // 로봇 미션 프로세스 시작 (sourceRack, destinationRack은 이제 null로 전달)
                        string processId = await InitiateRobotMissionProcess(
                            "ExecuteCheckoutProduct", // 미션 프로세스 유형
                            missionSteps,
                            null, // SourceRack은 이제 MissionStepDefinition의 ID로 관리
                            null, // DestinationRack은 이제 MissionStepDefinition의 ID로 관리
                            null,
                            lockedRackIds, // 잠긴 랙 ID 목록 전달
                            racksToProcess // 처리할 랙 ViewModel 목록 전달
                        );
                        ShowAutoClosingMessage($"로봇 미션 프로세스 시작됨: {processId}");
                        // **중요**: 로봇 미션이 시작되었으므로, 이 시점에서는 랙의 잠금 상태만 유지하고,
                        // 실제 DB 업데이트 (비우기, 채우기)는 RobotMissionService의 폴링 로직에서
                        // 미션 완료 시점에 이루어지도록 위임합니다.

                    }
                    catch (Exception ex) // 외부 try-catch 추가
                    {
                        MessageBox.Show($"출고 작업을 시작하는 중 오류가 발생: {ex.Message}", "예외 발생", MessageBoxButton.OK, MessageBoxImage.Error);
                        Debug.WriteLine($"[Checkout] Error initiating checkout: {ex.GetType().Name} - {ex.Message}");
                        foreach (var id in lockedRackIds)
                        {
                            await _databaseService.UpdateIsLockedAsync(id, false);
                            Application.Current.Dispatcher.Invoke(() => (RackList?.FirstOrDefault(r => r.Id == id)).IsLocked = false);
                        }
                    }
                }
            }
            else
            {
                ShowAutoClosingMessage("재공품 입고가 취소되었습니다.");
            }
        }

        private bool CanExecuteCheckoutProduct(object parameter)
        {
            if (parameter is CheckoutRequest request)
            {
                return RackList?.Any(r => r.RackType == 1 && r.BulletType == request.BulletType && !r.IsLocked && r.LotNumber.Contains((InputStringForShipOut == null || InputStringForShipOut == "") ? "" : "-" + InputStringForShipOut)) == true;
            }
            return false;
        }

        private void RaiseAllCheckoutCanExecuteChanged()
        {
            ((RelayCommand)InboundProductCommand).RaiseCanExecuteChanged();
            ((RelayCommand)Checkout223aProductCommand).RaiseCanExecuteChanged();
            ((RelayCommand)Checkout223spProductCommand).RaiseCanExecuteChanged();
            ((RelayCommand)Checkout223xmProductCommand).RaiseCanExecuteChanged();
            ((RelayCommand)Checkout556xProductCommand).RaiseCanExecuteChanged();
            ((RelayCommand)Checkout556kProductCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CheckoutM855tProductCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CheckoutM193ProductCommand).RaiseCanExecuteChanged();
            ((RelayCommand)Checkout308bProductCommand).RaiseCanExecuteChanged();
            ((RelayCommand)Checkout308spProductCommand).RaiseCanExecuteChanged();
            ((RelayCommand)Checkout308xmProductCommand).RaiseCanExecuteChanged();
            ((RelayCommand)Checkout762xProductCommand).RaiseCanExecuteChanged();
            ((RelayCommand)CheckoutM80ProductCommand).RaiseCanExecuteChanged();
        }

        // Helper method to get BulletType from input string (copied from CanExecuteInboundProduct)
        private int GetBulletTypeFromInputString(string inputString)
        {
            if (inputString.Contains("223A")) return 1;
            if (inputString.Contains("223SP")) return 2;
            if (inputString.Contains("223XM")) return 3;
            if (inputString.Contains("5.56X")) return 4;
            if (inputString.Contains("5.56K")) return 5;
            if (inputString.Contains("PSD") && inputString.Contains(" a")) return 6; // M855T
            if (inputString.Contains("PSD") && inputString.Contains(" b")) return 7; // M193
            if (inputString.Contains("PSD") && inputString.Contains(" c")) return 12; // M80
            if (inputString.Contains("308B")) return 8;
            if (inputString.Contains("308SP")) return 9;
            if (inputString.Contains("308XM")) return 10;
            if (inputString.Contains("7.62X")) return 11;
            return 0; // Default or invalid
        }
    }
}
