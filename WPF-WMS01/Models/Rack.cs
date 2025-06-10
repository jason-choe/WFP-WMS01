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
                    //OnPropertyChanged(nameof(Id));
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
            get { return RackType * 7 + BulletType; }
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
                    //OnPropertyChanged(nameof(LotNumber));
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
                    //OnPropertyChanged(nameof(RackedAt));
                }
            }
        }

        public Rack()
        {
            // 이 생성자는 RackViewModel에서 새로운 랙을 추가할 때 호출될 수 있습니다.
            // 또는 DatabaseService에서 더미 데이터를 생성할 때 호출될 수 있습니다.
            // 특정 ID에 대한 생성자 호출만 로깅하려면 여기에 조건 추가
        }
    }
}