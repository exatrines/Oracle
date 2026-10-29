using Oracle.Models;

namespace Oracle.UI;

/// <summary>Sync row: type, then Cast or Status fields (id picker + edge).</summary>
internal static class SyncCueFields
{
    private const float TypeWidth = 72f;
    private const float EdgeWidth = 96f;

    private static string _actionSearch = string.Empty;
    private static string _statusSearch = string.Empty;

    public static void DrawImportOption(ref bool enabled, ref uint appliedTerritoryId, uint territoryTypeId)
    {
        var specs = EnemySyncPresets.SpecsFor(territoryTypeId);
        if (specs.Count == 0)
        {
            enabled = false;
            appliedTerritoryId = 0;
            return;
        }

        if (appliedTerritoryId != territoryTypeId)
        {
            enabled = true;
            appliedTerritoryId = territoryTypeId;
        }

        MirageUi.Checkbox(I18n.Get("fflogs.checkbox.enemy_sync"), ref enabled);
        if (!enabled)
            return;

        foreach (var spec in specs)
        {
            var name = spec.SyncType == EnemySyncType.Status
                ? EnemySyncCatalog.FormatStatusLabel(spec.ActionId)
                : EnemySyncCatalog.FormatCastLabel(spec.ActionId);
            var edge = spec.SyncType == EnemySyncType.Status
                ? StatusEdgeLabel(spec.Effected)
                : CastEdgeLabel(spec.Effected);
            DrawReadOnlyRow(spec.SyncType, name, edge);
        }
    }

    public static bool DrawRow(TimelineCue cue, string idPrefix)
    {
        var syncType = cue.SyncType;
        var actionId = cue.ActionId;
        var effected = cue.Effected;
        if (!DrawRow(ref syncType, ref actionId, ref effected, idPrefix))
            return false;

        cue.SyncType = syncType;
        cue.ActionId = actionId;
        cue.Effected = effected;
        return true;
    }

    public static bool DrawRow(
        ref EnemySyncType syncType,
        ref uint actionId,
        ref bool effected,
        string idPrefix)
    {
        var dirty = false;
        var gap = ImGui.GetStyle().ItemSpacing.X;
        if (DrawTypeDropdown(ref syncType, idPrefix + "Type", TypeWidth))
        {
            actionId = 0;
            effected = false;
            dirty = true;
        }

        var isStatus = syncType == EnemySyncType.Status;
        ImGui.SameLine(0f, gap);
        var pickerWidth = Math.Max(40f, ImGui.GetContentRegionAvail().X - EdgeWidth - gap);
        if (DrawNameField(ref actionId, syncType, idPrefix + (isStatus ? "Status" : "Action"), pickerWidth))
            dirty = true;

        ImGui.SameLine(0f, gap);
        if (DrawEdgeDropdown(ref effected, idPrefix + "Edge", isStatus, EdgeWidth))
            dirty = true;

        return dirty;
    }

    public static bool DrawTypeField(ref EnemySyncType syncType, string id) =>
        DrawTypeDropdown(ref syncType, id, MirageUi.InputWidthFill);

    public static bool DrawNameField(ref uint actionId, EnemySyncType syncType, string id) =>
        DrawNameField(ref actionId, syncType, id, MirageUi.InputWidthFill);

    public static bool DrawEdgeField(ref bool effected, EnemySyncType syncType, string id) =>
        DrawEdgeDropdown(ref effected, id, syncType == EnemySyncType.Status, MirageUi.InputWidthFill);

    private static bool DrawNameField(ref uint actionId, EnemySyncType syncType, string id, float width) =>
        syncType == EnemySyncType.Status
            ? DrawStatusPicker(ref actionId, id, width)
            : DrawCastPicker(ref actionId, id, width);

    private static bool DrawCastPicker(ref uint actionId, string id, float pickerWidth)
    {
        var selected = EnemySyncCatalog.FormatCastLabel(actionId);
        var items = EnemySyncCatalog.CastLabelsForPicker(actionId);
        if (!MirageUi.SearchableDropdown(
                string.Empty,
                ref selected,
                items,
                ref _actionSearch,
                placeholder: I18n.Get("config.cue.pick_action"),
                id: id,
                allowClear: false,
                searchHint: I18n.Get("action_search.hint.filter"),
                width: pickerWidth)
            || !EnemySyncCatalog.TryParseLabel(selected, out var parsed)
            || parsed == actionId)
            return false;

        actionId = parsed;
        return true;
    }

