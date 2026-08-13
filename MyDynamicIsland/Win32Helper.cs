using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace MyDynamicIsland
{
    public static class Win32Helper
    {
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, [In, Out] MONITORINFO lpmi);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MONITORINFO
        {
            public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        public static Rect GetCurrentScreenWorkAreaDip(Window window)
        {
            IntPtr windowHandle = new WindowInteropHelper(window).Handle;
            IntPtr monitorHandle = MonitorFromWindow(windowHandle, MONITOR_DEFAULTTONEAREST);

            MONITORINFO monitorInfo = new MONITORINFO();
            if (GetMonitorInfo(monitorHandle, monitorInfo))
            {
                DpiScale dpi = VisualTreeHelper.GetDpi(window);

                double left = monitorInfo.rcWork.Left / dpi.DpiScaleX;
                double top = monitorInfo.rcWork.Top / dpi.DpiScaleY;
                double width = (monitorInfo.rcWork.Right - monitorInfo.rcWork.Left) / dpi.DpiScaleX;
                double height = (monitorInfo.rcWork.Bottom - monitorInfo.rcWork.Top) / dpi.DpiScaleY;

                return new Rect(left, top, width, height);
            }

            return new Rect(
                SystemParameters.WorkArea.Left,
                SystemParameters.WorkArea.Top,
                SystemParameters.WorkArea.Width,
                SystemParameters.WorkArea.Height
            );
        }

        /// <summary>
        /// Проверяет, включена ли светлая тема в настройках Windows.
        /// </summary>
        public static bool IsWindowsLightMode()
        {
            try
            {
                // Читаем параметр из реестра Windows 10 / 11
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object registryValue = key.GetValue("AppsUseLightTheme")!;
                        if (registryValue is int value)
                        {
                            return value == 1; // 1 — светлая тема, 0 — тёмная
                        }
                    }
                }
            }
            catch
            {
                // Если не удалось прочитать реестр, по умолчанию возвращаем тёмную тему
            }
            return false;
        }
    }
}