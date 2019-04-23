using System;
using System.IO;
using System.Timers;
using System.Windows;
using System.Threading;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Input;
using System.ComponentModel;
using System.Windows.Interop;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace SekiroFpsUnlockAndMore
{
    public partial class MainWindow : Window
    {
        internal Process _gameProc;
        internal IntPtr _gameHwnd = IntPtr.Zero;
        internal IntPtr _gameAccessHwnd = IntPtr.Zero;
        internal static IntPtr _gameAccessHwndStatic;
        internal long _offset_framelock = 0x0;
        internal long _offset_resolution = 0x0;
        internal long _offset_resolution_default = 0x0;
        internal long _offset_resolution_scaling_fix = 0x0;
        internal long _offset_total_kills = 0x0;
        internal long _offset_player_deaths = 0x0;
        internal long _offset_camera_reset = 0x0;
        internal long _offset_dragonrot_routine = 0x0;
        internal long _offset_deathpenalties1 = 0x0;
        internal long _offset_deathpenalties2 = 0x0;
        internal long _offset_deathscounter_routine = 0x0;
        internal long _offset_timescale = 0x0;
        internal long _offset_timescale_player = 0x0;
        internal long _offset_timescale_player_pointer_start = 0x0;

        internal byte[] _patch_deathpenalties1_enable;
        internal byte[] _patch_deathpenalties2_enable;

        internal MemoryCaveGenerator _memoryCaveGenerator;
        internal SettingsService _settingsService;
        internal StatusViewModel _statusViewModel = new StatusViewModel();

        internal readonly DispatcherTimer _dispatcherTimerGameCheck = new DispatcherTimer();
        internal readonly DispatcherTimer _dispatcherTimerFreezeMem = new DispatcherTimer();
        internal readonly BackgroundWorker _bgwScanGame = new BackgroundWorker();
        internal readonly System.Timers.Timer _timerStatsCheck = new System.Timers.Timer();
        internal bool _running = false;
        internal bool _gameInitializing = false;
        internal bool _use_resolution_720 = false;
        internal bool _dataCave_speedfix = false;
        internal bool _dataCave_fovsetting = false;
        internal bool _codeCave_camadjust = false;
        internal bool _retryAccess = true;
        internal bool _statLoggingEnabled = false;
        internal bool _initialStartup = true;
        internal bool _debugMode = false;
        internal static string _path_logs;
        internal string _path_deathsLog;
        internal string _path_killsLog;
        internal RECT _windowRect;
        internal Size _screenSize;

        internal const string _DATACAVE_SPEEDFIX_POINTER = "speedfixPointer";
        internal const string _DATACAVE_FOV_POINTER = "fovPointer";
        internal const string _CODECAVE_CAMADJUST_PITCH = "camAdjustPitch";
        internal const string _CODECAVE_CAMADJUST_YAW_Z = "camAdjustYawZ";
        internal const string _CODECAVE_CAMADJUST_PITCH_XY = "camAdjustPitchXY";
        internal const string _CODECAVE_CAMADJUST_YAW_XY = "camAdjustYawXY";

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _statusViewModel;
        }

        /// <summary>
        /// On window loaded.
        /// </summary>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            var mutex = new Mutex(true, "sekiroFpsUnlockAndMore", out bool isNewInstance);
            if (!isNewInstance)
            {
                MessageBox.Show("Another instance is already running!", "Sekiro FPS Unlocker and more", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                Environment.Exit(0);
            }
            GC.KeepAlive(mutex);

            _path_logs = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location) + @"\SekiroFpsUnlockAndMore.log";
            _path_deathsLog = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location) + @"\DeathCounter.txt";
            _path_killsLog = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location) + @"\TotalKillsCounter.txt";

            LoadConfiguration();

            if (_settingsService.ApplicationSettings.cameraAdjustNotify)
                this.sbInput.Text = _settingsService.ApplicationSettings.peasantInput ? "Controller" : "Mouse";

            IntPtr hWnd = new WindowInteropHelper(this).Handle;
            if (!RegisterHotKey(hWnd, 9009, MOD_CONTROL, VK_P))
                MessageBox.Show("Hotkey is already in use, it may not work.", "Sekiro FPS Unlocker and more", MessageBoxButton.OK, MessageBoxImage.Warning);

            ComponentDispatcher.ThreadFilterMessage += new ThreadMessageEventHandler(ComponentDispatcherThreadFilterMessage);

            _screenSize = GetDpiSafeResolution();
            if ((int)_screenSize.Width < 1920) _use_resolution_720 = true;

            _bgwScanGame.DoWork += new DoWorkEventHandler(ReadGame);
            _bgwScanGame.RunWorkerCompleted += new RunWorkerCompletedEventHandler(OnReadGameFinish);

            _dispatcherTimerGameCheck.Tick += new EventHandler(async (object s, EventArgs a) =>
            {
                bool result = await CheckGame();
                if (result)
                {
                    UpdateStatus("scanning game...", Brushes.Orange);
                    _bgwScanGame.RunWorkerAsync();
                    _dispatcherTimerGameCheck.Stop();
                }
            });
            _dispatcherTimerGameCheck.Interval = new TimeSpan(0, 0, 0, 0, 2000);
            _dispatcherTimerGameCheck.Start();

            _dispatcherTimerFreezeMem.Tick += new EventHandler(FreezeMemory);
            _dispatcherTimerFreezeMem.Interval = new TimeSpan(0, 0, 0, 0, 2000);

            _timerStatsCheck.Elapsed += new ElapsedEventHandler(StatsReadTimer);
            _timerStatsCheck.Interval = 2000;
        }

        /// <summary>
        /// On window closing.
        /// </summary>
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            _timerStatsCheck.Stop();
            SaveConfiguration();
            ComponentDispatcher.ThreadFilterMessage -= ComponentDispatcherThreadFilterMessage;
            IntPtr hWnd = new WindowInteropHelper(this).Handle;
            UnregisterHotKey(hWnd, 9009);
            if (_gameAccessHwnd != IntPtr.Zero)
                CloseHandle(_gameAccessHwnd);
        }

        /// <summary>
        /// Windows Message queue (Wndproc) to catch HotKeyPressed
        /// </summary>
        private void ComponentDispatcherThreadFilterMessage(ref MSG msg, ref bool handled)
        {
            if (handled) return;
            if (msg.message != WM_HOTKEY_MSG_ID) return;

            if (msg.wParam.ToInt32() == 9009)   // patch game
            {
                handled = true;
                PatchGame();
            }
        }

        /// <summary>
        /// Load all saved settings from previous run.
        /// </summary>
        private void LoadConfiguration()
        {
            _settingsService = new SettingsService(Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location) + @"\SekiroFpsUnlockAndMore.xml");
            if (!_settingsService.Load()) return;
            this.cbFramelock.IsChecked = _settingsService.ApplicationSettings.cbFramelock;
            this.tbFramelock.Text = _settingsService.ApplicationSettings.tbFramelock.ToString();
            this.cbAddResolution.IsChecked = _settingsService.ApplicationSettings.cbAddResolution;
            this.tbWidth.Text = _settingsService.ApplicationSettings.tbWidth.ToString();
            this.tbHeight.Text = _settingsService.ApplicationSettings.tbHeight.ToString();
            this.cbFov.IsChecked = _settingsService.ApplicationSettings.cbFov;
            this.tbFov.Text = _settingsService.ApplicationSettings.tbFov.ToString();
            this.cbBorderless.IsChecked = _settingsService.ApplicationSettings.cbBorderless;
            this.cbBorderlessStretch.IsChecked = _settingsService.ApplicationSettings.cbBorderlessStretch;
            this.cbLogStats.IsChecked = _settingsService.ApplicationSettings.cbLogStats;
            this.exGameMods.IsExpanded = _settingsService.ApplicationSettings.exGameMods;
            this.cbCamAdjust.IsChecked = _settingsService.ApplicationSettings.cbCamAdjust;
            this.cbCamReset.IsChecked = _settingsService.ApplicationSettings.cbCamReset;
            this.cbDragonrot.IsChecked = _settingsService.ApplicationSettings.cbDragonrot;
            this.cbDeathPenalty.IsChecked = _settingsService.ApplicationSettings.cbDeathPenalty;
            this.cbDeathPenaltyHidden.Visibility = _settingsService.ApplicationSettings.hiddenDPs == ZUH_HIDDEN_DP ? Visibility.Visible : Visibility.Collapsed;
            if (_settingsService.ApplicationSettings.hiddenDPs == ZUH_HIDDEN_DP) { _debugMode = true; sbMode.Text = "DEBUG"; }
            this.cbGameSpeed.IsChecked = _settingsService.ApplicationSettings.cbGameSpeed;
            this.tbGameSpeed.Text = _settingsService.ApplicationSettings.tbGameSpeed.ToString();
            this.cbPlayerSpeed.IsChecked = _settingsService.ApplicationSettings.cbPlayerSpeed;
            this.tbPlayerSpeed.Text = _settingsService.ApplicationSettings.tbPlayerSpeed.ToString();
            this.exGuide.IsExpanded = _settingsService.ApplicationSettings.exGuide;
        }

        /// <summary>
        /// Save all settings to configuration file.
        /// </summary>
        private void SaveConfiguration()
        {
            _settingsService.ApplicationSettings.cbFramelock = this.cbFramelock.IsChecked == true;
            _settingsService.ApplicationSettings.tbFramelock = this.tbFramelock.Text != "" && !this.tbFramelock.Text.Contains(" ") ? Convert.ToInt32(this.tbFramelock.Text) : 144;
            _settingsService.ApplicationSettings.cbAddResolution = this.cbAddResolution.IsChecked == true;
            _settingsService.ApplicationSettings.tbWidth = this.tbWidth.Text != "" && !this.tbWidth.Text.Contains(" ") ? Convert.ToInt32(this.tbWidth.Text) : 2560;
            _settingsService.ApplicationSettings.tbHeight = this.tbHeight.Text != "" && !this.tbHeight.Text.Contains(" ") ? Convert.ToInt32(this.tbHeight.Text) : 1080;
            _settingsService.ApplicationSettings.cbFov = this.cbFov.IsChecked == true;
            _settingsService.ApplicationSettings.tbFov = this.tbFov.Text != "" && !this.tbFov.Text.Contains(" ") ? Convert.ToInt32(this.tbFov.Text) : 25;
            _settingsService.ApplicationSettings.cbBorderless = this.cbBorderless.IsChecked == true;
            _settingsService.ApplicationSettings.cbBorderlessStretch = this.cbBorderlessStretch.IsChecked == true;
            _settingsService.ApplicationSettings.cbLogStats = this.cbLogStats.IsChecked == true;
            _settingsService.ApplicationSettings.exGameMods = this.exGameMods.IsExpanded;
            _settingsService.ApplicationSettings.cbCamAdjust = this.cbCamAdjust.IsChecked == true;
            _settingsService.ApplicationSettings.cbCamReset = this.cbCamReset.IsChecked == true;
            _settingsService.ApplicationSettings.cbDragonrot = this.cbDragonrot.IsChecked == true;
            _settingsService.ApplicationSettings.cbDeathPenalty = this.cbDeathPenalty.IsChecked == true;
            _settingsService.ApplicationSettings.cbGameSpeed = this.cbGameSpeed.IsChecked == true;
            _settingsService.ApplicationSettings.tbGameSpeed = this.tbGameSpeed.Text != "" && !this.tbGameSpeed.Text.Contains(" ") ? Convert.ToInt32(this.tbGameSpeed.Text) : 100;
            _settingsService.ApplicationSettings.cbPlayerSpeed = this.cbPlayerSpeed.IsChecked == true;
            _settingsService.ApplicationSettings.tbPlayerSpeed = this.tbPlayerSpeed.Text != "" && !this.tbPlayerSpeed.Text.Contains(" ") ? Convert.ToInt32(this.tbPlayerSpeed.Text) : 100;
            _settingsService.ApplicationSettings.exGuide = this.exGuide.IsExpanded;
            _settingsService.Save();
        }

        /// <summary>
        /// Resets GUI and clears configuration file.
        /// </summary>
        private void ClearConfiguration()
        {
            this.cbFramelock.IsChecked = false;
            this.tbFramelock.Text = "144";
            this.cbAddResolution.IsChecked = false;
            this.tbWidth.Text = "2560";
            this.tbHeight.Text = "1080";
            this.cbFov.IsChecked = false;
            this.tbFov.Text = "25";
            this.cbBorderless.IsChecked = false;
            this.cbBorderlessStretch.IsChecked = false;
            this.cbLogStats.IsChecked = false;
            this.exGameMods.IsExpanded = true;
            this.cbCamAdjust.IsChecked = false;
            this.cbCamReset.IsChecked = false;
            this.cbDragonrot.IsChecked = false;
            this.cbDeathPenalty.IsChecked = false;
            this.cbDeathPenaltyHidden.Visibility = Visibility.Collapsed;
            this.cbGameSpeed.IsChecked = false;
            this.tbGameSpeed.Text = "100";
            this.cbPlayerSpeed.IsChecked = false;
            this.tbPlayerSpeed.Text = "100";
            this.sbMode.Text = "";
            _settingsService.Clear();
        }

        /// <summary>
        /// Checks if game is running and initializes further functionality.
        /// </summary>
        private Task<bool> CheckGame()
        {
            // game process have been found last check and can be read now, aborting
            if (_gameInitializing)
                return Task.FromResult(true);
                
            Process[] procList = Process.GetProcessesByName(GameData.PROCESS_NAME);
            if (procList.Length < 1)
                return Task.FromResult(false);

            if (_running || _offset_framelock != 0x0)
                return Task.FromResult(false);

            int gameIndex = -1;
            for (int i = 0; i < procList.Length; i++)
            {
                if (procList[i].MainWindowTitle != GameData.PROCESS_TITLE || !procList[i].MainModule.FileVersionInfo.FileDescription.Contains(GameData.PROCESS_DESCRIPTION))
                    continue;
                gameIndex = i;
                break;
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
                return Task.FromResult(false);
            }

            _gameProc = procList[gameIndex];
            _gameHwnd = procList[gameIndex].MainWindowHandle;
            _gameAccessHwnd = OpenProcess(PROCESS_ALL_ACCESS, false, (uint)procList[gameIndex].Id);
            _gameAccessHwndStatic = _gameAccessHwnd;
            if (_gameHwnd == IntPtr.Zero || _gameAccessHwnd == IntPtr.Zero || _gameProc.MainModule.BaseAddress == IntPtr.Zero)
            {
                LogToFile("no access to game...");
                LogToFile("hWnd: " + _gameHwnd.ToString("X"));
                LogToFile("Access hWnd: " + _gameAccessHwnd.ToString("X"));
                LogToFile("BaseAddress: " + procList[gameIndex].MainModule.BaseAddress.ToString("X"));
                if (!_retryAccess)
                {
                    UpdateStatus("no access to game...", Brushes.Red);
                    _dispatcherTimerGameCheck.Stop();
                    return Task.FromResult(false);
                }
                _gameHwnd = IntPtr.Zero;
                if (_gameAccessHwnd != IntPtr.Zero)
                {
                    CloseHandle(_gameAccessHwnd);
                    _gameAccessHwnd = IntPtr.Zero;
                    _gameAccessHwndStatic = IntPtr.Zero;
                }
                LogToFile("retrying...");
                _retryAccess = false;
                return Task.FromResult(false);
            }

            string gameFileVersion = FileVersionInfo.GetVersionInfo(procList[0].MainModule.FileName).FileVersion;
            if (gameFileVersion != GameData.PROCESS_EXE_VERSION && Array.IndexOf(GameData.PROCESS_EXE_VERSION_SUPPORTED, gameFileVersion) < 0 && !_settingsService.ApplicationSettings.gameVersionNotify)
            {
                MessageBox.Show(string.Format("Unknown game version '{0}'.\nSome functions might not work properly or even crash the game. " +
                                "Check for updates on this utility regularly following the link at the bottom.", gameFileVersion), "Sekiro FPS Unlocker and more", MessageBoxButton.OK, MessageBoxImage.Warning);
                ClearConfiguration();
                _settingsService.ApplicationSettings.gameVersionNotify = true;
            }
            else
                _settingsService.ApplicationSettings.gameVersionNotify = false;

            // give the game some time to initialize
            _gameInitializing = true;
            UpdateStatus("game initializing...", Brushes.Orange);
            return Task.FromResult(false);
        }

        /// <summary>
        /// Read all game offsets and pointer (external).
        /// </summary>
        private void ReadGame(object sender, DoWorkEventArgs doWorkEventArgs)
        {
            PatternScan patternScan = new PatternScan(_gameAccessHwnd, _gameProc.MainModule);
            _memoryCaveGenerator = new MemoryCaveGenerator(_gameAccessHwnd, _gameProc.MainModule.BaseAddress.ToInt64());

            _offset_framelock = patternScan.FindPattern(GameData.PATTERN_FRAMELOCK) + GameData.PATTERN_FRAMELOCK_OFFSET;
            Debug.WriteLine("fFrameTick found at: 0x" + _offset_framelock.ToString("X"));
            if (!IsValidAddress(_offset_framelock))
            {
                _offset_framelock = patternScan.FindPattern(GameData.PATTERN_FRAMELOCK_FUZZY) + GameData.PATTERN_FRAMELOCK_FUZZY_OFFSET;
                Debug.WriteLine("2. fFrameTick found at: 0x" + _offset_framelock.ToString("X"));
            }
            if (!IsValidAddress(_offset_framelock))
                _offset_framelock = 0x0;

            long lpSpeedFixPointer = patternScan.FindPattern(GameData.PATTERN_FRAMELOCK_SPEED_FIX) + GameData.PATTERN_FRAMELOCK_SPEED_FIX_OFFSET;
            Debug.WriteLine("lpSpeedFixPointer at: 0x" + lpSpeedFixPointer.ToString("X"));
            if (IsValidAddress(lpSpeedFixPointer))
            {
                if (_memoryCaveGenerator.CreateNewDataCave(_DATACAVE_SPEEDFIX_POINTER, lpSpeedFixPointer, BitConverter.GetBytes(GameData.PATCH_FRAMELOCK_SPEED_FIX_DEFAULT_VALUE), PointerStyle.dwRelative))
                    _dataCave_speedfix = true;
                Debug.WriteLine("lpSpeedFixPointer data cave at: 0x" + _memoryCaveGenerator.GetDataCaveAddressByName(_DATACAVE_SPEEDFIX_POINTER).ToString("X"));
            }

            _offset_resolution_default = patternScan.FindPattern(_use_resolution_720 ? GameData.PATTERN_RESOLUTION_DEFAULT_720 : GameData.PATTERN_RESOLUTION_DEFAULT);
            Debug.WriteLine("default resolution found at: 0x" + _offset_resolution_default.ToString("X"));
            if (!IsValidAddress(_offset_resolution_default))
                _offset_resolution_default = 0x0;

            _offset_resolution_scaling_fix = patternScan.FindPattern(GameData.PATTERN_RESOLUTION_SCALING_FIX);
            Debug.WriteLine("scaling fix found at: 0x" + _offset_resolution_scaling_fix.ToString("X"));
            if (!IsValidAddress(_offset_resolution_scaling_fix))
                _offset_resolution_scaling_fix = 0x0;

            long ref_lpCurrentResolutionWidth = patternScan.FindPattern(GameData.PATTERN_RESOLUTION_POINTER) + GameData.PATTERN_RESOLUTION_POINTER_OFFSET;
            Debug.WriteLine("ref_lpCurrentResolutionWidth found at: 0x" + ref_lpCurrentResolutionWidth.ToString("X"));
            if (IsValidAddress(ref_lpCurrentResolutionWidth))
            {
                _offset_resolution = DereferenceStaticX64Pointer(_gameAccessHwnd, ref_lpCurrentResolutionWidth, GameData.PATTERN_RESOLUTION_POINTER_INSTRUCTION_LENGTH);
                Debug.WriteLine("lpCurrentResolutionWidth at: 0x" + _offset_resolution.ToString("X"));
                if (!IsValidAddress(_offset_resolution))
                    _offset_resolution = 0x0;
            }

            long lpFovPointer = patternScan.FindPattern(GameData.PATTERN_FOVSETTING) + GameData.PATTERN_FOVSETTING_OFFSET;
            Debug.WriteLine("lpFovPointer found at: 0x" + lpFovPointer.ToString("X"));
            if (IsValidAddress(lpFovPointer))
            {
                if (_memoryCaveGenerator.CreateNewDataCave(_DATACAVE_FOV_POINTER, lpFovPointer, BitConverter.GetBytes(GameData.PATCH_FOVSETTING_DISABLE), PointerStyle.dwRelative))
                    _dataCave_fovsetting = true;
                Debug.WriteLine("lpFovPointer data cave at: 0x" + _memoryCaveGenerator.GetDataCaveAddressByName(_DATACAVE_FOV_POINTER).ToString("X"));
            }

            long ref_lpPlayerStatsRelated = patternScan.FindPattern(GameData.PATTERN_PLAYER_DEATHS) + GameData.PATTERN_PLAYER_DEATHS_OFFSET;
            Debug.WriteLine("ref_lpPlayerStatsRelated found at: 0x" + ref_lpPlayerStatsRelated.ToString("X"));
            if (IsValidAddress(ref_lpPlayerStatsRelated))
            {
                long lpPlayerStatsRelated = DereferenceStaticX64Pointer(_gameAccessHwndStatic, ref_lpPlayerStatsRelated, GameData.PATTERN_PLAYER_DEATHS_INSTRUCTION_LENGTH);
                Debug.WriteLine("lpPlayerStatsRelated found at: 0x" + lpPlayerStatsRelated.ToString("X"));
                if (IsValidAddress(lpPlayerStatsRelated))
                {
                    int dwPlayerStatsToDeathsOffset = Read<Int32>(_gameAccessHwndStatic, ref_lpPlayerStatsRelated + GameData.PATTERN_PLAYER_DEATHS_POINTER_OFFSET_OFFSET);
                    Debug.WriteLine("offset pPlayerStats->iPlayerDeaths found : 0x" + dwPlayerStatsToDeathsOffset.ToString("X"));

                    if (dwPlayerStatsToDeathsOffset > 0)
                        _offset_player_deaths = Read<Int64>(_gameAccessHwndStatic, lpPlayerStatsRelated) + dwPlayerStatsToDeathsOffset;
                    Debug.WriteLine("iPlayerDeaths found at: 0x" + _offset_player_deaths.ToString("X"));
                }
            }
            if (!IsValidAddress(_offset_player_deaths))
                _offset_player_deaths = 0x0;

            long ref_lpTotalKills = patternScan.FindPattern(GameData.PATTERN_TOTAL_KILLS);
            Debug.WriteLine("ref_lpTotalKills found at: 0x" + ref_lpTotalKills.ToString("X"));
            if (IsValidAddress(ref_lpTotalKills))
            {
                _offset_total_kills = DereferenceStaticX64Pointer(_gameAccessHwndStatic, ref_lpTotalKills, GameData.PATTERN_TOTAL_KILLS_INSTRUCTION_LENGTH);
                if (!IsValidAddress(_offset_total_kills))
                    _offset_total_kills = 0x0;
            }

            long lpCamAdjustPitch = patternScan.FindPattern(GameData.PATTERN_CAMADJUST_PITCH);
            long lpCamAdjustYawZ = patternScan.FindPattern(GameData.PATTERN_CAMADJUST_YAW_Z) + GameData.PATTERN_CAMADJUST_YAW_Z_OFFSET;
            long lpCamAdjustPitchXY = patternScan.FindPattern(GameData.PATTERN_CAMADJUST_PITCH_XY);
            long lpCamAdjustYawXY = patternScan.FindPattern(GameData.PATTERN_CAMADJUST_YAW_XY) + GameData.PATTERN_CAMADJUST_YAW_XY_OFFSET;
            Debug.WriteLine("lpCamAdjustPitch found at: 0x" + lpCamAdjustPitch.ToString("X"));
            Debug.WriteLine("lpCamAdjustYawZ found at: 0x" + lpCamAdjustYawZ.ToString("X"));
            Debug.WriteLine("lpCamAdjustPitchXY found at: 0x" + lpCamAdjustPitchXY.ToString("X"));
            Debug.WriteLine("lpCamAdjustYawXY found at: 0x" + lpCamAdjustYawXY.ToString("X"));
            if (IsValidAddress(lpCamAdjustPitch) && IsValidAddress(lpCamAdjustYawZ) && IsValidAddress(lpCamAdjustPitchXY) && IsValidAddress(lpCamAdjustYawXY))
            {
                List<bool> results = new List<bool>
                {
                    _memoryCaveGenerator.CreateNewCodeCave(_CODECAVE_CAMADJUST_PITCH, lpCamAdjustPitch, GameData.INJECT_CAMADJUST_PITCH_OVERWRITE_LENGTH, GameData.INJECT_CAMADJUST_PITCH_SHELLCODE),
                    _memoryCaveGenerator.CreateNewCodeCave(_CODECAVE_CAMADJUST_YAW_Z, lpCamAdjustYawZ, GameData.INJECT_CAMADJUST_YAW_Z_OVERWRITE_LENGTH, GameData.INJECT_CAMADJUST_YAW_Z_SHELLCODE),
                    _memoryCaveGenerator.CreateNewCodeCave(_CODECAVE_CAMADJUST_PITCH_XY, lpCamAdjustPitchXY, GameData.INJECT_CAMADJUST_PITCH_XY_OVERWRITE_LENGTH, GameData.INJECT_CAMADJUST_PITCH_XY_SHELLCODE),
                    _memoryCaveGenerator.CreateNewCodeCave(_CODECAVE_CAMADJUST_YAW_XY, lpCamAdjustYawXY, GameData.INJECT_CAMADJUST_YAW_XY_OVERWRITE_LENGTH, GameData.INJECT_CAMADJUST_YAW_XY_SHELLCODE)
                };
                Debug.WriteLine("lpCamAdjustPitch code cave at: 0x" + _memoryCaveGenerator.GetCodeCaveAddressByName(_CODECAVE_CAMADJUST_PITCH).ToString("X"));
                Debug.WriteLine("lpCamAdjustYawZ code cave at: 0x" + _memoryCaveGenerator.GetCodeCaveAddressByName(_CODECAVE_CAMADJUST_YAW_Z).ToString("X"));
                Debug.WriteLine("lpCamAdjustPitchXY code cave at: 0x" + _memoryCaveGenerator.GetCodeCaveAddressByName(_CODECAVE_CAMADJUST_PITCH_XY).ToString("X"));
                Debug.WriteLine("lpCamAdjustYawXY code cave at: 0x" + _memoryCaveGenerator.GetCodeCaveAddressByName(_CODECAVE_CAMADJUST_YAW_XY).ToString("X"));
                if (results.IndexOf(false) < 0)
                    _codeCave_camadjust = true;
            }

            _offset_camera_reset = patternScan.FindPattern(GameData.PATTERN_CAMRESET_LOCKON) + GameData.PATTERN_CAMRESET_LOCKON_OFFSET;
            Debug.WriteLine("lpCameraReset found at: 0x" + _offset_camera_reset.ToString("X"));
            if (!IsValidAddress(_offset_camera_reset))
                _offset_camera_reset = 0x0;

            _offset_dragonrot_routine = patternScan.FindPattern(GameData.PATTERN_DRAGONROT_EFFECT) + GameData.PATTERN_DRAGONROT_EFFECT_OFFSET;
            Debug.WriteLine("lpDragonRot found at: 0x" + _offset_dragonrot_routine.ToString("X"));
            if (!IsValidAddress(_offset_dragonrot_routine))
                _offset_dragonrot_routine = 0x0;

            _offset_deathpenalties1 = patternScan.FindPattern(GameData.PATTERN_DEATHPENALTIES1) + GameData.PATTERN_DEATHPENALTIES1_OFFSET;
            Debug.WriteLine("lpDeathPenalties1 found at: 0x" + _offset_deathpenalties1.ToString("X"));
            if (IsValidAddress(_offset_deathpenalties1))
            {
                _patch_deathpenalties1_enable = new byte[GameData.PATCH_DEATHPENALTIES1_INSTRUCTION_LENGTH];
                if (!ReadProcessMemory(_gameAccessHwnd, _offset_deathpenalties1, _patch_deathpenalties1_enable, (ulong)GameData.PATCH_DEATHPENALTIES1_INSTRUCTION_LENGTH, out IntPtr lpNumberOfBytesRead) || lpNumberOfBytesRead.ToInt32() != GameData.PATCH_DEATHPENALTIES1_INSTRUCTION_LENGTH)
                    _patch_deathpenalties1_enable = null;
                else
                    Debug.WriteLine("deathPenalties1 original instruction set: " + BitConverter.ToString(_patch_deathpenalties1_enable).Replace('-', ' '));
                if (_patch_deathpenalties1_enable != null)
                {
                    _offset_deathpenalties2 = patternScan.FindPattern(GameData.PATTERN_DEATHPENALTIES2) + GameData.PATTERN_DEATHPENALTIES2_OFFSET;
                    Debug.WriteLine("lpDeathPenalties2 found at: 0x" + _offset_deathpenalties2.ToString("X"));
                    if (IsValidAddress(_offset_deathpenalties2))
                    {
                        _patch_deathpenalties2_enable = new byte[GameData.PATCH_DEATHPENALTIES2_INSTRUCTION_LENGTH];
                        if (!ReadProcessMemory(_gameAccessHwnd, _offset_deathpenalties2, _patch_deathpenalties2_enable, (ulong) GameData.PATCH_DEATHPENALTIES2_INSTRUCTION_LENGTH, out lpNumberOfBytesRead) || lpNumberOfBytesRead.ToInt32() != GameData.PATCH_DEATHPENALTIES2_INSTRUCTION_LENGTH)
                            _patch_deathpenalties2_enable = null;
                        else
                            Debug.WriteLine("deathPenalties2 original instruction set: " + BitConverter.ToString(_patch_deathpenalties2_enable).Replace('-', ' '));
                    }
                    else
                        _offset_deathpenalties2 = 0x0;
                }
            }
            if (_offset_deathpenalties2 == 0x0 || _patch_deathpenalties2_enable == null)
            {
                _offset_deathpenalties1 = 0x0;
                _offset_deathpenalties2 = 0x0;
                _patch_deathpenalties1_enable = null;
                _patch_deathpenalties2_enable = null;
            }

            if (_settingsService.ApplicationSettings.hiddenDPs == ZUH_HIDDEN_DP)
            {
                _offset_deathscounter_routine = patternScan.FindPattern(GameData.PATTERN_DEATHSCOUNTER) + GameData.PATTERN_DEATHSCOUNTER_OFFSET;
                Debug.WriteLine("lpDeathsCounter found at: 0x" + _offset_deathscounter_routine.ToString("X"));
                if (!IsValidAddress(_offset_deathscounter_routine))
                    _offset_deathscounter_routine = 0x0;
            }

            long ref_lpTimeRelated = patternScan.FindPattern(GameData.PATTERN_TIMESCALE);
            Debug.WriteLine("ref_lpTimeRelated found at: 0x" + ref_lpTimeRelated.ToString("X"));
            if (IsValidAddress(ref_lpTimeRelated))
            {
                long lpTimescaleManager = DereferenceStaticX64Pointer(_gameAccessHwndStatic, ref_lpTimeRelated, GameData.PATTERN_TIMESCALE_INSTRUCTION_LENGTH);
                Debug.WriteLine("lpTimescaleManager found at: 0x" + lpTimescaleManager.ToString("X"));
                if (IsValidAddress(lpTimescaleManager))
                {
                    _offset_timescale = Read<Int64>(_gameAccessHwndStatic, lpTimescaleManager) + Read<Int32>(_gameAccessHwndStatic, ref_lpTimeRelated + GameData.PATTERN_TIMESCALE_POINTER_OFFSET_OFFSET);
                    Debug.WriteLine("fTimescale found at: 0x" + _offset_timescale.ToString("X"));
                    if (!IsValidAddress(_offset_timescale))
                        _offset_timescale = 0x0;
                }
            }

            long lpPlayerStructRelated1 = patternScan.FindPattern(GameData.PATTERN_TIMESCALE_PLAYER);
            Debug.WriteLine("lpPlayerStructRelated1 found at: 0x" + lpPlayerStructRelated1.ToString("X"));

            if (IsValidAddress(lpPlayerStructRelated1))
            {
                long lpPlayerStructRelated2 = DereferenceStaticX64Pointer(_gameAccessHwndStatic, lpPlayerStructRelated1, GameData.PATTERN_TIMESCALE_PLAYER_INSTRUCTION_LENGTH);
                Debug.WriteLine("lpPlayerStructRelated2 found at: 0x" + lpPlayerStructRelated2.ToString("X"));
                if (IsValidAddress(lpPlayerStructRelated2))
                {
                    _offset_timescale_player_pointer_start = lpPlayerStructRelated2;
                    long lpPlayerStructRelated3 = Read<Int64>(_gameAccessHwndStatic, lpPlayerStructRelated2) + GameData.PATTERN_TIMESCALE_POINTER2_OFFSET;
                    Debug.WriteLine("lpPlayerStructRelated3 found at: 0x" + lpPlayerStructRelated3.ToString("X"));
                    if (IsValidAddress(lpPlayerStructRelated3))
                    {
                        long lpPlayerStructRelated4 = Read<Int64>(_gameAccessHwndStatic, lpPlayerStructRelated3) + GameData.PATTERN_TIMESCALE_POINTER3_OFFSET;
                        Debug.WriteLine("lpPlayerStructRelated4 found at: 0x" + lpPlayerStructRelated4.ToString("X"));
                        if (IsValidAddress(lpPlayerStructRelated4))
                        {
                            long lpPlayerStructRelated5 = Read<Int64>(_gameAccessHwndStatic, lpPlayerStructRelated4) + GameData.PATTERN_TIMESCALE_POINTER4_OFFSET;
                            Debug.WriteLine("lpPlayerStructRelated5 found at: 0x" + lpPlayerStructRelated5.ToString("X"));
                            if (IsValidAddress(lpPlayerStructRelated5))
                            {
                                _offset_timescale_player = Read<Int64>(_gameAccessHwndStatic, lpPlayerStructRelated5) + GameData.PATTERN_TIMESCALE_POINTER5_OFFSET;
                                Debug.WriteLine("fTimescalePlayer found at: 0x" + _offset_timescale_player.ToString("X"));
                                if (!IsValidAddress(_offset_timescale_player))
                                    _offset_timescale_player = 0x0;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// All game data has been read.
        /// </summary>
        private void OnReadGameFinish(object sender, RunWorkerCompletedEventArgs runWorkerCompletedEventArgs)
        {
            if (_offset_framelock == 0x0)
            {
                UpdateStatus("frame tick not found...", Brushes.Red);
                LogToFile("frame tick not found...");
                this.cbFramelock.IsEnabled = false;
            }

            if (!_dataCave_speedfix)
            {
                UpdateStatus("could not create speed fix table...", Brushes.Red);
                LogToFile("could not create speed fix table...");
                this.cbFramelock.IsEnabled = false;
            }

            if (_offset_resolution_default == 0x0)
            {
                UpdateStatus("default resolution not found...", Brushes.Red);
                LogToFile("default resolution not found...");
                this.cbAddResolution.IsEnabled = false;
            }
            if (_offset_resolution_scaling_fix == 0x0)
            {
                UpdateStatus("scaling fix not found...", Brushes.Red);
                LogToFile("scaling fix not found...");
                this.cbAddResolution.IsEnabled = false;
            }
            if (_offset_resolution == 0x0)
            {
                UpdateStatus("current resolution not found...", Brushes.Red);
                LogToFile("current resolution not found...");
                this.cbAddResolution.IsEnabled = false;
            }

            if (!_dataCave_fovsetting)
            {
                UpdateStatus("could not create FOV table...", Brushes.Red);
                LogToFile("could not create FOV table...");
                this.cbFov.IsEnabled = false;
            }

            this.cbBorderless.IsEnabled = true;

            if (_offset_player_deaths == 0x0)
            {
                UpdateStatus("player deaths not found...", Brushes.Red);
                LogToFile("player deaths not found...");
                this.cbLogStats.IsEnabled = false;
            }
            if (_offset_total_kills == 0x0)
            {
                UpdateStatus("player kills not found...", Brushes.Red);
                LogToFile("player kills not found...");
                this.cbLogStats.IsEnabled = false;
            }
            if (_offset_player_deaths > 0x0 && _offset_total_kills > 0x0)
                _timerStatsCheck.Start();

            if (!_codeCave_camadjust)
            {
                UpdateStatus("cam adjust not found...", Brushes.Red);
                LogToFile("cam adjust not found...");
            }
            this.cbCamAdjust.IsEnabled = _codeCave_camadjust;

            if (_offset_camera_reset == 0x0)
            {
                UpdateStatus("camera reset not found...", Brushes.Red);
                LogToFile("camera reset not found...");
                this.cbCamReset.IsEnabled = false;
            }

            if (_offset_dragonrot_routine == 0x0)
            {
                UpdateStatus("dragonrot not found...", Brushes.Red);
                LogToFile("dragonrot not found...");
                this.cbDragonrot.IsEnabled = false;
            }

            if (_offset_deathpenalties2 == 0x0)
            {
                UpdateStatus("death penalties not found...", Brushes.Red);
                LogToFile("death penalties not found...");
                this.cbDeathPenalty.IsEnabled = false;
            }

            if (_offset_deathscounter_routine == 0x0)
                this.cbDeathPenaltyHidden.IsEnabled = false;

            if (_offset_timescale == 0x0)
            {
                UpdateStatus("timescale not found...", Brushes.Red);
                LogToFile("timescale not found...");
                this.cbGameSpeed.IsEnabled = false;
            }
            if (_offset_timescale_player_pointer_start == 0x0)
            {
                UpdateStatus("player timescale not found...", Brushes.Red);
                //LogToFile("player timescale not found...");
                this.cbPlayerSpeed.IsEnabled = false;
            }

            this.bPatch.IsEnabled = true;
            _running = true;
            PatchGame();
            InjectToGame();
        }

        /// <summary>
        /// Read and refresh the player speed offset that can change on quick travel or save game loading.
        /// </summary>
        private void ReadPlayerTimescaleOffsets()
        {
            bool valid = false;
            if (_offset_timescale_player_pointer_start > 0)
            {
                long lpPlayerStructRelated3 = Read<Int64>(_gameAccessHwndStatic, _offset_timescale_player_pointer_start) + GameData.PATTERN_TIMESCALE_POINTER2_OFFSET;
                if (IsValidAddress(lpPlayerStructRelated3))
                {
                    long lpPlayerStructRelated4 = Read<Int64>(_gameAccessHwndStatic, lpPlayerStructRelated3) + GameData.PATTERN_TIMESCALE_POINTER3_OFFSET;
                    if (IsValidAddress(lpPlayerStructRelated4))
                    {
                        long lpPlayerStructRelated5 = Read<Int64>(_gameAccessHwndStatic, lpPlayerStructRelated4) + GameData.PATTERN_TIMESCALE_POINTER4_OFFSET;
                        if (IsValidAddress(lpPlayerStructRelated5))
                        {
                            _offset_timescale_player = Read<Int64>(_gameAccessHwndStatic, lpPlayerStructRelated5) + GameData.PATTERN_TIMESCALE_POINTER5_OFFSET;
                            if (IsValidAddress(_offset_timescale_player))
                                valid = true;
                        }
                    }
                }
            }
            if (!valid) _offset_timescale_player = 0x0;
        }

        /// <summary>
        /// Determines whether everything is ready for patching.
        /// </summary>
        /// <returns>True if we can patch game, false otherwise.</returns>
        private bool CanPatchGame()
        {
            if (!_running) return false;
            if (!_gameProc.HasExited) return true;

            _running = false;
            if (_gameAccessHwnd != IntPtr.Zero)
                CloseHandle(_gameAccessHwnd);
            _dispatcherTimerFreezeMem.Stop();
            _timerStatsCheck.Stop();
            _gameProc = null;
            _gameHwnd = IntPtr.Zero;
            _gameAccessHwnd = IntPtr.Zero;
            _gameAccessHwndStatic = IntPtr.Zero;
            _gameInitializing = false;
            _initialStartup = true;
            _offset_framelock = 0x0;
            _dataCave_speedfix = false;
            _offset_resolution = 0x0;
            _offset_resolution_default = 0x0;
            _offset_resolution_scaling_fix = 0x0;
            _dataCave_fovsetting = false;
            _offset_player_deaths = 0x0;
            _offset_total_kills = 0x0;
            _codeCave_camadjust = false;
            _offset_camera_reset = 0x0;
            _offset_dragonrot_routine = 0x0;
            _offset_deathpenalties1 = 0x0;
            _offset_deathpenalties2 = 0x0;
            _offset_deathscounter_routine = 0x0;
            _offset_timescale = 0x0;
            _offset_timescale_player = 0x0;
            _offset_timescale_player_pointer_start = 0x0;
            _patch_deathpenalties1_enable = null;
            _patch_deathpenalties2_enable = null;
            _memoryCaveGenerator.ClearCaves();
            _memoryCaveGenerator = null;
            this.cbFramelock.IsEnabled = true;
            this.cbAddResolution.IsEnabled = true;
            this.cbFov.IsEnabled = true;
            this.cbBorderless.IsEnabled = false;
            this.cbCamAdjust.IsEnabled = true;
            this.bPatch.IsEnabled = false;
            this.cbGameSpeed.IsEnabled = true;
            this.cbPlayerSpeed.IsEnabled = true;
            UpdateStatus("waiting for game...", Brushes.White);
            _dispatcherTimerGameCheck.Start();

            return false;
        }

        /// <summary>
        /// Patch the game's frame rate lock.
        /// </summary>
        /// <param name="showStatus">Determines if status should be updated from within method, default is true.</param>
        private bool PatchFramelock(bool showStatus = true)
        {
            if (!this.cbFramelock.IsEnabled || _offset_framelock == 0x0 || !_dataCave_speedfix || !CanPatchGame()) return false;
            if (this.cbFramelock.IsChecked == true)
            {
                int fps = -1;
                bool isNumber = Int32.TryParse(this.tbFramelock.Text, out fps);
                if (fps < 1 || !isNumber)
                {
                    this.tbFramelock.Text = "60";
                    fps = 60;
                }
                else if (fps > 1 && fps < 30)
                {
                    this.tbFramelock.Text = "30";
                    fps = 30;
                }
                else if (fps > 300)
                {
                    this.tbFramelock.Text = "300";
                    fps = 300;
                }

                float deltaTime = (1000f / fps) / 1000f;
                float speedFix = GameData.FindSpeedFixForRefreshRate(fps);
                Debug.WriteLine("Deltatime hex: " + GetHexRepresentationFromFloat(deltaTime));
                Debug.WriteLine("Speed hex: " + GetHexRepresentationFromFloat(speedFix));
                WriteBytes(_gameAccessHwndStatic, _offset_framelock, BitConverter.GetBytes(deltaTime));
                _memoryCaveGenerator.UpdateDataCaveValueByName(_DATACAVE_SPEEDFIX_POINTER, BitConverter.GetBytes(speedFix));
                _memoryCaveGenerator.ActivateDataCaveByName(_DATACAVE_SPEEDFIX_POINTER);
            }
            else if (this.cbFramelock.IsChecked == false)
            {
                if (!_initialStartup)
                {
                    float deltaTime = (1000f / 60) / 1000f;
                    WriteBytes(_gameAccessHwndStatic, _offset_framelock, BitConverter.GetBytes(deltaTime));
                    _memoryCaveGenerator.DeactivateDataCaveByName(_DATACAVE_SPEEDFIX_POINTER);
                }
                if (showStatus) UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game unpatched!", Brushes.White);
                return false;
            }

            if (showStatus) UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game patched!", Brushes.Green);
            return true;
        }

        /// <summary>
        /// Patches the game's default resolution.
        /// </summary>
        /// <param name="showStatus">Determines if status should be updated from within method, default is true.</param>
        private bool PatchResolution(bool showStatus = true)
        {
            if (!this.cbAddResolution.IsEnabled || _offset_resolution == 0x0 || _offset_resolution_default == 0x0 || _offset_resolution_scaling_fix == 0x0 || !CanPatchGame()) return false;
            if (this.cbAddResolution.IsChecked == true)
            {
                this.cbBorderless.IsChecked = false;
                bool isNumber = Int32.TryParse(this.tbWidth.Text, out int width);
                if (width < 800 || !isNumber)
                {
                    this.tbWidth.Text = "800";
                    width = 800;
                }
                else if (width > 7680)
                {
                    this.tbWidth.Text = "7680";
                    width = 7680;
                }
                isNumber = Int32.TryParse(this.tbHeight.Text, out int height);
                if (height < 450 || !isNumber)
                {
                    this.tbHeight.Text = "450";
                    height = 450;
                }
                else if (height > 2160)
                {
                    this.tbHeight.Text = "2160";
                    height = 2160;
                }
                WriteBytes(_gameAccessHwndStatic, _offset_resolution_default, BitConverter.GetBytes(width));
                WriteBytes(_gameAccessHwndStatic, _offset_resolution_default + 4, BitConverter.GetBytes(height));
                WriteBytes(_gameAccessHwndStatic, _offset_resolution_scaling_fix, GameData.PATCH_RESOLUTION_SCALING_FIX_ENABLE);
            }
            else if (this.cbAddResolution.IsChecked == false)
            {
                if (!_initialStartup)
                {
                    this.cbBorderless.IsChecked = false;
                    WriteBytes(_gameAccessHwndStatic, _offset_resolution_default, !_use_resolution_720 ? GameData.PATCH_RESOLUTION_DEFAULT_DISABLE : GameData.PATCH_RESOLUTION_DEFAULT_DISABLE_720);
                    WriteBytes(_gameAccessHwndStatic, _offset_resolution_scaling_fix, GameData.PATCH_RESOLUTION_SCALING_FIX_DISABLE);
                }
                if (showStatus) UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game unpatched!", Brushes.White);
                return false;
            }

            if (showStatus) UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game patched!", Brushes.Green);
            return true;
        }

        /// <summary>
        /// Patches the game's field of view.
        /// </summary>
        /// <param name="showStatus">Determines if status should be updated from within method, default is true.</param>
        private bool PatchFov(bool showStatus = true)
        {
            if (!this.cbFov.IsEnabled || !_dataCave_fovsetting|| !CanPatchGame()) return false;
            if (this.cbFov.IsChecked == true)
            {
                bool isNumber = Int32.TryParse(this.tbFov.Text, out int fovIncrease);
                if (fovIncrease < -95 || !isNumber)
                {
                    this.tbFov.Text = "-95";
                    fovIncrease = -95;
                }
                else if (fovIncrease > 95)
                {
                    this.tbFov.Text = "95";
                    fovIncrease = 95;
                }

                float fovValue = (float)(Math.PI / 180) * ((fovIncrease / 100.0f) + 1); // convert change in %degree to radians
                _memoryCaveGenerator.UpdateDataCaveValueByName(_DATACAVE_FOV_POINTER, BitConverter.GetBytes(fovValue));
                _memoryCaveGenerator.ActivateDataCaveByName(_DATACAVE_FOV_POINTER);
            }
            else if (this.cbFov.IsChecked == false)
            {
                if (!_initialStartup)
                    _memoryCaveGenerator.DeactivateDataCaveByName(_DATACAVE_FOV_POINTER);
                if (showStatus) UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game unpatched!", Brushes.White);
                return false;
            }

            if (showStatus) UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game patched!", Brushes.Green);
            return true;
        }

        /// <summary>
        /// Patches the game's window.
        /// </summary>
        /// <param name="showStatus">Determines if status should be updated from within method, default is true.</param>
        private bool PatchWindow(bool showStatus = true)
        {
            if (!this.cbBorderless.IsEnabled || !CanPatchGame()) return false;
            if (this.cbBorderless.IsChecked == true)
            {
                if (IsFullscreen(_gameHwnd) || IsMinimized(_gameHwnd))
                {
                    MessageBox.Show("Please un-minimize window and exit fullscreen first before activating borderless window mode.", "Sekiro FPS Unlocker and more");
                    this.cbBorderless.IsChecked = false;
                    return false;
                }
                if (!IsBorderless(_gameHwnd))
                    GetWindowRect(_gameHwnd, out _windowRect);
                int width = Read<Int32>(_gameAccessHwnd, _offset_resolution);
                int height = Read<Int32>(_gameAccessHwnd, _offset_resolution + 4);
                Debug.WriteLine(string.Format("Client Resolution: {0}x{1}", width, height));
                if (this.cbBorderlessStretch.IsChecked == true)
                    SetWindowBorderless(_gameHwnd, (int)_screenSize.Width, (int)_screenSize.Height, 0, 0);
                else
                    SetWindowBorderless(_gameHwnd, width, height, _windowRect.Left, _windowRect.Top);
            }
            else if (this.cbBorderless.IsChecked == false && IsBorderless(_gameHwnd))
            {
                if (_windowRect.Bottom > 0)
                {
                    int width = _windowRect.Right - _windowRect.Left;
                    int height = _windowRect.Bottom - _windowRect.Top;
                    Debug.WriteLine(string.Format("Window Resolution: {0}x{1}", width, height));
                    SetWindowWindowed(_gameHwnd, width, height, _windowRect.Left, _windowRect.Top);
                    if (showStatus) UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game unpatched!", Brushes.White);
                }
                return false;
            }
            else
            {
                return false;
            }

            if (showStatus) UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game patched!", Brushes.Green);
            return true;
        }

        /// <summary>
        /// Patches the game's camera centering on lock-on.
        /// </summary>
        /// <param name="showStatus">Determines if status should be updated from within method, default is true.</param>
        private bool PatchCamReset(bool showStatus = true)
        {
            if (!this.cbCamReset.IsEnabled || _offset_camera_reset == 0x0 || !CanPatchGame()) return false;
            if (this.cbCamReset.IsChecked == true)
            {
                WriteBytes(_gameAccessHwndStatic, _offset_camera_reset, GameData.PATCH_CAMRESET_LOCKON_DISABLE);
            }
            else if (this.cbCamReset.IsChecked == false)
            {
                if (!_initialStartup)
                    WriteBytes(_gameAccessHwndStatic, _offset_camera_reset, GameData.PATCH_CAMRESET_LOCKON_ENABLE);
                if (showStatus) UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game unpatched!", Brushes.White);
                return false;
            }

            if (showStatus) UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game patched!", Brushes.Green);
            return true;
        }

        /// <summary>
        /// Patches the game's dragonrot effect on NPCs.
        /// </summary>
        /// <param name="showStatus">Determines if status should be updated from within method, default is true.</param>
        private bool PatchDragonrot(bool showStatus = true)
        {
            if (!this.cbDragonrot.IsEnabled || _offset_dragonrot_routine == 0x0 || !CanPatchGame()) return false;
            if (this.cbDragonrot.IsChecked == true)
            {
                WriteBytes(_gameAccessHwndStatic, _offset_dragonrot_routine, GameData.PATCH_DRAGONROT_EFFECT_DISABLE);
            }
            else if (this.cbDragonrot.IsChecked == false)
            {
                if (!_initialStartup)
                    WriteBytes(_gameAccessHwndStatic, _offset_dragonrot_routine, GameData.PATCH_DRAGONROT_EFFECT_ENABLE);
                if (showStatus) UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game unpatched!", Brushes.White);
                return false;
            }

            if (showStatus) UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game patched!", Brushes.Green);
            return true;
        }

        /// <summary>
        /// Patches the game's death penalties.
        /// </summary>
        /// <param name="showStatus">Determines if status should be updated from within method, default is true.</param>
        private bool PatchDeathPenalty(bool showStatus = true)
        {
            if (!this.cbDeathPenalty.IsEnabled || _offset_deathpenalties2 == 0x0 || !CanPatchGame()) return false;
            SetModeTag();
            if (this.cbDeathPenalty.IsChecked == true)
            {
                WriteBytes(_gameAccessHwndStatic, _offset_deathpenalties1, GameData.PATCH_DEATHPENALTIES1_DISABLE);
                WriteBytes(_gameAccessHwndStatic, _offset_deathpenalties2, GameData.PATCH_DEATHPENALTIES2_DISABLE);
            }
            else if (this.cbDeathPenalty.IsChecked == false)
            {
                if (_initialStartup)
                {
                    WriteBytes(_gameAccessHwndStatic, _offset_deathpenalties1, _patch_deathpenalties1_enable);
                    WriteBytes(_gameAccessHwndStatic, _offset_deathpenalties2, _patch_deathpenalties2_enable);
                }
                if (showStatus) UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game unpatched!", Brushes.White);
                return false;
            }

            if (showStatus) UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game patched!", Brushes.Green);
            return true;
        }

        /// <summary>
        /// Patches the game's hidden death penalties.
        /// </summary>
        /// <param name="showStatus">Determines if status should be updated from within method, default is true.</param>
        private bool PatchDeathPenaltyHidden(bool showStatus = true)
        {
            if (!this.cbDeathPenaltyHidden.IsEnabled || _offset_deathscounter_routine == 0x0 || !CanPatchGame()) return false;
            if (this.cbDeathPenaltyHidden.IsChecked == true)
            {
                WriteBytes(_gameAccessHwndStatic, _offset_deathscounter_routine, GameData.PATCH_DEATHSCOUNTER_DISABLE);
            }
            else if (this.cbDeathPenaltyHidden.IsChecked == false)
            {
                if (!_initialStartup)
                    WriteBytes(_gameAccessHwndStatic, _offset_deathscounter_routine, GameData.PATCH_DEATHSCOUNTER_ENABLE);
                if (showStatus) UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game unpatched!", Brushes.White);
                return false;
            }

            if (showStatus) UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game patched!", Brushes.Green);
            return true;
        }

        /// <summary>
        /// Patches game's global speed.
        /// </summary>
        /// <param name="showStatus">Determines if status should be updated from within method, default is true.</param>
        private bool PatchGameSpeed(bool showStatus = true)
        {
            if (!this.cbGameSpeed.IsEnabled || _offset_timescale == 0x0 || !CanPatchGame()) return false;
            if (this.cbGameSpeed.IsChecked == true)
            {
                bool isNumber = Int32.TryParse(this.tbGameSpeed.Text, out int gameSpeed);
                if (gameSpeed < 0 || !isNumber)
                {
                    this.tbGameSpeed.Text = "100";
                    gameSpeed = 100;
                }
                else if (gameSpeed >= 999)
                {
                    this.tbGameSpeed.Text = "999";
                    gameSpeed = 1000;
                }
                float timeScale = gameSpeed / 100f;
                if (timeScale < 0.01f)
                    timeScale = 0.0001f;
                WriteBytes(_gameAccessHwndStatic, _offset_timescale, BitConverter.GetBytes(timeScale));
                SetModeTag();
            }
            else if (this.cbGameSpeed.IsChecked == false)
            {
                if (!_initialStartup)
                    WriteBytes(_gameAccessHwndStatic, _offset_timescale, BitConverter.GetBytes(1.0f));
                if (showStatus) UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game unpatched!", Brushes.White);
                SetModeTag();
                return false;
            }

            if (showStatus) UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game patched!", Brushes.Green);
            return true;
        }

        /// <summary>
        /// Patches game's player speed.
        /// </summary>
        /// <param name="showStatus">Determines if status should be updated from within method, default is true.</param>
        private bool PatchPlayerSpeed(bool showStatus = true)
        {
            if (!this.cbPlayerSpeed.IsEnabled || !CanPatchGame()) return false;
            if (this.cbPlayerSpeed.IsChecked == true)
            {
                if (_offset_timescale_player_pointer_start > 0x0) ReadPlayerTimescaleOffsets();
                if (_offset_timescale_player == 0x0)
                {
                    this.cbPlayerSpeed.IsChecked = false;
                    return false;
                }
            }
            if (_offset_timescale_player == 0x0) return false;
            if (this.cbPlayerSpeed.IsChecked == true)
            {
                bool isNumber = Int32.TryParse(this.tbPlayerSpeed.Text, out int playerSpeed);
                if (playerSpeed < 0 || !isNumber)
                {
                    this.tbPlayerSpeed.Text = "100";
                    playerSpeed = 100;
                }
                else if (playerSpeed >= 999)
                {
                    this.tbPlayerSpeed.Text = "999";
                    playerSpeed = 1000;
                }
                float timeScalePlayer = playerSpeed / 100f;
                if (timeScalePlayer < 0.01f)
                    timeScalePlayer = 0.0001f;
                WriteBytes(_gameAccessHwndStatic, _offset_timescale_player, BitConverter.GetBytes(timeScalePlayer));
                if (!_dispatcherTimerFreezeMem.IsEnabled) _dispatcherTimerFreezeMem.Start();
                SetModeTag();
            }
            else if (this.cbPlayerSpeed.IsChecked == false)
            {
                if (!_initialStartup)
                    WriteBytes(_gameAccessHwndStatic, _offset_timescale_player, BitConverter.GetBytes(1.0f));
                if (showStatus) UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game unpatched!", Brushes.White);
                _dispatcherTimerFreezeMem.Stop();
                SetModeTag();
                return false;
            }

            if (showStatus) UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game patched!", Brushes.Green);
            return true;
        }

        /// <summary>
        /// Patch up this broken port of a game.
        /// </summary>
        private void PatchGame()
        {
            if (!CanPatchGame()) return;

            List<bool> results = new List<bool>
            {
                PatchFramelock(false),
                PatchResolution(false),
                PatchFov(false),
                PatchWindow(false),
                PatchCamReset(false),
                PatchDragonrot(false),
                PatchDeathPenalty(false),
                PatchGameSpeed(false),
                PatchPlayerSpeed(false)
            };
            if (results.IndexOf(true) > -1)
                UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game patched!", Brushes.Green);
            else
                UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game unpatched!", Brushes.White);
            _initialStartup = false;
        }

        /// <summary>
        /// Inject or eject code to game using code caves.
        /// </summary>
        private void InjectToGame()
        {
            if (!CanPatchGame() || !_codeCave_camadjust) return;

            if (this.cbCamAdjust.IsChecked == true)
            {
                if (!_settingsService.ApplicationSettings.cameraAdjustNotify)
                {
                    MessageBoxResult result = MessageBox.Show("Disabling camera auto adjustment is intended for mouse users.\n\n" +
                                                              "If you are using a controller this will not work perfectly and you will temporary loose the deadzones on your controller (slow tiling).\n\n" +
                                                              "Do you want to continue?", "Sekiro FPS Unlocker and more", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (result == MessageBoxResult.No)
                    {
                        this.cbCamAdjust.IsEnabled = false;
                        this.cbCamAdjust.IsChecked = false;
                        this.cbCamAdjust.IsEnabled = true;
                        return;
                    }
                    result = MessageBox.Show("Are you using a mouse as input?\n\n" +
                                             "To change your selection just delete the configuration file: SekiroFpsUnlockAndMore.xml", "Sekiro FPS Unlocker and more", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.No)
                    {
                        _settingsService.ApplicationSettings.peasantInput = true;
                        this.sbInput.Text = "Controller";
                    }
                    else this.sbInput.Text = "Mouse";
                    _settingsService.ApplicationSettings.cameraAdjustNotify = true;
                }

                this.cbCamAdjust.IsEnabled = false;
                _memoryCaveGenerator.ActivateCodeCaveByName(_CODECAVE_CAMADJUST_PITCH);
                _memoryCaveGenerator.ActivateCodeCaveByName(_CODECAVE_CAMADJUST_YAW_Z);
                if (!_settingsService.ApplicationSettings.peasantInput)
                    _memoryCaveGenerator.ActivateCodeCaveByName(_CODECAVE_CAMADJUST_PITCH_XY); // BREAKS PITCH AND OTHER CONTROLS ON CONTROLLERS
                _memoryCaveGenerator.ActivateCodeCaveByName(_CODECAVE_CAMADJUST_YAW_XY);
                this.cbCamAdjust.IsEnabled = true;
            }
            else
            {
                if (!_initialStartup)
                {
                    this.cbCamAdjust.IsEnabled = false;
                    _memoryCaveGenerator.DeactivateCodeCaveByName(_CODECAVE_CAMADJUST_PITCH);
                    _memoryCaveGenerator.DeactivateCodeCaveByName(_CODECAVE_CAMADJUST_YAW_Z);
                    _memoryCaveGenerator.DeactivateCodeCaveByName(_CODECAVE_CAMADJUST_PITCH_XY);
                    _memoryCaveGenerator.DeactivateCodeCaveByName(_CODECAVE_CAMADJUST_YAW_XY);
                    this.cbCamAdjust.IsEnabled = true;
                }
            }
        }

        /// <summary>
        /// Freeze values in memory that can't be patched to require no freezing easily.
        /// </summary>
        private void FreezeMemory(object sender, EventArgs e)
        {
            if (!this.cbPlayerSpeed.IsEnabled || this.cbPlayerSpeed.IsChecked != true)
            {
                _dispatcherTimerFreezeMem.Stop();
                return;
            }
            if (_offset_timescale_player_pointer_start == 0x0 || !CanPatchGame()) return;
            if (_offset_timescale_player_pointer_start > 0x0) ReadPlayerTimescaleOffsets();
            if (_offset_timescale_player == 0x0) return;

            bool isNumber = Int32.TryParse(this.tbPlayerSpeed.Text, out int playerSpeed);
            if (playerSpeed < 0 || !isNumber)
            {
                this.tbPlayerSpeed.Text = "100";
                playerSpeed = 100;
            }
            else if (playerSpeed >= 999)
            {
                this.tbPlayerSpeed.Text = "999";
                playerSpeed = 1000;
            }
            float timeScalePlayer = playerSpeed / 100f;
            if (timeScalePlayer < 0.01f) timeScalePlayer = 0.00001f;
            WriteBytes(_gameAccessHwndStatic, _offset_timescale_player, BitConverter.GetBytes(timeScalePlayer));
        }

        /// <summary>
        /// Reads some hidden stats and outputs them to text files and status bar. Use to display counters on Twitch stream or just look at them and get disappointed.
        /// </summary>
        private void StatsReadTimer(object sender, EventArgs e)
        {
            if (!_running || _gameAccessHwndStatic == IntPtr.Zero || _offset_player_deaths == 0x0 || _offset_total_kills == 0x0) return;
            int playerDeaths = Read<Int32>(_gameAccessHwndStatic, _offset_player_deaths);
            _statusViewModel.Deaths = playerDeaths;
            if (_statLoggingEnabled) LogStatsFile(_path_deathsLog, playerDeaths.ToString());
            int totalKills = Read<Int32>(_gameAccessHwndStatic, _offset_total_kills);
            totalKills -= playerDeaths; // Since this value seems to track every death, including the player
            _statusViewModel.Kills = totalKills;
            if (_statLoggingEnabled) LogStatsFile(_path_killsLog, totalKills.ToString());
        }

        /// <summary>
        /// Sets mode according to user settings.
        /// </summary>
        private void SetModeTag()
        {
            if (_debugMode) return;
            string mode = "";
            bool isGameSpeed = this.cbGameSpeed.IsChecked == true;
            bool isPlayerSpeed = this.cbPlayerSpeed.IsChecked == true;
            if (!Int32.TryParse(this.tbGameSpeed.Text, out int gameSpeed)) gameSpeed = 100;
            if (!Int32.TryParse(this.tbPlayerSpeed.Text, out int playerSpeed)) playerSpeed = 100;
            if (!isGameSpeed) gameSpeed = 100;
            if (!isPlayerSpeed) playerSpeed = 100;
            int speedDifference = playerSpeed - gameSpeed;
            bool gitGudLmao = false;
            if (isGameSpeed || isPlayerSpeed)
            {
                if (speedDifference > 5 || (isGameSpeed && gameSpeed < 90))
                    mode = "Easy mode";
                if (speedDifference > 20 || (isGameSpeed && gameSpeed <= 80))
                {
                    gitGudLmao = true;
                    mode = "Journalist mode";
                }
                if (speedDifference > 35 || (isGameSpeed && gameSpeed <= 65))
                {
                    gitGudLmao = true;
                    mode = "you've cheated yourself";
                }
                if (speedDifference <= -10 && (!isGameSpeed || gameSpeed >= 100))
                    mode = "Getting gud";
                if (isGameSpeed && gameSpeed == 0)
                    mode = "Time freeze";
                if (isGameSpeed && gameSpeed >= 200)
                    mode = "Super speed";
            }
            if (this.cbDeathPenalty.IsChecked == true)
            {
                mode = "Cheater mode";
                this.sbMode.Foreground = Brushes.Red;
            }
            else if (gitGudLmao)
            {
                ResourceDictionary resourceDictionary = Application.Current.Resources;
                LinearGradientBrush statusBarModeColor = resourceDictionary["resStatusBarModeColorMock"] as LinearGradientBrush;
                this.sbMode.Foreground = statusBarModeColor;
            }
            else
                this.sbMode.Foreground = Brushes.Black;

            this.sbMode.Text = mode;
        }

        /// <summary>
        /// Returns the hexadecimal representation of an IEEE-754 floating point number
        /// </summary>
        /// <param name="input">The floating point number.</param>
        /// <returns>The hexadecimal representation of the input.</returns>
        private static string GetHexRepresentationFromFloat(float input)
        {
            uint f = BitConverter.ToUInt32(BitConverter.GetBytes(input), 0);
            return "0x" + f.ToString("X8");
        }

        /// <summary>
        /// Calculates DPI-clean resolution of the primary screen. Requires dpiAware in manifest.
        /// </summary>
        /// <returns></returns>
        private Size GetDpiSafeResolution()
        {
            PresentationSource presentationSource = PresentationSource.FromVisual(this);
            if (presentationSource == null)
                return new Size(SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
            Matrix matrix = presentationSource.CompositionTarget.TransformToDevice;
            return new Size(SystemParameters.PrimaryScreenWidth * matrix.M22, SystemParameters.PrimaryScreenHeight * matrix.M11);
        }

        /// <summary>
        /// Checks if window is minimized.
        /// </summary>
        /// <param name="hWnd">The main window handle of the window.</param>
        /// <remarks>
        /// Even minimized fullscreen windows have WS_MINIMIZED normal borders and caption set.
        /// </remarks>
        /// <returns>True if window is minimized.</returns>
        private static bool IsMinimized(IntPtr hWnd)
        {
            long wndStyle = GetWindowLongPtr(hWnd, GWL_STYLE).ToInt64();
            if (wndStyle == 0)
                return false;

            return (wndStyle & WS_MINIMIZE) != 0;
        }

        /// <summary>
        /// Checks if window is in fullscreen mode.
        /// </summary>
        /// <param name="hWnd">The main window handle of the window.</param>
        /// <remarks>
        /// Fullscreen windows have WS_EX_TOPMOST flag set.
        /// </remarks>
        /// <returns>True if window is run in fullscreen mode.</returns>
        private static bool IsFullscreen(IntPtr hWnd)
        {
            long wndStyle = GetWindowLongPtr(hWnd, GWL_STYLE).ToInt64();
            long wndExStyle = GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64();
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
        /// <param name="hWnd">The main window handle of the window.</param>
        /// <remarks>
        /// Borderless windows have WS_POPUP flag set.
        /// </remarks>
        /// <returns>True if window is run in borderless window mode.</returns>
        private static bool IsBorderless(IntPtr hWnd)
        {
            long wndStyle = GetWindowLongPtr(hWnd, GWL_STYLE).ToInt64();
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
        /// <param name="hWnd">The handle to the window.</param>
        /// <param name="width">The desired window width.</param>
        /// <param name="height">The desired window height.</param>
        /// <param name="posX">The desired X position of the window.</param>
        /// <param name="posY">The desired Y position of the window.</param>
        /// <param name="demoMode">Execute functionality without stealing focus, does not retain client size scaling. FOR DEMONSTRATION ONLY.</param>
        private static void SetWindowWindowed(IntPtr hWnd, int width, int height, int posX, int posY, bool demoMode = false)
        {
            SetWindowLongPtr(hWnd, GWL_STYLE, WS_VISIBLE | WS_CAPTION | WS_BORDER | WS_CLIPSIBLINGS | WS_DLGFRAME | WS_SYSMENU | WS_GROUP | WS_MINIMIZEBOX);
            SetWindowPos(hWnd, HWND_NOTOPMOST, posX, posY, width, height, !demoMode ? SWP_FRAMECHANGED | SWP_SHOWWINDOW : SWP_SHOWWINDOW | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        /// <summary>
        /// Sets a window to borderless windowed mode and moves it to position 0x0.
        /// </summary>
        /// <param name="hWnd">The handle to the window.</param>
        /// <param name="width">The desired window width.</param>
        /// <param name="height">The desired window height.</param>
        /// <param name="posX">The desired X position of the window.</param>
        /// <param name="posY">The desired Y position of the window.</param>
        /// <param name="demoMode">Execute functionality without stealing focus, does not retain client size scaling. FOR DEMONSTRATION ONLY.</param>
        private static void SetWindowBorderless(IntPtr hWnd, int width, int height, int posX, int posY, bool demoMode = false)
        {
            SetWindowLongPtr(hWnd, GWL_STYLE, WS_VISIBLE | WS_POPUP);
            SetWindowPos(hWnd, !demoMode ? HWND_TOP : HWND_NOTOPMOST, posX, posY, width, height, !demoMode ? SWP_FRAMECHANGED | SWP_SHOWWINDOW : SWP_SHOWWINDOW | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        /// <summary>
        /// Checks if an address is valid.
        /// </summary>
        /// <param name="address">The address (the pointer points to).</param>
        /// <returns>True if (pointer points to a) valid address.</returns>
        private static bool IsValidAddress(Int64 address)
        {
            return (address >= 0x10000 && address < 0x000F000000000000);
        }

        /// <summary>
        /// Reads a given type from processes memory using a generic method.
        /// </summary>
        /// <typeparam name="T">The base type to read.</typeparam>
        /// <param name="hProcess">The process handle to read from.</param>
        /// <param name="lpBaseAddress">The address to read from.</param>
        /// <returns>The given base type read from memory.</returns>
        /// <remarks>GCHandle and Marshal are costy.</remarks>
        private static T Read<T>(IntPtr hProcess, Int64 lpBaseAddress)
        {
            byte[] lpBuffer = new byte[Marshal.SizeOf(typeof(T))];
            ReadProcessMemory(hProcess, lpBaseAddress, lpBuffer, (ulong)lpBuffer.Length, out _);
            GCHandle gcHandle = GCHandle.Alloc(lpBuffer, GCHandleType.Pinned);
            T structure = (T)Marshal.PtrToStructure(gcHandle.AddrOfPinnedObject(), typeof(T));
            gcHandle.Free();
            return structure;
        }

        /// <summary>
        /// Writes a given type and value to processes memory using a generic method.
        /// </summary>
        /// <param name="hProcess">The process handle to read from.</param>
        /// <param name="lpBaseAddress">The address to write from.</param>
        /// <param name="bytes">The byte array to write.</param>
        /// <returns>True if successful, false otherwise.</returns>
        private static bool WriteBytes(IntPtr hProcess, Int64 lpBaseAddress, byte[] bytes)
        {
            return WriteProcessMemory(hProcess, lpBaseAddress, bytes, (ulong)bytes.Length, out _);
        }

        /// <summary>
        /// Gets the static offset to the referenced object instead of the offset from the instruction.
        /// </summary>
        /// <param name="hProcess">Handle to the process.</param>
        /// <param name="lpInstructionAddress">The address of the instruction.</param>
        /// <param name="instructionLength">The length of the instruction including the 4 bytes offset.</param>
        /// <remarks>Static pointers in x86-64 are relative offsets from their instruction address.</remarks>
        /// <returns>The static offset from the process to the referenced object.</returns>
        private static Int64 DereferenceStaticX64Pointer(IntPtr hProcess, Int64 lpInstructionAddress, int instructionLength)
        {
            return lpInstructionAddress + Read<Int32>(hProcess, lpInstructionAddress + (instructionLength - 0x04)) + instructionLength;
        }

        /// <summary>
        /// Check whether input is numeric only.
        /// </summary>
        /// <param name="text">The text to check.</param>
        /// <returns>True if input is numeric only.</returns>
        private static bool IsNumericInput(string text)
        {
            return Regex.IsMatch(text, "[^0-9]+");
        }

        /// <summary>
        /// Logs messages to log file.
        /// </summary>
        /// <param name="msg">The message to write to file.</param>
        internal static void LogToFile(string msg)
        {
            string timedMsg = "[" + DateTime.Now + "] " + msg;
            Debug.WriteLine(timedMsg);
            try
            {
                using (StreamWriter writer = new StreamWriter(_path_logs, true))
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
        /// Logs stats values to separate files for use in OBS or similar.
        /// </summary>
        /// <param name="filename">The filepath to the status file.</param>
        /// <param name="msg">The value to write to the text file.</param>
        private void LogStatsFile(string filename, string msg)
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(filename, false))
                {
                    writer.Write(msg);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed writing stats file: " + ex.Message, "Sekiro Fps Unlock And More");
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

        private void CbFramelock_Check_Handler(object sender, RoutedEventArgs e)
        {
            PatchFramelock();
        }

        private void CbAddResolution_Check_Handler(object sender, RoutedEventArgs e)
        {
            PatchResolution();
        }

        private void CbFov_Check_Handler(object sender, RoutedEventArgs e)
        {
            PatchFov();
        }

        private void BFov0_Click(object sender, RoutedEventArgs e)
        {
            this.tbFov.Text = "0";
            if (this.cbFov.IsChecked == true) PatchFov();
        }

        private void BFovLower_Click(object sender, RoutedEventArgs e)
        {
            if (Int32.TryParse(this.tbFov.Text, out int fov) && fov > -91)
            {
                this.tbFov.Text = (fov - 5).ToString();
                if (this.cbFov.IsChecked == true) PatchFov();
            }
        }

        private void BFovHigher_Click(object sender, RoutedEventArgs e)
        {
            if (Int32.TryParse(this.tbFov.Text, out int fov) && fov < 91)
            {
                this.tbFov.Text = (fov + 5).ToString();
                if (this.cbFov.IsChecked == true) PatchFov();
            }
        }

        private void CbBorderless_Checked(object sender, RoutedEventArgs e)
        {
            if (!this.cbBorderless.IsEnabled) return;
            this.cbBorderlessStretch.IsEnabled = true;
            PatchWindow();
        }

        private void CbBorderless_Unchecked(object sender, RoutedEventArgs e)
        {
            if (!this.cbBorderless.IsEnabled) return;
            this.cbBorderlessStretch.IsEnabled = false;
            this.cbBorderlessStretch.IsChecked = false;
            PatchWindow();
        }

        private void CbBorderlessStretch_Check_Handler(object sender, RoutedEventArgs e)
        {
            if (!this.cbBorderlessStretch.IsEnabled) return;
            PatchWindow();
        }

        private void CbStatChanged(object sender, RoutedEventArgs e)
        {
            _statLoggingEnabled = cbLogStats.IsChecked == true;
            if (!_statLoggingEnabled)
            {
                try
                {
                    if (File.Exists(_path_deathsLog))
                        File.Delete(_path_deathsLog);
                    if (File.Exists(_path_killsLog))
                        File.Delete(_path_killsLog);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Failed to delete stats files: " + ex.Message, "Sekiro Fps Unlock And More");
                }
            }
        }

        private void CbCamAdjust_Check_Handler(object sender, RoutedEventArgs e)
        {
            if (this.cbCamAdjust.IsEnabled)
                InjectToGame();
        }

        private void CbCamReset_Check_Handler(object sender, RoutedEventArgs e)
        {
            if (this.cbCamReset.IsEnabled)
                PatchCamReset();
        }

        private void CbDragonrot_Check_Handler(object sender, RoutedEventArgs e)
        {
            if (this.cbDragonrot.IsEnabled)
                PatchDragonrot();
        }

        private void CbDeathPenalty_Check_Handler(object sender, RoutedEventArgs e)
        {
            if (this.cbDeathPenalty.IsEnabled)
                PatchDeathPenalty();
        }

        private void CbDeathPenaltyHidden_Check_Handler(object sender, RoutedEventArgs e)
        {
            if (this.cbDeathPenaltyHidden.IsEnabled && this.cbDeathPenaltyHidden.Visibility == Visibility.Visible)
                PatchDeathPenaltyHidden();
        }

        private void CbGameSpeed_Check_Handler(object sender, RoutedEventArgs e)
        {
            PatchGameSpeed();
        }

        private void BGs0_Click(object sender, RoutedEventArgs e)
        {
            this.tbGameSpeed.Text = "0";
            if (cbGameSpeed.IsChecked == true) PatchGameSpeed();
        }

        private void BGsLower_Click(object sender, RoutedEventArgs e)
        {
            if (Int32.TryParse(this.tbGameSpeed.Text, out int gameSpeed) && gameSpeed > 4)
            {
                this.tbGameSpeed.Text = (gameSpeed - 5).ToString();
                if (cbGameSpeed.IsChecked == true) PatchGameSpeed();
            }
        }

        private void BGsHigher_Click(object sender, RoutedEventArgs e)
        {
            if (Int32.TryParse(this.tbGameSpeed.Text, out int gameSpeed) && gameSpeed < 995)
            {
                this.tbGameSpeed.Text = (gameSpeed + 5).ToString();
                if (cbGameSpeed.IsChecked == true) PatchGameSpeed();
            }
        }

        private void BGs100_Click(object sender, RoutedEventArgs e)
        {
            this.tbGameSpeed.Text = "100";
            if (cbGameSpeed.IsChecked == true) PatchGameSpeed();
        }

        private void CbPlayerSpeed_Check_Handler(object sender, RoutedEventArgs e)
        {
            PatchPlayerSpeed();
        }

        private void BPs0_Click(object sender, RoutedEventArgs e)
        {
            this.tbPlayerSpeed.Text = "0";
            if (this.cbPlayerSpeed.IsChecked == true) PatchPlayerSpeed();
        }

        private void BPsLower_Click(object sender, RoutedEventArgs e)
        {
            if (Int32.TryParse(this.tbPlayerSpeed.Text, out int playerSpeed) && playerSpeed > 4)
            {
                this.tbPlayerSpeed.Text = (playerSpeed - 5).ToString();
                if (this.cbPlayerSpeed.IsChecked == true) PatchPlayerSpeed();
            }
        }

        private void BPsHigher_Click(object sender, RoutedEventArgs e)
        {
            if (Int32.TryParse(this.tbPlayerSpeed.Text, out int playerSpeed) && playerSpeed < 995)
            {
                this.tbPlayerSpeed.Text = (playerSpeed + 5).ToString();
                if (this.cbPlayerSpeed.IsChecked == true) PatchPlayerSpeed();
            }
        }

        private void BPs100_Click(object sender, RoutedEventArgs e)
        {
            this.tbPlayerSpeed.Text = "100";
            if (this.cbPlayerSpeed.IsChecked == true) PatchPlayerSpeed();
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
        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const uint WS_GROUP = 0x00020000;
        private const uint WS_MINIMIZEBOX = 0x00020000;
        private const uint WS_SYSMENU = 0x00080000;
        private const uint WS_DLGFRAME = 0x00400000;
        private const uint WS_BORDER = 0x00800000;
        private const uint WS_CAPTION = 0x00C00000;
        private const uint WS_CLIPSIBLINGS = 0x04000000;
        private const uint WS_VISIBLE = 0x10000000;
        private const uint WS_MINIMIZE = 0x20000000;
        private const uint WS_POPUP = 0x80000000;
        private const uint WS_EX_TOPMOST = 0x00000008;
        private const int HWND_TOP = 0;
        private const int HWND_NOTOPMOST = -2;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int ZUH_HIDDEN_DP = 0x7;

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
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

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
