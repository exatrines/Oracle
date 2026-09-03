using System.Text.Json.Serialization;

namespace Oracle.Models;

/// <summary>On-disk timeline JSON (one file per document under Config/Timelines).</summary>
public sealed class TimelineDocument
{
    /// <summary>Wire format. Missing on disk is read as 1; this release writes 1.</summary>
    public int FormatVersion { get; set; } = 1;

    // Identity & load command
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Untitled";

    /// <summary>
    /// When true, this timeline auto-loads when Zone / Job / Scene match
    /// (Scene filter applies out of combat only; the clock never auto-switches docs).
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

    /// <summary>
    /// When false, Auto Load ignores scene (any). When true, requires <see cref="SceneId"/>
    /// (0 is a valid scene id).
    /// </summary>
    public bool SceneFilterEnabled { get; set; }

    /// <summary>EnvManager scene (Splatoon-compatible). Used only when <see cref="SceneFilterEnabled"/>.</summary>
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

public enum MajorOverlayLaneMode
{
    Single = 0,
    AbilityAndSkill = 1,
}

public enum TimelineCueKind
{
    Action = 0,
    Memo = 1,
    // 2 = retired SceneTransition, 3 = retired BossHpZero (ignored on load).
    Sync = 4,
}

public sealed class TimelineCue
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Relative to clock zero (pull). Negative = pre-pull.</summary>
    public float TimeOffsetSec { get; set; }

    /// <summary>Action / memo / sync row in the Contents column.</summary>
    public TimelineCueKind Kind { get; set; } = TimelineCueKind.Action;

    /// <summary>
    /// Player action id, enemy ability game id when <see cref="IsCastSync"/>,
    /// or status id when <see cref="IsStatusSync"/>.
    /// </summary>
    public uint ActionId { get; set; }

    /// <summary>
    /// Memo text. Unused for Action / Sync (name from ActionId).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Label { get; set; } = string.Empty;

    /// <summary>None / job / role. Empty (None) = no party target.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public CueTargetKind TargetKind { get; set; }

    /// <summary>ClassJob row id when <see cref="TargetKind"/> is Job.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public uint TargetJobId { get; set; }

    /// <summary>Role when <see cref="TargetKind"/> is Role.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public CueTargetRole TargetRole { get; set; }

    /// <summary>Cast (omitted) or Status. Used only when Kind is Sync.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public EnemySyncType SyncType { get; set; }

    /// <summary>
    /// Sync edge. Cast: false = start, true = ActionEffect complete.
    /// Status: false = apply, true = remove.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Effected { get; set; }

    [JsonIgnore]
    public bool IsCastSync =>
        Kind == TimelineCueKind.Sync && SyncType == EnemySyncType.Cast;

    [JsonIgnore]
    public bool IsStatusSync =>
        Kind == TimelineCueKind.Sync && SyncType == EnemySyncType.Status;

    [JsonIgnore]
    public bool IsClockSync => IsCastSync || IsStatusSync;

    public TimelineCue CopyForDocument() =>
        new()
        {
            TimeOffsetSec = TimeOffsetSec,
            Kind = Kind,
            ActionId = Kind == TimelineCueKind.Action || IsClockSync
                ? ActionId
                : 0,
            Label = Kind == TimelineCueKind.Memo
                ? Label
                : string.Empty,
            TargetKind = Kind == TimelineCueKind.Action ? TargetKind : CueTargetKind.None,
            TargetJobId = Kind == TimelineCueKind.Action ? TargetJobId : 0,
            TargetRole = Kind == TimelineCueKind.Action ? TargetRole : CueTargetRole.None,
            SyncType = Kind == TimelineCueKind.Sync ? SyncType : default,
            Effected = IsClockSync && Effected,
        };
}

public enum EnemySyncType
{
    Cast = 0,
    Status = 1,
}

/// <summary>One Cast or Status row in a per-zone sync preset.</summary>
public sealed class SyncPresetEntry
{
    public EnemySyncType SyncType { get; set; }
    public uint ActionId { get; set; }
    public bool Effected { get; set; }
}

public enum CueTargetKind
{
    None = 0,
    Job = 1,
    Role = 2,
}

public enum CueTargetRole
{
    None = 0,
    Tank = 1,
    Healer = 2,
    Melee = 3,
    Ranged = 4,
    Caster = 5,
}
