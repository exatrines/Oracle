using Oracle.Models;

namespace Oracle.Services;

internal readonly record struct EnemySyncSpec(EnemySyncType SyncType, uint ActionId, bool Effected);

// Named sync lists, one zone each. Missing built-in id in config uses default rows.
internal static class EnemySyncPresets
{
    private const uint DsrTerritoryTypeId = 968;
    private const uint TopTerritoryTypeId = 1122;
    private const uint FruTerritoryTypeId = 1238;
    private const uint DancingMadTerritoryTypeId = 1363;

    private const string DsrId = "dsr";
    private const string TopId = "top";
    private const string FruId = "fru";
    private const string DancingMadId = "dancing-mad";

    // DSR: Strength of the Ward start, Final Chorus, Resentment, Incarnation start,
    // Wroth Flames start, Alternative End.
    private static readonly EnemySyncSpec[] Dsr =
    [
        new(EnemySyncType.Cast, 25555, false),
        new(EnemySyncType.Cast, 26377, true),
        new(EnemySyncType.Cast, 26810, true),
        new(EnemySyncType.Cast, 27526, false),
        new(EnemySyncType.Cast, 27973, false),
        new(EnemySyncType.Cast, 29752, true),
    ];

    // TOP: Guard Program start, Hello World start, Blue Screen start, Blind Faith knockdown apply.
    private static readonly EnemySyncSpec[] Top =
    [
        new(EnemySyncType.Cast, 31552, false),
        new(EnemySyncType.Cast, 31573, false),
        new(EnemySyncType.Cast, 31611, false),
        new(EnemySyncType.Status, 2408, false),
    ];

    // FRU: Quadruple Slap start, Junction complete, Materialization start, knockdown apply.
    private static readonly EnemySyncSpec[] Fru =
    [
        new(EnemySyncType.Cast, 40191, false),
        new(EnemySyncType.Cast, 40226, true),
        new(EnemySyncType.Cast, 40246, false),
        new(EnemySyncType.Status, 2408, false),
    ];

    // DMU: Forsaken start, knockdown apply, Kefka Says start.
    private static readonly EnemySyncSpec[] DancingMad =
    [
        new(EnemySyncType.Cast, 47804, false),
        new(EnemySyncType.Status, 774, false),
        new(EnemySyncType.Cast, 49884, false),
    ];

    private static readonly (string Id, string Name, uint Territory, EnemySyncSpec[] Specs)[] BuiltIns =
    [
        (DsrId, "DSR", DsrTerritoryTypeId, Dsr),
        (TopId, "TOP", TopTerritoryTypeId, Top),
        (FruId, "FRU", FruTerritoryTypeId, Fru),
        (DancingMadId, "Dancing Mad", DancingMadTerritoryTypeId, DancingMad),
    ];

    public static IReadOnlyList<NamedZonePreset> Listed()
    {
        var builtIns = new List<NamedZonePreset>(BuiltIns.Length);
        foreach (var builtIn in BuiltIns)
            builtIns.Add(new NamedZonePreset(builtIn.Id, builtIn.Name, builtIn.Territory, true));

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
        if (string.IsNullOrWhiteSpace(presetId) || IsBuiltInId(presetId))
            return;

        var stored = Stored();
        if (stored.RemoveAll(p => p != null && string.Equals(p.Id, presetId, StringComparison.OrdinalIgnoreCase)) == 0)
            return;

        C.Save();
    }

    public static void Reset(string presetId)
    {
        if (!IsBuiltInId(presetId))
            return;

        var stored = Stored();
        if (stored.RemoveAll(p => p != null && string.Equals(p.Id, presetId, StringComparison.OrdinalIgnoreCase)) == 0)
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

    public static List<EnemySyncSpec> EditableSpecsFor(string presetId)
    {
        if (string.IsNullOrWhiteSpace(presetId))
            return [];

        var stored = FindStored(presetId);
        if (stored != null)
            return ToSpecs(stored.Entries ?? []);

        foreach (var builtIn in BuiltIns)
        {
            if (string.Equals(builtIn.Id, presetId, StringComparison.OrdinalIgnoreCase))
                return builtIn.Specs.ToList();
        }

        return [];
    }

    public static List<EnemySyncSpec> SpecsFor(uint territoryTypeId)
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
        IReadOnlyList<EnemySyncSpec> specs)
    {
        if (specs.Count == 0)
            return [];

        var list = recorded.OrderBy(c => c.TimeOffsetSec).ToList();
        var cues = new List<TimelineCue>();
        foreach (var spec in specs)
        {
            if (spec.ActionId == 0)
                continue;

            var source = spec.SyncType == EnemySyncType.Status
                ? list.FirstOrDefault(c =>
                    c.IsStatusSync
                    && c.ActionId == spec.ActionId
                    && c.Effected == spec.Effected)
                : list.FirstOrDefault(c =>
                    c.IsCastSync
                    && c.ActionId == spec.ActionId
                    && c.Effected == spec.Effected);
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

    public static void Save(string presetId, IReadOnlyList<EnemySyncSpec> specs)
    {
        if (string.IsNullOrWhiteSpace(presetId))
            return;

        var entries = specs.Select(s => new SyncPresetEntry
        {
            SyncType = s.SyncType,
            ActionId = s.ActionId,
            Effected = s.Effected,
        }).ToList();

        var stored = FindStored(presetId);
        if (stored != null)
        {
            stored.Entries = entries;
            C.Save();
            return;
        }

        foreach (var builtIn in BuiltIns)
        {
            if (!string.Equals(builtIn.Id, presetId, StringComparison.OrdinalIgnoreCase))
                continue;

            Stored().Add(new SyncPreset
            {
                Id = builtIn.Id,
                Name = builtIn.Name,
                TerritoryTypeId = builtIn.Territory,
                Entries = entries,
            });
            C.Save();
            return;
        }
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
                string id;
                string name;
                var builtIn = FindBuiltInByTerritory(pair.Key);
                if (builtIn != null)
                {
                    id = builtIn.Value.Id;
                    name = builtIn.Value.Name;
                }
                else
                {
                    id = Guid.NewGuid().ToString("N");
                    name = I18n.Get("settings.presets.default_name");
                }

                C.SyncPresets.Add(new SyncPreset
                {
                    Id = id,
                    Name = name,
                    TerritoryTypeId = pair.Key,
                    Entries = entries,
                });
            }
        }

        source.Clear();
        C.Save();
    }

    private static bool IsBuiltInId(string presetId) =>
        FindBuiltIn(presetId) != null;

    private static (string Id, string Name, uint Territory, EnemySyncSpec[] Specs)? FindBuiltIn(string presetId)
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

    private static (string Id, string Name, uint Territory, EnemySyncSpec[] Specs)? FindBuiltInByTerritory(uint territoryTypeId)
    {
        foreach (var builtIn in BuiltIns)
        {
            if (builtIn.Territory == territoryTypeId)
                return builtIn;
        }

        return null;
    }

    private static List<EnemySyncSpec> ToSpecs(List<SyncPresetEntry> entries)
    {
        var specs = new List<EnemySyncSpec>(entries.Count);
        foreach (var entry in entries)
            specs.Add(new EnemySyncSpec(entry.SyncType, entry.ActionId, entry.Effected));
        return specs;
    }
}
