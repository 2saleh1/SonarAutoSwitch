using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace SonarAutoSwitch
{
    public class SonarAppContext : ApplicationContext
    {
        private AppConfig _config;
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private SettingsForm _settingsForm;
        private Thread _workerThread;
        private Thread _ipcThread;
        private volatile bool _isRunning = true;
        private string _currentTargetId = null;
        private int _cachedPort = -1;

        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        private Control _invoker;

        public SonarAppContext(AppConfig config, bool startSilent)
        {
            _config = config;

            _invoker = new Control();
            IntPtr forcedHandle = _invoker.Handle; // Guarantees handle creation on STA UI thread

            InitializeTray();
            StartIpcListener();
            StartAudioWorker();

            if (!startSilent)
            {
                ShowSettingsWindow();
            }
        }

        private void InitializeTray()
        {
            try
            {
                _trayMenu = new ContextMenuStrip();

                var itemTitle = new ToolStripMenuItem("🎧 Sonar Auto Switcher")
                {
                    Enabled = false,
                    Font = new Font("Segoe UI", 9f, FontStyle.Bold)
                };
                _trayMenu.Items.Add(itemTitle);
                _trayMenu.Items.Add(new ToolStripSeparator());

                var itemSettings = new ToolStripMenuItem("⚙️ Settings (الإعدادات)...", null, (s, e) => ShowSettingsWindow());
                itemSettings.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                _trayMenu.Items.Add(itemSettings);

                var itemSwitch = new ToolStripMenuItem("🔄 Switch Now (تحويل فوري)", null, (s, e) => ForceSwitch());
                _trayMenu.Items.Add(itemSwitch);

                var itemLogs = new ToolStripMenuItem("📄 View Logs (سجل العمليات)...", null, (s, e) => OpenLogFile());
                _trayMenu.Items.Add(itemLogs);

                _trayMenu.Items.Add(new ToolStripSeparator());

                var itemExit = new ToolStripMenuItem("❌ Exit (إغلاق نهائي)", null, (s, e) => ExitApplication());
                _trayMenu.Items.Add(itemExit);

                Icon appIcon = null;
                try
                {
                    appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }
                catch { }

                if (appIcon == null)
                {
                    appIcon = SystemIcons.Application;
                }

                _trayIcon = new NotifyIcon
                {
                    Text = "Sonar Auto Switcher",
                    ContextMenuStrip = _trayMenu,
                    Icon = appIcon,
                    Visible = true
                };

                _trayIcon.DoubleClick += (s, e) => ShowSettingsWindow();
            }
            catch (Exception ex)
            {
                SonarClient.Log("Error initializing tray icon: " + ex.Message);
            }
        }

        public void ShowSettingsWindow()
        {
            if (_invoker != null && _invoker.InvokeRequired)
            {
                _invoker.BeginInvoke(new Action(ShowSettingsWindow));
                return;
            }

            try
            {
                SonarClient.Log("Opening/restoring SettingsForm on PrimaryScreen");
                if (_settingsForm == null || _settingsForm.IsDisposed)
                {
                    _settingsForm = new SettingsForm(_config, this);
                }

                _settingsForm.BringToFrontOfPrimaryScreen();
            }
            catch (Exception ex)
            {
                SonarClient.Log("Error showing settings window: " + ex.Message);
            }
        }

        public void NotifyConfigUpdated(AppConfig newConfig)
        {
            _config = newConfig;
            _currentTargetId = null; // force immediate re-evaluation
        }

        public void ShowNotification(string title, string message, ToolTipIcon icon)
        {
            try
            {
                if (_trayIcon != null && _config.ShowNotifications)
                {
                    _trayIcon.ShowBalloonTip(3000, title, message, icon);
                }
            }
            catch { }
        }

        private void StartIpcListener()
        {
            _ipcThread = new Thread(() =>
            {
                EventWaitHandle evt = new EventWaitHandle(false, EventResetMode.AutoReset, Program.EventName);
                while (_isRunning)
                {
                    if (evt.WaitOne(250))
                    {
                        if (_isRunning)
                        {
                            try
                            {
                                ShowSettingsWindow();
                            }
                            catch (Exception ex)
                            {
                                SonarClient.Log("Error invoking ShowSettingsWindow from IPC: " + ex.Message);
                            }
                        }
                    }
                }
            })
            {
                IsBackground = true,
                Name = "IpcListenerThread"
            };
            _ipcThread.Start();
        }

        private void StartAudioWorker()
        {
            _workerThread = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "SonarAudioWorker"
            };
            _workerThread.Start();
        }

        private void WorkerLoop()
        {
            int verifyCounter = 0;

            while (_isRunning)
            {
                try
                {
                    EvaluateAndSwitch();

                    verifyCounter++;
                    if (verifyCounter >= 10) // Every ~15 seconds audit
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
                                _currentTargetId = null;
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

        private void EvaluateAndSwitch()
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

            if (desiredDevice == null) return;

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

        public void ForceSwitch()
        {
            _currentTargetId = null;
            SonarClient.Log("Manual switch requested by user.");
            ThreadPool.QueueUserWorkItem((state) =>
            {
                EvaluateAndSwitch();
            });
        }

        public void ExitApplication()
        {
            _isRunning = false;
            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
            }
            if (_settingsForm != null && !_settingsForm.IsDisposed)
            {
                _settingsForm.Close();
            }
            ExitThread();
        }

        private void OpenLogFile()
        {
            try
            {
                if (File.Exists(SonarClient.LogPath))
                {
                    Process.Start("notepad.exe", SonarClient.LogPath);
                }
                else
                {
                    MessageBox.Show("لا يوجد سجل حتى الآن.", "SonarAutoSwitch", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("خطأ في فتح السجل: " + ex.Message);
            }
        }
    }
}
