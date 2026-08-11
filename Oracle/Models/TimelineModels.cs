using System.Text.Json.Serialization;

namespace Oracle.Models;

/// <summary>On-disk timeline JSON (one file per document under Config/Timelines).</summary>
public sealed class TimelineDocument
{
    // Identity & load command
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Untitled";

    /// <summary>
    /// When true, this timeline auto-loads when Zone / Job / SceneId match.
    /// </summary>
    public bool AutoLoadEnabled { get; set; } = true;

    /// <summary>
    /// Token for <c>/oracle load &lt;token&gt;</c>. Empty = use <see cref="Name"/>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string LoadCommand { get; set; } = string.Empty;

    // Auto-load match keys (zone + job required; scene optional)
    /// <summary>0 = not set (zone required).</summary>
    public uint TerritoryTypeId { get; set; }

    /// <summary>
    /// ContentFinderCondition row id for trial/raid selection. 0 = none (zone/level editable).
    /// </summary>
    public uint ContentFinderConditionId { get; set; }

    /// <summary>EnvManager scene (Splatoon-compatible). 0 = any.</summary>
    public uint SceneId { get; set; }

    /// <summary>ClassJob row id. 0 = not set (job required).</summary>
    public uint ClassJobId { get; set; }

    /// <summary>
    /// Job level for action catalog filtering only (not used for timeline matching).
    /// Often set from zone content; 0 = no level cap on actions.
    /// </summary>
    public byte ClassJobLevel { get; set; }

    public List<TimelineCue> Cues { get; set; } = [];
}

public enum TimelineCueKind
{
    Action = 0,
    Memo = 1,
}

public sealed class TimelineCue
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Relative to clock zero (pull). Negative = pre-pull.</summary>
    public float TimeOffsetSec { get; set; }

    /// <summary>Action button vs free-text memo in the Contents column.</summary>
    public TimelineCueKind Kind { get; set; } = TimelineCueKind.Action;

    public uint ActionId { get; set; }

    /// <summary>Memo text when <see cref="Kind"/> is Memo. Unused for Action (name from ActionId).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Label { get; set; } = string.Empty;
}
