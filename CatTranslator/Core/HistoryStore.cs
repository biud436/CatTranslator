using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CatTranslator.Core;

public sealed record HistoryEntry(
    DateTimeOffset Time,
    string TargetLanguage,
    string Source,
    string Translation,
    string? Warning)
{
    [JsonIgnore]
    public string Header =>
        $"{Time.LocalDateTime:MM-dd HH:mm}  →  {Languages.Get(TargetLanguage).Display}" +
        (Warning is null ? "" : "  ⚠");

    [JsonIgnore]
    public string Preview
    {
        get
        {
            string firstLine = Source.Trim().Split('\n', 2)[0].Trim();
            return firstLine.Length > 60 ? firstLine[..60] + "…" : firstLine;
        }
    }
}

/// <summary>Keeps the most recent <see cref="MaxEntries"/> translations, newest first.</summary>
public sealed class HistoryStore
{
    public const int MaxEntries = 20;

    private bool _persist;

    private HistoryStore(bool persist) => _persist = persist;

    public ObservableCollection<HistoryEntry> Entries { get; } = [];

    public static HistoryStore Load(bool persist)
    {
        var store = new HistoryStore(persist);
        if (!persist || !File.Exists(AppPaths.HistoryFile))
        {
            return store;
        }
        try
        {
            var saved = JsonSerializer.Deserialize<List<HistoryEntry>>(
                File.ReadAllText(AppPaths.HistoryFile), AppPaths.Json);
            foreach (var entry in (saved ?? []).Take(MaxEntries))
            {
                store.Entries.Add(entry);
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Ignore a broken history file; it is overwritten on the next save.
        }
        return store;
    }

    public void Add(HistoryEntry entry)
    {
        Entries.Insert(0, entry);
        while (Entries.Count > MaxEntries)
        {
            Entries.RemoveAt(Entries.Count - 1);
        }
        Persist();
    }

    public void Clear()
    {
        Entries.Clear();
        Persist();
    }

    public void SetPersistence(bool persist)
    {
        _persist = persist;
        if (persist)
        {
            Persist();
        }
        else if (File.Exists(AppPaths.HistoryFile))
        {
            File.Delete(AppPaths.HistoryFile);
        }
    }

    private void Persist()
    {
        if (!_persist)
        {
            return;
        }
        try
        {
            AppPaths.WriteAllTextAtomic(AppPaths.HistoryFile, JsonSerializer.Serialize(Entries, AppPaths.Json));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A failed save must not turn a successful translation into an error.
        }
    }
}
