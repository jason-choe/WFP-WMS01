using System.Windows;
using System.ComponentModel;
using WPF_WMS01.ViewModels.Popups;

namespace WPF_WMS01.Views.Popups
{
    public partial class SelectEmptyRackPopupView : Window
    {
        public SelectEmptyRackPopupView()
        {
            InitializeComponent();
            this.DataContextChanged += SelectEmptyRackPopupView_DataContextChanged;
        }

        private void SelectEmptyRackPopupView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is INotifyPropertyChanged oldViewModel)
            {
                oldViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }
            if (e.NewValue is SelectEmptyRackPopupViewModel newViewModel)
            {
                newViewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SelectEmptyRackPopupViewModel.DialogResult))
            {
                if (DataContext is SelectEmptyRackPopupViewModel viewModel && viewModel.DialogResult.HasValue)
                {
                    this.DialogResult = viewModel.DialogResult.Value;
                    this.Close();
                }
            }
        }
    }
}