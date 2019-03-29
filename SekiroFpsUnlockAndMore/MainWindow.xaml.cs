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
using System.Timers;

namespace SekiroFpsUnlockAndMore
{
    public partial class MainWindow : Window
    {
        internal const string PROCESS_NAME = "sekiro";
        internal const string PROCESS_TITLE = "Sekiro";
        internal const string PROCESS_DESCRIPTION = "Shadows Die Twice";
        internal byte[] PATCH_FRAMERATE_RUNNING_FIX_DISABLE = new byte[1] { 0x90 };
        internal byte[] PATCH_FRAMERATE_UNLIMITED = new byte[4] { 0x00, 0x00, 0x00, 0x00 };
        internal byte[] PATCH_WIDESCREEN_219_DISABLE = new byte[1] { 0x74 };
        internal byte[] PATCH_WIDESCREEN_219_ENABLE = new byte[1] { 0xEB };
        internal byte[] PATCH_FOV_DISABLE = new byte[2] { 0x0C, 0xE7};
        internal Dictionary<byte[], string> PATCH_FOVMATRIX = new Dictionary<byte[], string>
        {
            { new byte[2] {0x00, 0xE7}, "- 50%" },
            { new byte[2] {0x04, 0xE7}, "- 10%" },
            { new byte[2] {0x10, 0xE7}, "+ 15%" },
            { new byte[2] {0x14, 0xE7}, "+ 40%" },
            { new byte[2] {0x18, 0xE7}, "+ 75%" },
            { new byte[2] {0x1C, 0xE7}, "+ 90%" },
        };

        internal Process _game;
        internal IntPtr _gameHwnd = IntPtr.Zero;
        internal IntPtr _gameProc = IntPtr.Zero;
        internal static IntPtr _gameProcStatic;
        internal long _offset_framelock = 0x0;
        internal long _offset_framelock_running_fix = 0x0;
        internal long _offset_resolution = 0x0;
        internal long _offset_resolution_default = 0x0;
        internal long _offset_widescreen_219 = 0x0;
        internal long _offset_fovsetting = 0x0;
		//game stat offsets
		internal long _offset_player_deaths = 0x0;
		internal long _pointer_player_deaths = 0x0;
		internal long _offset_total_kills = 0x0;
		internal long _pointer_total_kills = 0x0;

		internal const string deathCounterFilename = "DeathCouner.txt";
		internal const string totalKillsFilename = "TotalKillsCounter.txt";

		internal readonly Timer _statRecordTimer = new Timer();
		internal readonly DispatcherTimer _dispatcherTimerCheck = new DispatcherTimer();
        internal bool _running = false;
        internal string _logPath;
        internal bool _retryAccess = true;
        internal RECT _windowRect;

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

            this.cbSelectFov.ItemsSource = PATCH_FOVMATRIX;
            this.cbSelectFov.SelectedIndex = 2;

            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (!RegisterHotKey(hwnd, 9009, MOD_CONTROL, VK_P))
                MessageBox.Show("Hotkey is already in use, it may not work.", "Sekiro FPS Unlocker and more");

            // add a hook for WindowsMessageQueue to recognize hotkey-press
            ComponentDispatcher.ThreadFilterMessage += new ThreadMessageEventHandler(ComponentDispatcherThreadFilterMessage);

            _dispatcherTimerCheck.Tick += new EventHandler(CheckGame);
            _dispatcherTimerCheck.Interval = new TimeSpan(0, 0, 0, 2);
            _dispatcherTimerCheck.Start();

			_statRecordTimer.Elapsed += new ElapsedEventHandler(StatReadTimer);
			_statRecordTimer.Interval = 1500;
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

			_statRecordTimer.Stop();
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

