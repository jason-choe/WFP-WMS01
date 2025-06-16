// Commands/AsyncRelayCommand.cs
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WPF_WMS01.Commands
{
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<object, Task> _execute;
        private readonly Func<object, bool> _canExecute;
        private bool _isExecuting; // 명령이 현재 실행 중인지 나타내는 플래그

        public AsyncRelayCommand(Func<object, Task> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            // 명령이 실행 중이 아닐 때만 실행 가능하며, 추가적인 canExecute 조건이 있다면 함께 평가
            return !_isExecuting && (_canExecute?.Invoke(parameter) ?? true);
        }

        public async void Execute(object parameter)
        {
            if (CanExecute(parameter))
            {
                try
                {
                    _isExecuting = true;
                    RaiseCanExecuteChanged(); // 실행 중이므로 CanExecute 상태 변경 알림
                    await _execute(parameter);
                }
                finally
                {
                    _isExecuting = false;
                    RaiseCanExecuteChanged(); // 실행 완료되었으므로 CanExecute 상태 변경 알림
                }
            }
        }

        public void RaiseCanExecuteChanged()
        {
            // UI 스레드에서 CanExecuteChanged 이벤트를 발생시키도록 Dispatcher를 사용할 수 있지만,
            // WPF의 CommandManager는 이를 자동으로 처리하는 경우가 많습니다.
            // 명시적으로 UI 스레드에서 실행되도록 하려면 Application.Current.Dispatcher.Invoke 등을 사용해야 합니다.
            // 여기서는 단순성을 위해 직접 호출합니다.
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}