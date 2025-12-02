using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading; // DispatcherTimer 사용을 위해 필요
using System.Diagnostics; // Debug.WriteLine을 위해 필요
using System.Threading; // CancellationTokenSource 사용을 위해 추가
using WPF_WMS01.Models; // RobotMissionInfo, MissionStepDefinition 등 모델 참조
using WPF_WMS01.ViewModels; // RackViewModel 참조를 위해 필요
using System.Net.Http; // HttpRequestException을 위해 필요
using Newtonsoft.Json; // JsonConvert를 위해 필요
using JsonException = Newtonsoft.Json.JsonException; // 충돌 방지를 위해 별칭 사용
using System.Windows; // Application.Current.Dispatcher.Invoke를 위해 추가 (MessageBox는 제거)
using System.Configuration; // ConfigurationManager를 위해 추가
using System.IO.Ports; // Parity, StopBits를 위해 추가 (더 이상 직접 사용되지 않지만, 다른 곳에서 필요할 수 있으므로 유지)
using System.Collections.Concurrent; // ConcurrentDictionary를 위해 추가

namespace WPF_WMS01.Services
{
    /// <summary>
    /// 로봇 미션의 시작, 상태 폴링, 완료/실패 처리를 담당하는 서비스 클래스입니다.
    /// MainViewModel으로부터 로봇 미션 관련 로직이 분리되었습니다.
    /// </summary>
    public class RobotMissionService : IRobotMissionService
    {
        private readonly HttpService _httpService;
        private readonly DatabaseService _databaseService;
        private readonly IMcProtocolService _mcProtocolService; // MC Protocol 서비스 주입
        private DispatcherTimer _robotMissionPollingTimer;
        private readonly ModbusClientService _missionCheckModbusServiceA; // 미션 실패 확인용 ModbusClientService for warehouse AMR
        private readonly ModbusClientService _missionCheckModbusServiceB; // 미션 실패 확인용 ModbusClientService for production line AMR
        private readonly MainViewModel _mainViewModel; // MainViewModel 참조 추가

        // 현재 진행 중인 로봇 미션 프로세스들을 추적 (Key: ProcessId)
        private readonly ConcurrentDictionary<string, RobotMissionInfo> _activeRobotProcesses = new ConcurrentDictionary<string, RobotMissionInfo>(); // ConcurrentDictionary로 변경

        // MainViewModel로부터 주입받을 종속성 (UI 업데이트 및 특정 값 조회용)
        private readonly string _wrapRackTitle;
        private Func<string> _getInputStringForBulletFunc; // MainViewModel의 InputStringForBullet 값을 가져오는 델리게이트
        private Func<string> _getInputStringForButtonFunc; // MainViewModel의 InputStringForButton 값을 가져오는 델리게이트
        private Func<string> _getInputStringForBoxesFunc;
        // MainViewModel에게 값을 전달하기 위한 델리게이트
        public Action<string> _setInputStringForBulletFunc { get; set; }
        public Action<string> _setInputStringForButtonFunc { get; set; }
        public Action<string> _setInputStringForBoxesFunc { get; set; }
        private Func<int, RackViewModel> _getRackViewModelByIdFunc; // MainViewModel에서 RackViewModel을 ID로 가져오는 델리게이트

        // RobotMissionService에서 발생하는 중요한 Modbus 오류 메시지가 이미 표시되었는지 추적하는 플래그
        private bool _hasMissionCriticalModbusErrorBeenDisplayed = false;
        // Modbus 오류 메시지 표시를 억제할 타이머
        private DispatcherTimer _modbusErrorMessageSuppressionTimer;
        private const int MODBUS_ERROR_MESSAGE_SUPPRESSION_SECONDS = 30; // 30초 동안 메시지 억제

        // PLC 연결 재시도 관련 상수 추가
        private const int MAX_PLC_CONNECT_RETRIES = 3; // MC Protocol 연결 시도 최대 재시도 횟수 // MAX_PLC_CONNECT_RETRIES
        private const int PLC_CONNECT_RETRY_DELAY_SECONDS = 5; // MC Protocol 재시도 간 지연 시간 // PLC_CONNECT_RETRY_DELAY_SECONDS

        private bool _isPollingInProgress = false; // 폴링 타이머 재진입 방지 플래그
        private readonly bool _mcProtocolInterface = ConfigurationManager.AppSettings["McProtocolInterface"].Equals("true") ? true : false;

        private readonly string _warehouseAmrName = ConfigurationManager.AppSettings["WarehouseAMRName"] ?? "";
        private readonly string _packagingLineAmrName = ConfigurationManager.AppSettings["PackagingLineAMRName"] ?? "";

        /// <summary>
        /// MainViewModel로 상태를 다시 보고하기 위한 이벤트
        /// </summary>
        public event Action<string> OnShowAutoClosingMessage;
        public event Action<int, bool> OnRackLockStateChanged;
        public event Action OnInputStringForBulletCleared;
        public event Action OnInputStringForButtonCleared;
        public event Action OnInputStringForBoxesCleared;
        public event Action<ushort> OnTurnOffAlarmLightRequest;
        public event Action<RobotMissionInfo> OnMissionProcessUpdated; // 새로운 이벤트 추가

        /// <summary>
        /// RobotMissionService의 새 인스턴스를 초기화합니다.
        /// 이 생성자는 미션 실패 확인을 위해 ModbusClientService 인스턴스와 MC Protocol 서비스 인스턴스를 주입받습니다.
        /// </summary>
        /// <param name="httpService">HTTP 통신을 위한 서비스 인스턴스.</param>
        /// <param name="databaseService">데이터베이스 접근을 위한 서비스 인스턴스.</param>
        /// <param name="mcProtocolService">MC Protocol 통신을 위한 서비스 인스턴스.</param>
        /// <param name="wrapRackTitle">WRAP 랙의 타이틀 문자열.</param>
        /// <param name="militaryCharacter">군수품 문자 배열.</param>
        /// <param name="getRackViewModelByIdFunc">Rack ID로 RackViewModel을 가져오는 함수.</param>
        /// <param name="missionCheckModbusService">미션 실패 확인용으로 미리 설정된 ModbusClientService 인스턴스.</param>
        /// <param name="getInputStringForBulletFunc">InputStringForBullet 값을 가져오는 델리게이트 (사용자 입력값).</param>
        /// <param name="getInputStringForButtonFunc">InputStringForButton 값을 가져오는 델리게이트 (사용자 입력값).</param>
        /// <param name="getInputStringForBoxesFunc">InputStringForBoxes 값을 가져오는 델리게이트 (사용자 입력값).</param>
        /// <param name="setInputStringForBulletFunc">InputStringForBullet 값을 써넣는 델리게이트 (사용자 입력값).</param>
        /// <param name="setInputStringForButtonFunc">InputStringForButton 값을 써넣는 델리게이트 (사용자 입력값).</param>
        /// <param name="setInputStringForBoxesFunc">InputStringForBoxes 값을 써넣는 델리게이트 (사용자 입력값).</param>
        public RobotMissionService(
            HttpService httpService,
            DatabaseService databaseService,
            IMcProtocolService mcProtocolService, // MC Protocol Service 주입
            string wrapRackTitle,
            Func<int, RackViewModel> getRackViewModelByIdFunc,
            ModbusClientService missionCheckModbusServiceA, // ModbusClientService 인스턴스를 직접 주입받도록 변경
            ModbusClientService missionCheckModbusServiceB, // ModbusClientService 인스턴스를 직접 주입받도록 변경
            Func<string> getInputStringForBulletFunc, // 델리게이트 재추가
            Func<string> getInputStringForButtonFunc, // 델리게이트 재추가
            Func<string> getInputStringForBoxesFunc, // 델리게이트 재추가
            Action<string> setInputStringForBulletFunc,
            Action<string> setInputStringForButtonFunc,
            Action<string> setInputStringForBoxesFunc,
            MainViewModel mainViewModel)
        {
            _httpService = httpService ?? throw new ArgumentNullException(nameof(httpService));
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _mcProtocolService = mcProtocolService ?? throw new ArgumentNullException(nameof(mcProtocolService)); // MC Protocol 서비스 초기화
            _wrapRackTitle = wrapRackTitle;
            _getRackViewModelByIdFunc = getRackViewModelByIdFunc ?? throw new ArgumentNullException(nameof(getRackViewModelByIdFunc));
            _getInputStringForBulletFunc = getInputStringForBulletFunc ?? throw new ArgumentNullException(nameof(getInputStringForBulletFunc)); // 델리게이트 초기화
            _getInputStringForButtonFunc = getInputStringForButtonFunc ?? throw new ArgumentNullException(nameof(getInputStringForButtonFunc)); // 델리게이트 초기화
            _getInputStringForBoxesFunc = getInputStringForBoxesFunc ?? throw new ArgumentNullException(nameof(getInputStringForBoxesFunc)); // 델리게이트 초기화
            _setInputStringForBulletFunc = setInputStringForBulletFunc ?? throw new ArgumentNullException(nameof(setInputStringForBulletFunc));
            _setInputStringForButtonFunc = setInputStringForButtonFunc ?? throw new ArgumentNullException(nameof(setInputStringForButtonFunc));
            _setInputStringForBoxesFunc = setInputStringForBoxesFunc ?? throw new ArgumentNullException(nameof(setInputStringForBoxesFunc));
            _mainViewModel = mainViewModel;
            // 미션 실패 확인용 ModbusClientService 인스턴스 주입
            _missionCheckModbusServiceA = missionCheckModbusServiceA ?? throw new ArgumentNullException(nameof(missionCheckModbusServiceA));
            Debug.WriteLine($"[RobotMissionService] Mission Check Modbus Service Injected. Current connection status: {_missionCheckModbusServiceA.IsConnected}");
            _missionCheckModbusServiceB = missionCheckModbusServiceB ?? throw new ArgumentNullException(nameof(missionCheckModbusServiceB));
            Debug.WriteLine($"[RobotMissionService] Mission Check Modbus Service Injected. Current connection status: {_missionCheckModbusServiceB.IsConnected}");

            SetupRobotMissionPollingTimer(); // 로봇 미션 폴링 타이머 설정 및 시작
            SetupModbusErrorMessageSuppressionTimer(); // Modbus 오류 메시지 억제 타이머 설정
        }

        /// <summary>
        /// 로봇 미션 폴링 타이머를 설정하고 시작합니다.
        /// </summary>
        private void SetupRobotMissionPollingTimer()
        {
            _robotMissionPollingTimer = new DispatcherTimer();
            _robotMissionPollingTimer.Interval = TimeSpan.FromSeconds(1); // 1초마다 폴링 (조정 가능)
            _robotMissionPollingTimer.Tick += RobotMissionPollingTimer_Tick;
            _robotMissionPollingTimer.Start();
            Debug.WriteLine("[RobotMissionService] Robot Mission Polling Timer Started.");
        }

        /// <summary>
        /// Modbus 오류 메시지 표시를 억제하기 위한 타이머를 설정합니다.
        /// </summary>
        private void SetupModbusErrorMessageSuppressionTimer()
        {
            _modbusErrorMessageSuppressionTimer = new DispatcherTimer();
            _modbusErrorMessageSuppressionTimer.Interval = TimeSpan.FromSeconds(MODBUS_ERROR_MESSAGE_SUPPRESSION_SECONDS);
            _modbusErrorMessageSuppressionTimer.Tick += (sender, e) =>
            {
                _hasMissionCriticalModbusErrorBeenDisplayed = false; // 타이머 만료 시 플래그 리셋
                _modbusErrorMessageSuppressionTimer.Stop(); // 타이머 중지
                Debug.WriteLine("[RobotMissionService] Modbus error message suppression timer expired. Messages can be displayed again.");
            };
            Debug.WriteLine("[RobotMissionService] Modbus Error Message Suppression Timer Setup.");
        }

        /// <summary>
        /// Modbus 오류 메시지를 표시하고, 일정 시간 동안 메시지 중복 표시를 억제합니다.
        /// </summary>
        /// <param name="message">표시할 오류 메시지.</param>
        private void DisplayModbusErrorMessage(string message)
        {
            if (!_hasMissionCriticalModbusErrorBeenDisplayed)
            {
                _hasMissionCriticalModbusErrorBeenDisplayed = true;
                OnShowAutoClosingMessage?.Invoke(message); // 자동 닫힘 메시지 사용
                _modbusErrorMessageSuppressionTimer.Start(); // 타이머 시작하여 메시지 억제
                Debug.WriteLine($"[RobotMissionService] Displayed Modbus Error Message: {message}");
            }
            else
            {
                Debug.WriteLine($"[RobotMissionService] Suppressed Modbus Error Message: {message}");
            }
        }

