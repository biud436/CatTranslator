using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CatTranslator.Platform;

/// <summary>
/// Detects "Ctrl+C pressed twice quickly" from clipboard change notifications
/// (AddClipboardFormatListener), without a keyboard hook and without reading
/// the clipboard on every copy.
/// </summary>
public sealed class ClipboardWatcher : IDisposable
{
    private const int WmClipboardUpdate = 0x031D;
    private const int VkControl = 0x11;

    // Some apps raise several updates for one copy; updates closer than this count as one.
    private const long BurstMs = 50;
    // Maximum gap between the two copies.
    private const long DoubleCopyMs = 700;

    private readonly HwndSource _source;
    private readonly bool _listening;
    private long _lastUpdate;
    private long _firstCopy;

    public ClipboardWatcher(Window window)
    {
        IntPtr handle = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(handle)
                  ?? throw new InvalidOperationException("Window handle is not available.");
        _source.AddHook(WndProc);
        _listening = AddClipboardFormatListener(handle);
    }

    public event Action? DoubleCopy;

    public void Dispose()
    {
        if (_listening)
        {
            RemoveClipboardFormatListener(_source.Handle);
        }
        _source.RemoveHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmClipboardUpdate)
        {
            return IntPtr.Zero;
        }

        long now = Environment.TickCount64;
        bool sameBurst = now - _lastUpdate < BurstMs;
        _lastUpdate = now;
        if (sameBurst)
        {
            return IntPtr.Zero;
        }

        // Ctrl still held on the second copy separates a deliberate Ctrl+C, C from
        // programs that happen to write the clipboard twice.
        if (_firstCopy != 0 && now - _firstCopy <= DoubleCopyMs && IsCtrlDown())
        {
            _firstCopy = 0;
            // Leave the message handler before touching the clipboard.
            _source.Dispatcher.BeginInvoke(() => DoubleCopy?.Invoke());
        }
        else
        {
            _firstCopy = now;
        }
        return IntPtr.Zero;
    }

    private static bool IsCtrlDown() => (GetAsyncKeyState(VkControl) & 0x8000) != 0;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}
