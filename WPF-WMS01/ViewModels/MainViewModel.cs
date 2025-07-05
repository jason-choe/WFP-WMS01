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
using System.Text.Json; // System.Text.Json은 사용되지 않으므로 제거 가능
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading; // DispatcherTimer 사용을 위해 추가
using Newtonsoft.Json;
using JsonException = Newtonsoft.Json.JsonException;

namespace WPF_WMS01.ViewModels
{
    // Modbus 버튼의 상태를 나타내는 ViewModel (각 버튼에 바인딩될 개별 항목)
    public class ModbusButtonViewModel : ViewModelBase
    {
        private bool _isEnabled; // 버튼의 활성화 상태 (Coil 1일 때 true, 그리고 작업 중이 아닐 때)
        private string _content; // 버튼에 표시될 텍스트 (예: "팔레트 공급")
        private ushort _discreteInputAddress; // 해당 버튼이 관여하는 Modbus Discrete Input 주소
        private ushort _coilOutputAddress; // 해당 버튼에 대응하는 Modbus Coil Output 주소 (경광등)
        private bool _isProcessing; // 비동기 작업 진행 중 여부
        private int _currentProgress; // 진행률 (0-100)
        private bool _isTaskInitiatedByDiscreteInput; // Discrete Input에 의해 작업이 시작되었음을 나타내는 플래그 (중복 트리거 방지용)
        private bool _currentDiscreteInputState; // 현재 Discrete Input 상태를 저장 (이전 상태 비교용)

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
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
                if (SetProperty(ref _isProcessing, value))
                {
                    // IsProcessing이 변경되면 Command의 CanExecute를 재평가하여 버튼 상태 갱신
                    ((RelayCommand)ExecuteButtonCommand)?.RaiseCanExecuteChanged();
                }
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
            set => SetProperty(ref _isTaskInitiatedByDiscreteInput, value);
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
                        ((RelayCommand)buttonVm.ExecuteButtonCommand)?.RaiseCanExecuteChanged();
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

        // Constructor: IRobotMissionService parameter removed
        public MainViewModel(DatabaseService databaseService, HttpService httpService, ModbusClientService modbusService,
                             string warehousePayload, string productionLinePayload)
        {
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
            // _robotMissionServiceInternal is not set here. It will be set via SetRobotMissionService.

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

            InitializeCommands(); // 기존의 다른 명령 초기화

            SetupRefreshTimer(); // RackList 갱신 타이머
            SetupModbusReadTimer(); // Modbus Coil 상태 읽기 타이머 설정
            _ = LoadRacksAsync();
            _ = AutoLoginOnStartup();
        }

