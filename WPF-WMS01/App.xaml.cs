using Microsoft.Extensions.DependencyInjection;
using System;
using System.Configuration;
using System.IO.Ports; // Parity, StopBits를 위해 추가
using System.Windows;
using WPF_WMS01.Services;
using WPF_WMS01.ViewModels;
using System.Globalization; // NumberStyles 및 CultureInfo를 위해 추가
using System.Diagnostics; // Debug.WriteLine을 위해 추가
using System.Threading;

namespace WPF_WMS01
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; }

        private static Mutex _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string appName = "PSBulletApp"; // 앱마다 고유한 이름 사용 필요
            bool createdNew;

            _mutex = new Mutex(true, appName, out createdNew);

            if (!createdNew)
            {
                MessageBox.Show("이미 실행 중인 프로그램이 있습니다.", "실행 중", MessageBoxButton.OK, MessageBoxImage.Information);
                Application.Current.Shutdown(); // 중복 실행 차단
                return;
            }

            base.OnStartup(e);
            ConfigureServices();

            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            // MainViewModel에 RobotMissionService 인스턴스 주입
            var mainViewModel = ServiceProvider.GetRequiredService<MainViewModel>();
            mainViewModel.SetRobotMissionService(ServiceProvider.GetRequiredService<IRobotMissionService>());
            mainWindow.DataContext = mainViewModel;
            mainWindow.Show();
        }

        private void ConfigureServices()
        {
            var services = new ServiceCollection();

            // App.config에서 메인 Modbus 설정 읽기 (MainViewModel용)
            string mainModbusMode = ConfigurationManager.AppSettings["ModbusMode"] ?? "TCP";
            string mainModbusIp = ConfigurationManager.AppSettings["ModbusIpAddress"] ?? "127.0.0.1";
            int mainModbusPort = int.Parse(ConfigurationManager.AppSettings["ModbusPort"] ?? "502");
            byte mainModbusSlaveId = ParseByteConfig(ConfigurationManager.AppSettings["ModbusSlaveId"], 1, false); // Modbus Slave ID는 보통 10진수
            string mainModbusComPort = ConfigurationManager.AppSettings["ModbusComPort"] ?? "COM3";
            int mainModbusBaudRate = int.Parse(ConfigurationManager.AppSettings["ModbusBaudRate"] ?? "9600");
            Parity mainModbusParity = (Parity)Enum.Parse(typeof(Parity), ConfigurationManager.AppSettings["ModbusParity"] ?? "None");
            int mainModbusDataBits = int.Parse(ConfigurationManager.AppSettings["ModbusDataBits"] ?? "8");
            StopBits mainModbusStopBits = (StopBits)Enum.Parse(typeof(StopBits), ConfigurationManager.AppSettings["ModbusStopBits"] ?? "One");

            // App.config에서 미션 체크용 Modbus 설정 읽기
            string missionModbusMode = ConfigurationManager.AppSettings["MissionModbusMode"] ?? "TCP";
            string missionModbusIp = ConfigurationManager.AppSettings["MissionModbusIpAddress"] ?? "192.168.200.202";
            int missionModbusPort = int.Parse(ConfigurationManager.AppSettings["MissionModbusPort"] ?? "503");
            byte missionModbusSlaveId = ParseByteConfig(ConfigurationManager.AppSettings["MissionModbusSlaveId"], 1, false); // Modbus Slave ID는 보통 10진수
            string missionModbusComPort = ConfigurationManager.AppSettings["MissionModbusComPort"] ?? "COM4";
            int missionModbusBaudRate = int.Parse(ConfigurationManager.AppSettings["MissionModbusBaudRate"] ?? "9600");
            Parity missionModbusParity = (Parity)Enum.Parse(typeof(Parity), ConfigurationManager.AppSettings["MissionModbusParity"] ?? "None");
            int missionModbusDataBits = int.Parse(ConfigurationManager.AppSettings["ModbusDataBits"] ?? "8");
            StopBits missionModbusStopBits = (StopBits)Enum.Parse(typeof(StopBits), ConfigurationManager.AppSettings["ModbusStopBits"] ?? "One");

            // App.config에서 MC Protocol 설정 읽기
            string mcProtocolIp = ConfigurationManager.AppSettings["McProtocolIpAddress"] ?? "192.168.200.61";
            int mcProtocolPort = int.Parse(ConfigurationManager.AppSettings["McProtocolPort"] ?? "6000");

            // FIX: McProtocolCpuType은 App.config에 10진수 값으로 있으므로 기본적으로 10진수 파싱 (parseAsHexByDefault: false)
            byte mcProtocolCpuType = ParseByteConfig(ConfigurationManager.AppSettings["McProtocolCpuType"], 0x90, false);
            // Network No는 보통 10진수. 기본값 "0".
            byte mcProtocolNetworkNo = ParseByteConfig(ConfigurationManager.AppSettings["McProtocolNetworkNo"], 0, false);
            // FIX: McProtocolPcNo도 App.config에 10진수 값으로 있으므로 기본적으로 10진수 파싱 (parseAsHexByDefault: false)
            byte mcProtocolPcNo = ParseByteConfig(ConfigurationManager.AppSettings["McProtocolPcNo"], 0xFF, false);

            // HttpService 등록 (생성자에 baseApiUrl 주입)
            services.AddSingleton<HttpService>(provider =>
            {
                string antApiBaseUrl = ConfigurationManager.AppSettings["AntApiBaseUrl"] ?? "http://localhost:8081/";
                return new HttpService(antApiBaseUrl);
            });

            // DatabaseService 등록
            services.AddSingleton<DatabaseService>();

            // MainViewModel용 ModbusClientService 인스턴스 등록
            services.AddSingleton(provider =>
            {
                if (mainModbusMode.Equals("TCP", StringComparison.OrdinalIgnoreCase))
                {
                    return new ModbusClientService(mainModbusIp, mainModbusPort, mainModbusSlaveId);
                }
                else // RTU
                {
                    return new ModbusClientService(mainModbusComPort, mainModbusBaudRate, mainModbusParity, mainModbusStopBits, mainModbusDataBits, mainModbusSlaveId);
                }
            });

            // MC Protocol Service 등록
            services.AddSingleton<IMcProtocolService, McProtocolService>(provider =>
                new McProtocolService(mcProtocolIp, mcProtocolPort, mcProtocolCpuType, mcProtocolNetworkNo, mcProtocolPcNo)
            );

            // RobotMissionService 등록
            services.AddSingleton<IRobotMissionService, RobotMissionService>(provider =>
            {
                var httpService = provider.GetRequiredService<HttpService>();
                var databaseService = provider.GetRequiredService<DatabaseService>();
                var mcProtocolService = provider.GetRequiredService<IMcProtocolService>(); // IMcProtocolService 주입
                string wrapRackTitle = ConfigurationManager.AppSettings["WrapRackTitle"] ?? "WRAP";
                char[] militaryCharacter = { 'a', 'b', 'c', ' ' }; // MainViewModel과 동일하게 정의

                // MainViewModel의 인스턴스를 직접 가져와 델리게이트를 할당 (MainViewModel은 나중에 GetRequiredService로 가져오므로 직접 할당)
                var mainViewModelInstance = provider.GetRequiredService<MainViewModel>();

                // Mission Check Modbus Service 인스턴스를 팩토리 메서드 내부에서 직접 생성
                ModbusClientService missionCheckModbusService;
                if (missionModbusMode.Equals("TCP", StringComparison.OrdinalIgnoreCase))
                {
                    missionCheckModbusService = new ModbusClientService(
                        ConfigurationManager.AppSettings["WarehouseAMR"].Equals("AMR_1") ? "192.168.200.202" : "192.168.200.222", // missionModbusIp, 
                        missionModbusPort, missionModbusSlaveId);
                }
                else // RTU
                {
                    missionCheckModbusService = new ModbusClientService(missionModbusComPort, missionModbusBaudRate, missionModbusParity, missionModbusStopBits, missionModbusDataBits, missionModbusSlaveId);
                }

                ModbusClientService missionCheckModbusService2; // for AMR_2
                if (missionModbusMode.Equals("TCP", StringComparison.OrdinalIgnoreCase))
                {
                    missionCheckModbusService2 = new ModbusClientService(
                        ConfigurationManager.AppSettings["PackagingLineAMR"].Equals("AMR_2") ? "192.168.200.222" : "192.168.200.202", // missionModbusIp, 
                        missionModbusPort, missionModbusSlaveId);
                }
                else // RTU
                {
                    missionCheckModbusService = new ModbusClientService(missionModbusComPort, missionModbusBaudRate, missionModbusParity, missionModbusStopBits, missionModbusDataBits, missionModbusSlaveId);
                }

                return new RobotMissionService(
                    httpService,
                    databaseService,
                    mcProtocolService, // IMcProtocolService 전달
                    wrapRackTitle,
                    militaryCharacter,
                    mainViewModelInstance.GetRackViewModelById, // MainViewModel의 델리게이트 전달
                    missionCheckModbusService, // Mission Check ModbusClientService 인스턴스 직접 전달
                    () => mainViewModelInstance.InputStringForBullet, // InputStringForBullet 델리게이트 전달
                    () => mainViewModelInstance.InputStringForButton, // InputStringForButton 델리게이트 전달
                    () => mainViewModelInstance.InputStringForBoxes, // InputStringForBoxes 델리게이트 전달
                    mainViewModelInstance.SetInputStringForBullet,
                    mainViewModelInstance.SetInputStringForButton,
                    mainViewModelInstance.SetInputStringForBoxes,
                    mainViewModelInstance
                );
            });

            // MainViewModel 등록
            services.AddSingleton<MainViewModel>(provider =>
            {
                string warehousePayload = ConfigurationManager.AppSettings["WarehouseAMR"] ?? "AMR_1";
                string packagingLinePayload = ConfigurationManager.AppSettings["PackagingLineAMR"] ?? "AMR_2";
                var mcProtocolService = provider.GetRequiredService<IMcProtocolService>(); // IMcProtocolService 주입

                return new MainViewModel(
                    provider.GetRequiredService<DatabaseService>(),
                    provider.GetRequiredService<HttpService>(),
                    provider.GetRequiredService<ModbusClientService>(), // MainViewModel용 ModbusClientService (기본 등록된 인스턴스)
                    warehousePayload,
                    packagingLinePayload,
                    mcProtocolService
                );
            });

            // MainWindow 등록
            services.AddSingleton<MainWindow>();

            ServiceProvider = services.BuildServiceProvider();
        }

        /// <summary>
        /// App.config에서 바이트 값을 안전하게 파싱하는 헬퍼 메서드.
        /// isHexByDefault가 true면 16진수 파싱을 우선 시도하고, 실패 시 10진수 파싱을 시도합니다.
        /// isHexByDefault가 false면 10진수 파싱을 우선 시도합니다.
        /// </summary>
        /// <param name="configValue">App.config에서 읽어온 문자열 값.</param>
        /// <param name="defaultValue">파싱 실패 시 사용할 기본 바이트 값.</param>
        /// <param name="parseAsHexByDefault">기본적으로 16진수로 파싱할지 여부.</param>
        /// <returns>파싱된 바이트 값 또는 기본값.</returns>
        private static byte ParseByteConfig(string configValue, byte defaultValue, bool parseAsHexByDefault)
        {
            if (string.IsNullOrWhiteSpace(configValue))
            {
                return defaultValue;
            }

            byte parsedValue;

            if (parseAsHexByDefault)
            {
                // 먼저 16진수로 시도 (예: "90", "FF")
                if (byte.TryParse(configValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsedValue))
                {
                    return parsedValue;
                }
                // 16진수 파싱 실패 또는 오버플로 발생 시 10진수로 재시도 (예: "144"를 10진수로 해석)
                if (byte.TryParse(configValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedValue))
                {
                    Debug.WriteLine($"[App.xaml.cs] Warning: Config value '{configValue}' was intended as hex but successfully parsed as decimal. Consider using hex format in App.config.");
                    return parsedValue;
                }
            }
            else
            {
                // 기본적으로 10진수로 시도 (예: "0", "1")
                if (byte.TryParse(configValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedValue))
                {
                    return parsedValue;
                }
                // 10진수 파싱 실패 또는 오버플로 발생 시 16진수로 재시도 (드문 경우지만 방어적 코드)
                if (byte.TryParse(configValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsedValue))
                {
                    Debug.WriteLine($"[App.xaml.cs] Warning: Config value '{configValue}' was intended as decimal but successfully parsed as hex. Consider using decimal format in App.config.");
                    return parsedValue;
                }
            }

            // 모든 시도가 실패하면 경고 로깅 및 기본값 반환
            Debug.WriteLine($"[App.xaml.cs] Error: Could not parse config value '{configValue}' as either hex or decimal. Using default value {defaultValue}.");
            return defaultValue;
        }
    }
}
