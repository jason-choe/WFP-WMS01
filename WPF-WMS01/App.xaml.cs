// App.xaml.cs
using System.Configuration; // ConfigurationManager를 위해 추가
using Microsoft.Extensions.DependencyInjection; // NuGet 패키지 설치 필요
using System.Windows;
using System;
using WPF_WMS01.Services; // HttpService
using WPF_WMS01.ViewModels; // MainViewModel (이 부분이 변경됨)

namespace WPF_WMS01
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ConfigureServices();

            // MainWindow를 생성하고 ViewModel을 주입
            var mainWindow = ServiceProvider.GetService<MainWindow>();
            mainWindow.Show();
        }

        private void ConfigureServices()
        {
            var services = new ServiceCollection();

            // App.config에서 설정 값 읽기
            string baseApiUrl = ConfigurationManager.AppSettings["AntApiBaseUrl"];
            string waitRackTitle = ConfigurationManager.AppSettings["WaitRackTitle"] ?? "WAIT";
            char[] militaryCharacter = (ConfigurationManager.AppSettings["MilitaryCharacters"] ?? "abc ").ToCharArray(); // App.config에 추가하거나 기본값 사용

            // 서비스 등록
            services.AddSingleton<HttpService>(new HttpService(baseApiUrl));
            services.AddSingleton<DatabaseService>(); // DatabaseService가 생성자 매개변수가 없다면
            services.AddSingleton<ModbusClientService>(provider =>
                new ModbusClientService(
                    ConfigurationManager.AppSettings["ModbusIpAddress"],
                    int.Parse(ConfigurationManager.AppSettings["ModbusPort"]),
                    byte.Parse(ConfigurationManager.AppSettings["ModbusSlaveId"])
                )); // ModbusClientService 인스턴스 생성

            // IRobotMissionService와 RobotMissionService 등록
            services.AddSingleton<IRobotMissionService, RobotMissionService>(provider =>
                new RobotMissionService(
                    provider.GetRequiredService<HttpService>(),
                    provider.GetRequiredService<DatabaseService>(),
                    waitRackTitle,
                    militaryCharacter // App.config에서 읽은 값 전달
                ));

            // MainViewModel 등록 (모든 종속성을 생성자 주입)
            services.AddSingleton<MainViewModel>(); // DI 컨테이너가 MainViewModel과 그 종속성을 해결

            // MainWindow 등록
            services.AddSingleton(provider => new MainWindow
            {
                DataContext = provider.GetRequiredService<MainViewModel>() // DataContext 설정
            });

            ServiceProvider = services.BuildServiceProvider();
        }
    }
}