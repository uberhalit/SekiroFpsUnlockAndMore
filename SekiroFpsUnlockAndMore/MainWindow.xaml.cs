using System;
using System.IO;
using System.Windows;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace SekiroFpsUnlockAndMore
{
    public partial class MainWindow : Window
    {
        internal const string PROCESS_NAME = "sekiro";
        internal const string PROCESS_TITLE = "Sekiro";
        internal const string PROCESS_DESCRIPTION = "Shadows Die Twice";

        internal const string PATTERN_FRAMELOCK = "00 88 88 3C 4C 89 AB 00"; // ?? 88 88 3C 4C 89 AB ?? // pattern/signature of frame rate limiter, first byte (last in mem) can can be 88/90 instead of 89 due to precision loss on floating point numbers
        internal const string PATTERN_FRAMELOCK_MASK = "?xxxxxx?"; // mask for frame rate limiter signature scanning
        internal const string PATTERN_FRAMELOCK_LONG = "44 88 6B 00 C7 43 00 89 88 88 3C 4C 89 AB 00 00 00 00"; // 44 88 6B ?? C7 43 ?? 89 88 88 3C 4C 89 AB ?? ?? ?? ??
        internal const string PATTERN_FRAMELOCK_LONG_MASK = "xxx?xx?xxxxxxx????";
        internal const int PATTERN_FRAMELOCK_LONG_OFFSET = 7;
        internal const string PATTERN_FRAMELOCK_FUZZY = "C7 43 00 00 00 00 00 4C 89 AB 00 00 00 00";  // C7 43 ?? ?? ?? ?? ?? 4C 89 AB ?? ?? ?? ??
        internal const string PATTERN_FRAMELOCK_FUZZY_MASK = "xx?????xxx????";
        internal const int PATTERN_FRAMELOCK_FUZZY_OFFSET = 3; // offset to byte array from found position
        internal const string PATTERN_FRAMELOCK_RUNNING_FIX = "F3 0F 59 05 00 30 92 02 0F 2F F8"; // F3 0F 59 05 ?? 30 92 02 0F 2F F8 | 0F 51 C2 F3 0F 59 05 ?? ?? ?? ?? 0F 2F F8
        internal const string PATTERN_FRAMELOCK_RUNNING_FIX_MASK = "xxxx?xxxxxx";
        internal const int PATTERN_FRAMELOCK_RUNNING_FIX_OFFSET = 4;
        internal const string PATTERN_RESOLUTION = "80 07 00 00 38 04"; // 1920x1080
        internal const string PATTERN_RESOLUTION_MASK = "xxxxxx";
        internal const string PATTERN_WIDESCREEN_219 = "00 47 47 8B 94 C7 1C 02 00 00"; // ?? 47 47 8B 94 C7 1C 02 00 00
        internal const string PATTERN_WIDESCREEN_219_MASK = "?xxxxxxxxx";
        internal byte[] PATCH_FRAMERATE_RUNNING_FIX_DISABLE = new byte[1] { 0x90 };
        internal byte[] PATCH_FRAMERATE_UNLIMITED = new byte[4] { 0x00, 0x00, 0x00, 0x00 };
        internal byte[] PATCH_WIDESCREEN_219_DISABLE = new byte[1] { 0x74 };
        internal byte[] PATCH_WIDESCREEN_219_ENABLE = new byte[1] { 0xEB };
        internal byte[] PATCH_FOV_DISABLE = new byte[1] { 0x0C };

        // credits to jackfuste for FOV findings
        internal const string PATTERN_FOVSETTING = "F3 0F 10 08 F3 0F 59 0D 00 E7 9B 02"; // F3 0F 10 08 F3 0F 59 0D ?? E7 9B 02
        internal const string PATTERN_FOVSETTING_MASK = "xxxxxxxx?xxx";
        internal const int PATTERN_FOVSETTING_OFFSET = 8;
        internal Dictionary<byte, string> _fovMatrix = new Dictionary<byte, string>
        {
            { 0x10, "+ 15%" },
            { 0x14, "+ 40%" },
            { 0x18, "+ 75%" },
            { 0x1C, "+ 90%" },
        };

        internal long _offset_framelock = 0x0;
        internal long _offset_framelock_running_fix = 0x0;
        internal long _offset_resolution = 0x0;
        internal long _offset_widescreen_219 = 0x0;
        internal long _offset_fovsetting = 0x0;
        internal bool _running = false;
        internal Process _game;
        internal IntPtr _gameHwnd = IntPtr.Zero;
        internal IntPtr _gameProc = IntPtr.Zero;
        internal static IntPtr _gameProcStatic;
        internal readonly DispatcherTimer _dispatcherTimerCheck = new DispatcherTimer();
        internal string _logPath;
        internal bool _retryAccess = true;
        internal RECT _windowRect;
        internal RECT _clientRect;

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// On window loaded.
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _logPath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location) + @"\SekiroFpsUnlockAndMore.log";

            this.cbSelectFov.ItemsSource = _fovMatrix;
            this.cbSelectFov.SelectedIndex = 0;

            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (!RegisterHotKey(hwnd, 9009, MOD_CONTROL, VK_P))
                MessageBox.Show("Hotkey is already in use, it may not work.", "Sekiro FPS Unlocker and more");

            // add a hook for WindowsMessageQueue to recognize hotkey-press
            ComponentDispatcher.ThreadFilterMessage += new ThreadMessageEventHandler(ComponentDispatcherThreadFilterMessage);

            _dispatcherTimerCheck.Tick += new EventHandler(CheckGame);
            _dispatcherTimerCheck.Interval = new TimeSpan(0, 0, 0, 3);
            _dispatcherTimerCheck.Start();
        }

        /// <summary>
        /// On window closing.
        /// </summary>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ComponentDispatcher.ThreadFilterMessage -= ComponentDispatcherThreadFilterMessage;
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(hwnd, 9009);
            if (_gameProc != IntPtr.Zero)
                CloseHandle(_gameProc);
        }

        /// <summary>
        /// Windows Message queue (Wndproc) to catch HotKeyPressed
        /// </summary>
        private void ComponentDispatcherThreadFilterMessage(ref MSG msg, ref bool handled)
        {
            if (!handled)
            {
                if (msg.message == WM_HOTKEY_MSG_ID)    // hotkeyevent
                {
                    if (msg.wParam.ToInt32() == 9009)   // patch game
                    {
                        handled = true;
                        PatchGame();
                    }
                }
            }
        }

        /// <summary>
        /// Checks if game is running and initializes further functionality.
        /// </summary>
        private void CheckGame(object sender, EventArgs e)
        {
            Process[] procList = Process.GetProcessesByName(PROCESS_NAME);
            if (procList.Length < 1)
                return;

            if (_running || _offset_framelock != 0x0)
                return;

            int gameIndex = -1;
            for (int i = 0; i < procList.Length; i++)
            {
                if (procList[i].MainWindowTitle == PROCESS_TITLE && procList[i].MainModule.FileVersionInfo.FileDescription.Contains(PROCESS_DESCRIPTION))
                {
                    gameIndex = i;
                    break;
                }
            }
            if (gameIndex < 0)
            {
                UpdateStatus("no valid game process found...", Brushes.Red);
                LogToFile("no valid game process found...");
                for (int j = 0; j < procList.Length; j++)
                {
                    LogToFile(string.Format("\tProcess #{0}: '{1}' | ({2})", j, procList[j].MainModule.FileName, procList[j].MainModule.FileVersionInfo.FileName));
                    LogToFile(string.Format("\tDescription #{0}: {1} | {2} | {3}", j, procList[j].MainWindowTitle, procList[j].MainModule.FileVersionInfo.CompanyName, procList[j].MainModule.FileVersionInfo.FileDescription));
                    LogToFile(string.Format("\tData #{0}: {1} | {2} | {3} | {4} | {5}", j, procList[j].MainModule.FileVersionInfo.FileVersion, procList[j].MainModule.ModuleMemorySize, procList[j].StartTime, procList[j].Responding, procList[j].HasExited));
                }
                return;
            }

            _game = procList[gameIndex];
            _gameHwnd = procList[gameIndex].MainWindowHandle;
            _gameProc = OpenProcess(PROCESS_ALL_ACCESS, false, (uint)procList[gameIndex].Id);
            _gameProcStatic = _gameProc;
            if (_gameHwnd == IntPtr.Zero || _gameProc == IntPtr.Zero || procList[gameIndex].MainModule.BaseAddress == IntPtr.Zero)
            {
                LogToFile("no access to game...");
                LogToFile("Hwnd: " + _gameHwnd.ToString("X"));
                LogToFile("Proc: " + _gameProc.ToString("X"));
                LogToFile("Base: " + procList[gameIndex].MainModule.BaseAddress.ToString("X"));
                if (!_retryAccess)
                {
                    UpdateStatus("no access to game...", Brushes.Red);
                    _dispatcherTimerCheck.Stop();
                    return;
                }
                _gameHwnd = IntPtr.Zero;
                if (_gameProc != IntPtr.Zero)
                {
                    CloseHandle(_gameProc);
                    _gameProc = IntPtr.Zero;
                    _gameProcStatic = IntPtr.Zero;
                }
                LogToFile("retrying...");
                _retryAccess = false;
                return;
            }

            //string gameFileVersion = FileVersionInfo.GetVersionInfo(procList[0].MainModule.FileName).FileVersion;

            _offset_framelock = PatternScan.FindPattern(_gameProc, procList[gameIndex].MainModule, PATTERN_FRAMELOCK, PATTERN_FRAMELOCK_MASK, ' ');
            Debug.WriteLine("1. Framelock found at: 0x" + _offset_framelock.ToString("X"));
            if (!IsValid(_offset_framelock))
            {
                _offset_framelock = PatternScan.FindPattern(_gameProc, procList[gameIndex].MainModule, PATTERN_FRAMELOCK_FUZZY, PATTERN_FRAMELOCK_FUZZY_MASK, ' ') + PATTERN_FRAMELOCK_FUZZY_OFFSET;
                Debug.WriteLine("2. Framelock found at: 0x" + _offset_framelock.ToString("X"));
            }
            if (!IsValid(_offset_framelock))
            {
                UpdateStatus("framelock not found...", Brushes.Red);
                LogToFile("framelock not found...");
                this.cbUnlockFps.IsEnabled = false;
                this.cbUnlockFps.IsChecked = false;
            }
            _offset_framelock_running_fix = PatternScan.FindPattern(_gameProc, procList[gameIndex].MainModule, PATTERN_FRAMELOCK_RUNNING_FIX, PATTERN_FRAMELOCK_RUNNING_FIX_MASK, ' ') + PATTERN_FRAMELOCK_RUNNING_FIX_OFFSET;
            Debug.WriteLine("Running fix found at: 0x" + _offset_framelock_running_fix.ToString("X"));
            if (!IsValid(_offset_framelock_running_fix))
            {
                UpdateStatus("running fix not found...", Brushes.Red);
                LogToFile("running fix not found...");
                this.cbAddWidescreen.IsEnabled = false;
                this.cbAddWidescreen.IsChecked = false;
            }

            _offset_resolution = PatternScan.FindPattern(_gameProc, procList[gameIndex].MainModule, PATTERN_RESOLUTION, PATTERN_RESOLUTION_MASK, ' ');
            Debug.WriteLine("Resolution found at: 0x" + _offset_resolution.ToString("X"));
            if (!IsValid(_offset_resolution))
            {
                UpdateStatus("resolution not found...", Brushes.Red);
                LogToFile("resolution not found...");
                this.cbAddWidescreen.IsEnabled = false;
                this.cbAddWidescreen.IsChecked = false;
            }
            _offset_widescreen_219 = PatternScan.FindPattern(_gameProc, procList[gameIndex].MainModule, PATTERN_WIDESCREEN_219, PATTERN_WIDESCREEN_219_MASK, ' ');
            Debug.WriteLine("Widescreen 21/9 found at: 0x" + _offset_widescreen_219.ToString("X"));
            if (!IsValid(_offset_widescreen_219))
            {
                UpdateStatus("widescreen 21/9 not found...", Brushes.Red);
                LogToFile("Widescreen 21/9 not found...");
                this.cbAddWidescreen.IsEnabled = false;
                this.cbAddWidescreen.IsChecked = false;
            }

            _offset_fovsetting = PatternScan.FindPattern(_gameProc, procList[gameIndex].MainModule, PATTERN_FOVSETTING, PATTERN_FOVSETTING_MASK, ' ') + PATTERN_FOVSETTING_OFFSET;
            Debug.WriteLine("FOV found at: 0x" + _offset_fovsetting.ToString("X"));
            if (!IsValid(_offset_fovsetting))
            {
                UpdateStatus("FOV not found...", Brushes.Red);
                LogToFile("FOV not found...");
                this.cbFov.IsEnabled = false;
                this.cbFov.IsChecked = false;
            }

            GetWindowRect(_gameHwnd, out _windowRect);
            GetClientRect(_gameHwnd, out _clientRect);
            _running = true;
            _dispatcherTimerCheck.Stop();
            PatchGame();
        }

        /// <summary>
        /// Patch up this broken port
        /// </summary>
        private void PatchGame()
        {
            if (!_running)
                return;

            if (_game.HasExited)
            {
                _running = false;
                _gameHwnd = IntPtr.Zero;
                _gameProc = IntPtr.Zero;
                _gameProcStatic = IntPtr.Zero;
                _offset_framelock = 0x0;
                _offset_framelock_running_fix = 0x0;
                _offset_resolution = 0x0;
                _offset_widescreen_219 = 0x0;
                _offset_fovsetting = 0x0;
                UpdateStatus("waiting for game...", Brushes.White);
                _dispatcherTimerCheck.Start();
                return;
            }

            if (this.cbUnlockFps.IsChecked == true)
            {
                int fps = -1;
                bool isNumber = Int32.TryParse(this.tbFps.Text, out fps);
                if (fps < 0 || !isNumber)
                {
                    this.tbFps.Text = "60";
                    fps = 60;
                }
                else if (fps > 0 && fps < 30)
                {
                    this.tbFps.Text = "30";
                    fps = 30;
                }
                else if (fps > 300)
                {
                    this.tbFps.Text = "300";
                    fps = 300;
                }

                if (fps == 0)
                {
                    WriteBytes(_gameProcStatic, _offset_framelock, PATCH_FRAMERATE_UNLIMITED);
                    WriteBytes(_gameProcStatic, _offset_framelock_running_fix, new byte[1] { 0xF8 }); // F8 is maximum
                }
                else
                {
                    int speed = 144 + (int)Math.Ceiling((fps - 60) / 16f) * 8; // calculation from game functions
                    if (speed > 248)
                        speed = 248;
                    float deltaTime = (1000f / fps) / 1000f;
                    Debug.WriteLine("Deltatime hex: 0x" + getHexRepresentationFromFloat(deltaTime));
                    Debug.WriteLine("Speed hex: 0x" + speed.ToString("X"));
                    WriteBytes(_gameProcStatic, _offset_framelock, BitConverter.GetBytes(deltaTime));
                    WriteBytes(_gameProcStatic, _offset_framelock_running_fix, new byte[] { (byte)Convert.ToInt16(speed) });
                }
            }
            else if (this.cbUnlockFps.IsChecked == false)
            {
                float deltaTime = (1000f / 60) / 1000f;
                WriteBytes(_gameProcStatic, _offset_framelock, BitConverter.GetBytes(deltaTime));
                WriteBytes(_gameProcStatic, _offset_framelock_running_fix, PATCH_FRAMERATE_RUNNING_FIX_DISABLE);
            }

            if (this.cbAddWidescreen.IsChecked == true)
            {
                int width = -1;
                bool isNumber = Int32.TryParse(this.tbWidth.Text, out width);
                if (width < 800 || !isNumber)
                {
                    this.tbWidth.Text = "2560";
                    width = 2560;
                }
                else if (width > 5760)
                {
                    this.tbWidth.Text = "5760";
                    width = 5760;
                }
                int height = -1;
                isNumber = Int32.TryParse(this.tbHeight.Text, out height);
                if (height < 450 || !isNumber)
                {
                    this.tbHeight.Text = "1080";
                    height = 1080;
                }
                else if (height > 2160)
                {
                    this.tbHeight.Text = "2160";
                    height = 2160;
                }
                WriteBytes(_gameProcStatic, _offset_resolution, BitConverter.GetBytes(width));
                WriteBytes(_gameProcStatic, _offset_resolution + 4, BitConverter.GetBytes(height));
                WriteBytes(_gameProcStatic, _offset_widescreen_219, (float) width / (float) height > 1.9f ? PATCH_WIDESCREEN_219_ENABLE : PATCH_WIDESCREEN_219_DISABLE);
            }
            else if (this.cbAddWidescreen.IsChecked == false)
            {
                WriteBytes(_gameProcStatic, _offset_resolution, BitConverter.GetBytes(1920));
                WriteBytes(_gameProcStatic, _offset_resolution + 4, BitConverter.GetBytes(1080));
                WriteBytes(_gameProcStatic, _offset_widescreen_219, PATCH_WIDESCREEN_219_DISABLE);
            }

            if (this.cbFov.IsChecked == true)
            {
                byte[] fovByte = new byte[1];
                fovByte[0] = ((KeyValuePair<byte, string>) this.cbSelectFov.SelectedItem).Key;
                WriteBytes(_gameProcStatic, _offset_fovsetting, fovByte);
            }
            else if (this.cbFov.IsChecked == false)
            {
                WriteBytes(_gameProcStatic, _offset_fovsetting, PATCH_FOV_DISABLE);
            }

            if (this.cbBorderless.IsChecked == true)
            {
                if (!IsFullscreen(_gameHwnd))
                    SetWindowBorderless(_gameHwnd);
                else
                {
                    MessageBox.Show("Please exit fullscreen first before activating borderless window mode.", "Sekiro FPS Unlocker and more");
                    this.cbBorderless.IsChecked = false;
                }
            }
            else if (this.cbBorderless.IsChecked == false && !IsFullscreen(_gameHwnd))
            {
                SetWindowWindowed(_gameHwnd);
            }

            if (this.cbUnlockFps.IsChecked == true || this.cbAddWidescreen.IsChecked == true || this.cbFov.IsChecked == true)
                UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game patched!", Brushes.Green);
            else
                UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game unpatched!", Brushes.White);
        }

        /// <summary>
        /// Returns the hexadecimal representation of an IEEE-754 floating point number
        /// </summary>
        /// <param name="input">The floating point number</param>
        /// <returns>The hexadecimal representation of the input</returns>
        private string getHexRepresentationFromFloat(float input)
        {
            uint f = BitConverter.ToUInt32(BitConverter.GetBytes(input), 0);
            return "0x" + f.ToString("X8");
        }

        /// <summary>
        /// Checks if window is in fullscreen mode.
        /// </summary>
        /// <param name="hwnd">The main window handle of the window.</param>
        /// <remarks>
        /// Fullscreen windows have WS_EX_TOPMOST flag set.
        /// </remarks>
        /// <returns>True if window is run in fullscreen mode.</returns>
        private bool IsFullscreen(IntPtr hwnd)
        {
            long wndStyle = GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
            long wndExStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();

            if (wndStyle == 0 || wndExStyle == 0)
                return false;

            if ((wndExStyle & WS_EX_TOPMOST) == 0)
                return false;
            if ((wndStyle & WS_POPUP) != 0)
                return false;
            if ((wndStyle & WS_CAPTION) != 0)
                return false;
            if ((wndStyle & WS_BORDER) != 0)
                return false;

            return true;
        }

        /// <summary>
        /// Sets a window to ordinary windowed mode
        /// </summary>
        /// <param name="hwnd">The handle to the window.</param>
        private void SetWindowWindowed(IntPtr hwnd)
        {
            var width = _windowRect.Right - _windowRect.Left;
            var height = _windowRect.Bottom - _windowRect.Top;
            Debug.WriteLine(string.Format("Window Resolution: {0}x{1}", width, height));
            SetWindowLongPtr(hwnd, GWL_STYLE, WS_VISIBLE | WS_CAPTION | WS_BORDER | WS_CLIPSIBLINGS | WS_DLGFRAME | WS_SYSMENU | WS_GROUP | WS_MINIMIZEBOX);
            SetWindowPos(hwnd, HWND_NOTOPMOST, 40, 40, width, height, SWP_FRAMECHANGED | SWP_SHOWWINDOW);
        }

        /// <summary>
        /// Sets a window to borderless windowed mode and moves it to position 0x0.
        /// </summary>
        /// <param name="hwnd">The handle to the window.</param>
        private void SetWindowBorderless(IntPtr hwnd)
        {
            var width = _clientRect.Right - _clientRect.Left;
            var height = _clientRect.Bottom - _clientRect.Top;
            Debug.WriteLine(string.Format("Client Resolution: {0}x{1}", width, height));
            SetWindowLongPtr(hwnd, GWL_STYLE, WS_VISIBLE | WS_POPUP);
            SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, width, height, SWP_FRAMECHANGED | SWP_SHOWWINDOW);
        }

        /// <summary>
        /// Checks if a pointer is valid.
        /// </summary>
        /// <param name="address">The address the pointer points to.</param>
        /// <returns>True if pointer points to a valid address.</returns>
        private static bool IsValid(Int64 address)
        {
            return (address >= 0x10000 && address < 0x000F000000000000);
        }

        /// <summary>
        /// Writes a given type and value to processes memory using a generic method.
        /// </summary>
        /// <param name="gameProc">The process handle to read from.</param>
        /// <param name="lpBaseAddress">The address to write from.</param>
        /// <param name="bytes">The byte array to write.</param>
        /// <returns>True if successful, false otherwise.</returns>
        private static bool WriteBytes(IntPtr gameProc, Int64 lpBaseAddress, byte[] bytes)
        {
            IntPtr lpNumberOfBytesWritten;
            return WriteProcessMemory(gameProc, lpBaseAddress, bytes, (ulong)bytes.Length, out lpNumberOfBytesWritten);
        }

        /// <summary>
        /// Check whether input is numeric only.
        /// </summary>
        /// <param name="text">The text to check.</param>
        /// <returns>True if inout is numeric only.</returns>
        private bool IsNumericInput(string text)
        {
            return Regex.IsMatch(text, "[^0-9]+");
        }

        private void UpdateStatus(string text, Brush color)
        {
            this.tbStatus.Background = color;
            this.tbStatus.Text = text;
        }

        private void Numeric_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = IsNumericInput(e.Text);
        }

        private void Numeric_PastingHandler(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(String)))
            {
                String text = (String)e.DataObject.GetData(typeof(String));
                if (IsNumericInput(text)) e.CancelCommand();
            }
            else e.CancelCommand();
        }

        private void CheckBoxChanged_Handler(object sender, RoutedEventArgs e)
        {
            PatchGame();
        }

        private void BPatch_Click(object sender, RoutedEventArgs e)
        {
            PatchGame();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        // log messages to file
        private void LogToFile(string msg)
        {
            string timedMsg = "[" + DateTime.Now + "] " + msg;
            Debug.WriteLine(timedMsg);
            try
            {
                using (StreamWriter writer = new StreamWriter(_logPath, true))
                {
                    writer.WriteLine(timedMsg);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Writing to log file failed: " + ex.Message, "Sekiro Fps Unlock And More");
            }
        }

        #region WINAPI
        private const int WM_HOTKEY_MSG_ID = 0x0312;
        private const int MOD_CONTROL = 0x0002;
        private const uint VK_P = 0x0050;
        private const uint PROCESS_ALL_ACCESS = 0x001F0FFF;
        private const int GWL_EXSTYLE = -20;
        private const int GWL_STYLE = -16;
        private const uint WS_CLIPSIBLINGS = 0x04000000;
        private const uint WS_DLGFRAME = 0x00400000;
        private const uint WS_SYSMENU = 0x00080000;
        private const uint WS_GROUP = 0x00020000;
        private const uint WS_MINIMIZEBOX = 0x00020000;
        private const uint WS_POPUP = 0x80000000;
        private const uint WS_VISIBLE = 0x10000000;
        private const uint WS_CAPTION = 0x00C00000;
        private const uint WS_BORDER = 0x00800000;
        private const uint WS_EX_TOPMOST = 0x00000008;
        private const uint WS_EX_WINDOWEDGE = 0x00000100;
        private const int HWND_NOTOPMOST = -2;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_SHOWWINDOW = 0x0040;

        [DllImport("user32.dll")]
        public static extern Boolean RegisterHotKey(IntPtr hWnd, Int32 id, UInt32 fsModifiers, UInt32 vlc);

        [DllImport("user32.dll")]
        public static extern Boolean UnregisterHotKey(IntPtr hWnd, Int32 id);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(
            UInt32 dwDesiredAccess,
            Boolean bInheritHandle,
            UInt32 dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern Boolean CloseHandle(IntPtr hObject);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, Int32 nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, Int32 nIndex, Int64 dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
        public static extern IntPtr SetWindowPos(IntPtr hWnd, Int32 hWndInsertAfter, Int32 X, Int32 Y, Int32 cx, Int32 cy, UInt32 uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;        // x position of upper-left corner
            public int Top;         // y position of upper-left corner
            public int Right;       // x position of lower-right corner
            public int Bottom;      // y position of lower-right corner
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool WriteProcessMemory(
            IntPtr hProcess,
            Int64 lpBaseAddress,
            [In, Out] Byte[] lpBuffer,
            UInt64 dwSize,
            out IntPtr lpNumberOfBytesWritten);

        #endregion
    }
}
