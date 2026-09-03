using Oracle.Models;

namespace Oracle.Services.FFLogs;

/// <summary>
/// Builds memo cues from enemy DamageTaken hits. Independent of player-cast import.
/// </summary>
internal static class FFLogsBossMemoImport
{
    public static List<TimelineCue> BuildMemos(
        FFLogsFightInfo fight,
        IReadOnlyList<FFLogsDamageHit> hits)
    {
        var points = hits
            .Where(IsUsableEnemyHit)
            .Select(hit => (
                Time: hit.Timestamp,
                ActionId: hit.AbilityGameId,
                Label: ResolveLabel(hit)));

        return HitMemoCluster.Cluster(points, HitMemoCluster.GapSec * 1000.0)
            .Select(c => ToMemo(fight, c.Time, c.Label))
            .ToList();
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

        return !EnemyHitRules.IsAutoAttackName(hit.AbilityName);
    }

    private static string ResolveLabel(FFLogsDamageHit hit) =>
        ActionLookup.IsPlaceholderName(hit.AbilityName)
            ? $"#{hit.AbilityGameId}"
            : hit.AbilityName.Trim();

    private static TimelineCue ToMemo(FFLogsFightInfo fight, double timestampMs, string label) =>
        new()
        {
            TimeOffsetSec = fight.OffsetSec(timestampMs),
            Kind = TimelineCueKind.Memo,
            Label = label,
        };
}
