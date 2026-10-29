namespace Oracle.UI;

// --- Plugin log ---

internal sealed partial class PluginSettingsWindow
{
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
                    "settings.checkbox.plugin_log.auto_load",
                    () => C.PluginLogAutoLoad,
                    v => C.PluginLogAutoLoad = v);
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
}
