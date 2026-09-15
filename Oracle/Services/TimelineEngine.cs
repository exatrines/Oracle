using System.Collections.Concurrent;
using Dalamud.Game.DutyState;
using Dalamud.Interface.ImGuiNotification;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Network;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Oracle.Models;

namespace Oracle.Services;

internal sealed class UpcomingCue
{
    public required TimelineCue Cue { get; init; }
    public float RemainingSeconds { get; init; }

    /// <summary>Pre-fire or post-fire stroke is active.</summary>
    public bool IsHighlighting { get; init; }

    /// <summary>Post-fire (after 0s). Timeline list shows NOW + remaining.</summary>
    public bool IsPostHighlight { get; init; }

    public float HighlightRemainingSec { get; init; }
}

internal sealed class ActiveHighlight
{
    public required string CueId { get; init; }
    public required DateTime StartedUtc { get; init; }
    public required float DurationSec { get; init; }
    public DateTime EndsUtc => StartedUtc.AddSeconds(DurationSec);
}

/// <summary>
/// Resolves zone/job/preset timelines, runs countdown/combat clock, feeds overlay cues.
/// Clock anchors: enemy cast start/effected and status apply/remove.
/// Hook → queue → matching cue → ResyncClockTo.
/// Auto Load waits through DutyWiped until DutyRecommenced (bosses may not be targetable yet).
/// </summary>
internal sealed unsafe class TimelineEngine : IDisposable
{
    private readonly TimelineStore _store;
    private readonly ActionEffectReceiveHub _receiveHub;
    private readonly ActorCastReceiveHub _castHub;
    private readonly StatusManagerReceiveHub _statusHub;
    private readonly PluginLogService _pluginLog;
    private readonly CombatSyncDetector _combat = new();
    private readonly CountdownSyncDetector _countdown = new();
    private readonly ActionUseDetector _actionUse;
    private readonly ConcurrentQueue<uint> _pendingEnemyCastActionIds = new();
    private readonly ConcurrentQueue<uint> _pendingEnemyEffectActionIds = new();
    private readonly ConcurrentQueue<uint> _pendingStatusApplyIds = new();
    private readonly ConcurrentQueue<uint> _pendingStatusRemoveIds = new();

    private TimelineDocument? _activeDoc;
    private DateTime _syncUtc;
    private float _clockOffset;
    private bool _running;
    private bool _previewMode;
    private bool _previewPaused;
    private DateTime _pauseHeldUtc;

    private readonly HashSet<string> _completedCueIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _startedHighlightIds = new(StringComparer.Ordinal);
    private readonly List<ActiveHighlight> _highlights = [];

    private string? _manualLoadId;
    private uint _manualLoadTerritory;
    private uint _lastPlayerJobId;
    private bool _hasTrackedPlayerJob;
    private uint _lastTerritoryType;
    private bool _hasTrackedTerritory;
    private bool _freezeAutoLoad;

    private readonly HashSet<string> _appliedAnchorCueIds = new(StringComparer.Ordinal);

    // --- Lifecycle ---

    public TimelineEngine(
        TimelineStore store,
        ActionEffectReceiveHub receiveHub,
        ActorCastReceiveHub castHub,
        StatusManagerReceiveHub statusHub,
        PluginLogService pluginLog)
    {
        _store = store;
        _receiveHub = receiveHub;
        _castHub = castHub;
        _statusHub = statusHub;
        _pluginLog = pluginLog;
        _actionUse = new ActionUseDetector(receiveHub);
        _countdown.Subscribe();
        _actionUse.Subscribe();
        _castHub.Received += OnEnemyCastReceived;
        _receiveHub.Received += OnEnemyEffectReceived;
        _statusHub.Received += OnStatusChanged;
        PluginServices.DutyState.DutyStarted += OnDutyStarted;
        PluginServices.DutyState.DutyWiped += OnDutyWiped;
        PluginServices.DutyState.DutyRecommenced += OnDutyRecommenced;
        PluginServices.DutyState.DutyCompleted += OnDutyCompleted;
    }

