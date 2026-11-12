using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CatTranslator.Core;

public sealed class AppSettings
{
    public const string DefaultModel = "claude-haiku-5-5";
    public const string DefaultHotkey = "Ctrl+Alt+T";

    /// <summary>API key encrypted with DPAPI (current Windows user), base64.</summary>
    public string? EncryptedApiKey { get; set; }
    public string Model { get; set; } = DefaultModel;
    public string Hotkey { get; set; } = DefaultHotkey;
    public string TargetLanguage { get; set; } = Languages.DefaultCode;
    /// <summary>Translate when Ctrl+C is pressed twice quickly.</summary>
    public bool DoubleCopyTranslate { get; set; } = true;
    public bool SaveHistory { get; set; } = true;
    public double? CatLeft { get; set; }
    public double? CatTop { get; set; }
}

public enum ApiKeySource
{
    None,
    Stored,
    EnvClaude,
    EnvAnthropic,
}

public sealed class SettingsStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("CatTranslator.ApiKey.v1");

    private SettingsStore(AppSettings current) => Current = current;

    public AppSettings Current { get; }

    public event Action? Changed;

    public static SettingsStore Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(
                    File.ReadAllText(AppPaths.SettingsFile), AppPaths.Json);
                if (loaded is not null)
                {
                    return new SettingsStore(loaded);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // A broken settings file falls back to defaults instead of blocking startup.
        }
        return new SettingsStore(new AppSettings());
    }

    public void Update(Action<AppSettings> mutate)
    {
        mutate(Current);
        Save();
        Changed?.Invoke();
    }

    /// <summary>Saves without raising <see cref="Changed"/> (used for frequent updates such as window position).</summary>
    public void SaveQuietly() => Save();

    public static string? Protect(string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }
        byte[] cipher = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(apiKey.Trim()), Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(cipher);
    }

    public bool HasStoredApiKey => GetStoredApiKey() is not null;

    public (string? Key, ApiKeySource Source) ResolveApiKey()
    {
        if (GetStoredApiKey() is { } stored)
        {
            return (stored, ApiKeySource.Stored);
        }
        if (Environment.GetEnvironmentVariable("CLAUDE_API_KEY") is { Length: > 0 } claude)
        {
            return (claude, ApiKeySource.EnvClaude);
        }
        if (Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") is { Length: > 0 } anthropic)
        {
            return (anthropic, ApiKeySource.EnvAnthropic);
        }
        return (null, ApiKeySource.None);
    }

    private string? GetStoredApiKey()
    {
        if (string.IsNullOrEmpty(Current.EncryptedApiKey))
        {
            return null;
        }
        try
        {
            byte[] plain = ProtectedData.Unprotect(
                Convert.FromBase64String(Current.EncryptedApiKey), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            // Encrypted by another Windows user or corrupted: treat as missing.
            return null;
        }
    }

    private void Save()
    {
        AppPaths.WriteAllTextAtomic(AppPaths.SettingsFile, JsonSerializer.Serialize(Current, AppPaths.Json));
    }
}
