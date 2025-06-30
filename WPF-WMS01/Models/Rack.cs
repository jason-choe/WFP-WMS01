using System;
using System.ComponentModel;

namespace WPF_WMS01.Models
{
    public class Rack : INotifyPropertyChanged
    {
        // INotifyPropertyChanged 구현을 위한 이벤트 핸들러
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private int _id;
        public int Id
        {
            get { return _id; }
            set
            {
                if (_id != value)
                {
                    _id = value;
                    // Id는 일반적으로 생성 시에만 설정되며 변경되지 않으므로 OnPropertyChanged는 필요하지 않을 수 있습니다.
                    // 그러나 ViewModel의 래퍼 속성이 업데이트될 때 변경 사항을 반영하려면 주석을 해제할 수 있습니다.
                    // OnPropertyChanged(nameof(Id));
                }
            }
        }

        private string _title;
        public string Title
        {
            get { return _title; }
            set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged(nameof(Title));
                }
            }
        }

        private bool _isLocked;
        public bool IsLocked
        {
            get { return _isLocked; }
            set
            {
                if (_isLocked != value)
                {
                    _isLocked = value;
                    OnPropertyChanged(nameof(IsLocked));
                }
            }
        }

        private bool _isVisible;
        public bool IsVisible
        {
            get { return _isVisible; }
            set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;
                    OnPropertyChanged(nameof(IsVisible));
                }
            }
        }

        private int _rackType;
        public int RackType
        {
            get { return _rackType; }
            set
            {
                if (_rackType != value)
                {
                    _rackType = value; // 값을 할당
                    OnPropertyChanged(nameof(RackType));
                    OnPropertyChanged(nameof(ImageIndex)); // RackType 변경 시 ImageIndex 재계산 및 알림
                }
            }
        }

        private int _bulletType;
        public int BulletType
        {
            get { return _bulletType; }
            set
            {
                if (_bulletType != value)
                {
                    _bulletType = value;
                    OnPropertyChanged(nameof(BulletType));
                    OnPropertyChanged(nameof(ImageIndex)); // BulletType 변경 시 ImageIndex 재계산 및 알림
                }
            }
        }

        // 계산된 ImageIndex 속성
        public int ImageIndex
        {
            get { return RackType * 13 + BulletType; }
        }

        private string _lotNumber;
        public string LotNumber
        {
            get { return _lotNumber; }
            set
            {
                if (_lotNumber != value)
                {
                    _lotNumber = value;
                    //OnPropertyChanged(nameof(LotNumber)); // LotNumber 변경 시 알림 추가
                }
            }
        }

        private DateTime? _rackedAt;
        public DateTime? RackedAt
        {
            get { return _rackedAt; }
            set
            {
                if (_rackedAt != value)
                {
                    _rackedAt = value;
                    //OnPropertyChanged(nameof(RackedAt)); // RackedAt 변경 시 알림 추가
                }
            }
        }

        private int _locationArea; // 새로운 location_area 필드
        public int LocationArea // 새로운 LocationArea 속성
        {
            get { return _locationArea; }
            set
            {
                if (_locationArea != value)
                {
                    _locationArea = value;
                    //OnPropertyChanged(nameof(LocationArea)); // LocationArea 변경 시 알림
                }
            }
        }

        // 모든 속성을 받는 생성자 추가
        public Rack(int id, string title, int rackType, int bulletType, bool isVisible, bool isLocked, string lotNumber, DateTime? rackedAt, int locationArea)
        {
            _id = id; // Id는 private set이므로 직접 할당
            _title = title;
            _rackType = rackType;
            _bulletType = bulletType;
            _isVisible = isVisible;
            _isLocked = isLocked;
            _lotNumber = lotNumber;
            _rackedAt = rackedAt;
            _locationArea = locationArea; // LocationArea 초기화
        }

        public Rack()
        {
            // 이 생성자는 RackViewModel에서 새로운 랙을 추가할 때 호출될 수 있습니다.
            // 또는 DatabaseService에서 더미 데이터를 생성할 때 호출될 수 있습니다.
            // 특정 ID에 대한 생성자 호출만 로깅하려면 여기에 조건 추가
        }
    }
}