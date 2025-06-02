using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Controls;

namespace ModernClicker
{
    public partial class MainWindow : Window
    {
        #region Fields
        private Thread clickThread;
        private bool clicking = false;
        private int clickInterval = 100; // milliseconds
        private const int HOTKEY_ID_TOGGLE = 9000;
        private bool isRecordingKey = false;
        private Key customKey = Key.F1;
        private string customKeyDisplay = "F1";
        #endregion

        #region Constructor and Initialization
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            KeyDown += MainWindow_KeyDown;
            Focusable = true; // Make window focusable to receive key events
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Initialize interval from textbox, default to 100ms
                if (!int.TryParse(IntervalBox.Text, out int interval) || interval < 1)
                {
                    IntervalBox.Text = "100";
                    clickInterval = 100;
                }
                else
                {
                    clickInterval = interval;
                }

                // Set default hotkey to F1 if nothing is selected
                if (MainKeyBox.SelectedIndex == -1)
                {
                    MainKeyBox.SelectedIndex = 0; // F1
                }
            }
            catch
            {
                IntervalBox.Text = "100";
                clickInterval = 100;
                MainKeyBox.SelectedIndex = 0;
            }

            // Register event handlers
            IntervalBox.TextChanged += IntervalBox_TextChanged;
            MainKeyBox.SelectionChanged += MainKeyBox_SelectionChanged;
            CtrlCheckBox.Checked += ModifierCheckBox_Changed;
            CtrlCheckBox.Unchecked += ModifierCheckBox_Changed;
            AltCheckBox.Checked += ModifierCheckBox_Changed;
            AltCheckBox.Unchecked += ModifierCheckBox_Changed;
            ShiftCheckBox.Checked += ModifierCheckBox_Changed;
            ShiftCheckBox.Unchecked += ModifierCheckBox_Changed;
            WinCheckBox.Checked += ModifierCheckBox_Changed;
            WinCheckBox.Unchecked += ModifierCheckBox_Changed;

