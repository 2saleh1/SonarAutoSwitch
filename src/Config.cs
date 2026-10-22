using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SonarAutoSwitch
{
    public class AppConfig
    {
        public List<string> PriorityDevices { get; set; }
        public string DefaultDevice { get; set; }
        public bool ShowNotifications { get; set; }
        public bool AutoStartWithWindows { get; set; }
        public List<string> Channels { get; set; }
        public bool IncludeMonitoring { get; set; }

        public AppConfig()
        {
            PriorityDevices = new List<string> { "TOZO" };
            DefaultDevice = "HyperX";
            ShowNotifications = true;
            AutoStartWithWindows = true;
            Channels = new List<string> { "game", "chat", "media", "aux" };
            IncludeMonitoring = true;
        }

        public static string GetConfigPath()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SonarAutoSwitch");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return Path.Combine(dir, "config.json");
        }

        public static AppConfig Load()
        {
            string path = GetConfigPath();
            if (!File.Exists(path))
            {
                var cfg = new AppConfig();
                cfg.Save();
                return cfg;
            }

            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                var cfg = new AppConfig();
                cfg.PriorityDevices = ParseStringList(json, "priorityDevices", new List<string> { "TOZO" });
                cfg.DefaultDevice = ParseString(json, "defaultDevice", "HyperX");
                cfg.ShowNotifications = ParseBool(json, "showNotifications", true);
                cfg.AutoStartWithWindows = ParseBool(json, "autoStartWithWindows", true);
                cfg.Channels = ParseStringList(json, "channels", new List<string> { "game", "chat", "media", "aux" });
                cfg.IncludeMonitoring = ParseBool(json, "includeMonitoring", true);
                return cfg;
            }
            catch
            {
                var cfg = new AppConfig();
                return cfg;
            }
        }

        public void Save()
        {
            try
            {
                string path = GetConfigPath();
                var sb = new StringBuilder();
                sb.AppendLine("{");

                sb.AppendLine("  \"priorityDevices\": [");
                for (int i = 0; i < PriorityDevices.Count; i++)
                {
                    string comma = (i < PriorityDevices.Count - 1) ? "," : "";
                    sb.AppendFormat("    \"{0}\"{1}\r\n", EscapeJson(PriorityDevices[i]), comma);
                }
                sb.AppendLine("  ],");

                sb.AppendFormat("  \"defaultDevice\": \"{0}\",\r\n", EscapeJson(DefaultDevice ?? ""));
                sb.AppendFormat("  \"showNotifications\": {0},\r\n", ShowNotifications ? "true" : "false");
                sb.AppendFormat("  \"autoStartWithWindows\": {0},\r\n", AutoStartWithWindows ? "true" : "false");

                sb.AppendLine("  \"channels\": [");
                for (int i = 0; i < Channels.Count; i++)
                {
                    string comma = (i < Channels.Count - 1) ? "," : "";
                    sb.AppendFormat("    \"{0}\"{1}\r\n", EscapeJson(Channels[i]), comma);
                }
                sb.AppendLine("  ],");

                sb.AppendFormat("  \"includeMonitoring\": {0}\r\n", IncludeMonitoring ? "true" : "false");
                sb.AppendLine("}");

                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string ParseString(string json, string key, string fallback)
        {
            int idx = json.IndexOf("\"" + key + "\"");
            if (idx == -1) return fallback;
            int colon = json.IndexOf(':', idx);
            if (colon == -1) return fallback;
            int q1 = json.IndexOf('"', colon);
            if (q1 == -1) return fallback;
            int q2 = json.IndexOf('"', q1 + 1);
            if (q2 == -1) return fallback;
            return json.Substring(q1 + 1, q2 - q1 - 1).Trim();
        }

        private static bool ParseBool(string json, string key, bool fallback)
        {
            int idx = json.IndexOf("\"" + key + "\"");
            if (idx == -1) return fallback;
            int colon = json.IndexOf(':', idx);
            if (colon == -1) return fallback;
            string sub = json.Substring(colon + 1);
            if (sub.TrimStart().StartsWith("true", StringComparison.OrdinalIgnoreCase)) return true;
            if (sub.TrimStart().StartsWith("false", StringComparison.OrdinalIgnoreCase)) return false;
            return fallback;
        }

        private static List<string> ParseStringList(string json, string key, List<string> fallback)
        {
            int idx = json.IndexOf("\"" + key + "\"");
            if (idx == -1) return fallback;
            int bracket1 = json.IndexOf('[', idx);
            if (bracket1 == -1) return fallback;
            int bracket2 = json.IndexOf(']', bracket1);
            if (bracket2 == -1) return fallback;

            string content = json.Substring(bracket1 + 1, bracket2 - bracket1 - 1);
            var list = new List<string>();
            int cur = 0;
            while (cur < content.Length)
            {
                int q1 = content.IndexOf('"', cur);
                if (q1 == -1) break;
                int q2 = content.IndexOf('"', q1 + 1);
                if (q2 == -1) break;
                string item = content.Substring(q1 + 1, q2 - q1 - 1).Trim();
                if (!string.IsNullOrEmpty(item))
                {
                    list.Add(item);
                }
                cur = q2 + 1;
            }
            return list.Count > 0 ? list : fallback;
        }
    }
}