            _offset_framelock = PatternScan.FindPattern(_gameProc, procList[gameIndex].MainModule, Offsets.PATTERN_FRAMELOCK, Offsets.PATTERN_FRAMELOCK_MASK, ' ');
            Debug.WriteLine("1. Framelock found at: 0x" + _offset_framelock.ToString("X"));
            if (!IsValid(_offset_framelock))
            {
                _offset_framelock = PatternScan.FindPattern(_gameProc, procList[gameIndex].MainModule, Offsets.PATTERN_FRAMELOCK_FUZZY, Offsets.PATTERN_FRAMELOCK_FUZZY_MASK, ' ') + Offsets.PATTERN_FRAMELOCK_FUZZY_OFFSET;
                Debug.WriteLine("2. Framelock found at: 0x" + _offset_framelock.ToString("X"));
            }
            if (!IsValid(_offset_framelock))
            {
                UpdateStatus("framelock not found...", Brushes.Red);
                LogToFile("framelock not found...");
                this.cbUnlockFps.IsEnabled = false;
                this.cbUnlockFps.IsChecked = false;
            }
            _offset_framelock_running_fix = PatternScan.FindPattern(_gameProc, procList[gameIndex].MainModule, Offsets.PATTERN_FRAMELOCK_RUNNING_FIX, Offsets.PATTERN_FRAMELOCK_RUNNING_FIX_MASK, ' ') + Offsets.PATTERN_FRAMELOCK_RUNNING_FIX_OFFSET;
            Debug.WriteLine("Running fix found at: 0x" + _offset_framelock_running_fix.ToString("X"));
            if (!IsValid(_offset_framelock_running_fix))
            {
                UpdateStatus("running fix not found...", Brushes.Red);
                LogToFile("running fix not found...");
                this.cbAddWidescreen.IsEnabled = false;
                this.cbAddWidescreen.IsChecked = false;
            }

            _offset_resolution_default = PatternScan.FindPattern(_gameProc, procList[gameIndex].MainModule, Offsets.PATTERN_RESOLUTION_DEFAULT, Offsets.PATTERN_RESOLUTION_DEFAULT_MASK, ' ');
            Debug.WriteLine("Default resolution found at: 0x" + _offset_resolution_default.ToString("X"));
            if (!IsValid(_offset_resolution_default))
            {
                UpdateStatus("default resolution not found...", Brushes.Red);
                LogToFile("default resolution not found...");
                this.cbAddWidescreen.IsEnabled = false;
                this.cbAddWidescreen.IsChecked = false;
            }
            _offset_widescreen_219 = PatternScan.FindPattern(_gameProc, procList[gameIndex].MainModule, Offsets.PATTERN_WIDESCREEN_219, Offsets.PATTERN_WIDESCREEN_219_MASK, ' ');
            Debug.WriteLine("Widescreen 21/9 found at: 0x" + _offset_widescreen_219.ToString("X"));
            if (!IsValid(_offset_widescreen_219))
            {
                UpdateStatus("widescreen 21/9 not found...", Brushes.Red);
                LogToFile("Widescreen 21/9 not found...");
                this.cbAddWidescreen.IsEnabled = false;
                this.cbAddWidescreen.IsChecked = false;
            }

            long offset_resolution_pointer = PatternScan.FindPattern(_gameProc, procList[gameIndex].MainModule, Offsets.PATTERN_RESOLUTION_POINTER, Offsets.PATTERN_RESOLUTION_POINTER_MASK, ' ') + Offsets.PATTERN_RESOLUTION_POINTER_OFFSET;
            Debug.WriteLine("Resolution pointer found at: 0x" + offset_resolution_pointer.ToString("X"));
            if (!IsValid(offset_resolution_pointer))
            {
                UpdateStatus("Resolution pointer not found...", Brushes.Red);
                LogToFile("Resolution pointer not found...");
                this.cbBorderless.IsEnabled = false;
                this.cbBorderless.IsChecked = false;
            }
            else
            {
                _offset_resolution = FindOffsetToStaticPointer(_gameProc, offset_resolution_pointer, Offsets.PATTERN_RESOLUTION_POINTER_INSTRUCTION_LENGTH);
                Debug.WriteLine("Resolution found at: 0x" + _offset_resolution.ToString("X"));
                if (!IsValid(_offset_resolution))
                {
                    UpdateStatus("Resolution not valid...", Brushes.Red);
                    LogToFile("Resolution not valid...");
                    this.cbBorderless.IsEnabled = false;
                    this.cbBorderless.IsChecked = false;
                }
            }

