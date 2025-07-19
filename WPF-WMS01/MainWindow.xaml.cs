// MainWindow.xaml.cs
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;
using WPF_WMS01.Views.Popups; // NumericKeypadPopup 네임스페이스 추가
using System;

namespace WPF_WMS01
{
    public partial class MainWindow : Window
    {
        private NumericKeypadPopup _currentKeypadPopup; // 현재 열린 키패드 팝업 참조

        public MainWindow()
        {
            InitializeComponent();
            //this.DataContext = new ViewModels.MainViewModel();
        }

        private void ProductCodeTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 이 부분은 물리 키보드 입력을 막는 용도로 유지
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void ProductBoxCount_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 이 부분은 물리 키보드 입력을 막는 용도로 유지
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void ProductCodeTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox != null)
            {
                // 기존 팝업이 있다면 닫기
                if (_currentKeypadPopup != null)
                {
                    _currentKeypadPopup.Close();
                    _currentKeypadPopup = null;
                }

                // 시스템 터치 키보드 (TabTip.exe)가 실행 중인지 확인 (선택 사항: 두 키패드 동시 출현 방지)
                // bool isTabTipRunning = Process.GetProcessesByName("TabTip").Any();
                // if (isTabTipRunning)
                // {
                //     // 시스템 키보드가 이미 나타났다면, 사용자 정의 키패드 팝업을 띄우지 않음.
                //     // 이 경우, 사용자는 시스템 키보드를 사용해야 함.
                //     return;
                // }

                // TextBox의 직접 입력을 막기 위해 ReadOnly 설정
                textBox.IsReadOnly = true;
                // TextBox가 포커스를 얻으면서 시스템 키보드가 나타나는 것을 방지하기 위해 포커스를 임시로 해제
                Keyboard.ClearFocus();

                _currentKeypadPopup = new NumericKeypadPopup();
                _currentKeypadPopup.Owner = this;
                _currentKeypadPopup.WindowStartupLocation = WindowStartupLocation.CenterOwner;

                // 팝업 이벤트 구독
                _currentKeypadPopup.KeypadTextChanged += Keypad_KeypadTextChanged;

                // 팝업이 닫힐 때 TextBox의 IsReadOnly를 false로 되돌리고 참조 해제
                _currentKeypadPopup.Closed += (s, ev) =>
                {
                    _currentKeypadPopup = null;
                    if (textBox != null)
                    {
                        textBox.IsReadOnly = false; // 다시 쓰기 가능
                        // 팝업이 닫힌 후 포커스 이동 (선택 사항)
                        // textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                    }
                };

                // 팝업에 현재 TextBox 값 초기화 (팝업이 열릴 때 바로 반영되도록)
                _currentKeypadPopup.SetInitialValue(textBox.Text);

                // 모달이 아닌 일반 창으로 열기 (ShowDialog() 대신 Show())
                _currentKeypadPopup.Show();

                // 팝업이 포커스를 가져가도록 설정
                _currentKeypadPopup.Activate();
            }
        }

