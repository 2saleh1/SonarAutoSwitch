using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace SonarAutoSwitch
{
    static class Program
    {
        private const string MutexName = "Local\\SonarAutoSwitch_SingleInstance_Mutex";
        private const string EventName = "Local\\SonarAutoSwitch_ShowSettings_Event";

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
                    // A background instance is already running: signal it to bring its settings window to the front!
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
