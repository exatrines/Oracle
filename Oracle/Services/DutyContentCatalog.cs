using Lumina.Excel.Sheets;

namespace Oracle.Services;

internal readonly record struct DutyContentInfo(
    uint ContentId,
    string Name,
    uint TerritoryTypeId,
    byte ClassJobLevel);

internal readonly record struct ZoneOption(
    string Label,
    uint TerritoryTypeId,
    uint ContentFinderConditionId,
    byte ClassJobLevel);

/// <summary>
/// All named territories for the zone picker.
/// When a ContentFinderCondition maps to the territory, the label uses the content name + level.
/// </summary>
internal static class DutyContentCatalog
{
    private static IReadOnlyList<DutyContentInfo>? _duties;
    private static IReadOnlyList<ZoneOption>? _zoneOptions;

    private static IReadOnlyList<DutyContentInfo> GetDuties()
    {
        if (_duties != null)
            return _duties;

        try
        {
            var sheet = PluginServices.DataManager.GetExcelSheet<ContentFinderCondition>();
            if (sheet == null)
                return _duties = [];

            var list = new List<DutyContentInfo>();
            foreach (var row in sheet)
            {
                if (row.RowId == 0 || row.PvP)
                    continue;

                var name = row.Name.ToString();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var territoryId = row.TerritoryType.RowId;
                if (territoryId == 0)
                    continue;

                var level = row.ClassJobLevelSync != 0
                    ? row.ClassJobLevelSync
                    : row.ClassJobLevelRequired;

                list.Add(new DutyContentInfo(
                    row.RowId,
                    name,
                    territoryId,
                    level));
            }

            _duties = list
                .OrderBy(d => d.TerritoryTypeId)
                .ThenBy(d => d.ClassJobLevel)
                .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            PluginServices.Log.Warning(ex, "Failed to load duty content catalog");
            _duties = [];
        }

        return _duties;
    }

