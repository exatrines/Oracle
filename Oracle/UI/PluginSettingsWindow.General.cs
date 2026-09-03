namespace Oracle.UI;

// --- General / Plugin log ---

internal sealed partial class PluginSettingsWindow
{
    // --- General ---

    private static void DrawGeneralSettings()
    {
        MirageUi.Header(I18n.Get("settings.header.general"));

        var selected = NormalizeUiLanguageSetting(C.UiLanguage);
        var labels = new[]
        {
            I18n.Get("settings.language.client"),
            I18n.Get("settings.language.en"),
            I18n.Get("settings.language.ja"),
        };
        var values = new[] { I18n.FollowClient, "en", "ja" };
        var selectedLabel = labels[Array.IndexOf(values, selected)];

        if (!MirageUi.Dropdown(
                I18n.Get("settings.label.ui_language"),
                ref selectedLabel,
                labels,
                id: "uiLanguage",
                allowClear: false))
            return;

        var index = Array.IndexOf(labels, selectedLabel);
        if (index < 0)
            return;

        var next = values[index];
        if (string.Equals(C.UiLanguage, next, StringComparison.OrdinalIgnoreCase))
            return;

        C.UiLanguage = next;
        C.Save();
        I18n.ApplyFromConfig();
    }

    // --- Plugin log ---

    private static void DrawPluginLogSettings()
    {
        MirageUi.Header(I18n.Get("settings.header.plugin_log"));

        var enabled = C.PluginLogEnabled;
        using (var group = MirageUi.CheckboxGroup(I18n.Get("settings.checkbox.enable"), ref enabled))
        {
            if (group.Changed)
            {
                C.PluginLogEnabled = enabled;
                C.Save();
            }

            using (ImRaii.Disabled(!C.PluginLogEnabled))
            {
                DrawPluginLogKind(
                    "settings.checkbox.plugin_log.countdown",
                    () => C.PluginLogCountdown,
                    v => C.PluginLogCountdown = v);
                DrawPluginLogKind(
                    "settings.checkbox.plugin_log.combat",
                    () => C.PluginLogCombat,
                    v => C.PluginLogCombat = v);
                DrawPluginLogKind(
                    "settings.checkbox.plugin_log.casts",
                    () => C.PluginLogCasts,
                    v => C.PluginLogCasts = v);
                DrawPluginLogKind(
                    "settings.checkbox.plugin_log.status",
                    () => C.PluginLogStatus,
                    v => C.PluginLogStatus = v);
                DrawPluginLogKind(
                    "settings.checkbox.plugin_log.action_effect",
                    () => C.PluginLogActionEffect,
                    v => C.PluginLogActionEffect = v);
                DrawPluginLogKind(
                    "settings.checkbox.plugin_log.scene",
                    () => C.PluginLogScene,
                    v => C.PluginLogScene = v);
            }
        }
    }

    private static void DrawPluginLogKind(string key, Func<bool> get, Action<bool> set)
    {
        var value = get();
        if (MirageUi.Checkbox(I18n.Get(key), ref value))
        {
            set(value);
            C.Save();
        }
    }

    private static string NormalizeUiLanguageSetting(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || string.Equals(value, I18n.FollowClient, StringComparison.OrdinalIgnoreCase))
            return I18n.FollowClient;

        var lang = value.Trim().ToLowerInvariant();
        if (lang.Length > 2)
            lang = lang[..2];
        return lang is "en" or "ja" ? lang : I18n.FollowClient;
    }
}
