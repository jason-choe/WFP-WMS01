// Models/AntApiModels.cs
using System;
using Newtonsoft.Json;
using System.Collections.Generic; // List<T> 사용을 위해 추가
using System.Threading; // CancellationTokenSource를 위해 추가
using WPF_WMS01.ViewModels; // ViewModelBase 참조

namespace WPF_WMS01.Models
{
    // ViewModelBase for common INotifyPropertyChanged implementation
    // 이 파일에 ViewModelBase가 중복 정의되어 있었으나,
    // ViewModelBase.cs 파일로 분리되었으므로 여기서는 제거합니다.
    // 만약 이 파일에 ViewModelBase가 필요하다면, WPF_WMS01.ViewModels 네임스페이스를 using하여 사용합니다.

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

    // ====== 2. Extract Vehicle (POST) - Appendix G.1.1.16 (p. 461) ======
    /// <summary>
    /// ExtractVehicle 요청의 실제 명령부 (command 객체)를 정의합니다.
    /// </summary>
    public class ExtractVehicleCommand
    {
        [JsonProperty("name")]
        public string Name { get; set; } // 실행할 명령 이름 (예: "extract")

        [JsonProperty("args")]
        public object Args { get; set; } = new { }; // 빈 객체
    }

    /// <summary>
    /// ExtractVehicle (Custom Command) 요청의 최종 모델입니다.
    /// </summary>
    public class ExtractVehicleRequest
    {
        [JsonProperty("command")]
        public ExtractVehicleCommand Command { get; set; } // 명령 정보
    }

    /// <summary>
    /// ExtractVehicle 응답의 Payload 객체를 정의합니다. (vehicle 필드만 사용)
    /// </summary>
    public class ExtractVehiclePayload
    {
        [JsonProperty("vehicle")]
        public string Vehicle { get; set; } // 명령이 성공적으로 적용된 차량 ID
    }

    /// <summary>
    /// ExtractVehicle (Custom Command) 요청의 최종 응답 모델입니다.
    /// retCode와 payload.vehicle만 참고합니다.
    /// </summary>
    public class ExtractVehicleResponse
    {
        [JsonProperty("payload")]
        public ExtractVehiclePayload Payload { get; set; } // 응답 페이로드

        [JsonProperty("retCode")]
        public int RetCode { get; set; } // 응답 코드 (0: 성공)
    }

    // ====== 3. Create Mission (POST) - Appendix G.3 (p. 433) ======
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
        public int Cardinality { get; set; }

        [JsonProperty("priority")]
        public int Priority { get; set; }

        [JsonProperty("deadline")]
        public string Deadline { get; set; } // ISO 8601 형식

        [JsonProperty("dispatchtime")]
        public string DispatchTime { get; set; } // ISO 8601 형식

        [JsonProperty("fromnode")]
        public string FromNode { get; set; } // 출발 노드 (선택 사항)

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

        [JsonProperty("schedulerstate")]
        public int SchedulerState { get; set; }

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

    // RobotMissionInfo 클래스는 이제 Models/RobotMissionInfo.cs에 정의됩니다.
    // 여기서는 해당 클래스 정의를 제거합니다.

    /// <summary>
    /// 단일 미션 단계에 대한 정의
    /// </summary>
    public class MissionStepDefinition
    {
        public string ProcessStepDescription { get; set; } // HMI에 표시될 단계 설명
        public string MissionType { get; set; }
        public string FromNode { get; set; }
        public string ToNode { get; set; }
        public int Priority { get; set; }
        public string Payload { get; set; }
        public bool IsLinkable { get; set; }
        public int? LinkedMission { get; set; }
        public int LinkWaitTimeout { get; set; }

        // 랙 업데이트 관련 필드: IsLinkable이 false일 때만 유효
        public int? SourceRackId { get; set; }
        public int? DestinationRackId { get; set; }

        /// <summary>
        /// 이 미션 단계에서 Modbus Discrete Input 검사를 수행할지 여부입니다.
        /// </summary>
        public bool CheckModbusDiscreteInput { get; set; } = false;
        /// <summary>
        /// 검사할 Modbus Discrete Input의 주소입니다. CheckModbusDiscreteInput이 true일 때만 유효합니다.
        /// </summary>
        public ushort? ModbusDiscreteInputAddressToCheck { get; set; } = null;


