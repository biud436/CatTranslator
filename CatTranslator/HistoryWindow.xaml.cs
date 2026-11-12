using System.Collections.Specialized;
using System.Windows;
using CatTranslator.Core;
using CatTranslator.Platform;

namespace CatTranslator;

public partial class HistoryWindow : Window
{
    private readonly HistoryStore _history;

    public HistoryWindow(HistoryStore history)
    {
        InitializeComponent();
        _history = history;

        EntryList.ItemsSource = history.Entries;
        EntryList.SelectionChanged += (_, _) => ShowSelected();
        history.Entries.CollectionChanged += OnEntriesChanged;
        Closed += (_, _) => history.Entries.CollectionChanged -= OnEntriesChanged;

        if (history.Entries.Count > 0)
        {
            EntryList.SelectedIndex = 0;
        }
        UpdateCount();
        ShowSelected();
    }

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateCount();
        if (EntryList.SelectedItem is null && _history.Entries.Count > 0)
        {
            EntryList.SelectedIndex = 0;
        }
    }

    private void UpdateCount() =>
        CountText.Text = $"최근 {_history.Entries.Count}건 / 최대 {HistoryStore.MaxEntries}건";

    private void ShowSelected()
    {
        var entry = EntryList.SelectedItem as HistoryEntry;
        SourceBox.Text = entry?.Source ?? "";
        TranslationBox.Text = entry?.Translation ?? "";
        WarningText.Text = entry?.Warning is { } w ? "⚠ " + w : "";
        WarningText.Visibility = entry?.Warning is null ? Visibility.Collapsed : Visibility.Visible;
        CopyButton.IsEnabled = entry is not null;
        CopyButton.Content = "번역문 복사";
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        if (EntryList.SelectedItem is HistoryEntry entry)
        {
            CopyButton.Content = ClipboardHelper.TrySetText(entry.Translation) ? "복사됨" : "복사 실패";
        }
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        if (_history.Entries.Count > 0 &&
            MessageBox.Show(this, "번역 기록을 모두 지울까요?", "번역 기록",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _history.Clear();
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
