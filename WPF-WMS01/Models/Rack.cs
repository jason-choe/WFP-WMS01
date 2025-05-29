using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WPF_WMS01.Models
{
    public class Rack : INotifyPropertyChanged
    {
        private int _id;
        private string _title;
        private int _imageIndex;
        private bool _isVisible;
        private bool _isLocked;
        private int _rackType;      /* 0: for unwrapped palette, 1: for wrapped palette */
        private int _bulletType;    /* 0: none, 1: 223 bullet, 2: 308 bullet */

        public int Id
        {
            get => _id;
            set
            {
                if (_id != value)
                {
                    _id = value;
                    OnPropertyChanged(nameof(Id));
                }
            }
        }

        public string Title
        {
            get => _title;
            set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged(nameof(Title));
                }
            }
        }

        // 랙 타입: 랙의 용도를 나타냄 (0-1), 0: for unwrapped palette, 1: for wrapped palette
        public int RackType
        {
            get => _rackType;
            set
            {
                if (_rackType != value)
                {
                    _rackType = value;
                    OnPropertyChanged(nameof(RackType));
                    //OnPropertyChanged(nameof(ImageIndex)); // RackType 변경 시 ImageIndex 재계산
                }
            }
        }
        // 총알 타입: 적재된 제품의 종류 (0-2), 0: none, 1: 223 bullet, 2: 308 bullet
        public int BulletType
        {
            get => _bulletType;
            set
            {
                if (_bulletType != value)
                {
                    _bulletType = value;
                    OnPropertyChanged(nameof(BulletType));
                    //OnPropertyChanged(nameof(ImageIndex)); // BulletType 변경 시 ImageIndex 재계산
                }
            }
        }
        // 이미지 인덱스: 랙의 상태를 나타냄 (0-5)
        public int ImageIndex
        {
            get => _imageIndex;
            set
            {
                if (_imageIndex != value)
                {
                    _imageIndex = value;
                    OnPropertyChanged(nameof(ImageIndex));
                }
            }
        }

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;
                    OnPropertyChanged(nameof(IsVisible));
                }
            }
        }
        public bool IsLocked
        {
            get => _isLocked;
            set
            {
                if (_isLocked != value)
                {
                    _isLocked = value;
                    OnPropertyChanged(nameof(IsLocked));
                }
            }
        }

        // INotifyPropertyChanged 구현을 위한 이벤트 핸들러
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}