        // 새로운 사전/사후 동작 목록 추가 (Ordered list of operations)
        public List<MissionSubOperation> PreMissionOperations { get; set; } = new List<MissionSubOperation>();
        public List<MissionSubOperation> PostMissionOperations { get; set; } = new List<MissionSubOperation>();

        // 기존 생성자 유지 (이전 코드와의 호환성을 위해)
        public MissionStepDefinition(
            string processStepDescription,
            string missionType = null,
            string fromNode = null,
            string toNode = null,
            int priority = 1,
            string payload = null,
            bool isLinkable = false,
            int linkWaitTimeout = 3600,
            int? sourceRackId = null,
            int? destinationRackId = null,
            bool checkModbusDiscreteInput = false,
            ushort? modbusDiscreteInputAddressToCheck = null
            )
        {
            ProcessStepDescription = processStepDescription;
            MissionType = missionType;
            FromNode = fromNode;
            ToNode = toNode;
            Priority = priority;
            Payload = payload;
            IsLinkable = isLinkable;
            LinkWaitTimeout = linkWaitTimeout;
            SourceRackId = sourceRackId;
            DestinationRackId = destinationRackId;
            CheckModbusDiscreteInput = checkModbusDiscreteInput;
            ModbusDiscreteInputAddressToCheck = modbusDiscreteInputAddressToCheck;

            // 기존 MC Operation 관련 필드들은 새로운 Pre/PostMissionOperations에서 사용하도록 유도
            // 여기서는 직접 할당하지 않고, 필요하다면 MissionSubOperation으로 변환하여 추가하는 로직을 고려할 수 있습니다.
        }

        // 기본 생성자 (역직렬화 및 컬렉션 초기화용)
        public MissionStepDefinition() { }
    }

    /// <summary>
    /// 미션 단계 내에서 실행될 세부 MC Protocol 또는 DB/UI 작업을 정의합니다.
    /// </summary>
    public class MissionSubOperation
    {
        public SubOperationType Type { get; set; } // 세부 동작 유형
        public string Description { get; set; } // 이 동작에 대한 설명 (로깅용)

        // MC Protocol 관련 파라미터
        public string McProtocolIpAddress { get; set; } // 개별 MC 동작에 대한 IP (옵션)
        public ushort? McCoilAddress { get; set; } // Coil (비트) 주소 (경광등 ON/OFF, 센서 명령)
        public ushort? McDiscreteInputAddress { get; set; } // Discrete Input (비트) 주소 (센서 상태 확인, 공 파레트 배출 준비)
        public ushort? McWordAddress { get; set; } // Word (16비트) 주소 (데이터 읽기/쓰기 시작 주소)
        public ushort? McStringLengthWords { get; set; } // 문자열 읽기/쓰기 시 워드 길이 (예: LotNo. 앞부분 8 words)
        public string McWriteValueString { get; set; } // MC WriteData 시 사용할 string 값
        public int? McWriteValueInt { get; set; } // MC WriteData 시 사용할 int 값 (1비트 쓰기 시 1/0, 단일 워드 쓰기 시 값)
        public int? McWateValueInt { get; set; } // MC WriteData 시 사용할 int 값 (1비트 쓰기 시 1/0, 단일 워드 쓰기 시 값)
        public int? WaitTimeoutSeconds { get; set; } // 센서 대기 타임아웃 (초)
        public string BitDeviceCode { get; set; } = "Y"; // 비트 쓰기/읽기 시 디바이스 코드 (예: X, Y)
        public string WordDeviceCode { get; set; } = "D"; // 워드 쓰기/읽기 시 디바이스 코드 (예: D, W, R)
        public bool? PauseButtonCallPlcStatus { get; set; } 


        // DB 관련 파라미터
        public int? TargetRackId { get; set; } // DB에서 읽거나 업데이트할 단일 랙 ID
        public int? SourceRackIdForDbUpdate { get; set; } // DbUpdateRackState 용 Source Rack ID
        public int? DestRackIdForDbUpdate { get; set; } // DbUpdateRackState 용 Destination Rack ID

