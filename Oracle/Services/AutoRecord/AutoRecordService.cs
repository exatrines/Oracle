using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;
using Oracle.Models;

namespace Oracle.Services.AutoRecord;

/// <summary>
/// Records action uses from combat enter to leave into AutoRecord JSON files.
/// </summary>
internal sealed class AutoRecordService : IDisposable
{
    private readonly AutoRecordStore _store;
    private readonly ActionUseDetector _actionUse;
    private readonly CombatSyncDetector _combat = new();

    private bool _recording;
    private bool _pendingSave;
    private float _frozenElapsed;
    private DateTime _startedUtc;
    private uint _territoryTypeId;
    private uint _contentFinderConditionId;
    private byte _classJobLevel;
    private uint _classJobId;
    private uint _sceneId;
    private string _contentLabel = string.Empty;
    private readonly List<TimelineCue> _cues = [];
    private readonly object _gate = new();

    private uint _lastTerritoryTypeId;

    private const float PrebufferWindowSec = 1.5f;
    private const int PrebufferMaxEntries = 32;
    private readonly List<(uint ActionId, DateTime Utc)> _prebuffer = [];

    public bool IsRecording
    {
        get
        {
            lock (_gate)
                return _recording;
        }
    }

    public bool HasPendingSave
    {
        get
        {
            lock (_gate)
                return _pendingSave;
        }
    }

    public bool IsCurrentZoneEnabled
    {
        get
        {
            var territory = PluginServices.ClientState.TerritoryType;
            return C.IsAutoRecordZoneEnabled(territory);
        }
    }

    public AutoRecordService(AutoRecordStore store, ActionUseDetector actionUse)
    {
        _store = store;
        _actionUse = actionUse;
        _actionUse.ActionUsed += OnActionUsed;
        _lastTerritoryTypeId = PluginServices.ClientState.TerritoryType;
    }

    public string SessionZoneLabel
    {
        get
        {
            lock (_gate)
                return _recording || _pendingSave ? _contentLabel : string.Empty;
        }
    }

    public uint SessionClassJobId
    {
        get
        {
            lock (_gate)
                return _recording || _pendingSave ? _classJobId : 0;
        }
    }

    public IReadOnlyList<TimelineCue> GetRecordedCuesSnapshot()
    {
        lock (_gate)
            return _cues.ToList();
    }

    public float SessionElapsedSeconds
    {
        get
        {
            lock (_gate)
            {
                if (_pendingSave)
                    return MathF.Round(_frozenElapsed, 1);
                if (!_recording)
                    return 0f;
                return MathF.Round((float)(DateTime.UtcNow - _startedUtc).TotalSeconds, 1);
            }
        }
    }

    public float SessionElapsedSecondsPrecise
    {
        get
        {
            lock (_gate)
            {
                if (_pendingSave)
                    return _frozenElapsed;
                if (!_recording)
                    return 0f;
                return (float)(DateTime.UtcNow - _startedUtc).TotalSeconds;
            }
        }
    }

    public void Dispose()
    {
        _actionUse.ActionUsed -= OnActionUsed;
        lock (_gate)
        {
            _recording = false;
            _pendingSave = false;
            _cues.Clear();
            _prebuffer.Clear();
        }
    }

    public void Update()
    {
        if (!C.AutoRecordEnabled)
        {
            if (_recording || _pendingSave)
                DiscardSession();
            lock (_gate)
                _prebuffer.Clear();
            return;
        }

        // 1) Zone change: maybe auto-open overlay.
        HandleTerritoryChange();

        // 2) Combat edges: start/stop a recording session.
        _combat.Update();
        if (_combat.JustEnteredCombat && IsCurrentZoneEnabled)
        {
            if (IsDutyReplayPlayback())
            {
                lock (_gate)
                    _prebuffer.Clear();
            }
            else
            {
                if (_pendingSave)
                    ResolvePendingForNextCombat();
                BeginSession();
            }
        }

        if (_combat.JustLeftCombat && _recording)
            EndSession();
    }

    private void ResolvePendingForNextCombat()
    {
        if (C.AutoRecordSavePendingOnNextCombat)
            ConfirmPendingSave();
        else
            CancelPendingSave();
    }