    public void Dispose()
    {
        PluginServices.DutyState.DutyCompleted -= OnDutyCompleted;
        PluginServices.DutyState.DutyRecommenced -= OnDutyRecommenced;
        PluginServices.DutyState.DutyWiped -= OnDutyWiped;
        PluginServices.DutyState.DutyStarted -= OnDutyStarted;
        _statusHub.Received -= OnStatusChanged;
        _receiveHub.Received -= OnEnemyEffectReceived;
        _castHub.Received -= OnEnemyCastReceived;
        _countdown.Dispose();
        _actionUse.Dispose();
        ClearPendingClockSync();
    }

    // --- Status ---

    public bool IsRunning => _running;
    public bool IsPreview => _previewMode;
    public bool IsPreviewPaused => _previewPaused;
    public TimelineDocument? ActiveDocument => _activeDoc;

    internal ActionUseDetector ActionUse => _actionUse;

    public bool IsContextMatched =>
        ResolveDocumentForPlayer() != null;

    public bool MatchesLiveZone(TimelineDocument doc) =>
        MatchesTerritory(doc, PluginServices.ClientState.TerritoryType);

    public bool MatchesLiveJob(TimelineDocument doc)
    {
        var playerJob = PluginServices.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;
        return MatchesJob(doc, playerJob);
    }

    public bool MatchesLivePreset(TimelineDocument doc) =>
        MatchesPreset(doc);

    private DateTime ClockNowUtc => _previewPaused ? _pauseHeldUtc : DateTime.UtcNow;

    public float ElapsedSeconds =>
        _running ? _clockOffset + (float)(ClockNowUtc - _syncUtc).TotalSeconds : 0f;

    private TimelineDocument? ResolveDocumentForPlayer()
    {
        if (!string.IsNullOrEmpty(_manualLoadId))
        {
            var forced = _store.FindById(_manualLoadId);
            if (forced != null)
                return forced;

            _manualLoadId = null;
        }

        if (_running || _combat.InCombat)
            return _activeDoc;
        if (_freezeAutoLoad)
            return FindHeldDocument();

        var candidates = ListAutoLoadCandidates();
        var held = FindHeldDocument();
        if (candidates.Count == 0)
            return held;

        var best = candidates[0];
        if (best.Spec > 0 || held == null)
            return best.Doc;
        return held;
    }

    private TimelineDocument? ResolveDocumentForCountdown()
    {
        var candidates = ListAutoLoadCandidates();
        if (candidates.Count > 0)
            return candidates[0].Doc;

        return FindHeldDocument();
    }

    private List<(TimelineDocument Doc, int Spec)> ListAutoLoadCandidates()
    {
        var territory = PluginServices.ClientState.TerritoryType;
        var playerJob = PluginServices.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;
        var ranked = new List<(TimelineDocument Doc, int Index, int Spec)>();
        HashSet<uint>? liveIds = null;

        var index = 0;
        foreach (var doc in _store.Documents)
        {
            var i = index++;
            if (!doc.AutoLoadEnabled || _store.HasMatchConflict(doc))
                continue;
            if (!MatchesTerritory(doc, territory) || !MatchesJob(doc, playerJob))
                continue;
            if (!TryAutoLoadMatch(doc, ref liveIds, out var spec))
                continue;
            ranked.Add((doc, i, spec));
        }

        return ranked
            .OrderByDescending(x => x.Spec)
            .ThenBy(x => x.Index)
            .Select(x => (x.Doc, x.Spec))
            .ToList();
    }

    private TimelineDocument? FindHeldDocument()
    {
        if (_activeDoc == null)
            return null;

        var live = _store.FindById(_activeDoc.Id);
        if (live == null)
            return null;
        if (!live.AutoLoadEnabled || _store.HasMatchConflict(live))
            return null;

        var territory = PluginServices.ClientState.TerritoryType;
        var playerJob = PluginServices.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;
        if (!MatchesTerritory(live, territory) || !MatchesJob(live, playerJob))
            return null;

        return live;
    }

    private static bool MatchesTerritory(TimelineDocument doc, uint territory) =>
        doc.TerritoryTypeId != 0 && doc.TerritoryTypeId == territory;

    private static bool MatchesJob(TimelineDocument doc, uint playerJob) =>
        doc.ClassJobId != 0 && playerJob != 0 && doc.ClassJobId == playerJob;

    private static bool MatchesPreset(TimelineDocument doc)
    {
        HashSet<uint>? liveIds = null;
        return TryAutoLoadMatch(doc, ref liveIds, out _);
    }