        /// <summary>
        /// 로봇 미션 상태를 주기적으로 폴링하고 처리합니다.
        /// </summary>
        private async void RobotMissionPollingTimer_Tick(object sender, EventArgs e)
        {
            if (_isPollingInProgress)
            {
                Debug.WriteLine("[RobotMissionService] Polling already in progress. Skipping this tick.");
                return;
            }

            _isPollingInProgress = true;
            try
            {
                // ConcurrentDictionary는 스레드 안전하므로 lock 불필요. ToList()로 복사본 생성하여 순회.
                List<RobotMissionInfo> currentActiveProcesses = _activeRobotProcesses.Values.ToList();

                foreach (var processInfo in currentActiveProcesses)
                {
                    // 이미 완료되었거나 실패한 미션은 폴링하지 않습니다.
                    if (processInfo.IsFinished || processInfo.IsFailed)
                    {
                        // 완료되거나 실패한 프로세스는 딕셔너리에서 제거합니다.
                        RobotMissionInfo removed;
                        _activeRobotProcesses.TryRemove(processInfo.ProcessId, out removed);
                        Debug.WriteLine($"[RobotMissionService] Process {processInfo.ProcessId} removed (Finished: {processInfo.IsFinished}, Failed: {processInfo.IsFailed}).");
                        continue;
                    }

                    // 재시도 로직: 너무 자주 폴링하지 않도록 시간 체크 (첫 시도는 바로 진행)
                    if (processInfo.PollingRetryCount > 0 && DateTime.Now - processInfo.LastPollingAttemptTime < TimeSpan.FromSeconds(RobotMissionInfo.PollingRetryDelaySeconds))
                    {
                        Debug.WriteLine($"[RobotMissionService] Process {processInfo.ProcessId}: Skipping polling due to retry delay. Retries: {processInfo.PollingRetryCount}");
                        continue;
                    }

                    processInfo.LastPollingAttemptTime = DateTime.Now; // 마지막 폴링 시도 시간 업데이트

                    // 현재 추적 중인 미션 (LastSentMissionId)이 있을 경우 ANT 서버에 미션 상태를 폴링합니다.
                    if (processInfo.LastSentMissionId.HasValue)
                    {
                        try
                        {
                            // AntApiModels.GetMissionInfoResponse 사용
                            var missionInfoResponse = await _httpService.GetAsync<GetMissionInfoResponse>($"wms/rest/v{_httpService.CurrentApiVersionMajor}.{_httpService.CurrentApiVersionMinor}/missions/{processInfo.LastSentMissionId.Value}").ConfigureAwait(false);

                            if (missionInfoResponse?.Payload?.Missions != null && missionInfoResponse.Payload.Missions.Any())
                            {
                                processInfo.PollingRetryCount = 0; // 성공적으로 응답 받으면 재시도 카운트 리셋

                                var latestMissionDetail = missionInfoResponse.Payload.Missions.First();
                                processInfo.CurrentMissionDetail = latestMissionDetail;

                                // UI 업데이트를 위한 이벤트 발생
                                MissionStatusEnum currentStatus;
                                // MissionDetail.NavigationState는 int 타입이므로 직접 비교
                                switch (latestMissionDetail.NavigationState)
                                {
                                    case 0: currentStatus = MissionStatusEnum.RECEIVED; break;
                                    case 1: currentStatus = MissionStatusEnum.ACCEPTED; break;
                                    case 2: currentStatus = MissionStatusEnum.REJECTED; break;
                                    case 3: currentStatus = MissionStatusEnum.STARTED; break;
                                    case 4: currentStatus = MissionStatusEnum.COMPLETED; break; // 4로 명확히 정의된 COMPLETED
                                    case 5: currentStatus = MissionStatusEnum.CANCELLED; break;
                                    case 7: currentStatus = MissionStatusEnum.FAILED; break;
                                    default: currentStatus = MissionStatusEnum.PENDING; break; // 알 수 없는 상태는 PENDING으로 처리
                                }
                                processInfo.CurrentStatus = currentStatus; // RobotMissionInfo 내부 상태 업데이트
                                processInfo.HmiStatus.Status = currentStatus.ToString();

                                var subPercentage = currentStatus == MissionStatusEnum.COMPLETED ? 1.0
                                    : currentStatus == MissionStatusEnum.FAILED ? 1.0
                                    : currentStatus == MissionStatusEnum.STARTED ? 0.5
                                    : currentStatus == MissionStatusEnum.ACCEPTED ? 0.1
                                    : 0.0;
                                double progressNumerator = processInfo.CurrentStepIndex + subPercentage;
                                // 진행률 업데이트 (전체 미션 단계 중 현재 완료된 단계의 비율)
                                //double progressNumerator = (currentStatus == MissionStatusEnum.COMPLETED && processInfo.CurrentStepIndex <= processInfo.TotalSteps) ?
                                //                           processInfo.CurrentStepIndex : Math.Max(0, processInfo.CurrentStepIndex - 1);
                                processInfo.HmiStatus.ProgressPercentage = (int)((progressNumerator / processInfo.TotalSteps) * 100);

                                // 현재 진행 중인 미션 단계의 설명
                                if (processInfo.CurrentStepIndex < processInfo.MissionSteps.Count) // 현재 단계가 유효한 범위 내에 있을 때
                                {
                                    processInfo.HmiStatus.CurrentStepDescription = processInfo.MissionSteps[processInfo.CurrentStepIndex].ProcessStepDescription;
                                }
                                else if (processInfo.CurrentStepIndex == processInfo.MissionSteps.Count && currentStatus == MissionStatusEnum.COMPLETED)
                                {
                                    processInfo.HmiStatus.CurrentStepDescription = "모든 미션 단계 완료";
                                }
                                else
                                {
                                    processInfo.HmiStatus.CurrentStepDescription = "미션 대기 중 또는 정보 없음";
                                }

                                OnShowAutoClosingMessage?.Invoke($"로봇 미션 단계 업데이트: {processInfo.HmiStatus.CurrentStepDescription} ({processInfo.HmiStatus.ProgressPercentage}%)");

                                // 미션 프로세스 업데이트 이벤트 발생
                                OnMissionProcessUpdated?.Invoke(processInfo);


                                Debug.WriteLine($"[RobotMissionService] Process {processInfo.ProcessId} - Polling Mission {latestMissionDetail.MissionId}: " +
                                                $"Status: {processInfo.HmiStatus.Status}, Progress: {processInfo.HmiStatus.ProgressPercentage}%. Desc: {processInfo.HmiStatus.CurrentStepDescription}");
                                Debug.WriteLine($"[RobotMissionService DEBUG] Polled Mission Detail for {latestMissionDetail.MissionId}:");
                                Debug.WriteLine($"  NavigationState: {latestMissionDetail.NavigationState}");
                                Debug.WriteLine($"  Priority / SchedulerState: {latestMissionDetail.Priority} / {latestMissionDetail.SchedulerState}");
                                Debug.WriteLine($"  State: {latestMissionDetail.State}");
                                Debug.WriteLine($"  TransportState: {latestMissionDetail.TransportState}");
                                Debug.WriteLine($"  PayloadStatus: {latestMissionDetail.PayloadStatus}");
                                Debug.WriteLine($"  AssignedTo: {latestMissionDetail.AssignedTo}");
                                Debug.WriteLine($"  ParametersJson: {latestMissionDetail.ParametersJson}");

                                // 현재 폴링 중인 미션이 완료되면, 다음 미션을 전송하거나 전체 프로세스 완료 처리
                                if (latestMissionDetail.NavigationState == (int)MissionStatusEnum.COMPLETED) // 4로 정의된 COMPLETED 사용
                                {
                                    // === 중단점: 개별 미션 완료 시점 ===
                                    Debug.WriteLine($"[RobotMissionService - BREAKPOINT] Mission {latestMissionDetail.MissionId} completed for Process {processInfo.ProcessId}. Proceeding to next step.");

                                    // string MissionId를 int로 파싱하여 할당
                                    if (int.TryParse(latestMissionDetail.MissionId, out int completedMissionId))
                                    {
                                        processInfo.LastCompletedMissionId = completedMissionId;
                                    }
                                    else
                                    {
                                        Debug.WriteLine($"[RobotMissionService] Warning: Could not parse MissionId '{latestMissionDetail.MissionId}' to int for LastCompletedMissionId.");
                                        processInfo.LastCompletedMissionId = null; // 파싱 실패 시 null
                                    }

                                    // 현재 완료된 미션 단계의 정의를 가져옵니다.
                                    var completedStepDefinition = processInfo.MissionSteps[processInfo.CurrentStepIndex];

                                    // PostMissionOperations 실행
                                    foreach (var subOperation in completedStepDefinition.PostMissionOperations)
                                    {
                                        if (processInfo.IsFailed) break; // ex: Modbus access 실패 시 Rack update는 안하는 경우
                                        processInfo.HmiStatus.SubOpDescription = subOperation.Description;
                                        OnMissionProcessUpdated?.Invoke(processInfo);
                                        await PerformSubOperation(subOperation, processInfo);
                                        processInfo.HmiStatus.SubOpDescription = "";
                                        OnMissionProcessUpdated?.Invoke(processInfo);
                                    }

                                    // 미션 단계 인덱스 증가
                                    processInfo.CurrentStepIndex++;
                                    OnShowAutoClosingMessage?.Invoke($"로봇 미션 단계 완료: {processInfo.HmiStatus.CurrentStepDescription}");

                                    Debug.WriteLine($"[RobotMissionService] Process {processInfo.ProcessId} - CurrentStepIndex incremented to: {processInfo.CurrentStepIndex}. Total steps: {processInfo.TotalSteps}.");

                                    if (processInfo.CurrentStepIndex >= processInfo.TotalSteps)
                                    {
                                        // 모든 미션 단계가 완료됨
                                        processInfo.IsFinished = true;
                                        await HandleRobotMissionCompletion(processInfo);
                                    }
                                    else
                                    {
                                        // 다음 단계로 진행 (다음 미션 전송)
                                        // LastSentMissionId를 null로 초기화하여 다음 틱에서 새로운 미션이 전송되도록 합니다.
                                        processInfo.LastSentMissionId = null;
                                        Debug.WriteLine($"[RobotMissionService] Process {processInfo.ProcessId}: Reset LastSentMissionId to null for next step.");
                                        // 다음 틱에서 SendAndTrackMissionStepsForProcess가 호출될 것입니다.
                                    }
                                }
                                else if (latestMissionDetail.NavigationState == (int)MissionStatusEnum.FAILED || latestMissionDetail.NavigationState == (int)MissionStatusEnum.REJECTED || latestMissionDetail.NavigationState == (int)MissionStatusEnum.CANCELLED)
                                {
                                    if (processInfo.LastRackDropMissionId.HasValue && processInfo.LastRackDropMissionId == processInfo.LastSentMissionId)
                                    {
                                        processInfo.LastRackDropMissionId = null;

                                        RackViewModel? destinationRackVm;
                                        List<int> racksToLock = new List<int>(); // No racks locked for simple supply missions initially
                                        if (processInfo.IsWarehouseMission)
                                        {
                                            destinationRackVm = await _mainViewModel.GetRackViewModelForWarehouseTemporary();    
                                        }
                                        else
                                        {
                                            destinationRackVm = await _mainViewModel.GetRackViewModelForInboundTemporary();    // 라인 입고 팔레트를 적치할 Rack
                                        }

                                        if (destinationRackVm == null)
                                        {
                                            _mainViewModel.WriteLog("\n[" + DateTimeOffset.Now.ToString() + $"] 창고 랙에 적치 중 에러 발생, 빈 랙이 없어서 로봇 추출함");
                                            ExtractVehicle(processInfo.IsWarehouseMission ? _warehouseAmrName : _packagingLineAmrName);
                                        }
                                        else
                                        {
                                            _mainViewModel.WriteLog("\n[" + DateTimeOffset.Now.ToString() + $"] 창고 랙에 적치 중 에러 발생, {destinationRackVm.Title}(으)로 이동 적치");

                                            var amrRackViewModel = _mainViewModel.RackList?.FirstOrDefault(r => r.Title.Equals("AMR"));
                                            await _databaseService.UpdateIsLockedAsync(destinationRackVm.Id, true);
                                            Application.Current.Dispatcher.Invoke(() => (_mainViewModel.RackList?.FirstOrDefault(r => r.Id == destinationRackVm.Id)).IsLocked = true);
                                            racksToLock.Add(destinationRackVm.Id);

                                            string shelf = $"{int.Parse(destinationRackVm.Title.Split('-')[0]):D2}_{destinationRackVm.Title.Split('-')[1]}";
                                            //string processType = $"다른 랙 {destinationRackVm.Title}에 제품 재 적치 작업";
                                            List<MissionStepDefinition> missionSteps = new List<MissionStepDefinition>();

                                            // 로봇 미션 단계 정의 (사용자 요청에 따라 4단계로 복원 및 IsLinkable, LinkedMission 조정)
                                            // Step 5 : Move, Unload
                                            if (destinationRackVm.LocationArea == 2 || destinationRackVm.LocationArea == 4) // 랙 2 ~ 8 번 1단 드롭 만 적용
                                            {
                                                missionSteps.Add(new MissionStepDefinition
                                                {
                                                    ProcessStepDescription = "팔레트 적재를 위한 이동 및 회전 2",
                                                    MissionType = "7",
                                                    FromNode = $"RACK_{shelf}_STEP1",
                                                    ToNode = $"RACK_{shelf}_STEP2",
                                                    Payload = processInfo.LastRackDropPayload,
                                                    Priority = 3,
                                                    IsLinkable = true,
                                                    LinkWaitTimeout = 3600
                                                });
                                            }

                                            if (processInfo.IsWarehouseMission)
                                            {
                                                missionSteps.Add(new MissionStepDefinition
                                                {
                                                    ProcessStepDescription = $"{destinationRackVm.Title}(으)로 이동 & 팔레트 드롭",
                                                    MissionType = "8",
                                                    ToNode = $"Rack_{shelf}_Drop",
                                                    Payload = processInfo.LastRackDropPayload,
                                                    Priority = 3,
                                                    IsLinkable = true,
                                                    LinkWaitTimeout = 3600,
                                                    PostMissionOperations = new List<MissionSubOperation> {
                                                    //new MissionSubOperation { Type = SubOperationType.CheckModbusDiscreteInput, Description = "팔레트 랙에 안착 여부 확인", McDiscreteInputAddress = _mainViewModel._checkModbusDescreteInputAddr },
                                                    new MissionSubOperation { Type = SubOperationType.DbUpdateRackState, Description = "랙 상태 업데이트", SourceRackIdForDbUpdate = amrRackViewModel.Id, DestRackIdForDbUpdate =destinationRackVm.Id }
                                                }
                                                });
                                                missionSteps.Add(new MissionStepDefinition
                                                {
                                                    ProcessStepDescription = "대기장소로 이동",
                                                    MissionType = "8",
                                                    ToNode = $"AMR1_WAIT",
                                                    Payload = processInfo.LastRackDropPayload,
                                                    Priority = 3,
                                                    IsLinkable = false,
                                                    LinkWaitTimeout = 3600
                                                });
                                            }
                                            else
                                            {
                                                missionSteps.Add(new MissionStepDefinition
                                                {
                                                    ProcessStepDescription = $"{destinationRackVm.Title}(으)로 이동 & 팔레트 드롭",
                                                    MissionType = "8",
                                                    ToNode = $"Rack_{shelf}_Drop",
                                                    Payload = processInfo.LastRackDropPayload,
                                                    Priority = 3,
                                                    IsLinkable = true,
                                                    LinkWaitTimeout = 3600,
                                                    PostMissionOperations = new List<MissionSubOperation> {
                                                    //new MissionSubOperation { Type = SubOperationType.CheckModbusDiscreteInput, Description = "팔레트 랙에 안착 여부 확인", McDiscreteInputAddress = _mainViewModel._checkModbusDescreteInputAddr },
                                                    new MissionSubOperation { Type = SubOperationType.DbWriteRackData, Description = "입고 팔레트 정보 업데이트", DestRackIdForDbUpdate = destinationRackVm.Id },
                                                    new MissionSubOperation { Type = SubOperationType.ClearLotInformation, Description = "Lot 정보 표시 지우기" }
                                                }
                                                });
                                                missionSteps.Add(new MissionStepDefinition
                                                {
                                                    ProcessStepDescription = "대기장소로 이동",
                                                    MissionType = "8",
                                                    ToNode = "Pallet_BWD_Pos",
                                                    Payload = processInfo.LastRackDropPayload,
                                                    Priority = 3,
                                                    IsLinkable = false,
                                                    LinkWaitTimeout = 3600
                                                });
                                            }

                                            // 로봇 미션 프로세스 시작
                                            string processId = await _mainViewModel.InitiateRobotMissionProcess(
                                                processInfo.ProcessType,
                                                missionSteps,
                                                racksToLock, // 잠긴 랙 ID 목록 전달
                                                null, // racksToProcess
                                                null, // initiatingCoilAddress
                                                processInfo.IsWarehouseMission
                                            );
                                        }
                                    }

                                    Debug.WriteLine($"[RobotMissionService] Mission {latestMissionDetail.MissionId} FAILED/REJECTED/CANCELLED. Process {processInfo.ProcessId} marked as FAILED.");
                                    processInfo.HmiStatus.Status = latestMissionDetail.NavigationState == (int)MissionStatusEnum.REJECTED ? MissionStatusEnum.REJECTED.ToString() :
                                                                       latestMissionDetail.NavigationState == (int)MissionStatusEnum.CANCELLED ? MissionStatusEnum.CANCELLED.ToString() :
                                                                       MissionStatusEnum.FAILED.ToString();
                                    processInfo.CurrentStatus = latestMissionDetail.NavigationState == (int)MissionStatusEnum.REJECTED ? MissionStatusEnum.REJECTED :
                                                                       latestMissionDetail.NavigationState == (int)MissionStatusEnum.CANCELLED ? MissionStatusEnum.CANCELLED :
                                                                       MissionStatusEnum.FAILED; // RobotMissionInfo 내부 상태 업데이트
                                    OnShowAutoClosingMessage?.Invoke($"로봇 미션 {processInfo.ProcessType} (ID: {processInfo.ProcessId}) 실패: {latestMissionDetail.MissionId}. 남은 미션 취소.");
                                    processInfo.IsFailed = true; // 프로세스를 실패로 마크
                                    await HandleRobotMissionCompletion(processInfo); // 실패 처리도 완료 처리로 간주

                                }
                            }
                            else
                            {
                                // 미션 정보를 찾을 수 없는 경우 (아직 생성되지 않았거나, 이미 너무 오래되어 조회 불가능)
                                // 재시도 횟수 증가
                                processInfo.PollingRetryCount++;
                                Debug.WriteLine($"[RobotMissionService] Mission {processInfo.LastSentMissionId} not found or no missions in payload. Process {processInfo.ProcessId}. Retry count: {processInfo.PollingRetryCount}/{RobotMissionInfo.MaxPollingRetries}.");

                                if (processInfo.PollingRetryCount >= RobotMissionInfo.MaxPollingRetries)
                                {
                                    Debug.WriteLine($"[RobotMissionService] Max retries reached for Process {processInfo.ProcessId}. Marking as FAILED.");
                                    processInfo.HmiStatus.Status = MissionStatusEnum.FAILED.ToString();
                                    processInfo.CurrentStatus = MissionStatusEnum.FAILED; // RobotMissionInfo 내부 상태 업데이트
                                    OnShowAutoClosingMessage?.Invoke($"로봇 미션 {processInfo.ProcessType} (ID: {processInfo.ProcessId}) 폴링 실패: 미션 {processInfo.LastSentMissionId.Value} 정보를 찾을 수 없습니다. (최대 재시도 횟수 초과)");
                                    processInfo.IsFailed = true;
                                    await HandleRobotMissionCompletion(processInfo);
                                }
                                // 재시도 횟수가 MaxPollingRetries 미만이면 다음 틱에서 다시 시도
                            }
                        }
                        catch (HttpRequestException httpEx)
                        {
                            processInfo.PollingRetryCount++;
                            Debug.WriteLine($"[RobotMissionService] HTTP Request Error for mission {processInfo.LastSentMissionId}: {httpEx.Message}. Process {processInfo.ProcessId}. Retry count: {processInfo.PollingRetryCount}/{RobotMissionInfo.MaxPollingRetries}.");

                            if (processInfo.PollingRetryCount >= RobotMissionInfo.MaxPollingRetries)
                            {
                                Debug.WriteLine($"[RobotMissionService] Max retries reached for Process {processInfo.ProcessId} due to HTTP error. Marking as FAILED.");
                                processInfo.HmiStatus.Status = MissionStatusEnum.FAILED.ToString();
                                processInfo.CurrentStatus = MissionStatusEnum.FAILED; // RobotMissionInfo 내부 상태 업데이트
                                OnShowAutoClosingMessage?.Invoke($"로봇 미션 상태 폴링 실패 (HTTP 오류, 최대 재시도 횟수 초과): {httpEx.Message.Substring(0, Math.Min(100, httpEx.Message.Length))}");
                                processInfo.IsFailed = true; // 폴링 실패도 프로세스 실패로 간주
                                await HandleRobotMissionCompletion(processInfo);
                            }
                        }
                        catch (Exception ex)
                        {
                            processInfo.PollingRetryCount++;
                            Debug.WriteLine($"[RobotMissionService] Error polling mission status for process {processInfo.ProcessId}: {ex.Message}. Retry count: {processInfo.PollingRetryCount}/{RobotMissionInfo.MaxPollingRetries}.");

                            if (processInfo.PollingRetryCount >= RobotMissionInfo.MaxPollingRetries)
                            {
                                Debug.WriteLine($"[RobotMissionService] Max retries reached for Process {processInfo.ProcessId} due to unexpected error. Marking as FAILED.");
                                processInfo.HmiStatus.Status = MissionStatusEnum.FAILED.ToString();
                                processInfo.CurrentStatus = MissionStatusEnum.FAILED; // RobotMissionInfo 내부 상태 업데이트
                                OnShowAutoClosingMessage?.Invoke($"로봇 미션 상태 폴링 중 예상치 못한 오류 (최대 재시도 횟수 초과): {ex.Message.Substring(0, Math.Min(100, ex.Message.Length))}");
                                processInfo.IsFailed = true; // 예상치 못한 오류도 프로세스 실패로 간주
                                await HandleRobotMissionCompletion(processInfo);
                            }
                        }
                    }
                    else // LastSentMissionId가 null인 경우 (첫 번째 미션이 아직 전송되지 않았거나, 이전 미션이 완료되어 다음 미션을 기다리는 경우)
                    {
                        if (processInfo.CurrentStepIndex < processInfo.TotalSteps && !processInfo.IsFailed)
                        {
                            Debug.WriteLine($"[RobotMissionService] Process {processInfo.ProcessId} - Sending next mission step. Current Step: {processInfo.CurrentStepIndex + 1}/{processInfo.TotalSteps}.");
                            // 여기서는 await를 사용하여 미션 전송이 완료될 때까지 기다리도록 합니다.
                            // 이렇게 하면 다음 타이머 틱이 발생하기 전에 미션 전송이 완료되어 LastSentMissionId가 업데이트됩니다.
                            await SendAndTrackMissionStepsForProcess(processInfo);
                        }
                    }
                }
            }
            finally
            {
                _isPollingInProgress = false; // 폴링 완료 후 플래그 리셋
            }
        }