            UpdateHotkeyDisplay();
            RegisterHotkeys();
        }

        // This method is for XAML Window_Loaded event (for animations)
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Window fade-in animation is handled by XAML triggers
        }

        private void IntervalBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ValidateInterval();
        }

        private void MainKeyBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateHotkeyFromComboBox();
        }

        private void ModifierCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateHotkeyDisplay();
        }
        #endregion

        #region UI Event Handlers
        private void Start_Click(object sender, RoutedEventArgs e)
        {
            StartClicking();
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            StopClicking();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void ToggleMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
            }
            else
            {
                this.WindowState = WindowState.Maximized;
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                try
                {
                    this.DragMove();
                }
                catch (InvalidOperationException)
                {
                    // Handle the case where DragMove is called when mouse is not pressed
                }
            }
        }

        private void RecordKey_Click(object sender, RoutedEventArgs e)
        {
            if (!isRecordingKey)
            {
                StartKeyRecording();
            }
            else
            {
                StopKeyRecording();
            }
        }
        #endregion

        #region Key Recording
        private void StartKeyRecording()
        {
            isRecordingKey = true;
            RecordKeyButton.Content = "Press any key... (ESC to cancel)";
            RecordKeyButton.Background = System.Windows.Media.Brushes.Orange;
            this.Focus(); // Focus the window to receive key events
        }

        private void StopKeyRecording()
        {
            isRecordingKey = false;
            RecordKeyButton.Content = "🎹 Record Custom Key";
            RecordKeyButton.Background = null; // Reset to default style
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (!isRecordingKey) return;

            // Cancel recording on Escape
            if (e.Key == Key.Escape)
            {
                StopKeyRecording();
                return;
            }

            // Don't allow modifier keys alone
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LWin || e.Key == Key.RWin)
            {
                return;
            }

            // Set the custom key
            customKey = e.Key;
            customKeyDisplay = GetKeyDisplayName(e.Key);

            // Update ComboBox to show "Custom" and update display
            bool foundCustomItem = false;
            for (int i = 0; i < MainKeyBox.Items.Count; i++)
            {
                if (MainKeyBox.Items[i] is ComboBoxItem item && item.Tag?.ToString() == "Custom")
                {
                    item.Content = $"Custom ({customKeyDisplay})";
                    MainKeyBox.SelectedIndex = i;
                    foundCustomItem = true;
                    break;
                }
            }

            if (!foundCustomItem)
            {
                // Add new custom item
                var customItem = new ComboBoxItem();
                customItem.Content = $"Custom ({customKeyDisplay})";
                customItem.Tag = "Custom";
                MainKeyBox.Items.Add(customItem);
                MainKeyBox.SelectedIndex = MainKeyBox.Items.Count - 1;
            }

            StopKeyRecording();
            UpdateHotkeyDisplay();

            e.Handled = true;
        }

        private string GetKeyDisplayName(Key key)
        {
            // Handle special cases for better display names
            switch (key)
            {
                case Key.Space: return "Space";
                case Key.Enter: return "Enter";
                case Key.Tab: return "Tab";
                case Key.Back: return "Backspace";
                case Key.Delete: return "Delete";
                case Key.Insert: return "Insert";
                case Key.Home: return "Home";
                case Key.End: return "End";
                case Key.PageUp: return "Page Up";
                case Key.PageDown: return "Page Down";
                case Key.Up: return "↑";
                case Key.Down: return "↓";
                case Key.Left: return "←";
                case Key.Right: return "→";
                case Key.NumPad0: return "Num 0";
                case Key.NumPad1: return "Num 1";
                case Key.NumPad2: return "Num 2";
                case Key.NumPad3: return "Num 3";
                case Key.NumPad4: return "Num 4";
                case Key.NumPad5: return "Num 5";
                case Key.NumPad6: return "Num 6";
                case Key.NumPad7: return "Num 7";
                case Key.NumPad8: return "Num 8";
                case Key.NumPad9: return "Num 9";
                case Key.Multiply: return "Num *";
                case Key.Add: return "Num +";
                case Key.Subtract: return "Num -";
                case Key.Divide: return "Num /";
                case Key.Decimal: return "Num .";
                case Key.OemTilde: return "~";
                case Key.OemMinus: return "-";
                case Key.OemPlus: return "+";
                case Key.OemOpenBrackets: return "[";
                case Key.OemCloseBrackets: return "]";
                case Key.OemPipe: return "\\";
                case Key.OemSemicolon: return ";";
                case Key.OemQuotes: return "'";
                case Key.OemComma: return ",";
                case Key.OemPeriod: return ".";
                case Key.OemQuestion: return "/";
                default:
                    return key.ToString();
            }
        }
        #endregion

        #region Input Validation
        private void ValidateInterval()
        {
            if (int.TryParse(IntervalBox.Text, out int interval))
            {
                if (interval < 1)
                {
                    interval = 1;
                    IntervalBox.Text = "1";
                }
                else if (interval > 10000)
                {
                    interval = 10000;
                    IntervalBox.Text = "10000";
                }
                clickInterval = interval;
            }
            else
            {
                // If parse fails, reset to default
                clickInterval = 100;
                IntervalBox.Text = "100";
            }
        }
        #endregion

        #region Hotkey Display Update
        private void UpdateHotkeyFromComboBox()
        {
            // If user selects a predefined key, clear custom key
            if (MainKeyBox.SelectedItem is ComboBoxItem item &&
                item.Tag?.ToString() != "Custom")
            {
                customKey = Key.None;
                customKeyDisplay = "";
            }
            UpdateHotkeyDisplay();
        }

        private void UpdateHotkeyDisplay()
        {
            try
            {
                string hotkey = BuildHotkeyString();
                CurrentHotkeyDisplay.Text = hotkey;
                HotkeyHintLabel.Text = $"💡 Press {hotkey} to toggle clicking on/off";
                RegisterHotkeys();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Update hotkey display error: {ex.Message}");
            }
        }

        private string BuildHotkeyString()
        {
            var parts = new System.Collections.Generic.List<string>();

            if (CtrlCheckBox.IsChecked == true) parts.Add("Ctrl");
            if (AltCheckBox.IsChecked == true) parts.Add("Alt");
            if (ShiftCheckBox.IsChecked == true) parts.Add("Shift");
            if (WinCheckBox.IsChecked == true) parts.Add("Win");

            // Get main key - check if it's custom first
            string mainKey = "F1"; // default
            if (customKey != Key.None)
            {
                mainKey = customKeyDisplay;
            }
            else if (MainKeyBox.SelectedItem is ComboBoxItem item &&
                     item.Tag?.ToString() != "Custom")
            {
                mainKey = item.Content.ToString();
            }
            parts.Add(mainKey);

            return string.Join(" + ", parts);
        }
        #endregion

        #region Clicking Logic
        private void StartClicking()
        {
            if (clicking) return;

            clicking = true;

            // Calculate CPS for display
            int cps = Math.Max(1, 1000 / clickInterval);
            StatusLabel.Text = $"Status: Active ({cps} CPS)";
            StatusLabel.Foreground = (System.Windows.Media.Brush)FindResource("Success");

            clickThread = new Thread(() =>
            {
                while (clicking)
                {
                    try
                    {
                        DoMouseClick();
                        Thread.Sleep(clickInterval);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Click thread error: {ex.Message}");
                        break;
                    }
                }
            });

            clickThread.IsBackground = true;
            clickThread.Start();
        }

        private void StopClicking()
        {
            clicking = false;

            Dispatcher.Invoke(() => {
                StatusLabel.Text = "Status: Ready";
                StatusLabel.Foreground = (System.Windows.Media.Brush)FindResource("Success");
            });

            clickThread?.Join(1000); // Wait max 1 second for thread to finish
        }
        #endregion

        #region Mouse Click Implementation
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x02;
        private const uint MOUSEEVENTF_LEFTUP = 0x04;

        private void DoMouseClick()
        {
            try
            {
                GetCursorPos(out POINT p);
                mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, (uint)p.X, (uint)p.Y, 0, UIntPtr.Zero);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Mouse click error: {ex.Message}");
            }
        }
        #endregion

        #region Global Hotkey Management
        private const uint MOD_NONE = 0x0000;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint WM_HOTKEY = 0x0312;

        [DllImport("user32.dll")]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source?.AddHook(HwndHook);
        }

        private void RegisterHotkeys()
        {
            try
            {
                var handle = new WindowInteropHelper(this).Handle;
                if (handle != IntPtr.Zero)
                {
                    UnregisterHotKey(handle, HOTKEY_ID_TOGGLE);

                    // Get the selected hotkey and modifiers
                    uint vkCode = GetVirtualKeyFromComboBox();
                    uint modifiers = GetModifierFlags();

                    if (vkCode != 0)
                    {
                        RegisterHotKey(handle, HOTKEY_ID_TOGGLE, modifiers, vkCode);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Hotkey registration error: {ex.Message}");
            }
        }

        private uint GetModifierFlags()
        {
            uint modifiers = MOD_NONE;

            if (CtrlCheckBox.IsChecked == true) modifiers |= MOD_CONTROL;
            if (AltCheckBox.IsChecked == true) modifiers |= MOD_ALT;
            if (ShiftCheckBox.IsChecked == true) modifiers |= MOD_SHIFT;
            if (WinCheckBox.IsChecked == true) modifiers |= MOD_WIN;

            return modifiers;
        }

        private uint GetVirtualKeyFromComboBox()
        {
            // Check if using custom key first
            if (customKey != Key.None)
            {
                return (uint)KeyInterop.VirtualKeyFromKey(customKey);
            }

            if (MainKeyBox.SelectedItem is ComboBoxItem item)
            {
                string keyName = item.Content.ToString();

                // Handle custom key display
                if (keyName.StartsWith("Custom ("))
                {
                    return (uint)KeyInterop.VirtualKeyFromKey(customKey);
                }

                switch (keyName)
                {
                    case "F1": return 0x70;
                    case "F2": return 0x71;
                    case "F3": return 0x72;
                    case "F4": return 0x73;
                    case "F5": return 0x74;
                    case "F6": return 0x75;
                    case "F7": return 0x76;
                    case "F8": return 0x77;
                    case "F9": return 0x78;
                    case "F10": return 0x79;
                    case "F11": return 0x7A;
                    case "F12": return 0x7B;
                    case "Space": return 0x20;
                    case "Enter": return 0x0D;
                    case "Tab": return 0x09;
                    case "A": return 0x41;
                    case "B": return 0x42;
                    case "C": return 0x43;
                    case "D": return 0x44;
                    case "E": return 0x45;
                    case "Q": return 0x51;
                    case "R": return 0x52;
                    case "T": return 0x54;
                    case "X": return 0x58;
                    case "Z": return 0x5A;
                    case "1": return 0x31;
                    case "2": return 0x32;
                    case "3": return 0x33;
                    case "4": return 0x34;
                    case "5": return 0x35;
                    default: return 0x70; // Default to F1
                }
            }
            return 0x70; // Default to F1
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID_TOGGLE)
            {
                // Toggle clicking state
                if (clicking)
                    StopClicking();
                else
                    StartClicking();

                handled = true;
            }

            return IntPtr.Zero;
        }
        #endregion

        #region Application Lifecycle
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                var handle = new WindowInteropHelper(this).Handle;
                if (handle != IntPtr.Zero)
                {
                    UnregisterHotKey(handle, HOTKEY_ID_TOGGLE);
                }
                StopClicking();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Cleanup error: {ex.Message}");
            }

            base.OnClosed(e);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            StopClicking();
            base.OnClosing(e);
        }
        #endregion
    }
}