    private static bool TryAutoLoadMatch(TimelineDocument doc, ref HashSet<uint>? liveIds, out int specificity)
    {
        specificity = 0;
        if (string.IsNullOrWhiteSpace(doc.AutoLoadPresetId))
            return true;

        var preset = AutoLoadPresets.Find(doc.TerritoryTypeId, doc.AutoLoadPresetId);
        if (preset == null)
            return false;

        var ids = AutoLoadPresets.DataIdsFor(preset);
        liveIds ??= BossPresence.LiveDataIds();
        if (!BossPresence.AllTargetable(ids, liveIds))
            return false;

        specificity = 1_000 + ids.Count;
        return true;
    }

    private void OnDutyStarted(IDutyStateEventArgs args) =>
        _freezeAutoLoad = false;

    private void OnDutyRecommenced(IDutyStateEventArgs args) =>
        _freezeAutoLoad = false;

    private void OnDutyWiped(IDutyStateEventArgs args) =>
        _freezeAutoLoad = true;

    private void OnDutyCompleted(IDutyStateEventArgs args) =>
        _freezeAutoLoad = true;

    // --- Load ---

    public void ManualLoad(TimelineDocument doc)
    {
        if (_running)
            StopClock();

        _manualLoadId = doc.Id;
        _manualLoadTerritory = PluginServices.ClientState.TerritoryType;
        _activeDoc = doc;
        NotifyTimelineLoad(manual: true, doc);
    }

    public bool TryManualLoadByToken(string token, out TimelineDocument? document)
    {
        document = _store.FindByLoadToken(token);
        if (document == null)
            return false;

        ManualLoad(document);
        return true;
    }

    // --- Clock ---

    public bool CanStartPreview =>
        ResolveDocumentForPlayer() != null || _store.ActiveDocument != null;

    public void StartPreview(float countdownSeconds = 21f)
    {
        var doc = ResolveDocumentForPlayer() ?? _store.ActiveDocument;
        if (doc == null)
            return;

        var sec = Math.Max(0f, countdownSeconds);
        StartClock(doc, clockOffset: -sec, preview: true);
        PluginServices.ChatGui.Print(I18n.Format("engine.chat.preview", doc.Name, sec));
    }

    public void StopPreview()
    {
        if (!_previewMode && !_running)
            return;
        StopClock();
        PluginServices.ChatGui.Print(I18n.Get("engine.chat.preview_stopped"));
    }

    public bool TryTogglePreviewPause()
    {
        if (!_running || !_previewMode)
            return false;

        if (_previewPaused)
        {
            var delta = DateTime.UtcNow - _pauseHeldUtc;
            _syncUtc += delta;
            for (var i = 0; i < _highlights.Count; i++)
            {
                var h = _highlights[i];
                _highlights[i] = new ActiveHighlight
                {
                    CueId = h.CueId,
                    StartedUtc = h.StartedUtc + delta,
                    DurationSec = h.DurationSec,
                };
            }

            _previewPaused = false;
            return true;
        }

        _pauseHeldUtc = DateTime.UtcNow;
        _previewPaused = true;
        return true;
    }

    public void Unload()
    {
        ClearTimelineState();
    }

    public void DropIfLoaded(string documentId)
    {
        if (!SameId(_activeDoc?.Id, documentId) && !SameId(_manualLoadId, documentId))
            return;

        ClearTimelineState();
    }

    public void DropIfMissingFromStore()
    {
        if (_activeDoc != null && _store.FindById(_activeDoc.Id) == null)
        {
            ClearTimelineState();
            return;
        }

        if (!string.IsNullOrEmpty(_manualLoadId) && _store.FindById(_manualLoadId) == null)
            ClearTimelineState();
    }

    private static bool SameId(string? a, string? b) =>
        !string.IsNullOrEmpty(a)
        && !string.IsNullOrEmpty(b)
        && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private void StartClock(TimelineDocument doc, float clockOffset, bool preview)
    {
        _previewMode = preview;
        Activate(doc, clockOffset);
    }

    private void ResyncClockTo(float timeOffsetSec)
    {
        if (!_running || _activeDoc == null)
            return;

        _syncUtc = DateTime.UtcNow;
        _clockOffset = timeOffsetSec;
        _completedCueIds.Clear();
        _startedHighlightIds.Clear();
        _highlights.Clear();
        _actionUse.Reset();

        MarkCuesAlreadyPassed(_activeDoc, ElapsedSeconds);
    }