            _offset_fovsetting = PatternScan.FindPattern(_gameProc, procList[gameIndex].MainModule, Offsets.PATTERN_FOVSETTING, Offsets.PATTERN_FOVSETTING_MASK, ' ') + Offsets.PATTERN_FOVSETTING_OFFSET;
            Debug.WriteLine("FOV found at: 0x" + _offset_fovsetting.ToString("X"));
            if (!IsValid(_offset_fovsetting))
            {
                UpdateStatus("FOV not found...", Brushes.Red);
                LogToFile("FOV not found...");
                this.cbFov.IsEnabled = false;
                this.cbFov.IsChecked = false;
            }

			//Game stats
			_offset_player_deaths = PatternScan.FindPattern(_gameProc, procList[gameIndex].MainModule, Offsets.PATTERN_PLAYER_DEATHS, Offsets.PATTERN_PLAYER_DEATHS_MASK, ' ');
			Debug.WriteLine("Player Deaths found at: 0x" + _offset_player_deaths.ToString("X"));
			if (!IsValid(_offset_player_deaths))
			{
				LogToFile("Player death counter not found...");
			}
			else
			{
				_pointer_player_deaths = Read<Int64>(_gameProc, FindOffsetToStaticPointer(_gameProc, _offset_player_deaths, 0)) + 0x90;
			}

			_offset_total_kills = PatternScan.FindPattern(_gameProc, procList[gameIndex].MainModule, Offsets.PATTERN_TOTAL_KILLS, Offsets.PATTERN_TOTAL_KILLS_MASK, ' ') + Offsets.PATTERN_TOTAL_KILLS_OFFSET;
			Debug.WriteLine("Total kills found at: 0x" + _offset_total_kills.ToString("X"));
			if (!IsValid(_offset_total_kills))
			{
				LogToFile("Total kills counter not found...");
			}
			else
			{
				_pointer_total_kills = FindOffsetToStaticPointer(_gameProc, _offset_total_kills, 0);
			}

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
                if (_gameProc != IntPtr.Zero)
                    CloseHandle(_gameProc);
                 _game = null;
                _gameHwnd = IntPtr.Zero;
                _gameProc = IntPtr.Zero;
                _gameProcStatic = IntPtr.Zero;
                _offset_framelock = 0x0;
                _offset_framelock_running_fix = 0x0;
                _offset_resolution = 0x0;
                _offset_resolution_default = 0x0;
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
                bool isNumber = Int32.TryParse(this.tbWidth.Text, out int width);
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
                isNumber = Int32.TryParse(this.tbHeight.Text, out int height);
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
                WriteBytes(_gameProcStatic, _offset_resolution_default, BitConverter.GetBytes(width));
                WriteBytes(_gameProcStatic, _offset_resolution_default + 4, BitConverter.GetBytes(height));
                WriteBytes(_gameProcStatic, _offset_widescreen_219, PATCH_WIDESCREEN_219_ENABLE);
            }
            else if (this.cbAddWidescreen.IsChecked == false)
            {
                WriteBytes(_gameProcStatic, _offset_resolution_default, BitConverter.GetBytes(1920));
                WriteBytes(_gameProcStatic, _offset_resolution_default + 4, BitConverter.GetBytes(1080));
                WriteBytes(_gameProcStatic, _offset_widescreen_219, PATCH_WIDESCREEN_219_DISABLE);
            }

            if (this.cbFov.IsChecked == true)
            {
                byte[] fovByte = ((KeyValuePair<byte[], string>) this.cbSelectFov.SelectedItem).Key;
                WriteBytes(_gameProcStatic, _offset_fovsetting, fovByte);
            }
            else if (this.cbFov.IsChecked == false)
            {
                WriteBytes(_gameProcStatic, _offset_fovsetting, PATCH_FOV_DISABLE);
            }

