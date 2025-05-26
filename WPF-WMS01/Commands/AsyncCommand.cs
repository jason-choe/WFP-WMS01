// Commands/AsyncCommand.cs
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WPF_WMS01.Commands
{
    public class AsyncCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool> _canExecute;
        private bool _isExecuting;

        public AsyncCommand(Func<Task> execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool IsExecuting
        {
            get => _isExecuting;
            set
            {
                if (_isExecuting != value)
                {
                    _isExecuting = value;
                    OnCanExecuteChanged();
                }
            }
        }

        public event EventHandler CanExecuteChanged;

        protected virtual void OnCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool CanExecute(object parameter) => !IsExecuting && (_canExecute?.Invoke() ?? true);

        public async void Execute(object parameter)
        {
            IsExecuting = true;
            try
            {
                await _execute();
            }
            finally
            {
                IsExecuting = false;
            }
        }
    }
}