// Models/AntApiModels.cs
using System;
using Newtonsoft.Json;
using System.Collections.Generic; // List<T> 사용을 위해 추가

namespace WPF_WMS01.Models
{
    // ====== 1. Login (POST) - Appendix G.1 (p. 427) ======
    public class ApiVersion
    {
        public int Major { get; set; }
        public int Minor { get; set; }
    }

    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public ApiVersion ApiVersion { get; set; } // 새로 추가된 필드
    }

    public class LoginResponse
    {
        public string FirstName { get; set; } // 새로 추가된 필드
        public string ApiVersion { get; set; } // 새로 추가된 필드 (문자열 타입)
        public string DisplayName { get; set; } // 새로 추가된 필드
        public string FamilyName { get; set; } // 새로 추가된 필드
        public List<string> Roles { get; set; }
        public string PersonalNumber { get; set; } // 새로 추가된 필드
        public string Token { get; set; }

        // 이전 응답에 있던 'validity' 필드는 현재 응답에 포함되어 있지 않습니다.
        // 만약 토큰 유효 기간 관리가 필요하다면, 이 새 API에서는 다른 방법을 찾아야 할 수 있습니다.
        // 현재로서는 해당 필드를 제거합니다.
        // public long Validity { get; set; }
    }

    // ====== 2. Create a mission (POST) - Appendix G.20 (p. 450) ======
    // 참고: 미션 생성은 복잡할 수 있으므로, PDF의 'Request Body' 섹션을 자세히 보고 필요한 모든 필드를 추가해야 합니다.
    // 여기서는 간단한 예시만 제공합니다.

    public class MissionProperties
    {
        [JsonProperty("fleet_name")]
        public string FleetName { get; set; }

        [JsonProperty("is_blocking")]
        public bool IsBlocking { get; set; }

        [JsonProperty("is_cancelable")]
        public bool IsCancelable { get; set; }

        [JsonProperty("is_pausable")]
        public bool IsPausable { get; set; }

        [JsonProperty("is_skippable")]
        public bool IsSkippable { get; set; }

        [JsonProperty("user_data")]
        public string UserData { get; set; } // JSON 문자열 또는 JObject 형태로 처리될 수 있음
    }

    public class MissionParameter
    {
        [JsonProperty("param_name")]
        public string ParamName { get; set; }

        [JsonProperty("param_value")]
        public string ParamValue { get; set; } // 또는 object/JToken (값 타입에 따라)
    }

    public class MissionStep
    {
        [JsonProperty("step_name")]
        public string StepName { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; } // 예: "GO_TO", "PICK_UP", "DROP_OFF" 등

        [JsonProperty("command_name")]
        public string CommandName { get; set; } // 예: "goToPoint", "pickUpLoad" 등

        [JsonProperty("parameters")]
        public List<MissionParameter> Parameters { get; set; } // 각 미션 스텝의 파라미터들
    }

    public class CreateMissionRequest
    {
        [JsonProperty("mission_name")]
        public string MissionName { get; set; }

        [JsonProperty("properties")]
        public MissionProperties Properties { get; set; }

        [JsonProperty("steps")]
        public List<MissionStep> Steps { get; set; }
    }

    public class CreateMissionResponse
    {
        [JsonProperty("mission_id")]
        public int MissionId { get; set; } // 또는 long (API 응답에 따라)
    }

    // ====== 3. Get information about one mission (GET) - Appendix G.21 (p. 452) ======

    public class MissionInfoResponse
    {
        [JsonProperty("mission_id")]
        public int MissionId { get; set; }

        [JsonProperty("fleet_name")]
        public string FleetName { get; set; }

        [JsonProperty("state")]
        public string State { get; set; } // "PENDING", "ACTIVE", "PAUSED", "CANCELLED", "COMPLETED", "FAILED" 등

        [JsonProperty("status")]
        public string Status { get; set; } // "IDLE", "RUNNING", "WAITING" 등

        [JsonProperty("start_time")]
        public long StartTime { get; set; } // Unix timestamp (seconds)

        [JsonProperty("end_time")]
        public long EndTime { get; set; } // Nullable 일 수 있음

        // 필요에 따라 더 많은 필드를 추가할 수 있습니다 (예: errors, vehicle_id 등)
    }
}