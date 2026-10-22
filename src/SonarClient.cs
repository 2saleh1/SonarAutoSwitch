using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;

namespace SonarAutoSwitch
{
    public class SonarClient
    {
        private const int AF_INET = 2; // IPv4

        private enum TCP_TABLE_CLASS
        {
            TCP_TABLE_BASIC_LISTENER,
            TCP_TABLE_BASIC_CONNECTIONS,
            TCP_TABLE_BASIC_ALL,
            TCP_TABLE_OWNER_PID_LISTENER,
            TCP_TABLE_OWNER_PID_CONNECTIONS,
            TCP_TABLE_OWNER_PID_ALL,
            TCP_TABLE_OWNER_MODULE_LISTENER,
            TCP_TABLE_OWNER_MODULE_CONNECTIONS,
            TCP_TABLE_OWNER_MODULE_ALL
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCPROW_OWNER_PID
        {
            public uint state;
            public uint localAddr;
            public byte localPort1;
            public byte localPort2;
            public byte localPort3;
            public byte localPort4;
            public uint remoteAddr;
            public byte remotePort1;
            public byte remotePort2;
            public byte remotePort3;
            public byte remotePort4;
            public int owningPid;

            public ushort LocalPort
            {
                get { return (ushort)((localPort1 << 8) | localPort2); }
            }
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(
            IntPtr pTcpTable,
            ref int pdwSize,
            bool bOrder,
            int ulAf,
            TCP_TABLE_CLASS TableClass,
            uint Reserved);

        private static string _logPath;

        public static string LogPath
        {
            get
            {
                if (_logPath == null)
                {
                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SonarAutoSwitch");
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    _logPath = Path.Combine(dir, "sonar_autoswitch.log");
                }
                return _logPath;
            }
        }

        public static void Log(string message)
        {
            try
            {
                string line = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] {1}", DateTime.Now, message);
                File.AppendAllText(LogPath, line + Environment.NewLine);
            }
            catch { }
        }

        public static int GetSonarPort()
        {
            try
            {
                Process[] procs = Process.GetProcessesByName("SteelSeriesSonar");
                if (procs.Length == 0) return -1;
                int targetPid = procs[0].Id;

                int bufferSize = 0;
                GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, false, AF_INET, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_LISTENER, 0);
                if (bufferSize <= 0) return -1;

                IntPtr tcpTablePtr = Marshal.AllocHGlobal(bufferSize);
                try
                {
                    uint ret = GetExtendedTcpTable(tcpTablePtr, ref bufferSize, false, AF_INET, TCP_TABLE_CLASS.TCP_TABLE_OWNER_PID_LISTENER, 0);
                    if (ret != 0) return -1;

                    int numEntries = Marshal.ReadInt32(tcpTablePtr);
                    IntPtr rowPtr = (IntPtr)((long)tcpTablePtr + 4);
                    int rowSize = Marshal.SizeOf(typeof(MIB_TCPROW_OWNER_PID));

                    for (int i = 0; i < numEntries; i++)
                    {
                        MIB_TCPROW_OWNER_PID row = (MIB_TCPROW_OWNER_PID)Marshal.PtrToStructure(rowPtr, typeof(MIB_TCPROW_OWNER_PID));
                        if (row.owningPid == targetPid)
                        {
                            byte[] ipBytes = BitConverter.GetBytes(row.localAddr);
                            // 127.0.0.1
                            if (ipBytes[0] == 127 && ipBytes[1] == 0 && ipBytes[2] == 0 && ipBytes[3] == 1)
                            {
                                int port = row.LocalPort;
                                if (TestSonarApi(port))
                                {
                                    return port;
                                }
                            }
                        }
                        rowPtr = (IntPtr)((long)rowPtr + rowSize);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(tcpTablePtr);
                }
            }
            catch (Exception ex)
            {
                Log("Error resolving Sonar port: " + ex.Message);
            }

            return -1;
        }

        private static bool TestSonarApi(int port)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + port + "/classicRedirections");
                req.Timeout = 800;
                req.Method = "GET";
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    return resp.StatusCode == HttpStatusCode.OK;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool SendPut(int port, string endpoint)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + port + endpoint);
                req.Timeout = 1500;
                req.Method = "PUT";
                req.ContentLength = 0;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    return resp.StatusCode == HttpStatusCode.OK;
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool IsSonarMatching(int port, string targetDeviceId, List<string> channels)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + port + "/classicRedirections");
                req.Timeout = 1000;
                req.Method = "GET";
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    if (resp.StatusCode != HttpStatusCode.OK) return false;
                    using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
                    {
                        string body = sr.ReadToEnd();
                        foreach (var ch in channels)
                        {
                            string needle = string.Format("\"id\":\"{0}\",\"deviceId\":\"{1}\"", ch, targetDeviceId);
                            if (!body.Contains(needle))
                            {
                                return false;
                            }
                        }
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        public static bool SwitchChannels(int port, string deviceId, string deviceName, List<string> channels, bool includeMonitoring)
        {
            bool allOk = true;

            foreach (var ch in channels)
            {
                bool ok = SendPut(port, "/classicRedirections/" + ch + "/deviceId/" + deviceId);
                if (!ok) allOk = false;
            }

            if (includeMonitoring)
            {
                bool monOk = SendPut(port, "/streamRedirections/monitoring/deviceId/" + deviceId);
                if (!monOk) allOk = false;
            }

            if (allOk)
            {
                Log(string.Format("Successfully switched Sonar output to: {0} ({1})", deviceName, deviceId));
            }
            else
            {
                Log(string.Format("Partial failure switching Sonar channels to: {0}", deviceName));
            }

            return allOk;
        }
    }
}
