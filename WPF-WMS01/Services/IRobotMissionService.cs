// Services/IRobotMissionService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WPF_WMS01.Models; // MissionStepDefinition 참조를 위해 필요
using WPF_WMS01.ViewModels; // RackViewModel 참조를 위해 필요 (이벤트 및 일부 ViewModel 참조 유지)
using WPF_WMS01.Services; // IMcProtocolService 참조를 위해 추가

namespace WPF_WMS01.Services
{
    /// <summary>
    /// 로봇 미션 프로세스 관리를 위한 인터페이스입니다.
    /// MainViewModel이 이 인터페이스를 통해 로봇 미션 기능을 사용합니다.
    /// </summary>
    public interface IRobotMissionService : IDisposable
    {
        /// <summary>
        /// UI에 자동 닫힘 메시지를 표시하도록 MainViewModel에 알리는 이벤트입니다.
        /// </summary>
        event Action<string> OnShowAutoClosingMessage;

        /// <summary>
        /// 랙의 잠금 상태가 변경되었음을 MainViewModel에 알리는 이벤트입니다.
        /// (랙 ID, 새로운 잠금 상태)
        /// </summary>
        event Action<int, bool> OnRackLockStateChanged;

        /// <summary>
        /// WAIT 랙 이동 완료 후 InputStringForButton을 초기화하도록 MainViewModel에 알리는 이벤트입니다.
        /// </summary>
        event Action OnInputStringForButtonCleared;
        event Action OnInputStringForBoxesCleared;

        /// <summary>
        /// RobotMissionService가 MainViewModel에게 특정 경광등을 끄도록 요청하는 이벤트입니다.
        /// (경광등 Coil 주소)
        /// </summary>
        event Action<ushort> OnTurnOffAlarmLightRequest; // 새로운 이벤트 추가

        /// <summary>
        /// 로봇 미션 프로세스의 상태가 업데이트될 때 발생합니다.
        /// (업데이트된 RobotMissionInfo 객체)
        /// </summary>
        event Action<RobotMissionInfo> OnMissionProcessUpdated; // 새로운 이벤트 추가

        /// <summary>
        /// 새로운 로봇 미션 프로세스를 시작합니다.
        /// </summary>
        /// <param name="processType">미션 프로세스의 유형 (예: "WaitToWrapTransfer").</param>
        /// <param name="missionSteps">이 프로세스를 구성하는 순차적인 미션 단계 목록.</param>
        /// <param name="racksLockedAtStart">이 프로세스 시작 시 잠긴 모든 랙의 ID 목록.</param>
        /// <param name="racksToProcess">여러 랙을 처리할 경우 (예: 출고) 해당 랙들의 ViewModel 목록.</param>
        /// <param name="initiatingCoilAddress">이 미션을 시작한 Modbus Coil의 주소 (경광등 제어용).</param>
        /// <param name="isWarehouseMission">이 미션이 창고 관련 미션인지 여부 (true: 창고, false: 포장실).</param>
        /// <returns>시작된 미션 프로세스의 고유 ID.</returns>
        Task<string> InitiateRobotMissionProcess(
            string processType,
            List<MissionStepDefinition> missionSteps,
            List<int> racksLockedAtStart,
            List<RackViewModel> racksToProcess = null,
            ushort? initiatingCoilAddress = null,
            bool isWarehouseMission = false
        );

        /// <summary>
        /// 특정 로봇 미션 프로세스의 HMI 상태를 업데이트합니다.
        /// </summary>
        /// <param name="processId">업데이트할 미션 프로세스의 고유 ID.</param>
        /// <param name="status">새로운 상태 문자열.</param>
        /// <param name="progressPercentage">새로운 진행률 (0-100).</param>
        /// <param name="currentStepDescription">현재 단계에 대한 설명.</param>
        Task UpdateHmiStatus(string processId, string status, int progressPercentage, string currentStepDescription);

        /// <summary>
        /// 특정 로봇 미션 프로세스의 단계를 완료 또는 실패로 표시합니다.
        /// </summary>
        /// <param name="processId">미션 프로세스의 고유 ID.</param>
        /// <param name="stepIndex">완료된 단계의 인덱스.</param>
        /// <param name="status">단계의 최종 상태 (COMPLETED, FAILED 등).</param>
        /// <param name="message">관련 메시지 (선택 사항).</param>
        Task CompleteMissionStep(string processId, int stepIndex, MissionStatusEnum status, string message = null);

        /// <summary>
        /// 현재 STARTED 상태이면서 창고 미션인 첫 번째 미션 프로세스를 반환합니다.
        /// (동시에 하나의 미션만 실행된다는 가정 하에 유효)
        /// </summary>
        /// <returns>STARTED 상태의 창고 미션 RobotMissionInfo 객체 또는 null.</returns>
        RobotMissionInfo GetFirstStartedWarehouseMission(); // 새로운 메서드 시그니처 추가
    }
}