        /// <summary>
        /// 새로운 로봇 미션 프로세스를 시작합니다.
        /// </summary>
        /// <param name="processType">미션 프로세스의 유형 (예: "WaitToWrapTransfer", "RackTransfer").</param>
        /// <param name="missionSteps">이 프로세스를 구성하는 순차적인 미션 단계 목록.</param>
        /// <param name="racksLockedAtStart">이 프로세스 시작 시 잠긴 모든 랙의 ID 목록.</param>
        /// <param name="racksToProcess">여러 랙을 처리할 경우 (예: 출고) 해당 랙들의 ViewModel 목록.</param>
        /// <param name="initiatingCoilAddress">이 미션을 시작한 Modbus Coil의 주소 (경광등 제어용).</param>
        /// <param name="isWarehouseMission">이 미션이 창고 관련 미션인지 여부 (true: 창고, false: 포장실).</param>
        /// <returns>시작된 미션 프로세스의 고유 ID.</returns>
        public async Task<string> InitiateRobotMissionProcess(
            string processType,
            List<MissionStepDefinition> missionSteps,
            List<int> racksLockedAtStart, // 새로 추가된 파라미터
            List<RackViewModel> racksToProcess = null, // 새로 추가된 파라미터
            ushort? initiatingCoilAddress = null, // 새로운 파라미터 추가
            bool isWarehouseMission = false, // isWarehouseMission 파라미터 추가
            string readStringValue = null,
            ushort? readIntValue = null
        )
        {
            // 새로운 미션이 시작될 때 Modbus 오류 플래그를 리셋합니다.
            _hasMissionCriticalModbusErrorBeenDisplayed = false;
            _modbusErrorMessageSuppressionTimer.Stop(); // 타이머도 중지하여 다음 오류 메시지 표시를 허용
            string processId = Guid.NewGuid().ToString(); // 고유한 프로세스 ID 생성
            var newMissionProcess = new RobotMissionInfo(processId, processType, missionSteps, racksLockedAtStart, initiatingCoilAddress, isWarehouseMission, readStringValue, readIntValue)
            {
                RacksToProcess = racksToProcess ?? new List<RackViewModel>(), // racksToProcess 설정
            };

            // ConcurrentDictionary는 TryAdd를 사용
            if (!_activeRobotProcesses.TryAdd(processId, newMissionProcess))
            {
                Debug.WriteLine($"[RobotMissionService] Failed to add process {processId} to active processes (already exists).");
                OnShowAutoClosingMessage?.Invoke($"로봇 미션 프로세스 시작 실패: {processType} (ID: {processId}) - 이미 존재");
                return null;
            }
            Debug.WriteLine($"[RobotMissionService] Initiated new robot mission process: {processId} ({processType}). Total steps: {missionSteps.Count}");
            OnShowAutoClosingMessage?.Invoke($"로봇 미션 프로세스 시작: {processType} (ID: {processId})");

            // 첫 번째 미션 단계를 전송하고 추적을 시작합니다.
            await SendAndTrackMissionStepsForProcess(newMissionProcess);
            if (initiatingCoilAddress.HasValue)
                _mainViewModel.WriteLog("\n[" + DateTimeOffset.Now.ToString() + $"] 포장실 미션 started by call button {initiatingCoilAddress + 1}.");

            return processId;
        }

        /// <summary>
        /// 주어진 미션 프로세스의 현재 단계를 ANT 서버에 전송하고 추적을 시작합니다.
        /// </summary>
        /// <param name="processInfo">현재 미션 프로세스 정보.</param>
        /// <returns>비동기 작업.</returns>
        private async Task SendAndTrackMissionStepsForProcess(RobotMissionInfo processInfo)
        {
            if (processInfo.CancellationTokenSource.Token.IsCancellationRequested)
            {
                Debug.WriteLine($"[RobotMissionService] Process {processInfo.ProcessId} cancellation requested before sending mission step.");
                processInfo.IsFailed = true; // 취소된 것으로 마크
                processInfo.CurrentStatus = MissionStatusEnum.CANCELLED;
                processInfo.HmiStatus.Status = MissionStatusEnum.CANCELLED.ToString();
                processInfo.HmiStatus.CurrentStepDescription = "미션 취소됨";
                await HandleRobotMissionCompletion(processInfo);
                return;
            }

            if (processInfo.CurrentStepIndex >= processInfo.TotalSteps)
            {
                Debug.WriteLine($"[RobotMissionService] All steps completed for Process {processInfo.ProcessId}. Handled in PollingTick.");
                processInfo.IsFinished = true; // 모든 단계가 완료되었음을 표시
                return;
            }

            var currentStep = processInfo.MissionSteps[processInfo.CurrentStepIndex];
            int? linkedMissionId = null;

            // "false 다음의 true인 mission 요청 시 LinkedMission을 null로 해달라."는 요청에 따라 로직 수정
            // 즉, 이전 단계가 IsLinkable=false 였다면, 현재 단계는 LinkedMission을 null로 설정
            if (processInfo.CurrentStepIndex > 0)
            {
                var previousStepDefinition = processInfo.MissionSteps[processInfo.CurrentStepIndex - 1];
                if (previousStepDefinition.IsLinkable) // 이전 단계가 연결 가능했다면
                {
                    linkedMissionId = processInfo.LastCompletedMissionId; // 이전에 완료된 미션 ID를 연결
                }
                // else: previousStepDefinition.IsLinkable이 false인 경우 linkedMissionId는 기본값인 null 유지
            }
            // 첫 번째 단계 (CurrentStepIndex == 0)는 항상 linkedMissionId가 null입니다.

            // PreMissionOperations 실행
            // 이 로직은 Modbus Discrete Input 체크보다 먼저 수행되어야 할 수도 있습니다.
            // 미션 정의에 따라 순서를 조정합니다.
            foreach (var subOperation in currentStep.PreMissionOperations)
            {
                processInfo.HmiStatus.SubOpDescription = subOperation.Description;
                OnMissionProcessUpdated?.Invoke(processInfo);
                await PerformSubOperation(subOperation, processInfo);
                processInfo.HmiStatus.SubOpDescription = "";
                OnMissionProcessUpdated?.Invoke(processInfo);
                // PerformSubOperation 내부에서 예외 발생 시 해당 미션은 이미 실패 처리되므로, 추가적인 예외 처리는 필요 없습니다.
                if (processInfo.IsFailed || processInfo.CancellationTokenSource.Token.IsCancellationRequested)
                {
                    Debug.WriteLine($"[RobotMissionService] PreMissionOperations failed or cancelled for Process {processInfo.ProcessId}. Aborting mission send.");
                    return;
                }
            }

            // AntApiModels.MissionRequest 및 AntApiModels.MissionRequestPayload 사용
            var missionRequest = new MissionRequest
            {
                RequestPayload = new MissionRequestPayload
                {
                    Requestor = "admin",
                    MissionType = currentStep.MissionType,
                    FromNode = currentStep.FromNode,
                    ToNode = currentStep.ToNode,
                    Cardinality = 1, // 기본값
                    Priority = (currentStep.Priority == 3) ? 3 : (currentStep.Priority == 2) ? 2 : 1,
                    Deadline = DateTime.UtcNow.AddHours(1).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    DispatchTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    Parameters = new MissionRequestParameters
                    {
                        Value = new MissionRequestParameterValue
                        {
                            Payload = currentStep.Payload,
                            IsLinkable = currentStep.IsLinkable, // 이 미션 자체가 다음 미션과 연결될 수 있는지 여부
                            LinkWaitTimeout = currentStep.LinkWaitTimeout,
                            LinkedMission = linkedMissionId // 이 미션이 이전 미션과 연결되는지 여부
                        },
                        Description = "Mission extension", // 기본값 설정
                        Type = "org.json.JSONObject",      // 기본값 설정
                        Name = "parameters"                 // 기본값 설정
                    }
                }
            };

            while(true)
            {
                try
                {
                    // === 중단점: 미션 요청 엔드포인트 및 페이로드 ===
                    string requestEndpoint = $"wms/rest/v{_httpService.CurrentApiVersionMajor}.{_httpService.CurrentApiVersionMinor}/missions";
                    string requestPayloadJson = JsonConvert.SerializeObject(missionRequest, Formatting.Indented); // 가독성을 위해 Indented 포맷 사용
                    Debug.WriteLine($"[RobotMissionService - BREAKPOINT] Sending Mission Request for Process {processInfo.ProcessId}, Step {processInfo.CurrentStepIndex + 1}:");
                    Debug.WriteLine($"  Endpoint: {requestEndpoint}");
                    Debug.WriteLine($"  Payload: {requestPayloadJson}");

                    // AntApiModels.MissionRequest, AntApiModels.MissionResponse 사용
                    var missionResponse = await _httpService.PostAsync<MissionRequest, MissionResponse>(requestEndpoint, missionRequest, processInfo.CancellationTokenSource.Token).ConfigureAwait(false);

                    if (missionResponse?.ReturnCode == 0 && missionResponse.Payload?.AcceptedMissions != null && missionResponse.Payload.AcceptedMissions.Any())
                    {
                        int acceptedMissionId = missionResponse.Payload.AcceptedMissions.First();
                        processInfo.LastSentMissionId = acceptedMissionId; // 전송된 미션 ID 저장
                        // CurrentStepIndex는 이 미션이 완료될 때 (폴링 로직에서) 증가시킴

                        if (currentStep.ToNode.Contains("Rack_") && currentStep.ToNode.Contains("_Drop"))
                        {
                            processInfo.LastRackDropMissionId = processInfo.LastSentMissionId;
                            processInfo.LastRackDropPayload = currentStep.Payload;
                        }
                        else
                        {
                            processInfo.LastRackDropMissionId = null;
                        }

                        processInfo.HmiStatus.Status = MissionStatusEnum.ACCEPTED.ToString();
                        processInfo.CurrentStatus = MissionStatusEnum.ACCEPTED; // RobotMissionInfo 내부 상태 업데이트
                        processInfo.HmiStatus.CurrentStepDescription = currentStep.ProcessStepDescription;
                        // 현재 전송된 단계까지의 진행률 (CurrentStepIndex는 다음 보낼 인덱스이므로 +1)
                        //processInfo.HmiStatus.ProgressPercentage = (int)(((double)(processInfo.CurrentStepIndex + 1) / processInfo.TotalSteps) * 100);
                        OnShowAutoClosingMessage?.Invoke($"로봇 미션 단계 전송 성공: {currentStep.ProcessStepDescription} (미션 ID: {acceptedMissionId})");

                        // 미션 프로세스 업데이트 이벤트 발생
                        OnMissionProcessUpdated?.Invoke(processInfo);

                        Debug.WriteLine($"[RobotMissionService] Process {processInfo.ProcessId}: Mission {acceptedMissionId} for step {processInfo.CurrentStepIndex + 1}/{processInfo.TotalSteps} sent successfully.");
                        break;
                    }
                    else
                    {
                        processInfo.HmiStatus.Status = MissionStatusEnum.FAILED.ToString();
                        processInfo.CurrentStatus = MissionStatusEnum.FAILED; // RobotMissionInfo 내부 상태 업데이트
                        OnShowAutoClosingMessage?.Invoke($"로봇 미션 단계 전송 실패: {currentStep.ProcessStepDescription}");
                        Debug.WriteLine($"[RobotMissionService] Process {processInfo.ProcessId}: Mission step {currentStep.ProcessStepDescription} failed to be accepted. Return Code: {missionResponse?.ReturnCode}. " +
                                        $"Rejected: {string.Join(",", missionResponse?.Payload?.RejectedMissions ?? new List<int>())}");
                        processInfo.IsFailed = true; // 프로세스 실패로 마크
                        await HandleRobotMissionCompletion(processInfo); // 실패 처리
                        break;
                    }

                }
                catch (OperationCanceledException) // 취소 예외 처리
                {
                    Debug.WriteLine($"[RobotMissionService] Process {processInfo.ProcessId} cancelled during mission step send.");
                    processInfo.CurrentStatus = MissionStatusEnum.CANCELLED;
                    processInfo.HmiStatus.Status = MissionStatusEnum.CANCELLED.ToString();
                    //processInfo.HmiStatus.ProgressPercentage = (int)(((double)(processInfo.CurrentStepIndex + 1) / processInfo.TotalSteps) * 100);
                    OnShowAutoClosingMessage?.Invoke($"로봇 미션 프로세스 취소됨: {currentStep.ProcessStepDescription}");
                    processInfo.IsFailed = true; // 취소도 실패로 간주하여 처리
                    await HandleRobotMissionCompletion(processInfo);
                }
                catch (HttpRequestException httpEx)
                {
                    var result = MessageBox.Show($"로봇 미션 단계 전송 HTTP 오류: {httpEx.Message.Substring(0, Math.Min(100, httpEx.Message.Length))}\n계속 시도하시겠습니까?",
                        "미션생성 오류", MessageBoxButton.YesNo, MessageBoxImage.Error, MessageBoxResult.Yes, MessageBoxOptions.DefaultDesktopOnly);
                    if (result == MessageBoxResult.No)
                    {
                        processInfo.HmiStatus.Status = MissionStatusEnum.FAILED.ToString();
                        processInfo.CurrentStatus = MissionStatusEnum.FAILED; // RobotMissionInfo 내부 상태 업데이트
                        OnShowAutoClosingMessage?.Invoke($"로봇 미션 단계 전송 HTTP 오류: {httpEx.Message.Substring(0, Math.Min(100, httpEx.Message.Length))}");
                        Debug.WriteLine($"[RobotMissionService] Process {processInfo.ProcessId}: HTTP Request Error sending mission step {currentStep.ProcessStepDescription}: {httpEx.Message}");
                        processInfo.IsFailed = true; // 프로세스 실패로 간주
                        await HandleRobotMissionCompletion(processInfo); // 실패 처리
                        break;
                    }
                }
                catch (Exception ex)
                {
                    processInfo.HmiStatus.Status = MissionStatusEnum.FAILED.ToString();
                    processInfo.CurrentStatus = MissionStatusEnum.FAILED; // RobotMissionInfo 내부 상태 업데이트
                    OnShowAutoClosingMessage?.Invoke($"로봇 미션 단계 전송 중 예상치 못한 오류: {ex.Message.Substring(0, Math.Min(100, ex.Message.Length))}");
                    Debug.WriteLine($"[RobotMissionService] Process {processInfo.ProcessId}: Unexpected Error sending mission step {currentStep.ProcessStepDescription}: {ex.Message}");
                    processInfo.IsFailed = true; // 예상치 못한 오류도 프로세스 실패로 간주
                    await HandleRobotMissionCompletion(processInfo); // 실패 처리
                }
            }
        }

