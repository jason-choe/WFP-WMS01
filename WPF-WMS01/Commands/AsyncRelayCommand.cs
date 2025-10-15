// Commands/AsyncRelayCommand.cs
using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WPF_WMS01.Commands
{
    /// <summary>
    /// 비동기 작업을 지원하는 ICommand 구현입니다.
    /// </summary>
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<object, Task> _executeWithParam;
        private readonly Func<object, bool> _canExecuteWithParam;

        // Parameterless delegates (for overloads)
        private readonly Func<Task> _execute;
        private readonly Func<bool> _canExecute;

        private bool _isExecuting; // 명령이 현재 실행 중인지 여부

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        /// <summary>
        /// 새 AsyncRelayCommand의 인스턴스를 초기화합니다.
        /// </summary>
        /// <param name="execute">실행할 비동기 작업.</param>
        /// <param name="canExecute">명령을 실행할 수 있는지 여부를 결정하는 함수 (선택 사항).</param>
        // ====================================================================
        // 1. Parameter-based Constructors (기존 로직 유지)
        // ====================================================================
        public AsyncRelayCommand(Func<object, Task> execute, Func<object, bool> canExecute = null)
        {
            _executeWithParam = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecuteWithParam = canExecute;
        }

        // ====================================================================
        // 2. Parameterless Constructors (CS1503 해결을 위한 추가 오버로드)
        // ====================================================================
        public AsyncRelayCommand(Func<Task> execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// CanExecute 상태를 수동으로 강제로 다시 평가하도록 알립니다.
        /// 이 메서드를 호출하면 모든 CS1061 오류가 해결됩니다.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        /// <summary>
        /// 명령이 현재 매개변수에서 실행될 수 있는지 여부를 결정합니다.
        /// </summary>
        /// <param name="parameter">명령 매개변수.</param>
        /// <returns>명령이 실행될 수 있으면 true, 그렇지 않으면 false.</returns>
        public bool CanExecute(object parameter)
        {
            if (_isExecuting) return false;

            // Parameter-based check
            if (_canExecuteWithParam != null)
            {
                return _canExecuteWithParam(parameter);
            }

            // Parameterless check
            if (_canExecute != null)
            {
                return _canExecute();
            }

            return true; // No canExecute logic provided
        }

        /// <summary>
        /// 명령을 실행합니다.
        /// </summary>
        /// <param name="parameter">명령 매개변수.</param>
        public async void Execute(object parameter)
        {
            if (!CanExecute(parameter)) return;

            _isExecuting = true;
            // Execute 전에 CanExecute 상태를 즉시 업데이트하여 버튼을 비활성화합니다.
            RaiseCanExecuteChanged();

            try
            {
                // Parameter-based execute
                if (_executeWithParam != null)
                {
                    await _executeWithParam(parameter);
                }
                // Parameterless execute
                else if (_execute != null)
                {
                    await _execute();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AsyncRelayCommand] Command execution failed: {ex.Message}");
                // 필요에 따라 오류 알림 로직 추가
            }
            finally
            {
                _isExecuting = false;
                // Execute 후에 CanExecute 상태를 업데이트하여 버튼을 다시 활성화합니다.
                RaiseCanExecuteChanged();
            }
        }
    }
}
