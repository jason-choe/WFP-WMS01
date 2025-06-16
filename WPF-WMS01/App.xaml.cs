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
            string username = ConfigurationManager.AppSettings["AntApiUsername"];
            string password = ConfigurationManager.AppSettings["AntApiPassword"];

            // HttpService를 싱글톤으로 등록
            services.AddSingleton(new HttpService(baseApiUrl));

            // MainViewModel 등록 (이 부분이 변경됨)
            services.AddSingleton(provider => new MainViewModel( // MainViewModel로 변경
                provider.GetRequiredService<HttpService>(),
                username,
                password
            ));

            // MainWindow 등록
            services.AddSingleton(provider => new MainWindow
            {
                DataContext = provider.GetRequiredService<MainViewModel>() // MainViewModel로 변경
            });

            ServiceProvider = services.BuildServiceProvider();
        }
    }
}