    public static IReadOnlyList<ZoneOption> GetZoneOptions()
    {
        if (_zoneOptions != null)
            return _zoneOptions;

        var dutiesByTerritory = GetDuties()
            .GroupBy(d => d.TerritoryTypeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var options = new List<ZoneOption>();
        var seenTerritories = new HashSet<uint>();

        try
        {
            var sheet = PluginServices.DataManager.GetExcelSheet<TerritoryType>();
            if (sheet != null)
            {
                foreach (var row in sheet)
                {
                    if (row.RowId == 0)
                        continue;

                    var fieldName = ResolveTerritoryName(row);
                    if (string.IsNullOrWhiteSpace(fieldName))
                        continue;

                    seenTerritories.Add(row.RowId);

                    if (dutiesByTerritory.TryGetValue(row.RowId, out var duties))
                    {
                        foreach (var duty in duties)
                        {
                            options.Add(new ZoneOption(
                                FormatZoneOption(duty),
                                duty.TerritoryTypeId,
                                duty.ContentId,
                                duty.ClassJobLevel));
                        }
                    }
                    else
                    {
                        options.Add(new ZoneOption(
                            FormatFieldOption(row.RowId, fieldName),
                            row.RowId,
                            0,
                            0));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            PluginServices.Log.Warning(ex, "Failed to load territory zone options");
        }

        // Duties whose territory lacked PlaceName still appear.
        foreach (var duty in GetDuties())
        {
            if (seenTerritories.Contains(duty.TerritoryTypeId))
                continue;

            options.Add(new ZoneOption(
                FormatZoneOption(duty),
                duty.TerritoryTypeId,
                duty.ContentId,
                duty.ClassJobLevel));
        }

        _zoneOptions = options
            .OrderBy(o => o.TerritoryTypeId)
            .ThenBy(o => o.ClassJobLevel)
            .ThenBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return _zoneOptions;
    }

    private static bool TryGetDuty(uint contentId, out DutyContentInfo duty)
    {
        duty = default;
        if (contentId == 0)
            return false;

        foreach (var d in GetDuties())
        {
            if (d.ContentId != contentId)
                continue;
            duty = d;
            return true;
        }

        return false;
    }

    public static bool TryGetZoneOption(string label, out ZoneOption option)
    {
        option = default;
        if (string.IsNullOrWhiteSpace(label))
            return false;

        foreach (var o in GetZoneOptions())
        {
            if (!string.Equals(o.Label, label, StringComparison.Ordinal))
                continue;
            option = o;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Prefer ContentFinderCondition rows, then higher level, for a territory.
    /// Falls back to a field label when no catalog option exists.
    /// </summary>
    public static bool TryResolvePreferredForTerritory(uint territoryTypeId, out ZoneOption option) =>
        TryResolveZoneFromTerritory(territoryTypeId, preferName: null, out option);

    public static bool TryResolveZoneFromTerritory(
        uint territoryTypeId,
        string? preferName,
        out ZoneOption option)
    {
        option = default;
        if (territoryTypeId == 0)
            return false;

        var matches = GetZoneOptions()
            .Where(o => o.TerritoryTypeId == territoryTypeId)
            .ToList();
        if (matches.Count == 0)
        {
            var label = ResolveZoneLabel(territoryTypeId, 0, 0);
            if (string.IsNullOrWhiteSpace(label))
                return false;

            option = new ZoneOption(label, territoryTypeId, 0, 0);
            return true;
        }

        if (!string.IsNullOrWhiteSpace(preferName))
        {
            var key = preferName.Trim();
            var byName = matches
                .Where(o => LabelMatchesName(o.Label, key))
                .OrderByDescending(o => o.ContentFinderConditionId != 0)
                .ThenByDescending(o => o.ClassJobLevel)
                .FirstOrDefault();
            if (byName.TerritoryTypeId != 0)
            {
                option = byName;
                return true;
            }
        }

        option = matches
            .OrderByDescending(o => o.ContentFinderConditionId != 0)
            .ThenByDescending(o => o.ClassJobLevel)
            .ThenBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .First();
        return option.TerritoryTypeId != 0;
    }

    private static bool LabelMatchesName(string label, string name)
    {
        if (label.Contains(name, StringComparison.OrdinalIgnoreCase))
            return true;

        var pipe = label.IndexOf('|');
        var body = pipe >= 0 ? label[(pipe + 1)..] : label;
        var levelMarker = I18n.Get("zone.level_marker");
        var dash = body.LastIndexOf(levelMarker, StringComparison.OrdinalIgnoreCase);
        if (dash > 0)
            body = body[..dash];
        body = body.Trim();
        return body.Length > 0
               && (name.Contains(body, StringComparison.OrdinalIgnoreCase)
                   || body.Contains(name, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatZoneOption(DutyContentInfo duty) =>
        I18n.Format("zone.option", duty.TerritoryTypeId, duty.Name, duty.ClassJobLevel);

    private static string FormatFieldOption(uint territoryTypeId, string fieldName) =>
        $"{territoryTypeId} | {fieldName}";

    public static string ResolveZoneLabel(uint territoryTypeId, uint contentFinderConditionId, byte classJobLevel)
    {
        if (territoryTypeId == 0)
            return string.Empty;

        if (TryResolveDuty(territoryTypeId, contentFinderConditionId, classJobLevel, out var duty))
            return FormatZoneOption(duty);

        var field = ResolveTerritoryNameById(territoryTypeId);
        return string.IsNullOrWhiteSpace(field)
            ? $"{territoryTypeId} |"
            : FormatFieldOption(territoryTypeId, field);
    }

    public static string ResolveContentName(uint territoryTypeId, uint contentFinderConditionId, byte classJobLevel)
    {
        if (TryResolveDuty(territoryTypeId, contentFinderConditionId, classJobLevel, out var duty))
            return duty.Name;

        return territoryTypeId == 0
            ? string.Empty
            : ResolveTerritoryNameById(territoryTypeId);
    }

    private static bool TryResolveDuty(
        uint territoryTypeId,
        uint contentFinderConditionId,
        byte classJobLevel,
        out DutyContentInfo duty)
    {
        if (TryGetDuty(contentFinderConditionId, out duty)
            && duty.TerritoryTypeId == territoryTypeId)
            return true;

        foreach (var d in GetDuties())
        {
            if (d.TerritoryTypeId != territoryTypeId)
                continue;
            if (classJobLevel != 0 && d.ClassJobLevel != classJobLevel)
                continue;
            duty = d;
            return true;
        }

        duty = default;
        return false;
    }

    public static string StripZoneLabelPrefix(string label)
    {
        var pipe = label.IndexOf('|');
        return pipe < 0 ? label : label[(pipe + 1)..].Trim();
    }

    private static string ResolveTerritoryNameById(uint territoryTypeId)
    {
        try
        {
            var row = PluginServices.DataManager.GetExcelSheet<TerritoryType>()?.GetRowOrDefault(territoryTypeId);
            return ResolveTerritoryName(row);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ResolveTerritoryName(TerritoryType? territory)
    {
        if (territory == null)
            return string.Empty;

        var place = territory.Value.PlaceName.ValueNullable?.Name.ToString();
        if (!string.IsNullOrWhiteSpace(place))
            return place;

        var raw = territory.Value.Name.ToString();
        return string.IsNullOrWhiteSpace(raw) ? string.Empty : raw;
    }
}