    private static void NotifyTimelineLoad(bool manual, TimelineDocument doc)
    {
        PluginServices.NotificationManager.AddNotification(new Notification
        {
            Title = I18n.Get(manual
                ? "engine.notify.manual_load"
                : "engine.notify.auto_load"),
            Content = I18n.Format("engine.notify.loaded", doc.Name),
            Type = NotificationType.Info,
            InitialDuration = TimeSpan.FromSeconds(4),
        });
    }

    private void StopClock()
    {
        _running = false;
        _previewMode = false;
        _previewPaused = false;
        _clockOffset = 0f;
        _completedCueIds.Clear();
        _startedHighlightIds.Clear();
        _highlights.Clear();
        ClearClockAnchors();
    }

    private void ClearTimelineState()
    {
        StopClock();
        _activeDoc = null;
        _manualLoadId = null;
        _manualLoadTerritory = 0;
    }

    private void Activate(TimelineDocument doc, float clockOffset)
    {
        _activeDoc = doc;
        _previewPaused = false;
        _syncUtc = DateTime.UtcNow;
        _clockOffset = clockOffset;
        _running = true;
        _completedCueIds.Clear();
        _startedHighlightIds.Clear();
        _highlights.Clear();
        _actionUse.Reset();
        ClearClockAnchors();

        MarkCuesAlreadyPassed(doc, ElapsedSeconds);
    }

    // --- Update (per frame) ---

    public void Update()
    {
        // combat
        _combat.Update();
        UpdateHighlights();

        // territory / job
        ClearManualLoadOnTerritoryChange();
        ClearFreezeOnTerritoryChange();
        ResetLoadOnJobChange();

        // Auto Load document
        if (!SyncActiveDocument())
            return;

        // countdown / clock
        ApplyCountdownStart();
        var doc = _activeDoc!;
        if (_combat.JustEnteredCombat && !_running)
            StartClock(doc, clockOffset: ResolveCombatStartOffset(), preview: false);

        // anchors / cues
        if (_running && !_previewPaused)
        {
            ApplyClockAnchors();
            ProcessCueFires();
        }

        if (_combat.JustLeftCombat)
            StopClock();

        if (_previewPaused)
        {
            while (_actionUse.TryDequeue(out _))
            {
            }
        }
        else
            DrainUsedActions();
    }

    private void ClearManualLoadOnTerritoryChange()
    {
        if (string.IsNullOrEmpty(_manualLoadId))
            return;

        var territory = PluginServices.ClientState.TerritoryType;
        if (territory == _manualLoadTerritory)
            return;

        _manualLoadId = null;
        _manualLoadTerritory = 0;
        if (_running)
            StopClock();
    }

    private void ClearFreezeOnTerritoryChange()
    {
        var territory = PluginServices.ClientState.TerritoryType;
        if (!_hasTrackedTerritory)
        {
            _hasTrackedTerritory = true;
            _lastTerritoryType = territory;
            return;
        }

        if (territory == _lastTerritoryType)
            return;

        _lastTerritoryType = territory;
        _freezeAutoLoad = false;
    }

    /// <summary>
    /// Any job swap clears the loaded timeline (manual included); Sync re-searches next.
    /// </summary>
    private void ResetLoadOnJobChange()
    {
        var job = PluginServices.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;
        if (!_hasTrackedPlayerJob)
        {
            _hasTrackedPlayerJob = true;
            _lastPlayerJobId = job;
            return;
        }

        if (job == _lastPlayerJobId)
            return;

        _lastPlayerJobId = job;
        _manualLoadId = null;
        _manualLoadTerritory = 0;
        if (_running)
            StopClock();
        _activeDoc = null;
    }

    private bool SyncActiveDocument()
    {
        DropIfMissingFromStore();

        // While the clock is running, never switch timelines via Auto Load.
        if (_running && _activeDoc != null)
            return true;

        var doc = ResolveDocumentForPlayer();
        if (doc == null)
        {
            if (_running || _activeDoc != null)
            {
                LogAutoLoad("Auto Load (none)");
                ClearTimelineState();
            }

            return false;
        }

        var changed = !SameId(_activeDoc?.Id, doc.Id);
        if (changed)
        {
            if (_running)
                StopClock();

            if (string.IsNullOrEmpty(_manualLoadId))
            {
                NotifyTimelineLoad(manual: false, doc);
                LogAutoLoad($"Auto Load {doc.Name}");
            }
        }

        _activeDoc = doc;
        return true;
    }

