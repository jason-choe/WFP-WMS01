// ViewModels/Popups/SelectProductionLinePopupViewModel.cs
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows; // MessageBox 사용을 위해 추가
using WPF_WMS01.Commands;
using WPF_WMS01.Models; // ProductionLineLocation 사용을 위해 추가

namespace WPF_WMS01.ViewModels.Popups
{
    public class SelectProductionLinePopupViewModel : ViewModelBase
    {
        private ObservableCollection<ProductionLineLocation> _productionLineLocations;
        public ObservableCollection<ProductionLineLocation> ProductionLineLocations
        {
            get => _productionLineLocations;
            set => SetProperty(ref _productionLineLocations, value);
        }

        private ProductionLineLocation _selectedLocation;
        public ProductionLineLocation SelectedLocation
        {
            get => _selectedLocation;
            set
            {
                if (SetProperty(ref _selectedLocation, value))
                {
                    // 선택된 장소가 변경될 때 확인 버튼의 CanExecute 상태 갱신
                    ((RelayCommand)ConfirmCommand).RaiseCanExecuteChanged();
                }
            }
        }
        private string _lotNo; // LotNo 속성
        public string LotNo
        {
            get => _lotNo;
            set
            {
                _lotNo = value;
                OnPropertyChanged();
            }
        }

        private bool? _dialogResult;
        public bool? DialogResult
        {
            get => _dialogResult;
            set => SetProperty(ref _dialogResult, value);
        }

        public ICommand ConfirmCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }

        public SelectProductionLinePopupViewModel(string lotNo)
        {
            // 샘플 생산 라인 장소 데이터 초기화 (6개)
            ProductionLineLocations = new ObservableCollection<ProductionLineLocation>
            {
                new ProductionLineLocation { Id = 1, Name = "7.62mm 라인" },
                new ProductionLineLocation { Id = 2, Name = "5.56mm 라인 1" },
                new ProductionLineLocation { Id = 3, Name = "5.56mm 라인 2" },
                new ProductionLineLocation { Id = 4, Name = "5.56mm 라인 3" },
                new ProductionLineLocation { Id = 5, Name = "5.56mm 라인 4" },
                new ProductionLineLocation { Id = 6, Name = "수작업 라인 1" },
                new ProductionLineLocation { Id = 7, Name = "수작업 라인 2" },
            };

            ConfirmCommand = new RelayCommand(ExecuteConfirm, CanExecuteConfirm);
            CancelCommand = new RelayCommand(ExecuteCancel);

            LotNo = lotNo;
        }

        private void ExecuteConfirm(object parameter)
        {
            if (SelectedLocation == null)
            {
                MessageBox.Show("재공품이 반출될 장소를 선택해주세요.", "선택 필요", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
        }

        private bool CanExecuteConfirm(object parameter)
        {
            // 장소가 선택되었을 때만 확인 버튼 활성화
            return SelectedLocation != null;
        }

        private void ExecuteCancel(object parameter)
        {
            DialogResult = false;
        }
    }
}