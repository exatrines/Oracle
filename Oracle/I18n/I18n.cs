using System.Text.Json;

namespace Oracle;

/// <summary>
/// UI-language strings from Data/I18n/{lang}.json (fallback: en).
/// Language follows config (<see cref="Configuration.UiLanguage"/>), not only Dalamud UI language.
/// </summary>
internal static class I18n
{
    public const string FollowClient = "client";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static IDalamudPluginInterface? _pluginInterface;
    private static Dictionary<string, string> _strings = new(StringComparer.Ordinal);
    private static string _lang = "en";

    public static event Action? Reloaded;

    public static string CurrentLang => _lang;

    public static void Init(IDalamudPluginInterface pluginInterface)
    {
        ArgumentNullException.ThrowIfNull(pluginInterface);
        _pluginInterface = pluginInterface;
        pluginInterface.LanguageChanged += OnClientLanguageChanged;
        ApplyFromConfig();
    }

    public static void Dispose()
    {
        if (_pluginInterface != null)
            _pluginInterface.LanguageChanged -= OnClientLanguageChanged;
        _pluginInterface = null;
        Reloaded = null;
        _strings = new(StringComparer.Ordinal);
    }

    public static void ApplyFromConfig()
    {
        Load(ResolveConfiguredLang());
        Reloaded?.Invoke();
    }

    public static string Get(string key)
    {
        if (string.IsNullOrEmpty(key))
            return string.Empty;

        if (_strings.TryGetValue(key, out var value) && value != null)
            return value;

        return key;
    }

    public static string Format(string key, params object?[] args)
    {
        var template = Get(key);
        try
        {
            return string.Format(template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    private static void OnClientLanguageChanged(string langCode)
    {
        if (!IsFollowClient())
            return;

        Load(NormalizeLangCode(langCode));
        Reloaded?.Invoke();
    }

    private static bool IsFollowClient()
    {
        try
        {
            var mode = C.UiLanguage;
            return string.IsNullOrWhiteSpace(mode)
                   || string.Equals(mode, FollowClient, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true;
        }
    }

    private static string ResolveConfiguredLang()
    {
        try
        {
            var mode = C.UiLanguage?.Trim();
            if (!string.IsNullOrEmpty(mode)
                && !string.Equals(mode, FollowClient, StringComparison.OrdinalIgnoreCase))
                return NormalizeLangCode(mode);
        }
        catch
        {
            // C may be unavailable during early init.
        }

        return NormalizeLangCode(_pluginInterface?.UiLanguage);
    }

    private static void Load(string? langCode)
    {
        _lang = NormalizeLangCode(langCode);
        var map = ReadLangFile(_lang);
        if (map.Count == 0 && !string.Equals(_lang, "en", StringComparison.Ordinal))
            map = ReadLangFile("en");

        _strings = map;
    }

    private static string NormalizeLangCode(string? langCode)
    {
        var lang = string.IsNullOrWhiteSpace(langCode) ? "en" : langCode.Trim().ToLowerInvariant();
        return lang.Length > 2 ? lang[..2] : lang;
    }

    private static Dictionary<string, string> ReadLangFile(string lang)
    {
        try
        {
            var pi = _pluginInterface ?? PluginServices.PluginInterface;
            var dir = pi.AssemblyLocation.DirectoryName ?? AppContext.BaseDirectory;
            var path = Path.Combine(dir, "Data", "I18n", $"{lang}.json");
            if (!File.Exists(path))
                return new Dictionary<string, string>(StringComparer.Ordinal);

            var json = File.ReadAllText(path);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
            return parsed == null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(parsed, StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            try
            {
                PluginServices.Log.Warning(ex, "Failed to load i18n file for {Lang}", lang);
            }
            catch
            {
                // PluginServices may not be ready during early init.
            }

            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }
}
