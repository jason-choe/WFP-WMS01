// Models/AntApiModels.cs
using System;
using Newtonsoft.Json;
using System.Collections.Generic; // List<T> 사용을 위해 추가

namespace WPF_WMS01.Models
{
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

        // !!! 핵심 변경: apiVersion 필드를 string 타입으로 변경 !!!
        [JsonProperty("apiVersion")]
        public string ApiVersionString { get; set; } // JSON 필드는 "apiVersion"이지만, C# 속성 이름은 충돌을 피하기 위해 변경

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("familyName")]
        public string FamilyName { get; set; }

        [JsonProperty("roles")]
        public List<string> Roles { get; set; }

        [JsonProperty("personalNumber")]
        public string PersonalNumber { get; set; }

        [JsonProperty("token")]
        public string Token { get; set; }
    }

    // ====== 2. Create a mission (POST) - Appendix G.20 (p. 450) ======
    // 참고: 미션 생성은 복잡할 수 있으므로, PDF의 'Request Body' 섹션을 자세히 보고 필요한 모든 필드를 추가해야 합니다.
    // 여기서는 간단한 예시만 제공합니다.
    // Create mission Request Body
    public class MissionParametersValue
    {
        [JsonProperty("payload")]
        public string Payload { get; set; }

        [JsonProperty("optionalinfo")]
        public string OptionalInfo { get; set; }
    }

    public class MissionParameterDetails
    {
        [JsonProperty("value")]
        public MissionParametersValue Value { get; set; }

        [JsonProperty("desc")]
        public string Description { get; set; } // "desc" 대신 Description 사용 (C# 명명 규칙)

        [JsonProperty("type")]
        public string Type { get; set; } // 예: "org.json.JSONObject"

        [JsonProperty("name")]
        public string Name { get; set; } // 예: "parameters"
    }

    public class MissionRequestInner
    {
        [JsonProperty("requestor")]
        public string Requestor { get; set; }

        [JsonProperty("missiontype")]
        public string MissionType { get; set; } // "0"과 같은 문자열 (PDF에 따라)

        [JsonProperty("fromnode")]
        public string FromNode { get; set; }

        [JsonProperty("tonode")]
        public string ToNode { get; set; }

        [JsonProperty("cardinality")]
        public string Cardinality { get; set; } // "1"과 같은 문자열

        [JsonProperty("priority")]
        public int Priority { get; set; }

        [JsonProperty("parameters")]
        public MissionParameterDetails Parameters { get; set; }
    }

    public class CreateMissionRequest
    {
        // 최상위 "missionrequest" 키를 위한 래퍼
        [JsonProperty("missionrequest")]
        public MissionRequestInner MissionRequest { get; set; }
    }

    // Create mission Response Body
    public class MissionPayloadResponse
    {
        [JsonProperty("rejectedmissions")]
        public List<string> RejectedMissions { get; set; } // 미션 ID가 문자열로 반환된다고 가정

        [JsonProperty("pendingmissions")]
        public List<string> PendingMissions { get; set; }

        [JsonProperty("acceptedmissions")]
        public List<string> AcceptedMissions { get; set; }
    }

    public class CreateMissionResponse
    {
        [JsonProperty("payload")]
        public MissionPayloadResponse Payload { get; set; }

        [JsonProperty("retcode")]
        public int RetCode { get; set; } // Return Code
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