using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace CatTranslator.Platform;

public readonly record struct HotkeyGesture(ModifierKeys Modifiers, Key Key)
{
    public override string ToString()
    {
        var parts = new List<string>(5);
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(Key is >= Key.D0 and <= Key.D9 ? ((int)(Key - Key.D0)).ToString() : Key.ToString());
        return string.Join("+", parts);
    }

    public static bool TryParse(string? text, out HotkeyGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var modifiers = ModifierKeys.None;
        string[] tokens = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2)
        {
            return false;
        }
        foreach (string token in tokens[..^1])
        {
            switch (token.ToLowerInvariant())
            {
                case "ctrl" or "control": modifiers |= ModifierKeys.Control; break;
                case "alt": modifiers |= ModifierKeys.Alt; break;
                case "shift": modifiers |= ModifierKeys.Shift; break;
                case "win" or "windows": modifiers |= ModifierKeys.Windows; break;
                default: return false;
            }
        }

        string last = tokens[^1];
        Key key;
        if (last.Length == 1 && char.IsDigit(last[0]))
        {
            key = Key.D0 + (last[0] - '0');
        }
        else if (!Enum.TryParse(last, ignoreCase: true, out key))
        {
            return false;
        }

        gesture = new HotkeyGesture(modifiers, key);
        return true;
    }

    public static bool IsModifierKey(Key key) => key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt
        or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;
}

/// <summary>System-wide hotkey via Win32 RegisterHotKey, delivered to a WPF window's message loop.</summary>
public sealed class GlobalHotkey : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0x4354; // Arbitrary id, unique within this window.
    private const uint ModAlt = 0x1, ModControl = 0x2, ModShift = 0x4, ModWin = 0x8, ModNoRepeat = 0x4000;

    private readonly HwndSource _source;
    private bool _registered;

    public GlobalHotkey(Window window)
    {
        IntPtr handle = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(handle)
                  ?? throw new InvalidOperationException("Window handle is not available.");
        _source.AddHook(WndProc);
    }

    public event Action? Pressed;

    public bool TryRegister(HotkeyGesture gesture)
    {
        Unregister();
        uint modifiers = ModNoRepeat;
        if (gesture.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= ModAlt;
        if (gesture.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= ModControl;
        if (gesture.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= ModShift;
        if (gesture.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= ModWin;

        uint vk = (uint)KeyInterop.VirtualKeyFromKey(gesture.Key);
        _registered = vk != 0 && RegisterHotKey(_source.Handle, HotkeyId, modifiers, vk);
        return _registered;
    }

    public void Unregister()
    {
        if (_registered)
        {
            UnregisterHotKey(_source.Handle, HotkeyId);
            _registered = false;
        }
    }

    public void Dispose()
    {
        Unregister();
        _source.RemoveHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            Pressed?.Invoke();
        }
        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
