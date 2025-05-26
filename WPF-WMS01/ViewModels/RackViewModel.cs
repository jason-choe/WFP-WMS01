// ViewModels/RackViewModel.cs
using WPF_WMS01.Models; // YourAppName을 실제 프로젝트 이름으로 변경하세요.
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WPF_WMS01.ViewModels
{
    public class RackViewModel : INotifyPropertyChanged
    {
        private readonly Rack _rack;

        public RackViewModel(Rack rack)
        {
            _rack = rack;
            _rack.PropertyChanged += (s, e) => OnPropertyChanged(e.PropertyName); // Model 변경 시 ViewModel도 업데이트
        }

        public string Id => _rack.Id;
        public string Title => _rack.Title;

        public int ImageIndex
        {
            get => _rack.ImageIndex;
            set => _rack.ImageIndex = value; // Model의 ImageIndex를 통해 값을 변경
        }

        public bool IsVisible
        {
            get => _rack.IsVisible;
            set => _rack.IsVisible = value; // Model의 IsVisible을 통해 값을 변경
        }
        public bool IsLocked
        {
            get => _rack.IsLocked;
            set => _rack.IsLocked = value; // Model의 IsLocked 통해 값을 변경
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}