            if (this.cbBorderless.IsChecked == true)
            {
                if (IsFullscreen(_gameHwnd))
                {
                    MessageBox.Show("Please exit fullscreen first before activating borderless window mode.", "Sekiro FPS Unlocker and more");
                    this.cbBorderless.IsChecked = false;
                }
                else
                {
                    if (!IsBorderless(_gameHwnd))
                        GetWindowRect(_gameHwnd, out _windowRect);
                    int width = Read<Int32>(_gameProc, _offset_resolution);
                    int height = Read<Int32>(_gameProc, _offset_resolution + 4);
                    Debug.WriteLine(string.Format("Client Resolution: {0}x{1}", width, height));
                    if (this.cbBorderlessStretch.IsChecked == true)
                        SetWindowBorderless(_gameHwnd, (int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight, 0, 0);
                    else
                        SetWindowBorderless(_gameHwnd, width, height, _windowRect.Left, _windowRect.Top);
                }
            }
            else if (this.cbBorderless.IsChecked == false && IsBorderless(_gameHwnd))
            {
                if (_windowRect.Bottom > 0)
                {
                    int width = _windowRect.Right - _windowRect.Left;
                    int height = _windowRect.Bottom - _windowRect.Top;
                    Debug.WriteLine(string.Format("Window Resolution: {0}x{1}", width, height));
                    SetWindowWindowed(_gameHwnd, width, height, _windowRect.Left, _windowRect.Top);
                }
            }

            if (this.cbUnlockFps.IsChecked == true || this.cbAddWidescreen.IsChecked == true || this.cbFov.IsChecked == true)
                UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game patched!", Brushes.Green);
            else
                UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game unpatched!", Brushes.White);
        }

		/// <summary>
		/// Reads some hidden stats and outputs them to text files. Use to display counters on Twitch stream or just look at them and get disspointed
		/// </summary
		private void StatReadTimer(object sender, EventArgs e)
		{
			if (IsValid(_pointer_player_deaths))
			{
				int playerDeaths = Read<Int32>(_gameProc, _pointer_player_deaths);
				//Debug.WriteLine("[STAT]Player deaths: " + playerDeaths);
				LogStatFile(deathCounterFilename, playerDeaths.ToString());

				if (IsValid(_pointer_total_kills))
				{
					int totalKills = Read<Int32>(_gameProc, _pointer_total_kills);
					totalKills -= playerDeaths; //Since this value seems to track every death, including the player
					//Debug.WriteLine("[STAT]Enemies killed: " + totalKills);
					LogStatFile(totalKillsFilename, totalKills.ToString());
				}
			}
		}

