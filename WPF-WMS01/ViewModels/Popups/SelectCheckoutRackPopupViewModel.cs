﻿using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using WPF_WMS01.Commands;
using WPF_WMS01.Models; // Rack 모델 사용
using System.Collections.Generic;
using System.Windows;
using System; // DateTime 사용을 위해 추가
using System.ComponentModel;

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

        // 디자인 타임 전용 매개변수 없는 생성자 (XDG0062 오류 해결)
        // 실제 런타임에서는 이 생성자가 호출되지 않습니다.
        public SelectCheckoutRackPopupViewModel() : this(new List<Rack>())
        {
            // 이 생성자는 디자인 타임에만 사용됩니다.
            // 필요하다면 여기에 디자인 타임 데이터를 초기화할 수 있습니다.
            // 예를 들어, AvailableRacks.Add(new CheckoutRackItem(new Rack { Id = 1, Title = "Sample Rack" }, this));
        }

        public SelectCheckoutRackPopupViewModel(List<Rack> racks)
        {
            // 입고 일자(RackedAt) 기준으로 오름차순 정렬하여 컬렉션 초기화
            AvailableRacks = new ObservableCollection<CheckoutRackItem>(
                racks.OrderBy(r => r.RackedAt) // RackedAt 기준으로 정렬
                     .Select(r => new CheckoutRackItem(r, this))
            );

            ConfirmCommand = new RelayCommand(ExecuteConfirm, CanExecuteConfirm);
            CancelCommand = new RelayCommand(ExecuteCancel);
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
                        // 선택 상태 변경 시 부모 ViewModel의 ConfirmCommand의 CanExecute를 다시 평가
                        // Application.Current가 null인 디자인 타임에는 호출되지 않도록 방어 코드 추가
                        if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()) && _parentViewModel != null && _parentViewModel.ConfirmCommand is RelayCommand confirmCommand)

                        {
                            confirmCommand.RaiseCanExecuteChanged(); // <-- 수정된 부분
                        }
                        // 선택 상태 변경 시 부모 ViewModel의 ConfirmCommand의 CanExecute를 다시 평가
                    }
                }
            }

            // 생성자 수정: 부모 ViewModel을 인자로 받도록 변경
            public CheckoutRackItem(Rack rack, SelectCheckoutRackPopupViewModel parentViewModel)
            {
                RackModel = rack;
                _parentViewModel = parentViewModel; // <-- 부모 ViewModel 저장
            }

            private string GetBulletTypeName(int bulletType)
            {
                switch (bulletType)
                {
                    case 1: return "223 제품";
                    case 2: return "308 제품";
                    default: return "알 수 없음";
                }
            }
        }
    }
}