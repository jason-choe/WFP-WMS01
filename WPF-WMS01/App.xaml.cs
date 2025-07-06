// App.xaml.cs
using System.Configuration; // ConfigurationManager를 위해 추가
using Microsoft.Extensions.DependencyInjection; // NuGet 패키지 설치 필요
using System.Windows;
using System;
using WPF_WMS01.Services; // HttpService
using WPF_WMS01.ViewModels; // MainViewModel
using System.Diagnostics; // Debug.WriteLine을 위해 추가

namespace WPF_WMS01
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private MainViewModel _mainViewModelInstance;
        private ModbusClientService _modbusServiceInstance;
        private RobotMissionService _robotMissionServiceInstance;

        public static IServiceProvider ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            Debug.WriteLine("[App] OnStartup: Configuring Services...");
            ConfigureServices();
            Debug.WriteLine("[App] OnStartup: Services Configured.");

            // DI 컨테이너에서 인스턴스를 가져와 필드에 할당 (Dispose를 위해)
            _mainViewModelInstance = ServiceProvider.GetRequiredService<MainViewModel>();
            // MainViewModel에 주입된 ModbusClientService는 콜 버튼용이므로, _modbusServiceInstance에 할당
            _modbusServiceInstance = ServiceProvider.GetRequiredService<ModbusClientService>();
            _robotMissionServiceInstance = (RobotMissionService)ServiceProvider.GetRequiredService<IRobotMissionService>();

            Debug.WriteLine("[App] OnStartup: Creating MainWindow...");
            // MainWindow를 생성하고 ViewModel을 주입
            var mainWindow = ServiceProvider.GetService<MainWindow>();
            mainWindow.Show();
            Debug.WriteLine("[App] OnStartup: MainWindow Shown.");
        }

        private void ConfigureServices()
        {
            var services = new ServiceCollection();

            // App.config에서 설정 값 읽기
            string baseApiUrl = ConfigurationManager.AppSettings["AntApiBaseUrl"];
            string waitRackTitle = ConfigurationManager.AppSettings["WaitRackTitle"] ?? "WAIT";
            char[] militaryCharacter = (ConfigurationManager.AppSettings["MilitaryCharacters"] ?? "abc ").ToCharArray();

            // 새로운 AMR Payload 값 읽기
            string warehousePayload = ConfigurationManager.AppSettings["WarehouseAMR"] ?? "AMR_1";
            string productionLinePayload = ConfigurationManager.AppSettings["ProductionLineAMR"] ?? "AMR_2";

            // 콜 버튼용 Modbus 설정 읽기
            string callButtonModbusIp = ConfigurationManager.AppSettings["ModbusIpAddress"];
            int callButtonModbusPort = int.Parse(ConfigurationManager.AppSettings["ModbusPort"]);
            byte callButtonModbusSlaveId = byte.Parse(ConfigurationManager.AppSettings["ModbusSlaveId"]);

            // 미션 실패 확인용 Modbus 설정 읽기
            string missionCheckModbusIp = ConfigurationManager.AppSettings["MissionCheckModbusIpAddress"];
            int missionCheckModbusPort = int.Parse(ConfigurationManager.AppSettings["MissionCheckModbusPort"]);
            byte missionCheckModbusSlaveId = byte.Parse(ConfigurationManager.AppSettings["MissionCheckModbusSlaveId"]);


            Debug.WriteLine("[App.ConfigureServices] Registering HttpService, DatabaseService, ModbusClientService...");
            services.AddSingleton<HttpService>(new HttpService(baseApiUrl));
            services.AddSingleton<DatabaseService>();
            // 콜 버튼용 ModbusClientService 등록
            services.AddSingleton<ModbusClientService>(provider =>
                new ModbusClientService(
                    callButtonModbusIp,
                    callButtonModbusPort,
                    callButtonModbusSlaveId
                ));

            Debug.WriteLine("[App.ConfigureServices] Registering MainViewModel (initial)...");
            // MainViewModel 등록: 이제 생성자에서 AMR payload 값들을 받습니다.
            services.AddSingleton<MainViewModel>(provider =>
                new MainViewModel(
                    provider.GetRequiredService<DatabaseService>(),
                    provider.GetRequiredService<HttpService>(),
                    provider.GetRequiredService<ModbusClientService>(), // 콜 버튼용 ModbusClientService 주입
                    warehousePayload, // App.config에서 읽은 값 전달
                    productionLinePayload // App.config에서 읽은 값 전달
                ));

            Debug.WriteLine("[App.ConfigureServices] Registering IRobotMissionService factory...");
            // IRobotMissionService와 RobotMissionService 등록
            services.AddSingleton<IRobotMissionService, RobotMissionService>(provider =>
            {
                Debug.WriteLine("[App.ConfigureServices] IRobotMissionService factory: Getting MainViewModel instance...");
                var mainViewModelInstance = provider.GetRequiredService<MainViewModel>();
                Debug.WriteLine("[App.ConfigureServices] IRobotMissionService factory: MainViewModel instance obtained. Creating RobotMissionService...");
                return new RobotMissionService(
                    provider.GetRequiredService<HttpService>(),
                    provider.GetRequiredService<DatabaseService>(),
                    waitRackTitle,
                    militaryCharacter,
                    mainViewModelInstance.GetRackViewModelById, // MainViewModel의 메서드를 델리게이트로 전달
                    missionCheckModbusIp, // 미션 실패 확인용 Modbus IP 전달
                    missionCheckModbusPort, // 미션 실패 확인용 Modbus Port 전달
                    missionCheckModbusSlaveId // 미션 실패 확인용 Modbus Slave ID 전달
                );
            });

            Debug.WriteLine("[App.ConfigureServices] Registering MainWindow...");
            services.AddSingleton(provider => new MainWindow
            {
                DataContext = provider.GetRequiredService<MainViewModel>() // DataContext 설정
            });

            Debug.WriteLine("[App.ConfigureServices] Building ServiceProvider...");
            ServiceProvider = services.BuildServiceProvider();
            Debug.WriteLine("[App.ConfigureServices] ServiceProvider Built.");

            // ServiceProvider 빌드 후, MainViewModel에 RobotMissionService 주입
            Debug.WriteLine("[App.ConfigureServices] Performing late injection of RobotMissionService into MainViewModel...");
            var resolvedMainViewModel = ServiceProvider.GetRequiredService<MainViewModel>();
            var resolvedRobotMissionService = ServiceProvider.GetRequiredService<IRobotMissionService>();
            resolvedMainViewModel.SetRobotMissionService(resolvedRobotMissionService);
            Debug.WriteLine("[App.ConfigureServices] Late injection complete.");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            Debug.WriteLine("[App] OnExit: Disposing services...");
            _mainViewModelInstance?.Dispose();
            _modbusServiceInstance?.Dispose(); // 콜 버튼용 Modbus 서비스 해제
            _robotMissionServiceInstance?.Dispose(); // 로봇 미션 서비스 해제 (내부적으로 미션 체크 Modbus 서비스도 해제)

            if (ServiceProvider is IDisposable disposableProvider)
            {
                disposableProvider.Dispose();
            }
            Debug.WriteLine("[App] OnExit: Services Disposed.");
        }
    }
}
