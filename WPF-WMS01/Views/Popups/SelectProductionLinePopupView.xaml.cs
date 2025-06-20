// Views/Popups/SelectProductionLinePopupView.xaml.cs
using System.Windows;
using System.ComponentModel;
using WPF_WMS01.ViewModels.Popups;

namespace WPF_WMS01.Views.Popups
{
    public partial class SelectProductionLinePopupView : Window
    {
        public SelectProductionLinePopupView()
        {
            InitializeComponent();
            this.DataContextChanged += SelectProductionLinePopupView_DataContextChanged;
        }

        private void SelectProductionLinePopupView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is INotifyPropertyChanged oldViewModel)
            {
                oldViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }
            if (e.NewValue is SelectProductionLinePopupViewModel newViewModel)
            {
                newViewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SelectProductionLinePopupViewModel.DialogResult))
            {
                if (DataContext is SelectProductionLinePopupViewModel viewModel && viewModel.DialogResult.HasValue)
                {
                    this.DialogResult = viewModel.DialogResult.Value;
                    this.Close();
                }
            }
        }
    }
}