// ViewModels/Popups/MissionStatusPopupViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input; // ICommand 사용
using System.Windows.Media; // SolidColorBrush 사용
using WPF_WMS01.Commands; // RelayCommand 사용
using WPF_WMS01.Models; // MissionStatusEnum, MissionStepDefinition, RobotMissionInfo 사용

namespace WPF_WMS01.ViewModels.Popups
{
    /// <summary>
    /// 로봇 미션 상태 팝업 창의 ViewModel입니다.
    /// </summary>
    public class MissionStatusPopupViewModel : ViewModelBase
    {
        private string _processId;
        private string _processType;
        private string _overallStatusText;
        private SolidColorBrush _overallStatusColor;
        private int _overallProgressPercentage;
        private string _currentStepDescription;
        private ObservableCollection<MissionStepStatusViewModel> _missionStepsStatus;

        public string ProcessId
        {
            get => _processId;
            set => SetProperty(ref _processId, value);
        }

        public string ProcessType
        {
            get => _processType;
            set => SetProperty(ref _processType, value);
        }

        public string OverallStatusText
        {
            get => _overallStatusText;
            set => SetProperty(ref _overallStatusText, value);
        }

        public SolidColorBrush OverallStatusColor
        {
            get => _overallStatusColor;
            set => SetProperty(ref _overallStatusColor, value);
        }

        public int OverallProgressPercentage
        {
            get => _overallProgressPercentage;
            set => SetProperty(ref _overallProgressPercentage, value);
        }

        public string CurrentStepDescription
        {
            get => _currentStepDescription;
            set => SetProperty(ref _currentStepDescription, value);
        }

        public ObservableCollection<MissionStepStatusViewModel> MissionStepsStatus
        {
            get => _missionStepsStatus;
            set => SetProperty(ref _missionStepsStatus, value);
        }

        public ICommand CloseCommand { get; }

        // 팝업을 닫기 위한 Action (View에서 호출될 것임)
        public Action CloseAction { get; set; }

        /// <summary>
        /// XAML 디자이너를 위한 매개변수 없는 생성자입니다.
        /// </summary>
        public MissionStatusPopupViewModel() : this("Design-Time ID", "Design-Time Type", new List<MissionStepDefinition>())
        {
            // 디자인 타임에 표시될 기본값 설정
            OverallStatusText = "디자인 모드";
            OverallStatusColor = new SolidColorBrush(Colors.LightGray);
            CurrentStepDescription = "디자인 타임 미리보기";
            OverallProgressPercentage = 0;
            MissionStepsStatus.Add(new MissionStepStatusViewModel("단계 1 (디자인)", MissionStatusEnum.PENDING));
            MissionStepsStatus.Add(new MissionStepStatusViewModel("단계 2 (디자인)", MissionStatusEnum.PENDING));
        }

        /// <summary>
        /// MissionStatusPopupViewModel의 새 인스턴스를 초기화합니다.
        /// </summary>
        /// <param name="processId">미션 프로세스의 고유 ID.</param>
        /// <param name="processType">미션 프로세스의 유형.</param>
        /// <param name="missionStepDefinitions">전체 미션 단계 정의 목록.</param>
        public MissionStatusPopupViewModel(string processId, string processType, List<MissionStepDefinition> missionStepDefinitions)
        {
            ProcessId = processId;
            ProcessType = processType;
            MissionStepsStatus = new ObservableCollection<MissionStepStatusViewModel>();
            InitializeMissionSteps(missionStepDefinitions);
            UpdateStatus(null, missionStepDefinitions); // 초기 상태 설정 (미션 정보가 아직 없는 경우)

            CloseCommand = new RelayCommand(p => CloseAction?.Invoke());
        }

        /// <summary>
        /// 초기 미션 단계 목록을 설정합니다. 모든 단계는 초기에는 "대기 중" 상태입니다.
        /// </summary>
        /// <param name="missionStepDefinitions">전체 미션 단계 정의 목록.</param>
        private void InitializeMissionSteps(List<MissionStepDefinition> missionStepDefinitions)
        {
            foreach (var step in missionStepDefinitions)
            {
                MissionStepsStatus.Add(new MissionStepStatusViewModel(step.ProcessStepDescription, MissionStatusEnum.PENDING));
            }
        }

