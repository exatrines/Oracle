using Dalamud.Configuration;
using Oracle.Models;
using Oracle.Services;
using Oracle.Services.AutoRecord;
using Oracle.Services.FFLogs;
using Newtonsoft.Json;

namespace Oracle;

/// <summary>Persisted plugin settings.</summary>
[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    // --- Timeline list overlay ---

    public bool ShowOverlay { get; set; } = true;
    public float OverlayPosX { get; set; } = 40f;
    public float OverlayPosY { get; set; } = 200f;
    public bool OverlayClickThrough { get; set; }
    public int OverlayMaxRows { get; set; } = 8;
    public float LookaheadSeconds { get; set; } = 30f;

    // --- Action highlight (shared by Timeline / Major / Hotbar) ---

    public float ActionHighlightBeforeSeconds { get; set; } = 3f;
    public float ActionHighlightBeforeLineThickness { get; set; } = 3f;
    public Vector4 ActionHighlightBeforeLineColor { get; set; } = new(1f, 0.9f, 0.15f, 1f);
    public bool ActionHighlightBeforeBlink { get; set; } = true;

    public float ActionHighlightAfterSeconds { get; set; } = 5f;
    public float ActionHighlightAfterLineThickness { get; set; } = 3f;
    public Vector4 ActionHighlightAfterLineColor { get; set; } = new(1f, 0.9f, 0.15f, 1f);
    public bool ActionHighlightAfterBlink { get; set; } = true;

    // --- Hotbar highlight ---

    public bool ShowHotbarHighlight { get; set; } = true;
    public List<byte>? HotbarHighlightEnabledIds { get; set; }
    public bool ShowHotbarHighlightDoubleCross { get; set; }

    // --- Major overlay ---

    public bool ShowMajorOverlay { get; set; } = true;
    public float MajorOverlayPosX { get; set; } = 40f;
    public float MajorOverlayPosY { get; set; } = 80f;
    public bool MajorOverlayClickThrough { get; set; }
    public float MajorPixelsPerSecond { get; set; } = 48f;
    public float MajorBeforeSeconds { get; set; } = 3f;
    public float MajorAfterSeconds { get; set; } = 5f;
    public float MajorIconSize { get; set; } = 32f;
    public MajorOverlayLaneMode MajorLaneMode { get; set; } = MajorOverlayLaneMode.Single;
    public bool MajorShowTitle { get; set; } = true;
    public bool MajorShowSecondLabels { get; set; } = true;
    public bool MajorShowGrid { get; set; } = true;
    public Vector4 MajorBackgroundColor { get; set; } = new(0.05f, 0.05f, 0.05f, 0.72f);
    public Vector4 MajorGridLineColor { get; set; } = new(1f, 1f, 1f, 0.22f);
    public Vector4 MajorZeroLineColor { get; set; } = new(0.2f, 0.95f, 0.35f, 1f);
    public float MajorZeroLineThickness { get; set; } = 2f;
    public Vector4 MajorLabelColor { get; set; } = new(0.85f, 0.85f, 0.85f, 0.9f);

    // --- Timeline selection & UI theme ---

    public string ActiveTimelineId { get; set; } = string.Empty;
    public List<string> TimelineOrder { get; set; } = [];
    public MirageColorSettings? ThemeColors { get; set; }

    /// <summary>
    /// Plugin UI language. <c>client</c> follows Dalamud UI language; otherwise a locale code (e.g. en, ja).
    /// </summary>
    public string UiLanguage { get; set; } = "client";

    // --- FFLogs import ---

    [JsonProperty("FflogsClientId")]
    public string FFLogsClientId { get; set; } = string.Empty;

    [JsonProperty("FflogsClientSecret")]
    public string FFLogsClientSecret { get; set; } = string.Empty;

    [JsonProperty("FflogsImportActionIdsByJob")]
    public Dictionary<uint, List<uint>> FFLogsImportActionIdsByJob { get; set; } = new();

    // --- Auto Record ---

    public bool AutoRecordEnabled { get; set; }
    public bool AutoRecordManualSave { get; set; }
    public bool AutoRecordSavePendingOnNextCombat { get; set; }
    public int AutoRecordMaxFiles { get; set; } = 50;
    public float AutoRecordOverlayPosX { get; set; } = 40f;
    public float AutoRecordOverlayPosY { get; set; } = 360f;
    public bool AutoRecordOverlayVisible { get; set; }
    public bool AutoRecordOverlayAutoOpenOnEffectiveZone { get; set; }
    public bool AutoRecordOverlayCollapsed { get; set; }
    public List<uint>? AutoRecordZoneWhitelist { get; set; }

    [NonSerialized]
    private IDalamudPluginInterface? _pluginInterface;

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        _pluginInterface = pluginInterface;
        EnsureHotbarHighlightDefaults();
    }

    public float MaxHighlightAfterSeconds =>
        Math.Max(
            Math.Max(0f, ActionHighlightAfterSeconds),
            Math.Max(0f, MajorAfterSeconds));

    public void Save() => _pluginInterface?.SavePluginConfig(this);

    public void EnsureAutoRecordZoneWhitelist()
    {
        if (AutoRecordZoneWhitelist != null)
            return;

        AutoRecordZoneWhitelist = AutoRecordDefaultWhitelist.CreateList();
        Save();
    }

    public bool IsAutoRecordZoneEnabled(uint territoryTypeId)
    {
        if (territoryTypeId == 0)
            return false;

        EnsureAutoRecordZoneWhitelist();
        return AutoRecordZoneWhitelist!.Contains(territoryTypeId);
    }

    public void AddAutoRecordZoneEnabled(uint territoryTypeId)
    {
        if (territoryTypeId == 0)
            return;

        EnsureAutoRecordZoneWhitelist();
        if (AutoRecordZoneWhitelist!.Contains(territoryTypeId))
            return;

        AutoRecordZoneWhitelist.Add(territoryTypeId);
        AutoRecordZoneWhitelist.Sort();
        Save();
    }

    public void RemoveAutoRecordZoneEnabled(uint territoryTypeId)
    {
        EnsureAutoRecordZoneWhitelist();
        if (!AutoRecordZoneWhitelist!.Remove(territoryTypeId))
            return;
        Save();
    }

    public void ResetAutoRecordZoneEnabledToDefault()
    {
        AutoRecordZoneWhitelist = AutoRecordDefaultWhitelist.CreateList();
        Save();
    }

    public static List<byte> CreateDefaultHotbarHighlightEnabledIds()
    {
        var ids = new List<byte>(10);
        for (byte i = 0; i < 10; i++)
            ids.Add(i);
        return ids;
    }

    public void EnsureHotbarHighlightDefaults()
    {
        HotbarHighlightEnabledIds ??= CreateDefaultHotbarHighlightEnabledIds();
    }

    public bool IsHotbarHighlightEnabled(byte hotbarId)
    {
        EnsureHotbarHighlightDefaults();
        return HotbarHighlightEnabledIds!.Contains(hotbarId);
    }

    public void SetHotbarHighlightEnabled(byte hotbarId, bool enabled)
    {
        if (hotbarId > 17)
            return;

        EnsureHotbarHighlightDefaults();
        var ids = HotbarHighlightEnabledIds!;
        var has = ids.Contains(hotbarId);
        if (enabled && !has)
            ids.Add(hotbarId);
        else if (!enabled && has)
            ids.Remove(hotbarId);

        ids.Sort();
        Save();
    }

    public HashSet<uint> GetFFLogsImportActionIds(uint classJobId)
    {
        if (classJobId == 0)
            return [];

        if (FFLogsImportActionIdsByJob != null
            && FFLogsImportActionIdsByJob.TryGetValue(classJobId, out var list)
            && list != null)
            return list.Where(id => id != 0).ToHashSet();

        return FFLogsImportActionDefaults.Get(classJobId);
    }

    public void SetFFLogsImportActionIds(uint classJobId, IEnumerable<uint> actionIds)
    {
        if (classJobId == 0)
            return;

        FFLogsImportActionIdsByJob ??= new Dictionary<uint, List<uint>>();
        FFLogsImportActionIdsByJob[classJobId] = actionIds
            .Where(id => id != 0)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
        Save();
    }

    public void ResetFFLogsImportActionIds(uint classJobId)
    {
        if (classJobId == 0)
            return;

        if (FFLogsImportActionIdsByJob == null)
            return;

        if (!FFLogsImportActionIdsByJob.Remove(classJobId))
            return;

        Save();
    }
}
