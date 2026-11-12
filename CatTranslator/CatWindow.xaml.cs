using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CatTranslator.Core;
using CatTranslator.Platform;

namespace CatTranslator;

public partial class CatWindow : Window
{
    private readonly SettingsStore _settings;
    private readonly HistoryStore _history;
    private readonly DispatcherTimer _blinkTimer;
    private readonly DispatcherTimer _errorBadgeTimer;
    private readonly Dictionary<string, MenuItem> _languageItems = [];
    private readonly MenuItem _hotkeyInfoItem = new() { IsEnabled = false };
    private readonly MenuItem _historyItem = new();
    private readonly MenuItem _doubleCopyItem = new() { Header = "Ctrl+C 두 번으로 번역" };

    private GlobalHotkey? _hotkey;
    private ClipboardWatcher? _clipboardWatcher;
    private BubbleWindow? _bubble;
    private HistoryWindow? _historyWindow;
    private SettingsWindow? _settingsWindow;

    private Point _pressPoint;
    private bool _pressed;
    private bool _busy;

    public CatWindow(SettingsStore settings, HistoryStore history)
    {
        InitializeComponent();
        _settings = settings;
        _history = history;

        RestorePosition();

        Root.MouseLeftButtonDown += OnMouseDown;
        Root.MouseMove += OnMouseMove;
        Root.MouseLeftButtonUp += OnMouseUp;
        Root.ContextMenu = BuildContextMenu();
        LocationChanged += (_, _) => _bubble?.PlaceNear(this);

        _settings.Changed += UpdateToolTip;
        UpdateToolTip();

        // A short blink every few seconds instead of a looping storyboard,
        // so the window does not re-render continuously while idle.
        _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4.5) };
        _blinkTimer.Tick += (_, _) => Blink();
        _blinkTimer.Start();

        _errorBadgeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _errorBadgeTimer.Tick += (_, _) =>
        {
            _errorBadgeTimer.Stop();
            ErrorBadge.Visibility = Visibility.Collapsed;
        };

        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _hotkey = new GlobalHotkey(this);
        _hotkey.Pressed += () => _ = TranslateClipboardAsync();
        RegisterHotkeyFromSettings();

        _clipboardWatcher = new ClipboardWatcher(this);
        _clipboardWatcher.DoubleCopy += () =>
        {
            if (_settings.Current.DoubleCopyTranslate)
            {
                _ = TranslateClipboardAsync(auto: true);
            }
        };

        if (_settings.ResolveApiKey().Key is null)
        {
            OpenSettings();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _blinkTimer.Stop();
        _errorBadgeTimer.Stop();
        _hotkey?.Dispose();
        _clipboardWatcher?.Dispose();
        _settings.Changed -= UpdateToolTip;
    }

    // ---------------------------------------------------------------- translation

    /// <param name="auto">True when triggered by Ctrl+C twice rather than an explicit click or hotkey.</param>
    private async Task TranslateClipboardAsync(bool auto = false)
    {
        if (_busy)
        {
            return;
        }
        if (auto && ClipboardHelper.IsExcludedFromMonitoring())
        {
            return; // e.g. a password copied from a password manager
        }

        var (apiKey, _) = _settings.ResolveApiKey();
        if (apiKey is null)
        {
            ShowError("API 키 없음",
                "Claude API 키가 설정되어 있지 않습니다.\n고양이를 우클릭해서 [설정]에서 키를 입력해 주세요.");
            return;
        }

        string? text = ClipboardHelper.TryGetText();
        if (string.IsNullOrWhiteSpace(text))
        {
            if (auto)
            {
                return; // Copied an image or file; nothing to say.
            }
            ShowBubble(BubbleKind.Info, "번역할 텍스트 없음",
                "클립보드에 텍스트가 없습니다.\n번역할 문장을 복사(Ctrl+C)한 뒤 다시 시도해 주세요.");
            return;
        }

        var target = Languages.Get(_settings.Current.TargetLanguage);
        SetBusy(true);
        try
        {
            var result = await Translator.TranslateAsync(apiKey, _settings.Current.Model, text, target);
            _history.Add(new HistoryEntry(DateTimeOffset.Now, target.Code, text.Trim(), result.Text, result.Warning));
            ShowBubble(BubbleKind.Translation, $"{target.Display} 번역", result.Text, result.Warning);
        }
        catch (TranslationException ex)
        {
            ShowError("번역 실패", ex.Message);
        }
        catch (Exception ex)
        {
            ShowError("번역 실패", $"예상하지 못한 오류가 발생했습니다.\n{ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        if (busy)
        {
            ErrorBadge.Visibility = Visibility.Collapsed;
            BusyBadge.Visibility = Visibility.Visible;
            BusyBadge.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.35, TimeSpan.FromMilliseconds(600))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
            });
        }
        else
        {
            BusyBadge.BeginAnimation(OpacityProperty, null);
            BusyBadge.Visibility = Visibility.Collapsed;
        }
    }

    // ---------------------------------------------------------------- bubble

    private void ShowBubble(BubbleKind kind, string title, string body, string? warning = null)
    {
        if (_bubble is null)
        {
            _bubble = new BubbleWindow { Owner = this };
            _bubble.Closed += (_, _) => _bubble = null;
        }
        _bubble.SetContent(kind, title, body, warning);
        _bubble.ShowNear(this);
    }

    private void ShowError(string title, string message)
    {
        ErrorBadge.Visibility = Visibility.Visible;
        _errorBadgeTimer.Stop();
        _errorBadgeTimer.Start();
        ShowBubble(BubbleKind.Error, title, message);
    }

    // ---------------------------------------------------------------- mouse: click translates, drag moves

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _pressed = true;
        _pressPoint = e.GetPosition(this);
        Root.CaptureMouse();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_pressed || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }
        Point p = e.GetPosition(this);
        if (Math.Abs(p.X - _pressPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(p.Y - _pressPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        _pressed = false;
        Root.ReleaseMouseCapture();
        try
        {
            DragMove(); // Blocks until the button is released.
        }
        catch (InvalidOperationException)
        {
            // The button was released before DragMove started.
        }
        SavePosition();
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pressed)
        {
            return;
        }
        _pressed = false;
        Root.ReleaseMouseCapture();
        _ = TranslateClipboardAsync();
    }

    private void RestorePosition()
    {
        var s = _settings.Current;
        if (s.CatLeft is double left && s.CatTop is double top &&
            ScreenHelper.IsOnVirtualScreen(new Point(left + Width / 2, top + Height / 2)))
        {
            Left = left;
            Top = top;
            return;
        }
        Rect workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 32;
        Top = workArea.Bottom - Height - 32;
    }

    private void SavePosition()
    {
        _settings.Current.CatLeft = Left;
        _settings.Current.CatTop = Top;
        try
        {
            _settings.SaveQuietly();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Losing the position is harmless.
        }
    }

    private void Blink()
    {
        if (Eyes.RenderTransform is not ScaleTransform scale)
        {
            return;
        }
        var blink = new DoubleAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromMilliseconds(220),
            FillBehavior = FillBehavior.Stop,
        };
        blink.KeyFrames.Add(new LinearDoubleKeyFrame(0.1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(90))));
        blink.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220))));
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, blink);
    }

    // ---------------------------------------------------------------- menu & windows

    private ContextMenu BuildContextMenu()
    {
        var menu = new ContextMenu();
        menu.Items.Add(_hotkeyInfoItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = "번역할 언어", IsEnabled = false });

        foreach (var language in Languages.All)
        {
            var item = new MenuItem { Header = language.Display };
            item.Click += (_, _) => _settings.Update(s => s.TargetLanguage = language.Code);
            _languageItems[language.Code] = item;
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());
        _doubleCopyItem.Click += (_, _) =>
            _settings.Update(s => s.DoubleCopyTranslate = !s.DoubleCopyTranslate);
        menu.Items.Add(_doubleCopyItem);
        _historyItem.Click += (_, _) => OpenHistory();
        menu.Items.Add(_historyItem);

        var settingsItem = new MenuItem { Header = "설정..." };
        settingsItem.Click += (_, _) => OpenSettings();
        menu.Items.Add(settingsItem);

        menu.Items.Add(new Separator());
        var exitItem = new MenuItem { Header = "종료" };
        exitItem.Click += (_, _) => Close();
        menu.Items.Add(exitItem);

        menu.Opened += (_, _) => RefreshMenu();
        return menu;
    }

    private void RefreshMenu()
    {
        _hotkeyInfoItem.Header = $"클릭 또는 {_settings.Current.Hotkey}: 클립보드 번역";
        foreach (var (code, item) in _languageItems)
        {
            item.IsChecked = code == _settings.Current.TargetLanguage;
        }
        _doubleCopyItem.IsChecked = _settings.Current.DoubleCopyTranslate;
        _historyItem.Header = $"번역 기록 ({_history.Entries.Count}/{HistoryStore.MaxEntries})...";
    }

    private void UpdateToolTip()
    {
        var target = Languages.Get(_settings.Current.TargetLanguage);
        string triggers = _settings.Current.DoubleCopyTranslate
            ? $"Ctrl+C 두 번, 클릭 또는 {_settings.Current.Hotkey}"
            : $"클릭 또는 {_settings.Current.Hotkey}";
        Root.ToolTip = $"{triggers}: 클립보드 → {target.Display} 번역\n" +
                       "드래그: 이동    우클릭: 언어 변경, 기록, 설정";
    }

    private void OpenHistory()
    {
        if (_historyWindow is not null)
        {
            _historyWindow.Activate();
            return;
        }
        _historyWindow = new HistoryWindow(_history);
        _historyWindow.Closed += (_, _) => _historyWindow = null;
        _historyWindow.Show();
        _historyWindow.Activate();
    }

    private void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        // Suspend the global hotkey so the settings window can capture the same combination.
        _hotkey?.Unregister();
        _settingsWindow = new SettingsWindow(_settings, _history, IsHotkeyAvailable);
        _settingsWindow.Closed += (_, _) =>
        {
            _settingsWindow = null;
            RegisterHotkeyFromSettings();
        };
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private bool IsHotkeyAvailable(HotkeyGesture gesture)
    {
        if (_hotkey is null)
        {
            return false;
        }
        bool ok = _hotkey.TryRegister(gesture);
        _hotkey.Unregister();
        return ok;
    }

    private void RegisterHotkeyFromSettings()
    {
        if (_hotkey is null)
        {
            return;
        }
        if (!HotkeyGesture.TryParse(_settings.Current.Hotkey, out var gesture))
        {
            HotkeyGesture.TryParse(AppSettings.DefaultHotkey, out gesture);
        }
        if (!_hotkey.TryRegister(gesture))
        {
            ShowError("단축키 등록 실패",
                $"{gesture} 단축키를 등록하지 못했습니다. 다른 프로그램이 이미 사용 중일 수 있습니다.\n" +
                "고양이를 우클릭해서 [설정]에서 다른 조합을 지정해 주세요.");
        }
    }
}
