// ViewModels/Popups/VehicleInformationPopupViewModel.cs
using System.ComponentModel;
using WPF_WMS01.ViewModels; // ViewModelBase 참조를 위해 추가

namespace WPF_WMS01.ViewModels.Popups
{
    public class VehicleInformationPopupViewModel : ViewModelBase
    {
        private string _message;

        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        public VehicleInformationPopupViewModel(string message)
        {
            Message = message;
        }
    }
}
