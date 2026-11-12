using System.IO;
using System.Windows;
using System.Windows.Input;
using CatTranslator.Core;
using CatTranslator.Platform;

namespace CatTranslator;

public partial class SettingsWindow : Window
{
    private readonly SettingsStore _settings;
    private readonly HistoryStore _history;
    private readonly Func<HotkeyGesture, bool> _isHotkeyAvailable;
    private HotkeyGesture _gesture;
    private bool _deleteStoredKey;

    public SettingsWindow(SettingsStore settings, HistoryStore history, Func<HotkeyGesture, bool> isHotkeyAvailable)
    {
        InitializeComponent();
        _settings = settings;
        _history = history;
        _isHotkeyAvailable = isHotkeyAvailable;

        ModelBox.Text = settings.Current.Model;
        if (!HotkeyGesture.TryParse(settings.Current.Hotkey, out _gesture))
        {
            HotkeyGesture.TryParse(AppSettings.DefaultHotkey, out _gesture);
        }
        HotkeyBox.Text = _gesture.ToString();
        SaveHistoryBox.IsChecked = settings.Current.SaveHistory;
        DoubleCopyBox.IsChecked = settings.Current.DoubleCopyTranslate;
        UpdateKeyStatus();

        Loaded += (_, _) => ApiKeyBox.Focus();
    }

    private void UpdateKeyStatus()
    {
        var (_, source) = _settings.ResolveApiKey();
        if (_deleteStoredKey && source == ApiKeySource.Stored)
        {
            source = ApiKeySource.None;
        }
        KeyStatusText.Text = source switch
        {
            ApiKeySource.Stored => "현재 사용 중: 저장된 키",
            ApiKeySource.EnvClaude => "현재 사용 중: 환경 변수 CLAUDE_API_KEY",
            ApiKeySource.EnvAnthropic => "현재 사용 중: 환경 변수 ANTHROPIC_API_KEY",
            _ => "API 키가 없습니다. 키를 입력해 주세요.",
        };
        KeyStatusText.Foreground = source == ApiKeySource.None
            ? System.Windows.Media.Brushes.Firebrick
            : System.Windows.Media.Brushes.DarkGreen;
        DeleteKeyButton.IsEnabled = _settings.HasStoredApiKey && !_deleteStoredKey;
    }

    private void OnDeleteKey(object sender, RoutedEventArgs e)
    {
        _deleteStoredKey = true;
        ApiKeyBox.Clear();
        UpdateKeyStatus();
        KeyStatusText.Text += "  (저장을 누르면 삭제됩니다)";
    }

    private void OnHotkeyKeyDown(object sender, KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Tab && Keyboard.Modifiers == ModifierKeys.None)
        {
            return; // Keep normal focus navigation.
        }
        e.Handled = true;
        if (HotkeyGesture.IsModifierKey(key))
        {
            return; // Wait for the main key.
        }
        if (Keyboard.Modifiers == ModifierKeys.None)
        {
            HotkeyHint.Text = "Ctrl, Alt, Shift, Win 중 하나 이상과 함께 눌러 주세요.";
            return;
        }
        _gesture = new HotkeyGesture(Keyboard.Modifiers, key);
        HotkeyBox.Text = _gesture.ToString();
        HotkeyHint.Text = "저장을 누르면 이 조합이 적용됩니다.";
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        string hotkey = _gesture.ToString();
        if (hotkey != _settings.Current.Hotkey && !_isHotkeyAvailable(_gesture))
        {
            MessageBox.Show(this,
                $"{hotkey} 단축키를 등록할 수 없습니다. 다른 프로그램이나 Windows가 이미 사용 중일 수 있습니다.\n다른 조합을 지정해 주세요.",
                "설정", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string newKey = ApiKeyBox.Password.Trim();
        string model = string.IsNullOrWhiteSpace(ModelBox.Text) ? AppSettings.DefaultModel : ModelBox.Text.Trim();
        bool saveHistory = SaveHistoryBox.IsChecked == true;
        bool doubleCopy = DoubleCopyBox.IsChecked == true;

        try
        {
            _settings.Update(s =>
            {
                if (newKey.Length > 0)
                {
                    s.EncryptedApiKey = SettingsStore.Protect(newKey);
                }
                else if (_deleteStoredKey)
                {
                    s.EncryptedApiKey = null;
                }
                s.Model = model;
                s.Hotkey = hotkey;
                s.SaveHistory = saveHistory;
                s.DoubleCopyTranslate = doubleCopy;
            });
            _history.SetPersistence(saveHistory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"설정을 저장하지 못했습니다.\n{ex.Message}", "설정",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        Close();
    }
}