        /// <summary>
        /// 특정 로봇 미션 프로세스의 HMI 상태를 업데이트합니다.
        /// </summary>
        /// <param name="processId">업데이트할 미션 프로세스의 고유 ID.</param>
        /// <param name="status">새로운 상태 문자열.</param>
        /// <param name="progressPercentage">새로운 진행률 (0-100).</param>
        /// <param name="currentStepDescription">현재 단계에 대한 설명.</param>
        public Task UpdateHmiStatus(string processId, string status, int progressPercentage, string currentStepDescription, string subOpDescription)
        {
            RobotMissionInfo processInfo;
            if (_activeRobotProcesses.TryGetValue(processId, out processInfo))
            {
                processInfo.HmiStatus.Status = status;
                processInfo.HmiStatus.ProgressPercentage = progressPercentage;
                processInfo.HmiStatus.CurrentStepDescription = currentStepDescription;
                processInfo.HmiStatus.SubOpDescription = subOpDescription;
                OnMissionProcessUpdated?.Invoke(processInfo);
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// 특정 로봇 미션 프로세스의 단계를 완료 또는 실패로 표시합니다.
        /// </summary>
        /// <param name="processId">미션 프로세스의 고유 ID.</param>
        /// <param name="stepIndex">완료된 단계의 인덱스.</param>
        /// <param name="status">단계의 최종 상태 (COMPLETED, FAILED 등).</param>
        /// <param name="message">관련 메시지 (선택 사항).</param>
        public Task CompleteMissionStep(string processId, int stepIndex, MissionStatusEnum status, string message = null)
        {
            RobotMissionInfo processInfo;
            if (_activeRobotProcesses.TryGetValue(processId, out processInfo))
            {
                // 이 메서드는 외부에서 강제로 단계를 완료/실패 처리할 때 사용될 수 있습니다.
                // 현재 폴링 로직에서 대부분의 상태 변경을 처리하므로, 필요에 따라 구현을 조정합니다.
                Debug.WriteLine($"[RobotMissionService] External CompleteMissionStep called for Process {processId}, Step {stepIndex} with status {status}. Message: {message}");
                processInfo.CurrentStepIndex = stepIndex; // 강제로 단계 인덱스 설정
                processInfo.HmiStatus.Status = status.ToString();
                processInfo.HmiStatus.CurrentStepDescription = message ?? $"단계 {stepIndex} 완료/실패";
                //processInfo.HmiStatus.ProgressPercentage = (int)(((double)(stepIndex + 1) / processInfo.TotalSteps) * 100);
                processInfo.CurrentStatus = status; // RobotMissionInfo 내부 상태 업데이트

                if (status == MissionStatusEnum.COMPLETED && stepIndex >= processInfo.TotalSteps - 1) // 마지막 단계 완료
                {
                    processInfo.IsFinished = true;
                    // HandleRobotMissionCompletion(processInfo); // 여기서 호출하면 중복될 수 있으므로 주의
                }
                else if (status == MissionStatusEnum.FAILED || status == MissionStatusEnum.CANCELLED || status == MissionStatusEnum.REJECTED)
                {
                    processInfo.IsFailed = true;
                    // HandleRobotMissionCompletion(processInfo); // 여기서 호출하면 중복될 수 있으므로 주의
                }
                OnMissionProcessUpdated?.Invoke(processInfo); // 외부 호출에 의한 상태 변경도 팝업에 알림
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// 현재 STARTED 상태이면서 창고 미션인 첫 번째 미션 프로세스를 반환합니다.
        /// (동시에 하나의 미션만 실행된다는 가정 하에 유효)
        /// </summary>
        /// <returns>STARTED 상태의 창고 미션 RobotMissionInfo 객체 또는 null.</returns>
        public RobotMissionInfo? GetFirstStartedWarehouseMission()
        {
            return _activeRobotProcesses.Values.FirstOrDefault(
                p => p.CurrentStatus == MissionStatusEnum.STARTED && p.IsWarehouseMission
            );
        }

        public RobotMissionInfo? GetFirstWaitingWarehouseMission()
        {
            return _activeRobotProcesses.Values.FirstOrDefault(
                p => p.CurrentStatus == MissionStatusEnum.ACCEPTED && p.IsWarehouseMission
            );
        }

        /// <summary>
        /// 주어진 랙 ID 목록에 대해 잠금을 해제합니다.
        /// </summary>
        /// <param name="rackIds">잠금을 해제할 랙 ID 목록.</param>
        private async Task UnlockAllRacksInProcess(List<int> rackIds)
        {
            foreach (var rackId in rackIds.Distinct()) // 중복 ID 제거
            {
                try
                {
                    await _databaseService.UpdateIsLockedAsync(rackId, false);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        OnRackLockStateChanged?.Invoke(rackId, false); // MainViewModel에 잠금 해제 알림
                    });
                    Debug.WriteLine($"[RobotMissionService] Rack {rackId} unlocked.");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[RobotMissionService] Failed to unlock rack {rackId}: {ex.Message}");
                    OnShowAutoClosingMessage?.Invoke($"랙 {rackId} 잠금 해제 실패: {ex.Message.Substring(0, Math.Min(100, ex.Message.Length))}");
                }
            }
        }

        private async Task SetVisibleRackInProcess(int rackId, bool newVisible)
        {
            try
            {
                await _databaseService.UpdateIsVisibleAsync(rackId, newVisible);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    OnRackLockStateChanged?.Invoke(rackId, false); // MainViewModel에 잠금 해제 알림
                });
            }
            catch (Exception ex)
            {
                OnShowAutoClosingMessage?.Invoke($"랙 {rackId} visible 설정 실패: {ex.Message.Substring(0, Math.Min(100, ex.Message.Length))}");
            }
        }

        /// <summary>
        /// 로봇 미션이 최종적으로 완료(성공 또는 실패)되었을 때 데이터베이스 및 UI를 업데이트합니다.
        /// 이 메서드는 RobotMissionService의 폴링 로직에서 호출되어야 합니다.
        /// </summary>
        /// <param name="missionInfo">완료된 미션 프로세스 정보.</param>
        private async Task HandleRobotMissionCompletion(RobotMissionInfo missionInfo)
        {
            // === 중단점: 로봇 미션 프로세스 최종 완료/실패 시점 ===
            Debug.WriteLine($"[RobotMissionService - BREAKPOINT] Handling completion for process {missionInfo.ProcessId}. Final Status: {missionInfo.HmiStatus.Status}. IsFailed: {missionInfo.IsFailed}");

            try
            {
                if (missionInfo.IsFinished) // 프로세스 성공 시 (모든 개별 IsLinkable=false 단계가 이미 처리됨)
                {
                    OnShowAutoClosingMessage?.Invoke($"로봇 미션 프로세스 성공적으로 완료: {missionInfo.ProcessType} (ID: {missionInfo.ProcessId})");
                    // 모든 랙 잠금 해제 (전체 프로세스 시작 시 잠긴 모든 랙)
                    await UnlockAllRacksInProcess(missionInfo.RacksLockedByProcess);
                }
                else if (missionInfo.IsFailed) // 프로세스 실패 시
                {
                    OnShowAutoClosingMessage?.Invoke($"로봇 미션 프로세스 실패! 관련된 모든 랙 잠금 해제 중...");
                    await UnlockAllRacksInProcess(missionInfo.RacksLockedByProcess);

                    // Extract vehicle
                    /*var payload = new ExtractVehicleRequest
                    {
                        Command = new ExtractVehicleCommand
                        {
                            Name = "extract",
                            Args = new { } // 빈 객체
                        }
                    };
                    string vehicleName = missionInfo.IsWarehouseMission ? _warehouseAmrName : _packagingLineAmrName;
                    string requestEndpoint = $"wms/rest/v{_httpService.CurrentApiVersionMajor}.{_httpService.CurrentApiVersionMinor}/vehicles/{vehicleName}/command";
                    //string requestPayloadJson = JsonConvert.SerializeObject(payload, Formatting.Indented);

                    OnShowAutoClosingMessage?.Invoke($"로봇 미션 프로세스 실패! Extracting {vehicleName} ...");
                    //_httpService.PostAsync<ExtractVehicleRequest>(requestEndpoint, payload);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _httpService.PostAsync<ExtractVehicleRequest>(requestEndpoint, payload);
                        }
                        catch (Exception ex)
                        {
                            // 예외 로깅 가능
                            Debug.WriteLine(ex.Message);
                        }
                    });
                    await Application.Current.Dispatcher.Invoke(async () =>
                    {
                        MessageBox.Show(Application.Current.MainWindow, $"AMR {vehicleName}(이)가 추출(Extraction)되었습니다.\r\nAMR 운용 담당자의 후속 조치가 필요합니다.", "AMR 추출", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });*/
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RobotMissionService] Error in HandleRobotMissionCompletion: {ex.Message}");
                OnShowAutoClosingMessage?.Invoke($"로봇 미션 최종 처리 중 오류 발생: {ex.Message.Substring(0, Math.Min(100, ex.Message.Length))}");
            }
            finally
            {
                // 미션이 시작된 콜 버튼의 경광등을 끕니다. (MainViewModel에게 이벤트로 요청)
                if (missionInfo.InitiatingCoilAddress.HasValue)
                {
                    OnTurnOffAlarmLightRequest?.Invoke(missionInfo.InitiatingCoilAddress.Value);
                    Debug.WriteLine($"[RobotMissionService] Requested MainViewModel to turn OFF Alarm Light Coil {missionInfo.InitiatingCoilAddress.Value}.");
                }

                // 프로세스가 최종적으로 완료되거나 실패하면, activeRobotProcesses에서 제거
                RobotMissionInfo removed;
                if (_activeRobotProcesses.TryRemove(missionInfo.ProcessId, out removed))
                {
                    Debug.WriteLine($"[RobotMissionService] Process {missionInfo.ProcessId} explicitly removed from active processes.");
                }
                // 최종 상태 업데이트를 팝업에 알림 (팝업이 닫히지 않고 최종 상태를 표시하도록)
                OnMissionProcessUpdated?.Invoke(missionInfo);
            }
        }

