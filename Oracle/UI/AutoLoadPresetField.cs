using Oracle.Models;
using Oracle.Services;

namespace Oracle.UI;

/// <summary>Phase picker: empty = any bosses for the zone.</summary>
internal static class AutoLoadPresetField
{
    public static bool Draw(
        string label,
        string id,
        uint territoryTypeId,
        ref string presetId,
        bool? liveMatch = null,
        string? matchTooltip = null)
    {
        var preset = AutoLoadPresets.FindForTerritory(territoryTypeId);
        var items = new List<string>();
        if (preset != null)
            items.Add(FormatName(preset));

        var currentId = presetId ?? string.Empty;
        var selected = string.Empty;
        if (!string.IsNullOrWhiteSpace(currentId))
        {
            var matchesZone = preset != null
                && string.Equals(preset.Id, currentId, StringComparison.OrdinalIgnoreCase);
            selected = matchesZone ? FormatName(preset!) : currentId;
        }

        if (!MirageUi.Dropdown(
                label,
                ref selected,
                items,
                placeholder: I18n.Get("config.autoload.preset.any"),
                id: id,
                allowClear: true,
                liveMatch: liveMatch,
                matchTooltip: matchTooltip))
            return false;

        if (string.IsNullOrWhiteSpace(selected))
        {
            presetId = string.Empty;
            return true;
        }

        if (preset != null
            && string.Equals(FormatName(preset), selected, StringComparison.Ordinal))
        {
            presetId = preset.Id;
            return true;
        }

        return false;
    }

    private static string FormatName(AutoLoadPresetEntry preset) =>
        string.IsNullOrWhiteSpace(preset.Name)
            ? I18n.Get("settings.autoload_presets.unnamed")
            : preset.Name.Trim();
}
