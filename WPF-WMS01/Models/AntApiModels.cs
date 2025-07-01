// Models/AntApiModels.cs
using System;
using Newtonsoft.Json;
using System.Collections.Generic; // List<T> 사용을 위해 추가
using System.ComponentModel; // INotifyPropertyChanged 사용을 위해 추가
using System.Runtime.CompilerServices; // CallerMemberName 사용을 위해 추가
using System.Threading; // CancellationTokenSource를 위해 추가

namespace WPF_WMS01.Models
{
    // ViewModelBase for common INotifyPropertyChanged implementation
    public class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    // ====== 1. Login (POST) - Appendix G.1 (p. 427) ======
    public class ApiVersion
    {
        [JsonProperty("major")]
        public int Major { get; set; }

        [JsonProperty("minor")]
        public int Minor { get; set; }
    }

    public class LoginRequest
    {
        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("password")]
        public string Password { get; set; }

        [JsonProperty("apiVersion")]
        public ApiVersion ApiVersion { get; set; } // 요청 시 보낼 apiVersion
    }

    public class LoginResponse
    {
        [JsonProperty("firstName")]
        public string FirstName { get; set; }

        [JsonProperty("apiVersion")]
        public string ApiVersionString { get; set; } // JSON 필드는 "apiVersion"이지만, C# 속성 이름은 충돌을 피하기 위해 변경

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("familyName")]
        public string FamilyName { get; set; }

        [JsonProperty("roles")]
        public List<string> Roles { get; set; }

        [JsonProperty("token")]
        public string Token { get; set; }

        [JsonProperty("tokenExpiryTime")]
        public long TokenExpiryTime { get; set; } // Unix Timestamp (milliseconds)
    }

    // ====== 2. Create Mission (POST) - Appendix G.3 (p. 433) ======
    public class MissionRequestParameterValue
    {
        [JsonProperty("payload")]
        public string Payload { get; set; } // AMR_1, AMR_2 등

        [JsonProperty("linkedMission")]
        public int? LinkedMission { get; set; } // 이전 미션의 ID (선택 사항)

        [JsonProperty("isLinkable")]
        public bool IsLinkable { get; set; }

        [JsonProperty("linkWaitTimeout")]
        public int LinkWaitTimeout { get; set; } // 초 단위
    }

    public class MissionRequestParameters
    {
        [JsonProperty("value")]
        public MissionRequestParameterValue Value { get; set; }

        [JsonProperty("desc")]
        public string Description { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public class MissionRequestPayload
    {
        [JsonProperty("requestor")]
        public string Requestor { get; set; }

        [JsonProperty("missiontype")]
        public string MissionType { get; set; } // 예: "8" (TurnRack)

        [JsonProperty("tonode")]
        public string ToNode { get; set; }

        [JsonProperty("cardinality")]
        public string Cardinality { get; set; }

        [JsonProperty("priority")]
        public int Priority { get; set; }

        [JsonProperty("deadline")]
        public string Deadline { get; set; } // ISO 8601 형식

        [JsonProperty("dispatchtime")]
        public string DispatchTime { get; set; } // ISO 8601 형식

        //[JsonProperty("fromnode")]
        //public string FromNode { get; set; } // 출발 노드 (선택 사항)

        [JsonProperty("parameters")]
        public MissionRequestParameters Parameters { get; set; }

        //[JsonProperty("payload")]
        //public string Payload { get; set; } // 페이로드 정보
    }

    public class MissionRequest
    {
        [JsonProperty("missionrequest")]
        public MissionRequestPayload RequestPayload { get; set; }
    }

    public class MissionPayloadResponse
    {
        [JsonProperty("rejectedmissions")]
        public List<int> RejectedMissions { get; set; } // 미션 ID가 int로 반환된다고 가정

        [JsonProperty("pendingmissions")]
        public List<int> PendingMissions { get; set; }

        [JsonProperty("acceptedmissions")]
        public List<int> AcceptedMissions { get; set; }
    }

    public class MissionResponse
    {
        [JsonProperty("payload")]
        public MissionPayloadResponse Payload { get; set; }

        [JsonProperty("retcode")]
        public int ReturnCode { get; set; } // Return Code
    }

    // ====== 3. Get information about one mission (GET) - Appendix G.21 (p. 452) ======
    public class MissionDetail
    {
        [JsonProperty("missionid")]
        public string MissionId { get; set; } // JSON에서는 string이지만 C#에서 필요시 int로 파싱

        [JsonProperty("dispatchtime")]
        public DateTime DispatchTime { get; set; }

        [JsonProperty("timetodestination")]
        public int TimeToDestination { get; set; }

        [JsonProperty("navigationstate")]
        public int NavigationState { get; set; }

        [JsonProperty("totalmissiontime")]
        public double TotalMissionTime { get; set; }

        [JsonProperty("missiontype")]
        public int MissionType { get; set; }

        [JsonProperty("groupid")]
        public int GroupId { get; set; }

        [JsonProperty("transportstate")]
        public int TransportState { get; set; }

        [JsonProperty("missionrule")]
        public object MissionRule { get; set; } // Dictionary<string, object> 등으로 변경 가능

        [JsonProperty("priority")]
        public int Priority { get; set; }

        [JsonProperty("assignedto")]
        public string AssignedTo { get; set; }

        [JsonProperty("payloadstatus")]
        public string PayloadStatus { get; set; } // "PickedUp", "Dropped" 등

        [JsonProperty("isloaded")]
        public bool IsLoaded { get; set; }

        [JsonProperty("payload")]
        public string Payload { get; set; } // "AMR_1" 등

        [JsonProperty("istoday")]
        public bool IsToday { get; set; }

        [JsonProperty("fromnode")]
        public string FromNode { get; set; }

        [JsonProperty("state")]
        public int State { get; set; } // 상위 레벨 상태 (navigationstate와 유사하거나 더 추상적일 수 있음)

        [JsonProperty("deadline")]
        public DateTime Deadline { get; set; }

        [JsonProperty("arrivingtime")]
        public DateTime ArrivingTime { get; set; }

        [JsonProperty("tonode")]
        public string ToNode { get; set; }

        // 추가 필드 (PDF에 따라 더 많은 필드를 추가할 수 있습니다)
        [JsonProperty("fleet_name")]
        public string FleetName { get; set; } // 미션이 할당된 Fleet의 이름

        [JsonProperty("status")]
        public string Status { get; set; } // 미션의 현재 상태 텍스트 ("IDLE", "RUNNING", "WAITING" 등)

        [JsonProperty("parameters")]
        public object Parameters { get; set; } // 미션 파라미터 (JSON 객체) - 직접 파싱이 필요할 수 있음
        [JsonProperty("parametersJson")]
        public string ParametersJson { get; set; } // Raw JSON string for Parameters
    }

    public class GetMissionInfoResponsePayload
    {
        [JsonProperty("missions")]
        public List<MissionDetail> Missions { get; set; }

        [JsonProperty("resultinfo")]
        public List<int> ResultInfo { get; set; }
    }

    public class GetMissionInfoResponse
    {
        [JsonProperty("payload")]
        public GetMissionInfoResponsePayload Payload { get; set; }

        [JsonProperty("retcode")]
        public int ReturnCode { get; set; }
    }

    // ====== Common Models ======
    /// <summary>
    /// 생산 라인 또는 특정 위치를 나타내는 모델
    /// </summary>
    public class Location
    {
        public string Name { get; set; }
        public string Node { get; set; }
        public int LineNumber { get; set; }
    }

    // Rack 클래스는 이제 Models/Rack.cs에 정의되어야 합니다.
    // 여기서는 제거됩니다.

    /// <summary>
    /// 로봇 미션 프로세스 전체의 상태를 추적하는 모델
    /// MainViewModel의 _activeRobotProcesses 딕셔너리에 저장될 각 프로세스의 인스턴스
    /// </summary>
    public class RobotMissionInfo : ViewModelBase
    {
        public string ProcessId { get; } // 이 프로세스의 고유 ID
        public string ProcessType { get; } // 예: "WaitToWrapTransfer", "RackTransfer"
        public List<MissionStepDefinition> MissionSteps { get; } // 이 프로세스를 구성하는 미션 단계들
        public int TotalSteps => MissionSteps.Count; // 총 미션 단계 수

        private int _currentStepIndex;
        public int CurrentStepIndex // 현재 진행 중인 미션 단계의 인덱스 (0-based)
        {
            get => _currentStepIndex;
            set => SetProperty(ref _currentStepIndex, value);
        }

        private int? _lastSentMissionId; // 가장 최근에 ANT 서버에 전송된 미션의 ID
        public int? LastSentMissionId
        {
            get => _lastSentMissionId;
            set => SetProperty(ref _lastSentMissionId, value);
        }

        private int? _lastCompletedMissionId; // 가장 최근에 완료된 미션의 ID
        public int? LastCompletedMissionId
        {
            get => _lastCompletedMissionId;
            set => SetProperty(ref _lastCompletedMissionId, value);
        }

        private MissionDetail _currentMissionDetail; // 현재 ANT 서버에서 폴링된 미션의 상세 정보
        public MissionDetail CurrentMissionDetail
        {
            get => _currentMissionDetail;
            set => SetProperty(ref _currentMissionDetail, value);
        }

        private HmiStatusInfo _hmiStatus; // HMI에 표시될 상태 정보
        public HmiStatusInfo HmiStatus
        {
            get => _hmiStatus;
            set => SetProperty(ref _hmiStatus, value);
        }

        private bool _isFinished;
        public bool IsFinished // 전체 프로세스 완료 여부 (성공적으로 모든 단계 완료)
        {
            get => _isFinished;
            set => SetProperty(ref _isFinished, value);
        }

        private bool _isFailed;
        public bool IsFailed // 전체 프로세스 실패 여부
        {
            get => _isFailed;
            set => SetProperty(ref _isFailed, value);
        }

        // 특정 프로세스와 관련된 랙 또는 라인 정보 (선택 사항)
        // RackViewModel은 Models가 아닌 ViewModels 네임스페이스에 있으므로 여기에 ViewModels.RackViewModel 타입으로 선언합니다.
        public ViewModels.RackViewModel SourceRack { get; set; } // 원본 랙
        public ViewModels.RackViewModel DestinationRack { get; set; } // 목적지 랙
        public Location DestinationLine { get; set; } // 목적지 생산 라인

        // 미션 폴링을 취소하기 위한 CancellationTokenSource
        public CancellationTokenSource CancellationTokenSource { get; } = new CancellationTokenSource();

        public RobotMissionInfo(string processId, string processType, List<MissionStepDefinition> missionSteps)
        {
            ProcessId = processId;
            ProcessType = processType;
            MissionSteps = missionSteps ?? new List<MissionStepDefinition>();
            CurrentStepIndex = 0;
            LastSentMissionId = null;
            LastCompletedMissionId = null;
            HmiStatus = new HmiStatusInfo { Status = MissionStatusEnum.PENDING.ToString(), ProgressPercentage = 0, CurrentStepDescription = "미션 대기 중" };
            IsFinished = false;
            IsFailed = false;
        }
    }

    /// <summary>
    /// 단일 미션 단계에 대한 정의
    /// </summary>
    public class MissionStepDefinition
    {
        public string ProcessStepDescription { get; set; } // HMI에 표시될 단계 설명
        public string MissionType { get; set; }
        public string FromNode { get; set; }
        public string ToNode { get; set; }
        public string Payload { get; set; }
        public bool IsLinkable { get; set; }
        public int LinkWaitTimeout { get; set; }
    }

    /// <summary>
    /// 미션 상태를 나타내는 열거형
    /// </summary>
    public enum MissionStatusEnum
    {
        RECEIVED = 0,
        ACCEPTED = 1,
        REJECTED = 2,
        STARTED = 3,
        COMPLETED = 6, // 4도 완료로 처리할 수 있으나, PDF 6을 따름
        CANCELLED = 5,
        FAILED = 7,
        PENDING // 커스텀 상태: ANT 서버에 아직 전송되지 않았거나 초기 상태
    }

    /// <summary>
    /// HMI (Human Machine Interface)에 표시될 로봇 미션의 현재 상태 정보
    /// </summary>
    public class HmiStatusInfo : ViewModelBase
    {
        private string _status;
        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        private int _progressPercentage;
        public int ProgressPercentage
        {
            get => _progressPercentage;
            set => SetProperty(ref _progressPercentage, value);
        }

        private string _currentStepDescription;
        public string CurrentStepDescription
        {
            get => _currentStepDescription;
            set => SetProperty(ref _currentStepDescription, value);
        }
    }
}
