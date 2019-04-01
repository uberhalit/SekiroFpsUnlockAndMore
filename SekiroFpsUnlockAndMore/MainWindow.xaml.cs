using System;
using System.IO;
using System.Timers;
using System.Windows;
using System.Threading;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Input;
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
        internal long _offset_framelock_speed_fix = 0x0;
        internal long _offset_resolution = 0x0;
        internal long _offset_resolution_default = 0x0;
        internal long _offset_resolution_scaling_fix = 0x0;
        internal long _offset_fovsetting = 0x0;
        internal long _offset_total_kills = 0x0;
        internal long _offset_player_deaths = 0x0;
        internal long _offset_timescale = 0x0;
        internal long _offset_timescale_player = 0x0;
        internal long _offset_timescale_player_pointer_start = 0x0;

        internal SettingsService _settingsService;
        internal StatusViewModel _statusViewModel = new StatusViewModel();
        
		internal readonly System.Timers.Timer _timerStatsCheck = new System.Timers.Timer();
		internal readonly DispatcherTimer _dispatcherTimerFreezeMem = new DispatcherTimer();
        internal readonly DispatcherTimer _dispatcherTimerCheck = new DispatcherTimer();
        internal bool _running = false;
        internal string _logPath;
        internal string _deathCounterPath;
        internal string _killCounterPath;
        internal bool _retryAccess = true;
		internal bool _use_resolution_720 = false;
        internal bool _statLoggingEnabled = false;
        internal RECT _windowRect;

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
                MessageBox.Show("Another instance is already running!", "Sekiro FPS Unlocker and more");
                Environment.Exit(0);
            }
            GC.KeepAlive(mutex);

            _logPath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location) + @"\SekiroFpsUnlockAndMore.log";
            _deathCounterPath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location) + @"\DeathCounter.txt";
            _killCounterPath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location) + @"\TotalKillsCounter.txt";

            this.cbSelectFov.ItemsSource = GameData.PATCH_FOVSETTING_MATRIX;
            this.cbSelectFov.SelectedIndex = 2;

            LoadConfiguration();

            IntPtr hWnd = new WindowInteropHelper(this).Handle;
            if (!RegisterHotKey(hWnd, 9009, MOD_CONTROL, VK_P))
                MessageBox.Show("Hotkey is already in use, it may not work.", "Sekiro FPS Unlocker and more");

            ComponentDispatcher.ThreadFilterMessage += new ThreadMessageEventHandler(ComponentDispatcherThreadFilterMessage);

            _dispatcherTimerCheck.Tick += new EventHandler(async (object s, EventArgs a) =>
            {
                bool result = await CheckGame();
                if (result) PatchGame();
            });
            _dispatcherTimerCheck.Interval = new TimeSpan(0, 0, 0, 2);
            _dispatcherTimerCheck.Start();

            _dispatcherTimerFreezeMem.Tick += new EventHandler(FreezeMemory);
            _dispatcherTimerFreezeMem.Interval = new TimeSpan(0, 0, 0, 0, 2000);

            _timerStatsCheck.Elapsed += new ElapsedEventHandler(StatReadTimer);
            _timerStatsCheck.Interval = 2000;
		}

        /// <summary>
        /// On window closing.
        /// </summary>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
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
            this.cbSelectFov.SelectedIndex = _settingsService.ApplicationSettings.cbSelectFov;
            this.cbBorderless.IsChecked = _settingsService.ApplicationSettings.cbBorderless;
            this.cbBorderlessStretch.IsChecked = _settingsService.ApplicationSettings.cbBorderlessStretch;
            this.exGameMods.IsExpanded = _settingsService.ApplicationSettings.exGameMods;
            this.cbGameSpeed.IsChecked = _settingsService.ApplicationSettings.cbGameSpeed;
            this.tbGameSpeed.Text = _settingsService.ApplicationSettings.tbGameSpeed.ToString();
            this.cbPlayerSpeed.IsChecked = _settingsService.ApplicationSettings.cbPlayerSpeed;
            this.tbPlayerSpeed.Text = _settingsService.ApplicationSettings.tbPlayerSpeed.ToString();
			this.cbLogStats.IsChecked = _settingsService.ApplicationSettings.cbLogStats;
		}

        /// <summary>
        /// Save all settings to configuration file.
        /// </summary>
        private void SaveConfiguration()
        {
            _settingsService.ApplicationSettings.cbFramelock = this.cbFramelock.IsChecked == true;
            _settingsService.ApplicationSettings.tbFramelock = Convert.ToInt32(this.tbFramelock.Text);
            _settingsService.ApplicationSettings.cbAddResolution = this.cbAddResolution.IsChecked == true;
            _settingsService.ApplicationSettings.tbWidth = Convert.ToInt32(this.tbWidth.Text);
            _settingsService.ApplicationSettings.tbHeight = Convert.ToInt32(this.tbHeight.Text);
            _settingsService.ApplicationSettings.cbFov = this.cbFov.IsChecked == true;
            _settingsService.ApplicationSettings.cbSelectFov = this.cbSelectFov.SelectedIndex;
            _settingsService.ApplicationSettings.cbBorderless = this.cbBorderless.IsChecked == true;
            _settingsService.ApplicationSettings.cbBorderlessStretch = this.cbBorderlessStretch.IsChecked == true;
            _settingsService.ApplicationSettings.exGameMods = this.exGameMods.IsExpanded;
            _settingsService.ApplicationSettings.cbGameSpeed = this.cbGameSpeed.IsChecked == true;
            _settingsService.ApplicationSettings.tbGameSpeed = Convert.ToInt32(this.tbGameSpeed.Text);
            _settingsService.ApplicationSettings.cbPlayerSpeed = this.cbPlayerSpeed.IsChecked == true;
            _settingsService.ApplicationSettings.tbPlayerSpeed = Convert.ToInt32(this.tbPlayerSpeed.Text);
			_settingsService.ApplicationSettings.cbLogStats = this.cbLogStats.IsChecked == true;
			_settingsService.Save();
        }

        /// <summary>
        /// Checks if game is running and initializes further functionality.
        /// </summary>
        private Task<bool> CheckGame()
        {
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
                Task.FromResult(false);
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
                    _dispatcherTimerCheck.Stop();
                    Task.FromResult(false);
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
                Task.FromResult(false);
            }

            //string gameFileVersion = FileVersionInfo.GetVersionInfo(procList[0].MainModule.FileName).FileVersion;
            PatternScan patternScan = new PatternScan(_gameAccessHwnd, _gameProc.MainModule);

            _offset_framelock = patternScan.FindPatternInternal(GameData.PATTERN_FRAMELOCK, GameData.PATTERN_FRAMELOCK_MASK, ' ') + GameData.PATTERN_FRAMELOCK_OFFSET;
            Debug.WriteLine("fFrameTick found at: 0x" + _offset_framelock.ToString("X"));
            if (!IsValidAddress(_offset_framelock))
            {
                _offset_framelock = patternScan.FindPatternInternal(GameData.PATTERN_FRAMELOCK_FUZZY, GameData.PATTERN_FRAMELOCK_FUZZY_MASK, ' ') + GameData.PATTERN_FRAMELOCK_FUZZY_OFFSET;
                Debug.WriteLine("2. fFrameTick found at: 0x" + _offset_framelock.ToString("X"));
            }
            if (!IsValidAddress(_offset_framelock))
            {
                UpdateStatus("fFrameTick not found...", Brushes.Red);
                LogToFile("fFrameTick not found...");
                _offset_framelock = 0x0;
                this.cbFramelock.IsEnabled = false;
            }

            _offset_framelock_speed_fix = patternScan.FindPatternInternal(GameData.PATTERN_FRAMELOCK_SPEED_FIX, GameData.PATTERN_FRAMELOCK_SPEED_FIX_MASK, ' ') + GameData.PATTERN_FRAMELOCK_SPEED_FIX_OFFSET;
            Debug.WriteLine("pFrametimeRunningSpeed at: 0x" + _offset_framelock_speed_fix.ToString("X"));
            if (!IsValidAddress(_offset_framelock_speed_fix))
            {
                UpdateStatus("pFrametimeRunningSpeed no found...", Brushes.Red);
                LogToFile("pFrametimeRunningSpeed not found...");
                _offset_framelock_speed_fix = 0x0;
                this.cbFramelock.IsEnabled = false;
            }

            bool enableResolutionPatch = true;
            if ((int) SystemParameters.PrimaryScreenWidth < 1281) _use_resolution_720 = true;
            _offset_resolution_default = patternScan.FindPatternInternal(!_use_resolution_720 ? GameData.PATTERN_RESOLUTION_DEFAULT : GameData.PATTERN_RESOLUTION_DEFAULT_720, GameData.PATTERN_RESOLUTION_DEFAULT_MASK, ' ');
            Debug.WriteLine("default resolution found at: 0x" + _offset_resolution_default.ToString("X"));
            if (!IsValidAddress(_offset_resolution_default))
            {
                UpdateStatus("default resolution not found...", Brushes.Red);
                LogToFile("default resolution not found...");
                enableResolutionPatch = false;
                _offset_resolution_default = 0x0;
            }
            _offset_resolution_scaling_fix = patternScan.FindPatternInternal(GameData.PATTERN_RESOLUTION_SCALING_FIX, GameData.PATTERN_RESOLUTION_SCALING_FIX_MASK, ' ') + GameData.PATTERN_RESOLUTION_SCALING_FIX_OFFSET;
            Debug.WriteLine("scaling fix found at: 0x" + _offset_resolution_scaling_fix.ToString("X"));
            if (!IsValidAddress(_offset_resolution_scaling_fix))
            {
                UpdateStatus("scaling fix not found...", Brushes.Red);
                LogToFile("scaling fix not found...");
                enableResolutionPatch = false;
                _offset_resolution_scaling_fix = 0x0;
            }
            long ref_pCurrentResolutionWidth = patternScan.FindPatternInternal(GameData.PATTERN_RESOLUTION_POINTER, GameData.PATTERN_RESOLUTION_POINTER_MASK, ' ') + GameData.PATTERN_RESOLUTION_POINTER_OFFSET;
            Debug.WriteLine("ref_pCurrentResolutionWidth found at: 0x" + ref_pCurrentResolutionWidth.ToString("X"));
            if (!IsValidAddress(ref_pCurrentResolutionWidth))
            {
                UpdateStatus("re_pCurrentResolutionWidth not found...", Brushes.Red);
                LogToFile("ref_pCurrentResolutionWidth not found...");
                enableResolutionPatch = false;
            }
            else
            {
                _offset_resolution = DereferenceStaticX64Pointer(_gameAccessHwnd, ref_pCurrentResolutionWidth, GameData.PATTERN_RESOLUTION_POINTER_INSTRUCTION_LENGTH);
                Debug.WriteLine("pCurrentResolutionWidth at: 0x" + _offset_resolution.ToString("X"));
                if (!IsValidAddress(_offset_resolution))
                {
                    UpdateStatus("pCurrentResolutionWidth not valid...", Brushes.Red);
                    LogToFile("pCurrentResolutionWidth not valid...");
                    _offset_resolution = 0x0;
                }
            }
            this.cbAddResolution.IsEnabled = enableResolutionPatch;

            _offset_fovsetting = patternScan.FindPatternInternal(GameData.PATTERN_FOVSETTING, GameData.PATTERN_FOVSETTING_MASK, ' ') + GameData.PATTERN_FOVSETTING_OFFSET;
            Debug.WriteLine("pFovTableEntry found at: 0x" + _offset_fovsetting.ToString("X"));
            if (!IsValidAddress(_offset_fovsetting))
            {
                UpdateStatus("pFovTableEntry not found...", Brushes.Red);
                LogToFile("pFovTableEntry not found...");
                _offset_fovsetting = 0x0;
                this.cbFov.IsEnabled = false;
            }

            long ref_pPlayerStatsRelated = patternScan.FindPatternInternal(GameData.PATTERN_PLAYER_DEATHS, GameData.PATTERN_PLAYER_DEATHS_MASK, ' ') + GameData.PATTERN_PLAYER_DEATHS_OFFSET;
			Debug.WriteLine("ref_pPlayerStatsRelated found at: 0x" + ref_pPlayerStatsRelated.ToString("X"));
			if (!IsValidAddress(ref_pPlayerStatsRelated))
			{
			    UpdateStatus("ref_pPlayerStatsRelated not found...", Brushes.Red);
			    LogToFile("ref_pPlayerStatsRelated not found...");
			    this.cbLogStats.IsEnabled = false;
            }
			else
			{
			    long pPlayerStatsRelated = DereferenceStaticX64Pointer(_gameAccessHwndStatic, ref_pPlayerStatsRelated, GameData.PATTERN_PLAYER_DEATHS_INSTRUCTION_LENGTH);
			    Debug.WriteLine("pPlayerStatsRelated found at: 0x" + pPlayerStatsRelated.ToString("X"));
                if (IsValidAddress(pPlayerStatsRelated))
			    {
			        int playerStatsToDeathsOffset = Read<Int32>(_gameAccessHwndStatic, ref_pPlayerStatsRelated + GameData.PATTERN_PLAYER_DEATHS_POINTER_OFFSET_OFFSET);
			        Debug.WriteLine("offset pPlayerStats->iPlayerDeaths found : 0x" + playerStatsToDeathsOffset.ToString("X"));
                    if (playerStatsToDeathsOffset > 0) _offset_player_deaths = Read<Int64>(_gameAccessHwndStatic, pPlayerStatsRelated) + playerStatsToDeathsOffset;
			        Debug.WriteLine("iPlayerDeaths found at: 0x" + _offset_player_deaths.ToString("X"));
                }
			}
            if (!IsValidAddress(_offset_player_deaths))
            {
                UpdateStatus("Player Deaths not found...", Brushes.Red);
                LogToFile("Player Deaths not found...");
                _offset_player_deaths = 0x0;
                this.cbLogStats.IsEnabled = false;
            }

			long ref_pTotalKills = patternScan.FindPatternInternal(GameData.PATTERN_TOTAL_KILLS, GameData.PATTERN_TOTAL_KILLS_MASK, ' ');
			Debug.WriteLine("ref_pTotalKills found at: 0x" + ref_pTotalKills.ToString("X"));
			if (!IsValidAddress(ref_pTotalKills))
			{
			    UpdateStatus("ref_pTotalKills not found...", Brushes.Red);
                LogToFile("ref_pTotalKills not found...");
			    this.cbLogStats.IsEnabled = false;
            }
			else
			{
			    _offset_total_kills = DereferenceStaticX64Pointer(_gameAccessHwndStatic, ref_pTotalKills, GameData.PATTERN_TOTAL_KILLS_INSTRUCTION_LENGTH);
			    if (!IsValidAddress(_offset_total_kills))
			    {
			        UpdateStatus("pTotalKills not valid...", Brushes.Red);
			        LogToFile("pTotalKills not valid...");
			        _offset_total_kills = 0x0;
                    this.cbLogStats.IsEnabled = false;
                }
			}
            if (_offset_player_deaths > 0x0 && _offset_total_kills > 0x0) _timerStatsCheck.Start();

            this.cbBorderless.IsEnabled = true;

            long ref_pTimeRelated = patternScan.FindPatternInternal(GameData.PATTERN_TIMESCALE, GameData.PATTERN_TIMESCALE_MASK, ' ');
            Debug.WriteLine("ref_pTimeRelated found at: 0x" + ref_pTimeRelated.ToString("X"));
            if (IsValidAddress(ref_pTimeRelated))
            {
                long pTimescaleManager = DereferenceStaticX64Pointer(_gameAccessHwndStatic, ref_pTimeRelated, GameData.PATTERN_TIMESCALE_INSTRUCTION_LENGTH);
                Debug.WriteLine("pTimescaleManager found at: 0x" + pTimescaleManager.ToString("X"));
                if (IsValidAddress(pTimescaleManager))
                {
                    _offset_timescale = Read<Int64>(_gameAccessHwndStatic, pTimescaleManager) + Read<Int32>(_gameAccessHwndStatic, ref_pTimeRelated + GameData.PATTERN_TIMESCALE_POINTER_OFFSET_OFFSET);
                    Debug.WriteLine("fTimescale found at: 0x" + _offset_timescale.ToString("X"));
                    if (!IsValidAddress(_offset_timescale))
                    {
                        _offset_timescale = 0x0;
                    }
                }
            }
            if (_offset_timescale == 0x0)
            {
                UpdateStatus("fTimescale not found...", Brushes.Red);
                LogToFile("fTimescale not found...");
                this.cbGameSpeed.IsEnabled = false;
            }

            long pPlayerStructRelated1 = patternScan.FindPatternInternal(GameData.PATTERN_TIMESCALE_PLAYER, GameData.PATTERN_TIMESCALE_PLAYER_MASK, ' ');
            Debug.WriteLine("pPlayerStructRelated1 found at: 0x" + pPlayerStructRelated1.ToString("X"));
            if (IsValidAddress(pPlayerStructRelated1))
            {
                long pPlayerStructRelated2 = DereferenceStaticX64Pointer(_gameAccessHwndStatic, pPlayerStructRelated1, GameData.PATTERN_TIMESCALE_PLAYER_INSTRUCTION_LENGTH);
                Debug.WriteLine("pPlayerStructRelated2 found at: 0x" + pPlayerStructRelated2.ToString("X"));
                if (IsValidAddress(pPlayerStructRelated2))
                {
                    _offset_timescale_player_pointer_start = pPlayerStructRelated2;
                    long pPlayerStructRelated3 = Read<Int64>(_gameAccessHwndStatic, pPlayerStructRelated2) + GameData.PATTERN_TIMESCALE_POINTER2_OFFSET;
                    Debug.WriteLine("pPlayerStructRelated3 found at: 0x" + pPlayerStructRelated3.ToString("X"));
                    if (IsValidAddress(pPlayerStructRelated3))
                    {
                        long pPlayerStructRelated4 = Read<Int64>(_gameAccessHwndStatic, pPlayerStructRelated3) + GameData.PATTERN_TIMESCALE_POINTER3_OFFSET;
                        Debug.WriteLine("pPlayerStructRelated4 found at: 0x" + pPlayerStructRelated4.ToString("X"));
                        if (IsValidAddress(pPlayerStructRelated4))
                        {
                            long pPlayerStructRelated5 = Read<Int64>(_gameAccessHwndStatic, pPlayerStructRelated4) + GameData.PATTERN_TIMESCALE_POINTER4_OFFSET;
                            Debug.WriteLine("pPlayerStructRelated5 found at: 0x" + pPlayerStructRelated5.ToString("X"));
                            if (IsValidAddress(pPlayerStructRelated5))
                            {
                                _offset_timescale_player = Read<Int64>(_gameAccessHwndStatic, pPlayerStructRelated5) + GameData.PATTERN_TIMESCALE_POINTER5_OFFSET;
                                Debug.WriteLine("fTimescalePlayer found at: 0x" + _offset_timescale_player.ToString("X"));
                                if (!IsValidAddress(_offset_timescale_player))
                                {
                                    _offset_timescale_player = 0x0;
                                }
                            }
                        }
                    }
                }
            }
            if (_offset_timescale_player_pointer_start == 0x0)
            {
                UpdateStatus("Playerscale not found...", Brushes.Red);
                LogToFile("Playerscale not found...");
                this.cbPlayerSpeed.IsEnabled = false;
            }

            this.bPatch.IsEnabled = true;

			_running = true;
            _dispatcherTimerCheck.Stop();
            return Task.FromResult(true);
        }

        /// <summary>
        /// Read and refresh all offsets that can change on quick travel or save game loading.
        /// </summary>
        private void ReadIngameOffsets()
        {
            bool valid = false;
            if (_offset_timescale_player_pointer_start > 0)
            {
                long pPlayerStructRelated3 = Read<Int64>(_gameAccessHwndStatic, _offset_timescale_player_pointer_start) + GameData.PATTERN_TIMESCALE_POINTER2_OFFSET;
                if (IsValidAddress(pPlayerStructRelated3))
                {
                    long pPlayerStructRelated4 = Read<Int64>(_gameAccessHwndStatic, pPlayerStructRelated3) + GameData.PATTERN_TIMESCALE_POINTER3_OFFSET;
                    if (IsValidAddress(pPlayerStructRelated4))
                    {
                        long pPlayerStructRelated5 = Read<Int64>(_gameAccessHwndStatic, pPlayerStructRelated4) + GameData.PATTERN_TIMESCALE_POINTER4_OFFSET;
                        if (IsValidAddress(pPlayerStructRelated5))
                        {
                            _offset_timescale_player = Read<Int64>(_gameAccessHwndStatic, pPlayerStructRelated5) + GameData.PATTERN_TIMESCALE_POINTER5_OFFSET;
                            if (IsValidAddress(_offset_timescale_player))
                            {
                                valid = true;
                            }
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
            _gameProc = null;
            _gameHwnd = IntPtr.Zero;
            _gameAccessHwnd = IntPtr.Zero;
            _gameAccessHwndStatic = IntPtr.Zero;
            _offset_framelock = 0x0;
            _offset_framelock_speed_fix = 0x0;
            _offset_resolution = 0x0;
            _offset_resolution_default = 0x0;
            _offset_resolution_scaling_fix = 0x0;
            _offset_fovsetting = 0x0;
            _offset_player_deaths = 0x0;
            _offset_total_kills = 0x0;
            _offset_timescale = 0x0;
            _offset_timescale_player = 0x0;
            this.cbFramelock.IsEnabled = true;
            this.cbAddResolution.IsEnabled = true;
            this.cbFov.IsEnabled = true;
            this.cbBorderless.IsEnabled = false;
            this.bPatch.IsEnabled = false;
            this.cbGameSpeed.IsEnabled = true;
            this.cbPlayerSpeed.IsEnabled = true;
            UpdateStatus("waiting for game...", Brushes.White);
            _dispatcherTimerCheck.Start();

            return false;
        }

        /// <summary>
        /// Patch the game's frame rate lock.
        /// </summary>
        /// <param name="showStatus">Determines if status should be updated from within method, default is true.</param>
        private bool PatchFramelock(bool showStatus = true)
        {
            if (!this.cbFramelock.IsEnabled || _offset_framelock == 0x0 || !CanPatchGame()) return false;
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

                byte[] speedFix = GameData.FindSpeedFixForRefreshRate(fps);
                float deltaTime = (1000f / fps) / 1000f;
                Debug.WriteLine("Deltatime hex: 0x" + GetHexRepresentationFromFloat(deltaTime));
                Debug.WriteLine("Speed hex: 0x" + speedFix[0].ToString("X"));
                WriteBytes(_gameAccessHwndStatic, _offset_framelock, BitConverter.GetBytes(deltaTime));
                WriteBytes(_gameAccessHwndStatic, _offset_framelock_speed_fix, speedFix);
            }
            else if (this.cbFramelock.IsChecked == false)
            {
                float deltaTime = (1000f / 60) / 1000f;
                WriteBytes(_gameAccessHwndStatic, _offset_framelock, BitConverter.GetBytes(deltaTime));
                WriteBytes(_gameAccessHwndStatic, _offset_framelock_speed_fix, GameData.PATCH_FRAMELOCK_SPEED_FIX_DISABLE);
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
                WriteBytes(_gameAccessHwndStatic, _offset_resolution_default, BitConverter.GetBytes(width));
                WriteBytes(_gameAccessHwndStatic, _offset_resolution_default + 4, BitConverter.GetBytes(height));
                WriteBytes(_gameAccessHwndStatic, _offset_resolution_scaling_fix, GameData.PATCH_RESOLUTION_SCALING_FIX_ENABLE);
            }
            else if (this.cbAddResolution.IsChecked == false)
            {
                WriteBytes(_gameAccessHwndStatic, _offset_resolution_default, !_use_resolution_720 ? GameData.PATCH_RESOLUTION_DEFAULT_DISABLE : GameData.PATCH_RESOLUTION_DEFAULT_DISABLE_720);
                WriteBytes(_gameAccessHwndStatic, _offset_resolution_scaling_fix, GameData.PATCH_RESOLUTION_SCALING_FIX_DISABLE);
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
            if (!this.cbFov.IsEnabled || _offset_fovsetting == 0x0 || !CanPatchGame()) return false;
            if (this.cbFov.IsChecked == true)
            {
                byte[] fovByte = ((KeyValuePair<byte[], string>)this.cbSelectFov.SelectedItem).Key;
                WriteBytes(_gameAccessHwndStatic, _offset_fovsetting, fovByte);
            }
            else if (this.cbFov.IsChecked == false)
            {
                WriteBytes(_gameAccessHwndStatic, _offset_fovsetting, GameData.PATCH_FOVSETTING_DISABLE);
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
                else
                {
                    if (!IsBorderless(_gameHwnd))
                        GetWindowRect(_gameHwnd, out _windowRect);
                    int width = Read<Int32>(_gameAccessHwnd, _offset_resolution);
                    int height = Read<Int32>(_gameAccessHwnd, _offset_resolution + 4);
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
                    timeScale = 0.00001f;
                WriteBytes(_gameAccessHwndStatic, _offset_timescale, BitConverter.GetBytes(timeScale));
            }
            else if (this.cbGameSpeed.IsChecked == false)
            {
                WriteBytes(_gameAccessHwndStatic, _offset_timescale, BitConverter.GetBytes(1.0f));
                if (showStatus) UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game unpatched!", Brushes.White);
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
                if (_offset_timescale_player_pointer_start > 0x0) ReadIngameOffsets();
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
                    timeScalePlayer = 0.00001f;
                WriteBytes(_gameAccessHwndStatic, _offset_timescale_player, BitConverter.GetBytes(timeScalePlayer));
                if (!_dispatcherTimerFreezeMem.IsEnabled) _dispatcherTimerFreezeMem.Start();
            }
            else if (this.cbPlayerSpeed.IsChecked == false)
            {
                WriteBytes(_gameAccessHwndStatic, _offset_timescale_player, BitConverter.GetBytes(1.0f));
                if (showStatus) UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game unpatched!", Brushes.White);
                _dispatcherTimerFreezeMem.Stop();
                return false;
            }

            if (showStatus) UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game patched!", Brushes.Green);
            return true;
        }

        /// <summary>
        /// Patch up this broken port of a game
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
                PatchGameSpeed(false),
                PatchPlayerSpeed(false)
            };
            if (results.IndexOf(true) > -1)
                UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game patched!", Brushes.Green);
            else
                UpdateStatus(DateTime.Now.ToString("HH:mm:ss") + " Game unpatched!", Brushes.White);
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
            if (_offset_timescale_player_pointer_start > 0x0) ReadIngameOffsets();
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
        private void StatReadTimer(object sender, EventArgs e)
		{
			if (_gameAccessHwndStatic == IntPtr.Zero || _offset_player_deaths == 0x0 || _offset_total_kills == 0x0) return;
            int playerDeaths = Read<Int32>(_gameAccessHwndStatic, _offset_player_deaths);
            _statusViewModel.Deaths = playerDeaths;
            if (_statLoggingEnabled) LogStatsFile(_deathCounterPath, playerDeaths.ToString());
            int totalKills = Read<Int32>(_gameAccessHwndStatic, _offset_total_kills);
            totalKills -= playerDeaths; // Since this value seems to track every death, including the player
            _statusViewModel.Kills = totalKills;
            if (_statLoggingEnabled) LogStatsFile(_killCounterPath, totalKills.ToString());
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
        /// Gets the static offset to the referenced object instead of the offset from the instruction.
        /// </summary>
        /// <param name="hProcess">Handle to the process.</param>
        /// <param name="lpInstructionAddress">The address of the instruction.</param>
        /// <param name="instructionLength">The length of the instruction including the 4 bytes offset.</param>
        /// <remarks>Static pointers in x86-64 are relative offsets from their instruction address.</remarks>
        /// <returns>The static offset from the process to the referenced object.</returns>
        private static Int64 DereferenceStaticX64Pointer(IntPtr hProcess, Int64 lpInstructionAddress, int instructionLength)
        {
            return lpInstructionAddress + Read<Int32>(hProcess, lpInstructionAddress + (instructionLength -0x04)) + instructionLength;
        }

        /// <summary>
        /// Check whether input is numeric only.
        /// </summary>
        /// <param name="text">The text to check.</param>
        /// <returns>True if inout is numeric only.</returns>
        private static bool IsNumericInput(string text)
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

        private void CbSelectFov_DropDownClosed(object sender, EventArgs e)
        {
            if (this.cbFov.IsChecked == true) PatchFov();
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

        private void CbBorderless_Checked(object sender, RoutedEventArgs e)
        {
            this.cbBorderlessStretch.IsEnabled = true;
            PatchWindow();
        }

        private void CbBorderless_Unchecked(object sender, RoutedEventArgs e)
        {
            this.cbBorderlessStretch.IsEnabled = false;
            this.cbBorderlessStretch.IsChecked = false;
            PatchWindow();
        }

        private void CbBorderlessStretch_Check_Handler(object sender, RoutedEventArgs e)
        {
            PatchWindow();
        }

        private void CbStatChanged(object sender, RoutedEventArgs e)
        {
            _statLoggingEnabled = cbLogStats.IsChecked == true;
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
            int gameSpeed = -1;
            Int32.TryParse(this.tbGameSpeed.Text, out gameSpeed);
            if (gameSpeed > -1 && gameSpeed > 4)
            {
                this.tbGameSpeed.Text = (gameSpeed - 5).ToString();
                if (cbGameSpeed.IsChecked == true) PatchGameSpeed();
            }
        }

        private void BGsHigher_Click(object sender, RoutedEventArgs e)
        {
            int gameSpeed = -1;
            Int32.TryParse(this.tbGameSpeed.Text, out gameSpeed);
            if (gameSpeed > -1 && gameSpeed < 995)
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
            int playerSpeed = -1;
            Int32.TryParse(this.tbPlayerSpeed.Text, out playerSpeed);
            if (playerSpeed > -1 && playerSpeed > 4)
            {
                this.tbPlayerSpeed.Text = (playerSpeed - 5).ToString();
                if (this.cbPlayerSpeed.IsChecked == true) PatchPlayerSpeed();
            }
        }

        private void BPsHigher_Click(object sender, RoutedEventArgs e)
        {
            int playerSpeed = -1;
            Int32.TryParse(this.tbPlayerSpeed.Text, out playerSpeed);
            if (playerSpeed > -1 && playerSpeed < 995)
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
        private const int GWL_STYLE         = -16;
        private const int GWL_EXSTYLE       = -20;
        private const uint WS_GROUP         = 0x00020000;
        private const uint WS_MINIMIZEBOX   = 0x00020000;
        private const uint WS_SYSMENU       = 0x00080000;
        private const uint WS_DLGFRAME      = 0x00400000;
        private const uint WS_BORDER        = 0x00800000;
        private const uint WS_CAPTION       = 0x00C00000;
        private const uint WS_CLIPSIBLINGS  = 0x04000000;
        private const uint WS_VISIBLE       = 0x10000000;
        private const uint WS_MINIMIZE      = 0x20000000;
        private const uint WS_POPUP         = 0x80000000;
        private const uint WS_EX_TOPMOST    = 0x00000008;
        private const int HWND_TOP          = 0;
        private const int HWND_NOTOPMOST    = -2;
        private const uint SWP_NOSIZE       = 0x0001;
        private const uint SWP_NOACTIVATE   = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_SHOWWINDOW   = 0x0040;

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