        /// <summary>
        /// RobotMissionService 인스턴스를 설정하는 메서드 (App.xaml.cs에서 호출)
        /// </summary>
        /// <param name="service">주입할 IRobotMissionService 인스턴스</param>
        public void SetRobotMissionService(IRobotMissionService service)
        {
            _robotMissionServiceInternal = service;
            SetupRobotMissionServiceEvents(); // 서비스가 설정된 후에 이벤트 구독
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

                // 새로 구독
                _robotMissionServiceInternal.OnShowAutoClosingMessage += ShowAutoClosingMessage;
                _robotMissionServiceInternal.OnRackLockStateChanged += OnRobotMissionRackLockStateChanged;
                _robotMissionServiceInternal.OnInputStringForButtonCleared += () => InputStringForButton = string.Empty;
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
                    rackViewModels.Add(new RackViewModel(rack, _databaseService, this));
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
                    RackList.Add(new RackViewModel(newRackData, _databaseService, this));
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
        /// <returns>시작된 미션 프로세스의 고유 ID.</returns>
        public async Task<string> InitiateRobotMissionProcess(
            string processType,
            List<MissionStepDefinition> missionSteps,
            RackViewModel sourceRack = null, // 이제 이 파라미터는 null로 전달됨
            RackViewModel destinationRack = null, // 이제 이 파라미터는 null로 전달됨
            Location destinationLine = null,
            List<int> racksLockedAtStart = null,
            List<RackViewModel> racksToProcess = null)
        {
            if (_robotMissionServiceInternal == null)
            {
                Debug.WriteLine("[MainViewModel] RobotMissionService is not initialized.");
                ShowAutoClosingMessage("로봇 미션 서비스를 초기화할 수 없습니다. 관리자에게 문의하세요.");
                return null;
            }

            return await _robotMissionServiceInternal.InitiateRobotMissionProcess(
                processType,
                missionSteps,
                sourceRack, // 여전히 인터페이스 호환을 위해 전달하지만, RobotMissionService에서 사용하지 않음
                destinationRack, // 여전히 인터페이스 호환을 위해 전달하지만, RobotMissionService에서 사용하지 않음
                destinationLine,
                () => InputStringForButton,
                racksLockedAtStart,
                racksToProcess
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
            if (!_modbusService.IsConnected)
            {
                Debug.WriteLine("[ModbusService] Read Timer: Not Connected. Attempting to reconnect asynchronously...");
                await _modbusService.ConnectAsync().ConfigureAwait(false); // ConfigureAwait(false) 사용
                if (!_modbusService.IsConnected)
                {
                    Debug.WriteLine("[ModbusService] Read Timer: Connection failed after reconnect attempt. Skipping Modbus read.");
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

                            // PLC 신호 (Discrete Input 0->1) 감지 및 PLC가 Run 상태일 때
                            if (currentDiscreteInputState && !previousDiscreteInputState && !buttonVm.IsProcessing && !buttonVm.IsTaskInitiatedByDiscreteInput)
                            {
                                // 이 버튼에 대한 작업이 이미 시작되지 않았고, Discrete Input이 0에서 1로 전환되었다면
                                buttonVm.IsTaskInitiatedByDiscreteInput = true; // 작업 시작 플래그 설정
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
                                    buttonVm.IsTaskInitiatedByDiscreteInput = false; // 작업 시작 실패했으므로 플래그 리셋
                                }
                            }
                            // 경광등이 꺼진 것이 감지되면 (Coil 1 -> 0) Discrete Input을 0으로 초기화
                            // 이 부분은 PLC가 처리하는 것이 일반적이지만, HMI가 트리거하는 경우를 위해 남겨둠.
                            // 스펙상 "경광등이 꺼지면 (1 -> 0) 이에 트리거되어 call button의 입력이 0으로 바뀐다"는 PLC의 동작을 의미.
                            // HMI는 단순히 경광등을 끄는 역할만 하므로, 여기서는 Discrete Input을 HMI가 직접 0으로 바꾸는 로직은 필요 없음.
                            // HMI가 경광등을 0으로 쓴 후, 다음 ModbusReadTimer_Tick에서 PLC의 Discrete Input이 0으로 바뀐 것을 확인하게 될 것임.

                            // 버튼의 IsEnabled 상태는 PLC가 Run 상태이고, Discrete Input이 1이고, 현재 작업 중이 아닐 때 활성화
                            buttonVm.IsEnabled = PlcStatusIsRun && currentDiscreteInputState && !buttonVm.IsProcessing;
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
            }
        }

        // Modbus 버튼 클릭 시 실행될 Command (HMI에서 작업 진행 상황 확인)
        private async Task ExecuteModbusButtonCommand(ModbusButtonViewModel buttonVm)
        {
            if (buttonVm == null) return;

            // 버튼 클릭 시점의 상태에 따라 팝업 메시지 표시
            if (buttonVm.IsProcessing)
            {
                ShowAutoClosingMessage($"[Modbus] {buttonVm.Content} 작업 진행 중: {buttonVm.CurrentProgress}% (주소: {buttonVm.DiscreteInputAddress}).");
                Debug.WriteLine($"[Modbus] Button clicked for {buttonVm.Content}. Task is already processing. Displaying current progress.");
            }
            else if (buttonVm.IsTaskInitiatedByDiscreteInput && buttonVm.CurrentDiscreteInputState) // Discrete Input이 1이고, 작업이 스케줄링되었지만 아직 진행 중은 아닌 경우 (10초 지연 중)
            {
                ShowAutoClosingMessage($"[Modbus] {buttonVm.Content} 작업 대기 중. 10초 지연 후 자동 시작됩니다. (주소: {buttonVm.DiscreteInputAddress}).");
                Debug.WriteLine($"[Modbus] Button clicked for {buttonVm.Content}. Discrete Input is active, task is scheduled to start soon.");
            }
            else if (buttonVm.IsEnabled && buttonVm.CurrentDiscreteInputState) // PLC가 Run이고 Discrete Input이 1이지만, 다른 조건으로 작업이 시작되지 않은 경우 (거의 없겠지만)
            {
                ShowAutoClosingMessage($"[Modbus] {buttonVm.Content} 작업 시작 준비 완료. (주소: {buttonVm.DiscreteInputAddress}).");
                Debug.WriteLine($"[Modbus] Button clicked for {buttonVm.Content}. Discrete Input is active and ready.");
            }
            else // 버튼이 비활성화된 경우 (PLC Stop 또는 Discrete Input 0)
            {
                string status = PlcStatusIsRun ? $"Discrete Input {buttonVm.DiscreteInputAddress}이 0입니다." : "PLC가 정지 상태입니다.";
                ShowAutoClosingMessage($"[Modbus] {buttonVm.Content} 작업 비활성화됨. ({status}).");
                Debug.WriteLine($"[Modbus] Button clicked for {buttonVm.Content}. Not active. Status: {status}");
            }
        }

        // Discrete Input이 0에서 1로 활성화될 때 수행될 비동기 작업
        private async Task HandleCallButtonActivatedTask(ModbusButtonViewModel buttonVm)
        {
            if (buttonVm == null) return;

            // 작업이 이미 진행 중인 경우 중복 실행 방지
            if (buttonVm.IsProcessing)
            {
                Debug.WriteLine($"[Modbus] Task for {buttonVm.Content} is already processing. Skipping new initiation.");
                return;
            }

            // UI 스레드에서 IsProcessing 및 CurrentProgress 업데이트 시작
            Application.Current.Dispatcher.Invoke(() =>
            {
                buttonVm.IsProcessing = true; // 작업 진행 중 상태로 변경 (UI에 ProgressBar 표시)
                buttonVm.CurrentProgress = 0; // 진행률 초기화
            });
            Debug.WriteLine($"[Modbus] Async task started for {buttonVm.Content} (Discrete Input: {buttonVm.DiscreteInputAddress}).");

            const int totalDurationSeconds = 45; // 45초 (30초 ~ 1분 사이)
            const int updateIntervalMs = 500; // 0.5초마다 진행률 업데이트
            int totalSteps = totalDurationSeconds * 1000 / updateIntervalMs;

            try
            {
                for (int i = 0; i <= totalSteps; i++)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        buttonVm.CurrentProgress = (int)((double)i / totalSteps * 100);
                    });

                    await Task.Delay(updateIntervalMs).ConfigureAwait(false);
                }

                // 작업 완료 후 해당 경광등 Coil을 0으로 쓰기 (App -> PLC)
                // 이 0으로 쓰는 동작이 PLC의 Discrete Input을 0으로 초기화하는 트리거가 됨.
                bool writeLampOffSuccess = await _modbusService.WriteSingleCoilAsync(buttonVm.CoilOutputAddress, false).ConfigureAwait(false);
                if (!writeLampOffSuccess)
                {
                    ShowAutoClosingMessage($"[Modbus] {buttonVm.Content} 경광등 끄기 실패!");
                    Debug.WriteLine($"[Modbus] Failed to write Lamp Coil {buttonVm.CoilOutputAddress} to OFF (0) after task completion.");
                }
                else
                {
                    Debug.WriteLine($"[Modbus] Lamp Coil {buttonVm.CoilOutputAddress} set to OFF (0) after task completion. Expecting PLC to reset Discrete Input {buttonVm.DiscreteInputAddress}.");
                }

                // UI 스레드에서 작업 완료 메시지 표시
                ShowAutoClosingMessage($"[Modbus] {buttonVm.Content} 작업 완료! (Discrete Input: {buttonVm.DiscreteInputAddress})");
            }
            catch (Exception ex)
            {
                // 작업 중 오류 발생 시 메시지 팝업 (UI 스레드에서)
                ShowAutoClosingMessage($"[Modbus] {buttonVm.Content} 작업 중 오류 발생: {ex.Message}");
                Debug.WriteLine($"[Modbus] Error during {buttonVm.Content} task: {ex.GetType().Name} - {ex.Message}");
            }
            finally
            {
                // 작업 완료 (성공/실패 무관) 후 UI 상태 업데이트 (UI 스레드에서)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    buttonVm.IsProcessing = false; // 작업 완료 상태로 변경 (ProgressBar 숨김)
                    buttonVm.CurrentProgress = 0; // 진행률 초기화
                    buttonVm.IsTaskInitiatedByDiscreteInput = false; // 다음 PLC 신호를 위해 플래그 리셋
                });
            }
        }

        // Modbus 버튼 활성화 여부를 결정하는 CanExecute 로직
        private bool CanExecuteModbusButtonCommand(ModbusButtonViewModel buttonVm)
        {
            // 버튼은 PLC가 Run 상태이고, 해당 Discrete Input이 1이며, 현재 비동기 작업이 진행 중이 아닐 때만 클릭 가능합니다.
            // 클릭은 작업 시작이 아닌, 진행 상황 확인/정보 표시용입니다.
            return PlcStatusIsRun && buttonVm?.CurrentDiscreteInputState == true && !buttonVm.IsProcessing;
        }

        public void Dispose()
        {
            _refreshTimer?.Stop();
            _refreshTimer.Tick -= RefreshTimer_Tick;
            _modbusReadTimer?.Stop(); // Modbus 타이머도 해제
            _modbusReadTimer.Tick -= ModbusReadTimer_Tick;

            _modbusService?.Dispose(); // Modbus 서비스 자원 해제
            _robotMissionServiceInternal?.Dispose(); // 로봇 미션 서비스 자원 해제
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
                            new MissionStepDefinition { ProcessStepDescription = $"{waitRackVm.Title} 픽업 준비", MissionType = "8", ToNode = "Turn_Rack_27_32", Payload = _warehousePayload, IsLinkable = true, LinkWaitTimeout = 3600 },
                            // 2. 랩핑 드롭 (랩핑 스테이션으로 이동하여 드롭)
                            new MissionStepDefinition { ProcessStepDescription = $"{waitRackVm.Title} 제품 픽업", MissionType = "7", FromNode = "Palette_OUT_PickUP", ToNode = "Turn_Rack_27_32", Payload = _warehousePayload, IsLinkable = true, LinkWaitTimeout = 3600 },
                            // 3. 다시 턴 랙 (27-32) - 아마도 WRAP 랙의 방향 정렬 또는 다음 작업을 위한 준비
                            new MissionStepDefinition { ProcessStepDescription = $"{targetRackVm.Title} 제품 드롭", MissionType = "8", ToNode = $"Rack_{shelf}_Drop", Payload = _warehousePayload, IsLinkable = true, LinkWaitTimeout = 3600 },
                            // 4. 턴 랙 (27-32) - 로봇이 랙을 회전하는 지점
                            new MissionStepDefinition { ProcessStepDescription = $"{targetRackVm.Title} 운반 완료", MissionType = "8", ToNode = "Turn_Rack_29", Payload = _warehousePayload, IsLinkable = true, LinkWaitTimeout = 3600 },
                            // 5.
                            new MissionStepDefinition {
                                ProcessStepDescription = $"{targetRackVm.Title} 복귀 완료", MissionType = "8", ToNode = "Charge1", Payload = _warehousePayload, IsLinkable = false, LinkWaitTimeout = 3600,
                                SourceRackId = waitRackVm.Id, DestinationRackId = targetRackVm.Id
                            }
                        };
                    }
                    else //if (destinationRack.LocationArea == 2 || sourceRackViewModel.LocationArea == 1)
                    {
                        missionSteps = new List<MissionStepDefinition>
                        {
                            // 1. 턴 랙 (27-32) - 로봇이 랙을 회전하는 지점
                            new MissionStepDefinition { ProcessStepDescription = $"{waitRackVm.Title} 픽업 준비", MissionType = "8", ToNode = "Turn_Rack_27_32", Payload = _warehousePayload, IsLinkable = true, LinkWaitTimeout = 3600 },
                            // 2. 턴 랙 (27-32) - 로봇이 랙을 회전하는 지점
                            new MissionStepDefinition { ProcessStepDescription = $"{waitRackVm.Title} 제품 픽업 & 드롭", MissionType = "7", FromNode = "Palette_OUT_PickUP", ToNode = $"Rack_{shelf}_Drop", Payload = _warehousePayload, IsLinkable = true, LinkWaitTimeout = 3600 },
                            // 3. 다시 턴 랙 (27-32) - 아마도 WRAP 랙의 방향 정렬 또는 다음 작업을 위한 준비
                            new MissionStepDefinition {
                                ProcessStepDescription = $"{targetRackVm.Title} 복귀 완료", MissionType = "8", ToNode = "Charge1", Payload = _warehousePayload, IsLinkable = false, LinkWaitTimeout = 3600,
                                SourceRackId = waitRackVm.Id, DestinationRackId = targetRackVm.Id
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

            return inputContainsValidProduct && waitRackNotLocked;
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
                            new MissionStepDefinition { ProcessStepDescription = $"{waitRackVm.Title} 픽업 준비", MissionType = "8", ToNode = "Turn_Rack_27_32", Payload = _warehousePayload, IsLinkable = true, LinkWaitTimeout = 3600 },
                            // 2. 랩핑 드롭 (랩핑 스테이션으로 이동하여 드롭)
                            new MissionStepDefinition { ProcessStepDescription = $"{waitRackVm.Title} 제품 픽업", MissionType = "7", FromNode = "Palette_OUT_PickUP", ToNode = "Turn_Rack_27_32", Payload = _warehousePayload, IsLinkable = true, LinkWaitTimeout = 3600 },
                            // 3. 다시 턴 랙 (27-32) - 아마도 WRAP 랙의 방향 정렬 또는 다음 작업을 위한 준비
                            new MissionStepDefinition { ProcessStepDescription = $"{targetRackVm.Title} 제품 드롭", MissionType = "8", ToNode = $"Rack_{shelf}_Drop", Payload = _warehousePayload, IsLinkable = true, LinkWaitTimeout = 3600 },
                            // 4. 턴 랙 (27-32) - 로봇이 랙을 회전하는 지점
                            new MissionStepDefinition { ProcessStepDescription = $"{targetRackVm.Title} 운반 완료", MissionType = "8", ToNode = "Turn_Rack_29", Payload = _warehousePayload, IsLinkable = true, LinkWaitTimeout = 3600 },
                            // 5.
                            new MissionStepDefinition {
                                ProcessStepDescription = $"{targetRackVm.Title} 복귀 완료", MissionType = "8", ToNode = "Charge1", Payload = _warehousePayload, IsLinkable = false, LinkWaitTimeout = 3600,
                                SourceRackId = waitRackVm.Id, DestinationRackId = targetRackVm.Id
                            }
                        };
                    }
                    else //if (destinationRack.LocationArea == 2 || sourceRackViewModel.LocationArea == 1)
                    {
                        missionSteps = new List<MissionStepDefinition>
                        {
                            // 1. 턴 랙 (27-32) - 로봇이 랙을 회전하는 지점
                            new MissionStepDefinition { ProcessStepDescription = $"{waitRackVm.Title} 픽업 준비", MissionType = "8", ToNode = "Turn_Rack_27_32", Payload = _warehousePayload, IsLinkable = true, LinkWaitTimeout = 3600 },
                            // 1. 턴 랙 (27-32) - 로봇이 랙을 회전하는 지점
                            new MissionStepDefinition { ProcessStepDescription = $"{waitRackVm.Title} 제품 픽업 & 드롭", MissionType = "7", FromNode = "Palette_OUT_PickUP", ToNode = $"Rack_{shelf}_Drop", Payload = _warehousePayload, IsLinkable = true, LinkWaitTimeout = 3600 },
                            // 3. 다시 턴 랙 (27-32) - 아마도 WRAP 랙의 방향 정렬 또는 다음 작업을 위한 준비
                            new MissionStepDefinition {
                                ProcessStepDescription = $"{targetRackVm.Title} 복귀 완료", MissionType = "8", ToNode = "Charge1", Payload = _warehousePayload, IsLinkable = false, LinkWaitTimeout = 3600,
                                SourceRackId = waitRackVm.Id, DestinationRackId = targetRackVm.Id
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

            return inputContainsValidProduct && waitRackNotLocked;
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
                            missionSteps.Add(new MissionStepDefinition { ProcessStepDescription = $"{targetRackVm.Title} 제품 픽업", MissionType = "7", FromNode = $"Rack_{shelf}_PickUP", ToNode = "Turn_Rack_29", Payload = _warehousePayload, IsLinkable = true, LinkedMission = null, LinkWaitTimeout = 3600 });
                            missionSteps.Add(new MissionStepDefinition { ProcessStepDescription = $"{targetRackVm.Title} 제품 드롭", MissionType = "8", ToNode = "WaitProduct_1_Drop", Payload = _warehousePayload, IsLinkable = false, LinkedMission = null, LinkWaitTimeout = 3600,
                                SourceRackId = targetRackVm.Id, DestinationRackId = null // 출고는 소스 랙만 비우므로 SourceRackId만 설정
                            });
                        }
                        else if (targetRackVm.LocationArea == 2)
                        {
                            missionSteps.Add(new MissionStepDefinition { ProcessStepDescription = $"{targetRackVm.Title} 회전 준비", MissionType = "8", ToNode = "Turn_Rack_27_32", Payload = _warehousePayload, IsLinkable = true, LinkedMission = null, LinkWaitTimeout = 3600 });
                            missionSteps.Add(new MissionStepDefinition { ProcessStepDescription = $"{targetRackVm.Title} 제품 픽업 & 드롭", MissionType = "7", FromNode = $"Rack_{shelf}_PickUP", ToNode = "WaitProduct_1_Drop", Payload = _warehousePayload, IsLinkable = false, LinkedMission = null, LinkWaitTimeout = 3600,
                                SourceRackId = targetRackVm.Id, DestinationRackId = null // 출고는 소스 랙만 비우므로 SourceRackId만 설정
                            });

                        }
                        else //if (targetRackVm.LocationArea == 1)
                        {
                            missionSteps.Add(new MissionStepDefinition { ProcessStepDescription = $"{targetRackVm.Title} 제품 픽업", MissionType = "7", FromNode = $"Rack_{shelf}_PickUP", ToNode = "Turn_Rack_27_32", Payload = _warehousePayload, IsLinkable = true, LinkedMission = null, LinkWaitTimeout = 3600 });
                            missionSteps.Add(new MissionStepDefinition { ProcessStepDescription = $"{targetRackVm.Title} 제품 드롭", MissionType = "8", ToNode = "WaitProduct_1_Drop", Payload = _warehousePayload, IsLinkable = false, LinkedMission = null, LinkWaitTimeout = 3600,
                                SourceRackId = targetRackVm.Id, DestinationRackId = null // 출고는 소스 랙만 비우므로 SourceRackId만 설정
                            });
                        }
                    }
                    missionSteps.Add(new MissionStepDefinition { ProcessStepDescription = "AMR 충전소 복귀", MissionType = "8", ToNode = "Charge1", Payload = _warehousePayload, IsLinkable = false, LinkedMission = null, LinkWaitTimeout = 3600,
                        SourceRackId = null, DestinationRackId = null // 충전소 복귀는 랙 상태 변화가 없음
                    });

                    try {
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
                else
                {
                    ShowAutoClosingMessage($"{productName} 제품 출고가 취소되었습니다.");
                }
            }
            else
            {
                MessageBox.Show("잘못된 출고 요청.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
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
            if (inputString.Contains("308B")) return 8;
            if (inputString.Contains("308SP")) return 9;
            if (inputString.Contains("308XM")) return 10;
            if (inputString.Contains("7.62X")) return 11;
            if (inputString.Contains("PSD") && inputString.Contains(" c")) return 12; // M80
            return 0; // Default or invalid
        }
    }
}
