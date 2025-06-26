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
using System.Text.Json;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading; // DispatcherTimer 사용을 위해 추가

namespace WPF_WMS01.ViewModels
{
    // Modbus 버튼의 상태를 나타내는 ViewModel (각 버튼에 바인딩될 개별 항목)
    public class ModbusButtonViewModel : ViewModelBase
    {
        private bool _isEnabled; // 버튼의 활성화 상태 (Coil 1일 때 true, 그리고 작업 중이 아닐 때)
        private string _content; // 버튼에 표시될 텍스트 (예: "팔레트 공급")
        private ushort _modbusAddress; // 해당 버튼이 관여하는 Modbus Coil 주소
        private bool _isProcessing; // 비동기 작업 진행 중 여부
        private int _currentProgress; // 진행률 (0-100)
        private bool _isCoilTaskScheduled; // 이 Coil 활성화에 대한 비동기 작업이 이미 스케줄링되었는지 여부

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

        public ushort ModbusAddress
        {
            get => _modbusAddress;
            set => SetProperty(ref _modbusAddress, value);
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

        public bool IsCoilTaskScheduled // 비동기 작업이 스케줄링/시작되었는지 여부 (중복 트리거 방지용)
        {
            get => _isCoilTaskScheduled;
            set => SetProperty(ref _isCoilTaskScheduled, value);
        }

        // 이 Command는 MainViewModel에서 초기화될 것입니다.
        public ICommand ExecuteButtonCommand { get; set; }

        public ModbusButtonViewModel(string content, ushort address)
        {
            Content = content;
            ModbusAddress = address;
            IsEnabled = false; // 초기에는 비활성화
            IsProcessing = false; // 초기에는 작업 중 아님
            CurrentProgress = 0; // 초기 진행률 0
            IsCoilTaskScheduled = false; // 초기 상태
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
        private readonly string _apiUsername;
        private readonly string _apiPassword;
        private readonly ModbusClientService _modbusService; // ModbusClientService 인스턴스 추가

        private ObservableCollection<RackViewModel> _rackList;
        private DispatcherTimer _refreshTimer;
        private DispatcherTimer _modbusReadTimer; // Modbus Coil 상태 읽기용 타이머
        public readonly string _waitRackTitle;
        public readonly char[] _militaryCharacter = { 'a', 'b', 'c', ' ' };

        // Modbus Coil 상태를 저장할 ObservableCollection
        public ObservableCollection<ModbusButtonViewModel> ModbusButtons { get; set; }


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

        // Constructor
        public MainViewModel(DatabaseService databaseService, HttpService httpService, ModbusClientService modbusService)
        {
            _databaseService = databaseService;
            _httpService = httpService;
            _waitRackTitle = ConfigurationManager.AppSettings["WaitRackTitle"] ?? "WAIT";
            _apiUsername = ConfigurationManager.AppSettings["ApiUsername"];
            _apiPassword = ConfigurationManager.AppSettings["ApiPassword"];

            // ModbusClientService 초기화 (TCP 모드 예시)
            // 실제 PLC의 IP 주소와 포트, 슬레이브 ID로 변경하세요.
            // RTU 모드를 사용하려면 ModbusClientService("COM1", 9600, Parity.None, StopBits.One, 8, 1) 와 같이 변경
            _modbusService = modbusService;

            // ModbusButtons 컬렉션 초기화 (XAML의 버튼 순서 및 내용에 맞춰)
            // Modbus Coil Address는 임의로 0부터 순차적으로 부여했습니다. 실제 PLC 주소에 맞춰 변경해야 합니다.
            ModbusButtons = new ObservableCollection<ModbusButtonViewModel>
            {
                new ModbusButtonViewModel("팔레트 공급", 0), // Coil Address 0
                new ModbusButtonViewModel("단프라 공급", 1), // Coil Address 1
                new ModbusButtonViewModel("7.62mm", 2),    // Coil Address 2
                new ModbusButtonViewModel("5.56mm[1]", 3), // Coil Address 3
                new ModbusButtonViewModel("5.56mm[2]", 4), // Coil Address 4
                new ModbusButtonViewModel("5.56mm[3]", 5), // Coil Address 5
                new ModbusButtonViewModel("5.56mm[4]", 6), // Coil Address 6
                new ModbusButtonViewModel("5.56mm[5]", 7), // Coil Address 7
                new ModbusButtonViewModel("5.56mm[6]", 8), // Coil Address 8
                new ModbusButtonViewModel("수작업[1]", 9), // Coil Address 9
                new ModbusButtonViewModel("수작업[2]", 10),// Coil Address 10
                new ModbusButtonViewModel("반팔렛 적치", 11) // Coil Address 11
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

            InitializeCommands(); // 기존의 다른 명령 초기화

            SetupRefreshTimer(); // RackList 갱신 타이머
            SetupModbusReadTimer(); // Modbus Coil 상태 읽기 타이머 설정
            _ = LoadRacksAsync();
            _ = AutoLoginOnStartup();

            IsMenuOpen = false;
            IsLoggedIn = false;
            IsLoginAttempting = false;
            LoginStatusMessage = "로그인 필요";
        }


        // Modbus Coil 상태 읽기 타이머 설정
        private void SetupModbusReadTimer()
        {
            _modbusReadTimer = new DispatcherTimer();
            _modbusReadTimer.Interval = TimeSpan.FromMilliseconds(500); // 0.5초마다 읽기 (조정 가능)
            _modbusReadTimer.Tick += ModbusReadTimer_Tick;
            _modbusReadTimer.Start();
            Debug.WriteLine("[ModbusService] Modbus Read Timer Started.");
        }

        // Modbus Coil 상태 주기적 읽기
        private async void ModbusReadTimer_Tick(object sender, EventArgs e)
        {
            if (!_modbusService.IsConnected)
            {
                // ConnectAsync를 await하여 연결 시도를 기다림 (UI 스레드 블로킹 방지)
                Debug.WriteLine("[ModbusService] Not Connected. Attempting to reconnect asynchronously...");
                await _modbusService.ConnectAsync().ConfigureAwait(false); // ConfigureAwait(false) 사용
                if (!_modbusService.IsConnected)
                {
                    Debug.WriteLine("[ModbusService] Connection failed after reconnect attempt. Skipping coil read.");
                    return;
                }
            }

            try
            {
                // PLC에서 12개 Coil의 상태를 한 번에 읽어옵니다. (주소 0부터 12개)
                ushort startAddress = 0; // Modbus Coil 시작 주소
                ushort numberOfCoils = (ushort)ModbusButtons.Count; // 12개

                // ReadCallButtonStatesAsync는 내부적으로 ConfigureAwait(false)를 사용하므로 여기서는 사용하지 않아도 됨.
                bool[] coilStates = await _modbusService.ReadCallButtonStatesAsync(startAddress, numberOfCoils);

                if (coilStates != null && coilStates.Length >= numberOfCoils)
                {
                    // UI 업데이트는 UI 스레드에서 수행해야 하므로 Dispatcher.Invoke 사용
                    // 여기서는 Task.Run으로 감싸지 않고, 내부에서 필요한 비동기 작업만 Task.Run으로 오프로드.
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        for (int i = 0; i < numberOfCoils; i++)
                        {
                            var buttonVm = ModbusButtons[i];
                            bool currentCoilState = coilStates[i];

                            // Coil이 1이고, 작업 중이 아니며, 아직 이 Coil에 대한 작업이 스케줄링되지 않았다면
                            if (currentCoilState && !buttonVm.IsProcessing && !buttonVm.IsCoilTaskScheduled)
                            {
                                buttonVm.IsCoilTaskScheduled = true; // 작업 스케줄링 플래그 설정
                                Debug.WriteLine($"[Modbus] Coil {buttonVm.ModbusAddress} activated (0->1). Scheduling task start in 10 seconds.");
                                ShowAutoClosingMessage($"[Modbus] {buttonVm.Content} Coil 신호 감지! 10초 후 작업 자동 시작됩니다.");

                                // 10초 지연 및 비동기 작업 시작을 백그라운드 스레드에서 처리
                                // _ = Task.Run(...) 형태로 fire-and-forget 패턴 사용.
                                _ = Task.Run(async () =>
                                {
                                    await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false); // 10초 지연 (백그라운드 스레드)

                                    // 지연 후, 현재 코일 상태를 다시 확인하여 작업 시작 여부 결정
                                    bool postDelayCoilState = false;
                                    try
                                    {
                                        // 백그라운드 스레드에서 Modbus Read (ConfigureAwait(false) 포함)
                                        postDelayCoilState = await _modbusService.ReadSingleCoilAsync(buttonVm.ModbusAddress).ConfigureAwait(false);
                                    }
                                    catch (Exception ex)
                                    {
                                        Debug.WriteLine($"[Modbus] Error re-reading coil {buttonVm.ModbusAddress} after delay: {ex.Message}");
                                        // 오류 발생 시 작업 스케줄링 취소 처리 (UI 스레드에서)
                                        Application.Current.Dispatcher.Invoke(() => buttonVm.IsCoilTaskScheduled = false);
                                        return;
                                    }

                                    // 10초 지연 후에도 Coil이 여전히 1이고, 아직 작업 중이 아니라면 메인 비동기 작업 시작
                                    if (postDelayCoilState && !buttonVm.IsProcessing)
                                    {
                                        // HandleCoilActivatedTask는 UI 업데이트를 포함하므로, UI 스레드에서 호출되어야 함.
                                        await Application.Current.Dispatcher.InvokeAsync(async () =>
                                        {
                                            await HandleCoilActivatedTask(buttonVm); // UI 스레드에서 작업 시작
                                        });
                                    }
                                    else
                                    {
                                        Debug.WriteLine($"[Modbus] Coil {buttonVm.ModbusAddress} task not started after delay. State changed or already processing.");
                                        // 작업이 시작되지 않았다면, 스케줄링 플래그 리셋 (UI 스레드에서)
                                        Application.Current.Dispatcher.Invoke(() => buttonVm.IsCoilTaskScheduled = false);
                                    }
                                }).ConfigureAwait(false); // Task.Run의 Continuation도 백그라운드 스레드에서 유지
                            }
                            else if (!currentCoilState && buttonVm.IsCoilTaskScheduled)
                            {
                                // Coil이 0으로 돌아갔고 작업이 스케줄링된 상태였다면, 스케줄링 취소
                                Debug.WriteLine($"[Modbus] Coil {buttonVm.ModbusAddress} went low, cancelling scheduled task initiation.");
                                buttonVm.IsCoilTaskScheduled = false; // 플래그 리셋
                            }

                            // 버튼의 IsEnabled 상태는 현재 Coil 상태와 IsProcessing 여부에 따라 즉시 업데이트
                            buttonVm.IsEnabled = currentCoilState && !buttonVm.IsProcessing;
                            // Command의 CanExecute 상태를 명시적으로 갱신하여 UI가 IsEnabled/IsProcessing 변화에 즉시 반응하도록 함
                            ((RelayCommand)buttonVm.ExecuteButtonCommand)?.RaiseCanExecuteChanged();
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ModbusService] Error reading Modbus coils: {ex.Message}");
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
                ShowAutoClosingMessage($"[Modbus] {buttonVm.Content} 작업 진행 중: {buttonVm.CurrentProgress}% (주소: {buttonVm.ModbusAddress}).");
                Debug.WriteLine($"[Modbus] Button clicked for {buttonVm.Content}. Task is already processing. Displaying current progress.");
            }
            else if (buttonVm.IsCoilTaskScheduled) // Coil은 1이지만 10초 지연 중인 경우
            {
                ShowAutoClosingMessage($"[Modbus] {buttonVm.Content} 작업 대기 중. 10초 지연 후 자동 시작됩니다. (주소: {buttonVm.ModbusAddress}).");
                Debug.WriteLine($"[Modbus] Button clicked for {buttonVm.Content}. Coil is active, task is scheduled to start soon.");
            }
            else if (buttonVm.IsEnabled) // Coil은 1이지만, IsProcessing도 IsCoilTaskScheduled도 아닌 경우는 거의 없겠지만, 혹시나
            {
                ShowAutoClosingMessage($"[Modbus] {buttonVm.Content} 작업 시작 준비 완료. (주소: {buttonVm.ModbusAddress}).");
                Debug.WriteLine($"[Modbus] Button clicked for {buttonVm.Content}. Coil is active and ready.");
            }
            else // 버튼이 비활성화된 경우 (Coil이 0)
            {
                ShowAutoClosingMessage($"[Modbus] {buttonVm.Content} 작업 비활성화됨. (주소: {buttonVm.ModbusAddress}).");
                Debug.WriteLine($"[Modbus] Button clicked for {buttonVm.Content}. Coil is not active.");
            }
        }

        // 비동기 작업 수행 및 진행률 업데이트, 작업 완료 후 Coil 0으로 초기화
        private async Task HandleCoilActivatedTask(ModbusButtonViewModel buttonVm)
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
            Debug.WriteLine($"[Modbus] Async task started for {buttonVm.Content} (Address: {buttonVm.ModbusAddress}).");

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

                    // await Task.Delay를 ConfigureAwait(false)와 함께 사용하여 백그라운드 스레드에서 지연.
                    await Task.Delay(updateIntervalMs).ConfigureAwait(false);
                }

                // 작업 완료 후 Coil 0으로 쓰기 (HMI 작업 완료 신호)
                // 백그라운드 스레드에서 Modbus Write (ConfigureAwait(false) 포함)
                bool writeSuccess = await _modbusService.WriteSingleCoilAsync(buttonVm.ModbusAddress, false).ConfigureAwait(false);
                if (!writeSuccess)
                {
                    ShowAutoClosingMessage($"[Modbus] {buttonVm.Content} Coil 0 쓰기 실패!");
                    Debug.WriteLine($"[Modbus] Failed to write Coil {buttonVm.ModbusAddress} to false after task completion.");
                }
                else
                {
                    Debug.WriteLine($"[Modbus] Coil {buttonVm.ModbusAddress} set to False after task completion.");
                }

                // 작업 완료 메시지는 UI 스레드에서 표시
                ShowAutoClosingMessage($"[Modbus] {buttonVm.Content} 작업 완료! (주소: {buttonVm.ModbusAddress})");
            }
            catch (Exception ex)
            {
                // 작업 중 오류 발생 시 메시지 팝업 (UI 스레드에서)
                ShowAutoClosingMessage($"[Modbus] {buttonVm.Content} 작업 중 오류 발생: {ex.Message}");
                Debug.WriteLine($"[Modbus] Error during {buttonVm.Content} task: {ex.Message}");
            }
            finally
            {
                // 작업 완료 (성공/실패 무관) 후 UI 상태 업데이트 (UI 스레드에서)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    buttonVm.IsProcessing = false; // 작업 완료 상태로 변경 (ProgressBar 숨김)
                    buttonVm.CurrentProgress = 0; // 진행률 초기화
                    buttonVm.IsCoilTaskScheduled = false; // 다음 PLC 신호를 위해 플래그 리셋
                });
            }
        }

        // Modbus 버튼 활성화 여부를 결정하는 CanExecute 로직
        private bool CanExecuteModbusButtonCommand(ModbusButtonViewModel buttonVm)
        {
            // 버튼은 Coil이 1(활성화 상태)이고, 현재 비동기 작업이 진행 중이 아닐 때만 클릭 가능합니다.
            // 클릭은 작업 시작이 아닌, 진행 상황 확인/정보 표시용입니다.
            return buttonVm?.IsEnabled == true && !buttonVm.IsProcessing;
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
            InboundProductCommand = new RelayCommand(ExecuteInboundProduct, CanExecuteInboundProduct);
            FakeInboundProductCommand = new RelayCommand(FakeExecuteInboundProduct, CanExecuteInboundProduct);
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
            }
        }
        private async Task LoadRacks()
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
            }
        }

        public ICommand UpdateRackStateCommand => new RelayCommand(async (parameter) =>
        {
            if (parameter is RackViewModel rackViewModel)
            {
                int newImageIndex = (rackViewModel.ImageIndex + 1) % 6;

                rackViewModel.RackModel.BulletType = newImageIndex % 7;
                rackViewModel.RackModel.RackType = newImageIndex / 7;

                await _databaseService.UpdateRackStateAsync(
                    rackViewModel.Id,
                    rackViewModel.RackModel.RackType,
                    rackViewModel.RackModel.BulletType,
                    rackViewModel.RackModel.IsLocked
                );
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
                            Console.WriteLine($"Warning: Login response API version '{loginRes.ApiVersionString}' parsing error. Using default v0.0.");
                        }
                    }
                    else
                    {
                        _httpService.SetCurrentApiVersion(0, 0);
                        LoginStatusMessage = $"로그인 성공! (API 버전 정보 없음, 기본값 v0.0 사용)";
                        Console.WriteLine("Warning: Login response does not contain API version information. Using default v0.0.");
                    }

                    IsLoggedIn = true;
                    Console.WriteLine("WMS server login successful!");
                }
            }
            catch (HttpRequestException httpEx)
            {
                IsLoggedIn = false;
                LoginStatusMessage = "로그인 실패";
                MessageBox.Show($"Login failed: Network error or no server response: {httpEx.Message}", "ANT");
            }
            catch (JsonException jsonEx)
            {
                IsLoggedIn = false;
                LoginStatusMessage = "로그인 실패";
                MessageBox.Show($"Login failed: Response data format error. {jsonEx.Message}", "ANT");
            }
            catch (Exception ex)
            {
                IsLoggedIn = false;
                LoginStatusMessage = "로그인 실패";
                MessageBox.Show($"Login failed: Unexpected error. {ex.Message}", "ANT");
                Debug.WriteLine($"Login general error: {ex.Message}");
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

        public void Dispose()
        {
            _refreshTimer?.Stop();
            _refreshTimer.Tick -= RefreshTimer_Tick;
            _modbusReadTimer?.Stop(); // Modbus 타이머도 해제
            _modbusReadTimer.Tick -= ModbusReadTimer_Tick;
            _modbusService?.Dispose(); // Modbus 서비스 자원 해제
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
                MessageBox.Show("No empty racks available for inbound currently.", "Inbound Not Possible", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectEmptyRackViewModel = new SelectEmptyRackPopupViewModel(emptyRacks.Select(r => r.RackModel).ToList(),
                _inputStringForButton.TrimStart().TrimEnd(_militaryCharacter), "Unpacked Storage", "Pre-packaged product");
            var selectEmptyRackView = new SelectEmptyRackPopupView { DataContext = selectEmptyRackViewModel };
            selectEmptyRackView.Title = $"Select rack for inbound of {InputStringForButton.TrimStart().TrimEnd(this._militaryCharacter)} product";

            if (selectEmptyRackView.ShowDialog() == true && selectEmptyRackViewModel.DialogResult == true)
            {
                var selectedRack = selectEmptyRackViewModel.SelectedRack;
                if (selectedRack != null)
                {
                    var targetRackVm = RackList?.FirstOrDefault(r => r.Id == selectedRack.Id);
                    var waitRackVm = RackList?.FirstOrDefault(r => r.Title == _waitRackTitle);

                    if (targetRackVm == null) return;
                    ShowAutoClosingMessage($"Starting inbound operation for {InputStringForButton} product on rack {selectedRack.Title}. Waiting 10 seconds...");

                    await _databaseService.UpdateRackStateAsync(targetRackVm.Id, targetRackVm.RackType, targetRackVm.BulletType, true);
                    Application.Current.Dispatcher.Invoke(() => targetRackVm.IsLocked = true);

                    if (waitRackVm != null)
                    {
                        await _databaseService.UpdateRackStateAsync(waitRackVm.Id, waitRackVm.RackType, waitRackVm.BulletType, true);
                        Application.Current.Dispatcher.Invoke(() => waitRackVm.IsLocked = true);
                    }

                    await Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10));

                        try
                        {
                            int newBulletType = GetBulletTypeFromInputString(_inputStringForButton); // Helper method
                            if (newBulletType == 0)
                            {
                                ShowAutoClosingMessage("Could not find a valid product type in the input string.");
                                await _databaseService.UpdateRackStateAsync(targetRackVm.Id, targetRackVm.RackType, targetRackVm.BulletType, false);
                                Application.Current.Dispatcher.Invoke(() => targetRackVm.IsLocked = false);
                                if (waitRackVm != null)
                                {
                                    await _databaseService.UpdateRackStateAsync(waitRackVm.Id, waitRackVm.RackType, waitRackVm.BulletType, false);
                                    Application.Current.Dispatcher.Invoke(() => waitRackVm.IsLocked = false);
                                }
                                return;
                            }

                            await _databaseService.UpdateRackStateAsync(
                                selectedRack.Id,
                                selectedRack.RackType,
                                newBulletType,
                                false
                            );

                            await _databaseService.UpdateLotNumberAsync(selectedRack.Id,
                                InputStringForButton.TrimStart().TrimEnd(_militaryCharacter));

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                targetRackVm.BulletType = newBulletType;
                                targetRackVm.IsLocked = false;

                                if (waitRackVm != null)
                                {
                                    waitRackVm.IsLocked = false;
                                }

                                ShowAutoClosingMessage($"Product inbound completed for rack {selectedRack.Title}.");
                                InputStringForButton = string.Empty;
                            });
                        }
                        catch (Exception ex)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"Error during inbound operation: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            });
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
                ShowAutoClosingMessage("Inbound operation cancelled.");
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

            bool emptyAndVisibleRackExists = RackList?.Any(r => (r.ImageIndex == 0 && r.IsVisible)) == true;

            var waitRackVm = RackList?.FirstOrDefault(r => r.Title == _waitRackTitle);
            bool waitRackNotLocked = (waitRackVm?.IsLocked == false) || (waitRackVm == null);

            if (waitRackVm != null)
            {
                int newBulletTypeForWaitRack = 0;
                if (inputContainsValidProduct && emptyAndVisibleRackExists)
                {
                    newBulletTypeForWaitRack = GetBulletTypeFromInputString(_inputStringForButton); // Helper method
                }

                Task.Run(async () =>
                {
                    await _databaseService.UpdateRackStateAsync(
                        waitRackVm.Id,
                        waitRackVm.RackType,
                        newBulletTypeForWaitRack,
                        waitRackVm.IsLocked
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
                MessageBox.Show("No empty half-pallet racks available for inbound currently.", "Fake Inbound Not Possible", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selectEmptyRackViewModel = new SelectEmptyRackPopupViewModel(emptyRacks.Select(r => r.RackModel).ToList(),
                _inputStringForButton.TrimStart().TrimEnd(_militaryCharacter), "Work-in-progress Storage", "Half-pallet Work-in-progress");
            var selectEmptyRackView = new SelectEmptyRackPopupView { DataContext = selectEmptyRackViewModel };
            selectEmptyRackView.Title = $"Select rack for half-pallet inbound of {InputStringForButton.TrimStart().TrimEnd(this._militaryCharacter)} product";

            if (selectEmptyRackView.ShowDialog() == true && selectEmptyRackViewModel.DialogResult == true)
            {
                var selectedRack = selectEmptyRackViewModel.SelectedRack;
                if (selectedRack != null)
                {
                    var targetRackVm = RackList?.FirstOrDefault(r => r.Id == selectedRack.Id);
                    var waitRackVm = RackList?.FirstOrDefault(r => r.Title == _waitRackTitle);

                    if (targetRackVm == null) return;
                    ShowAutoClosingMessage($"Starting inbound operation for {InputStringForButton} product on rack {selectedRack.Title}. Waiting 10 seconds...");

                    await _databaseService.UpdateRackStateAsync(targetRackVm.Id, targetRackVm.RackType, targetRackVm.BulletType, true);
                    Application.Current.Dispatcher.Invoke(() => targetRackVm.IsLocked = true);

                    if (waitRackVm != null)
                    {
                        await _databaseService.UpdateRackStateAsync(waitRackVm.Id, waitRackVm.RackType, waitRackVm.BulletType, true);
                        Application.Current.Dispatcher.Invoke(() => waitRackVm.IsLocked = true);
                    }

                    await Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10));

                        try
                        {
                            int newBulletType = GetBulletTypeFromInputString(_inputStringForButton); // Helper method
                            if (newBulletType == 0)
                            {
                                ShowAutoClosingMessage("Could not find a valid product type in the input string.");
                                await _databaseService.UpdateRackStateAsync(targetRackVm.Id, targetRackVm.RackType, targetRackVm.BulletType, false);
                                Application.Current.Dispatcher.Invoke(() => targetRackVm.IsLocked = false);
                                if (waitRackVm != null)
                                {
                                    await _databaseService.UpdateRackStateAsync(waitRackVm.Id, waitRackVm.RackType, waitRackVm.BulletType, false);
                                    Application.Current.Dispatcher.Invoke(() => waitRackVm.IsLocked = false);
                                }
                                return;
                            }

                            await _databaseService.UpdateRackStateAsync(
                                selectedRack.Id,
                                3,
                                newBulletType,
                                false
                            );

                            await _databaseService.UpdateLotNumberAsync(selectedRack.Id,
                                InputStringForButton.TrimStart().TrimEnd(_militaryCharacter));

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                targetRackVm.BulletType = newBulletType;
                                targetRackVm.IsLocked = false;

                                if (waitRackVm != null)
                                {
                                    waitRackVm.IsLocked = false;
                                }

                                ShowAutoClosingMessage($"Product inbound completed for rack {selectedRack.Title}.");
                                InputStringForButton = string.Empty;
                            });
                        }
                        catch (Exception ex)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"Error during inbound operation: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            });
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
                ShowAutoClosingMessage("Inbound operation cancelled.");
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

            if (waitRackVm != null)
            {
                int newBulletTypeForWaitRack = 0;
                if (inputContainsValidProduct && emptyAndVisibleRackExists)
                {
                    newBulletTypeForWaitRack = GetBulletTypeFromInputString(_inputStringForButton);
                }

                Task.Run(async () =>
                {
                    await _databaseService.UpdateRackStateAsync(
                        waitRackVm.Id,
                        waitRackVm.RackType,
                        newBulletTypeForWaitRack,
                        waitRackVm.IsLocked
                    );

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        waitRackVm.BulletType = newBulletTypeForWaitRack;
                    });
                });
            }

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
                    MessageBox.Show($"No racks with {productName} product to checkout.", $"{productName} Checkout Not Possible", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var selectCheckoutRackViewModel = new SelectCheckoutRackPopupViewModel(availableRacksForCheckout);
                var selectCheckoutRackView = new SelectCheckoutRackPopupView { DataContext = selectCheckoutRackViewModel };
                selectCheckoutRackView.Title = $"Select rack for checkout of {productName} product";

                if (selectCheckoutRackView.ShowDialog() == true && selectCheckoutRackViewModel.DialogResult == true)
                {
                    var selectedRacksForCheckout = selectCheckoutRackViewModel.GetSelectedRacks();

                    if (selectedRacksForCheckout == null || !selectedRacksForCheckout.Any())
                    {
                        MessageBox.Show("No racks selected.", "Checkout Cancelled", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    ShowAutoClosingMessage($"Starting checkout operation for {selectedRacksForCheckout.Count} {productName} product racks.");

                    var targetRackVmsToLock = selectedRacksForCheckout.Select(r => RackList?.FirstOrDefault(rvm => rvm.Id == r.Id))
                                                                       .Where(rvm => rvm != null)
                                                                       .ToList();
                    foreach (var rvm in targetRackVmsToLock)
                    {
                        await _databaseService.UpdateRackStateAsync(rvm.Id, rvm.RackType, rvm.BulletType, true);
                        Application.Current.Dispatcher.Invoke(() => rvm.IsLocked = true);
                    }

                    await Task.Run(async () =>
                    {
                        foreach (var rackModelToCheckout in selectedRacksForCheckout)
                        {
                            var targetRackVm = RackList?.FirstOrDefault(r => r.Id == rackModelToCheckout.Id);
                            if (targetRackVm == null) continue;

                            try
                            {
                                ShowAutoClosingMessage($"Processing checkout for rack {targetRackVm.Title}... (Waiting 10 seconds)");

                                await Task.Delay(TimeSpan.FromSeconds(10));

                                await _databaseService.UpdateRackStateAsync(targetRackVm.Id, targetRackVm.RackType, 0, false);
                                await _databaseService.UpdateLotNumberAsync(targetRackVm.Id, String.Empty);
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    targetRackVm.BulletType = 0;
                                    targetRackVm.IsLocked = false;
                                    ShowAutoClosingMessage($"Checkout completed for rack {targetRackVm.Title}.");
                                });
                            }
                            catch (Exception ex)
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    MessageBox.Show($"Error during checkout for rack {targetRackVm.Title}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                                });
                                await _databaseService.UpdateRackStateAsync(targetRackVm.Id, targetRackVm.RackType, targetRackVm.BulletType, false);
                                Application.Current.Dispatcher.Invoke(() => targetRackVm.IsLocked = false);
                            }
                        }

                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            ShowAutoClosingMessage($"All {productName} product checkout operations completed.");
                        });
                    });
                }
                else
                {
                    ShowAutoClosingMessage($"{productName} product checkout operation cancelled.");
                }
            }
            else
            {
                MessageBox.Show("Invalid checkout request.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