        /// <summary>
        /// 미션 단계 내의 개별 서브 동작을 수행합니다.
        /// </summary>
        /// <param name="subOp">수행할 서브 동작 정의.</param>
        /// <param name="processInfo">현재 미션 프로세스 정보.</param>
        /// <returns>비동기 작업.</returns>
        private async Task PerformSubOperation(MissionSubOperation subOp, RobotMissionInfo processInfo)
        {
            Debug.WriteLine($"[RobotMissionService] Performing Sub Operation: {subOp.Type} - {subOp.Description}");

            // MC Protocol IP 주소가 지정된 경우, 해당 IP로 연결 시도. 아니면 서비스의 기본 IP 사용.
            string mcIpAddress = subOp.McProtocolIpAddress ?? _mcProtocolService.ConnectedIpAddress;

            try
            {
                switch (subOp.Type)
                {
                    case SubOperationType.McReadLotNoBoxCount:
                        if (!_mcProtocolInterface) break;
                        if (subOp.McProtocolIpAddress == null || !subOp.McWordAddress.HasValue || !subOp.McStringLengthWords.HasValue)
                        {
                            throw new ArgumentException("McReadLotNoBoxCount: IpAddress, WordAddress 또는 StringLengthWords가 지정되지 않았습니다.");
                        }

                        // ConnectAsync는 내부적으로 재연결 로직을 포함하고 있으므로 여기서 직접 Connect/Disconnect를 관리하지 않아도 됩니다.
                        string? bulletType = await _mcProtocolService.ReadStringDataAsync(subOp.McProtocolIpAddress, subOp.WordDeviceCode, subOp.McWordAddress.Value + 0, 4);//.ConfigureAwait(false); // 4 words
                        string? lotNoPart1 = await _mcProtocolService.ReadStringDataAsync(subOp.McProtocolIpAddress, subOp.WordDeviceCode, subOp.McWordAddress.Value + 4, 6);//.ConfigureAwait(false); // 6 words
                        ushort? lotNoPart2 = await _mcProtocolService.ReadWordAsync(subOp.McProtocolIpAddress, subOp.WordDeviceCode, subOp.McWordAddress.Value + 10);//.ConfigureAwait(false);
                        ushort? boxCount = await _mcProtocolService.ReadWordAsync(subOp.McProtocolIpAddress, subOp.WordDeviceCode, subOp.McWordAddress.Value + 11);//.ConfigureAwait(false);

                        if (bulletType == null || lotNoPart1 == null || lotNoPart2 == null || boxCount == null)
                        {
                            // 이 시점에서 미션 프로세스를 실패 상태로 마크하고 완료 처리
                            processInfo.CurrentStatus = MissionStatusEnum.FAILED;
                            processInfo.HmiStatus.Status = "FAILED";
                            //processInfo.HmiStatus.ProgressPercentage = 100;
                            processInfo.IsFailed = true; // 프로세스 실패 플래그 설정
                            processInfo.CancellationTokenSource.Cancel(); // 미션 취소 요청
                            await HandleRobotMissionCompletion(processInfo);
                            break;
                        }

                        processInfo.ReadBulletTypeValue = $"{bulletType}";
                        if (lotNoPart1.Length > 1 && lotNoPart1[0] == 'P') // PSD... 의 경우 lot number는 3자리
                            processInfo.ReadStringValue = $"{lotNoPart1.Trim()}-{lotNoPart2:D3}";
                        else
                            processInfo.ReadStringValue = $"{lotNoPart1.Trim()}-{lotNoPart2:D4}"; // "5-3"에 따라 LotNo 조합
                        processInfo.ReadIntValue = boxCount; // Box Count 저장

                        Debug.WriteLine($"[RobotMissionService] McReadLotNoBoxCount: Addr {subOp.McWordAddress.Value}, BulletType '{bulletType}', LotNo '{processInfo.ReadStringValue}', BoxCount {processInfo.ReadIntValue}");
                        _mainViewModel.WriteLog("\n[" + DateTimeOffset.Now.ToString() + $"] BulletType = '{bulletType}', Lot No. = '{processInfo.ReadStringValue}', Box Count = {boxCount}");
                        _mainViewModel.LotInfoViewModel.Message = $"Lot Info. : '{bulletType}' / '{processInfo.ReadStringValue}' / {boxCount}";
                        processInfo.HmiStatus.SubOpDescription = "";
                        break;

                    case SubOperationType.McReadSingleWord:
                        if (!_mcProtocolInterface) break;
                        if (subOp.McProtocolIpAddress == null || !subOp.McWordAddress.HasValue)
                        {
                            throw new ArgumentException("McReadSingleWord: IpAddress, WordAddress가 지정되지 않았습니다.");
                        }
                        ushort? readWord = await _mcProtocolService.ReadWordAsync(subOp.McProtocolIpAddress, subOp.WordDeviceCode, subOp.McWordAddress.Value).ConfigureAwait(false);
                        processInfo.ReadIntValue = readWord;
                        Debug.WriteLine($"[RobotMissionService] McReadSingleWord: Device '{subOp.WordDeviceCode}', Address {subOp.McWordAddress.Value}, Value {readWord}");
                        processInfo.HmiStatus.SubOpDescription = "";
                        break;

                    case SubOperationType.McWriteLotNoBoxCount:
                        if (!_mcProtocolInterface) break;
                        if (subOp.McProtocolIpAddress == null || !subOp.McWordAddress.HasValue || !subOp.McStringLengthWords.HasValue || string.IsNullOrEmpty(processInfo.ReadStringValue) || !processInfo.ReadIntValue.HasValue)
                        {
                            throw new ArgumentException("McWriteLotNoBoxCount: 필요한 파라미터 (IpAddress, WordAddress, StringLengthWords, ReadStringValue, ReadIntValue)가 충분하지 않습니다.");
                        }

                        string[] lotNoParts = processInfo.ReadStringValue.Split('-');
                        if (lotNoParts.Length != 2)
                        {
                            throw new FormatException($"LotNo 형식 오류: {processInfo.ReadStringValue}. '부분1-부분2' 형식이어야 합니다.");
                        }
                        string writeLotNoPart1 = lotNoParts[0];
                        ushort writeLotNoPart2 = ushort.Parse(lotNoParts[1]);

                        await _mcProtocolService.WriteStringDataAsync(subOp.McProtocolIpAddress, subOp.WordDeviceCode, subOp.McWordAddress.Value + 0, processInfo.ReadBulletTypeValue, 4);//.ConfigureAwait(false);
                        await _mcProtocolService.WriteStringDataAsync(subOp.McProtocolIpAddress, subOp.WordDeviceCode, subOp.McWordAddress.Value + 4, writeLotNoPart1, 6);//.ConfigureAwait(false);
                        await _mcProtocolService.WriteWordAsync(subOp.McProtocolIpAddress, subOp.WordDeviceCode, subOp.McWordAddress.Value + 10, writeLotNoPart2);//.ConfigureAwait(false);
                        await _mcProtocolService.WriteWordAsync(subOp.McProtocolIpAddress, subOp.WordDeviceCode, subOp.McWordAddress.Value + 11, processInfo.ReadIntValue.Value);//.ConfigureAwait(false);

                        Debug.WriteLine($"[RobotMissionService] McWriteLotNoBoxCount: LotNo '{processInfo.ReadStringValue}', BoxCount {processInfo.ReadIntValue.Value} Written.");
                        processInfo.HmiStatus.SubOpDescription = "";
                        break;

                    case SubOperationType.McWriteSingleWord:
                        if (!_mcProtocolInterface) break;
                        if (subOp.McProtocolIpAddress == null || !subOp.McWordAddress.HasValue || !subOp.McWriteValueInt.HasValue)
                        {
                            throw new ArgumentException("McWriteSingleWord: IpAddress, WordAddress 또는 WriteValueInt가 지정되지 않았습니다.");
                        }
                        await _mcProtocolService.WriteWordAsync(subOp.McProtocolIpAddress, subOp.WordDeviceCode, subOp.McWordAddress.Value, (ushort)subOp.McWriteValueInt.Value);//.ConfigureAwait(false);
                        Debug.WriteLine($"[RobotMissionService] McWriteSingleWord: Device '{subOp.WordDeviceCode}', Address {subOp.McWordAddress.Value}, Value {(ushort)subOp.McWriteValueInt.Value}");
                        processInfo.HmiStatus.SubOpDescription = "";
                        break;

                    case SubOperationType.McWaitAvailable:
                        if (!_mcProtocolInterface) break;
                        if (subOp.McProtocolIpAddress == null || !subOp.McWordAddress.HasValue || !subOp.McWateValueInt.HasValue)
                        {
                            throw new ArgumentException("McWaitSensorOff/On: IpAddress, WordAddress 또는 WriteValueInt가 지정되지 않았습니다.");
                        }
                        if (!subOp.WaitTimeoutSeconds.HasValue || subOp.WaitTimeoutSeconds.Value <= 0)
                        {
                            subOp.WaitTimeoutSeconds = 60; // 기본 타임아웃 60초
                        }
                        DateTime startTime_A = DateTime.Now;
                        while (!processInfo.CancellationTokenSource.Token.IsCancellationRequested)
                        {
                            ushort? currentState = await _mcProtocolService.ReadWordAsync(subOp.McProtocolIpAddress, subOp.WordDeviceCode, subOp.McWordAddress.Value); //.ConfigureAwait(false);
                            if (currentState == null) // mc protocol communication error
                            {
                                // 이 시점에서 미션 프로세스를 실패 상태로 마크하고 완료 처리
                                //processInfo.CurrentStatus = MissionStatusEnum.FAILED;
                                //processInfo.HmiStatus.Status = "FAILED";
                                //processInfo.HmiStatus.ProgressPercentage = 100;
                                //processInfo.IsFailed = true; // 프로세스 실패 플래그 설정
                                //processInfo.CancellationTokenSource.Cancel(); // 미션 취소 요청
                                //await HandleRobotMissionCompletion(processInfo);
                                //break;
                            }
                            if (currentState == subOp.McWateValueInt)
                            {
                                processInfo.HmiStatus.SubOpDescription = "";
                                break;
                            }
                            if (DateTime.Now - startTime_A > TimeSpan.FromSeconds(subOp.WaitTimeoutSeconds.Value))
                            {
                                var result = MessageBox.Show($"AMR 진입 요청에서 timeout이 발생했습니다.\n해당 PLC의 확인이 필요합니다.\n(IP: {subOp.McProtocolIpAddress}, Addr: {subOp.McWordAddress.Value})\n계속 시도하시겠습니까?",
                                    "AMR 진입 요청", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes, MessageBoxOptions.DefaultDesktopOnly);
                                if (result == MessageBoxResult.Yes)
                                {
                                    startTime_A = DateTime.Now;
                                }
                                else if (result == MessageBoxResult.No)
                                {
                                    // 이 시점에서 미션 프로세스를 실패 상태로 마크하고 완료 처리
                                    processInfo.CurrentStatus = MissionStatusEnum.FAILED;
                                    processInfo.HmiStatus.Status = "FAILED";
                                    processInfo.HmiStatus.ProgressPercentage = 100;
                                    processInfo.IsFailed = true; // 프로세스 실패 플래그 설정
                                    processInfo.CancellationTokenSource.Cancel(); // 미션 취소 요청
                                    await HandleRobotMissionCompletion(processInfo);
                                    break;
                                }
                            }
                            await Task.Delay(500).ConfigureAwait(false); // 0.5초마다 체크
                        }
                        break;

                    case SubOperationType.McWaitSensorOff:
                    case SubOperationType.McWaitSensorOn:
                        if (!_mcProtocolInterface) break;
                        if (subOp.McProtocolIpAddress == null || !subOp.McWordAddress.HasValue || !subOp.McWriteValueInt.HasValue)
                        {
                            break; // No action
                            //throw new ArgumentException("McWaitSensorOff/On: WordAddress 또는 WriteValueInt가 지정되지 않았습니다.");
                        }
                        if (!subOp.WaitTimeoutSeconds.HasValue || subOp.WaitTimeoutSeconds.Value <= 0)
                        {
                            subOp.WaitTimeoutSeconds = 60; // 기본 타임아웃 30초
                            Debug.WriteLine($"[RobotMissionService] McWaitSensor: WaitTimeoutSeconds가 지정되지 않아 기본값 30초 사용.");
                        }

                        // 센서 명령 (켜거나 끄기)
                        await _mcProtocolService.WriteWordAsync(subOp.McProtocolIpAddress, subOp.WordDeviceCode, subOp.McWordAddress.Value, (ushort)subOp.McWriteValueInt.Value);//.ConfigureAwait(false);
                        Debug.WriteLine($"[RobotMissionService] McWaitSensor: Command written to Device '{subOp.WordDeviceCode}', Address {subOp.McWordAddress.Value}, Value {(ushort)subOp.McWriteValueInt.Value}. Waiting for sensor state...");
                        // Sensor ON은 확인 없이 진행한다.
                        if (subOp.Type == SubOperationType.McWaitSensorOn)
                            break;

                        bool targetState = (subOp.Type == SubOperationType.McWaitSensorOn); // ON을 기다리면 true, OFF를 기다리면 false
                        DateTime startTime = DateTime.Now;
                        bool sensorStateReached = false;
                        subOp.McWordAddress = (ushort)(subOp.McWordAddress + 0x500);  // read address = write address + 0x500

                        while (/*DateTime.Now - startTime < TimeSpan.FromSeconds(subOp.WaitTimeoutSeconds.Value) &&*/ !processInfo.CancellationTokenSource.Token.IsCancellationRequested)
                        {
                            ushort? currentState = await _mcProtocolService.ReadWordAsync(subOp.McProtocolIpAddress, subOp.WordDeviceCode, subOp.McWordAddress.Value);//.ConfigureAwait(false);
                            //if ((currentState != 0) == targetState) // 0이 아니면 ON, 0이면 OFF로 간주
                            if (currentState == null) // mc protocol communication error
                            {
                                // 이 시점에서 미션 프로세스를 실패 상태로 마크하고 완료 처리
                                //processInfo.CurrentStatus = MissionStatusEnum.FAILED;
                                //processInfo.HmiStatus.Status = "FAILED";
                                //processInfo.HmiStatus.ProgressPercentage = 100;
                                //processInfo.IsFailed = true; // 프로세스 실패 플래그 설정
                                //processInfo.CancellationTokenSource.Cancel(); // 미션 취소 요청
                                //await HandleRobotMissionCompletion(processInfo);
                                //break;
                            }
                            if (currentState == subOp.McWriteValueInt.Value)
                            {
                                sensorStateReached = true;
                                processInfo.HmiStatus.SubOpDescription = "";
                                break;
                            }
                            if (DateTime.Now - startTime > TimeSpan.FromSeconds(subOp.WaitTimeoutSeconds.Value))
                            {
                                var result = MessageBox.Show($"패킹로봇 정지 요청에서 timeout이 발생했습니다.\n해당 PLC의 확인이 필요합니다.\n(IP: {subOp.McProtocolIpAddress}, Addr: {subOp.McWordAddress.Value})\n계속 시도하시겠습니까?",
                                    "패킹로봇 정지 요청", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes, MessageBoxOptions.DefaultDesktopOnly);
                                if (result == MessageBoxResult.Yes)
                                {
                                    startTime_A = DateTime.Now;
                                }
                                else if (result == MessageBoxResult.No)
                                {
                                    // 이 시점에서 미션 프로세스를 실패 상태로 마크하고 완료 처리
                                    processInfo.CurrentStatus = MissionStatusEnum.FAILED;
                                    processInfo.HmiStatus.Status = "FAILED";
                                    processInfo.HmiStatus.ProgressPercentage = 100;
                                    processInfo.IsFailed = true; // 프로세스 실패 플래그 설정
                                    processInfo.CancellationTokenSource.Cancel(); // 미션 취소 요청
                                    await HandleRobotMissionCompletion(processInfo);
                                    break;
                                }
                            }
                            await Task.Delay(500).ConfigureAwait(false); // 0.5초마다 체크
                        }

                        if (!sensorStateReached)
                        {
                            processInfo.CancellationTokenSource.Token.ThrowIfCancellationRequested(); // 취소된 경우 예외 던지기
                                                                                                      // 이 시점에서 미션 프로세스를 실패 상태로 마크하고 완료 처리
                            processInfo.CurrentStatus = MissionStatusEnum.FAILED;
                            processInfo.HmiStatus.Status = "FAILED";
                            //processInfo.HmiStatus.ProgressPercentage = 100;
                            processInfo.IsFailed = true; // 프로세스 실패 플래그 설정
                            processInfo.CancellationTokenSource.Cancel(); // 미션 취소 요청
                            await HandleRobotMissionCompletion(processInfo);
                            break;
                            //throw new TimeoutException($"McWaitSensor: 센서 상태 대기 시간 초과 ({subOp.WaitTimeoutSeconds.Value}초). 목표 상태: {(targetState ? "ON" : "OFF")}");
                        }
                        Debug.WriteLine($"[RobotMissionService] McWaitSensor: Sensor state {(targetState ? "ON" : "OFF")} reached.");
                        break;

                    case SubOperationType.DbReadRackData:
                        if (!subOp.TargetRackId.HasValue)
                        {
                            throw new ArgumentException("DbReadRackData: TargetRackId가 지정되지 않았습니다.");
                        }
                        var rack = await _databaseService.GetRackByIdAsync(subOp.TargetRackId.Value).ConfigureAwait(false);
                        if (rack != null)
                        {
                            processInfo.ReadBulletTypeValue = _mainViewModel.GetBulletTypeNameFromNumber(rack.BulletType);
                            processInfo.ReadStringValue = rack.LotNumber;
                            processInfo.ReadIntValue = (ushort)rack.BoxCount;
                            Debug.WriteLine($"[RobotMissionService] DbReadRackData: Rack {rack.Title} (ID: {rack.Id}) - LotNo '{processInfo.ReadStringValue}', BoxCount {processInfo.ReadIntValue}");
                        }
                        else
                        {
                            Debug.WriteLine($"[RobotMissionService] DbReadRackData: Rack ID {subOp.TargetRackId.Value} not found in DB.");
                            processInfo.ReadBulletTypeValue = "";
                            processInfo.ReadStringValue = "";
                            processInfo.ReadIntValue = 0;
                        }
                        processInfo.HmiStatus.SubOpDescription = "";
                        break;

                    case SubOperationType.DbWriteRackData:
                        if (!subOp.DestRackIdForDbUpdate.HasValue)
                        {
                            throw new ArgumentException("DbReadRackData: TargetRackId가 지정되지 않았습니다.");
                        }
                        await PerformDbUpdateForInboundStepLogic(processInfo, subOp.DestRackIdForDbUpdate).ConfigureAwait(false);
                        processInfo.HmiStatus.SubOpDescription = "";
                        break;

                    case SubOperationType.DbUpdateRackState:
                        if (!subOp.SourceRackIdForDbUpdate.HasValue && !subOp.DestRackIdForDbUpdate.HasValue)
                        {
                            throw new ArgumentException("DbUpdateRackState: SourceRackIdForDbUpdate 또는 DestRackIdForDbUpdate가 지정되지 않았습니다.");
                        }
                        await PerformDbUpdateForCompletedStepLogic(processInfo, subOp.SourceRackIdForDbUpdate, subOp.DestRackIdForDbUpdate).ConfigureAwait(false);
                        processInfo.HmiStatus.SubOpDescription = "";
                        break;

                    case SubOperationType.DbBackupRackState:
                        await PerformDbUpdateForCompletedStepLogic(processInfo, subOp.SourceRackIdForDbUpdate, subOp.DestRackIdForDbUpdate).ConfigureAwait(false);
                        break;

                    case SubOperationType.UiDisplayLotNoBoxCount:
                        if (string.IsNullOrEmpty(processInfo.ReadStringValue) || !processInfo.ReadIntValue.HasValue)
                        {
                            Debug.WriteLine($"[RobotMissionService] UiDisplayLotNoBoxCount: 표시할 LotNo 또는 BoxCount 데이터가 없습니다.");
                            OnShowAutoClosingMessage?.Invoke("UI 표시: LotNo/BoxCount 데이터 없음.");
                        }
                        else
                        {
                            // MainViewModel에 직접 값을 전달하는 이벤트 또는 델리게이트가 필요합니다.
                            _setInputStringForBulletFunc(processInfo.ReadBulletTypeValue);
                            _setInputStringForButtonFunc(processInfo.ReadStringValue);
                            _setInputStringForBoxesFunc(processInfo.ReadIntValue.ToString());
                            OnShowAutoClosingMessage?.Invoke($"UI 표시: LotNo: {processInfo.ReadStringValue}, BoxCount: {processInfo.ReadIntValue.Value}");
                        }
                        processInfo.HmiStatus.SubOpDescription = "";
                        break;

                    case SubOperationType.CheckModbusDiscreteInput:
                        if (!_mcProtocolInterface) break;
                        if (processInfo.IsWarehouseMission)
                            await PerformModbusDiscreteInputCheck(processInfo, subOp.McDiscreteInputAddress, _missionCheckModbusServiceA).ConfigureAwait(false); // Warehouse AMR
                        else
                            await PerformModbusDiscreteInputCheck(processInfo, subOp.McDiscreteInputAddress, _missionCheckModbusServiceB).ConfigureAwait(false); // Packaging Line AMR
                        break;

                    case SubOperationType.SetPlcStatusIsPaused:
                        if (!subOp.PauseButtonCallPlcStatus.HasValue)
                        {
                            throw new ArgumentException("SetPlcStatusIsPaused: PauseButtonCallPlcStatus 지정되지 않았습니다.");
                        }
                        _mainViewModel.PlcStatusIsPaused = subOp.PauseButtonCallPlcStatus.Value;
                        break;

                    case SubOperationType.DbInsertInboundData:
                        if (!subOp.DestRackIdForDbUpdate.HasValue)
                        {
                            throw new ArgumentException("DbUpdateRackState: DestRackIdForDbUpdate가 지정되지 않았습니다.");
                        }
                        await PerformDbInsertForInbound(processInfo, subOp.DestRackIdForDbUpdate).ConfigureAwait(false);
                        break;

                    case SubOperationType.DbUpdateOutboundData:
                        if (!subOp.SourceRackIdForDbUpdate.HasValue && !subOp.DestRackIdForDbUpdate.HasValue)
                        {
                            throw new ArgumentException("DbUpdateRackState: SourceRackIdForDbUpdate (= inserted line id) 가 지정되지 않았습니다.");
                        }
                        await PerformDbUpdateForOutbound(processInfo, subOp.SourceRackIdForDbUpdate).ConfigureAwait(false);
                        break;

                    case SubOperationType.UpdatePalletSupOdd:
                        _mainViewModel._isPalletDummyOdd = !(_mainViewModel._isPalletDummyOdd); // for use both alternately
                        Settings.Default.IsPalletSupOdd = _mainViewModel._isPalletDummyOdd;
                        Settings.Default.Save();
                        break;

                    case SubOperationType.ClearLotInformation:
                        _mainViewModel.LotInfoViewModel.Message = ""; // clear message
                        break;

                    case SubOperationType.None:
                        // 아무 작업 없음
                        break;
                    default:
                        Debug.WriteLine($"[RobotMissionService] Unknown SubOperationType: {subOp.Type}");
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"[RobotMissionService] Sub operation {subOp.Type} cancelled for Process {processInfo.ProcessId}.");
                throw; // 취소 예외는 상위 호출자에게 전파
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RobotMissionService] Error performing sub operation {subOp.Type} for Process {processInfo.ProcessId}: {ex.Message}");
                processInfo.IsFailed = true;
                processInfo.CurrentStatus = MissionStatusEnum.FAILED;
                processInfo.HmiStatus.Status = MissionStatusEnum.FAILED.ToString();
                processInfo.HmiStatus.CurrentStepDescription = $"서브 동작 '{subOp.Description}' 실패: {ex.Message}";
                OnShowAutoClosingMessage?.Invoke($"미션 {processInfo.ProcessType} (ID: {processInfo.ProcessId}) 실패: {subOp.Description} - {ex.Message.Substring(0, Math.Min(100, ex.Message.Length))}");
                await HandleRobotMissionCompletion(processInfo); // 실패 처리
                throw; // 예외를 다시 던져 상위 호출자가 이를 인지하게 함
            }
            finally
            {
                // 각 SubOperation이 독립적인 MC Protocol 연결을 사용하므로,
                // McProtocolService 내부에서 Connect/Disconnect를 관리하도록 위임합니다.
            }
        }

