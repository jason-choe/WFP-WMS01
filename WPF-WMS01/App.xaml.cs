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

            Debug.WriteLine("[App.ConfigureServices] Registering HttpService, DatabaseService, ModbusClientService...");
            services.AddSingleton<HttpService>(new HttpService(baseApiUrl));
            services.AddSingleton<DatabaseService>();
            services.AddSingleton<ModbusClientService>(provider =>
                new ModbusClientService(
                    ConfigurationManager.AppSettings["ModbusIpAddress"],
                    int.Parse(ConfigurationManager.AppSettings["ModbusPort"]),
                    byte.Parse(ConfigurationManager.AppSettings["ModbusSlaveId"])
                ));

            Debug.WriteLine("[App.ConfigureServices] Registering MainViewModel (initial)...");
            // MainViewModel 등록: 이제 생성자에서 IRobotMissionService를 받지 않습니다.
            services.AddSingleton<MainViewModel>();

            Debug.WriteLine("[App.ConfigureServices] Registering IRobotMissionService factory...");
            // IRobotMissionService와 RobotMissionService 등록
            // RobotMissionService는 MainViewModel의 GetRackViewModelById 메서드를 필요로 하므로,
            // MainViewModel 인스턴스가 이미 DI 컨테이너에 존재한다고 가정하고 가져와 전달합니다.
            services.AddSingleton<IRobotMissionService, RobotMissionService>(provider =>
            {
                Debug.WriteLine("[App.ConfigureServices] IRobotMissionService factory: Getting MainViewModel instance...");
                // MainViewModel 인스턴스를 가져옵니다. 이 시점에는 MainViewModel이 이미 생성되어 있어야 합니다.
                var mainViewModelInstance = provider.GetRequiredService<MainViewModel>();
                Debug.WriteLine("[App.ConfigureServices] IRobotMissionService factory: MainViewModel instance obtained. Creating RobotMissionService...");
                return new RobotMissionService(
                    provider.GetRequiredService<HttpService>(),
                    provider.GetRequiredService<DatabaseService>(),
                    waitRackTitle,
                    militaryCharacter,
                    mainViewModelInstance.GetRackViewModelById // MainViewModel의 메서드를 델리게이트로 전달
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
            // 이는 MainViewModel과 RobotMissionService 간의 순환 참조를 해결하는 핵심 단계입니다.
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
            _modbusServiceInstance?.Dispose();
            _robotMissionServiceInstance?.Dispose();

            if (ServiceProvider is IDisposable disposableProvider)
            {
                disposableProvider.Dispose();
            }
            Debug.WriteLine("[App] OnExit: Services Disposed.");
        }
    }
}
