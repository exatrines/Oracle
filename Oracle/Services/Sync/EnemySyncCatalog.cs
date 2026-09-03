using LuminaAction = Lumina.Excel.Sheets.Action;
using LuminaStatus = Lumina.Excel.Sheets.Status;

namespace Oracle.Services;

// Cast / Status picker labels (Name#Id) from the Action and Status sheets.
internal static class EnemySyncCatalog
{
    private static List<string>? _castLabels;
    private static HashSet<uint>? _castIds;
    private static List<string>? _statusLabels;
    private static HashSet<uint>? _statusIds;

    public static bool TryParseLabel(string label, out uint id)
    {
        id = 0;
        if (string.IsNullOrWhiteSpace(label))
            return false;

        var hash = label.LastIndexOf('#');
        var idText = hash >= 0 ? label[(hash + 1)..] : label.Trim();
        return uint.TryParse(idText, out id) && id > 0;
    }

    public static IReadOnlyList<string> CastLabelsForPicker(uint selectedActionId)
    {
        EnsureCast();
        return WithSelected(_castLabels!, _castIds!, selectedActionId, FormatCastLabel(selectedActionId));
    }

    public static IReadOnlyList<string> StatusLabelsForPicker(uint selectedStatusId)
    {
        EnsureStatus();
        return WithSelected(_statusLabels!, _statusIds!, selectedStatusId, FormatStatusLabel(selectedStatusId));
    }

    public static string FormatCastLabel(uint actionId) =>
        FormatSheetLabel(actionId, ActionLookup.GetName(actionId));

    public static string FormatStatusLabel(uint statusId) =>
        FormatSheetLabel(statusId, ActionLookup.GetStatusName(statusId));

    private static string FormatSheetLabel(uint id, string name)
    {
        if (id == 0)
            return string.Empty;
        return name == $"#{id}" ? name : $"{name}#{id}";
    }

    private static IReadOnlyList<string> WithSelected(
        IReadOnlyList<string> labels,
        HashSet<uint> ids,
        uint selected,
        string formatted)
    {
        if (selected == 0 || ids.Contains(selected))
            return labels;

        var extra = new List<string>(labels.Count + 1) { formatted };
        extra.AddRange(labels);
        return extra;
    }

    private static bool TryPickerName(string? raw, out string name)
    {
        name = string.Empty;
        if (ActionLookup.IsPlaceholderName(raw))
            return false;

        name = raw!.Trim();
        return true;
    }

    private static void EnsureCast()
    {
        if (_castLabels != null)
            return;

        _castLabels = [];
        _castIds = [];
        try
        {
            var sheet = PluginServices.DataManager.GetExcelSheet<LuminaAction>();
            if (sheet == null)
                return;

            var rows = new List<(uint Id, string Name)>();
            foreach (var action in sheet)
            {
                if (!IsPickerAction(action, out var name))
                    continue;
                rows.Add((action.RowId, name));
            }

            Fill(rows, _castLabels, _castIds);
        }
        catch (Exception ex)
        {
            PluginServices.Log.Warning(ex, "Failed to build Cast Sync Action sheet list");
        }
    }

    private static void EnsureStatus()
    {
        if (_statusLabels != null)
            return;

        _statusLabels = [];
        _statusIds = [];
        try
        {
            var sheet = PluginServices.DataManager.GetExcelSheet<LuminaStatus>();
            if (sheet == null)
                return;

            var rows = new List<(uint Id, string Name)>();
            foreach (var status in sheet)
            {
                if (!IsPickerStatus(status, out var name))
                    continue;
                rows.Add((status.RowId, name));
            }

            Fill(rows, _statusLabels, _statusIds);
        }
        catch (Exception ex)
        {
            PluginServices.Log.Warning(ex, "Failed to build Status Sync sheet list");
        }
    }

    private static void Fill(List<(uint Id, string Name)> rows, List<string> labels, HashSet<uint> ids)
    {
        rows.Sort((a, b) => a.Id.CompareTo(b.Id));
        labels.Capacity = rows.Count;
        foreach (var row in rows)
        {
            labels.Add($"{row.Name}#{row.Id}");
            ids.Add(row.Id);
        }
    }

    private static bool IsPickerAction(LuminaAction action, out string name)
    {
        name = string.Empty;
        if (action.RowId == 0 || action.RowId is 7 or 8)
            return false;
        if (action.IsPvP || action.IsPlayerAction || action.Icon == 0)
            return false;
        if (action.ActionCategory.RowId is 0 or 1)
            return false;

        return TryPickerName(action.Name.ToString(), out name);
    }

    private static bool IsPickerStatus(LuminaStatus status, out string name)
    {
        name = string.Empty;
        if (status.RowId == 0 || status.Icon == 0)
            return false;

        return TryPickerName(status.Name.ToString(), out name);
    }
}