        /// <summary>
        /// 주어진 Modbus Discrete Input 주소를 확인하고, 1일 경우 미션 프로세스를 취소합니다.
        /// 이 메서드는 PerformSubOperation에서 CheckModbusDiscreteInput 타입으로 호출됩니다.
        /// </summary>
        /// <param name="processInfo">현재 미션 프로세스 정보.</param>
        /// <param name="discreteInputAddress">확인할 Discrete Input 주소.</param>
        /// <returns>비동기 작업.</returns>
        private async Task PerformModbusDiscreteInputCheck(RobotMissionInfo processInfo, ushort? discreteInputAddress, ModbusClientService _missionCheckModbusService)
        {
            if (!discreteInputAddress.HasValue)
            {
                throw new ArgumentException("CheckModbusDiscreteInput: DiscreteInputAddress가 지정되지 않았습니다.");
            }

            int modbusConnectRetryCount = 0;

            while (modbusConnectRetryCount < MAX_PLC_CONNECT_RETRIES) // MAX_MODBUS_CONNECT_RETRIES를 MC_PROTOCOL_CONNECT_RETRIES로 변경
            {
                try
                {
                    Debug.WriteLine($"[RobotMissionService] Checking Modbus Discrete Input {discreteInputAddress.Value} before sending mission step. Attempt {modbusConnectRetryCount + 1}/{MAX_PLC_CONNECT_RETRIES}.");

                    // Modbus 연결이 끊겼다면 재연결 시도
                    if (!_missionCheckModbusService.IsConnected)
                    {
                        Debug.WriteLine("[RobotMissionService] Modbus Check Service: Not Connected. Attempting to reconnect...");
                        await _missionCheckModbusService.ConnectAsync().ConfigureAwait(false);
                        if (!_missionCheckModbusService.IsConnected) // 연결 시도 후에도 연결되지 않았다면
                        {
                            throw new InvalidOperationException("Modbus 연결에 실패했습니다. Discrete Input을 읽을 수 없습니다.");
                        }
                        Debug.WriteLine("[RobotMissionService] Modbus Check Service: Reconnected successfully.");
                    }

                    // Modbus 연결이 여전히 안 되어 있다면 예외 발생 (위에서 이미 처리하지만, 방어적으로 한 번 더)
                    if (!_missionCheckModbusService.IsConnected)
                    {
                        throw new InvalidOperationException("Modbus 연결이 활성화되지 않아 Discrete Input을 읽을 수 없습니다.");
                    }

                    // ReadDiscreteInputStatesAsync는 bool[]을 반환하므로, 첫 번째 값만 확인
                    bool[] discreteInputStates = await _missionCheckModbusService.ReadDiscreteInputStatesAsync(discreteInputAddress.Value, 1).ConfigureAwait(false);

                    if (discreteInputStates != null && discreteInputStates.Length > 0 && discreteInputStates[0])
                    {
                        // Discrete input이 1이므로 미션 단계 실패 처리
                        await Application.Current.Dispatcher.Invoke(async () =>
                        {
                            MessageBox.Show(Application.Current.MainWindow, $"Modbus Discrete Input {discreteInputAddress.Value}이(가) 1입니다. 미션 단계를 시작할 수 없습니다. 미션 프로세스를 취소합니다.", "미션 취소", MessageBoxButton.OK, MessageBoxImage.Warning);
                        });
                        Debug.WriteLine($"[RobotMissionService] Modbus Discrete Input {discreteInputAddress.Value} is 1. Cancelling mission process {processInfo.ProcessId}.");
                        // 이 시점에서 미션 프로세스를 실패 상태로 마크하고 완료 처리
                        processInfo.CurrentStatus = MissionStatusEnum.FAILED;
                        processInfo.HmiStatus.Status = "FAILED";
                        //processInfo.HmiStatus.ProgressPercentage = 100;
                        processInfo.IsFailed = true; // 프로세스 실패 플래그 설정
                        processInfo.CancellationTokenSource.Cancel(); // 미션 취소 요청
                        await HandleRobotMissionCompletion(processInfo);
                        //throw new OperationCanceledException($"Modbus Discrete Input {discreteInputAddress.Value} is 1. Mission cancelled.");
                        return;
                    }
                    else
                    {
                        Debug.WriteLine($"[RobotMissionService] Modbus Discrete Input {discreteInputAddress.Value} is 0. Proceeding with mission step.");
                        processInfo.HmiStatus.SubOpDescription = "";
                        return; // Modbus 체크 성공, 함수 종료
                    }
                }
                catch (OperationCanceledException)
                {
                    throw; // 상위 호출자로 취소 예외 전파
                }
                catch (InvalidOperationException ex) // ModbusClientService에서 던지는 연결/작업 오류 예외
                {
                    modbusConnectRetryCount++;
                    Debug.WriteLine($"[RobotMissionService] Modbus check attempt {modbusConnectRetryCount}/{MAX_PLC_CONNECT_RETRIES} failed: {ex.Message}");
                    _missionCheckModbusService.Dispose(); // 다음 재시도를 위해 자원 정리

                    if (modbusConnectRetryCount >= MAX_PLC_CONNECT_RETRIES)
                    {
                        // 최대 재시도 횟수 도달, 미션 실패로 마크
                        await Application.Current.Dispatcher.Invoke(async () =>
                        {
                            MessageBox.Show(Application.Current.MainWindow, $"Modbus 연결/통신 오류: InvalidOperationException: {ex.Message}. 미션 프로세스를 취소합니다.", "미션 취소", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                        Debug.WriteLine($"[RobotMissionService] Modbus Connection/Communication Error during check: {ex.Message}. Cancelling mission process {processInfo.ProcessId}.");
                        processInfo.CurrentStatus = MissionStatusEnum.FAILED;
                        processInfo.HmiStatus.Status = "FAILED";
                        //processInfo.HmiStatus.ProgressPercentage = 100;
                        processInfo.IsFailed = true; // 프로세스 실패 플래그 설정
                        processInfo.CancellationTokenSource.Cancel(); // 미션 취소 요청
                        await HandleRobotMissionCompletion(processInfo);
                        //throw new Exception($"Modbus 연결/통신 오류 (최대 재시도 횟수 초과): {ex.Message}");
                        return;
                    }
                    else
                    {
                        DisplayModbusErrorMessage($"AMR Modbus 연결/통신 오류: InvalidOperationException: {ex.Message}.");
                        // 다음 재시도 전 지연
                        await Task.Delay(TimeSpan.FromSeconds(PLC_CONNECT_RETRY_DELAY_SECONDS)).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    modbusConnectRetryCount++;
                    Debug.WriteLine($"[RobotMissionService] Modbus check attempt {modbusConnectRetryCount}/{MAX_PLC_CONNECT_RETRIES} failed: {ex.Message}");
                    _missionCheckModbusService.Dispose(); // 다음 재시도를 위해 자원 정리

                    if (modbusConnectRetryCount >= MAX_PLC_CONNECT_RETRIES)
                    {
                        // 최대 재시도 횟수 도달, 미션 실패로 마크
                        await Application.Current.Dispatcher.Invoke(async () =>
                        {
                            MessageBox.Show(Application.Current.MainWindow, $"Modbus 연결/통신 오류: Exception: {ex.Message}. 미션 프로세스를 취소합니다.", "미션 취소", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                        Debug.WriteLine($"[RobotMissionService] Max Modbus check retries reached for process {processInfo.ProcessId}. Cancelling mission.");
                        processInfo.CurrentStatus = MissionStatusEnum.FAILED;
                        processInfo.HmiStatus.Status = "FAILED";
                        //processInfo.HmiStatus.ProgressPercentage = 100;
                        processInfo.IsFailed = true;
                        processInfo.CancellationTokenSource.Cancel();
                        await HandleRobotMissionCompletion(processInfo);
                        //throw new Exception($"Modbus 연결/통신 오류 (최대 재시도 횟수 초과): {ex.Message}");
                        return;
                    }
                    else
                    {
                        DisplayModbusErrorMessage($"AMR Modbus 연결/통신 오류: Exception: {ex.Message}.");
                        // 다음 재시도 전 지연
                        await Task.Delay(TimeSpan.FromSeconds(PLC_CONNECT_RETRY_DELAY_SECONDS)).ConfigureAwait(false);
                    }
                }
            }
        }

        /// <summary>
        /// DbUpdateRackState 서브 동작을 위한 실제 DB 업데이트 로직을 수행합니다.
        /// </summary>
        /// <param name="processInfo">현재 미션 프로세스 정보.</param>
        /// <param name="sourceRackId">원본 랙 ID.</param>
        /// <param name="destinationRackId">목적지 랙 ID.</param>
        private async Task PerformDbUpdateForCompletedStepLogic(RobotMissionInfo processInfo, int? sourceRackId, int? destinationRackId)
        {
            Debug.WriteLine($"[RobotMissionService] Performing DB update for completed step. ProcessType: {processInfo.ProcessType}, SourceRackId: {sourceRackId}, DestinationRackId: {destinationRackId}");

            RackViewModel sourceRackVm = null;
            if (sourceRackId.HasValue)
            {
                sourceRackVm = _getRackViewModelByIdFunc(sourceRackId.Value);
                if (sourceRackVm == null)
                {
                    Debug.WriteLine($"[RobotMissionService] Warning: SourceRackViewModel not found for ID {sourceRackId.Value}.");
                }
            }

            RackViewModel destinationRackVm = null;
            if (destinationRackId.HasValue)
            {
                destinationRackVm = _getRackViewModelByIdFunc(destinationRackId.Value);
                if (destinationRackVm == null)
                {
                    Debug.WriteLine($"[RobotMissionService] Warning: DestinationRackViewModel not found for ID {destinationRackId.Value}.");
                }
            }

            try
            {
                // 모든 이동 작업의 결과는 source rack에서 destination rack으로의 파레트 이동이며,
                // 이 때, source rack의 파레트 정보 (RackType, BulletType, Lot No.)를 destination rack의 파레트 정보로 copy하고
                // source rack의 파레트 정보를 초기화 (BulletType = 0, Lot No. = null)하는 것이다.
                // 특별한 경우는 1) source rack이 WRAP rack일 경우는 BulletType과 Lot No.를 InputStringForButton으로 부터 얻는 것과,
                // 2) ProcessType이 "FakeExecuteInboundProduct" 또는 "HandleHalfPalletExport" RackType이 바뀌는 경우 뿐이다.

                if (sourceRackVm != null && destinationRackVm != null)
                {
                    // 1. Destination Rack에 Source Rack의 제품 정보 복사
                    string sourceLotNumber;
                    int sourceBoxCount;
                    int sourceBulletType; // = sourceRackVm.BulletType;
                    int newDestinationRackType = destinationRackVm.RackType; // 기본적으로 목적지 랙의 현재 RackType 유지
                    int newSourceRackType = sourceRackVm.RackType; // 기본적으로 현재 RackType 유지

                    // 특별한 경우 1): source rack이 WRAP rack일 경우
                    if (sourceRackVm.Title.Equals(_wrapRackTitle) && _getInputStringForBulletFunc != null && _getInputStringForButtonFunc != null && _getInputStringForBoxesFunc != null)
                    {
                        // BulletType은 이미 MainViewModel에서 WRAP 랙에 설정된 상태이므로 sourceRackVm.BulletType 사용
                        sourceBulletType = _mainViewModel.GetBulletTypeFromInputString(_getInputStringForBulletFunc.Invoke().TrimStart());
                        sourceLotNumber = _getInputStringForButtonFunc.Invoke().TrimStart();
                        sourceBoxCount = string.IsNullOrWhiteSpace(_getInputStringForBoxesFunc.Invoke()) ? 0 : Int32.Parse(_getInputStringForBoxesFunc.Invoke());
                    }
                    else
                    {
                        sourceBulletType = sourceRackVm.BulletType;
                        sourceLotNumber = sourceRackVm.LotNumber;
                        sourceBoxCount = sourceRackVm.BoxCount;
                    }

                    // 특별한 경우 2): ProcessType이 "라이트 입고 작업 = FakeExecuteInboundProduct"일 경우 RackType 변경
                    if (processInfo.ProcessType == "라이트 입고 작업") // WRAP -> AMR -> Rack
                    {
                        newDestinationRackType = 3; // 라이트 랙 타입으로 변경 (완제품 랙에서 라이트 랙으로)
                        if (sourceRackVm.Title.Equals("AMR")) newSourceRackType = 2;
                    }
                    else if (processInfo.ProcessType == "라이트 반출 준비" || processInfo.ProcessType == "라이트 반출 작업") // Rack -> AMR -> Rack 1-1 -> (NULL)
                    {
                        newDestinationRackType = 3;
                        if (sourceRackVm.Title.Equals("AMR")) newSourceRackType = 2; // AMR -> WRAP, AMR -> RACK
                        else if (sourceRackVm.Title.Equals("1-1")) newSourceRackType = 3; // 1-1은 항상 RackType = 3 유지
                        else newSourceRackType = 1; // Rack -> AMR, OUT -> (NULL)

                        /*if (destinationRackVm.Title.Equals("OUT"))
                            await SetVisibleRackInProcess(destinationRackVm.Id, true);
                        if (sourceRackVm.Title.Equals("OUT"))
                            await SetVisibleRackInProcess(sourceRackVm.Id, false);*/
                    }
                    // 특별한 경우 3)
                    if (destinationRackVm.Title.Equals(_wrapRackTitle))
                    {
                        _setInputStringForBulletFunc(_mainViewModel.GetBulletTypeNameFromNumber(sourceBulletType));
                        //_mainViewModel.SetInputStringForBullet(sou_mainViewModel.GetBulletTypeNameFromNumber(sourceBulletType)rceBulletString);
                        _setInputStringForButtonFunc(sourceLotNumber);
                        _setInputStringForBoxesFunc(sourceBoxCount.ToString());
                        //OnShowAutoClosingMessage?.Invoke($"UI 표시: LotNo: {processInfo.ReadStringValue}, BoxCount: {processInfo.ReadIntValue.Value}");
                    }
                    else
                    {
                        await _databaseService.UpdateRackStateAsync(
                            destinationRackVm.Id,
                            newDestinationRackType,
                            sourceBulletType
                        );
                        await _databaseService.UpdateLotNumberAsync(
                            destinationRackVm.Id,
                            sourceLotNumber,
                            sourceBoxCount
                        );
                    }

                    Debug.WriteLine($"[RobotMissionService] DB Update: Rack {destinationRackVm.Title} (ID: {destinationRackVm.Id}) updated with BulletType {sourceBulletType}, LotNumber '{sourceLotNumber}', RackType {newDestinationRackType}.");

                    // 2. Source Rack의 파레트 정보 초기화 (BulletType = 0, Lot No. = null)
                    // Source Rack의 RackType은 유지
                    await _databaseService.UpdateRackStateAsync(
                        sourceRackVm.Id,
                        newSourceRackType,
                        0 // BulletType을 0으로 설정 (비움)
                    );
                    await _databaseService.UpdateLotNumberAsync(
                        sourceRackVm.Id,
                        String.Empty, // LotNumber 비움
                        0
                    );
                    if (sourceRackVm.Title.Equals(_wrapRackTitle))
                    {
                        OnInputStringForBulletCleared?.Invoke(); // WRAP 랙 비우면 입력 필드 초기화
                        OnInputStringForButtonCleared?.Invoke();
                        OnInputStringForBoxesCleared?.Invoke();
                        Debug.WriteLine($"[RobotMissionService] DB Update: WRAP rack {sourceRackVm.Title} cleared.");
                    }
                    Debug.WriteLine($"[RobotMissionService] DB Update: Source Rack {sourceRackVm.Title} (ID: {sourceRackVm.Id}) cleared.");

                    // 3. 랙 잠금 해제 (이동 완료된 랙만 해제)
                    await _databaseService.UpdateIsLockedAsync(sourceRackVm.Id, false);
                    Application.Current.Dispatcher.Invoke(() => OnRackLockStateChanged?.Invoke(sourceRackVm.Id, false));
                    processInfo.RacksLockedByProcess.Remove(sourceRackVm.Id); // locked list에서 삭제
                    //if (destinationRackVm.Title.Equals("OUT"))
                    //{
                    //    await _databaseService.UpdateIsLockedAsync(destinationRackVm.Id, true); // 재공풐 반출 시 click 안되게...
                    //    Application.Current.Dispatcher.Invoke(() => OnRackLockStateChanged?.Invoke(destinationRackVm.Id, false));
                    //}
                    //else
                    {
                        await _databaseService.UpdateIsLockedAsync(destinationRackVm.Id, false);
                        Application.Current.Dispatcher.Invoke(() => OnRackLockStateChanged?.Invoke(destinationRackVm.Id, false));
                    }
                    Debug.WriteLine($"[RobotMissionService] Racks {sourceRackVm.Title} and {destinationRackVm.Title} unlocked.");

                    OnShowAutoClosingMessage?.Invoke($"랙 {sourceRackVm.Title}에서 랙 {destinationRackVm.Title}으로 이동 및 업데이트 성공.");
                }
                else if (sourceRackVm != null && destinationRackId == null) // DestinationRackId가 null인 경우 (예: 출고, 반출)
                {
                    // HandleHalfPalletExport 또는 HandleRackShipout (단일 랙 출고)
                    // 랙 비우기 (BulletType = 0, LotNumber = String.Empty)
                    int newSourceRackType = sourceRackVm.RackType;

                    if (processInfo.ProcessType == "라이트 반출 작업")
                    {
                        // 반출의 경우 RackType은 1 (완제품)으로 유지
                        // newSourceRackType = 1;
                        if (sourceRackVm.Title.Equals("OUT"))
                            await SetVisibleRackInProcess(sourceRackVm.Id, false);
                    }

                    await _databaseService.UpdateRackStateAsync(
                        sourceRackVm.Id,
                        newSourceRackType,
                        0 // BulletType을 0으로 설정 (비움)
                    );
                    await _databaseService.UpdateLotNumberAsync(
                        sourceRackVm.Id,
                        String.Empty, // LotNumber 비움
                        0
                    );
                    Debug.WriteLine($"[RobotMissionService] DB Update: Rack {sourceRackVm.Title} (ID: {sourceRackVm.Id}) cleared for {processInfo.ProcessType}.");

                    // 랙 잠금 해제
                    await _databaseService.UpdateIsLockedAsync(sourceRackVm.Id, false);
                    Application.Current.Dispatcher.Invoke(() => OnRackLockStateChanged?.Invoke(sourceRackVm.Id, false));
                    Debug.WriteLine($"[RobotMissionService] Rack {sourceRackVm.Title} unlocked.");

                    OnShowAutoClosingMessage?.Invoke($"랙 {sourceRackVm.Title} {processInfo.ProcessType} 성공.");
                }
                else if (processInfo.RacksToProcess != null && processInfo.RacksToProcess.Any() && processInfo.ProcessType == "다중 출고 작업")
                {
                    // 다중 랙 출고 (ExecuteCheckoutProduct) - RacksToProcess 목록을 기반으로 처리
                    // 이 부분은 위의 sourceRackVm != null && destinationRackId == null 로직으로 처리될 것임.
                    Debug.WriteLine($"[RobotMissionService] Processing ExecuteCheckoutProduct completion for racks in RacksToProcess. (This path should be covered by single rack logic if SourceRackId is provided per step).");
                }
                else
                {
                    Debug.WriteLine($"[RobotMissionService] PerformDbUpdateForCompletedStepLogic: No specific rack handling logic for ProcessType '{processInfo.ProcessType}' with provided rack IDs. SourceRackId: {sourceRackId}, DestinationRackId: {destinationRackId}. No DB update performed.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RobotMissionService] Error performing DB update for completed step: {ex.Message}");
                OnShowAutoClosingMessage?.Invoke($"미션 완료 후 DB 업데이트 중 오류 발생: {ex.Message.Substring(0, Math.Min(100, ex.Message.Length))}");
                throw; // 예외를 다시 던져 상위 호출자가 이를 인지하게 함
            }
        }

        /// <summary>
        /// DbUpdateRackState 서브 동작을 위한 실제 DB 업데이트 로직을 수행합니다.
        /// </summary>
        /// <param name="processInfo">현재 미션 프로세스 정보.</param>
        /// <param name="sourceRackId">원본 랙 ID.</param>
        /// <param name="destinationRackId">목적지 랙 ID.</param>
        private async Task PerformDbUpdateForInboundStepLogic(RobotMissionInfo processInfo, int? destinationRackId)
        {
            Debug.WriteLine($"[RobotMissionService] Performing DB update for completed step. ProcessType: {processInfo.ProcessType}, DestinationRackId: {destinationRackId}");

            RackViewModel destinationRackVm = null;
            if (destinationRackId.HasValue)
            {
                destinationRackVm = _getRackViewModelByIdFunc(destinationRackId.Value);
                if (destinationRackVm == null)
                {
                    Debug.WriteLine($"[RobotMissionService] Warning: DestinationRackViewModel not found for ID {destinationRackId.Value}.");
                }
            }

            try
            {
                // 모든 이동 작업의 결과는 source rack에서 destination rack으로의 파레트 이동이며,
                // 이 때, source rack의 파레트 정보 (RackType, BulletType, Lot No.)를 destination rack의 파레트 정보로 copy하고
                // source rack의 파레트 정보를 초기화 (BulletType = 0, Lot No. = null)하는 것이다.
                // 특별한 경우는 1) source rack이 WRAP rack일 경우는 BulletType과 Lot No.를 InputStringForButton으로 부터 얻는 것과,
                // 2) ProcessType이 "FakeExecuteInboundProduct" 또는 "HandleHalfPalletExport" RackType이 바뀌는 경우 뿐이다.

                if (destinationRackVm != null)
                {
                    // 1. Destination Rack에 Source Rack의 제품 정보 복사
                    string sourceLotNumber = processInfo.ReadStringValue;
                    int sourceBoxCount = processInfo.ReadIntValue.HasValue ? processInfo.ReadIntValue.Value : 0;
                    int sourceBulletType = _mainViewModel.GetBulletTypeFromInputString(processInfo.ReadBulletTypeValue);
                    int newDestinationRackType = destinationRackVm.RackType; // 기본적으로 목적지 랙의 현재 RackType 유지

                    await _databaseService.UpdateRackStateAsync(
                        destinationRackVm.Id,
                        newDestinationRackType,
                        sourceBulletType
                    );
                    await _databaseService.UpdateLotNumberAsync(
                        destinationRackVm.Id,
                        sourceLotNumber,
                        sourceBoxCount
                    );

                    // 3. 랙 잠금 해제 (이동 완료된 랙만 해제)
                    await _databaseService.UpdateIsLockedAsync(destinationRackVm.Id, false);
                    Application.Current.Dispatcher.Invoke(() => OnRackLockStateChanged?.Invoke(destinationRackVm.Id, false));
                    processInfo.RacksLockedByProcess.Remove(destinationRackVm.Id); // locked list에서 삭제
                    //if (destinationRackVm.Title.Equals("OUT"))
                    //{
                    //    await _databaseService.UpdateIsLockedAsync(destinationRackVm.Id, true); // 재공풐 반출 시 click 안되게...
                    //    Application.Current.Dispatcher.Invoke(() => OnRackLockStateChanged?.Invoke(destinationRackVm.Id, false));
                    //}
                    //else
                    {
                        await _databaseService.UpdateIsLockedAsync(destinationRackVm.Id, false);
                        Application.Current.Dispatcher.Invoke(() => OnRackLockStateChanged?.Invoke(destinationRackVm.Id, false));
                    }
                    Debug.WriteLine($"[RobotMissionService] Rack {destinationRackVm.Title} unlocked.");

                    OnShowAutoClosingMessage?.Invoke($"입고 팔레트를 랙{destinationRackVm.Title}에 적치 및 업데이트 성공.");
                }
                else
                {
                    Debug.WriteLine($"[RobotMissionService] PerformDbUpdateForCompletedStepLogic: No specific rack handling logic for ProcessType '{processInfo.ProcessType}' with provided rack IDs. DestinationRackId: {destinationRackId}. No DB update performed.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RobotMissionService] Error performing DB update for completed step: {ex.Message}");
                OnShowAutoClosingMessage?.Invoke($"미션 완료 후 DB 업데이트 중 오류 발생: {ex.Message.Substring(0, Math.Min(100, ex.Message.Length))}");
                throw; // 예외를 다시 던져 상위 호출자가 이를 인지하게 함
            }
        }

        /// <summary>
        /// DbUpdateRackState 서브 동작을 위한 실제 DB 업데이트 로직을 수행합니다.
        /// </summary>
        /// <param name="processInfo">현재 미션 프로세스 정보.</param>
        /// <param name="sourceRackId">원본 랙 ID.</param>
        /// <param name="destinationRackId">목적지 랙 ID.</param>
        private async Task PerformDbInsertForInbound(RobotMissionInfo processInfo, int? destinationRackId)
        {
            Debug.WriteLine($"[RobotMissionService] Performing DB insert for inbound step. ProcessType: {processInfo.ProcessType}, DestinationRackId: {destinationRackId}");

            RackViewModel destinationRackVm = null;
            if (destinationRackId.HasValue)
            {
                destinationRackVm = _getRackViewModelByIdFunc(destinationRackId.Value);
                if (destinationRackVm == null)
                {
                    Debug.WriteLine($"[RobotMissionService] Warning: DestinationRackViewModel not found for ID {destinationRackId.Value}.");
                }
            }

            try
            {
                // 모든 이동 작업의 결과는 source rack에서 destination rack으로의 파레트 이동이며,
                // 이 때, source rack의 파레트 정보 (RackType, BulletType, Lot No.)를 destination rack의 파레트 정보로 copy하고
                // source rack의 파레트 정보를 초기화 (BulletType = 0, Lot No. = null)하는 것이다.
                // 특별한 경우는 1) source rack이 WRAP rack일 경우는 BulletType과 Lot No.를 InputStringForButton으로 부터 얻는 것과,
                // 2) ProcessType이 "FakeExecuteInboundProduct" 또는 "HandleHalfPalletExport" RackType이 바뀌는 경우 뿐이다.
                string rackName = destinationRackVm.Title;
                int bulletType = destinationRackVm.BulletType;
                string lotNumber = destinationRackVm.LotNumber;
                int boxCount = destinationRackVm.BoxCount;
                DateTime? inboundAt = destinationRackVm.RackedAt;

                var innsertedIn = await _databaseService.InsertInbountDBAsync(rackName, bulletType, lotNumber, boxCount, inboundAt);
                await _databaseService.UpdateIsInsertedInAsync((int)destinationRackId, innsertedIn);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RobotMissionService] Error performing DB insert for inbound step: {ex.Message}");
                OnShowAutoClosingMessage?.Invoke($"미션 완료 후 DB 업데이트 중 오류 발생: {ex.Message.Substring(0, Math.Min(100, ex.Message.Length))}");
                throw; // 예외를 다시 던져 상위 호출자가 이를 인지하게 함
            }
        }

        /// <summary>
        /// DbUpdateRackState 서브 동작을 위한 실제 DB 업데이트 로직을 수행합니다.
        /// </summary>
        /// <param name="processInfo">현재 미션 프로세스 정보.</param>
        /// <param name="sourceRackId">원본 랙 ID.</param>
        /// <param name="destinationRackId">목적지 랙 ID.</param>
        private async Task PerformDbUpdateForOutbound(RobotMissionInfo processInfo, int? insertedInID)
        {
            Debug.WriteLine($"[RobotMissionService] Performing DB update for outbound step. ProcessType: {processInfo.ProcessType}, inserted lin id: {insertedInID}");

            if (insertedInID.HasValue)
            {
                try
                {
                    await _databaseService.UpdateOutboundDBAsync((int)insertedInID);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[RobotMissionService] Error performing DB update for outbound step: {ex.Message}");
                    OnShowAutoClosingMessage?.Invoke($"미션 완료 후 DB 업데이트 중 오류 발생: {ex.Message.Substring(0, Math.Min(100, ex.Message.Length))}");
                    throw; // 예외를 다시 던져 상위 호출자가 이를 인지하게 함
                }
            }
        }

        private async Task PerformDbUpdateBackupForWrap(RobotMissionInfo processInfo, int? insertedInID)
        {
            Debug.WriteLine($"[RobotMissionService] Performing DB update for outbound step. ProcessType: {processInfo.ProcessType}, inserted lin id: {insertedInID}");

            if (insertedInID.HasValue)
            {
                try
                {
                    await _databaseService.UpdateOutboundDBAsync((int)insertedInID);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[RobotMissionService] Error performing DB update for outbound step: {ex.Message}");
                    OnShowAutoClosingMessage?.Invoke($"미션 완료 후 DB 업데이트 중 오류 발생: {ex.Message.Substring(0, Math.Min(100, ex.Message.Length))}");
                    throw; // 예외를 다시 던져 상위 호출자가 이를 인지하게 함
                }
            }
        }

        /// <summary>
        /// 서비스 자원을 해제합니다.
        /// </summary>
        public void Dispose()
        {
            _robotMissionPollingTimer?.Stop();
            _robotMissionPollingTimer.Tick -= RobotMissionPollingTimer_Tick;
            _modbusErrorMessageSuppressionTimer?.Stop(); // 타이머 해제
            _modbusErrorMessageSuppressionTimer.Tick -= (sender, e) => { /* empty */ }; // 이벤트 핸들러 해제

            foreach (var processInfo in _activeRobotProcesses.Values)
            {
                processInfo.CancellationTokenSource?.Cancel();
                processInfo.CancellationTokenSource?.Dispose();
            }
            _activeRobotProcesses.Clear(); // 딕셔너리 비우기

            _missionCheckModbusServiceA?.Dispose(); // 미션 체크용 Modbus 서비스도 해제
            _missionCheckModbusServiceB?.Dispose(); // 미션 체크용 Modbus 서비스도 해제
            _mcProtocolService?.Dispose(); // MC Protocol 서비스도 해제
            Debug.WriteLine("[RobotMissionService] Disposed.");
        }

        /// <summary>
        /// IsLinkable이 false인 미션 단계가 성공적으로 완료되었을 때 DB 업데이트 및 랙 잠금 해제를 수행합니다.
        /// </summary>
        /// <param name="processInfo">현재 미션 프로세스 정보.</param>
        /// <param name="completedStep">완료된 미션 단계의 정의.</param>
        private async Task PerformDbUpdateForCompletedStep(RobotMissionInfo processInfo, MissionStepDefinition completedStep)
        {
            Debug.WriteLine($"[RobotMissionService] Performing DB update for completed step. ProcessType: {processInfo.ProcessType}, SourceRackId: {completedStep.SourceRackId}, DestinationRackId: {completedStep.DestinationRackId}");

            RackViewModel sourceRackVm = null;
            if (completedStep.SourceRackId.HasValue)
            {
                sourceRackVm = _getRackViewModelByIdFunc(completedStep.SourceRackId.Value);
                if (sourceRackVm == null)
                {
                    Debug.WriteLine($"[RobotMissionService] Warning: SourceRackViewModel not found for ID {completedStep.SourceRackId.Value}.");
                }
            }

            RackViewModel destinationRackVm = null;
            if (completedStep.DestinationRackId.HasValue)
            {
                destinationRackVm = _getRackViewModelByIdFunc(completedStep.DestinationRackId.Value);
                if (destinationRackVm == null)
                {
                    Debug.WriteLine($"[RobotMissionService] Warning: DestinationRackViewModel not found for ID {completedStep.DestinationRackId.Value}.");
                }
            }

            try
            {
                // 모든 이동 작업의 결과는 source rack에서 destination rack으로의 파레트 이동이며,
                // 이 때, source rack의 파레트 정보 (RackType, BulletType, Lot No.)를 destination rack의 파레트 정보로 copy하고
                // source rack의 파레트 정보를 초기화 (BulletType = 0, Lot No. = null)하는 것이다.
                // 특별한 경우는 1) source rack이 WRAP rack일 경우는 BulletType과 Lot No.를 InputStringForButton으로 부터 얻는 것과,
                // 2) ProcessType이 "FakeExecuteInboundProduct" 또는 "HandleHalfPalletExport" RackType이 바뀌는 경우 뿐이다.

                if (sourceRackVm != null && destinationRackVm != null)
                {
                    // 1. Destination Rack에 Source Rack의 제품 정보 복사
                    string sourceLotNumber;
                    int sourceBoxCount;
                    int sourceBulletType = sourceRackVm.BulletType;
                    int newDestinationRackType = destinationRackVm.RackType; // 기본적으로 목적지 랙의 현재 RackType 유지
                    int newSourceRackType = sourceRackVm.RackType; // 기본적으로 현재 RackType 유지

                    // 특별한 경우 1): source rack이 WRAP rack일 경우
                    if (sourceRackVm.Title.Equals(_wrapRackTitle) && _getInputStringForBulletFunc != null && _getInputStringForButtonFunc != null && _getInputStringForBoxesFunc != null)
                    {
                        sourceLotNumber = _getInputStringForButtonFunc.Invoke().TrimStart();
                        // BulletType은 이미 MainViewModel에서 WRAP 랙에 설정된 상태이므로 sourceRackVm.BulletType 사용
                        sourceBoxCount = string.IsNullOrWhiteSpace(_getInputStringForBoxesFunc.Invoke()) ? 0 : Int32.Parse(_getInputStringForBoxesFunc.Invoke());
                    }
                    else
                    {
                        sourceLotNumber = sourceRackVm.LotNumber;
                        sourceBoxCount = sourceRackVm.BoxCount;
                    }

                    // 특별한 경우 2): ProcessType이 "라이트 입고 작업 = FakeExecuteInboundProduct"일 경우 RackType 변경
                    if (processInfo.ProcessType == "라이트 입고 작업")
                    {
                        newDestinationRackType = 3; // 라이트 랙 타입으로 변경 (완제품 랙에서 라이트 랙으로)
                        if (sourceRackVm.Title.Equals("WRAP")) newSourceRackType = 2;
                        else if (sourceRackVm.Title.Equals("AMR")) newSourceRackType = 1;
                    }
                    else if (processInfo.ProcessType == "라이트 반출 작업")
                    {
                        newDestinationRackType = 3;
                        newSourceRackType = 1;
                    }

                    await _databaseService.UpdateRackStateAsync(
                        destinationRackVm.Id,
                        newDestinationRackType,
                        sourceBulletType
                    );
                    await _databaseService.UpdateLotNumberAsync(
                        destinationRackVm.Id,
                        sourceLotNumber,
                        sourceBoxCount
                    );
                    Debug.WriteLine($"[RobotMissionService] DB Update: Rack {destinationRackVm.Title} (ID: {destinationRackVm.Id}) updated with BulletType {sourceBulletType}, LotNumber '{sourceLotNumber}', RackType {newDestinationRackType}.");

                    // 2. Source Rack의 파레트 정보 초기화 (BulletType = 0, Lot No. = null)
                    // Source Rack의 RackType은 유지
                    await _databaseService.UpdateRackStateAsync(
                        sourceRackVm.Id,
                        newSourceRackType,
                        0 // BulletType을 0으로 설정 (비움)
                    );
                    await _databaseService.UpdateLotNumberAsync(
                        sourceRackVm.Id,
                        String.Empty, // LotNumber 비움
                        0
                    );
                    if (sourceRackVm.Title.Equals("WRAP"))
                    {
                        OnInputStringForBulletCleared?.Invoke();
                        OnInputStringForButtonCleared?.Invoke(); // WRAP 랙 비우면 입력 필드 초기화
                        OnInputStringForBoxesCleared?.Invoke();
                        Debug.WriteLine($"[RobotMissionService] DB Update: WRAP rack {sourceRackVm.Title} cleared.");
                    }
                    Debug.WriteLine($"[RobotMissionService] DB Update: Source Rack {sourceRackVm.Title} (ID: {sourceRackVm.Id}) cleared.");

                    // 3. 랙 잠금 해제 (이동 완료된 랙만 해제)
                    await _databaseService.UpdateIsLockedAsync(sourceRackVm.Id, false);
                    Application.Current.Dispatcher.Invoke(() => OnRackLockStateChanged?.Invoke(sourceRackVm.Id, false));
                    await _databaseService.UpdateIsLockedAsync(destinationRackVm.Id, false);
                    Application.Current.Dispatcher.Invoke(() => OnRackLockStateChanged?.Invoke(destinationRackVm.Id, false));
                    Debug.WriteLine($"[RobotMissionService] Racks {sourceRackVm.Title} and {destinationRackVm.Title} unlocked.");

                    OnShowAutoClosingMessage?.Invoke($"랙 {sourceRackVm.Title}에서 랙 {destinationRackVm.Title}으로 이동 및 업데이트 성공.");
                }
                else if (sourceRackVm != null && destinationRackVm == null) // DestinationRackId가 null인 경우 (예: 출고, 반출)
                {
                    // HandleHalfPalletExport 또는 HandleRackShipout (단일 랙 출고)
                    // 랙 비우기 (BulletType = 0, LotNumber = String.Empty)
                    int newSourceRackType = sourceRackVm.RackType;
                    if (processInfo.ProcessType == "라이트 반출 작업")
                    {
                        // 반출의 경우 RackType은 1 (완제품)으로 유지
                        newSourceRackType = 1;
                    }

                    await _databaseService.UpdateRackStateAsync(
                        sourceRackVm.Id,
                        newSourceRackType,
                        0 // BulletType을 0으로 설정 (비움)
                    );
                    await _databaseService.UpdateLotNumberAsync(
                        sourceRackVm.Id,
                        String.Empty, // LotNumber 비움
                        0
                    );
                    Debug.WriteLine($"[RobotMissionService] DB Update: Rack {sourceRackVm.Title} (ID: {sourceRackVm.Id}) cleared for {processInfo.ProcessType}.");

                    // 랙 잠금 해제
                    await _databaseService.UpdateIsLockedAsync(sourceRackVm.Id, false);
                    Application.Current.Dispatcher.Invoke(() => OnRackLockStateChanged?.Invoke(sourceRackVm.Id, false));
                    Debug.WriteLine($"[RobotMissionService] Rack {sourceRackVm.Title} unlocked.");

                    OnShowAutoClosingMessage?.Invoke($"랙 {sourceRackVm.Title} {processInfo.ProcessType} 성공.");
                }
                else if (processInfo.RacksToProcess != null && processInfo.RacksToProcess.Any() && processInfo.ProcessType == "다중 출고 작업")
                {
                    // 다중 랙 출고 (ExecuteCheckoutProduct) - RacksToProcess 목록을 기반으로 처리
                    // 이 시나리오는 각 랙에 대한 개별 미션 완료 시점이 아니라, 전체 출고 프로세스 완료 시점에
                    // 일괄적으로 처리되는 것이 더 적합할 수 있으나, 현재 요청에 따라 개별 IsLinkable=false 단계에서 처리.
                    // 만약 ExecuteCheckoutProduct가 단일 IsLinkable=false 스텝으로 끝나지 않고 각 랙마다 IsLinkable=false 스텝이 있다면 이 로직이 맞음.
                    // 현재 MainViewModel의 ExecuteCheckoutProduct를 보면, 각 랙마다 마지막 스텝에 SourceRackId가 설정되어 있으므로 이 로직이 맞음.
                    Debug.WriteLine($"[RobotMissionService] Processing ExecuteCheckoutProduct completion for racks in RacksToProcess.");
                    // 이 부분은 이미 위에서 sourceRackVm != null && destinationRackVm == null 로직으로 처리될 것임.
                    // RacksToProcess는 InitiateRobotMissionProcess 시점에 전달되지만,
                    // PerformDbUpdateForCompletedStep은 단일 SourceRackId/DestinationRackId를 기반으로 동작하므로,
                    // ExecuteCheckoutProduct의 경우 각 랙에 대한 마지막 미션 단계가 완료될 때마다 이 로직이 트리거되어야 함.
                    // 따라서 이 else if 블록은 필요 없으며, 위의 sourceRackVm != null && destinationRackVm == null 블록이 처리해야 함.
                }
                else
                {
                    Debug.WriteLine($"[RobotMissionService] PerformDbUpdateForCompletedStep: No specific rack handling logic for ProcessType '{processInfo.ProcessType}' with provided rack IDs. SourceRackId: {completedStep.SourceRackId}, DestinationRackId: {completedStep.DestinationRackId}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RobotMissionService] Error performing DB update for completed step: {ex.Message}");
                OnShowAutoClosingMessage?.Invoke($"미션 완료 후 DB 업데이트 중 오류 발생: {ex.Message.Substring(0, Math.Min(100, ex.Message.Length))}");
            }
        }

        private async Task ExtractVehicle(string amrName)
        {
            // Extract vehicle
            var payload = new ExtractVehicleRequest
            {
                Command = new ExtractVehicleCommand
                {
                    Name = "extract",
                    Args = new { } // 빈 객체
                }
            };
            string vehicleName = amrName; // missionInfo.IsWarehouseMission ? _warehouseAmrName : _packagingLineAmrName;
            string requestEndpoint = $"wms/rest/v{_httpService.CurrentApiVersionMajor}.{_httpService.CurrentApiVersionMinor}/vehicles/{vehicleName}/command";
            //string requestPayloadJson = JsonConvert.SerializeObject(payload, Formatting.Indented);

            OnShowAutoClosingMessage?.Invoke($"로봇 미션 프로세스 실패! Extracting {vehicleName} ...");
            //_httpService.PostAsync<ExtractVehicleRequest>(requestEndpoint, payload);
            _ = Task.Run(async () =>
            {
                try
                {
                    await _httpService.PostAsync<ExtractVehicleRequest>(requestEndpoint, payload);
                }
                catch (Exception ex)
                {
                    // 예외 로깅 가능
                    Debug.WriteLine(ex.Message);
                }
            });
            await Application.Current.Dispatcher.Invoke(async () =>
            {
                MessageBox.Show(Application.Current.MainWindow, $"AMR {vehicleName}(이)가 추출(Extraction)되었습니다.\r\nAMR 운용 담당자의 후속 조치가 필요합니다.", "AMR 추출", MessageBoxButton.OK, MessageBoxImage.Warning);
                });
            }
    }
}