    private void ApplyCountdownStart()
    {
        _countdown.Update();
        if (!_countdown.JustStarted)
            return;

        if (!_combat.InCombat)
            ReselectForCountdown();

        if (_activeDoc == null)
            return;

        StartClock(_activeDoc, -Math.Abs(_countdown.StartedRemaining), preview: false);
    }

    private void ReselectForCountdown()
    {
        if (!string.IsNullOrEmpty(_manualLoadId))
            return;

        var selected = ResolveDocumentForCountdown();
        if (selected == null)
        {
            LogAutoLoad("Auto Load reselect (none)");
            return;
        }

        var previous = _activeDoc?.Name;
        var changed = !SameId(_activeDoc?.Id, selected.Id);
        if (changed)
            NotifyTimelineLoad(manual: false, selected);

        _activeDoc = selected;
        LogAutoLoad(changed && !string.IsNullOrEmpty(previous)
            ? $"Auto Load reselect {selected.Name} (was {previous})"
            : $"Auto Load reselect {selected.Name}");
    }

    private void LogAutoLoad(string message) =>
        _pluginLog.WriteAutoLoad(message);

    // Hook edge → matching cue → ResyncClockTo.
    private void ApplyClockAnchors()
    {
        if (_activeDoc is null || !_running)
        {
            ClearPendingClockSync();
            return;
        }

        DrainClockSyncQueue(_pendingEnemyCastActionIds, status: false, edge: false);
        DrainClockSyncQueue(_pendingEnemyEffectActionIds, status: false, edge: true);
        DrainClockSyncQueue(_pendingStatusApplyIds, status: true, edge: false);
        DrainClockSyncQueue(_pendingStatusRemoveIds, status: true, edge: true);
    }

    private unsafe void OnEnemyCastReceived(uint casterEntityId, ActorCastPacket* packet)
    {
        if (packet == null)
            return;
        if (!EnemyHitRules.TryMatchCastStart(
                casterEntityId,
                packet->ActionId,
                (byte)packet->ActionType,
                out var hit))
            return;
        _pendingEnemyCastActionIds.Enqueue(hit.ActionId);
    }

    private unsafe void OnEnemyEffectReceived(
        uint casterEntityId,
        Character* casterPtr,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targetEntityIds)
    {
        if (!EnemyHitRules.TryMatchCastEffected(casterEntityId, header, out var hit))
            return;
        _pendingEnemyEffectActionIds.Enqueue(hit.ActionId);
    }

    private void OnStatusChanged(uint statusId, bool removed, uint sourceEntityId)
    {
        if (statusId == 0)
            return;
        if (removed)
            _pendingStatusRemoveIds.Enqueue(statusId);
        else
            _pendingStatusApplyIds.Enqueue(statusId);
    }

    private void DrainClockSyncQueue(ConcurrentQueue<uint> queue, bool status, bool edge)
    {
        while (queue.TryDequeue(out var id))
        {
            TimelineCue? match = null;
            foreach (var cue in _activeDoc!.Cues)
            {
                if (!CueMatchesAnchor(cue, id, status, edge))
                    continue;
                if (_appliedAnchorCueIds.Contains(cue.Id))
                    continue;
                if (match is null || cue.TimeOffsetSec < match.TimeOffsetSec)
                    match = cue;
            }

            if (match is null)
                continue;

            _appliedAnchorCueIds.Add(match.Id);
            var from = ElapsedSeconds;
            ResyncClockTo(match.TimeOffsetSec);
            LogClockSync(match, from);
        }
    }

    private static bool CueMatchesAnchor(TimelineCue cue, uint id, bool status, bool edge) =>
        (status ? cue.IsStatusSync : cue.IsCastSync)
        && cue.Effected == edge
        && cue.ActionId == id;

    private static void LogClockSync(TimelineCue cue, float fromSec)
    {
        var status = cue.IsStatusSync;
        PluginServices.Log.Information(
            "{Kind:l} sync {Name:l} id={Id} edge={Edge:l} {From:l} -> {To:l}",
            status ? "Status" : "Cast",
            status ? ActionLookup.GetStatusName(cue.ActionId) : ActionLookup.GetName(cue.ActionId),
            cue.ActionId,
            status
                ? (cue.Effected ? "Remove" : "Apply")
                : (cue.Effected ? "Effected" : "Start"),
            CueTime.Format(fromSec),
            CueTime.Format(cue.TimeOffsetSec));
    }

