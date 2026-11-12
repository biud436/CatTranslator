using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CatTranslator.Platform;

internal static class ScreenHelper
{
    private const uint MonitorDefaultToNearest = 2;

    /// <summary>Work area (excluding the taskbar) of the monitor the window is on, in WPF units.</summary>
    public static Rect GetWorkArea(Window window)
    {
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        var source = PresentationSource.FromVisual(window);
        if (hwnd == IntPtr.Zero || source?.CompositionTarget is null)
        {
            return SystemParameters.WorkArea;
        }

        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(hwnd, MonitorDefaultToNearest), ref info))
        {
            return SystemParameters.WorkArea;
        }

        var fromDevice = source.CompositionTarget.TransformFromDevice;
        return new Rect(
            fromDevice.Transform(new Point(info.Work.Left, info.Work.Top)),
            fromDevice.Transform(new Point(info.Work.Right, info.Work.Bottom)));
    }

    /// <summary>True if the point lies anywhere on the virtual desktop (all monitors).</summary>
    public static bool IsOnVirtualScreen(Point p) =>
        p.X >= SystemParameters.VirtualScreenLeft &&
        p.Y >= SystemParameters.VirtualScreenTop &&
        p.X <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth &&
        p.Y <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
