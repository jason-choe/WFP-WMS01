// Services/RobotMissionService.cs
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
using System.Windows; // Application.Current.Dispatcher.Invoke, MessageBox, MessageBoxImage를 위해 추가
using System.Configuration; // ConfigurationManager를 위해 추가
using System.IO.Ports; // Parity, StopBits를 위해 추가 (더 이상 직접 사용되지 않지만, 다른 곳에서 필요할 수 있으므로 유지)

namespace WPF_WMS01.Services
{
    /// <summary>
    /// 로봇 미션의 시작, 상태 폴링, 완료/실패 처리를 담당하는 서비스 클래스입니다.
    /// MainViewModel로부터 로봇 미션 관련 로직이 분리되었습니다.
    /// </summary>
    public class RobotMissionService : IRobotMissionService
    {
        private readonly HttpService _httpService;
        private readonly DatabaseService _databaseService;
        private DispatcherTimer _robotMissionPollingTimer;
        private readonly ModbusClientService _missionCheckModbusService; // 미션 실패 확인용 ModbusClientService

        // 현재 진행 중인 로봇 미션 프로세스들을 추적 (Key: ProcessId)
        private readonly Dictionary<string, RobotMissionInfo> _activeRobotProcesses = new Dictionary<string, RobotMissionInfo>();
        private readonly object _activeRobotProcessesLock = new object(); // _activeRobotProcesses 컬렉션에 대한 스레드 안전 잠금 객체

        // MainViewModel로부터 주입받을 종속성 (UI 업데이트 및 특정 값 조회용)
        private readonly string _waitRackTitle;
        private readonly char[] _militaryCharacter;
        private Func<string> _getInputStringForButtonFunc; // MainViewModel의 InputStringForButton 값을 가져오는 델리게이트
        private Func<int, RackViewModel> _getRackViewModelByIdFunc; // MainViewModel에서 RackViewModel을 ID로 가져오는 델리게이트

        // 미션 실패 확인용 Modbus 서버 설정 값 (TCP 고정)
        // 이 값들은 App.config에서 읽어오거나, 필요에 따라 하드코딩할 수 있습니다.
        // 현재는 App.config에서 읽어온 값을 생성자를 통해 주입받습니다.
        private readonly string _missionModbusIp;
        private readonly int _missionModbusPort;
        private readonly byte _missionModbusSlaveId;

        // RobotMissionService에서 발생하는 중요한 Modbus 오류 MessageBox가 이미 표시되었는지 추적하는 플래그
        private bool _hasMissionCriticalModbusErrorBeenDisplayed = false;

        /// <summary>
        /// MainViewModel로 상태를 다시 보고하기 위한 이벤트
        /// </summary>
        public event Action<string> OnShowAutoClosingMessage;
        public event Action<int, bool> OnRackLockStateChanged;
        public event Action OnInputStringForButtonCleared;
        public event Action<ushort> OnTurnOffAlarmLightRequest;
        public event Action<RobotMissionInfo> OnMissionProcessUpdated; // 새로운 이벤트 추가


        /// <summary>
        /// RobotMissionService의 새 인스턴스를 초기화합니다.
        /// 이 생성자는 미션 실패 확인을 위해 Modbus TCP 통신을 사용합니다.
        /// </summary>
        /// <param name="httpService">HTTP 통신을 위한 서비스 인스턴스.</param>
        /// <param name="databaseService">데이터베이스 접근을 위한 서비스 인스턴스.</param>
        /// <param name="waitRackTitle">WAIT 랙의 타이틀 문자열.</param>
        /// <param name="militaryCharacter">군수품 문자 배열.</param>
        /// <param name="getRackViewModelByIdFunc">Rack ID로 RackViewModel을 가져오는 함수.</param>
        /// <param name="missionModbusIp">미션 실패 확인용 Modbus 서버 IP 주소.</param>
        /// <param name="missionModbusPort">미션 실패 확인용 Modbus 서버 포트.</param>
        /// <param name="missionModbusSlaveId">미션 실패 확인용 Modbus 서버 슬레이브 ID.</param>
        public RobotMissionService(
            HttpService httpService,
            DatabaseService databaseService,
            string waitRackTitle,
            char[] militaryCharacter,
            Func<int, RackViewModel> getRackViewModelByIdFunc,
            string missionModbusIp,
            int missionModbusPort,
            byte missionModbusSlaveId)
        {
            _httpService = httpService ?? throw new ArgumentNullException(nameof(httpService));
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _waitRackTitle = waitRackTitle;
            _militaryCharacter = militaryCharacter;
            _getRackViewModelByIdFunc = getRackViewModelByIdFunc ?? throw new ArgumentNullException(nameof(getRackViewModelByIdFunc));

            // 미션 실패 확인용 Modbus 설정 (항상 TCP)
            _missionModbusIp = missionModbusIp;
            _missionModbusPort = missionModbusPort;
            _missionModbusSlaveId = missionModbusSlaveId;

            // 미션 실패 확인용 ModbusClientService 인스턴스 생성 (TCP 모드 고정)
            _missionCheckModbusService = new ModbusClientService(_missionModbusIp, _missionModbusPort, _missionModbusSlaveId);
            Debug.WriteLine($"[RobotMissionService] Mission Check Modbus Service Initialized for TCP: {_missionModbusIp}:{_missionModbusPort}, Slave ID: {_missionModbusSlaveId}");

            SetupRobotMissionPollingTimer(); // 로봇 미션 폴링 타이머 설정 및 시작
        }

