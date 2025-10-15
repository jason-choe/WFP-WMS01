using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using WPF_WMS01.Models;
using WPF_WMS01.Services; // CS1061 오류 해결을 위해 DatabaseService 네임스페이스 추가
using WPF_WMS01.Commands; // AsyncRelayCommand를 사용하기 위해 네임스페이스 추가

namespace WPF_WMS01.ViewModels.Popups
{
    // 클래스 이름이 'InOurLedgerViewModel'로 오타가 났을 수 있지만,
    // 오류 메시지 경로를 따라 'InOurLedgerViewModel'로 진행합니다.
    public class InOutLedgerViewModel : ViewModelBase
    {
        private readonly DatabaseService _databaseService;

        // =========================================================================
        // 1. 속성 정의 (CS0103 오류 해결)
        // =========================================================================

        // 검색 필터 속성
        private string _searchLotNumber;
        public string SearchLotNumber
        {
            get => _searchLotNumber;
            set => SetProperty(ref _searchLotNumber, value); // Line 53 오류 해결
        }

        private DateTime? _inboundStart;
        public DateTime? InboundStart
        {
            get => _inboundStart;
            set => SetProperty(ref _inboundStart, value); // Line 54 오류 해결
        }

        private DateTime? _inboundEnd;
        public DateTime? InboundEnd
        {
            get => _inboundEnd;
            set => SetProperty(ref _inboundEnd, value); // Line 55 오류 해결
        }

        private DateTime? _outboundStart;
        public DateTime? OutboundStart
        {
            get => _outboundStart;
            set => SetProperty(ref _outboundStart, value); // Line 56, 58, 89 오류 해결
        }

        private DateTime? _outboundEnd;
        public DateTime? OutboundEnd
        {
            get => _outboundEnd;
            set => SetProperty(ref _outboundEnd, value); // Line 57, 90 오류 해결
        }

        private bool _showOnlyCurrentStock = false;
        /// <summary>
        /// 체크 시 outbound_at이 NULL인 (현재 재고) 항목만 조회
        /// </summary>
        public bool ShowOnlyCurrentStock
        {
            get => _showOnlyCurrentStock;
            set
            {
                if (SetProperty(ref _showOnlyCurrentStock, value))
                {
                    // "현재 재고만 보기"가 켜지면 outbound 기간 필터는 무시됩니다.
                    if (_showOnlyCurrentStock)
                    {
                        OutboundStart = null;
                        OutboundEnd = null;
                    }
                    // 체크박스 상태 변경 시 자동 조회
                    SearchCommand.Execute(null);
                }
            }
        }

        // 데이터 속성 및 상태
        private ObservableCollection<InOutLedger> _ledgerItems;
        /// <summary>
        /// 조회된 입출고 이력 목록
        /// </summary>
        public ObservableCollection<InOutLedger> LedgerItems // Line 25, 47, 64 오류 해결
        {
            get => _ledgerItems;
            set => SetProperty(ref _ledgerItems, value);
        }

        private bool _isBusy;
        public bool IsBusy // Line 44, 46, 74 오류 해결
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public AsyncRelayCommand SearchCommand { get; private set; }

        // =========================================================================
        // 2. 생성자 및 Command 초기화
        // =========================================================================

        /*public InOurLedgerViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            this.ViewTitle = "입출고 장부 관리"; // 팝업 창 제목 설정
            LedgerItems = new ObservableCollection<InOutLedger>();

            // AsyncRelayCommand를 매개변수 없는 ExecuteSearchAsync와 연결
            SearchCommand = new AsyncRelayCommand(ExecuteSearchAsync);
        }*/
        
        // XAML 디자인 타임용 생성자 (필요 시)
        //public InOutLedgerViewModel() : this(new DatabaseService()) { }

        private string _viewTitle;
        public string ViewTitle
        {
            get => _viewTitle;
            set => SetProperty(ref _viewTitle, value);
        }
        // =========================================================================
        // 3. Command 실행 로직
        // =========================================================================
        /// <summary>
        /// 검색 필터에 따라 데이터를 조회하고 목록을 업데이트합니다.
        /// </summary>
        private async Task ExecuteSearchAsync()
        {
            if (IsBusy) return;

            IsBusy = true; // Line 46 오류 해결
            LedgerItems.Clear(); // Line 47 오류 해결

            try
            {
                // Line 52: DatabaseService에 GetInOutLedgerAsync 메서드가 있다고 가정하고 호출
                // 네임스페이스 추가 및 속성 선언으로 오류 해결
                List<InOutLedger> results = await _databaseService.GetInOutLedgerAsync(
                    SearchLotNumber,
                    InboundStart,
                    InboundEnd,
                    ShowOnlyCurrentStock ? (DateTime?)null : OutboundStart, // Line 56, 89 오류 해결
                    ShowOnlyCurrentStock ? (DateTime?)null : OutboundEnd,   // Line 57, 90 오류 해결
                    ShowOnlyCurrentStock);

                if (results != null)
                {
                    foreach (var item in results)
                    {
                        LedgerItems.Add(item); // Line 64 오류 해결
                    }
                }
            }
            catch (Exception ex)
            {
                // 오류 처리 로직
                System.Diagnostics.Debug.WriteLine($"[InOurLedgerViewModel] Data search failed: {ex.Message}");
            }
            finally
            {
                IsBusy = false; // Line 74 오류 해결
            }
        }
    }
}
