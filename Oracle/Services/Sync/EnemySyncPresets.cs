using Oracle.Models;

namespace Oracle.Services;

internal readonly record struct EnemySyncSpec(EnemySyncType SyncType, uint ActionId, bool Effected);

// Per-zone sync rows. Missing config uses built-in; stored list (even empty) is the override.
internal static class EnemySyncPresets
{
    private const uint DsrTerritoryTypeId = 968;
    private const uint TopTerritoryTypeId = 1122;
    private const uint FruTerritoryTypeId = 1238;
    private const uint DancingMadTerritoryTypeId = 1363;

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

    public static bool HasBuiltIn(uint territoryTypeId) =>
        BuiltInFor(territoryTypeId).Count > 0;

    public static IReadOnlyList<EnemySyncSpec> BuiltInFor(uint territoryTypeId) =>
        territoryTypeId switch
        {
            DsrTerritoryTypeId => Dsr,
            TopTerritoryTypeId => Top,
            FruTerritoryTypeId => Fru,
            DancingMadTerritoryTypeId => DancingMad,
            _ => [],
        };

    public static List<EnemySyncSpec> EditableSpecsFor(uint territoryTypeId)
    {
        if (territoryTypeId == 0)
            return [];

        if (C.TryGetSyncPresetOverride(territoryTypeId, out var entries))
            return ToSpecs(entries);

        return BuiltInFor(territoryTypeId).ToList();
    }

    public static List<EnemySyncSpec> SpecsFor(uint territoryTypeId) =>
        EditableSpecsFor(territoryTypeId).Where(s => s.ActionId != 0).ToList();

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

    public static void Save(uint territoryTypeId, IReadOnlyList<EnemySyncSpec> specs)
    {
        C.SetSyncPresets(
            territoryTypeId,
            specs.Select(s => new SyncPresetEntry
            {
                SyncType = s.SyncType,
                ActionId = s.ActionId,
                Effected = s.Effected,
            }));
    }

    private static List<EnemySyncSpec> ToSpecs(List<SyncPresetEntry> entries)
    {
        var specs = new List<EnemySyncSpec>(entries.Count);
        foreach (var entry in entries)
            specs.Add(new EnemySyncSpec(entry.SyncType, entry.ActionId, entry.Effected));
        return specs;
    }
}