        // 생성자
        public MissionSubOperation(
            SubOperationType type,
            string description = "",
            string mcProtocolIpAddress = null,
            ushort? mcCoilAddress = null,
            ushort? mcDiscreteInputAddress = null,
            ushort? mcWordAddress = null,
            ushort? mcStringLengthWords = null,
            string mcWriteValueString = null,
            int? mcWriteValueInt = null,
            int? waitTimeoutSeconds = null,
            string bitDeviceCode = "Y",
            string wordDeviceCode = "D",
            int? targetRackId = null,
            int? sourceRackIdForDbUpdate = null,
            int? destRackIdForDbUpdate = null)
        {
            Type = type;
            Description = description;
            McProtocolIpAddress = mcProtocolIpAddress;
            McCoilAddress = mcCoilAddress;
            McDiscreteInputAddress = mcDiscreteInputAddress;
            McWordAddress = mcWordAddress;
            McStringLengthWords = mcStringLengthWords;
            McWriteValueString = mcWriteValueString;
            McWriteValueInt = mcWriteValueInt;
            WaitTimeoutSeconds = waitTimeoutSeconds;
            BitDeviceCode = bitDeviceCode;
            WordDeviceCode = wordDeviceCode;
            TargetRackId = targetRackId;
            SourceRackIdForDbUpdate = sourceRackIdForDbUpdate;
            DestRackIdForDbUpdate = destRackIdForDbUpdate;
        }

        // 기본 생성자 (역직렬화용)
        public MissionSubOperation() { }
    }

    /// <summary>
    /// 미션 단계 내에서 실행될 세부 동작의 유형 정의.
    /// 모든 MC Protocol 동작은 Word 단위로 처리됩니다.
    /// </summary>
    public enum SubOperationType
    {
        None,
        // MC Protocol - Read
        McReadLotNoBoxCount,        // 3-1, 5: PLC로부터 LotNo, Box Count 읽어 임시 저장소에 저장 (8+2+2 워드)
        McReadSingleWord,           // 3-3: PLC로부터 단일 워드 읽기 (예: 공 파레트 배출 준비 확인)

        // MC Protocol - Write
        McWriteLotNoBoxCount,       // 4-3, 6: 임시 저장소의 LotNo, Box Count를 PLC에 쓰기
        McWriteSingleWord,          // 3-4, 4-4: PLC에 단일 워드 쓰기 (경광등 ON/OFF 또는 센서 명령)

        // MC Protocol - Wait (Word 상태 대기)
        McWaitSensorOff,            // 3-2: PLC에 area sensor를 끄고 (Word 쓰기) 꺼질 때까지 대기 (Word 읽기)
        McWaitSensorOn,             // 4-5: PLC에 area sensor를 켜고 (Word 쓰기) 켜질 때까지 대기 (Word 읽기)
        McWaitAvailable,            // 특정 word의 값이 원하는 값일 때까지 대기 (Word 읽기)

        // DB Operations
        DbReadRackData,             // 4-2: Database로부터 특정 Rack의 LotNo, Box Count를 임시 저장소에 저장
        DbUpdateRackState,          // 4-6: 기존 PerformDbUpdateForCompletedStep() 실행 (Source, Destination Rack ID 사용)

        // UI Operations
        UiDisplayLotNoBoxCount,     // 4-1: 임시 저장소의 LotNo, Box Count를 UI의 TextBox에 표시

        // Other Operations (Modbus Discrete Input Check)
        CheckModbusDiscreteInput,    // 4-7: 기존 Modbus Discrete Input 체크 로직 (_missionCheckModbusService 사용)

        // Modbus PLC option
        SetPlcStatusIsPaused,       // Buttpn call modebus enabled/disabled

        // insert 입고 to DB
        DbInsertInboundData,

        // update 출고 to DB
        DbUpdateOutboundData
    }

    // 기존 McOperationType 열거형 유지 (이전 코드 호환성을 위해)
    // 새로운 SubOperationType과 충돌하지 않도록 사용 시 주의 필요
    public enum McOperationType
    {
        None,
        ReadData,
        WriteData,
        WriteToUI,
        ReadFromDB,
        SensorCommandOff,
        SensorCommandOn
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

        private int _currentStepIndex;
        public int CurrentStepIndex
        {
            get => _currentStepIndex;
            set => SetProperty(ref _currentStepIndex, value);
        }

        private string _subOpDescription;
        public string SubOpDescription
        {
            get => _subOpDescription;
            set => SetProperty(ref _subOpDescription, value);
        }

        private int _totalSteps;
        public int TotalSteps
        {
            get => _totalSteps;
            set => SetProperty(ref _totalSteps, value);
        }
    }
}
