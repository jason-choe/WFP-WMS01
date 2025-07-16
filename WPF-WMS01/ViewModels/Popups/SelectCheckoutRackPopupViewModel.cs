using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using WPF_WMS01.Commands;
using WPF_WMS01.Models; // Rack 모델 사용
using System.Collections.Generic;
using System.Windows;
using System; // DateTime 사용을 위해 추가
using System.ComponentModel;
using System.Diagnostics;

namespace WPF_WMS01.ViewModels.Popups
{
    public class SelectCheckoutRackPopupViewModel : ViewModelBase
    {
        private ObservableCollection<CheckoutRackItem> _availableRacks;
        public ObservableCollection<CheckoutRackItem> AvailableRacks
        {
            get => _availableRacks;
            set => SetProperty(ref _availableRacks, value);
        }

        private bool? _dialogResult;
        public bool? DialogResult
        {
            get => _dialogResult;
            set => SetProperty(ref _dialogResult, value);
        }

        public ICommand ConfirmCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }

        private bool _isSelectedAll;
        // --- 수정된 부분: _isUpdatingAllSelection 플래그 추가 ---
        private bool _isUpdatingAllSelection = false; // 순환 업데이트 방지를 위한 플래그

        public bool IsSelectedAll
        {
            get => _isSelectedAll;
            set
            {
                // _isUpdatingAllSelection 플래그를 설정하여 개별 항목의 PropertyChanged 이벤트에서 UpdateIsSelectedAllState 호출 방지
                _isUpdatingAllSelection = true;
                try
                {
                    if (SetProperty(ref _isSelectedAll, value))
                    {
                        if (AvailableRacks != null)
                        {
                            foreach (var item in AvailableRacks)
                            {
                                item.IsSelected = value; // 개별 항목의 IsSelected 변경
                            }
                        }
                        // IsSelectedAll이 변경될 때 ConfirmCommand의 CanExecute 상태 업데이트 (안정적으로 호출)
                        ((RelayCommand)ConfirmCommand).RaiseCanExecuteChanged();
                    }
                }
                finally
                {
                    // 작업이 끝나면 플래그 해제
                    _isUpdatingAllSelection = false;
                }
            }
        }

        private string _subTitle; // LotNo 속성
        public string SubTitle
        {
            get => _subTitle;
            set
            {
                _subTitle = value;
                OnPropertyChanged();
            }
        }

        public ICommand ToggleSelectAllCommand { get; private set; } // '모두 선택/해제' 명령

        /*// 디자인 타임 전용 매개변수 없는 생성자 (XDG0062 오류 해결)
        // 실제 런타임에서는 이 생성자가 호출되지 않습니다.
        public SelectCheckoutRackPopupViewModel() : this(new List<Rack>())
        {
            // 이 생성자는 디자인 타임에만 사용됩니다.
            // 필요하다면 여기에 디자인 타임 데이터를 초기화할 수 있습니다.
            // 예를 들어, AvailableRacks.Add(new CheckoutRackItem(new Rack { Id = 1, Title = "Sample Rack" }, this));
        }*/