    private void ClearPendingClockSync()
    {
        Discard(_pendingEnemyCastActionIds);
        Discard(_pendingEnemyEffectActionIds);
        Discard(_pendingStatusApplyIds);
        Discard(_pendingStatusRemoveIds);
    }

    private static void Discard(ConcurrentQueue<uint> queue)
    {
        while (queue.TryDequeue(out _))
        {
        }
    }

    private void MarkCuesAlreadyPassed(TimelineDocument doc, float elapsed)
    {
        foreach (var cue in doc.Cues)
        {
            if (SkipsCueFire(cue))
                continue;
            if (GetDisplayOffset(cue) - elapsed < -0.05f)
                _completedCueIds.Add(cue.Id);
        }
    }

    private void ClearClockAnchors()
    {
        _appliedAnchorCueIds.Clear();
        ClearPendingClockSync();
    }

    /// <summary>
    /// When combat starts mid-cast, shift so cast complete (land) is 0.
    /// </summary>
    private static float ResolveCombatStartOffset()
    {
        var player = PluginServices.ObjectTable.LocalPlayer;
        if (player is not { IsCasting: true })
            return 0f;

        var castActionId = player.CastActionId;
        if (castActionId == 0 || !ActionLookup.IsSpell(castActionId))
            return 0f;

        var remaining = player.TotalCastTime - player.CurrentCastTime;
        if (remaining > 0.05f)
            return -remaining;

        var sheetCast = ActionTiming.GetCastSeconds(castActionId);
        return sheetCast > 0f ? -sheetCast : 0f;
    }

    private void DrainUsedActions()
    {
        while (_actionUse.TryDequeue(out var usedActionId))
            TryCompleteCueForUsedAction(usedActionId);
    }

    private void ProcessCueFires()
    {
        if (!_running || _activeDoc == null)
            return;

        var elapsed = ElapsedSeconds;
        foreach (var cue in _activeDoc.Cues)
        {
            if (SkipsCueFire(cue))
                continue;
            if (_completedCueIds.Contains(cue.Id) || _startedHighlightIds.Contains(cue.Id))
                continue;

            if (GetDisplayOffset(cue) - elapsed > 0f)
                continue;

            _startedHighlightIds.Add(cue.Id);
            _highlights.Add(new ActiveHighlight
            {
                CueId = cue.Id,
                StartedUtc = ClockNowUtc,
                DurationSec = C.MaxHighlightAfterSeconds > 0f ? C.MaxHighlightAfterSeconds : 0.1f,
            });
        }
    }

    private void TryCompleteCueForUsedAction(uint usedActionId)
    {
        if (!_running || _activeDoc == null)
            return;

        if (usedActionId == 0)
            return;

        var elapsed = ElapsedSeconds;
        string? bestCueId = null;
        var bestHighlighting = false;
        var bestRemaining = float.MaxValue;

        foreach (var cue in _activeDoc.Cues)
        {
            if (_completedCueIds.Contains(cue.Id))
                continue;
            if (cue.Kind != TimelineCueKind.Action || cue.ActionId == 0)
                continue;
            if (!CueMatchesUsedActionId(cue.ActionId, usedActionId))
                continue;

            var remaining = GetDisplayOffset(cue) - elapsed;
            var highlighting = IsCueBeforeHighlightActive(remaining)
                || _highlights.Any(h => h.CueId == cue.Id);

            // Only clear cues that are highlighting, or still upcoming within the complete window.
            if (!highlighting && (remaining < -0.5f || remaining > C.ActionCompleteWindowSeconds))
                continue;

            if (bestCueId == null
                || (highlighting && !bestHighlighting)
                || (highlighting == bestHighlighting && remaining < bestRemaining))
            {
                bestCueId = cue.Id;
                bestHighlighting = highlighting;
                bestRemaining = remaining;
            }
        }

        if (bestCueId == null)
            return;

        CompleteCue(bestCueId);
    }

