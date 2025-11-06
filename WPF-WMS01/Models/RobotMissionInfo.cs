using System;
using System.Collections.Generic;
using WPF_WMS01.ViewModels;
using System.Threading; // CancellationTokenSource를 위해 추가

namespace WPF_WMS01.Models
{
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

        // 미션 진행 상태 관련
        private int _currentStepIndex;
        public int CurrentStepIndex // 현재 진행 중인 미션 단계의 인덱스 (0-based)
        {
            get => _currentStepIndex;
            set => SetProperty(ref _currentStepIndex, value);
        }

        private MissionStatusEnum _currentStatus; // 현재 프로세스의 전체 상태 (RECEIVED, ACCEPTED, FAILED 등)
        public MissionStatusEnum CurrentStatus
        {
            get => _currentStatus;
            set => SetProperty(ref _currentStatus, value);
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

        private HmiStatusInfo _hmiStatus; // HMI에 표시될 상태 정보
        public HmiStatusInfo HmiStatus
        {
            get => _hmiStatus;
            set => SetProperty(ref _hmiStatus, value);
        }

        // 미션 폴링을 취소하기 위한 CancellationTokenSource
        public CancellationTokenSource CancellationTokenSource { get; }

        // 랙 잠금 관리
        // 프로세스 시작 시 잠긴 모든 랙의 ID 목록. 미션 실패 시 이 목록의 랙들을 잠금 해제합니다.
        public List<int> RacksLockedByProcess { get; set; }
        // 이 미션을 시작한 Modbus Coil의 주소 (경광등 제어용)
        public ushort? InitiatingCoilAddress { get; set; } // 기존 정의 유지
        // AMR 랙 버튼 기능 추가: 창고 미션 여부
        public bool IsWarehouseMission { get; set; } // 창고 미션 여부

        // 미션 재시도 및 폴링 관련
        public int PollingRetryCount { get; set; }
        public DateTime LastPollingAttemptTime { get; set; }
        public const int MaxPollingRetries = 3; // 최대 재시도 횟수
        public const int PollingRetryDelaySeconds = 2; // 재시도 간 지연 시간 (초)

        // ANT MissionDetail 응답의 전체 정보 저장
        private MissionDetail _currentMissionDetail; // 현재 ANT 서버에서 폴링된 미션의 상세 정보
        public MissionDetail CurrentMissionDetail
        {
            get => _currentMissionDetail;
            set => SetProperty(ref _currentMissionDetail, value);
        }

        // 임시 저장소: MC Protocol Read/Write 및 DB Read/Write 간 데이터 전달용
        public string ReadBulletTypeValue { get; set; }
        public string ReadStringValue { get; set; }
        public ushort? ReadIntValue { get; set; }

        // 특정 프로세스와 관련된 랙 또는 라인 정보 (선택 사항)
        // RackViewModel은 Models가 아닌 ViewModels 네임스페이스에 있으므로 여기에 ViewModels.RackViewModel 타입으로 선언합니다.
        // SourceRack과 DestinationRack은 이제 MissionStepDefinition 내에서 ID로 관리됩니다.
        // public ViewModels.RackViewModel SourceRack { get; set; } // 제거
        // public ViewModels.RackViewModel DestinationRack { get; set; } // 제거
        public List<ViewModels.RackViewModel> RacksToProcess { get; set; } // 여러 랙을 처리할 경우 (예: 출고)

        public RobotMissionInfo(string processId, string processType, List<MissionStepDefinition> missionSteps, List<int> racksLockedByProcess, ushort? initiatingCoilAddress = null, bool isWarehouseMission = false, string readStringValue = null, ushort? readIntValue = null)
        {
            ProcessId = processId;
            ProcessType = processType;
            MissionSteps = missionSteps ?? new List<MissionStepDefinition>();
            CurrentStepIndex = 0;
            CurrentStatus = MissionStatusEnum.PENDING; // 초기 상태 설정
            HmiStatus = new HmiStatusInfo
            {
                Status = MissionStatusEnum.PENDING.ToString(), // 초기 상태를 PENDING으로
                ProgressPercentage = 0,
                CurrentStepDescription = "미션 대기 중",
                CurrentStepIndex = 0
            };
            CancellationTokenSource = new CancellationTokenSource();
            RacksLockedByProcess = racksLockedByProcess ?? new List<int>();
            InitiatingCoilAddress = initiatingCoilAddress; // 경광등 Coil 주소 저장
            IsWarehouseMission = isWarehouseMission; // isWarehouseMission 초기화

            // Polling 관련 필드 초기화
            PollingRetryCount = 0;
            LastPollingAttemptTime = DateTime.MinValue;

            RacksToProcess = new List<ViewModels.RackViewModel>(); // 초기화

            //LastSentMissionId = null;
            //LastCompletedMissionId = null;
            //IsFinished = false;
            //IsFailed = false;
            ReadStringValue = readStringValue;
            ReadIntValue = readIntValue;
        }

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
        COMPLETED = 4, // 4로 명확히 정의
        CANCELLED = 5,
        FAILED = 7,
        PENDING = 6, // 커스텀 상태: ANT 서버에 아직 전송되지 않았거나 초기 상태
        UNKNOWN = 99 // 정의되지 않은 상태
    }

    public enum ProcessTypeEnum
    {
        WIPMOVEIN = 0, // 제공품 입고
        WIPMOVE = 1, // 제공품 출고 (이동)
        WIPOUT = 2, // 제공품 출고 (반출)
        UNPACKED = 3, // 미포장 입고
        PACKING = 4, // 포장기로 이동
        PACKED = 5, // 포장 입고
        SINGLEOUT = 6, // 단일 출고
        MULTIOUT = 7, // 멀티 출고
        TOBUFFER = 8, // AMR 2 임시 이동
        REPACKED = 9, // 포장 재입고
        UNKNOWN = 99 // 정의되지 않은 상태
    }
}