        private void ProductBoxCount_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox != null)
            {
                // 기존 팝업이 있다면 닫기
                if (_currentKeypadPopup != null)
                {
                    _currentKeypadPopup.Close();
                    _currentKeypadPopup = null;
                }

                // 시스템 터치 키보드 (TabTip.exe)가 실행 중인지 확인 (선택 사항: 두 키패드 동시 출현 방지)
                // bool isTabTipRunning = Process.GetProcessesByName("TabTip").Any();
                // if (isTabTipRunning)
                // {
                //     // 시스템 키보드가 이미 나타났다면, 사용자 정의 키패드 팝업을 띄우지 않음.
                //     // 이 경우, 사용자는 시스템 키보드를 사용해야 함.
                //     return;
                // }

                // TextBox의 직접 입력을 막기 위해 ReadOnly 설정
                textBox.IsReadOnly = true;
                // TextBox가 포커스를 얻으면서 시스템 키보드가 나타나는 것을 방지하기 위해 포커스를 임시로 해제
                Keyboard.ClearFocus();

                _currentKeypadPopup = new NumericKeypadPopup();
                _currentKeypadPopup.Owner = this;
                _currentKeypadPopup.WindowStartupLocation = WindowStartupLocation.CenterOwner;

                // 팝업 이벤트 구독
                _currentKeypadPopup.KeypadTextChanged += Keypad_KeypadTextChanged2;

                // 팝업이 닫힐 때 TextBox의 IsReadOnly를 false로 되돌리고 참조 해제
                _currentKeypadPopup.Closed += (s, ev) =>
                {
                    _currentKeypadPopup = null;
                    if (textBox != null)
                    {
                        textBox.IsReadOnly = false; // 다시 쓰기 가능
                        // 팝업이 닫힌 후 포커스 이동 (선택 사항)
                        // textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
                    }
                };

                // 팝업에 현재 TextBox 값 초기화 (팝업이 열릴 때 바로 반영되도록)
                _currentKeypadPopup.SetInitialValue(textBox.Text);

                // 모달이 아닌 일반 창으로 열기 (ShowDialog() 대신 Show())
                _currentKeypadPopup.Show();

                // 팝업이 포커스를 가져가도록 설정
                _currentKeypadPopup.Activate();
            }
        }

        // TextBox가 포커스를 잃을 때 키패드 팝업 닫기
        private void ProductCodeTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // 관련된 논리적 포커스 이동인지 확인
            // 키패드 팝업이 TextBox의 "논리적" 자식(Logical Child)으로 간주되지 않기 때문에
            // LostFocus 이벤트가 발생할 수 있습니다.
            // 팝업이 열릴 때 잠시 포커스를 잃는 문제가 발생할 수 있으므로,
            // 팝업이 '활성' 상태가 아닐 때만 닫도록 조건을 추가할 수 있습니다.
            // 하지만 기본적으로 팝업은 GotFocus에서 열리고, 다른 곳을 클릭하면 LostFocus가 발생하므로
            // 이 시점에 닫는 것이 일반적입니다.
            if (_currentKeypadPopup != null && _currentKeypadPopup.IsVisible)
            {
                // 포커스를 잃은 것이 팝업으로의 이동이 아니라 다른 컨트롤로의 이동이라면 팝업 닫기
                // 이 부분을 더 정교하게 제어하려면 P/Invoke를 통해 활성 윈도우를 확인하는 복잡한 로직이 필요할 수 있습니다.
                // 일단은 단순하게 팝업이 열려있다면 닫도록 합니다.
                _currentKeypadPopup.Close();
                _currentKeypadPopup = null;
            }
        }

        private void ProductBoxCount_LostFocus(object sender, RoutedEventArgs e)
        {
            // 관련된 논리적 포커스 이동인지 확인
            // 키패드 팝업이 TextBox의 "논리적" 자식(Logical Child)으로 간주되지 않기 때문에
            // LostFocus 이벤트가 발생할 수 있습니다.
            // 팝업이 열릴 때 잠시 포커스를 잃는 문제가 발생할 수 있으므로,
            // 팝업이 '활성' 상태가 아닐 때만 닫도록 조건을 추가할 수 있습니다.
            // 하지만 기본적으로 팝업은 GotFocus에서 열리고, 다른 곳을 클릭하면 LostFocus가 발생하므로
            // 이 시점에 닫는 것이 일반적입니다.
            if (_currentKeypadPopup != null && _currentKeypadPopup.IsVisible)
            {
                // 포커스를 잃은 것이 팝업으로의 이동이 아니라 다른 컨트롤로의 이동이라면 팝업 닫기
                // 이 부분을 더 정교하게 제어하려면 P/Invoke를 통해 활성 윈도우를 확인하는 복잡한 로직이 필요할 수 있습니다.
                // 일단은 단순하게 팝업이 열려있다면 닫도록 합니다.
                _currentKeypadPopup.Close();
                _currentKeypadPopup = null;
            }
        }

        // 키패드에서 텍스트가 변경될 때마다 호출될 이벤트 핸들러
        private void Keypad_KeypadTextChanged(object sender, string newText)
        {
            // TextBox의 MaxLength를 고려하여 텍스트 업데이트
            if (ProductCodeTextBox != null)
            {
                if (newText.Length > ProductCodeTextBox.MaxLength)
                {
                    ProductCodeTextBox.Text = newText.Substring(0, ProductCodeTextBox.MaxLength);
                }
                else
                {
                    ProductCodeTextBox.Text = newText;
                }
                ProductCodeTextBox.CaretIndex = ProductCodeTextBox.Text.Length; // 커서를 맨 뒤로 이동
            }
        }

        private void Keypad_KeypadTextChanged2(object sender, string newText)
        {
            // TextBox의 MaxLength를 고려하여 텍스트 업데이트
            if (ProductBoxCount != null)
            {
                if (newText.Length > ProductBoxCount.MaxLength)
                {
                    ProductBoxCount.Text = newText.Substring(0, ProductBoxCount.MaxLength);
                }
                else
                {
                    ProductBoxCount.Text = newText;
                }
                ProductBoxCount.CaretIndex = ProductBoxCount.Text.Length; // 커서를 맨 뒤로 이동
            }
        }

        // Window가 닫힐 때 팝업도 함께 닫히도록 처리 (선택 사항)
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_currentKeypadPopup != null)
            {
                _currentKeypadPopup.Close();
                _currentKeypadPopup = null;
            }
        }

        // 붙여넣기 방지 (선택 사항, 여전히 유용)
        private void ProductCodeTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                Regex regex = new Regex("[^0-9]+");
                if (regex.IsMatch(text))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }
    }
}