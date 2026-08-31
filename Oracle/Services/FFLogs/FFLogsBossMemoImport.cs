using Oracle.Models;

namespace Oracle.Services.FFLogs;

/// <summary>
/// Builds memo cues from enemy DamageTaken hits. Independent of player-cast import.
/// </summary>
internal static class FFLogsBossMemoImport
{
    private const double ClusterGapMs = 1000.0;

    public static List<TimelineCue> BuildMemos(
        FFLogsFightInfo fight,
        IReadOnlyList<FFLogsDamageHit> hits)
    {
        var memos = new List<TimelineCue>();

        foreach (var group in hits.Where(IsUsableEnemyHit).GroupBy(h => h.AbilityGameId))
        {
            double? clusterStart = null;
            var clusterLast = 0.0;
            var clusterName = string.Empty;

            foreach (var hit in group.OrderBy(h => h.Timestamp))
            {
                var label = ResolveLabel(hit);
                if (clusterStart is null || hit.Timestamp - clusterLast > ClusterGapMs)
                {
                    if (clusterStart is double start)
                        memos.Add(ToMemo(fight, start, clusterName));

                    clusterStart = hit.Timestamp;
                    clusterName = label;
                }

                clusterLast = hit.Timestamp;
            }

            if (clusterStart is double remaining)
                memos.Add(ToMemo(fight, remaining, clusterName));
        }

        return memos;
    }

    private static bool IsUsableEnemyHit(FFLogsDamageHit hit)
    {
        if (hit.AbilityGameId == 0 || hit.Tick)
            return false;

        var type = hit.Type;
        if (!string.IsNullOrEmpty(type)
            && !string.Equals(type, "damage", StringComparison.OrdinalIgnoreCase))
            return false;

        if (hit.SourceIsFriendly == true)
            return false;
        if (hit.TargetIsFriendly == false)
            return false;

        return !IsAutoAttack(hit.AbilityName);
    }

    private static string ResolveLabel(FFLogsDamageHit hit) =>
        FFLogsDamageHit.IsUnusableName(hit.AbilityName)
            ? $"#{hit.AbilityGameId}"
            : hit.AbilityName.Trim();

    private static bool IsAutoAttack(string name) =>
        name.Equals("Attack", StringComparison.OrdinalIgnoreCase)
        || name.Equals("攻撃", StringComparison.Ordinal);

    private static TimelineCue ToMemo(FFLogsFightInfo fight, double timestampMs, string label) =>
        new()
        {
            TimeOffsetSec = fight.OffsetSec(timestampMs),
            Kind = TimelineCueKind.Memo,
            Label = label,
        };
}
