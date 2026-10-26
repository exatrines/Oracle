using Oracle.Models;

namespace Oracle.Services;

// Named sync lists, one zone each. Missing built-in id in config uses default rows.
internal static class EnemySyncPresets
{
    private static readonly SyncPreset[] BuiltIns =
    [
        // Strength of the Ward start, Final Chorus, P4 entry cast, Incarnation start,
        // P6 entry cast, Alternative End.
        new()
        {
            Id = "dsr",
            Name = "DSR",
            TerritoryTypeId = 968,
            Entries =
            [
                Cast(25555, false),
                Cast(26377, true),
                Cast(29050, true),
                Cast(27526, false),
                Cast(26215, true),
                Cast(29752, true),
            ],
        },
        // Guard Program start, Hello World start, P4 entry cast, Blue Screen start,
        // Blind Faith knockdown apply.
        new()
        {
            Id = "top",
            Name = "TOP",
            TerritoryTypeId = 1122,
            Entries =
            [
                Cast(31552, false),
                Cast(31573, false),
                Cast(31559, true),
                Cast(31611, false),
                Status(2408, false),
            ],
        },
        // Quadruple Slap start, Junction complete, Materialization start, knockdown apply.
        new()
        {
            Id = "fru",
            Name = "FRU",
            TerritoryTypeId = 1238,
            Entries =
            [
                Cast(40191, false),
                Cast(40226, true),
                Cast(40246, false),
                Status(2408, false),
            ],
        },
        // Forsaken start, knockdown apply, Kefka Says start.
        new()
        {
            Id = "dancing-mad",
            Name = "Dancing Mad",
            TerritoryTypeId = 1363,
            Entries =
            [
                Cast(47804, false),
                Status(774, false),
                Cast(49884, false),
            ],
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
        FindBuiltIn(presetId) != null && FindStored(presetId) != null;

    public static string AddCustom()
    {
        var created = new SyncPreset
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = I18n.Get("settings.presets.default_name"),
            TerritoryTypeId = NamedZonePresets.FreeCurrentTerritory(
                NamedZonePresets.UsedTerritoriesExcept(Listed(), string.Empty)),
            Entries = [],
        };
        Stored().Add(created);
        C.Save();
        return created.Id;
    }

    public static void Remove(string presetId)
    {
        if (string.IsNullOrWhiteSpace(presetId) || FindBuiltIn(presetId) != null)
            return;

        if (Stored().RemoveAll(p => p != null && string.Equals(p.Id, presetId, StringComparison.OrdinalIgnoreCase)) == 0)
            return;

        C.Save();
    }

    public static void Reset(string presetId)
    {
        if (FindBuiltIn(presetId) == null)
            return;

        if (Stored().RemoveAll(p => p != null && string.Equals(p.Id, presetId, StringComparison.OrdinalIgnoreCase)) == 0)
            return;

        C.Save();
    }

    public static void SetName(string presetId, string name)
    {
        if (FindBuiltIn(presetId) != null)
            return;

        var stored = FindStored(presetId);
        if (stored == null)
            return;

        stored.Name = name ?? string.Empty;
        C.Save();
    }

    public static void SetTerritory(string presetId, uint territoryTypeId)
    {
        if (FindBuiltIn(presetId) != null)
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

    public static List<SyncPresetEntry> EditableSpecsFor(string presetId)
    {
        if (string.IsNullOrWhiteSpace(presetId))
            return [];

        var stored = FindStored(presetId);
        if (stored != null)
            return CopyEntries(stored.Entries);

        var builtIn = FindBuiltIn(presetId);
        return builtIn == null ? [] : CopyEntries(builtIn.Entries);
    }

    public static List<SyncPresetEntry> SpecsFor(uint territoryTypeId)
    {
        if (territoryTypeId == 0)
            return [];

        foreach (var preset in Listed())
        {
            if (preset.TerritoryTypeId != territoryTypeId)
                continue;
            return EditableSpecsFor(preset.Id).Where(s => s.ActionId != 0).ToList();
        }

        return [];
    }

    public static List<TimelineCue> TakeFirstCues(
        IEnumerable<TimelineCue> recorded,
        IReadOnlyList<SyncPresetEntry> specs)
    {
        if (specs.Count == 0)
            return [];

        var list = recorded.OrderBy(c => c.TimeOffsetSec).ToList();
        var cues = new List<TimelineCue>();
        foreach (var spec in specs)
        {
            if (spec.ActionId == 0)
                continue;

            var source = list.FirstOrDefault(c =>
                c.ActionId == spec.ActionId
                && c.Effected == spec.Effected
                && (spec.SyncType == EnemySyncType.Status ? c.IsStatusSync : c.IsCastSync));
            if (source == null)
                continue;

            cues.Add(new TimelineCue
            {
                TimeOffsetSec = source.TimeOffsetSec,
                Kind = TimelineCueKind.Sync,
                ActionId = spec.ActionId,
                SyncType = spec.SyncType,
                Effected = spec.Effected,
            });
        }

        return cues.OrderBy(c => c.TimeOffsetSec).ToList();
    }

    public static void Save(string presetId, IReadOnlyList<SyncPresetEntry> specs)
    {
        if (string.IsNullOrWhiteSpace(presetId))
            return;

        var entries = CopyEntries(specs);

        var stored = FindStored(presetId);
        if (stored != null)
        {
            stored.Entries = entries;
            C.Save();
            return;
        }

        var builtIn = FindBuiltIn(presetId);
        if (builtIn == null)
            return;

        Stored().Add(new SyncPreset
        {
            Id = builtIn.Id,
            Name = builtIn.Name,
            TerritoryTypeId = builtIn.TerritoryTypeId,
            Entries = entries,
        });
        C.Save();
    }

    public static void MigrateOnce()
    {
        C.SyncPresets ??= [];
        var source = C.SyncPresetsByTerritory;
        if (source == null || source.Count == 0)
            return;

        if (C.SyncPresets.Count == 0)
        {
            foreach (var pair in source)
            {
                if (pair.Key == 0)
                    continue;

                // Empty list is a stored override (do not use built-in rows).
                var entries = pair.Value ?? [];
                var builtIn = FindBuiltInByTerritory(pair.Key);
                C.SyncPresets.Add(new SyncPreset
                {
                    Id = builtIn?.Id ?? Guid.NewGuid().ToString("N"),
                    Name = builtIn?.Name ?? I18n.Get("settings.presets.default_name"),
                    TerritoryTypeId = pair.Key,
                    Entries = entries,
                });
            }
        }

        source.Clear();
        C.Save();
    }

    private static SyncPresetEntry Cast(uint actionId, bool effected) =>
        Entry(EnemySyncType.Cast, actionId, effected);

    private static SyncPresetEntry Status(uint actionId, bool effected) =>
        Entry(EnemySyncType.Status, actionId, effected);

    private static SyncPresetEntry Entry(EnemySyncType syncType, uint actionId, bool effected) =>
        new()
        {
            SyncType = syncType,
            ActionId = actionId,
            Effected = effected,
        };

    private static List<SyncPresetEntry> CopyEntries(IReadOnlyList<SyncPresetEntry>? entries)
    {
        if (entries == null || entries.Count == 0)
            return [];

        var copy = new List<SyncPresetEntry>(entries.Count);
        foreach (var entry in entries)
            copy.Add(Entry(entry.SyncType, entry.ActionId, entry.Effected));
        return copy;
    }

    private static SyncPreset? FindBuiltIn(string presetId)
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

    private static SyncPreset? FindBuiltInByTerritory(uint territoryTypeId)
    {
        foreach (var builtIn in BuiltIns)
        {
            if (builtIn.TerritoryTypeId == territoryTypeId)
                return builtIn;
        }

        return null;
    }

    private static List<SyncPreset> Stored() => C.SyncPresets;

    private static SyncPreset? FindStored(string presetId)
    {
        foreach (var stored in Stored())
        {
            if (stored != null && string.Equals(stored.Id, presetId, StringComparison.OrdinalIgnoreCase))
                return stored;
        }

        return null;
    }

}
