using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using WPF_WMS01.Commands;
using WPF_WMS01.Models; // Rack 모델 사용

namespace WPF_WMS01.ViewModels.Popups
{
    public class SelectStorageRackPopupViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private ObservableCollection<Rack> _availableRacks;
        public ObservableCollection<Rack> AvailableRacks
        {
            get => _availableRacks;
            set
            {
                _availableRacks = value;
                OnPropertyChanged();
            }
        }

        private Rack _selectedRack;
        public Rack SelectedRack
        {
            get => _selectedRack;
            set
            {
                _selectedRack = value;
                OnPropertyChanged();
                ((RelayCommand)SelectCommand).RaiseCanExecuteChanged();
            }
        }

        public ICommand SelectCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }

        public bool DialogResult { get; private set; }

        public SelectStorageRackPopupViewModel(IEnumerable<Rack> racks)
        {
            AvailableRacks = new ObservableCollection<Rack>(racks);
            SelectCommand = new RelayCommand(ExecuteSelect, CanExecuteSelect);
            CancelCommand = new RelayCommand(ExecuteCancel);
        }

        private void ExecuteSelect(object parameter)
        {
            DialogResult = true;
        }

        private bool CanExecuteSelect(object parameter)
        {
            return SelectedRack != null;
        }

        private void ExecuteCancel(object parameter)
        {
            DialogResult = false;
        }
    }
}