        /// <summary>
        /// 로봇 미션의 현재 상태를 기반으로 팝업의 내용을 업데이트합니다.
        /// </summary>
        /// <param name="missionInfo">현재 로봇 미션 정보 (RobotMissionService에서 전달됨).</param>
        /// <param name="allMissionStepDefinitions">전체 미션 단계 정의 목록.</param>
        public void UpdateStatus(RobotMissionInfo missionInfo, List<MissionStepDefinition> allMissionStepDefinitions)
        {
            // 전체 미션 상태 업데이트
            if (missionInfo != null)
            {
                OverallStatusText = missionInfo.HmiStatus.Status;
                OverallProgressPercentage = missionInfo.HmiStatus.ProgressPercentage;
                CurrentStepDescription = missionInfo.HmiStatus.CurrentStepDescription;

                switch (missionInfo.CurrentStatus)
                {
                    case MissionStatusEnum.COMPLETED:
                        OverallStatusColor = new SolidColorBrush(Colors.Green);
                        break;
                    case MissionStatusEnum.FAILED:
                    case MissionStatusEnum.REJECTED:
                    case MissionStatusEnum.CANCELLED:
                        OverallStatusColor = new SolidColorBrush(Colors.Red);
                        break;
                    case MissionStatusEnum.STARTED:
                        OverallStatusColor = new SolidColorBrush(Colors.Blue);
                        break;
                    default:
                        OverallStatusColor = new SolidColorBrush(Colors.Orange); // PENDING, RECEIVED, ACCEPTED 등
                        break;
                }

                // 개별 미션 단계 상태 업데이트
                for (int i = 0; i < allMissionStepDefinitions.Count; i++)
                {
                    if (i < MissionStepsStatus.Count) // ViewModel 컬렉션이 정의 목록보다 작을 수 있으므로 방어 코드
                    {
                        var stepVm = MissionStepsStatus[i];
                        if (i < missionInfo.CurrentStepIndex)
                        {
                            // 현재 진행 중인 단계보다 이전 단계는 '완료'
                            stepVm.SetStatus(MissionStatusEnum.COMPLETED);
                        }
                        else if (i == missionInfo.CurrentStepIndex)
                        {
                            // 현재 진행 중인 단계는 '실행 중' 또는 '수락됨' 등 ANT 상태에 따라
                            // 여기서는 ANT의 NavigationState를 직접 반영하는 대신, HMI Status를 따릅니다.
                            // 더 세분화된 상태가 필요하면 missionInfo.CurrentMissionDetail.NavigationState 등을 활용
                            if (missionInfo.CurrentStatus == MissionStatusEnum.COMPLETED && i == allMissionStepDefinitions.Count - 1)
                            {
                                stepVm.SetStatus(MissionStatusEnum.COMPLETED); // 마지막 단계가 완료되었을 때
                            }
                            else if (missionInfo.CurrentStatus == MissionStatusEnum.FAILED || missionInfo.CurrentStatus == MissionStatusEnum.REJECTED || missionInfo.CurrentStatus == MissionStatusEnum.CANCELLED)
                            {
                                stepVm.SetStatus(missionInfo.CurrentStatus); // 미션 실패/거부/취소 시 현재 단계도 실패로 표시
                            }
                            else
                            {
                                stepVm.SetStatus(MissionStatusEnum.STARTED); // 현재 진행 중인 단계
                            }
                        }
                        else
                        {
                            // 현재 진행 중인 단계보다 이후 단계는 '대기 중'
                            stepVm.SetStatus(MissionStatusEnum.PENDING);
                        }
                    }
                }
            }
            else
            {
                // missionInfo가 null인 초기 상태 (미션 시작 전)
                OverallStatusText = "미션 시작 대기 중";
                OverallStatusColor = new SolidColorBrush(Colors.Gray);
                OverallProgressPercentage = 0;
                CurrentStepDescription = "로봇 미션 대기 중...";
                foreach (var stepVm in MissionStepsStatus)
                {
                    stepVm.SetStatus(MissionStatusEnum.PENDING);
                }
            }
        }
    }
}
