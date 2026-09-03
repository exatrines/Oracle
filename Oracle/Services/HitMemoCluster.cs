namespace Oracle.Services;

/// <summary>1s-gap first-hit clustering for enemy hits.</summary>
internal static class HitMemoCluster
{
    public const float GapSec = 1f;

    public static List<(double Time, uint ActionId, string Label)> Cluster(
        IEnumerable<(double Time, uint ActionId, string Label)> hits,
        double gap)
    {
        var clustered = new List<(double Time, uint ActionId, string Label)>();
        foreach (var group in hits.GroupBy(h => h.ActionId))
        {
            double? clusterStart = null;
            var clusterLast = 0.0;
            var clusterActionId = 0u;
            var clusterLabel = string.Empty;

            foreach (var hit in group.OrderBy(h => h.Time))
            {
                if (clusterStart is null || hit.Time - clusterLast > gap)
                {
                    if (clusterStart is double start)
                        clustered.Add((start, clusterActionId, clusterLabel));

                    clusterStart = hit.Time;
                    clusterActionId = hit.ActionId;
                    clusterLabel = hit.Label;
                }

                clusterLast = hit.Time;
            }

            if (clusterStart is double remaining)
                clustered.Add((remaining, clusterActionId, clusterLabel));
        }

        return clustered;
    }
}
