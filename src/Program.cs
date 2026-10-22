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
        private static SettingsForm _openSettingsForm = null;
        private static HiddenAnchorForm _hiddenForm = null;

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
                    // Already running: signal the background instance to show its settings window
                    try
                    {
                        using (var evt = EventWaitHandle.OpenExisting(EventName))
                        {
                            evt.Set();
                        }
                    }
                    catch { }
                    return;
                }

                _config = AppConfig.Load();
                SonarClient.Log("=== SonarAutoSwitch Service Started ===");

                // Ensure initial startup shortcut is set if enabled
                if (_config.AutoStartWithWindows)
                {
                    try
                    {
                        string startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                        string lnkPath = Path.Combine(startupPath, "SonarAutoSwitch.lnk");
                        if (!File.Exists(lnkPath))
                        {
                            SettingsForm.CreateShortcut(lnkPath, Application.ExecutablePath);
                        }
                    }
                    catch { }
                }

                _hiddenForm = new HiddenAnchorForm();

                InitializeTray();

                // Setup IPC listener to show settings when user runs the exe again
                EventWaitHandle showSettingsEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
                Thread ipcThread = new Thread(() =>
                {
                    while (_isRunning)
                    {
                        if (showSettingsEvent.WaitOne(500))
                        {
                            if (_isRunning && _hiddenForm != null && _hiddenForm.IsHandleCreated)
                            {
                                _hiddenForm.BeginInvoke(new Action(ShowSettings));
                            }
                        }
                    }
                })
                {
                    IsBackground = true,
                    Name = "SettingsIpcListener"
                };
                ipcThread.Start();

                // Start background worker for audio monitoring
                _workerThread = new Thread(WorkerLoop)
                {
                    IsBackground = true,
                    Name = "SonarMonitorWorker"
                };
                _workerThread.Start();

                // If user passed --settings argument, show settings immediately
                if (args.Length > 0 && args[0].ToLowerInvariant().Contains("setting"))
                {
                    _hiddenForm.Load += (s, e) => ShowSettings();
                }

                Application.Run(_hiddenForm);

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

            var itemExit = new ToolStripMenuItem("❌ Exit (خروج)", null, (s, e) => ExitApp());
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
            using (Bitmap bmp = new Bitmap(32, 32))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                // Background Circle (Dark Teal/Steel)
                using (Brush b = new SolidBrush(Color.FromArgb(24, 24, 37)))
                {
                    g.FillEllipse(b, 1, 1, 29, 29);
                }
                using (Pen p = new Pen(Color.FromArgb(137, 180, 250), 2.0f))
                {
                    g.DrawEllipse(p, 1, 1, 29, 29);
                }

                // Headphone headband arch
                using (Pen archPen = new Pen(Color.FromArgb(166, 227, 161), 2.5f))
                {
                    g.DrawArc(archPen, 7, 7, 17, 15, 180, 180);
                }

                // Left and right ear cups
                using (Brush cupBrush = new SolidBrush(Color.FromArgb(137, 180, 250)))
                {
                    g.FillRectangle(cupBrush, 6, 14, 4, 8);
                    g.FillRectangle(cupBrush, 21, 14, 4, 8);
                }

                IntPtr hIcon = bmp.GetHicon();
                return Icon.FromHandle(hIcon);
            }
        }

        public static void ShowSettings()
        {
            try
            {
                if (_openSettingsForm != null && !_openSettingsForm.IsDisposed)
                {
                    if (_openSettingsForm.WindowState == FormWindowState.Minimized)
                    {
                        _openSettingsForm.WindowState = FormWindowState.Normal;
                    }
                    _openSettingsForm.BringToFront();
                    _openSettingsForm.Activate();
                    _openSettingsForm.TopMost = true;
                    _openSettingsForm.TopMost = false;
                    return;
                }

                _openSettingsForm = new SettingsForm(_config, () =>
                {
                    _config = AppConfig.Load();
                    _currentTargetId = null; // Forces re-evaluation
                });

                _openSettingsForm.Show();
                _openSettingsForm.BringToFront();
                _openSettingsForm.Activate();
                _openSettingsForm.TopMost = true;
                _openSettingsForm.TopMost = false;
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
            if (_hiddenForm != null && !_hiddenForm.IsDisposed)
            {
                _hiddenForm.Close();
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

    internal class HiddenAnchorForm : Form
    {
        public HiddenAnchorForm()
        {
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(0, 0);
            this.Opacity = 0;
        }
    }
}
