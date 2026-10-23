using System;
using System.Collections.Generic;
using Microsoft.Win32;

namespace SonarAutoSwitch
{
    public class AudioDeviceInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int State { get; set; } // 1 = Active

        public bool IsActive
        {
            get { return State == 1; }
        }

        public string DisplayName
        {
            get
            {
                if (string.IsNullOrEmpty(Description) || Description == Name)
                {
                    return Name ?? "Unknown Device";
                }
                return string.Format("{0} ({1})", Name, Description);
            }
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    public static class AudioHelper
    {
        private const string RenderKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render";

        public static List<AudioDeviceInfo> GetAllRenderDevices()
        {
            var list = new List<AudioDeviceInfo>();

            try
            {
                using (RegistryKey baseKey = Registry.LocalMachine.OpenSubKey(RenderKeyPath))
                {
                    if (baseKey == null) return list;

                    string[] subKeys = baseKey.GetSubKeyNames();
                    foreach (string guid in subKeys)
                    {
                        try
                        {
                            using (RegistryKey devKey = baseKey.OpenSubKey(guid))
                            {
                                if (devKey == null) continue;

                                int state = 0;
                                object stVal = devKey.GetValue("DeviceState");
                                if (stVal is int) state = (int)stVal;

                                string name = null;
                                string desc = null;

                                using (RegistryKey propKey = devKey.OpenSubKey("Properties"))
                                {
                                    if (propKey != null)
                                    {
                                        object n = propKey.GetValue("{a45c254e-df1c-4efd-8020-67d146a850e0},2");
                                        if (n != null) name = n.ToString();

                                        object d = propKey.GetValue("{b3f8fa53-0004-438e-9003-51a46e139bfc},6");
                                        if (d != null) desc = d.ToString();
                                    }
                                }

                                if (!string.IsNullOrEmpty(name))
                                {
                                    list.Add(new AudioDeviceInfo
                                    {
                                        Id = "{0.0.0.00000000}." + guid,
                                        Name = name,
                                        Description = desc ?? "",
                                        State = state
                                    });
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return list;
        }

        public static List<AudioDeviceInfo> GetActiveRenderDevices()
        {
            var all = GetAllRenderDevices();
            var active = new List<AudioDeviceInfo>();
            foreach (var d in all)
            {
                if (d.IsActive) active.Add(d);
            }
            return active;
        }

        public static AudioDeviceInfo FindMatchingActiveDevice(string keyword)
        {
            if (string.IsNullOrEmpty(keyword)) return null;

            var active = GetActiveRenderDevices();
            string lowerKw = keyword.Trim().ToLowerInvariant();

            // 1. Exact or partial match on Friendly Name or Description (preferring Stereo, strictly excluding Hands-Free)
            foreach (var d in active)
            {
                string n = (d.Name ?? "").ToLowerInvariant();
                string desc = (d.Description ?? "").ToLowerInvariant();

                bool isHandsFree = n.Contains("hands-free") || n.Contains("handsfree") ||
                                   desc.Contains("hands-free") || desc.Contains("handsfree") ||
                                   n.Contains("hf audio") || desc.Contains("hf audio");

                if (!isHandsFree && (n.Contains(lowerKw) || desc.Contains(lowerKw)))
                {
                    return d;
                }
            }

            // 2. Exact match on ID if keyword was a GUID (also excluding Hands-Free)
            foreach (var d in active)
            {
                string n = (d.Name ?? "").ToLowerInvariant();
                string desc = (d.Description ?? "").ToLowerInvariant();

                bool isHandsFree = n.Contains("hands-free") || n.Contains("handsfree") ||
                                   desc.Contains("hands-free") || desc.Contains("handsfree") ||
                                   n.Contains("hf audio") || desc.Contains("hf audio");

                if (!isHandsFree && string.Equals(d.Id, keyword, StringComparison.OrdinalIgnoreCase))
                {
                    return d;
                }
            }

            // Hands-Free telephony devices are intentionally excluded to prevent low-fidelity mono audio
            // and transient bluetooth disconnect race conditions.
            return null;
        }
    }
}
