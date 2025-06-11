// Views/Popups/NumericKeypadPopup.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WPF_WMS01.Views.Popups
{
    public partial class NumericKeypadPopup : Window
    {
        // 텍스트가 변경될 때마다 발생시킬 이벤트 정의
        public event EventHandler<string> KeypadTextChanged;
        // Confirmed 이벤트는 제거

        public string CurrentInput { get; private set; } // 현재 키패드에 입력된 값 (내부 관리용)

        public NumericKeypadPopup()
        {
            InitializeComponent();
            CurrentInput = string.Empty;
        }

        private void NumberButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button != null)
            {
                CurrentInput += button.Content.ToString();
                OnKeypadTextChanged(CurrentInput); // 이벤트 발생
            }
        }

        private void BackspaceButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentInput.Length > 0)
            {
                CurrentInput = CurrentInput.Substring(0, CurrentInput.Length - 1);
                OnKeypadTextChanged(CurrentInput); // 이벤트 발생
            }
        }

        // 새로운 Clear 버튼 클릭 이벤트
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentInput = string.Empty; // 입력값 초기화
            OnKeypadTextChanged(CurrentInput); // 이벤트 발생
        }

        // EnterButton_Click 메서드 제거 (XAML에서 확인 버튼 삭제)
        // private void EnterButton_Click(object sender, RoutedEventArgs e) { ... }

        // 키보드 입력 처리 (물리 키보드 입력 방지 및 일부 유효 키 허용)
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key >= Key.D0 && e.Key <= Key.D9) // 숫자 키 (0-9)
            {
                CurrentInput += (e.Key - Key.D0).ToString();
                OnKeypadTextChanged(CurrentInput);
                e.Handled = true; // 이벤트 처리 완료
            }
            else if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9) // 숫자 패드 키 (0-9)
            {
                CurrentInput += (e.Key - Key.NumPad0).ToString();
                OnKeypadTextChanged(CurrentInput);
                e.Handled = true;
            }
            else if (e.Key == Key.Back) // Backspace 키
            {
                if (CurrentInput.Length > 0)
                {
                    CurrentInput = CurrentInput.Substring(0, CurrentInput.Length - 1);
                }
                OnKeypadTextChanged(CurrentInput);
                e.Handled = true;
            }
            // Enter 키는 이제 팝업을 닫지 않고, 단순 입력 완료 역할로만 사용하거나 제거 (팝업 닫기는 LostFocus에서 처리)
            //else if (e.Key == Key.Enter)
            //{
            //    // 엔터 키 처리 로직 (예: 포커스를 다음 컨트롤로 이동)
            //    // 여기서는 팝업을 닫지 않으므로 필요에 따라 다른 로직 추가
            //    e.Handled = true;
            //}
            else
            {
                e.Handled = true; // 그 외의 모든 키 입력 무시
            }
        }

        // KeypadTextChanged 이벤트를 발생시키는 헬퍼 메서드
        protected virtual void OnKeypadTextChanged(string newText)
        {
            KeypadTextChanged?.Invoke(this, newText);
        }

        // SetInitialValue 메서드 유지
        public void SetInitialValue(string initialValue)
        {
            CurrentInput = initialValue;
            OnKeypadTextChanged(CurrentInput);
        }
    }
}