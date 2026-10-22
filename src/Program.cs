using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace SonarAutoSwitch
{
    static class Program
    {
        private const string MutexName = "Local\\SonarAutoSwitch_SingleInstance_Mutex";
        private const string EventName = "Local\\SonarAutoSwitch_ShowSettings_Event";

        private static NotifyIcon _trayIcon;
        private static ContextMenuStrip _trayMenu;
        private static AppConfig _config;
        private static Thread _workerThread;
        private static volatile bool _isRunning = true;
        private static string _currentTargetId = null;
        private static int _cachedPort = -1;
        private static SettingsForm _mainForm = null;

        [STAThread]
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                SonarClient.Log("Unhandled exception: " + e.ExceptionObject);
            };
            Application.ThreadException += (s, e) =>
            {
                SonarClient.Log("Thread exception: " + e.Exception.Message);
            };

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool createdNew;
            using (Mutex mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    // A background instance is already running.
                    // Open SettingsForm directly in this process so the user immediately gets the window!
                    AppConfig cfg = AppConfig.Load();
                    var settingsDialog = new SettingsForm(cfg, null);
                    settingsDialog.AllowRealExit = true;
                    Application.Run(settingsDialog);
                    return;
                }

                _config = AppConfig.Load();
                SonarClient.Log("=== SonarAutoSwitch Service Started ===");
                SonarClient.Log("Step A: Config loaded");

                // Ensure initial startup shortcut is set if enabled
                if (_config.AutoStartWithWindows)
                {
                    try
                    {
                        string startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                        string lnkPath = Path.Combine(startupPath, "SonarAutoSwitch.lnk");
                        if (!File.Exists(lnkPath))
                        {
                            SettingsForm.CreateShortcut(lnkPath, Application.ExecutablePath, "--silent");
                        }
                    }
                    catch (Exception ex)
                    {
                        SonarClient.Log("Error in startup shortcut: " + ex.Message);
                    }
                }
                SonarClient.Log("Step B: Startup check passed");

                _mainForm = new SettingsForm(_config, () =>
                {
                    _config = AppConfig.Load();
                    _currentTargetId = null; // Forces re-evaluation
                });
                SonarClient.Log("Step C: SettingsForm created");

                InitializeTray();
                SonarClient.Log("Step D: InitializeTray done");

                // Setup IPC listener to show settings when user runs the exe again
                EventWaitHandle showSettingsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
                Thread ipcThread = new Thread(() =>
                {
                    while (_isRunning)
                    {
                        if (showSettingsEvent.WaitOne(300))
                        {
                            if (_isRunning && _mainForm != null)
                            {
                                try
                                {
                                    _mainForm.BeginInvoke(new Action(ShowSettings));
                                }
                                catch { }
                            }
                        }
                    }
                })
                {
                    IsBackground = true,
                    Name = "SettingsIpcListener"
                };
                ipcThread.Start();
                SonarClient.Log("Step E: IPC thread started");

                // Start background worker for audio monitoring
                _workerThread = new Thread(WorkerLoop)
                {
                    IsBackground = true,
                    Name = "SonarMonitorWorker"
                };
                _workerThread.Start();
                SonarClient.Log("Step F: Worker thread started");

                bool silent = args.Length > 0 && args[0].ToLowerInvariant().Contains("silent");
                SonarClient.Log("Step G: silent = " + silent);
                if (silent)
                {
                    _mainForm.WindowState = FormWindowState.Minimized;
                    _mainForm.ShowInTaskbar = false;
                    _mainForm.Opacity = 0;
                    _mainForm.Load += (s, e) =>
                    {
                        _mainForm.Hide();
                        _mainForm.Opacity = 1;
                        _mainForm.ShowInTaskbar = true;
                    };
                }

                SonarClient.Log("Step H: Calling Application.Run(_mainForm)");
                Application.Run(_mainForm);
                SonarClient.Log("Step I: Application.Run finished!");

                _isRunning = false;
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                }
            }
        }

        private static void InitializeTray()
        {
            _trayMenu = new ContextMenuStrip();

            var itemTitle = new ToolStripMenuItem("🎧 Sonar Auto Switcher")
            {
                Enabled = false,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            _trayMenu.Items.Add(itemTitle);
            _trayMenu.Items.Add(new ToolStripSeparator());

            var itemSettings = new ToolStripMenuItem("⚙️ Settings (الإعدادات)...", null, (s, e) => ShowSettings());
            itemSettings.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            _trayMenu.Items.Add(itemSettings);

            var itemSwitch = new ToolStripMenuItem("🔄 Switch Now (فحص وتحويل فوري)", null, (s, e) => ForceSwitch());
            _trayMenu.Items.Add(itemSwitch);

            var itemLogs = new ToolStripMenuItem("📄 View Logs (عرض السجل)...", null, (s, e) => OpenLogs());
            _trayMenu.Items.Add(itemLogs);

            _trayMenu.Items.Add(new ToolStripSeparator());

            var itemExit = new ToolStripMenuItem("❌ Exit (خروج نهائي)", null, (s, e) => ExitApp());
            _trayMenu.Items.Add(itemExit);

            _trayIcon = new NotifyIcon
            {
                Text = "Sonar Auto Switcher",
                ContextMenuStrip = _trayMenu,
                Icon = CreateAppIcon(),
                Visible = true
            };

            _trayIcon.DoubleClick += (s, e) => ShowSettings();
        }

        private static Icon CreateAppIcon()
        {
            try
            {
                Icon exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (exeIcon != null) return exeIcon;
            }
            catch { }
            return SystemIcons.Application;
        }

        public static void ShowSettings()
        {
            try
            {
                if (_mainForm != null && !_mainForm.IsDisposed)
                {
                    _mainForm.Show();
                    if (_mainForm.WindowState == FormWindowState.Minimized)
                    {
                        _mainForm.WindowState = FormWindowState.Normal;
                    }
                    _mainForm.BringToFront();
                    _mainForm.Activate();
                    _mainForm.TopMost = true;
                    _mainForm.TopMost = false;
                }
            }
            catch (Exception ex)
            {
                SonarClient.Log("Error showing settings: " + ex.Message);
            }
        }

        private static void ForceSwitch()
        {
            _currentTargetId = null;
            SonarClient.Log("Manual switch requested by user.");
            ThreadPool.QueueUserWorkItem((state) =>
            {
                EvaluateAndSwitch();
            });
        }

        private static void OpenLogs()
        {
            try
            {
                if (File.Exists(SonarClient.LogPath))
                {
                    System.Diagnostics.Process.Start("notepad.exe", SonarClient.LogPath);
                }
            }
            catch { }
        }

        private static void ExitApp()
        {
            _isRunning = false;
            SonarClient.Log("SonarAutoSwitch closed by user.");
            if (_mainForm != null && !_mainForm.IsDisposed)
            {
                _mainForm.AllowRealExit = true;
                _mainForm.Close();
            }
            Application.Exit();
        }

        private static void WorkerLoop()
        {
            int verifyCounter = 0;

            while (_isRunning)
            {
                try
                {
                    EvaluateAndSwitch();

                    verifyCounter++;
                    if (verifyCounter >= 10) // ~15 seconds periodic audit
                    {
                        verifyCounter = 0;
                        try
                        {
                            _config = AppConfig.Load();
                        }
                        catch { }

                        if (_cachedPort > 0 && !string.IsNullOrEmpty(_currentTargetId))
                        {
                            if (!SonarClient.IsSonarMatching(_cachedPort, _currentTargetId, _config.Channels))
                            {
                                SonarClient.Log("Drift detected in Sonar output channels. Re-applying current target...");
                                _currentTargetId = null; // Re-apply
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    SonarClient.Log("Worker loop exception: " + ex.Message);
                }

                Thread.Sleep(1500);
            }
        }

        private static void EvaluateAndSwitch()
        {
            AudioDeviceInfo desiredDevice = null;

            foreach (var kw in _config.PriorityDevices)
            {
                var match = AudioHelper.FindMatchingActiveDevice(kw);
                if (match != null)
                {
                    desiredDevice = match;
                    break;
                }
            }

            if (desiredDevice == null && !string.IsNullOrEmpty(_config.DefaultDevice))
            {
                desiredDevice = AudioHelper.FindMatchingActiveDevice(_config.DefaultDevice);
            }

            if (desiredDevice == null)
            {
                return;
            }

            if (desiredDevice.Id != _currentTargetId)
            {
                if (_cachedPort <= 0)
                {
                    _cachedPort = SonarClient.GetSonarPort();
                }

                if (_cachedPort > 0)
                {
                    bool ok = SonarClient.SwitchChannels(
                        _cachedPort,
                        desiredDevice.Id,
                        desiredDevice.DisplayName,
                        _config.Channels,
                        _config.IncludeMonitoring);

                    if (!ok)
                    {
                        _cachedPort = SonarClient.GetSonarPort();
                        if (_cachedPort > 0)
                        {
                            ok = SonarClient.SwitchChannels(
                                _cachedPort,
                                desiredDevice.Id,
                                desiredDevice.DisplayName,
                                _config.Channels,
                                _config.IncludeMonitoring);
                        }
                    }

                    if (ok)
                    {
                        string oldId = _currentTargetId;
                        _currentTargetId = desiredDevice.Id;

                        if (_config.ShowNotifications && _trayIcon != null)
                        {
                            string msg = string.Format("تم تحويل مخرجات Sonar إلى:\n{0}", desiredDevice.Name);
                            _trayIcon.ShowBalloonTip(3000, "Sonar Auto Switch", msg, ToolTipIcon.Info);
                        }
                    }
                }
            }
        }
    }
}
