// Commands/RelayCommand.cs
using System;
using System.Windows.Input; // ICommand 사용을 위해 추가

namespace WPF_WMS01.Commands
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute; // canExecute는 선택 사항이므로 null 허용
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        public void Execute(object parameter)
        {
            _execute(parameter);
        }

        /// <summary>
        /// 이 메서드는 Command의 CanExecute 상태가 변경되었음을 UI에 알립니다.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            // CommandManager.InvalidateRequerySuggested()는 CommandManager가 관리하는 모든 Command에 대해
            // CanExecuteChanged 이벤트를 발생시키도록 요청합니다.
            // 이렇게 하면 CommandManager.RequerySuggested에 등록된 모든 핸들러가 실행됩니다.
            CommandManager.InvalidateRequerySuggested();
        }
    }
}