    private static bool DrawStatusPicker(ref uint statusId, string id, float pickerWidth)
    {
        var selected = EnemySyncCatalog.FormatStatusLabel(statusId);
        var items = EnemySyncCatalog.StatusLabelsForPicker(statusId);
        if (!MirageUi.SearchableDropdown(
                string.Empty,
                ref selected,
                items,
                ref _statusSearch,
                placeholder: I18n.Get("config.cue.pick_status"),
                id: id,
                allowClear: false,
                searchHint: I18n.Get("action_search.hint.filter"),
                width: pickerWidth)
            || !EnemySyncCatalog.TryParseLabel(selected, out var parsed)
            || parsed == statusId)
            return false;

        statusId = parsed;
        return true;
    }

    private static void DrawReadOnlyRow(EnemySyncType syncType, string name, string edge)
    {
        var gap = ImGui.GetStyle().ItemSpacing.X;
        ImGui.AlignTextToFramePadding();
        MirageUi.Text(SyncTypeLabel(syncType), wrap: false);
        ImGui.SameLine(0f, gap);
        var nameWidth = Math.Max(40f, ImGui.GetContentRegionAvail().X - EdgeWidth - gap);
        DrawClippedLabel(name, nameWidth);
        ImGui.SameLine(0f, gap);
        ImGui.AlignTextToFramePadding();
        MirageUi.Text(edge, wrap: false);
    }

    private static bool DrawTypeDropdown(ref EnemySyncType syncType, string id, float width)
    {
        var label = SyncTypeLabel(syncType);
        if (!MirageUi.Dropdown(
                string.Empty,
                ref label,
                SyncTypeLabels,
                id: id,
                allowClear: false,
                width: width))
            return false;

        syncType = string.Equals(label, I18n.Get("config.cue.sync_type.status"), StringComparison.Ordinal)
            ? EnemySyncType.Status
            : EnemySyncType.Cast;
        return true;
    }

    private static bool DrawEdgeDropdown(ref bool effected, string id, bool isStatus, float width)
    {
        var label = isStatus ? StatusEdgeLabel(effected) : CastEdgeLabel(effected);
        var items = isStatus ? StatusEdgeLabels : CastEdgeLabels;
        if (!MirageUi.Dropdown(
                string.Empty,
                ref label,
                items,
                id: id,
                allowClear: false,
                width: width))
            return false;

        effected = isStatus
            ? string.Equals(label, I18n.Get("config.cue.status_edge.remove"), StringComparison.Ordinal)
            : string.Equals(label, I18n.Get("config.cue.cast_edge.effected"), StringComparison.Ordinal);
        return true;
    }

    private static void DrawClippedLabel(string text, float width)
    {
        ImGui.AlignTextToFramePadding();
        var min = ImGui.GetCursorScreenPos();
        var height = ImGui.GetFrameHeight();
        ImGui.PushClipRect(min, min + new Vector2(width, height), true);
        MirageUi.Text(text, MirageUi.Color.Secondary, wrap: false);
        ImGui.PopClipRect();
        ImGui.SetCursorScreenPos(min);
        ImGui.Dummy(new Vector2(width, height));
    }

    private static string[] SyncTypeLabels =>
    [
        I18n.Get("config.cue.sync_type.cast"),
        I18n.Get("config.cue.sync_type.status"),
    ];

    private static string SyncTypeLabel(EnemySyncType syncType) =>
        syncType == EnemySyncType.Status
            ? I18n.Get("config.cue.sync_type.status")
            : I18n.Get("config.cue.sync_type.cast");

    private static string[] CastEdgeLabels =>
    [
        I18n.Get("config.cue.cast_edge.start"),
        I18n.Get("config.cue.cast_edge.effected"),
    ];

    private static string CastEdgeLabel(bool effected) =>
        effected
            ? I18n.Get("config.cue.cast_edge.effected")
            : I18n.Get("config.cue.cast_edge.start");

    private static string[] StatusEdgeLabels =>
    [
        I18n.Get("config.cue.status_edge.apply"),
        I18n.Get("config.cue.status_edge.remove"),
    ];

    private static string StatusEdgeLabel(bool removed) =>
        removed
            ? I18n.Get("config.cue.status_edge.remove")
            : I18n.Get("config.cue.status_edge.apply");
}
