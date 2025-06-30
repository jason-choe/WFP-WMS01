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
        private readonly Func<object, Task> _execute;
        private readonly Func<object, bool> _canExecute;
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
        public AsyncRelayCommand(Func<object, Task> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// 명령이 현재 매개변수에서 실행될 수 있는지 여부를 결정합니다.
        /// </summary>
        /// <param name="parameter">명령 매개변수.</param>
        /// <returns>명령이 실행될 수 있으면 true, 그렇지 않으면 false.</returns>
        public bool CanExecute(object parameter)
        {
            // 명령이 실행 중이 아니거나 (이중 클릭 방지), 선택적 canExecute 함수가 true를 반환할 때만 실행 가능
            return !_isExecuting && (_canExecute == null || _canExecute(parameter));
        }

        /// <summary>
        /// 명령을 실행합니다.
        /// </summary>
        /// <param name="parameter">명령 매개변수.</param>
        public async void Execute(object parameter)
        {
            if (CanExecute(parameter))
            {
                try
                {
                    _isExecuting = true;
                    RaiseCanExecuteChanged(); // 버튼 비활성화 (UI 업데이트)
                    await _execute(parameter); // 비동기 작업 실행
                }
                finally
                {
                    _isExecuting = false;
                    RaiseCanExecuteChanged(); // 버튼 다시 활성화
                }
            }
        }

        /// <summary>
        /// CanExecute 상태를 수동으로 강제로 다시 평가하도록 알립니다.
        /// 일반적으로 Command에 영향을 미치는 속성이 변경될 때 호출됩니다.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
