// ViewModels/Popups/MissionStepStatusViewModel.cs
using System.Windows.Media; // Color, SolidColorBrush 사용을 위해 필요
using WPF_WMS01.Models; // MissionStatusEnum 사용을 위해 필요

namespace WPF_WMS01.ViewModels.Popups
{
    /// <summary>
    /// 로봇 미션 상태 팝업 내에서 개별 미션 단계의 상태를 나타내는 ViewModel입니다.
    /// </summary>
    public class MissionStepStatusViewModel : ViewModelBase
    {
        private string _description;
        private string _statusText;
        private SolidColorBrush _statusColor;

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public SolidColorBrush StatusColor
        {
            get => _statusColor;
            set => SetProperty(ref _statusColor, value);
        }

        public MissionStepStatusViewModel(string description, MissionStatusEnum status)
        {
            Description = description;
            SetStatus(status);
        }

        /// <summary>
        /// 미션 단계의 상태를 업데이트하고 그에 따른 텍스트와 색상을 설정합니다.
        /// </summary>
        /// <param name="status">새로운 미션 상태.</param>
        public void SetStatus(MissionStatusEnum status)
        {
            switch (status)
            {
                case MissionStatusEnum.COMPLETED:
                    StatusText = "완료";
                    StatusColor = new SolidColorBrush(Colors.Green);
                    break;
                case MissionStatusEnum.STARTED:
                    StatusText = "실행 중";
                    StatusColor = new SolidColorBrush(Colors.Blue);
                    break;
                case MissionStatusEnum.ACCEPTED:
                    StatusText = "수락됨 (대기 중)";
                    StatusColor = new SolidColorBrush(Colors.Orange);
                    break;
                case MissionStatusEnum.RECEIVED:
                    StatusText = "수신됨 (대기 중)";
                    StatusColor = new SolidColorBrush(Colors.Orange);
                    break;
                case MissionStatusEnum.PENDING:
                    StatusText = "대기 중";
                    StatusColor = new SolidColorBrush(Colors.Gray);
                    break;
                case MissionStatusEnum.REJECTED:
                    StatusText = "거부됨";
                    StatusColor = new SolidColorBrush(Colors.Red);
                    break;
                case MissionStatusEnum.FAILED:
                    StatusText = "실패";
                    StatusColor = new SolidColorBrush(Colors.Red);
                    break;
                case MissionStatusEnum.CANCELLED:
                    StatusText = "취소됨";
                    StatusColor = new SolidColorBrush(Colors.DarkRed);
                    break;
                default:
                    StatusText = "알 수 없음";
                    StatusColor = new SolidColorBrush(Colors.Black);
                    break;
            }
        }
    }
}
