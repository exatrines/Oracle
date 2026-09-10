using Oracle.Models;

namespace Oracle.Services;

/// <summary>Named Auto Load boss sets, one zone each. Missing built-in id in config uses default.</summary>
internal static class AutoLoadPresets
{
    private const uint DsrTerritoryTypeId = 968;
    private const uint M12sTerritoryTypeId = 1327;
    private const string DsrId = "dsr-p2";
    private const string M12sId = "m12s-p2";

    private static readonly AutoLoadPresetEntry[] BuiltIns =
    [
        new()
        {
            Id = DsrId,
            Name = "DSR P2",
            TerritoryTypeId = DsrTerritoryTypeId,
            DataIds = [0x313C],
        },
        new()
        {
            Id = M12sId,
            Name = "M12S P2",
            TerritoryTypeId = M12sTerritoryTypeId,
            DataIds = [0x4B02],
        },
    ];

    public static IReadOnlyList<NamedZonePreset> Listed()
    {
        var builtIns = new List<NamedZonePreset>(BuiltIns.Length);
        foreach (var builtIn in BuiltIns)
            builtIns.Add(new NamedZonePreset(builtIn.Id, builtIn.Name, builtIn.TerritoryTypeId, true));

        var customs = new List<NamedZonePreset>();
        foreach (var stored in Stored())
        {
            if (stored == null)
                continue;
            customs.Add(new NamedZonePreset(
                stored.Id ?? string.Empty,
                stored.Name ?? string.Empty,
                stored.TerritoryTypeId,
                false));
        }

        return NamedZonePresets.Merge(builtIns, customs);
    }

    public static bool HasOverride(string presetId) =>
        IsBuiltInId(presetId) && FindStored(presetId) != null;

    public static string AddCustom()
    {
        var created = new AutoLoadPresetEntry
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = I18n.Get("settings.presets.default_name"),
            TerritoryTypeId = NamedZonePresets.FreeCurrentTerritory(
                NamedZonePresets.UsedTerritoriesExcept(Listed(), string.Empty)),
            DataIds = [],
        };
        Stored().Add(created);
        C.Save();
        return created.Id;
    }

    public static void Remove(string presetId)
    {
        if (string.IsNullOrWhiteSpace(presetId) || IsBuiltInId(presetId))
            return;

        if (Stored().RemoveAll(p => p != null && string.Equals(p.Id, presetId, StringComparison.OrdinalIgnoreCase)) == 0)
            return;

        C.Save();
    }

    public static void Reset(string presetId)
    {
        if (!IsBuiltInId(presetId))
            return;

        if (Stored().RemoveAll(p => p != null && string.Equals(p.Id, presetId, StringComparison.OrdinalIgnoreCase)) == 0)
            return;

        C.Save();
    }

    public static void SetName(string presetId, string name)
    {
        if (IsBuiltInId(presetId))
            return;

        var stored = FindStored(presetId);
        if (stored == null)
            return;

        stored.Name = name ?? string.Empty;
        C.Save();
    }

    public static void SetTerritory(string presetId, uint territoryTypeId)
    {
        if (IsBuiltInId(presetId))
            return;

        var stored = FindStored(presetId);
        if (stored == null)
            return;

        if (territoryTypeId != 0
            && NamedZonePresets.UsedTerritoriesExcept(Listed(), presetId).Contains(territoryTypeId))
            return;

        stored.TerritoryTypeId = territoryTypeId;
        C.Save();
    }

    public static AutoLoadPresetEntry? FindForTerritory(uint territoryTypeId)
    {
        if (territoryTypeId == 0)
            return null;

        foreach (var preset in Listed())
        {
            if (preset.TerritoryTypeId != territoryTypeId)
                continue;
            return Resolve(preset.Id);
        }

        return null;
    }

    public static AutoLoadPresetEntry? Find(uint territoryTypeId, string presetId)
    {
        if (territoryTypeId == 0 || string.IsNullOrWhiteSpace(presetId))
            return null;

        var entry = Resolve(presetId);
        if (entry == null || entry.TerritoryTypeId != territoryTypeId)
            return null;

        return entry;
    }

    public static IReadOnlyList<uint> DataIdsFor(AutoLoadPresetEntry entry)
    {
        if (entry.DataIds == null || entry.DataIds.Count == 0)
            return [];

        return entry.DataIds.Where(id => id != 0).Distinct().ToList();
    }

    public static AutoLoadPresetEntry? Resolve(string presetId)
    {
        if (string.IsNullOrWhiteSpace(presetId))
            return null;

        var stored = FindStored(presetId);
        if (stored != null)
            return CloneOne(stored);

        var builtIn = FindBuiltIn(presetId);
        return builtIn == null ? null : CloneOne(builtIn);
    }

    public static void Save(string presetId, IReadOnlyList<uint> dataIds)
    {
        if (string.IsNullOrWhiteSpace(presetId))
            return;

        var ids = dataIds == null ? new List<uint>() : [.. dataIds];
        var stored = FindStored(presetId);
        if (stored != null)
        {
            stored.DataIds = ids;
            C.Save();
            return;
        }

        var builtIn = FindBuiltIn(presetId);
        if (builtIn == null)
            return;

        Stored().Add(new AutoLoadPresetEntry
        {
            Id = builtIn.Id,
            Name = builtIn.Name,
            TerritoryTypeId = builtIn.TerritoryTypeId,
            DataIds = ids,
        });
        C.Save();
    }

    private static bool IsBuiltInId(string presetId) =>
        FindBuiltIn(presetId) != null;

    private static AutoLoadPresetEntry? FindBuiltIn(string presetId)
    {
        if (string.IsNullOrWhiteSpace(presetId))
            return null;

        foreach (var builtIn in BuiltIns)
        {
            if (string.Equals(builtIn.Id, presetId, StringComparison.OrdinalIgnoreCase))
                return builtIn;
        }

        return null;
    }

    private static List<AutoLoadPresetEntry> Stored() => C.AutoLoadPresets;

    private static AutoLoadPresetEntry? FindStored(string presetId)
    {
        foreach (var stored in Stored())
        {
            if (stored != null && string.Equals(stored.Id, presetId, StringComparison.OrdinalIgnoreCase))
                return stored;
        }

        return null;
    }

    private static AutoLoadPresetEntry CloneOne(AutoLoadPresetEntry entry) =>
        new()
        {
            Id = string.IsNullOrWhiteSpace(entry.Id) ? Guid.NewGuid().ToString("N") : entry.Id,
            Name = entry.Name ?? string.Empty,
            TerritoryTypeId = entry.TerritoryTypeId,
            DataIds = entry.DataIds == null ? [] : [.. entry.DataIds],
        };
}
