using System.Windows;
using WPF_WMS01.ViewModels.Popups; // ViewModel 네임스페이스에 따라 수정이 필요할 수 있습니다.

namespace WPF_WMS01.Views.Popups
{
    /// <summary>
    /// InOutLedgerView.xaml에 대한 상호 작용 논리
    /// 이 뷰는 입/출고 내역을 보여주는 독립적인 팝업 창입니다.
    /// </summary>
    public partial class InOutLedgerView : Window
    {
        public InOutLedgerView()
        {
            InitializeComponent();
            // DataContext는 일반적으로 팝업을 띄우는 쪽에서 설정하지만,
            // 테스트를 위해 주석 처리된 상태로 남겨둡니다.
            // DataContext = new InOutLedgerViewModel(); 
        }

        /// <summary>
        /// '닫기' 버튼 클릭 이벤트 핸들러. 창을 닫습니다.
        /// </summary>
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}