// Models/RobotMission.cs
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System; // DateTime을 위해 추가
using System.Text.Json;

namespace WPF_WMS01.Models
{
    // 로봇 미션 요청을 위한 데이터 모델 (missions.json 구조에 맞춰 재정의)
    public class RobotMissionRequest
    {
        [JsonPropertyName("missionrequest")]
        public MissionRequestContent MissionRequestContent { get; set; }
    }

    public class MissionRequestContent
    {
        [JsonPropertyName("requestor")]
        public string Requestor { get; set; } = "admin";

        [JsonPropertyName("missiontype")]
        public string MissionType { get; set; } // 예: "8" (이동), "9" (충전) 등

        [JsonPropertyName("tonode")]
        public string ToNode { get; set; }

        [JsonPropertyName("cardinality")]
        public string Cardinality { get; set; } = "1";

        [JsonPropertyName("priority")]
        public int Priority { get; set; } = 2;

        [JsonPropertyName("deadline")]
        public string Deadline { get; set; } // ISO 8601 형식

        [JsonPropertyName("dispatchtime")]
        public string DispatchTime { get; set; } // ISO 8601 형식

        [JsonPropertyName("parameters")]
        public MissionParameters Parameters { get; set; }
    }

    public class MissionParameters
    {
        [JsonPropertyName("value")]
        public ParameterValue Value { get; set; }

        [JsonPropertyName("desc")]
        public string Description { get; set; } = "Mission extension";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "org.json.JSONObject";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "parameters";
    }

    public class ParameterValue
    {
        [JsonPropertyName("payload")]
        public string Payload { get; set; } = "AMR_1";

        [JsonPropertyName("linkedMission")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] // 값이 null일 때 JSON에 포함하지 않음
        public int? LinkedMission { get; set; }

        [JsonPropertyName("isLinkable")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] // 값이 null일 때 JSON에 포함하지 않음
        public bool? IsLinkable { get; set; }

        [JsonPropertyName("linkWaitTimeout")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] // 기본값(0)일 때 JSON에 포함하지 않음
        public int? LinkWaitTimeout { get; set; }
    }

    // 로봇 미션 명령 응답을 위한 데이터 모델 (missions.json의 응답 구조)
    public class RobotMissionCommandResponse
    {
        [JsonPropertyName("payload")]
        public CommandResponsePayload Payload { get; set; }

        [JsonPropertyName("retcode")]
        public int Retcode { get; set; }
    }

    public class CommandResponsePayload
    {
        [JsonPropertyName("rejectedmissions")]
        public List<int> RejectedMissions { get; set; } = new List<int>();

        [JsonPropertyName("pendingmissions")]
        public List<int> PendingMissions { get; set; } = new List<int>();

        [JsonPropertyName("acceptedmissions")]
        public List<int> AcceptedMissions { get; set; } = new List<int>();
    }

    // --- 미션 상태 조회 API 응답 모델 (Get information about one mission.json 에 맞춰 업데이트) ---
    public class GetMissionInfoApiResponse
    {
        [JsonPropertyName("payload")]
        public MissionInfoPayload Payload { get; set; }

        [JsonPropertyName("retcode")]
        public int Retcode { get; set; }
    }

    public class MissionInfoPayload
    {
        [JsonPropertyName("missions")]
        public List<MissionDetail> Missions { get; set; } = new List<MissionDetail>();

        [JsonPropertyName("resultinfo")]
        public List<int> ResultInfo { get; set; } = new List<int>();
    }

    // 단일 미션 상세 정보 (Get information about one mission.json의 "missions" 배열 내부 객체)
    public class MissionDetail
    {
        [JsonPropertyName("missionid")]
        [JsonConverter(typeof(IntToStringConverter))] // int, string 모두 처리 위함
        public string MissionId { get; set; }

        [JsonPropertyName("dispatchtime")]
        public string DispatchTime { get; set; }

        [JsonPropertyName("timetodestination")]
        public int TimeToDestination { get; set; }

        [JsonPropertyName("navigationstate")]
        public int NavigationState { get; set; } // 0: Received, 1: Accepted, 2: Rejected, 3: Started, 4: Completed, 5: Cancelled

        [JsonPropertyName("totalmissiontime")]
        public double TotalMissionTime { get; set; }

        [JsonPropertyName("missiontype")]
        public int MissionType { get; set; }

        [JsonPropertyName("groupid")]
        public int GroupId { get; set; }

        [JsonPropertyName("transportstate")]
        public int TransportState { get; set; }

        [JsonPropertyName("missionrule")]
        public object MissionRule { get; set; }

        [JsonPropertyName("priority")]
        public int Priority { get; set; }

        [JsonPropertyName("assignedto")]
        public string AssignedTo { get; set; }

        [JsonPropertyName("payloadstatus")]
        public string PayloadStatus { get; set; }

        [JsonPropertyName("isloaded")]
        public bool IsLoaded { get; set; }

        [JsonPropertyName("payload")]
        public string Payload { get; set; }

        [JsonPropertyName("istoday")]
        public bool IsToday { get; set; }

        [JsonPropertyName("fromnode")]
        public string FromNode { get; set; }

        [JsonPropertyName("state")] // General mission state (not navigation state)
        public int State { get; set; }

        [JsonPropertyName("deadline")]
        public string Deadline { get; set; }

        [JsonPropertyName("arrivingtime")]
        public string ArrivingTime { get; set; }

