using System.IO;
using System.Runtime.InteropServices;
using System.Windows;

namespace CatTranslator.Platform;

/// <summary>
/// The clipboard can be briefly locked by another process (CLIPBRD_E_CANT_OPEN),
/// so every access retries a few times before giving up.
/// </summary>
internal static class ClipboardHelper
{
    private const int Attempts = 5;

    public static string? TryGetText()
    {
        for (int i = 0; i < Attempts; i++)
        {
            try
            {
                return Clipboard.ContainsText() ? Clipboard.GetText() : null;
            }
            catch (COMException)
            {
                Thread.Sleep(40);
            }
        }
        return null;
    }

    /// <summary>
    /// Password managers mark copied secrets with these formats (the same ones Windows
    /// clipboard history honors). Automatic translation must skip such content.
    /// </summary>
    public static bool IsExcludedFromMonitoring()
    {
        try
        {
            if (Clipboard.ContainsData("ExcludeClipboardContentFromMonitorProcessing"))
            {
                return true;
            }
            if (Clipboard.GetData("CanIncludeInClipboardHistory") is MemoryStream stream && stream.Length >= 4)
            {
                var value = new byte[4];
                stream.Position = 0;
                stream.ReadExactly(value);
                return BitConverter.ToInt32(value) == 0;
            }
            return false;
        }
        catch (COMException)
        {
            return true; // Cannot tell: stay on the safe side.
        }
    }

    public static bool TrySetText(string text)
    {
        for (int i = 0; i < Attempts; i++)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch (COMException)
            {
                Thread.Sleep(40);
            }
        }
        return false;
    }
}
