namespace Oracle.Services.FFLogs;

internal sealed class FFLogsFightInfo
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public double StartTime { get; init; }
    public double EndTime { get; init; }
    public bool Kill { get; init; }
    public IReadOnlyList<int> FriendlyPlayers { get; init; } = [];

    /// <summary>FFLogs <c>gameZone.id</c> (FFXIV TerritoryType id when present).</summary>
    public int GameZoneId { get; init; }

    public string GameZoneName { get; init; } = string.Empty;

    internal float OffsetSec(double timestampMs) =>
        MathF.Round((float)((timestampMs - StartTime) / 1000.0));
}

internal sealed class FFLogsActorInfo
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string SubType { get; init; } = string.Empty;
    public string Server { get; init; } = string.Empty;
}

internal sealed class FFLogsCastEvent
{
    public double Timestamp { get; init; }
    public uint AbilityGameId { get; init; }
    public int TargetId { get; init; }
}

internal sealed class FFLogsReportMeta
{
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<FFLogsFightInfo> Fights { get; init; } = [];
    public IReadOnlyList<FFLogsActorInfo> Players { get; init; } = [];
}

/// <summary>One FFLogs DamageTaken event. Clustering lives in <see cref="FFLogsBossMemoImport"/>.</summary>
internal sealed record FFLogsDamageHit
{
    public double Timestamp { get; init; }
    public uint AbilityGameId { get; init; }
    public string AbilityName { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public bool Tick { get; init; }
    public bool? SourceIsFriendly { get; init; }
    public bool? TargetIsFriendly { get; init; }

    internal static bool IsUnusableName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return true;

        var trimmed = name.Trim();
        if (trimmed.StartsWith('#'))
            return true;
        if (trimmed.StartsWith("_rsv_", StringComparison.OrdinalIgnoreCase))
            return true;
        if (trimmed.StartsWith("unknown_", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