    public void ConfirmPendingSave()
    {
        TimelineDocument? doc;
        string stem;
        lock (_gate)
        {
            if (!_pendingSave)
                return;

            if (_cues.Count == 0)
            {
                ClearPendingUnlocked();
                return;
            }

            if (!TryBuildDocumentUnlocked(out doc, out stem))
            {
                ClearPendingUnlocked();
                return;
            }

            ClearPendingUnlocked();
        }

        Persist(doc!, stem);
    }

    public void CancelPendingSave()
    {
        lock (_gate)
        {
            if (!_pendingSave)
                return;
            ClearPendingUnlocked();
        }
    }

    private void HandleTerritoryChange()
    {
        var territory = PluginServices.ClientState.TerritoryType;
        if (territory == _lastTerritoryTypeId)
            return;

        _lastTerritoryTypeId = territory;

        lock (_gate)
            _prebuffer.Clear();

        // Only when entering an enabled record zone.
        if (!C.AutoRecordOverlayAutoOpenOnEffectiveZone)
            return;
        if (territory == 0 || !C.IsAutoRecordZoneEnabled(territory))
            return;
        if (C.AutoRecordOverlayVisible)
            return;

        C.AutoRecordOverlayVisible = true;
        C.Save();
    }

    private void BeginSession()
    {
        lock (_gate)
        {
            var combatEnterUtc = DateTime.UtcNow;
            PrunePrebufferUnlocked(combatEnterUtc);

            var seed = _prebuffer.OrderBy(e => e.Utc).ToList();
            _prebuffer.Clear();

            _recording = true;
            _pendingSave = false;
            _frozenElapsed = 0f;
            _cues.Clear();

            // Zero = first GCD skill in the prebuffer (0s-land), not an earlier oGCD.
            var firstGcdUtc = seed
                .Where(e => ActionLookup.IsGcdSkill(e.ActionId))
                .Select(e => (DateTime?)e.Utc)
                .FirstOrDefault();
            _startedUtc = firstGcdUtc is { } gcdUtc && gcdUtc < combatEnterUtc
                ? gcdUtc
                : combatEnterUtc;

            foreach (var entry in seed)
            {
                var offset = (float)(entry.Utc - _startedUtc).TotalSeconds;
                offset = MathF.Round(offset, 1);
                _cues.Add(new TimelineCue
                {
                    TimeOffsetSec = offset,
                    Kind = TimelineCueKind.Action,
                    ActionId = entry.ActionId,
                });
            }

            CaptureContext();
        }
    }

    private void DiscardSession()
    {
        lock (_gate)
            ClearPendingUnlocked();
    }

    private void ClearPendingUnlocked()
    {
        _recording = false;
        _pendingSave = false;
        _frozenElapsed = 0f;
        _cues.Clear();
    }

    private void EndSession()
    {
        if (C.AutoRecordManualSave)
        {
            EndSessionPendingSave();
            return;
        }

        EndSessionAndSave();
    }

    private void EndSessionPendingSave()
    {
        lock (_gate)
        {
            if (!_recording)
                return;

            _frozenElapsed = (float)(DateTime.UtcNow - _startedUtc).TotalSeconds;
            _recording = false;

            if (_cues.Count == 0)
            {
                ClearPendingUnlocked();
                return;
            }

            _pendingSave = true;
        }

        if (!C.AutoRecordOverlayVisible)
        {
            C.AutoRecordOverlayVisible = true;
            C.Save();
        }
    }

    private void EndSessionAndSave()
    {
        TimelineDocument? doc;
        string stem;
        lock (_gate)
        {
            if (!_recording)
                return;

            _recording = false;
            if (_cues.Count == 0)
            {
                _cues.Clear();
                return;
            }

            if (!TryBuildDocumentUnlocked(out doc, out stem))
            {
                _cues.Clear();
                return;
            }

            _cues.Clear();
        }

        Persist(doc!, stem);
    }