		/// <summary>
		/// Returns the hexadecimal representation of an IEEE-754 floating point number
		/// </summary>
		/// <param name="input">The floating point number.</param>
		/// <returns>The hexadecimal representation of the input.</returns>
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
        /// Checks if window is in borderless window mode.
        /// </summary>
        /// <param name="hwnd">The main window handle of the window.</param>
        /// <remarks>
        /// Borderless windows have WS_POPUP flag set.
        /// </remarks>
        /// <returns>True if window is run in borderless window mode.</returns>
        private bool IsBorderless(IntPtr hwnd)
        {
            long wndStyle = GetWindowLongPtr(hwnd, GWL_STYLE).ToInt64();
            if (wndStyle == 0)
                return false;

            if ((wndStyle & WS_POPUP) == 0)
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
        /// <param name="width">The desired window width.</param>
        /// <param name="height">The desired window height.</param>
        /// <param name="posX">The desired X position of the window.</param>
        /// <param name="posY">The desired Y position of the window.</param>
        private void SetWindowWindowed(IntPtr hwnd, int width, int height, int posX, int posY)
        {
            SetWindowLongPtr(hwnd, GWL_STYLE, WS_VISIBLE | WS_CAPTION | WS_BORDER | WS_CLIPSIBLINGS | WS_DLGFRAME | WS_SYSMENU | WS_GROUP | WS_MINIMIZEBOX);
            SetWindowPos(hwnd, HWND_NOTOPMOST, posX, posY, width, height, SWP_FRAMECHANGED | SWP_SHOWWINDOW);
        }

        /// <summary>
        /// Sets a window to borderless windowed mode and moves it to position 0x0.
        /// </summary>
        /// <param name="hwnd">The handle to the window.</param>
        /// <param name="width">The desired window width.</param>
        /// <param name="height">The desired window height.</param>
        /// <param name="posX">The desired X position of the window.</param>
        /// <param name="posY">The desired Y position of the window.</param>
        private void SetWindowBorderless(IntPtr hwnd, int width, int height, int posX, int posY)
        {
            SetWindowLongPtr(hwnd, GWL_STYLE, WS_VISIBLE | WS_POPUP);
            SetWindowPos(hwnd, HWND_TOP, posX, posY, width, height, SWP_FRAMECHANGED | SWP_SHOWWINDOW);
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
        /// Reads a given type from processes memory using a generic method.
        /// </summary>
        /// <typeparam name="T">The base type to read.</typeparam>
        /// <param name="gameProc">The process handle to read from.</param>
        /// <param name="lpBaseAddress">The address to read from.</param>
        /// <returns>The given base type read from memory.</returns>
        /// <remarks>GCHandle and Marshal are costy.</remarks>
        private static T Read<T>(IntPtr gameProc, Int64 lpBaseAddress)
        {
            byte[] lpBuffer = new byte[Marshal.SizeOf(typeof(T))];
            ReadProcessMemory(gameProc, lpBaseAddress, lpBuffer, (ulong)lpBuffer.Length, out _);
            GCHandle gcHandle = GCHandle.Alloc(lpBuffer, GCHandleType.Pinned);
            T structure = (T)Marshal.PtrToStructure(gcHandle.AddrOfPinnedObject(), typeof(T));
            gcHandle.Free();
            return structure;
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
            return WriteProcessMemory(gameProc, lpBaseAddress, bytes, (ulong)bytes.Length, out _);
        }

        /// <summary>
        /// Gets the static offset to a desired object instead of an offset to a pointer.
        /// </summary>
        /// <param name="hProcess">Handle to the process in whose memory the pattern has been found.</param>
        /// <param name="lpPatternAddress">The address where the pattern has been found.</param>
        /// <param name="instructionLength">The length of the instruction including the 4 bytes pointer.</param>
        /// <remarks>Static pointers in x86-64 are relative offsets from the instruction address.</remarks>
        /// <returns>The static offset from the process to desired object).</returns>
        internal static Int64 FindOffsetToStaticPointer(IntPtr hProcess, Int64 lpPatternAddress, int instructionLength)
        {
            return lpPatternAddress + Read<Int32>(hProcess, lpPatternAddress + (instructionLength -0x04)) + instructionLength;
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

        /// <summary>
        /// Logs messages to log file
        /// </summary>
        /// <param name="msg">The message to write to file.</param>
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

		/// <summary>
		/// Logs stat values to separate files for the use in OBS
		/// </summary>
		/// <param name="filename">File name</param>
		/// <param name="msg">Just a single stat value</param>
		private void LogStatFile(string filename, string value)
		{
			try
			{
				using (StreamWriter writer = new StreamWriter(filename, false))
				{
					writer.Write(value);
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show("Failed writing stat file: " + ex.Message, "Sekiro Fps Unlock And More");
			}
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
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                if (IsNumericInput(text)) e.CancelCommand();
            }
            else e.CancelCommand();
        }

        private void CheckBoxChanged_Handler(object sender, RoutedEventArgs e)
        {
            PatchGame();
        }

        private void CbBorderless_Checked(object sender, RoutedEventArgs e)
        {
            this.cbBorderlessStretch.IsEnabled = true;
            PatchGame();
        }

        private void CbBorderless_Unchecked(object sender, RoutedEventArgs e)
        {
            this.cbBorderlessStretch.IsEnabled = false;
            this.cbBorderlessStretch.IsChecked = false;
            PatchGame();
        }

		private void CbStatChanged(object sender, RoutedEventArgs e)
		{
			_statRecordTimer.Enabled = (bool)cbLogStats.IsChecked;
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
        private const int HWND_TOP = 0;
        private const int HWND_TOPMOST = -1;
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

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;        // x position of upper-left corner
            public int Top;         // y position of upper-left corner
            public int Right;       // x position of lower-right corner
            public int Bottom;      // y position of lower-right corner
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern Boolean ReadProcessMemory(
            IntPtr hProcess,
            Int64 lpBaseAddress,
            [Out] Byte[] lpBuffer,
            UInt64 dwSize,
            out IntPtr lpNumberOfBytesRead);

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
