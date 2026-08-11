using Oracle.Services;

namespace Oracle.UI;

/// <summary>
/// Searchable zone dropdown over <see cref="DutyContentCatalog"/>.
/// Returns true when the selection changes (including Set Current / clear).
/// </summary>
internal static class ZoneCombo
{
    public static bool Draw(
        string label,
        ref uint territoryTypeId,
        ref uint contentFinderConditionId,
        ref byte classJobLevel,
        ref string zoneLabel,
        ref string searchFilter,
        string id,
        bool allowClear = true,
        bool showSetCurrent = true)
    {
        SyncLabel(territoryTypeId, contentFinderConditionId, classJobLevel, ref zoneLabel);

        var options = DutyContentCatalog.GetZoneOptions();
        var items = options.Select(o => o.Label).ToList();
        var selected = territoryTypeId == 0 ? string.Empty : zoneLabel;

        if (territoryTypeId != 0
            && !string.IsNullOrWhiteSpace(selected)
            && !items.Contains(selected, StringComparer.Ordinal))
            items.Insert(0, selected);

        var setCurrentClicked = false;
        Action? onHeaderButton = null;
        string? headerButtonLabel = null;
        if (showSetCurrent)
        {
            headerButtonLabel = I18n.Get("config.zone.set_current");
            onHeaderButton = () => setCurrentClicked = true;
        }

        var changed = MirageUi.SearchableDropdown(
            label,
            ref selected,
            items,
            ref searchFilter,
            placeholder: I18n.Get("config.zone.not_set"),
            id: id,
            allowClear: allowClear,
            emptyMessage: I18n.Get("config.zone.empty"),
            searchHint: I18n.Get("config.zone.search_hint"),
            width: MirageUi.InputWidthFill,
            headerButtonLabel: headerButtonLabel,
            onHeaderButton: onHeaderButton);

        if (setCurrentClicked)
            return ApplyCurrent(ref territoryTypeId, ref contentFinderConditionId, ref classJobLevel, ref zoneLabel);

        if (!changed)
            return false;

        if (string.IsNullOrWhiteSpace(selected))
        {
            territoryTypeId = 0;
            contentFinderConditionId = 0;
            classJobLevel = 0;
            zoneLabel = string.Empty;
            return true;
        }

        ApplyLabel(selected, ref territoryTypeId, ref contentFinderConditionId, ref classJobLevel, ref zoneLabel);
        return true;
    }

    public static void DrawReadonly(
        string label,
        uint territoryTypeId,
        uint contentFinderConditionId,
        byte classJobLevel,
        string id,
        bool? liveMatch = null,
        string? matchTooltip = null)
    {
        var selected = territoryTypeId == 0
            ? string.Empty
            : DutyContentCatalog.ResolveZoneLabel(territoryTypeId, contentFinderConditionId, classJobLevel);
        var items = string.IsNullOrWhiteSpace(selected) ? Array.Empty<string>() : new[] { selected };

        using (MirageUi.DisabledIf(true))
        {
            MirageUi.Dropdown(
                label,
                ref selected,
                items,
                placeholder: I18n.Get("config.zone.not_set"),
                id: id,
                allowClear: false,
                liveMatch: liveMatch,
                matchTooltip: matchTooltip);
        }
    }

    public static bool ApplyCurrent(
        ref uint territoryTypeId,
        ref uint contentFinderConditionId,
        ref byte classJobLevel,
        ref string zoneLabel)
    {
        var territory = PluginServices.ClientState.TerritoryType;
        if (territory == 0)
            return false;

        if (!DutyContentCatalog.TryResolvePreferredForTerritory(territory, out var option))
            return false;

        territoryTypeId = option.TerritoryTypeId;
        contentFinderConditionId = option.ContentFinderConditionId;
        classJobLevel = option.ClassJobLevel;
        zoneLabel = option.Label;
        return true;
    }

    private static void SyncLabel(
        uint territoryTypeId,
        uint contentFinderConditionId,
        byte classJobLevel,
        ref string zoneLabel)
    {
        if (territoryTypeId == 0)
        {
            zoneLabel = string.Empty;
            return;
        }

        if (!string.IsNullOrWhiteSpace(zoneLabel))
            return;

        zoneLabel = DutyContentCatalog.ResolveZoneLabel(
            territoryTypeId,
            contentFinderConditionId,
            classJobLevel);
    }

    private static void ApplyLabel(
        string selected,
        ref uint territoryTypeId,
        ref uint contentFinderConditionId,
        ref byte classJobLevel,
        ref string zoneLabel)
    {
        if (DutyContentCatalog.TryGetZoneOption(selected, out var option))
        {
            territoryTypeId = option.TerritoryTypeId;
            contentFinderConditionId = option.ContentFinderConditionId;
            classJobLevel = option.ClassJobLevel;
            zoneLabel = option.Label;
            return;
        }

        zoneLabel = selected;
        var pipe = selected.IndexOf('|');
        var idPart = pipe >= 0 ? selected[..pipe].Trim() : selected.Trim();
        if (!uint.TryParse(idPart, out var territoryId))
            return;

        territoryTypeId = territoryId;
        contentFinderConditionId = 0;
        classJobLevel = 0;
    }
}
