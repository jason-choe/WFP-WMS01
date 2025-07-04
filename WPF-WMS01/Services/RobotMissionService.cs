// Services/RobotMissionService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading; // DispatcherTimer 사용을 위해 필요
using System.Diagnostics; // Debug.WriteLine을 위해 필요
using WPF_WMS01.Models; // RobotMissionInfo, MissionStepDefinition 등 모델 참조
using WPF_WMS01.ViewModels; // RackViewModel 참조를 위해 필요
using System.Net.Http; // HttpRequestException을 위해 필요
using Newtonsoft.Json; // JsonConvert를 위해 필요
using JsonException = Newtonsoft.Json.JsonException; // 충돌 방지를 위해 별칭 사용

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

        // 현재 진행 중인 로봇 미션 프로세스들을 추적 (Key: ProcessId)
        private readonly Dictionary<string, RobotMissionInfo> _activeRobotProcesses = new Dictionary<string, RobotMissionInfo>();
        private readonly object _activeRobotProcessesLock = new object(); // _activeRobotProcesses 컬렉션에 대한 스레드 안전 잠금 객체

        // MainViewModel로부터 주입받을 종속성 (UI 업데이트 및 특정 값 조회용)
        private readonly string _waitRackTitle;
        private readonly char[] _militaryCharacter;
        private Func<string> _getInputStringForButtonFunc; // MainViewModel의 InputStringForButton 값을 가져오는 델리게이트

        // MainViewModel로 상태를 다시 보고하기 위한 이벤트
        public event Action<string> OnShowAutoClosingMessage;
        public event Action<int, bool> OnRackLockStateChanged;
        public event Action OnInputStringForButtonCleared;

        /// <summary>
        /// RobotMissionService의 새 인스턴스를 초기화합니다.
        /// </summary>
        /// <param name="httpService">HTTP 통신을 위한 서비스 인스턴스.</param>
        /// <param name="databaseService">데이터베이스 접근을 위한 서비스 인스턴스.</param>
        /// <param name="waitRackTitle">WAIT 랙의 타이틀 문자열.</param>
        /// <param name="militaryCharacter">군수품 문자 배열.</param>
        public RobotMissionService(HttpService httpService, DatabaseService databaseService, string waitRackTitle, char[] militaryCharacter)
        {
            _httpService = httpService ?? throw new ArgumentNullException(nameof(httpService));
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _waitRackTitle = waitRackTitle;
            _militaryCharacter = militaryCharacter;

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

                // 현재 추적 중인 미션 (LastSentMissionId)이 있을 경우에만 폴링을 시도
                if (processInfo.LastSentMissionId.HasValue)
                {
                    try
                    {
                        var missionInfoResponse = await _httpService.GetAsync<GetMissionInfoResponse>($"wms/rest/v{_httpService.CurrentApiVersionMajor}.{_httpService.CurrentApiVersionMinor}/missions/{processInfo.LastSentMissionId.Value}").ConfigureAwait(false);

                        if (missionInfoResponse?.Payload?.Missions != null && missionInfoResponse.Payload.Missions.Any())
                        {
                            var latestMissionDetail = missionInfoResponse.Payload.Missions.First();
                            processInfo.CurrentMissionDetail = latestMissionDetail;

                            // UI 업데이트를 위한 이벤트 발생
                            MissionStatusEnum currentStatus;
                            switch (latestMissionDetail.NavigationState)
                            {
                                case 0: currentStatus = MissionStatusEnum.RECEIVED; break;  //  Accepted (=planned).
                                case 1: currentStatus = MissionStatusEnum.ACCEPTED; break;
                                case 2: currentStatus = MissionStatusEnum.REJECTED; break;
                                case 3: currentStatus = MissionStatusEnum.STARTED; break;   // Started (=running)
                                case 4: currentStatus = MissionStatusEnum.COMPLETED; break; // navigationstate 4를 COMPLETED로 처리 // Terminated (=successful).
                                case 5: currentStatus = MissionStatusEnum.CANCELLED; break;
                                default: currentStatus = MissionStatusEnum.FAILED; break;  // 지원하지 않는 알 수 없는 상태
                            }
                            processInfo.HmiStatus.Status = currentStatus.ToString();

                            // 진행률 업데이트 (전체 미션 단계 중 현재 완료된 단계의 비율)
                            double progressNumerator = (currentStatus == MissionStatusEnum.COMPLETED && processInfo.CurrentStepIndex <= processInfo.TotalSteps) ?
                                                       processInfo.CurrentStepIndex : Math.Max(0, processInfo.CurrentStepIndex - 1);
                            processInfo.HmiStatus.ProgressPercentage = (int)((progressNumerator / processInfo.TotalSteps) * 100);

                            // 현재 진행 중인 미션 단계의 설명
                            if (currentStatus == MissionStatusEnum.COMPLETED && processInfo.CurrentStepIndex < processInfo.TotalSteps)
                            {
                                // 현재 미션이 완료되었고 다음 미션이 남아있다면, 다음 미션 설명을 표시
                                processInfo.HmiStatus.CurrentStepDescription = processInfo.MissionSteps[processInfo.CurrentStepIndex].ProcessStepDescription;
                            }
                            else if (processInfo.CurrentStepIndex > 0 && processInfo.CurrentStepIndex <= processInfo.TotalSteps)
                            {
                                // 현재 미션이 아직 완료되지 않았거나 마지막 미션인 경우, 현재 미션 설명을 표시
                                processInfo.HmiStatus.CurrentStepDescription = processInfo.MissionSteps[processInfo.CurrentStepIndex - 1].ProcessStepDescription;
                            }
                            else if (processInfo.TotalSteps > 0 && processInfo.CurrentStepIndex == 0 && currentStatus != MissionStatusEnum.COMPLETED)
                            {
                                // 아직 첫 미션이 진행중 (Accepted/Started)
                                processInfo.HmiStatus.CurrentStepDescription = processInfo.MissionSteps[0].ProcessStepDescription;
                            }
                            else
                            {
                                processInfo.HmiStatus.CurrentStepDescription = "No mission steps defined or process completed.";
                            }
                            OnShowAutoClosingMessage?.Invoke($"로봇 미션 단계 업데이트: {processInfo.HmiStatus.CurrentStepDescription} ({processInfo.HmiStatus.ProgressPercentage}%)");


                            Debug.WriteLine($"[RobotMissionService] Process {processInfo.ProcessId} - Polling Mission {latestMissionDetail.MissionId}: " +
                                            $"Status: {processInfo.HmiStatus.Status}, Progress: {processInfo.HmiStatus.ProgressPercentage}%. Desc: {processInfo.HmiStatus.CurrentStepDescription}");
                            Debug.WriteLine($"[RobotMissionService DEBUG] Polled Mission Detail for {latestMissionDetail.MissionId}:");
                            Debug.WriteLine($"  NavigationState: {latestMissionDetail.NavigationState}");
                            Debug.WriteLine($"  State: {latestMissionDetail.State}");
                            Debug.WriteLine($"  TransportState: {latestMissionDetail.TransportState}");
                            Debug.WriteLine($"  PayloadStatus: {latestMissionDetail.PayloadStatus}");
                            Debug.WriteLine($"  AssignedTo: {latestMissionDetail.AssignedTo}");
                            Debug.WriteLine($"  ParametersJson: {latestMissionDetail.ParametersJson}");

                            // 현재 폴링 중인 미션이 완료되면, 다음 미션을 전송
                            if (latestMissionDetail.NavigationState == (int)MissionStatusEnum.COMPLETED || latestMissionDetail.NavigationState == 4)
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

                                // 미션 단계 인덱스 증가
                                processInfo.CurrentStepIndex++;
                                OnShowAutoClosingMessage?.Invoke($"로봇 미션 단계 완료: {processInfo.HmiStatus.CurrentStepDescription}");

                                Debug.WriteLine($"[RobotMissionService] Process {processInfo.ProcessId} - CurrentStepIndex incremented to: {processInfo.CurrentStepIndex}. Total steps: {processInfo.TotalSteps}. Calling SendAndTrackMissionStepsForProcess.");

                                // 다음 단계로 진행 (다음 미션 전송)
                                await SendAndTrackMissionStepsForProcess(processInfo);
                            }
                            else if (latestMissionDetail.NavigationState == (int)MissionStatusEnum.FAILED || latestMissionDetail.NavigationState == (int)MissionStatusEnum.REJECTED || latestMissionDetail.NavigationState == (int)MissionStatusEnum.CANCELLED)
                            {
                                Debug.WriteLine($"[RobotMissionService] Mission {latestMissionDetail.MissionId} FAILED/REJECTED/CANCELLED. Process {processInfo.ProcessId} marked as FAILED.");
                                processInfo.HmiStatus.Status = latestMissionDetail.NavigationState == (int)MissionStatusEnum.REJECTED ? MissionStatusEnum.REJECTED.ToString() :
                                                                   latestMissionDetail.NavigationState == (int)MissionStatusEnum.CANCELLED ? MissionStatusEnum.CANCELLED.ToString() :
                                                                   MissionStatusEnum.FAILED.ToString();
                                OnShowAutoClosingMessage?.Invoke($"로봇 미션 {processInfo.ProcessType} (ID: {processInfo.ProcessId}) 실패: {latestMissionDetail.MissionId}. 남은 미션 취소.");
                                processInfo.IsFailed = true; // 프로세스를 실패로 마크
                                await HandleRobotMissionCompletion(processInfo); // 실패 처리도 완료 처리로 간주
                            }
                        }
                        else
                        {
                            Debug.WriteLine($"[RobotMissionService] Mission {processInfo.LastSentMissionId} not found or no missions in payload. Potentially completed or invalid ID.");
                            // 미션이 없거나 페이로드가 비어있지만, 프로세스 전체가 완료되지 않았다면 오류로 간주
                            if (processInfo.LastSentMissionId.HasValue && !processInfo.IsFinished && !processInfo.IsFailed)
                            {
                                processInfo.HmiStatus.Status = MissionStatusEnum.FAILED.ToString();
                                OnShowAutoClosingMessage?.Invoke($"로봇 미션 {processInfo.ProcessType} (ID: {processInfo.ProcessId}) 폴링 실패: 미션 {processInfo.LastSentMissionId.Value} 정보를 찾을 수 없습니다.");
                                processInfo.IsFailed = true;
                                await HandleRobotMissionCompletion(processInfo);
                            }
                        }
                    }
                    catch (HttpRequestException httpEx)
                    {
                        Debug.WriteLine($"[RobotMissionService] HTTP Request Error for mission {processInfo.LastSentMissionId}: {httpEx.Message}");
                        processInfo.HmiStatus.Status = MissionStatusEnum.FAILED.ToString();
                        OnShowAutoClosingMessage?.Invoke($"로봇 미션 상태 폴링 실패 (HTTP 오류): {httpEx.Message.Substring(0, Math.Min(100, httpEx.Message.Length))}");
                        processInfo.IsFailed = true; // 폴링 실패도 프로세스 실패로 간주
                        await HandleRobotMissionCompletion(processInfo);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[RobotMissionService] Error polling mission status for process {processInfo.ProcessId}: {ex.Message}");
                        processInfo.HmiStatus.Status = MissionStatusEnum.FAILED.ToString();
                        OnShowAutoClosingMessage?.Invoke($"로봇 미션 상태 폴링 중 예상치 못한 오류: {ex.Message.Substring(0, Math.Min(100, ex.Message.Length))}");
                        processInfo.IsFailed = true; // 예상치 못한 오류도 프로세스 실패로 간주
                        await HandleRobotMissionCompletion(processInfo);
                    }
                }
                else
                {
                    // LastSentMissionId가 아직 설정되지 않은 경우 (첫 번째 단계가 아직 보내지지 않은 경우)
                    if (processInfo.CurrentStepIndex == 0 && processInfo.MissionSteps.Any() && !processInfo.IsFailed)
                    {
                        Debug.WriteLine($"[RobotMissionService] Process {processInfo.ProcessId} - First step pending. Attempting to send initial mission.");
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
        /// <param name="sourceRack">원본 랙 ViewModel.</param>
        /// <param name="destinationRack">목적지 랙 ViewModel.</param>
        /// <param name="destinationLine">목적지 생산 라인.</param>
        /// <param name="getInputStringForButtonFunc">MainViewModel에서 InputStringForButton 값을 가져오는 함수.</param>
        /// <returns>시작된 미션 프로세스의 고유 ID.</returns>
        public async Task<string> InitiateRobotMissionProcess(
            string processType,
            List<MissionStepDefinition> missionSteps,
            RackViewModel sourceRack,
            RackViewModel destinationRack,
            Location destinationLine,
            Func<string> getInputStringForButtonFunc)
        {
            // MainViewModel로부터 받은 델리게이트를 저장하여 필요할 때 사용합니다.
            _getInputStringForButtonFunc = getInputStringForButtonFunc;

            string processId = Guid.NewGuid().ToString(); // 고유한 프로세스 ID 생성
            var newMissionProcess = new RobotMissionInfo(processId, processType, missionSteps)
            {
                SourceRack = sourceRack,
                DestinationRack = destinationRack,
                DestinationLine = destinationLine
            };

            lock (_activeRobotProcessesLock) // _activeRobotProcesses에 대한 스레드 안전한 접근
            {
                _activeRobotProcesses.Add(processId, newMissionProcess);
            }
            Debug.WriteLine($"[RobotMissionService] Initiated new robot mission process: {processId} ({processType}). Total steps: {missionSteps.Count}");
            OnShowAutoClosingMessage?.Invoke($"로봇 미션 프로세스 시작: {processType} (ID: {processId})");

            // 첫 번째 미션 단계를 전송하고 추적을 시작합니다.
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
                if (previousStepDefinition.IsLinkable)
                {
                    // 이전 단계가 연결 가능했다면, 이전에 완료된 미션 ID를 연결
                    linkedMissionId = missionInfo.LastCompletedMissionId;
                }
                // else: previousStepDefinition.IsLinkable이 false인 경우 linkedMissionId는 기본값인 null 유지
            }
            // 첫 번째 단계 (CurrentStepIndex == 0)는 항상 linkedMissionId가 null입니다.

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
                            IsLinkable = currentStep.IsLinkable,
                            LinkWaitTimeout = currentStep.LinkWaitTimeout,
                            LinkedMission = linkedMissionId // 이전 미션의 ID를 연결
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

                var missionResponse = await _httpService.PostAsync<MissionRequest, MissionResponse>(requestEndpoint, missionRequest).ConfigureAwait(false);

                if (missionResponse?.ReturnCode == 0 && missionResponse.Payload?.AcceptedMissions != null && missionResponse.Payload.AcceptedMissions.Any())
                {
                    int acceptedMissionId = missionResponse.Payload.AcceptedMissions.First();
                    missionInfo.LastSentMissionId = acceptedMissionId; // 전송된 미션 ID 저장
                    // CurrentStepIndex는 이 미션이 완료될 때 (폴링 로직에서) 증가시킴

                    missionInfo.HmiStatus.Status = MissionStatusEnum.ACCEPTED.ToString();
                    missionInfo.HmiStatus.CurrentStepDescription = currentStep.ProcessStepDescription;
                    // 현재 전송된 단계까지의 진행률 (CurrentStepIndex는 다음 보낼 인덱스이므로 +1)
                    missionInfo.HmiStatus.ProgressPercentage = (int)(((double)(missionInfo.CurrentStepIndex + 1) / missionInfo.TotalSteps) * 100);
                    OnShowAutoClosingMessage?.Invoke($"로봇 미션 단계 전송 성공: {currentStep.ProcessStepDescription} (미션 ID: {acceptedMissionId})");

                    Debug.WriteLine($"[RobotMissionService] Process {missionInfo.ProcessId}: Mission {acceptedMissionId} for step {missionInfo.CurrentStepIndex + 1}/{missionInfo.TotalSteps} sent successfully.");
                }
                else
                {
                    missionInfo.HmiStatus.Status = MissionStatusEnum.FAILED.ToString();
                    OnShowAutoClosingMessage?.Invoke($"로봇 미션 단계 전송 실패: {currentStep.ProcessStepDescription}");
                    Debug.WriteLine($"[RobotMissionService] Process {missionInfo.ProcessId}: Mission step {currentStep.ProcessStepDescription} failed to be accepted. Return Code: {missionResponse?.ReturnCode}. " +
                                    $"Rejected: {string.Join(",", missionResponse?.Payload?.RejectedMissions ?? new List<int>())}");
                    missionInfo.IsFailed = true; // 프로세스 실패로 마크
                    await HandleRobotMissionCompletion(missionInfo); // 실패 처리
                }
            }
            catch (HttpRequestException httpEx)
            {
                missionInfo.HmiStatus.Status = MissionStatusEnum.FAILED.ToString();
                OnShowAutoClosingMessage?.Invoke($"로봇 미션 단계 전송 HTTP 오류: {httpEx.Message.Substring(0, Math.Min(100, httpEx.Message.Length))}");
                Debug.WriteLine($"[RobotMissionService] Process {missionInfo.ProcessId}: HTTP Request Error sending mission step {currentStep.ProcessStepDescription}: {httpEx.Message}");
                missionInfo.IsFailed = true; // 프로세스 실패로 마크
                await HandleRobotMissionCompletion(missionInfo); // 실패 처리
            }
            catch (Exception ex)
            {
                missionInfo.HmiStatus.Status = MissionStatusEnum.FAILED.ToString();
                OnShowAutoClosingMessage?.Invoke($"로봇 미션 단계 전송 중 예상치 못한 오류: {ex.Message.Substring(0, Math.Min(100, ex.Message.Length))}");
                Debug.WriteLine($"[RobotMissionService] Process {missionInfo.ProcessId}: Unexpected Error sending mission step {currentStep.ProcessStepDescription}: {ex.Message}");
                missionInfo.IsFailed = true; // 프로세스 실패로 마크
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

            // 현재는 "WaitToWrapTransfer" 프로세스에 대한 완료 처리만 포함.
            // 다른 프로세스 유형에 대한 완료 로직은 필요에 따라 추가
            //if (missionInfo.ProcessType == "WaitToWrapTransfer" || missionInfo.ProcessType == "HandleRackTransfer" || missionInfo.ProcessType == "HandleRackShipout")
            {
                var sourceRackVm = missionInfo.SourceRack;
                var destinationRackVm = missionInfo.DestinationRack; // WRAP 랙 또는 다른 저장 랙

                if (sourceRackVm != null)
                {
                    // 미션이 성공적으로 완료되었을 때만 DB 업데이트 수행
                    if (missionInfo.HmiStatus.Status == MissionStatusEnum.COMPLETED.ToString() && !missionInfo.IsFailed)
                    {
                        try
                        {
                            if (destinationRackVm != null)
                            {
                                // 1) WRAP 랙으로 제품 정보 이동 (BulletType, LotNumber)
                                // WAIT 랙의 LotNumber는 InputStringForButton에서 가져옴.
                                string originalSourceLotNumber = sourceRackVm.Title.Equals(_waitRackTitle) ?
                                    _getInputStringForButtonFunc?.Invoke().TrimStart().TrimEnd(_militaryCharacter) : sourceRackVm.LotNumber;
                                int originalSourceBulletType = sourceRackVm.BulletType;

                                await _databaseService.UpdateRackStateAsync(
                                    destinationRackVm.Id,
                                    missionInfo.ProcessType == "FakeExecuteInboundProduct" ? 3 : destinationRackVm.RackType,
                                    originalSourceBulletType
                                );
                                await _databaseService.UpdateLotNumberAsync(
                                    destinationRackVm.Id,
                                    originalSourceLotNumber
                                );
                                Debug.WriteLine($"[RobotMissionService] DB Update: Rack {destinationRackVm.Title} updated with BulletType {originalSourceBulletType}, LotNumber {originalSourceLotNumber}.");

                                // 2) 원본 랙 비우기
                                await _databaseService.UpdateRackStateAsync(
                                    sourceRackVm.Id,
                                    missionInfo.ProcessType == "HandleHalfPalletExport" ? 1 : sourceRackVm.RackModel.RackType,
                                    0 // BulletType을 0으로 설정 (비움)
                                );
                                await _databaseService.UpdateLotNumberAsync(
                                    sourceRackVm.Id,
                                    String.Empty // LotNumber 비움
                                );
                                // WAIT 랙이 비워지면 입력 필드도 초기화 (WAIT 랙에 대해서만)
                                if (sourceRackVm.Title.Equals(_waitRackTitle))
                                {
                                    OnInputStringForButtonCleared?.Invoke(); // MainViewModel에 InputStringForButton 초기화 요청
                                    Debug.WriteLine($"[RobotMissionService] DB Update: WAIT rack {sourceRackVm.Title} cleared.");
                                }

                                OnShowAutoClosingMessage?.Invoke($"로봇 미션 완료! 랙 {sourceRackVm.Title}에서 랙 {destinationRackVm.Title}으로 이동 성공.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[RobotMissionService] DB update failed after robot mission completion: {ex.Message}");
                            OnShowAutoClosingMessage?.Invoke($"로봇 미션 완료 후 DB 업데이트 실패: {ex.Message.Substring(0, Math.Min(100, ex.Message.Length))}");
                        }
                    }
                    else // 미션 실패 시
                    {
                        OnShowAutoClosingMessage?.Invoke($"로봇 미션 실패! 랙 {sourceRackVm.Title}에서 랙 {destinationRackVm.Title}으로 이동 실패..");
                    }

                    // 미션 완료/실패 여부와 관계없이 랙 잠금 해제
                    try
                    {
                        await _databaseService.UpdateIsLockedAsync(sourceRackVm.Id, false);
                        OnRackLockStateChanged?.Invoke(sourceRackVm.Id, false); // MainViewModel에 잠금 해제 알림

                        if (destinationRackVm != null)
                        {
                            await _databaseService.UpdateIsLockedAsync(destinationRackVm.Id, false);
                            OnRackLockStateChanged?.Invoke(destinationRackVm.Id, false); // MainViewModel에 잠금 해제 알림
                        }
                        Debug.WriteLine($"[RobotMissionService] Racks {sourceRackVm.Title} and {(destinationRackVm != null ? destinationRackVm.Title : "N/A")} unlocked.");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[RobotMissionService] Failed to unlock racks after robot mission: {ex.Message}");
                        OnShowAutoClosingMessage?.Invoke($"랙 잠금 해제 실패: {ex.Message.Substring(0, Math.Min(100, ex.Message.Length))}");
                    }
                }
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
            Debug.WriteLine("[RobotMissionService] Disposed.");
        }
    }
}
