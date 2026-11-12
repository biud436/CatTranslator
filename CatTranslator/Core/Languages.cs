namespace CatTranslator.Core;

public enum LanguageScript
{
    Hangul,
    Latin,
    Japanese,
    Han,
    Cyrillic,
}

/// <param name="Code">Stored in settings and history.</param>
/// <param name="Display">Shown in the UI.</param>
/// <param name="PromptName">English name inserted into the prompt.</param>
/// <param name="Script">Used by the output sanity check.</param>
public sealed record LanguageOption(string Code, string Display, string PromptName, LanguageScript Script);

public static class Languages
{
    public const string DefaultCode = "ko";

    public static readonly IReadOnlyList<LanguageOption> All =
    [
        new("ko", "한국어", "Korean", LanguageScript.Hangul),
        new("en", "English", "English", LanguageScript.Latin),
        new("ja", "日本語", "Japanese", LanguageScript.Japanese),
        new("zh-Hans", "中文(简体)", "Simplified Chinese", LanguageScript.Han),
        new("zh-Hant", "中文(繁體)", "Traditional Chinese", LanguageScript.Han),
        new("es", "Español", "Spanish", LanguageScript.Latin),
        new("fr", "Français", "French", LanguageScript.Latin),
        new("de", "Deutsch", "German", LanguageScript.Latin),
        new("vi", "Tiếng Việt", "Vietnamese", LanguageScript.Latin),
        new("ru", "Русский", "Russian", LanguageScript.Cyrillic),
    ];

    public static LanguageOption Get(string? code) =>
        All.FirstOrDefault(l => l.Code == code) ?? All.First(l => l.Code == DefaultCode);
}