        /// <summary>
        /// 로봇 미션 폴링 타이머를 설정하고 시작합니다.
        /// </summary>
        private void SetupRobotMissionPollingTimer()
        {
            _robotMissionPollingTimer = new DispatcherTimer();
            _robotMissionPollingTimer.Interval = TimeSpan.FromSeconds(5); // 5초마다 폴링 (조정 가능)
            _robotMissionPollingTimer.Tick += RobotMissionPollingTimer_Tick;
            _robotMissionPollingTimer.Start();
            Debug.WriteLine("[RobotMissionService] Robot Mission Polling Timer Started.");
        }

        /// <summary>
        /// 로봇 미션 상태를 주기적으로 폴링하고 처리합니다.
        /// </summary>
        private async void RobotMissionPollingTimer_Tick(object sender, EventArgs e)
        {
            // _activeRobotProcesses 컬렉션에 대한 스레드 안전 복사본 생성
            List<RobotMissionInfo> currentActiveProcesses;
            lock (_activeRobotProcessesLock)
            {
                currentActiveProcesses = _activeRobotProcesses.Values.ToList();
            }

            foreach (var processInfo in currentActiveProcesses)
            {
                // 이미 완료되었거나 실패한 미션은 폴링하지 않습니다.
                if (processInfo.IsFinished || processInfo.IsFailed)
                {
                    // 완료되거나 실패한 프로세스는 딕셔너리에서 제거합니다.
                    // Dictionary에서 제거는 lock 안에서 이루어져야 합니다.
                    lock (_activeRobotProcessesLock)
                    {
                        _activeRobotProcesses.Remove(processInfo.ProcessId);
                    }
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
                        var missionInfoResponse = await _httpService.GetAsync<GetMissionInfoResponse>($"wms/rest/v{_httpService.CurrentApiVersionMajor}.{_httpService.CurrentApiVersionMinor}/missions/{processInfo.LastSentMissionId.Value}").ConfigureAwait(false);

                        if (missionInfoResponse?.Payload?.Missions != null && missionInfoResponse.Payload.Missions.Any())
                        {
                            processInfo.PollingRetryCount = 0; // 성공적으로 응답 받으면 재시도 카운트 리셋

                            var latestMissionDetail = missionInfoResponse.Payload.Missions.First();
                            processInfo.CurrentMissionDetail = latestMissionDetail;

                            // UI 업데이트를 위한 이벤트 발생
                            MissionStatusEnum currentStatus;
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

                            // 진행률 업데이트 (전체 미션 단계 중 현재 완료된 단계의 비율)
                            double progressNumerator = (currentStatus == MissionStatusEnum.COMPLETED && processInfo.CurrentStepIndex <= processInfo.TotalSteps) ?
                                                       processInfo.CurrentStepIndex : Math.Max(0, processInfo.CurrentStepIndex - 1);
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

                                // IsLinkable이 false인 미션이 성공적으로 완료되면 DB 업데이트 및 랙 잠금 해제
                                if (!completedStepDefinition.IsLinkable)
                                {
                                    await PerformDbUpdateForCompletedStep(processInfo, completedStepDefinition);
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

        /// <summary>
        /// 새로운 로봇 미션 프로세스를 시작합니다.
        /// </summary>
        /// <param name="processType">미션 프로세스의 유형 (예: "WaitToWrapTransfer", "RackTransfer").</param>
        /// <param name="missionSteps">이 프로세스를 구성하는 순차적인 미션 단계 목록.</param>
        /// <param name="sourceRack">원본 랙 ViewModel (더 이상 사용되지 않음, MissionStepDefinition의 SourceRackId 사용).</param>
        /// <param name="destinationRack">목적지 랙 ViewModel (더 이상 사용되지 않음, MissionStepDefinition의 DestinationRackId 사용).</param>
        /// <param name="destinationLine">목적지 생산 라인 (선택 사항).</param>
        /// <param name="getInputStringForButtonFunc">MainViewModel의 InputStringForButton 값을 가져오는 델리게이트.</param>
        /// <param name="racksLockedByProcess">이 프로세스 시작 시 잠긴 모든 랙의 ID 목록.</param>
        /// <param name="racksToProcess">여러 랙을 처리할 경우 (예: 출고) 해당 랙들의 ViewModel 목록.</param>
        /// <param name="initiatingCoilAddress">이 미션을 시작한 Modbus Coil의 주소 (경광등 제어용).</param>
        /// <returns>시작된 미션 프로세스의 고유 ID.</returns>
        public async Task<string> InitiateRobotMissionProcess(
            string processType,
            List<MissionStepDefinition> missionSteps,
            RackViewModel sourceRack, // 이 파라미터는 더 이상 사용되지 않지만, IRobotMissionService 인터페이스 호환을 위해 유지.
            RackViewModel destinationRack, // 이 파라미터는 더 이상 사용되지 않지만, IRobotMissionService 인터페이스 호환을 위해 유지.
            Location destinationLine,
            Func<string> getInputStringForButtonFunc,
            List<int> racksLockedByProcess, // 새로 추가된 파라미터
            List<RackViewModel> racksToProcess = null, // 새로 추가된 파라미터
            ushort? initiatingCoilAddress = null) // 새로운 파라미터 추가
        {
            // MainViewModel로부터 받은 델리게이트를 저장하여 필요할 때 사용합니다.
            _getInputStringForButtonFunc = getInputStringForButtonFunc;

            // 새로운 미션이 시작될 때 Modbus 오류 플래그를 리셋합니다.
            _hasMissionCriticalModbusErrorBeenDisplayed = false;

            string processId = Guid.NewGuid().ToString(); // 고유한 프로세스 ID 생성
            var newMissionProcess = new RobotMissionInfo(processId, processType, missionSteps, racksLockedByProcess, initiatingCoilAddress) // 생성자에 racksLockedByProcess와 initiatingCoilAddress 전달
            {
                // SourceRack과 DestinationRack은 이제 MissionStepDefinition 내에서 ID로 관리됩니다.
                // 여기에 직접 ViewModel을 할당하지 않습니다.
                // SourceRack = sourceRack, // 제거
                // DestinationRack = destinationRack, // 제거
                RacksToProcess = racksToProcess ?? new List<RackViewModel>() // racksToProcess 설정
            };

            lock (_activeRobotProcessesLock) // _activeRobotProcesses에 대한 스레드 안전한 접근
            {
                _activeRobotProcesses.Add(processId, newMissionProcess);
            }
            Debug.WriteLine($"[RobotMissionService] Initiated new robot mission process: {processId} ({processType}). Total steps: {missionSteps.Count}");
            OnShowAutoClosingMessage?.Invoke($"로봇 미션 프로세스 시작: {processType} (ID: {processId})");

            // 첫 번째 미션 단계를 전송하고 추적을 시작합니다.
            // InitiateRobotMissionProcess는 이미 MainViewModel에서 async void로 호출되므로 여기서 await는 필요 없습니다.
            // 하지만, 이 메서드 자체는 Task<string>을 반환하므로, 내부적으로 await를 사용하는 것은 괜찮습니다.
            // 중요한 것은 SendAndTrackMissionStepsForProcess가 중복 실행되지 않도록 하는 것입니다.
            // _sendingMissionLock을 제거했으므로, 이제 타이머 틱과 InitiateRobotMissionProcess 호출이
            // 동시에 SendAndTrackMissionStepsForProcess를 호출할 수 있는 경쟁 조건이 다시 발생할 수 있습니다.
            // 이를 방지하기 위해, LastSentMissionId를 설정하는 부분을 SendAndTrackMissionStepsForProcess 내부에서
            // 미션 전송 성공 직후에 하도록 하고, LastSentMissionId가 null이 아닐 경우 SendAndTrackMissionStepsForProcess가
            // 아무것도 하지 않도록 하는 것이 중요합니다.
            await SendAndTrackMissionStepsForProcess(newMissionProcess);

            return processId;
        }

        /// <summary>
        /// 주어진 로봇 미션 프로세스에서 현재 단계의 미션을 ANT 서버에 전송하고 추적을 시작합니다.
        /// 현재 미션이 완료되면 이 메서드를 다시 호출하여 다음 미션을 전송합니다.
        /// </summary>
        /// <param name="missionInfo">현재 미션 프로세스 정보.</param>
        private async Task SendAndTrackMissionStepsForProcess(RobotMissionInfo missionInfo)
        {
            // 이 메서드가 이미 미션을 전송했거나, 미션이 완료/실패 상태라면 더 이상 진행하지 않습니다.
            // LastSentMissionId가 이미 설정되어 있고, 미션이 아직 완료/실패 상태가 아니라면 중복 전송을 방지합니다.
            if (missionInfo.LastSentMissionId.HasValue && missionInfo.CurrentStatus != MissionStatusEnum.COMPLETED && missionInfo.CurrentStatus != MissionStatusEnum.FAILED)
            {
                Debug.WriteLine($"[RobotMissionService] SendAndTrackMissionStepsForProcess: Mission for Process {missionInfo.ProcessId} (Step {missionInfo.CurrentStepIndex + 1}) already sent or in progress. Skipping.");
                return;
            }

            Debug.WriteLine($"[RobotMissionService] SendAndTrackMissionStepsForProcess called for Process {missionInfo.ProcessId}. Current Step: {missionInfo.CurrentStepIndex + 1}/{missionInfo.TotalSteps}. IsFailed: {missionInfo.IsFailed}");

            // 미션 프로세스가 이미 실패했거나 모든 단계를 완료했다면 더 이상 미션을 보내지 않습니다.
            if (missionInfo.IsFailed || missionInfo.CurrentStepIndex >= missionInfo.TotalSteps)
            {
                if (missionInfo.CurrentStepIndex >= missionInfo.TotalSteps && !missionInfo.IsFailed)
                {
                    Debug.WriteLine($"[RobotMissionService] Process {missionInfo.ProcessId}: All steps completed. Setting status to COMPLETED.");
                    missionInfo.HmiStatus.Status = MissionStatusEnum.COMPLETED.ToString();
                    missionInfo.HmiStatus.ProgressPercentage = 100;
                    OnShowAutoClosingMessage?.Invoke($"로봇 미션 프로세스 완료: {missionInfo.ProcessType} (ID: {missionInfo.ProcessId})");
                }
                await HandleRobotMissionCompletion(missionInfo);
                return;
            }

            var currentStep = missionInfo.MissionSteps[missionInfo.CurrentStepIndex];
            int? linkedMissionId = null;

            // "false 다음의 true인 mission 요청 시 LinkedMission을 null로 해달라."는 요청에 따라 로직 수정
            // 즉, 이전 단계가 IsLinkable=false 였다면, 현재 단계는 LinkedMission을 null로 설정
            if (missionInfo.CurrentStepIndex > 0)
            {
                var previousStepDefinition = missionInfo.MissionSteps[missionInfo.CurrentStepIndex - 1];
                if (previousStepDefinition.IsLinkable) // 이전 단계가 연결 가능했다면
                {
                    linkedMissionId = missionInfo.LastCompletedMissionId; // 이전에 완료된 미션 ID를 연결
                }
                // else: previousStepDefinition.IsLinkable이 false인 경우 linkedMissionId는 기본값인 null 유지
            }
            // 첫 번째 단계 (CurrentStepIndex == 0)는 항상 linkedMissionId가 null입니다.

            // Modbus Discrete Input 체크 로직 추가 (_missionCheckModbusService 사용)
            if (currentStep.CheckModbusDiscreteInput && currentStep.ModbusDiscreteInputAddressToCheck.HasValue)
            {
                try
                {
                    Debug.WriteLine($"[RobotMissionService] Checking Modbus Discrete Input {currentStep.ModbusDiscreteInputAddressToCheck.Value} before sending mission step.");

                    // 미션 체크용 Modbus 서비스 연결 시도 (필요 시)
                    if (!_missionCheckModbusService.IsConnected)
                    {
                        await _missionCheckModbusService.ConnectAsync().ConfigureAwait(false);
                    }

                    // Modbus 연결이 여전히 안 되어 있다면 예외 발생 (ModbusClientService에서 이미 던지도록 수정됨)
                    bool[] discreteInputStates = await _missionCheckModbusService.ReadDiscreteInputStatesAsync(currentStep.ModbusDiscreteInputAddressToCheck.Value, 1).ConfigureAwait(false);

                    if (discreteInputStates != null && discreteInputStates.Length > 0 && discreteInputStates[0])
                    {
                        // Discrete input이 1이므로 미션 단계 실패 처리
                        if (!_hasMissionCriticalModbusErrorBeenDisplayed) // 플래그 확인
                        {
                            _hasMissionCriticalModbusErrorBeenDisplayed = true; // MessageBox 표시 후 플래그 설정
                            await Application.Current.Dispatcher.Invoke(async () =>
                            {
                                MessageBox.Show(Application.Current.MainWindow, $"Modbus Discrete Input {currentStep.ModbusDiscreteInputAddressToCheck.Value}이(가) 1입니다. 미션 단계 '{currentStep.ProcessStepDescription}'을(를) 시작할 수 없습니다. 미션 프로세스를 취소합니다.", "미션 취소", MessageBoxButton.OK, MessageBoxImage.Warning);
                            });
                        }
                        Debug.WriteLine($"[RobotMissionService] Modbus Discrete Input {currentStep.ModbusDiscreteInputAddressToCheck.Value} is 1. Cancelling mission process {missionInfo.ProcessId}.");
                        missionInfo.CurrentStatus = MissionStatusEnum.FAILED;
                        missionInfo.HmiStatus.Status = "FAILED";
                        missionInfo.HmiStatus.ProgressPercentage = 100;
                        missionInfo.IsFailed = true; // 프로세스 실패 플래그 설정
                        missionInfo.CancellationTokenSource.Cancel(); // 미션 취소 요청
                        await HandleRobotMissionCompletion(missionInfo);
                        return; // 미션 단계 전송 중단
                    }
                    else
                    {
                        Debug.WriteLine($"[RobotMissionService] Modbus Discrete Input {currentStep.ModbusDiscreteInputAddressToCheck.Value} is 0. Proceeding with mission step.");
                    }
                }
                catch (InvalidOperationException ex) // ModbusClientService에서 던지는 연결 오류 예외
                {
                    if (!_hasMissionCriticalModbusErrorBeenDisplayed) // 플래그 확인
                    {
                        _hasMissionCriticalModbusErrorBeenDisplayed = true; // MessageBox 표시 후 플래그 설정
                        await Application.Current.Dispatcher.Invoke(async () =>
                        {
                            MessageBox.Show(Application.Current.MainWindow, $"Modbus 연결 오류: {ex.Message}. 미션 프로세스를 취소합니다.", "미션 취소", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                    Debug.WriteLine($"[RobotMissionService] Modbus Connection Error during check: {ex.Message}. Cancelling mission process {missionInfo.ProcessId}.");
                    missionInfo.CurrentStatus = MissionStatusEnum.FAILED;
                    missionInfo.HmiStatus.Status = "FAILED";
                    missionInfo.HmiStatus.ProgressPercentage = 100;
                    missionInfo.IsFailed = true; // 프로세스 실패 플래그 설정
                    missionInfo.CancellationTokenSource.Cancel(); // 미션 취소 요청
                    await HandleRobotMissionCompletion(missionInfo);
                    return; // 미션 단계 전송 중단
                }
                catch (Exception ex)
                {
                    // Modbus 읽기 중 오류 발생 시 미션 취소
                    if (!_hasMissionCriticalModbusErrorBeenDisplayed) // 플래그 확인
                    {
                        _hasMissionCriticalModbusErrorBeenDisplayed = true; // MessageBox 표시 후 플래그 설정
                        await Application.Current.Dispatcher.Invoke(async () =>
                        {
                            MessageBox.Show(Application.Current.MainWindow, $"Modbus Discrete Input {currentStep.ModbusDiscreteInputAddressToCheck.Value} 읽기 중 오류 발생: {ex.Message}. 미션 프로세스를 취소합니다.", "미션 취소", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                    Debug.WriteLine($"[RobotMissionService] Error reading Modbus Discrete Input {currentStep.ModbusDiscreteInputAddressToCheck.Value}: {ex.Message}. Cancelling mission process {missionInfo.ProcessId}.");
                    missionInfo.CurrentStatus = MissionStatusEnum.FAILED;
                    missionInfo.HmiStatus.Status = "FAILED";
                    missionInfo.HmiStatus.ProgressPercentage = 100;
                    missionInfo.IsFailed = true; // 프로세스 실패 플래그 설정
                    missionInfo.CancellationTokenSource.Cancel(); // 미션 취소 요청
                    await HandleRobotMissionCompletion(missionInfo);
                    return; // 미션 단계 전송 중단
                }
            }


            var missionRequest = new MissionRequest
            {
                RequestPayload = new MissionRequestPayload
                {
                    Requestor = "admin",
                    MissionType = currentStep.MissionType,
                    FromNode = currentStep.FromNode,
                    ToNode = currentStep.ToNode,
                    Cardinality = "1",
                    Priority = 2,
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

            try
            {
                // === 중단점: 미션 요청 엔드포인트 및 페이로드 ===
                string requestEndpoint = $"wms/rest/v{_httpService.CurrentApiVersionMajor}.{_httpService.CurrentApiVersionMinor}/missions";
                string requestPayloadJson = JsonConvert.SerializeObject(missionRequest, Formatting.Indented); // 가독성을 위해 Indented 포맷 사용
                Debug.WriteLine($"[RobotMissionService - BREAKPOINT] Sending Mission Request for Process {missionInfo.ProcessId}, Step {missionInfo.CurrentStepIndex + 1}:");
                Debug.WriteLine($"  Endpoint: {requestEndpoint}");
                Debug.WriteLine($"  Payload: {requestPayloadJson}");

                // CancellationToken을 PostAsync에 전달
                var missionResponse = await _httpService.PostAsync<MissionRequest, MissionResponse>(requestEndpoint, missionRequest, missionInfo.CancellationTokenSource.Token).ConfigureAwait(false);

                if (missionResponse?.ReturnCode == 0 && missionResponse.Payload?.AcceptedMissions != null && missionResponse.Payload.AcceptedMissions.Any())
                {
                    int acceptedMissionId = missionResponse.Payload.AcceptedMissions.First();
                    missionInfo.LastSentMissionId = acceptedMissionId; // 전송된 미션 ID 저장
                    // CurrentStepIndex는 이 미션이 완료될 때 (폴링 로직에서) 증가시킴

                    missionInfo.HmiStatus.Status = MissionStatusEnum.ACCEPTED.ToString();
                    missionInfo.CurrentStatus = MissionStatusEnum.ACCEPTED; // RobotMissionInfo 내부 상태 업데이트
                    missionInfo.HmiStatus.CurrentStepDescription = currentStep.ProcessStepDescription;
                    // 현재 전송된 단계까지의 진행률 (CurrentStepIndex는 다음 보낼 인덱스이므로 +1)
                    missionInfo.HmiStatus.ProgressPercentage = (int)(((double)(missionInfo.CurrentStepIndex + 1) / missionInfo.TotalSteps) * 100);
                    OnShowAutoClosingMessage?.Invoke($"로봇 미션 단계 전송 성공: {currentStep.ProcessStepDescription} (미션 ID: {acceptedMissionId})");

                    // 미션 프로세스 업데이트 이벤트 발생
                    OnMissionProcessUpdated?.Invoke(missionInfo);

                    Debug.WriteLine($"[RobotMissionService] Process {missionInfo.ProcessId}: Mission {acceptedMissionId} for step {missionInfo.CurrentStepIndex + 1}/{missionInfo.TotalSteps} sent successfully.");
                }
                else
                {
                    missionInfo.HmiStatus.Status = MissionStatusEnum.FAILED.ToString();
                    missionInfo.CurrentStatus = MissionStatusEnum.FAILED; // RobotMissionInfo 내부 상태 업데이트
                    OnShowAutoClosingMessage?.Invoke($"로봇 미션 단계 전송 실패: {currentStep.ProcessStepDescription}");
                    Debug.WriteLine($"[RobotMissionService] Process {missionInfo.ProcessId}: Mission step {currentStep.ProcessStepDescription} failed to be accepted. Return Code: {missionResponse?.ReturnCode}. " +
                                    $"Rejected: {string.Join(",", missionResponse?.Payload?.RejectedMissions ?? new List<int>())}");
                    missionInfo.IsFailed = true; // 프로세스 실패로 마크
                    await HandleRobotMissionCompletion(missionInfo); // 실패 처리
                }
            }
            catch (OperationCanceledException) // 취소 예외 처리
            {
                Debug.WriteLine($"[RobotMissionService] Process {missionInfo.ProcessId} cancelled during mission step send.");
                missionInfo.CurrentStatus = MissionStatusEnum.CANCELLED;
                missionInfo.HmiStatus.Status = MissionStatusEnum.CANCELLED.ToString();
                missionInfo.HmiStatus.ProgressPercentage = (int)(((double)(missionInfo.CurrentStepIndex + 1) / missionInfo.TotalSteps) * 100);
                OnShowAutoClosingMessage?.Invoke($"로봇 미션 프로세스 취소됨: {currentStep.ProcessStepDescription}");
                missionInfo.IsFailed = true; // 취소도 실패로 간주하여 처리
                await HandleRobotMissionCompletion(missionInfo);
            }
            catch (HttpRequestException httpEx)
            {
                missionInfo.HmiStatus.Status = MissionStatusEnum.FAILED.ToString();
                missionInfo.CurrentStatus = MissionStatusEnum.FAILED; // RobotMissionInfo 내부 상태 업데이트
                OnShowAutoClosingMessage?.Invoke($"로봇 미션 단계 전송 HTTP 오류: {httpEx.Message.Substring(0, Math.Min(100, httpEx.Message.Length))}");
                Debug.WriteLine($"[RobotMissionService] Process {missionInfo.ProcessId}: HTTP Request Error sending mission step {currentStep.ProcessStepDescription}: {httpEx.Message}");
                missionInfo.IsFailed = true; // 프로세스 실패로 간주
                await HandleRobotMissionCompletion(missionInfo); // 실패 처리
            }
            catch (Exception ex)
            {
                missionInfo.HmiStatus.Status = MissionStatusEnum.FAILED.ToString();
                missionInfo.CurrentStatus = MissionStatusEnum.FAILED; // RobotMissionInfo 내부 상태 업데이트
                OnShowAutoClosingMessage?.Invoke($"로봇 미션 단계 전송 중 예상치 못한 오류: {ex.Message.Substring(0, Math.Min(100, ex.Message.Length))}");
                Debug.WriteLine($"[RobotMissionService] Process {missionInfo.ProcessId}: Unexpected Error sending mission step {currentStep.ProcessStepDescription}: {ex.Message}");
                missionInfo.IsFailed = true; // 예상치 못한 오류도 프로세스 실패로 간주
                await HandleRobotMissionCompletion(missionInfo); // 실패 처리
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
                lock (_activeRobotProcessesLock)
                {
                    if (_activeRobotProcesses.ContainsKey(missionInfo.ProcessId))
                    {
                        _activeRobotProcesses.Remove(missionInfo.ProcessId);
                        Debug.WriteLine($"[RobotMissionService] Process {missionInfo.ProcessId} explicitly removed from active processes.");
                    }
                }
                // 최종 상태 업데이트를 팝업에 알림 (팝업이 닫히지 않고 최종 상태를 표시하도록)
                OnMissionProcessUpdated?.Invoke(missionInfo);
            }
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
                // 특별한 경우는 1) source rack이 WAIT rack일 경우는 BulletType과 Lot No.를 InputStringForButton으로 부터 얻는 것과,
                // 2) ProcessType이 "FakeExecuteInboundProduct" 또는 "HandleHalfPalletExport" RackType이 바뀌는 경우 뿐이다.

                if (sourceRackVm != null && destinationRackVm != null)
                {
                    // 1. Destination Rack에 Source Rack의 제품 정보 복사
                    string sourceLotNumber;
                    int sourceBulletType = sourceRackVm.BulletType;
                    int newDestinationRackType = destinationRackVm.RackType; // 기본적으로 목적지 랙의 현재 RackType 유지
                    int newSourceRackType = sourceRackVm.RackType; // 기본적으로 현재 RackType 유지

                    // 특별한 경우 1): source rack이 WAIT rack일 경우
                    if (sourceRackVm.Title.Equals(_waitRackTitle) && _getInputStringForButtonFunc != null)
                    {
                        sourceLotNumber = _getInputStringForButtonFunc.Invoke().TrimStart().TrimEnd(_militaryCharacter);
                        // BulletType은 이미 MainViewModel에서 WAIT 랙에 설정된 상태이므로 sourceRackVm.BulletType 사용
                    }
                    else
                    {
                        sourceLotNumber = sourceRackVm.LotNumber;
                    }

                    // 특별한 경우 2): ProcessType이 "FakeExecuteInboundProduct"일 경우 RackType 변경
                    if (processInfo.ProcessType == "FakeExecuteInboundProduct")
                    {
                        newDestinationRackType = 3; // 재공품 랙 타입으로 변경 (완제품 랙에서 재공품 랙으로)
                    }
                    else if (processInfo.ProcessType == "HandleHalfPalletExport")
                    {
                        newSourceRackType = 1;
                    }

                    await _databaseService.UpdateRackStateAsync(
                        destinationRackVm.Id,
                        newDestinationRackType,
                        sourceBulletType
                    );
                    await _databaseService.UpdateLotNumberAsync(
                        destinationRackVm.Id,
                        sourceLotNumber
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
                        String.Empty // LotNumber 비움
                    );
                    if (sourceRackVm.Title.Equals(_waitRackTitle))
                    {
                        OnInputStringForButtonCleared?.Invoke(); // WAIT 랙 비우면 입력 필드 초기화
                        Debug.WriteLine($"[RobotMissionService] DB Update: WAIT rack {sourceRackVm.Title} cleared.");
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
                    if (processInfo.ProcessType == "HandleHalfPalletExport")
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
                        String.Empty // LotNumber 비움
                    );
                    Debug.WriteLine($"[RobotMissionService] DB Update: Rack {sourceRackVm.Title} (ID: {sourceRackVm.Id}) cleared for {processInfo.ProcessType}.");

                    // 랙 잠금 해제
                    await _databaseService.UpdateIsLockedAsync(sourceRackVm.Id, false);
                    Application.Current.Dispatcher.Invoke(() => OnRackLockStateChanged?.Invoke(sourceRackVm.Id, false));
                    Debug.WriteLine($"[RobotMissionService] Rack {sourceRackVm.Title} unlocked.");

                    OnShowAutoClosingMessage?.Invoke($"랙 {sourceRackVm.Title} {processInfo.ProcessType} 성공.");
                }
                else if (processInfo.RacksToProcess != null && processInfo.RacksToProcess.Any() && processInfo.ProcessType == "ExecuteCheckoutProduct")
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

        /// <summary>
        /// 서비스 자원을 해제합니다.
        /// </summary>
        public void Dispose()
        {
            _robotMissionPollingTimer?.Stop();
            _robotMissionPollingTimer.Tick -= RobotMissionPollingTimer_Tick;

            lock (_activeRobotProcessesLock)
            {
                foreach (var processInfo in _activeRobotProcesses.Values)
                {
                    processInfo.CancellationTokenSource?.Cancel();
                    processInfo.CancellationTokenSource?.Dispose();
                }
                _activeRobotProcesses.Clear(); // 딕셔너리 비우기
            }
            _missionCheckModbusService?.Dispose(); // 미션 체크용 Modbus 서비스도 해제
            Debug.WriteLine("[RobotMissionService] Disposed.");
        }

        // IRobotMissionService 인터페이스의 나머지 메서드 구현
        public Task UpdateHmiStatus(string processId, string status, int progressPercentage)
        {
            lock (_activeRobotProcessesLock)
            {
                if (_activeRobotProcesses.TryGetValue(processId, out var processInfo))
                {
                    processInfo.HmiStatus.Status = status;
                    processInfo.HmiStatus.ProgressPercentage = progressPercentage;
                    // UI 업데이트를 위해 MainViewModel에 이벤트를 발생시킬 수 있음
                    // 예: OnShowAutoClosingMessage?.Invoke($"HMI Status Update: {status} {progressPercentage}%");
                }
            }
            return Task.CompletedTask;
        }

        public Task CompleteMissionStep(string processId, int stepIndex, MissionStatusEnum status, string message = null)
        {
            lock (_activeRobotProcessesLock)
            {
                if (_activeRobotProcesses.TryGetValue(processId, out var processInfo))
                {
                    // 이 메서드는 외부에서 강제로 단계를 완료/실패 처리할 때 사용될 수 있습니다.
                    // 현재 폴링 로직에서 대부분의 상태 변경을 처리하므로, 필요에 따라 구현을 조정합니다.
                    Debug.WriteLine($"[RobotMissionService] External CompleteMissionStep called for Process {processId}, Step {stepIndex} with status {status}. Message: {message}");
                    processInfo.CurrentStepIndex = stepIndex; // 강제로 단계 인덱스 설정
                    processInfo.HmiStatus.Status = status.ToString();
                    processInfo.HmiStatus.CurrentStepDescription = message ?? $"단계 {stepIndex} 완료/실패";
                    processInfo.HmiStatus.ProgressPercentage = (int)(((double)(stepIndex + 1) / processInfo.TotalSteps) * 100);
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
            }
            return Task.CompletedTask;
        }
    }
}
