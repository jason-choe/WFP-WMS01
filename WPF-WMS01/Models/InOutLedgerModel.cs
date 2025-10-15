using System;
using WPF_WMS01.ViewModels; // ViewModelBase 참조

namespace WPF_WMS01.Models
{
    /// <summary>
    /// [dbo].[InOutLedger] 테이블에 대응하는 입출고 이력 데이터 모델입니다.
    /// </summary>
    public class InOutLedger : ViewModelBase
    {
        private long _id;
        public long Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        private string _rackName;
        public string RackName
        {
            get => _rackName;
            set => SetProperty(ref _rackName, value);
        }

        private short? _bulletType;
        /// <summary>
        /// 총알 유형 (smallint)
        /// </summary>
        public short? BulletType
        {
            get => _bulletType;
            set => SetProperty(ref _bulletType, value);
        }

        private string _lotNumber;
        public string LotNumber
        {
            get => _lotNumber;
            set => SetProperty(ref _lotNumber, value);
        }

        private short? _boxCount;
        /// <summary>
        /// 입/출고된 박스 수량 (smallint)
        /// </summary>
        public short? BoxCount
        {
            get => _boxCount;
            set => SetProperty(ref _boxCount, value);
        }

        private DateTime? _inboundAt;
        /// <summary>
        /// 입고 시간 (datetime)
        /// </summary>
        public DateTime? InboundAt
        {
            get => _inboundAt;
            set => SetProperty(ref _inboundAt, value);
        }

        private DateTime? _outboundAt;
        /// <summary>
        /// 출고 시간 (datetime)
        /// </summary>
        public DateTime? OutboundAt
        {
            get => _outboundAt;
            set => SetProperty(ref _outboundAt, value);
        }

        /// <summary>
        /// 입출고 구분 표시용 속성 (Computed Property)
        /// </summary>
        public string TypeDisplay
        {
            get
            {
                if (_inboundAt.HasValue && !_outboundAt.HasValue)
                    return "입고 (현재 재고)";
                if (_inboundAt.HasValue && _outboundAt.HasValue)
                    return "출고 완료";
                return "상태 미확인";
            }
        }
    }
}