    private static bool CueMatchesUsedActionId(uint cueActionId, uint usedActionId)
    {
        if (cueActionId == usedActionId)
            return true;

        unsafe
        {
            var actionManager = ActionManager.Instance();
            if (actionManager == null)
                return false;

            var adjustedCue = actionManager->GetAdjustedActionId(cueActionId);
            var adjustedUsed = actionManager->GetAdjustedActionId(usedActionId);

            return adjustedCue == usedActionId
                   || cueActionId == adjustedUsed
                   || (adjustedCue != 0 && adjustedUsed != 0 && adjustedCue == adjustedUsed);
        }
    }

    // --- Highlights ---

    private void UpdateHighlights()
    {
        var now = ClockNowUtc;
        for (var i = _highlights.Count - 1; i >= 0; i--)
        {
            if (now < _highlights[i].EndsUtc)
                continue;
            _completedCueIds.Add(_highlights[i].CueId);
            _highlights.RemoveAt(i);
        }
    }

    private void CompleteCue(string cueId)
    {
        _completedCueIds.Add(cueId);
        _startedHighlightIds.Add(cueId);
        for (var i = _highlights.Count - 1; i >= 0; i--)
        {
            if (_highlights[i].CueId == cueId)
                _highlights.RemoveAt(i);
        }
    }

    // --- Upcoming ---

    public IReadOnlyList<UpcomingCue> GetUpcoming(float lookaheadSeconds)
    {
        if (_activeDoc == null)
            return [];

        var elapsed = _running ? ElapsedSeconds : 0f;
        var now = ClockNowUtc;
        var list = new List<UpcomingCue>();

        foreach (var cue in _activeDoc.Cues)
        {
            if (_completedCueIds.Contains(cue.Id))
                continue;

            var displayOffset = GetDisplayOffset(cue);
            var remaining = displayOffset - elapsed;
            var highlighting = _highlights.FirstOrDefault(h => h.CueId == cue.Id);
            if (highlighting != null)
            {
                // Keep the cue while post-fire highlight or Major after-window still covers it.
                var sinceStart = (float)(now - highlighting.StartedUtc).TotalSeconds;
                var highlightAfter = Math.Max(0f, C.ActionHighlightAfterSeconds);
                var majorVisibleAfter = Math.Max(0f, C.MajorAfterSeconds);
                var post = highlightAfter > 0f && sinceStart < highlightAfter;
                var keepForMajorVisible = majorVisibleAfter > 0f && sinceStart < majorVisibleAfter;
                if (!post && !keepForMajorVisible)
                    continue;

                list.Add(new UpcomingCue
                {
                    Cue = cue,
                    RemainingSeconds = remaining,
                    IsHighlighting = post,
                    IsPostHighlight = post,
                    HighlightRemainingSec = post ? highlightAfter - sinceStart : 0f,
                });
                continue;
            }

            if (!_running)
            {
                // Stopped: show cues around clock zero within lookahead.
                if (displayOffset < -lookaheadSeconds || displayOffset > lookaheadSeconds)
                    continue;
                list.Add(new UpcomingCue
                {
                    Cue = cue,
                    RemainingSeconds = displayOffset,
                });
                continue;
            }

            if (remaining < -0.05f)
                continue;

            if (remaining <= lookaheadSeconds)
            {
                // Running: future cues, with pre-fire highlight when inside the before window.
                var pre = IsCueBeforeHighlightActive(remaining);
                list.Add(new UpcomingCue
                {
                    Cue = cue,
                    RemainingSeconds = remaining,
                    IsHighlighting = pre,
                });
            }
        }

        return list.OrderBy(u => u.IsHighlighting ? -1000f : u.RemainingSeconds).ToList();
    }

    /// <summary>
    /// Spells display at activation minus cast (log time is cast complete).
    /// Weaponskills, abilities, and memos stay on activation time.
    /// </summary>
    public static float GetDisplayOffset(TimelineCue cue)
    {
        var cast = ActionTiming.GetCastSeconds(cue);
        return cue.TimeOffsetSec - cast;
    }

    // Overlay skip only; clock anchors resync separately.
    private static bool SkipsCueFire(TimelineCue cue) =>
        cue.Kind == TimelineCueKind.Sync;

    private static bool IsCueBeforeHighlightActive(float remainingSeconds)
    {
        if (remainingSeconds < 0f)
            return false;

        return C.ActionHighlightBeforeSeconds > 0f
               && remainingSeconds <= C.ActionHighlightBeforeSeconds;
    }
}
