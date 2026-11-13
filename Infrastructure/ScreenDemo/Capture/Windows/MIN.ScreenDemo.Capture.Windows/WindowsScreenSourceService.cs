using System.Runtime.InteropServices;
using System.Text;
using MIN.ScreenDemo.Services.Contracts.Enums;
using MIN.ScreenDemo.Services.Contracts.Interfaces;
using MIN.ScreenDemo.Services.Contracts.Models;

namespace MIN.ScreenDemo.Capture.Windows;

/// <inheritdoc cref="IScreenSourceService"/>
public class WindowsScreenSourceService : IScreenSourceService
{
    private const int DwmwaCloaked = 14;
    private const int GwlExstyle = -20;
    private const long WsExToolwindow = 0x00000080;

    /// <inheritdoc />
    public IReadOnlyList<ScreenSourceInfo> GetSources()
    {
        var sources = new List<ScreenSourceInfo>();

        // ===== Мониторы =====
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMonitor, _, ref _, _) =>
        {
            var info = new Monitorinfo { cbSize = Marshal.SizeOf<Monitorinfo>() };

            if (GetMonitorInfo(hMonitor, ref info))
            {
                var monitorNumber = sources.Count(x => x.Kind == ScreenDemoSourceKind.Monitor) + 1;

                sources.Add(new ScreenSourceInfo(
                    $"M:{hMonitor.ToInt64()}",
                    ScreenDemoSourceKind.Monitor,
                    $"Монитор {monitorNumber} ({info.rcMonitor.Right - info.rcMonitor.Left}x{info.rcMonitor.Bottom - info.rcMonitor.Top})",
                    (info.dwFlags & MonitorinfofPrimary) != 0));
            }

            return true;
        }, IntPtr.Zero);

        // ===== Окна =====
        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd) || IsIconic(hWnd))
            {
                return true;
            }

            var titleLength = GetWindowTextLength(hWnd);

            if (titleLength == 0)
            {
                return true;
            }

            // Скрываем "cloaked" окна UWP (невидимые, но формально видимые)
            if (DwmGetWindowAttribute(hWnd, DwmwaCloaked, out var cloaked, sizeof(int)) == 0 && cloaked != 0)
            {
                return true;
            }

            // Скрываем служебные tool-окна
            var exStyle = GetWindowLongPtr(hWnd, GwlExstyle).ToInt64();

            if ((exStyle & WsExToolwindow) != 0)
            {
                return true;
            }

            var titleBuilder = new StringBuilder(titleLength + 1);
            _ = GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);

            sources.Add(new ScreenSourceInfo(
                $"W:{hWnd.ToInt64()}",
                ScreenDemoSourceKind.Window,
                titleBuilder.ToString(),
                false));

            return true;
        }, IntPtr.Zero);

        return sources;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData);
    private delegate bool WindowEnumProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Monitorinfo
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public int dwFlags;
    }

    private const int MonitorinfofPrimary = 1;

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref Monitorinfo lpmi);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(WindowEnumProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int pvAttribute, int cbAttribute);
}
