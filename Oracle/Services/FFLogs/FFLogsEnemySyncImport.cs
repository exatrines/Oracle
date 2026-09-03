using Oracle.Models;

namespace Oracle.Services.FFLogs;

// First matching enemy Cast / Status per preset. Cast Start falls back to an instant complete.
internal static class FFLogsEnemySyncImport
{
    public static List<TimelineCue> BuildCastCues(
        FFLogsFightInfo fight,
        IReadOnlyList<FFLogsCastEvent> casts,
        IReadOnlyList<EnemySyncSpec> specs)
    {
        if (specs.Count == 0)
            return [];

        var ordered = casts.OrderBy(c => c.Timestamp).ToList();
        var cues = new List<TimelineCue>();
        foreach (var spec in specs)
        {
            if (spec.SyncType != EnemySyncType.Cast || spec.ActionId == 0)
                continue;

            var ev = spec.Effected
                ? FindFirstComplete(ordered, spec.ActionId)
                : FindFirstStart(ordered, spec.ActionId);
            if (ev == null)
                continue;

            cues.Add(new TimelineCue
            {
                TimeOffsetSec = fight.OffsetSec(ev.Timestamp),
                Kind = TimelineCueKind.Sync,
                ActionId = spec.ActionId,
                Effected = spec.Effected,
            });
        }

        return cues.OrderBy(c => c.TimeOffsetSec).ToList();
    }

    public static List<TimelineCue> BuildStatusCues(
        FFLogsFightInfo fight,
        IReadOnlyList<FFLogsStatusEvent> events,
        IReadOnlyList<EnemySyncSpec> specs)
    {
        if (specs.Count == 0)
            return [];

        var ordered = events.OrderBy(e => e.Timestamp).ToList();
        var cues = new List<TimelineCue>();
        foreach (var spec in specs)
        {
            if (spec.SyncType != EnemySyncType.Status || spec.ActionId == 0)
                continue;

            var ev = FindFirstStatus(ordered, spec.ActionId, spec.Effected);
            if (ev == null)
                continue;

            cues.Add(new TimelineCue
            {
                TimeOffsetSec = fight.OffsetSec(ev.Timestamp),
                Kind = TimelineCueKind.Sync,
                ActionId = spec.ActionId,
                SyncType = EnemySyncType.Status,
                Effected = spec.Effected,
            });
        }

        return cues.OrderBy(c => c.TimeOffsetSec).ToList();
    }

    private static FFLogsCastEvent? FindFirstStart(IReadOnlyList<FFLogsCastEvent> casts, uint abilityId)
    {
        FFLogsCastEvent? completeFallback = null;
        foreach (var ev in casts)
        {
            if (ev.AbilityGameId != abilityId || !IsUsableEnemyCast(ev))
                continue;
            if (IsBeginCast(ev))
                return ev;
            if (completeFallback == null && IsCastComplete(ev))
                completeFallback = ev;
        }

        return completeFallback;
    }

    private static FFLogsCastEvent? FindFirstComplete(IReadOnlyList<FFLogsCastEvent> casts, uint abilityId)
    {
        foreach (var ev in casts)
        {
            if (ev.AbilityGameId != abilityId || !IsUsableEnemyCast(ev))
                continue;
            if (IsCastComplete(ev) && !IsBeginCast(ev))
                return ev;
        }

        return null;
    }

    private static FFLogsStatusEvent? FindFirstStatus(
        IReadOnlyList<FFLogsStatusEvent> events,
        uint statusId,
        bool removed)
    {
        foreach (var ev in events)
        {
            if (ev.StatusId != statusId || ev.Removed != removed)
                continue;
            if (ev.SourceIsFriendly == true)
                continue;
            return ev;
        }

        return null;
    }

    private static bool IsUsableEnemyCast(FFLogsCastEvent ev) =>
        ev.AbilityGameId is not (7 or 8)
        && (IsBeginCast(ev) || IsCastComplete(ev));

    private static bool IsBeginCast(FFLogsCastEvent ev) =>
        string.Equals(ev.Type, "begincast", StringComparison.OrdinalIgnoreCase);

    private static bool IsCastComplete(FFLogsCastEvent ev) =>
        string.IsNullOrEmpty(ev.Type)
        || string.Equals(ev.Type, "cast", StringComparison.OrdinalIgnoreCase);
}
