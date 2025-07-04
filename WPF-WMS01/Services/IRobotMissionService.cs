// Services/IRobotMissionService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WPF_WMS01.Models;
using WPF_WMS01.ViewModels; // RackViewModel 참조를 위해 필요

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

        /// <summary>
        /// 새로운 로봇 미션 프로세스를 시작합니다.
        /// </summary>
        /// <param name="processType">미션 프로세스의 유형 (예: "WaitToWrapTransfer").</param>
        /// <param name="missionSteps">이 프로세스를 구성하는 순차적인 미션 단계 목록.</param>
        /// <param name="sourceRack">원본 랙 ViewModel.</param>
        /// <param name="destinationRack">목적지 랙 ViewModel.</param>
        /// <param name="destinationLine">목적지 생산 라인.</param>
        /// <param name="getInputStringForButtonFunc">MainViewModel에서 InputStringForButton 값을 가져오는 함수.</param>
        /// <returns>시작된 미션 프로세스의 고유 ID.</returns>
        Task<string> InitiateRobotMissionProcess(
            string processType,
            List<MissionStepDefinition> missionSteps,
            RackViewModel sourceRack,
            RackViewModel destinationRack,
            Location destinationLine,
            Func<string> getInputStringForButtonFunc
        );
    }
}
