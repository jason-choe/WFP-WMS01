using Microsoft.Extensions.DependencyInjection;
using System;
using System.Configuration;
using System.IO.Ports; // Parity, StopBits를 위해 추가
using System.Windows;
using WPF_WMS01.Services;
using WPF_WMS01.ViewModels;

namespace WPF_WMS01
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
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
            byte mainModbusSlaveId = byte.Parse(ConfigurationManager.AppSettings["ModbusSlaveId"] ?? "1");
            string mainModbusComPort = ConfigurationManager.AppSettings["ModbusComPort"] ?? "COM3";
            int mainModbusBaudRate = int.Parse(ConfigurationManager.AppSettings["ModbusBaudRate"] ?? "9600");
            Parity mainModbusParity = (Parity)Enum.Parse(typeof(Parity), ConfigurationManager.AppSettings["ModbusParity"] ?? "None");
            int mainModbusDataBits = int.Parse(ConfigurationManager.AppSettings["ModbusDataBits"] ?? "8");
            StopBits mainModbusStopBits = (StopBits)Enum.Parse(typeof(StopBits), ConfigurationManager.AppSettings["ModbusStopBits"] ?? "One");

            // App.config에서 미션 체크용 Modbus 설정 읽기 (RobotMissionService용)
            string missionModbusMode = ConfigurationManager.AppSettings["MissionModbusMode"] ?? "TCP";
            string missionModbusIp = ConfigurationManager.AppSettings["MissionModbusIpAddress"] ?? "127.0.0.1";
            int missionModbusPort = int.Parse(ConfigurationManager.AppSettings["MissionModbusPort"] ?? "503");
            byte missionModbusSlaveId = byte.Parse(ConfigurationManager.AppSettings["MissionModbusSlaveId"] ?? "1");
            string missionModbusComPort = ConfigurationManager.AppSettings["MissionModbusComPort"] ?? "COM4";
            int missionModbusBaudRate = int.Parse(ConfigurationManager.AppSettings["MissionModbusBaudRate"] ?? "9600");
            Parity missionModbusParity = (Parity)Enum.Parse(typeof(Parity), ConfigurationManager.AppSettings["MissionModbusParity"] ?? "None");
            int missionModbusDataBits = int.Parse(ConfigurationManager.AppSettings["MissionModbusDataBits"] ?? "8");
            StopBits missionModbusStopBits = (StopBits)Enum.Parse(typeof(StopBits), ConfigurationManager.AppSettings["MissionModbusStopBits"] ?? "One");

            // HttpService 등록 (생성자에 baseApiUrl 주입)
            services.AddSingleton<HttpService>(provider =>
            {
                string antApiBaseUrl = ConfigurationManager.AppSettings["AntApiBaseUrl"] ?? "http://localhost:8081/";
                return new HttpService(antApiBaseUrl);
            });

            // DatabaseService 등록
            services.AddSingleton<DatabaseService>();

            // ModbusClientService 인스턴스 등록 (MainViewModel용)
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

            // RobotMissionService에 주입될 ModbusClientService 인스턴스 등록 (미션 체크용)
            services.AddSingleton<IRobotMissionService, RobotMissionService>(provider =>
            {
                var httpService = provider.GetRequiredService<HttpService>();
                var databaseService = provider.GetRequiredService<DatabaseService>();
                string waitRackTitle = ConfigurationManager.AppSettings["WaitRackTitle"] ?? "WAIT";
                char[] militaryCharacter = { 'a', 'b', 'c', ' ' }; // MainViewModel과 동일하게 정의

                // Mission Check Modbus Service 인스턴스 생성
                ModbusClientService missionCheckModbusService;
                if (missionModbusMode.Equals("TCP", StringComparison.OrdinalIgnoreCase))
                {
                    missionCheckModbusService = new ModbusClientService(missionModbusIp, missionModbusPort, missionModbusSlaveId);
                }
                else // RTU
                {
                    missionCheckModbusService = new ModbusClientService(missionModbusComPort, missionModbusBaudRate, missionModbusParity, missionModbusStopBits, missionModbusDataBits, missionModbusSlaveId);
                }

                return new RobotMissionService(
                    httpService,
                    databaseService,
                    waitRackTitle,
                    militaryCharacter,
                    provider.GetRequiredService<MainViewModel>().GetRackViewModelById, // MainViewModel의 델리게이트 전달
                    missionCheckModbusService // ModbusClientService 인스턴스 직접 전달
                );
            });

            // MainViewModel 등록
            services.AddSingleton<MainViewModel>(provider =>
            {
                string warehousePayload = ConfigurationManager.AppSettings["WarehouseAMR"] ?? "AMR_1";
                string packagingLinePayload = ConfigurationManager.AppSettings["PackagingLineAMR"] ?? "AMR_2";

                return new MainViewModel(
                    provider.GetRequiredService<DatabaseService>(),
                    provider.GetRequiredService<HttpService>(),
                    provider.GetRequiredService<ModbusClientService>(), // MainViewModel용 ModbusClientService
                    warehousePayload,
                    packagingLinePayload
                );
            });

            // MainWindow 등록
            services.AddSingleton<MainWindow>();

            ServiceProvider = services.BuildServiceProvider();
        }
    }
}
