// ViewModels/Popups/MissionStatusPopupViewModel.cs
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
        private int _overallCurrentStep;
        private int _overallTotalStep;
        private string _currentStepDescription;
        private string _subOpDescription;
        private ObservableCollection<MissionStepStatusViewModel> _missionStepsStatus;

        // 새로 추가된 Title 속성
        private string _payload;
        public string Payload
        {
            get => _payload;
            set => SetProperty(ref _payload, value);
        }

        // 새로 추가된 Title 속성
        private string _title;
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

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

        public int OverallCurrentStep
        {
            get => _overallCurrentStep;
            set => SetProperty(ref _overallCurrentStep, value);
        }

        public int OverallTotalStep
        {
            get => _overallTotalStep;
            set => SetProperty(ref _overallTotalStep, value);
        }

        public string CurrentStepDescription
        {
            get => _currentStepDescription;
            set => SetProperty(ref _currentStepDescription, value);
        }

        public string SubOpDescription
        {
            get => _subOpDescription;
            set => SetProperty(ref _subOpDescription, value);
        }

        public ObservableCollection<MissionStepStatusViewModel> MissionStepsStatus
        {
            get => _missionStepsStatus;
            set => SetProperty(ref _missionStepsStatus, value);
        }

        public ICommand CloseCommand { get; } // 기존 닫기 명령 (팝업 닫기 액션에 연결)
        public ICommand ConfirmCloseCommand { get; } // 새로 추가된 '확인' 버튼용 명령

        // 팝업을 닫기 위한 Action (View에서 호출될 것임)
        public Action CloseAction { get; set; }

        /// <summary>
        /// XAML 디자이너를 위한 매개변수 없는 생성자입니다.
        /// </summary>
        public MissionStatusPopupViewModel() : this("Design-Time ID", "Design-Time Type", new List<MissionStepDefinition>(), false)
        {
            // 디자인 타임에 표시될 기본값 설정
            OverallStatusText = "디자인 모드";
            OverallStatusColor = new SolidColorBrush(Colors.LightGray);
            CurrentStepDescription = "디자인 타임 미리보기";
            OverallProgressPercentage = 0;
            OverallTotalStep = 0;
            OverallCurrentStep = 0;
            Payload = "디자인 타임 미리보기";
            MissionStepsStatus.Add(new MissionStepStatusViewModel("단계 1 (디자인)", MissionStatusEnum.PENDING));
            MissionStepsStatus.Add(new MissionStepStatusViewModel("단계 2 (디자인)", MissionStatusEnum.PENDING));
        }

        /// <summary>
        /// MissionStatusPopupViewModel의 새 인스턴스를 초기화합니다.
        /// </summary>
        /// <param name="processId">미션 프로세스의 고유 ID.</param>
        /// <param name="processType">미션 프로세스의 유형.</param>
        /// <param name="missionStepDefinitions">전체 미션 단계 정의 목록.</param>
        public MissionStatusPopupViewModel(string processId, string processType, List<MissionStepDefinition> missionStepDefinitions, bool isWarehouseMission)
        {
            ProcessId = processId;
            ProcessType = processType;
            MissionStepsStatus = new ObservableCollection<MissionStepStatusViewModel>();
            InitializeMissionSteps(missionStepDefinitions);
            // 초기 상태 설정: missionInfo가 아직 없는 경우, 기본 "대기 중" 상태로 설정
            UpdateStatus(null, missionStepDefinitions);

            CloseCommand = new RelayCommand(p => CloseAction?.Invoke()); // 기존 닫기 명령
            ConfirmCloseCommand = new RelayCommand(p => CloseAction?.Invoke()); // '확인' 버튼 클릭 시 팝업 닫기
            Title = (isWarehouseMission ? "창고":"포장실") + " AMR 미션 진행 상황";
            Payload = "";
        }

        /// <summary>
        /// 초기 미션 단계 목록을 설정합니다. 모든 단계는 초기에는 "대기 중" 상태입니다.
        /// </summary>
        /// <param name="missionStepDefinitions">전체 미션 단계 정의 목록.</param>
        public void InitializeMissionSteps(List<MissionStepDefinition> missionStepDefinitions)
        {
            MissionStepsStatus.Clear(); // 기존 내용이 있을 수 있으므로 클리어
            foreach (var step in missionStepDefinitions)
            {
                MissionStepsStatus.Add(new MissionStepStatusViewModel(step.ProcessStepDescription, MissionStatusEnum.PENDING));
            }
        }

        /// <summary>
        /// 로봇 미션의 현재 상태를 기반으로 팝업의 내용을 업데이트합니다.
        /// </summary>
        /// <param name="missionInfo">현재 로봇 미션 정보 (RobotMissionService에서 전달됨).</param>
        /// <param name="allMissionStepDefinitions">전체 미션 단계 정의 목록 (초기화 또는 재설정용).</param>
        public void UpdateStatus(RobotMissionInfo missionInfo, List<MissionStepDefinition> allMissionStepDefinitions)
        {
            // 전체 미션 상태 업데이트
            if (missionInfo != null)
            {
                // ProcessId와 ProcessType은 생성자에서 설정되거나, 첫 업데이트 시 설정됩니다.
                // 여기서는 이미 설정된 값을 사용하거나, 필요한 경우 업데이트합니다.
                if (ProcessId == "N/A" && !string.IsNullOrEmpty(missionInfo.ProcessId))
                {
                    ProcessId = missionInfo.ProcessId;
                }
                ProcessType = missionInfo.ProcessType; // ProcessType은 변경될 수 있으므로 항상 업데이트

                OverallStatusText = missionInfo.HmiStatus?.Status; // Null-conditional operator
                OverallProgressPercentage = missionInfo.HmiStatus?.ProgressPercentage ?? 0;
                OverallCurrentStep = missionInfo.CurrentStepIndex + 1;
                Debug.WriteLine($"[CheckPoint] missionInfo.CurrentStepIndex = {missionInfo.CurrentStepIndex}, OverallCurrentStep = {OverallCurrentStep}");
                OverallTotalStep = missionInfo.TotalSteps;
                CurrentStepDescription = missionInfo.HmiStatus?.CurrentStepDescription;
                SubOpDescription = missionInfo.HmiStatus?.SubOpDescription;
                if (missionInfo.CurrentStepIndex < allMissionStepDefinitions.Count) // Index was out of range exception 방지
                    Payload = allMissionStepDefinitions[missionInfo.CurrentStepIndex].Payload.Equals("AMR_2")?"Poongsan_2":"Poongsan_1";

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
                // allMissionStepDefinitions가 변경될 수 있으므로, MissionStepsStatus를 재초기화합니다.
                // 이전에 InitializeMissionSteps에서 생성된 MissionStepsStatus와 동기화합니다.
                if (MissionStepsStatus.Count != allMissionStepDefinitions.Count ||
                    !MissionStepsStatus.Select(s => s.Description).SequenceEqual(allMissionStepDefinitions.Select(d => d.ProcessStepDescription)))
                {
                    InitializeMissionSteps(allMissionStepDefinitions); // 구조가 변경되었으면 다시 초기화
                }

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
                            else if (missionInfo.CurrentStatus == MissionStatusEnum.RECEIVED)
                            {
                                stepVm.SetStatus(missionInfo.CurrentStatus); // 서버가 미션 수신함
                            }
                            else if (missionInfo.CurrentStatus == MissionStatusEnum.ACCEPTED)
                            {
                                stepVm.SetStatus(missionInfo.CurrentStatus); // 서버가 미션 수락함
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
                // InitializeMissionSteps에서 이미 PENDING으로 설정되었으므로 추가적인 업데이트는 필요 없을 수 있습니다.
                // 하지만 명시적으로 다시 설정하여 확실히 PENDING 상태임을 보장합니다.
                foreach (var stepVm in MissionStepsStatus)
                {
                    stepVm.SetStatus(MissionStatusEnum.PENDING);
                }
            }
            // 미션 완료/실패 시 팝업을 자동으로 닫는 로직은 여기서 제거됩니다.
            // 사용자가 '확인' 버튼을 눌러야 닫히도록 합니다.
        }
    }
}
