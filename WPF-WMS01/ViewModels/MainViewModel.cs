// ViewModels/MainViewModel.cs
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Threading.Tasks; // 비동기 작업용 Task.Run, Task.Delay 사용을 위해 추가
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
using System.Net.Http;
using System.Text.Json;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WPF_WMS01.ViewModels
{
    public class CheckoutRequest
    {
        public int BulletType { get; set; } // 제품 유형 (예: 1 for 223A, 4 for 308B, etc.)
        public string ProductName { get; set; } // 제품 이름 (예: "223A", "308B      ")
    }

    public class MainViewModel : ViewModelBase // INotifyPropertyChanged를 구현하는 ViewModelBase 사용
    {
        private readonly DatabaseService _databaseService;
        private readonly HttpService _httpService; // HttpService 필드 추가
        private readonly string _apiUsername; // App.config에서 읽어올 사용자 이름
        private readonly string _apiPassword; // App.config에서 읽어올 비밀번호

        private ObservableCollection<RackViewModel> _rackList;
        private DispatcherTimer _refreshTimer; // 타이머 선언
        public readonly string _waitRackTitle; // App.config에서 읽어올 WAIT 랙 타이틀
        public readonly char[] _militaryCharacter = { 'a', 'b', 'c', ' ' };

        // === 로그인 관련 속성 및 Command 추가 ===
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
                    // 로그인 상태가 변경되면 로그인 버튼의 CanExecute 상태도 업데이트
                    ((AsyncRelayCommand)LoginCommand).RaiseCanExecuteChanged();
                    // ANT 서버와 통신하는 다른 Command들도 여기서 CanExecute 상태를 갱신할 수 있음
                    // 예: RaiseAllAntApiCommandsCanExecuteChanged();
                }
            }
        }

        private string _authToken; // 받은 토큰 저장
        public string AuthToken
        {
            get => _authToken;
            set => SetProperty(ref _authToken, value);
        }

        private DateTime? _tokenExpiryTime; // 토큰 만료 시간
        public DateTime? TokenExpiryTime
        {
            get => _tokenExpiryTime;
            set => SetProperty(ref _tokenExpiryTime, value);
        }

        private bool _isLoginAttempting; // 로그인 시도 중임을 나타내는 플래그
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
                if (_isMenuOpen != value)
                {
                    _isMenuOpen = value;
                    OnPropertyChanged(nameof(IsMenuOpen)); // 반드시 호출되어야 합니다.
                }
            }
        }
        // INotifyPropertyChanged 구현 (예: ObservableObject 상속 또는 수동 구현)
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public ICommand LoginCommand { get; private set; }
        // === 로그인 관련 속성 및 Command 끝 ===
        // 햄버거 메뉴를 열고 닫는 커맨드
        public ICommand OpenMenuCommand { get; }
        public ICommand CloseMenuCommand { get; }
        // 메뉴 아이템 커맨드 (예시)
        public ICommand MenuItem1Command { get; }
        public ICommand MenuItem2Command { get; }
        public ICommand MenuItem3Command { get; }

        public MainViewModel() // 디자인 타임용 또는 DI가 없는 경우를 위한 기본 생성자
        {
            _databaseService = new DatabaseService();
            _waitRackTitle = ConfigurationManager.AppSettings["WaitRackTitle"] ?? "WAIT";
            _httpService = new HttpService("http://localhost:8080/"); // 기본 URL, 실제 App.config에서 가져와야 함

            //OpenMenuCommand = new RelayCommand(p => IsMenuOpen = !IsMenuOpen);
            OpenMenuCommand = new RelayCommand(p => ExecuteOpenMenuCommand());
            MenuItem1Command = new RelayCommand(p => OnMenuItem1Executed(p));
            MenuItem2Command = new RelayCommand(p => OnMenuItem2Executed(p));
            MenuItem3Command = new RelayCommand(p => OnMenuItem3Executed(p));
            CloseMenuCommand = new RelayCommand(p => ExecuteCloseMenuCommand());

            InitializeCommands();
            SetupRefreshTimer();
            _ = LoadRacksAsync();
        }

        private string _popupDebugMessage;
        public string PopupDebugMessage
        {
            get => _popupDebugMessage;
            set
            {
                if (_popupDebugMessage != value)
                {
                    _popupDebugMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        // DI를 통해 HttpService와 로그인 정보를 주입받는 주 생성자
        public MainViewModel(HttpService httpService, string username, string password)
        {
            _databaseService = new DatabaseService();
            _waitRackTitle = ConfigurationManager.AppSettings["WaitRackTitle"] ?? "WAIT";

            _httpService = httpService ?? throw new ArgumentNullException(nameof(httpService));
            _apiUsername = username;
            _apiPassword = password;

           // OpenMenuCommand = new RelayCommand(p => IsMenuOpen = !IsMenuOpen);
            //OpenMenuCommand = new RelayCommand(p => ExecuteOpenMenuCommand());
            // 다른 메뉴 아이템 커맨드 초기화 (예시)
            //MenuItem1Command = new RelayCommand(p => OnMenuItem1Executed(p));
            //MenuItem2Command = new RelayCommand(p => OnMenuItem2Executed(p));
            //MenuItem3Command = new RelayCommand(p => OnMenuItem3Executed(p));
            //CloseMenuCommand = new RelayCommand(p => ExecuteCloseMenuCommand());
            InitializeCommands();
            SetupRefreshTimer();
            _ = LoadRacksAsync();

            // 애플리케이션 시작 시 자동 로그인 시도
            _ = AutoLoginOnStartup(); // Fire and forget. 비동기 메서드를 호출하고 반환 값을 기다리지 않음.
        }

        private void ExecuteOpenMenuCommand()
        {
            IsMenuOpen = !IsMenuOpen; // 햄버거 버튼 클릭 시 팝업 상태 토글
            Debug.WriteLine($"햄버거 버튼 클릭됨. IsMenuOpen: {IsMenuOpen}"); // 디버깅을 위한 출력
        }

        private void ExecuteCloseMenuCommand()
        {
            IsMenuOpen = false; // 메뉴 닫기 버튼 클릭 시 팝업 닫기
            Debug.WriteLine("메뉴 닫기 버튼 클릭됨. IsMenuOpen: False");
        }

        // 메뉴 아이템 실행 로직 (예시)
        private void OnMenuItem1Executed(object parameter)
        {
            // "옵션 1" 클릭 시 실행될 로직
            System.Diagnostics.Debug.WriteLine($"옵션 1이 클릭되었습니다. 파라미터: {parameter}");
            IsMenuOpen = false; // 메뉴 닫기
        }

        private void OnMenuItem2Executed(object parameter)
        {
            // "옵션 2" 클릭 시 실행될 로직
            System.Diagnostics.Debug.WriteLine($"옵션 2가 클릭되었습니다. 파라미터: {parameter}");
            IsMenuOpen = false; // 메뉴 닫기
        }

        private void OnMenuItem3Executed(object parameter)
        {
            // "옵션 3" 클릭 시 실행될 로직
            System.Diagnostics.Debug.WriteLine($"옵션 3이 클릭되었습니다. 파라미터: {parameter}");
            IsMenuOpen = false; // 메뉴 닫기
        }

        private void InitializeCommands()
        {
            // 기존 Command 초기화
            // --- Grid>Row="1"에 새로 추가된 명령 초기화 ---
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
                param => CanExecuteCheckoutProduct(new CheckoutRequest { BulletType = 4, ProductName = "5.56K" }));
            Checkout556kProductCommand = new RelayCommand(
                param => ExecuteCheckoutProduct(new CheckoutRequest { BulletType = 5, ProductName = "5.56K" }),
                param => CanExecuteCheckoutProduct(new CheckoutRequest { BulletType = 5, ProductName = "7.62X" }));
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
            // 하나의 공통 명령으로 묶는 경우 (XAML에서 CommandParameter를 사용)
            // CheckoutProductCommand = new RelayCommand(ExecuteCheckoutProduct, CanExecuteCheckoutProduct);
            // 이 경우 XAML 버튼에서 CommandParameter="{Binding Source={StaticResource CheckoutRequest223}}" 와 같이 바인딩해야 합니다.
            // StaticResource로 CheckoutRequest 인스턴스를 미리 정의해야 합니다.
            // 예를 들어 App.xaml에 <WPF_WMS01:CheckoutRequest x:Key="CheckoutRequest223" BulletType="1" ProductName="223 제품" ProductCode="223"/>
            // 이런 방식은 유연하지만, XAML 설정이 조금 더 복잡해질 수 있습니다.

            // 새로 추가된 로그인 Command 초기화
            LoginCommand = new AsyncRelayCommand(ExecuteLogin, CanExecuteLogin);
        }

        public ObservableCollection<RackViewModel> RackList
        {
            get => _rackList;
            set => SetProperty(ref _rackList, value); // ViewModelBase의 SetProperty 사용
        }

        public ICommand LoadRacksCommand { get; }
        // RackView들의 상태 업데이트 및 초기화
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
                MessageBox.Show($"랙 데이터 로드 중 오류 발생: {ex.Message}", "데이터베이스 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
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
                // ImageIndex = RackType * 7 + BulletType;
                // BulletType = ImageIndex % 7;
                // RackType = ImageIndex / 7;
                rackViewModel.RackModel.BulletType = newImageIndex % 7; // BulletType은 0, 1, 2, 3, 4, 5, 6,
                rackViewModel.RackModel.RackType = newImageIndex / 7;    // RackType은 0, 1, 2

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
            //_refreshTimer.Tick += async (sender, e) => await LoadRacks(); // 비동기 메서드 호출
            _refreshTimer.Tick += RefreshTimer_Tick;
            _refreshTimer.Start();
        }

        private async void RefreshTimer_Tick(object sender, EventArgs e)
        {
            await LoadRacksAsync(); // 타이머 틱마다 데이터를 다시 로드
        }

        private async Task AutoLoginOnStartup()
        {
            LoginStatusMessage = "로그인 시도 중...";
            IsLoggedIn = false;
            // ExecuteLogin 메서드를 호출하여 실제 로그인 로직을 수행합니다.
            // parameter는 null로 전달해도 무방합니다.
            await ExecuteLogin(null);
        }

        private async Task ExecuteLogin(object parameter)
        {
            if (IsLoginAttempting) return; // 이미 로그인 시도 중이라면 중복 실행 방지

            IsLoginAttempting = true; // 로그인 시도 중 상태 설정
            LoginStatusMessage = "로그인 중...";
            IsLoggedIn = false; // 로그인 시도 중이므로 잠시 false로 설정
            AuthToken = null;
            //TokenExpiryTime = null;   // 새 POST login response에 validity 필드가 없으므로 이 부분은 필요 없음.

            try
            {
                LoginRequest loginReq = new LoginRequest
                {
                    Username = _apiUsername,
                    Password = _apiPassword,
                    // 로그인 엔드포인트에서 예상하는 초기 API 버전을 보냅니다.
                    // 이는 일반적으로 고정된 기본값이거나 클라이언트가 결정합니다.
                    ApiVersion = new ApiVersion { Major = 0, Minor = 0 } // 로그인 요청 시 보낼 API 버전
                };

                Debug.WriteLine($"로그인 요청: {_httpService.BaseApiUrl}wms/rest/login (사용자: {_apiUsername})");
                LoginResponse loginRes = await _httpService.PostAsync<LoginRequest, LoginResponse>("wms/rest/login", loginReq);

                if (!string.IsNullOrEmpty(loginRes?.Token))
                {
                    _httpService.SetAuthorizationHeader(loginRes.Token); // 향후 호출을 위해 Authorization 헤더 설정
                    AuthToken = loginRes.Token;

                    // !!! 핵심 변경: loginRes.ApiVersion을 loginRes.ApiVersionString으로 변경 !!!
                    if (!string.IsNullOrEmpty(loginRes.ApiVersionString)) // <-- 여기를 ApiVersionString으로 변경
                    {
                        // "v0.19"와 같은 문자열에서 숫자 부분만 추출
                        // 예: "v0.19" -> "0.19" -> Split('.') -> "0", "19"
                        string versionNumbers = loginRes.ApiVersionString.TrimStart('v'); // "v" 제거
                        string[] parts = versionNumbers.Split('.');

                        if (parts.Length == 2 && int.TryParse(parts[0], out int major) && int.TryParse(parts[1], out int minor))
                        {
                            _httpService.SetCurrentApiVersion(major, minor);
                            LoginStatusMessage = $"로그인 성공! (API v{major}.{minor})";
                        }
                        else
                        {
                            // 파싱 실패 시 기본값 설정
                            _httpService.SetCurrentApiVersion(0, 0); // 기본 폴백
                            LoginStatusMessage = $"로그인 성공! (API 버전 파싱 오류: {loginRes.ApiVersionString}, 기본값 v0.0 사용)";
                            Console.WriteLine($"경고: 로그인 응답 API 버전 '{loginRes.ApiVersionString}' 파싱 오류. 기본값 v0.0 사용.");
                        }
                    }
                    else
                    {
                        // API 버전 정보가 아예 없는 경우
                        _httpService.SetCurrentApiVersion(0, 0); // 기본 폴백
                        LoginStatusMessage = $"로그인 성공! (API 버전 정보 없음, 기본값 v0.0 사용)";
                        Console.WriteLine("경고: 로그인 응답에 API 버전 정보가 없습니다. 기본값 v0.0 사용.");
                    }

                    IsLoggedIn = true;
                    Console.WriteLine("WMS 서버 로그인 성공!");
                }
            }
            catch (HttpRequestException httpEx)
            {
                IsLoggedIn = false;
                LoginStatusMessage = "로그인 실패";
                MessageBox.Show($"로그인 실패: 네트워크 오류 또는 서버 응답 없음.: {httpEx.Message}", "ANT");
            }
            catch (JsonException jsonEx)
            {
                IsLoggedIn = false;
                LoginStatusMessage = "로그인 실패";
                MessageBox.Show($"로그인 실패: 응답 데이터 형식 오류. {jsonEx.Message}", "ANT");
            }
            catch (Exception ex)
            {
                IsLoggedIn = false;
                LoginStatusMessage = "로그인 실패";
                MessageBox.Show($"로그인 실패: 예상치 못한 오류. {ex.Message}", "ANT");
                Debug.WriteLine($"로그인 일반 오류: {ex.Message}");
            }
            finally
            {
                IsLoginAttempting = false; // 로그인 시도 중 상태 해제
            }
        }

        private bool CanExecuteLogin(object parameter)
        {
            // 이미 로그인 상태이거나 로그인 시도 중이면 로그인 버튼 비활성화
            return !IsLoginAttempting; // return !IsLoggedIn && !IsLoginAttempting;
        }

        // ViewModel이 소멸될 때 타이머를 멈추는 것이 좋습니다. (Window.Closed 이벤트 등에서 호출)
        public void Dispose()
        {
            _refreshTimer?.Stop();
            _refreshTimer.Tick -= RefreshTimer_Tick;
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

        // Grid>Row="3"에 새로 추가된 속성 및 명령 ---

        private string _inputStringForShipOut;
        public string InputStringForShipOut
        {
            get => _inputStringForShipOut;
            set
            {
                _inputStringForShipOut = value;
                OnPropertyChanged();
                // TextBlock 내용이 변경될 때마다 버튼의 활성화 여부를 다시 평가
                //((RelayCommand)InboundProductCommand).RaiseCanExecuteChanged();
            }
        }

        public ICommand InboundProductCommand { get; private set; } // '입고' 버튼 명령
        public ICommand FakeInboundProductCommand { get; private set; } // '가입고' 버튼 명령
        public ICommand Checkout223aProductCommand { get; private set; } // '223A 출고' 버튼 명령
        public ICommand Checkout223spProductCommand { get; private set; } // '223SP 출고' 버튼 명령
        public ICommand Checkout223xmProductCommand { get; private set; } // '223XM 출고' 버튼 명령
        public ICommand Checkout556xProductCommand { get; private set; } // '5.56X 출고' 버튼 명령
        public ICommand Checkout556kProductCommand { get; private set; } // '5.56K 출고' 버튼 명령
        public ICommand CheckoutM855tProductCommand { get; private set; } // 'M855T 출고' 버튼 명령
        public ICommand CheckoutM193ProductCommand { get; private set; } // 'M193 출고' 버튼 명령
        public ICommand Checkout308bProductCommand { get; private set; } // '308B 출고' 버튼 명령
        public ICommand Checkout308spProductCommand { get; private set; } // '30SP8 출고' 버튼 명령
        public ICommand Checkout308xmProductCommand { get; private set; } // '308XM 출고' 버튼 명령
        public ICommand Checkout762xProductCommand { get; private set; } // '7.62X 출고' 버튼 명령
        public ICommand CheckoutM80ProductCommand { get; private set; } // 'M80 출고' 버튼 명령
        //public ICommand CheckoutProductCommand { get; private set; } // 필요하다면 하나의 공통 명령으로 묶을 수도 있음.

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

            var selectEmptyRackViewModel = new SelectEmptyRackPopupViewModel(emptyRacks.Select(r => r.RackModel).ToList(),
                _inputStringForButton.TrimStart().TrimEnd(_militaryCharacter), "미 포장 적재", "포장 전 제품");
            var selectEmptyRackView = new SelectEmptyRackPopupView { DataContext = selectEmptyRackViewModel };
            selectEmptyRackView.Title = $"{InputStringForButton.TrimStart().TrimEnd(this._militaryCharacter)} 제품 입고할 랙 선택";

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
                    //MessageBox.Show($"랙 {selectedRack.Title} 에 {InputStringForButton} 제품 입고 작업을 시작합니다. 10초 대기...", "입고 작업 시작", MessageBoxButton.OK, MessageBoxImage.Information);
                    ShowAutoClosingMessage($"랙 {selectedRack.Title} 에 {InputStringForButton} 제품 입고 작업을 시작합니다. 10초 대기...");

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
                            if (InputStringForButton.Contains("223A"))
                            {
                                newBulletType = 1;
                            }
                            else if (InputStringForButton.Contains("223SP"))
                            {
                                newBulletType = 2;
                            }
                            else if (InputStringForButton.Contains("223XM"))
                            {
                                newBulletType = 3;
                            }
                            else if (InputStringForButton.Contains("5.56X"))
                            {
                                newBulletType = 4;
                            }
                            else if (InputStringForButton.Contains("5.56K"))
                            {
                                newBulletType = 5;
                            }
                            else if (InputStringForButton.Contains("PSD"))
                            {
                                if (InputStringForButton.Contains(" a"))   // M885T
                                {
                                    newBulletType = 6;
                                }
                                else if (InputStringForButton.Contains(" b"))  // M193
                                {
                                    newBulletType = 7;
                                }
                                else if (InputStringForButton.Contains(" c"))  // M80
                                {
                                    newBulletType = 12;
                                }
                                else
                                {
                                    newBulletType = 6;   // M885T (default)
                                }

                            }
                            else if (InputStringForButton.Contains("308B"))
                            {
                                newBulletType = 8;
                            }
                            else if (InputStringForButton.Contains("308SP"))
                            {
                                newBulletType = 9;
                            }
                            else if (InputStringForButton.Contains("308XM"))
                            {
                                newBulletType = 10;
                            }
                            else if (InputStringForButton.Contains("7.62X"))
                            {
                                newBulletType = 11;
                            }
                            else
                            {
                                //Application.Current.Dispatcher.Invoke(() =>   // ToDo Check
                                //{
                                //MessageBox.Show("입력된 문자열에서 유효한 제품 유형을 찾을 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                                ShowAutoClosingMessage("입력된 문자열에서 유효한 제품 유형을 찾을 수 없습니다."); // 오류 메시지도 자동 닫힘

                                //});
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

                            await _databaseService.UpdateLotNumberAsync(selectedRack.Id,
                                InputStringForButton.TrimStart().TrimEnd(_militaryCharacter)); // Register lot number

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
                                //MessageBox.Show($"랙 {selectedRack.Title} 에 제품 입고 완료.", "입고 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                                ShowAutoClosingMessage($"랙 {selectedRack.Title} 에 제품 입고 완료.");
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
                //MessageBox.Show("입고 작업이 취소되었습니다.", "취소", MessageBoxButton.OK, MessageBoxImage.Information);
                ShowAutoClosingMessage("입고 작업이 취소되었습니다.");
            }
        }

        private bool CanExecuteInboundProduct(object parameter)
        {
            // 1) InputStringForButton이 '223' 또는 '308'을 포함하는지 확인
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
                    if (_inputStringForButton.Contains("223A"))
                    {
                        newBulletTypeForWaitRack = 1;
                    }
                    else if (_inputStringForButton.Contains("223SP"))
                    {
                        newBulletTypeForWaitRack = 2;
                    }
                    else if (_inputStringForButton.Contains("223XM"))
                    {
                        newBulletTypeForWaitRack = 3;
                    }
                    else if (_inputStringForButton.Contains("5.56X"))
                    {
                        newBulletTypeForWaitRack = 4;
                    }
                    else if (_inputStringForButton.Contains("5.56K"))
                    {
                        newBulletTypeForWaitRack = 5;
                    }
                    else if (_inputStringForButton.Contains("PSD") && _inputStringForButton.Contains(" a")) // M855T
                    {
                        newBulletTypeForWaitRack = 6;
                    }
                    else if (_inputStringForButton.Contains("PSD") && _inputStringForButton.Contains(" b")) // M193
                    {
                        newBulletTypeForWaitRack = 7;
                    }
                    else if (_inputStringForButton.Contains("308B"))
                    {
                        newBulletTypeForWaitRack = 8;
                    }
                    else if (_inputStringForButton.Contains("308SP"))
                    {
                        newBulletTypeForWaitRack = 9;
                    }
                    else if (_inputStringForButton.Contains("308XM"))
                    {
                        newBulletTypeForWaitRack = 10;
                    }
                    else if (_inputStringForButton.Contains("7.62X"))
                    {
                        newBulletTypeForWaitRack = 11;
                    }
                    else if (_inputStringForButton.Contains("PSD") && _inputStringForButton.Contains(" c")) // M80
                    {
                        newBulletTypeForWaitRack = 12;
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
            return /* IsLoggedIn && */ inputContainsValidProduct && emptyAndVisibleRackExists && waitRackNotLocked;

        }

        private async void FakeExecuteInboundProduct(object parameter)
        {
            // 이 시점에서는 CanExecute에서 이미 빈 랙 존재 여부를 확인했으나, 한 번 더 확인하여 안전성을 높입니다.
            var emptyRacks = RackList?.Where(r => r.ImageIndex == 13 && r.IsVisible).ToList();

            if (emptyRacks == null || !emptyRacks.Any())
            {
                MessageBox.Show("현재 반팔렛 입고 가능한 빈 랙이 없습니다.", "가입고 불가", MessageBoxButton.OK, MessageBoxImage.Warning);
                // CanExecute에서 이미 막았지만, 혹시 모를 상황 대비 (경쟁 조건 등)
                return;
            }

            var selectEmptyRackViewModel = new SelectEmptyRackPopupViewModel(emptyRacks.Select(r => r.RackModel).ToList(),
                _inputStringForButton.TrimStart().TrimEnd(_militaryCharacter),"재공품 적재", "반팔렛 재공품");
            var selectEmptyRackView = new SelectEmptyRackPopupView { DataContext = selectEmptyRackViewModel };
            selectEmptyRackView.Title = $"{InputStringForButton.TrimStart().TrimEnd(this._militaryCharacter)} 반팔렛 제품 입고할 랙 선택";

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
                    //MessageBox.Show($"랙 {selectedRack.Title} 에 {InputStringForButton} 제품 입고 작업을 시작합니다. 10초 대기...", "입고 작업 시작", MessageBoxButton.OK, MessageBoxImage.Information);
                    ShowAutoClosingMessage($"랙 {selectedRack.Title} 에 {InputStringForButton} 제품 입고 작업을 시작합니다. 10초 대기...");

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
                            if (InputStringForButton.Contains("223A"))
                            {
                                newBulletType = 1;
                            }
                            else if (InputStringForButton.Contains("223SP"))
                            {
                                newBulletType = 2;
                            }
                            else if (InputStringForButton.Contains("223XM"))
                            {
                                newBulletType = 3;
                            }
                            else if (InputStringForButton.Contains("5.56X"))
                            {
                                newBulletType = 4;
                            }
                            else if (InputStringForButton.Contains("5.56K"))
                            {
                                newBulletType = 5;
                            }
                            else if (InputStringForButton.Contains("PSD"))
                            {
                                if (InputStringForButton.Contains(" a"))   // M885T
                                {
                                    newBulletType = 6;
                                }
                                else if (InputStringForButton.Contains(" b"))  // M193
                                {
                                    newBulletType = 7;
                                }
                                else if (InputStringForButton.Contains(" c"))  // M80
                                {
                                    newBulletType = 12;
                                }
                                else
                                {
                                    newBulletType = 6;   // M885T (default)
                                }

                            }
                            else if (InputStringForButton.Contains("308B"))
                            {
                                newBulletType = 8;
                            }
                            else if (InputStringForButton.Contains("308SP"))
                            {
                                newBulletType = 9;
                            }
                            else if (InputStringForButton.Contains("308XM"))
                            {
                                newBulletType = 10;
                            }
                            else if (InputStringForButton.Contains("7.62X"))
                            {
                                newBulletType = 11;
                            }
                            else
                            {
                                //Application.Current.Dispatcher.Invoke(() =>   // ToDo Check
                                //{
                                //MessageBox.Show("입력된 문자열에서 유효한 제품 유형을 찾을 수 없습니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                                ShowAutoClosingMessage("입력된 문자열에서 유효한 제품 유형을 찾을 수 없습니다."); // 오류 메시지도 자동 닫힘

                                //});
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
                                3, //selectedRack.RackType,
                                newBulletType,
                                false // 입고 후 타겟 랙만 잠금 해제
                            );

                            await _databaseService.UpdateLotNumberAsync(selectedRack.Id,
                                InputStringForButton.TrimStart().TrimEnd(_militaryCharacter)); // Register lot number

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
                                //MessageBox.Show($"랙 {selectedRack.Title} 에 제품 입고 완료.", "입고 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                                ShowAutoClosingMessage($"랙 {selectedRack.Title} 에 제품 입고 완료.");
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
                //MessageBox.Show("입고 작업이 취소되었습니다.", "취소", MessageBoxButton.OK, MessageBoxImage.Information);
                ShowAutoClosingMessage("입고 작업이 취소되었습니다.");
            }
        }

        private bool CanFakeExecuteInboundProduct(object parameter)
        {
            // 1) InputStringForButton이 '223' 또는 '308'을 포함하는지 확인
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

            // 2) ImageIndex가 13 (빈 랙)이고 IsVisible이 True인 랙이 존재하는지 확인
            bool emptyAndVisibleRackExists = RackList?.Any(r => (r.ImageIndex == 13 && r.IsVisible)) == true;

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
                    if (_inputStringForButton.Contains("223A"))
                    {
                        newBulletTypeForWaitRack = 1;
                    }
                    else if (_inputStringForButton.Contains("223SP"))
                    {
                        newBulletTypeForWaitRack = 2;
                    }
                    else if (_inputStringForButton.Contains("223XM"))
                    {
                        newBulletTypeForWaitRack = 3;
                    }
                    else if (_inputStringForButton.Contains("5.56X"))
                    {
                        newBulletTypeForWaitRack = 4;
                    }
                    else if (_inputStringForButton.Contains("5.56K"))
                    {
                        newBulletTypeForWaitRack = 5;
                    }
                    else if (_inputStringForButton.Contains("PSD") && _inputStringForButton.Contains(" a")) // M855T
                    {
                        newBulletTypeForWaitRack = 6;
                    }
                    else if (_inputStringForButton.Contains("PSD") && _inputStringForButton.Contains(" b")) // M193
                    {
                        newBulletTypeForWaitRack = 7;
                    }
                    else if (_inputStringForButton.Contains("308B"))
                    {
                        newBulletTypeForWaitRack = 8;
                    }
                    else if (_inputStringForButton.Contains("308SP"))
                    {
                        newBulletTypeForWaitRack = 9;
                    }
                    else if (_inputStringForButton.Contains("308XM"))
                    {
                        newBulletTypeForWaitRack = 10;
                    }
                    else if (_inputStringForButton.Contains("7.62X"))
                    {
                        newBulletTypeForWaitRack = 11;
                    }
                    else if (_inputStringForButton.Contains("PSD") && _inputStringForButton.Contains(" c")) // M80
                    {
                        newBulletTypeForWaitRack = 12;
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
            return /* IsLoggedIn && */ inputContainsValidProduct && emptyAndVisibleRackExists && waitRackNotLocked;
        }

        private async void ExecuteCheckoutProduct(object parameter)
        {
            // 출고 가능한 308 제품 랙 목록 가져오기 (잠겨있지 않은 랙만)
            if (parameter is CheckoutRequest request)
            {
                var availableRacksForCheckout = RackList?.Where(r => r.RackType == 1 && r.BulletType == request.BulletType && r.LotNumber.Contains((InputStringForShipOut == null || InputStringForShipOut == "") ? "" : "-" + InputStringForShipOut) && !r.IsLocked).Select(rvm => rvm.RackModel).ToList();
                var productName = request.ProductName;

                if (availableRacksForCheckout == null || !availableRacksForCheckout.Any())
                {
                    MessageBox.Show($"출고할 {productName} 제품이 있는 랙이 없습니다.", $"{productName} 출고 불가", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 팝업 ViewModel 인스턴스 생성 및 랙 리스트 전달
                var selectCheckoutRackViewModel = new SelectCheckoutRackPopupViewModel(availableRacksForCheckout);
                var selectCheckoutRackView = new SelectCheckoutRackPopupView { DataContext = selectCheckoutRackViewModel };
                selectCheckoutRackView.Title = $"출고할 {productName} 제품 랙 선택";

                if (selectCheckoutRackView.ShowDialog() == true && selectCheckoutRackViewModel.DialogResult == true)
                {
                    // 사용자가 선택한 랙 목록 가져오기
                    var selectedRacksForCheckout = selectCheckoutRackViewModel.GetSelectedRacks();

                    if (selectedRacksForCheckout == null || !selectedRacksForCheckout.Any())
                    {
                        MessageBox.Show("선택된 랙이 없습니다.", "출고 취소", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    //MessageBox.Show($"{selectedRacksForCheckout.Count}개의 308 제품 랙 출고 작업을 시작합니다.", "출고 작업 시작", MessageBoxButton.OK, MessageBoxImage.Information);
                    ShowAutoClosingMessage($"{selectedRacksForCheckout.Count}개의 {productName} 제품 랙 출고 작업을 시작합니다.");

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
                            //Application.Current.Dispatcher.Invoke(() =>   // ToDo Check
                            //{
                            //MessageBox.Show($"랙 {targetRackVm.Title} 출고 처리 중... (10초 대기)", "출고 진행", MessageBoxButton.OK, MessageBoxImage.Information);
                            ShowAutoClosingMessage($"랙 {targetRackVm.Title} 출고 처리 중... (10초 대기)");
                            //});

                            await Task.Delay(TimeSpan.FromSeconds(10)); // 10초 지연

                            // **출고: 각 랙이 개별적으로 잠금 해제 (IsLocked = false) 및 BulletType 변경**
                            await _databaseService.UpdateRackStateAsync(targetRackVm.Id, targetRackVm.RackType, 0, false);
                                await _databaseService.UpdateLotNumberAsync(targetRackVm.Id, String.Empty);
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    targetRackVm.BulletType = 0; // UI 업데이트
                                targetRackVm.IsLocked = false; // UI 업데이트 (잠금 해제)
                                                               //MessageBox.Show($"랙 {targetRackVm.Title} 출고 완료.", "출고 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                                ShowAutoClosingMessage($"랙 {targetRackVm.Title} 출고 완료.");
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
                        //MessageBox.Show("모든 308 제품 출고 작업이 완료되었습니다.", "모든 출고 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                        ShowAutoClosingMessage($"모든 {productName} 제품 출고 작업이 완료되었습니다.");
                        });
                    });
                }
                else
                {
                    //MessageBox.Show("308 제품 출고 작업이 취소되었습니다.", "취소", MessageBoxButton.OK, MessageBoxImage.Information);
                    ShowAutoClosingMessage($"{productName} 제품 출고 작업이 취소되었습니다.");
                }
            }
            else
            {
                MessageBox.Show("유효하지 않은 출고 요청입니다.", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanExecuteCheckoutProduct(object parameter)
        {
            if (parameter is CheckoutRequest request)
            {
                // 잠겨있지 않은 해당 제품 랙이 하나라도 있으면 활성화
                return /* IsLoggedIn &&*/ RackList?.Any(r => r.RackType == 1 && r.BulletType == request.BulletType && !r.IsLocked && r.LotNumber.Contains((InputStringForShipOut == null || InputStringForShipOut == "") ? "" : "-" + InputStringForShipOut)) == true;
            }
            return false; // 유효하지 않은 요청이면 비활성화
        }

        // 모든 출고 관련 버튼의 CanExecute 상태를 갱신
        private void RaiseAllCheckoutCanExecuteChanged()
        {
            ((RelayCommand)InboundProductCommand).RaiseCanExecuteChanged(); // 입고 버튼도 갱신하도록 추가
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
            //((RelayCommand)CheckoutProductCommand).RaiseCanExecuteChanged();
            // 필요한 경우 LoginCommand도 여기서 RaiseCanExecuteChanged()를 호출하여 상태를 갱신할 수 있음
        }

    }
}