        [JsonPropertyName("tonode")]
        public string ToNode { get; set; }
    }

    // HMI 내부에서 미션의 진행 상태를 간소화하여 관리하기 위한 ViewModel 속성
    // 이 클래스는 INotifyPropertyChanged를 구현하여 UI 업데이트를 트리거합니다.
    public class RobotMissionStatusResponse : INotifyPropertyChanged // 이름 유지, 내부 로직 변경
    {
        private string _missionId;
        private string _status; // "PENDING", "IN_PROGRESS", "COMPLETED", "FAILED"
        private int _currentStepIndex; // 현재 진행 중인 프로세스의 단계 인덱스 (HMI 내부용)
        private string _errorMessage;
        private int _progressPercentage; // HMI 내부 계산/관리용 진행률

        public string MissionId
        {
            get => _missionId;
            set => SetProperty(ref _missionId, value);
        }

        public string Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public int CurrentStepIndex
        {
            get => _currentStepIndex;
            set => SetProperty(ref _currentStepIndex, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        public int ProgressPercentage
        {
            get => _progressPercentage;
            set => SetProperty(ref _progressPercentage, value);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (!EqualityComparer<T>.Default.Equals(field, value))
            {
                field = value;
                OnPropertyChanged(propertyName);
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // MissionDetail 객체로부터 RobotMissionStatusResponse를 업데이트하는 헬퍼 메서드
        public void UpdateFromMissionDetail(MissionDetail detail)
        {
            MissionId = detail.MissionId;
            // navigationstate를 HMI Status Enum에 매핑
            Status = GetHmiStatusFromNavigationState(detail.NavigationState);
            // 진행률은 HMI에서 계산하거나, 서버가 제공하는 값을 사용 (여기서는 단순 매핑)
            // Get information about one mission.json에는 직접적인 ProgressPercentage 필드가 없으므로,
            // navigationstate에 따라 대략적인 진행률을 매핑합니다.
            ProgressPercentage = GetProgressFromNavigationState(detail.NavigationState, detail.TotalMissionTime, detail.TimeToDestination);
            ErrorMessage = (detail.NavigationState == (int)NavigationStateEnum.Rejected || detail.NavigationState == (int)NavigationStateEnum.Cancelled) ? "로봇 미션 실패 또는 취소됨." : null;
            // CurrentStepIndex는 HMI 내부에서 관리하므로 여기서 업데이트하지 않음
        }

        // navigationstate 값에 따라 HMI용 상태 문자열 반환
        private string GetHmiStatusFromNavigationState(int navState)
        {
            switch ((NavigationStateEnum)navState)
            {
                case NavigationStateEnum.Received:
                case NavigationStateEnum.Accepted: return MissionStatusEnum.PENDING.ToString();
                case NavigationStateEnum.Started: return MissionStatusEnum.IN_PROGRESS.ToString();
                case NavigationStateEnum.Completed: return MissionStatusEnum.COMPLETED.ToString();
                case NavigationStateEnum.Rejected:
                case NavigationStateEnum.Cancelled: return MissionStatusEnum.FAILED.ToString(); // HMI에서는 Rejected/Cancelled를 FAILED로 처리
                default: return "UNKNOWN";
            }
        }

        // navigationstate와 시간 정보를 바탕으로 진행률 추정 (임시)
        private int GetProgressFromNavigationState(int navState, double totalTime, int timeToDest)
        {
            switch ((NavigationStateEnum)navState)
            {
                case NavigationStateEnum.Received: return 0;
                case NavigationStateEnum.Accepted: return 10; // 계획됨
                case NavigationStateEnum.Started:
                    // Started 상태에서 좀 더 세분화된 진행률을 원하면 totalTime과 timeToDest 활용
                    // 예를 들어, (totalTime - timeToDest) / totalTime * 100
                    if (totalTime > 0)
                    {
                        int calculatedProgress = (int)(((totalTime - timeToDest) / totalTime) * 100);
                        return Math.Max(20, Math.Min(90, calculatedProgress)); // 20%에서 90% 사이
                    }
                    return 50; // 시작됨
                case NavigationStateEnum.Completed: return 100;
                case NavigationStateEnum.Rejected:
                case NavigationStateEnum.Cancelled: return 0; // 실패/취소 시 0% 또는 특정 값
                default: return 0;
            }
        }
    }

    // 미션 상태를 위한 Enum
    public enum MissionStatusEnum
    {
        PENDING,
        IN_PROGRESS,
        COMPLETED,
        FAILED
    }

    // Get information about one mission.json에서 정의된 navigationstate의 숫자 값에 대한 Enum
    public enum NavigationStateEnum
    {
        Received = 0,
        Accepted = 1, // (=planned)
        Rejected = 2,
        Started = 3, // (=running)
        Completed = 4, // (=successful)
        Cancelled = 5
    }

    // HMI 내부에서 정의하는 이동 경로 정보
    public class MissionStepDefinition
    {
        public string StepName { get; set; }
        public string MissionType { get; set; }
        public Dictionary<string, string> Context { get; set; }
        public bool IsPickupDropoffPoint { get; set; } = false;
    }

    // JSON 응답에서 missionid가 int 또는 string으로 올 수 있으므로 이를 처리하기 위한 커스텀 컨버터
    public class IntToStringConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                return reader.GetInt32().ToString();
            }
            else if (reader.TokenType == JsonTokenType.String)
            {
                return reader.GetString();
            }
            return null;
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }
    }
}
