using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace SonarAutoSwitch
{
    static class Program
    {
        private const string MutexName = "Local\\SonarAutoSwitch_SingleInstance_Mutex";
        public const string EventName = "Local\\SonarAutoSwitch_ShowSettings_Event";

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int dwProcessId);

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
                    // An instance is already running: signal it and grant foreground permission
                    try
                    {
                        Process current = Process.GetCurrentProcess();
                        foreach (Process p in Process.GetProcessesByName(current.ProcessName))
                        {
                            if (p.Id != current.Id)
                            {
                                AllowSetForegroundWindow(p.Id);
                                if (p.MainWindowHandle != IntPtr.Zero)
                                {
                                    ShowWindowAsync(p.MainWindowHandle, 9 /* SW_RESTORE */);
                                    SetForegroundWindow(p.MainWindowHandle);
                                }
                                break;
                            }
                        }

                        using (var evt = EventWaitHandle.OpenExisting(EventName))
                        {
                            evt.Set();
                        }
                    }
                    catch (Exception ex)
                    {
                        SonarClient.Log("Error signaling running instance: " + ex.Message);
                    }
                    return;
                }

                AppConfig config = AppConfig.Load();
                SonarClient.Log("=== SonarAutoSwitch Started ===");

                // Ensure initial startup shortcut is set if enabled
                if (config.AutoStartWithWindows)
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
                    catch { }
                }

                bool silent = args.Length > 0 && args[0].ToLowerInvariant().Contains("silent");
                var mainForm = new SettingsForm(config, silent);
                Application.Run(mainForm);
            }
        }
    }
}
