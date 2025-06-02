using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace bananaclicker
{
    public partial class MainWindow : Window
    {
        private Thread clickThread;
        private bool clicking = false;
        private int targetCps = 100;
        private const int HOTKEY_ID_TOGGLE = 9000;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Fade in animation
            var fade = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.5));
            this.BeginAnimation(Window.OpacityProperty, fade);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Populate hotkey dropdown
            foreach (Key key in Enum.GetValues(typeof(Key)))
            {
                if (key >= Key.F1 && key <= Key.F12 || key >= Key.A && key <= Key.Z)
                    ToggleKeyBox.Items.Add(key);
            }

            // Load saved settings
            try
            {
                IntervalBox.Text = Properties.Settings.Default.Interval.ToString();
                string savedKey = Properties.Settings.Default.ToggleHotkey;
                if (Enum.TryParse(savedKey, out Key parsedKey))
                    ToggleKeyBox.SelectedItem = parsedKey;
                else
                    ToggleKeyBox.SelectedItem = Key.J;
            }
            catch
            {
                IntervalBox.Text = "6";
                ToggleKeyBox.SelectedItem = Key.J;
            }

            // Save on change
            IntervalBox.TextChanged += (_, __) => SaveSettings();
            ToggleKeyBox.SelectionChanged += (_, __) => { RegisterHotkeys(); SaveSettings(); };

            RegisterHotkeys();
        }

        private void Start_Click(object sender, RoutedEventArgs e) => StartClicking();
        private void Stop_Click(object sender, RoutedEventArgs e) => StopClicking();

        private void StartClicking()
        {
            if (!int.TryParse(IntervalBox.Text, out int intervalMs) || intervalMs < 1)
                intervalMs = 1;

            targetCps = 1000 / intervalMs;

            if (clicking) return;

            clicking = true;
            StatusLabel.Text = $"Status: Running at ~{targetCps} CPS";

            clickThread = new Thread(() =>
            {
                Stopwatch sw = new Stopwatch();
                long ticksPerClick = Stopwatch.Frequency / targetCps;

                sw.Start();
                long lastClick = sw.ElapsedTicks;

                while (clicking)
                {
                    long now = sw.ElapsedTicks;
                    if (now - lastClick >= ticksPerClick)
                    {
                        DoMouseClick();
                        lastClick = now;
                    }
                    else
                    {
                        Thread.SpinWait(50);
                    }
                }

                sw.Stop();
            });

            clickThread.IsBackground = true;
            clickThread.Priority = ThreadPriority.Highest;
            clickThread.Start();
        }

        private void StopClicking()
        {
            clicking = false;
            StatusLabel.Text = "Status: Stopped";
            clickThread?.Join();
        }

        #region Mouse Click

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")] static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x02;
        private const uint MOUSEEVENTF_LEFTUP = 0x04;

        private void DoMouseClick()
        {
            GetCursorPos(out POINT p);
            mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, (uint)p.X, (uint)p.Y, 0, UIntPtr.Zero);
        }

        #endregion

        #region Hotkeys

        private const uint MOD_NONE = 0x0000;
        private const uint WM_HOTKEY = 0x0312;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source.AddHook(HwndHook);
        }

        private void RegisterHotkeys()
        {
            var handle = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(handle, HOTKEY_ID_TOGGLE);

            if (ToggleKeyBox.SelectedItem is Key toggleKey)
            {
                RegisterHotKey(handle, HOTKEY_ID_TOGGLE, MOD_NONE, (uint)KeyInterop.VirtualKeyFromKey(toggleKey));
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            var handle = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(handle, HOTKEY_ID_TOGGLE);
            StopClicking();
            base.OnClosed(e);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }


        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID_TOGGLE)
            {
                if (clicking) StopClicking();
                else StartClicking();
                handled = true;
            }

            return IntPtr.Zero;
        }

        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        #endregion

        #region Save Settings

        private void SaveSettings()
        {
            if (int.TryParse(IntervalBox.Text, out int interval))
                Properties.Settings.Default.Interval = interval;

            if (ToggleKeyBox.SelectedItem is Key key)
                Properties.Settings.Default.ToggleHotkey = key.ToString();

            Properties.Settings.Default.Save();
        }

        #endregion
    }
}