    private bool TryBuildDocumentUnlocked(out TimelineDocument? doc, out string stem)
    {
        var stamp = _startedUtc.ToLocalTime().ToString("yyyyMMddHHmmss");
        var label = string.IsNullOrWhiteSpace(_contentLabel)
            ? I18n.Get("fflogs.title.unknown")
            : _contentLabel;
        stem = $"{stamp}_{label}";

        doc = new TimelineDocument
        {
            Name = stem,
            AutoLoadEnabled = false,
            TerritoryTypeId = _territoryTypeId,
            ContentFinderConditionId = _contentFinderConditionId,
            ClassJobLevel = _classJobLevel,
            ClassJobId = _classJobId,
            SceneId = _sceneId,
            Cues = _cues
                .Select(c => new TimelineCue
                {
                    TimeOffsetSec = c.TimeOffsetSec,
                    Kind = TimelineCueKind.Action,
                    ActionId = c.ActionId,
                })
                .ToList(),
        };
        return true;
    }

    private void Persist(TimelineDocument doc, string stem)
    {
        try
        {
            var max = Math.Clamp(C.AutoRecordMaxFiles, 1, 500);
            _store.Save(doc, stem, max);
        }
        catch (Exception ex)
        {
            PluginServices.Log.Warning(ex, "Failed to save AutoRecord session");
        }
    }

    private void OnActionUsed(uint actionId)
    {
        if (actionId == 0 || !C.AutoRecordEnabled)
            return;

        if (IsDutyReplayPlayback())
            return;

        lock (_gate)
        {
            if (_recording)
            {
                // Record every action; Import Actions filter is applied when creating a timeline.
                var offset = (float)(DateTime.UtcNow - _startedUtc).TotalSeconds;
                offset = MathF.Round(offset, 1);

                _cues.Add(new TimelineCue
                {
                    TimeOffsetSec = offset,
                    Kind = TimelineCueKind.Action,
                    ActionId = actionId,
                });
                return;
            }

            // Pre-buffer confirmed actions so a 0s-land pull is not dropped before InCombat.
            if (!IsCurrentZoneEnabled)
                return;

            var now = DateTime.UtcNow;
            PrunePrebufferUnlocked(now);
            _prebuffer.Add((actionId, now));
            if (_prebuffer.Count > PrebufferMaxEntries)
                _prebuffer.RemoveAt(0);
        }
    }

    private static unsafe bool IsDutyReplayPlayback()
    {
        try
        {
            var manager = ContentsReplayManager.Instance();
            if (manager == null)
                return false;
            return manager->PlaybackControls.HasFlag(ContentsReplayPlaybackControl.InPlayback);
        }
        catch
        {
            return false;
        }
    }

    private void PrunePrebufferUnlocked(DateTime now)
    {
        var cutoff = now.AddSeconds(-PrebufferWindowSec);
        _prebuffer.RemoveAll(e => e.Utc < cutoff);
    }

    private void CaptureContext()
    {
        _territoryTypeId = PluginServices.ClientState.TerritoryType;
        _classJobId = PluginServices.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;
        _sceneId = ReadGameSceneId();
        _contentFinderConditionId = 0;
        _classJobLevel = 0;
        _contentLabel = string.Empty;

        if (_territoryTypeId == 0)
        {
            _contentLabel = I18n.Get("fflogs.title.unknown");
            return;
        }

        if (DutyContentCatalog.TryResolvePreferredForTerritory(_territoryTypeId, out var match))
        {
            _territoryTypeId = match.TerritoryTypeId;
            _contentFinderConditionId = match.ContentFinderConditionId;
            _classJobLevel = match.ClassJobLevel;
        }

        var label = DutyContentCatalog.ResolveZoneLabel(
            _territoryTypeId,
            _contentFinderConditionId,
            _classJobLevel);
        var body = DutyContentCatalog.StripZoneLabelPrefix(label);
        var levelMarker = I18n.Get("zone.level_marker");
        var dash = body.LastIndexOf(levelMarker, StringComparison.OrdinalIgnoreCase);
        if (dash > 0)
            body = body[..dash].Trim();

        _contentLabel = string.IsNullOrWhiteSpace(body)
            ? I18n.Get("fflogs.title.unknown")
            : body;
    }

    private static unsafe uint ReadGameSceneId()
    {
        try
        {
            var env = EnvManager.Instance();
            if (env == null)
                return 0;
            return *((byte*)env + 0x24);
        }
        catch
        {
            return 0;
        }
    }
}
