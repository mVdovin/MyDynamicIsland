using System;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace MyDynamicIsland.Modules
{
    public class CpuMonitorEngine : IDisposable
    {
        private PerformanceCounter? _netCpuLoadCounter;
        private ManagementObjectSearcher? _wmiSearcher;

        #region Импорт Win32 API (kernel32.dll)
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
            public ulong ToUInt64() => ((ulong)dwHighDateTime << 32) + dwLowDateTime;
        }

        private ulong _prevIdleTime;
        private ulong _prevKernelTime;
        private ulong _prevUserTime;
        private bool _isFirstCpuRun = true;
        #endregion

        public CpuMonitorEngine()
        {
            try
            {
                _netCpuLoadCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
                _netCpuLoadCounter.NextValue();
            }
            catch { }

            try
            {
                // Настраиваем быстрый WMI-поисковик для получения реальной скорости процессора
                _wmiSearcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT CurrentClockSpeed, MaxClockSpeed FROM Win32_Processor");
            }
            catch
            {
                _wmiSearcher = null;
            }
        }

        public int GetCpuUsagePercent()
        {
            try
            {
                if (_netCpuLoadCounter != null)
                {
                    return (int)Math.Round(_netCpuLoadCounter.NextValue());
                }
            }
            catch { }

            if (!GetSystemTimes(out FILETIME idleTime, out FILETIME kernelTime, out FILETIME userTime))
                return 0;

            ulong idle = idleTime.ToUInt64();
            ulong kernel = kernelTime.ToUInt64();
            ulong user = userTime.ToUInt64();

            if (_isFirstCpuRun)
            {
                _prevIdleTime = idle; _prevKernelTime = kernel; _prevUserTime = user;
                _isFirstCpuRun = false;
                return 0;
            }

            ulong idleDiff = idle - _prevIdleTime;
            ulong kernelDiff = kernel - _prevKernelTime;
            ulong userDiff = user - _prevUserTime;

            _prevIdleTime = idle; _prevKernelTime = kernel; _prevUserTime = user;
            ulong totalSystemTime = kernelDiff + userDiff;
            if (totalSystemTime == 0) return 0;

            ulong cpuUsage = ((totalSystemTime - idleDiff) * 100) / totalSystemTime;
            return Math.Min(100, Math.Max(0, (int)cpuUsage));
        }

        /// <summary>
        /// Получает актуальную частоту процессора через WMI Win32_Processor
        /// </summary>
        public double GetCpuFrequencyGhz()
        {
            try
            {
                if (_wmiSearcher != null)
                {
                    using var collection = _wmiSearcher.Get();
                    foreach (ManagementObject obj in collection)
                    {
                        if (obj["CurrentClockSpeed"] is uint mhz && mhz > 0)
                        {
                            return Math.Round(mhz / 1000.0, 2);
                        }
                        if (obj["CurrentClockSpeed"] is ulong mhzLong && mhzLong > 0)
                        {
                            return Math.Round(mhzLong / 1000.0, 2);
                        }
                    }
                }
            }
            catch
            {
                // Переинициализация при сбое
                try
                {
                    _wmiSearcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT CurrentClockSpeed FROM Win32_Processor");
                }
                catch { }
            }

            return 0.0;
        }

        public void Dispose()
        {
            try
            {
                _netCpuLoadCounter?.Dispose();
                _wmiSearcher?.Dispose();
            }
            catch { }
        }
    }
}