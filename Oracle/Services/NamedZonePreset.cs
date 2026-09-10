namespace Oracle.Services;

internal readonly record struct NamedZonePreset(
    string Id,
    string Name,
    uint TerritoryTypeId,
    bool IsBuiltIn);

/// <summary>Shared list helpers for named, one-zone presets.</summary>
internal static class NamedZonePresets
{
    public static IReadOnlyList<NamedZonePreset> Merge(
        IReadOnlyList<NamedZonePreset> builtIns,
        IEnumerable<NamedZonePreset> customs)
    {
        var list = new List<NamedZonePreset>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var builtIn in builtIns)
        {
            if (string.IsNullOrWhiteSpace(builtIn.Id) || !seen.Add(builtIn.Id))
                continue;
            list.Add(builtIn);
        }

        foreach (var custom in customs)
        {
            if (string.IsNullOrWhiteSpace(custom.Id) || !seen.Add(custom.Id))
                continue;
            list.Add(custom);
        }

        return list;
    }

    public static HashSet<uint> UsedTerritoriesExcept(IReadOnlyList<NamedZonePreset> listed, string presetId)
    {
        var used = new HashSet<uint>();
        foreach (var preset in listed)
        {
            if (preset.TerritoryTypeId == 0)
                continue;
            if (string.Equals(preset.Id, presetId, StringComparison.OrdinalIgnoreCase))
                continue;
            used.Add(preset.TerritoryTypeId);
        }

        return used;
    }

    public static uint FreeCurrentTerritory(IReadOnlySet<uint> used)
    {
        var territory = PluginServices.ClientState.TerritoryType;
        if (territory == 0 || used.Contains(territory))
            return 0;
        return territory;
    }
}