        public SelectCheckoutRackPopupViewModel(List<Rack> racks, string subTitle)
        {
            ConfirmCommand = new RelayCommand(ExecuteConfirm, CanExecuteConfirm);
            CancelCommand = new RelayCommand(ExecuteCancel);
            ToggleSelectAllCommand = new RelayCommand(ExecuteToggleSelectAll);
            // 입고 일자(RackedAt) 기준으로 오름차순 정렬하여 컬렉션 초기화
            AvailableRacks = new ObservableCollection<CheckoutRackItem>(
                racks.OrderBy(r => r.RackedAt).OrderBy(r => r.LotNumber) // LotNumber and RackedAt 기준으로 정렬
                     .Select(r => new CheckoutRackItem(r, this))
            );

            // 각 CheckoutRackItem의 IsSelected 속성 변경을 구독하여 IsSelectedAll 상태를 동기화
            foreach (var item in AvailableRacks)
            {
                item.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(CheckoutRackItem.IsSelected))
                    {
                        // --- 수정된 부분: 플래그 확인 후 UpdateIsSelectedAllState 호출 ---
                        if (!_isUpdatingAllSelection) // IsSelectedAll Setter에서 호출된 것이 아닐 때만 업데이트
                        {
                            UpdateIsSelectedAllState();
                        }
                    }
                };
            }

            SubTitle = subTitle;

            // 초기 IsSelectedAll 상태 설정
            UpdateIsSelectedAllState();

        }

        private void ExecuteConfirm(object parameter)
        {
            DialogResult = true;
        }

        private bool CanExecuteConfirm(object parameter)
        {
            // 하나 이상의 랙이 선택되어야 확인 버튼 활성화
            return AvailableRacks?.Any(item => item.IsSelected) == true;
        }

        private void ExecuteCancel(object parameter)
        {
            DialogResult = false;
        }

        // 선택된 랙들을 반환하는 속성
        public List<Rack> GetSelectedRacks()
        {
            return AvailableRacks.Where(item => item.IsSelected)
                                 .Select(item => item.RackModel)
                                 .ToList();
        }

        private void ExecuteToggleSelectAll(object parameter)
        {
            // IsSelectedAll 속성의 Setter에서 모든 항목의 IsSelected를 업데이트하므로
            // 여기서는 단순히 IsSelectedAll 값을 토글합니다.
            IsSelectedAll = !IsSelectedAll;
        }

        // 모든 CheckoutRackItem의 IsSelected 상태에 따라 IsSelectedAll을 업데이트하는 내부 메서드
        private void UpdateIsSelectedAllState()
        {
            // --- 수정된 부분: ConfirmCommand CanExecuteChanged 호출 추가 ---
            ((RelayCommand)ConfirmCommand).RaiseCanExecuteChanged();

            if (AvailableRacks == null || !AvailableRacks.Any())
            {
                // 리스트가 비어 있으면 '모두 선택'은 false 또는 원하는 초기값으로 설정
                SetProperty(ref _isSelectedAll, false, nameof(IsSelectedAll));
            }
            else
            {
                // 모든 랙이 선택되었는지 확인
                bool allSelected = AvailableRacks.All(item => item.IsSelected);
                // 현재 _isSelectedAll 값과 다를 경우에만 업데이트 및 PropertyChanged 호출
                if (_isSelectedAll != allSelected)
                {
                    SetProperty(ref _isSelectedAll, allSelected, nameof(IsSelectedAll));
                }
            }
        }

        // 팝업에서 사용할 랙 아이템 클래스 (체크박스 상태 포함)
        public class CheckoutRackItem : ViewModelBase
        {
            public Rack RackModel { get; }
            // 랙 번호, Lot No., 입고 일자를 직접 노출
            public string RackTitle => RackModel.Title;
            public string LotNumber => RackModel.LotNumber;
            public DateTime? RackedAt => RackModel.RackedAt; // Nullable DateTime
            //public string DisplayText => $"랙 {RackModel.Title} (제품: {GetBulletTypeName(RackModel.BulletType)})";

            private SelectCheckoutRackPopupViewModel _parentViewModel;

            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (SetProperty(ref _isSelected, value))
                    {
                        // --- 수정된 부분: _parentViewModel의 UpdateIsSelectedAllState 호출 로직 제거 (이제 SelectCheckoutRackPopupViewModel에서 직접 처리) ---
                        // 이 부분은 이제 CheckoutRackItem의 PropertyChanged 구독 로직에서 처리됩니다.
                        // 이전에 있었던 _parentViewModel.UpdateIsSelectedAllState() 호출은 제거합니다.
                        // 단, ConfirmCommand의 CanExecuteChanged는 계속 호출해야 합니다.
                        if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()) && _parentViewModel != null)
                        {
                            if (_parentViewModel.ConfirmCommand is RelayCommand confirmCommand)
                            {
                                confirmCommand.RaiseCanExecuteChanged();
                            }
                        }
                    }
                }
            }

            // 생성자 수정: 부모 ViewModel을 인자로 받도록 변경
            public CheckoutRackItem(Rack rack, SelectCheckoutRackPopupViewModel parentViewModel)
            {
                RackModel = rack;
                _parentViewModel = parentViewModel; // <-- 부모 ViewModel 저장
            }
        }
    }
}