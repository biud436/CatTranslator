using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CatTranslator.Platform;

namespace CatTranslator;

public enum BubbleKind
{
    Translation,
    Info,
    Error,
}

public partial class BubbleWindow : Window
{
    private Window? _anchor;

    public BubbleWindow()
    {
        InitializeComponent();
        SizeChanged += (_, _) => { if (_anchor is not null) PlaceNear(_anchor); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
    }

    public void SetContent(BubbleKind kind, string title, string body, string? warning)
    {
        TitleText.Text = title;
        TitleText.Foreground = kind == BubbleKind.Error
            ? new SolidColorBrush(Color.FromRgb(0xD9, 0x30, 0x25))
            : new SolidColorBrush(Color.FromRgb(0x5C, 0x3D, 0x2E));
        BodyText.Text = body;
        BodyText.ScrollToHome();
        CopyButton.Visibility = kind == BubbleKind.Translation ? Visibility.Visible : Visibility.Collapsed;
        CopyButton.Content = "복사";
        WarningText.Text = warning is null ? "" : "⚠ " + warning;
        WarningText.Visibility = warning is null ? Visibility.Collapsed : Visibility.Visible;
    }

    public void ShowNear(Window anchor)
    {
        _anchor = anchor;
        if (!IsVisible)
        {
            Show();
        }
        PlaceNear(anchor);
    }

    /// <summary>Places the bubble beside the cat, on whichever side has more room.</summary>
    public void PlaceNear(Window anchor)
    {
        Rect workArea = ScreenHelper.GetWorkArea(anchor);
        double width = ActualWidth;
        double height = ActualHeight;
        bool catOnRightHalf = anchor.Left + anchor.ActualWidth / 2 > workArea.Left + workArea.Width / 2;

        double left = catOnRightHalf ? anchor.Left - width : anchor.Left + anchor.ActualWidth;
        double top = anchor.Top + anchor.ActualHeight - height;

        Left = Math.Clamp(left, workArea.Left, Math.Max(workArea.Left, workArea.Right - width));
        Top = Math.Clamp(top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - height));
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        CopyButton.Content = ClipboardHelper.TrySetText(BodyText.Text) ? "복사됨" : "복사 실패";
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
