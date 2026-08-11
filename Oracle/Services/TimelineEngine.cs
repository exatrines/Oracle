using Dalamud.Interface.ImGuiNotification;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;
using Oracle.Models;

namespace Oracle.Services;

internal sealed class UpcomingCue
{
    public required TimelineCue Cue { get; init; }
    public float RemainingSeconds { get; init; }

    public bool IsHighlighting { get; init; }
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
/// Resolves zone/scene/job timelines, runs countdown/combat clock, feeds overlay cues.
/// </summary>
internal sealed class TimelineEngine : IDisposable
{
    private readonly TimelineStore _store;
    private readonly CombatSyncDetector _combat = new();
    private readonly CountdownSyncDetector _countdown = new();
    private readonly ActionUseDetector _actionUse = new();

    private TimelineDocument? _activeDoc;
    private DateTime _syncUtc;
    private float _clockOffset;
    private bool _running;
    private bool _previewMode;

    private uint? _lockedSceneId;

    private readonly HashSet<string> _completedCueIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _startedHighlightIds = new(StringComparer.Ordinal);
    private readonly List<ActiveHighlight> _highlights = [];

    private string? _manualLoadId;
    private uint _manualLoadTerritory;
    private uint _lastPlayerJobId;
    private bool _hasTrackedPlayerJob;

    // --- Lifecycle ---

    public TimelineEngine(TimelineStore store)
    {
        _store = store;
        _countdown.Subscribe();
        _actionUse.Subscribe();
    }

    public void Dispose()
    {
        _countdown.Dispose();
        _actionUse.Dispose();
    }

    // --- Status ---

    public bool IsRunning => _running;
    public bool IsPreview => _previewMode;
    public TimelineDocument? ActiveDocument => _activeDoc;

    internal ActionUseDetector ActionUse => _actionUse;

    public bool IsContextMatched =>
        ResolveDocumentForPlayer() != null;

    public uint CurrentGameSceneId => ReadGameSceneId();

    public uint? LockedSceneId => _lockedSceneId;

    public uint EffectiveSceneId => _lockedSceneId ?? ReadGameSceneId();

    public float ElapsedSeconds =>
        _running ? _clockOffset + (float)(DateTime.UtcNow - _syncUtc).TotalSeconds : 0f;

    // --- Matching ---

    public bool MatchesLiveZone(TimelineDocument doc) =>
        MatchesTerritory(doc, PluginServices.ClientState.TerritoryType);

    public bool MatchesLiveJob(TimelineDocument doc)
    {
        var playerJob = PluginServices.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;
        return MatchesJob(doc, playerJob);
    }

    public bool MatchesLiveScene(TimelineDocument doc) =>
        MatchesScene(doc, ReadGameSceneId());

    private TimelineDocument? ResolveDocumentForPlayer()
    {
        if (!string.IsNullOrEmpty(_manualLoadId))
        {
            var forced = _store.Documents.FirstOrDefault(d =>
                string.Equals(d.Id, _manualLoadId, StringComparison.OrdinalIgnoreCase));
            if (forced != null)
                return forced;

            _manualLoadId = null;
        }

        var territory = PluginServices.ClientState.TerritoryType;
        var player = PluginServices.ObjectTable.LocalPlayer;
        var playerJob = player?.ClassJob.RowId ?? 0;
        // While running, use the scene locked at countdown/combat start.
        var scene = EffectiveSceneId;

        var candidates = _store.Documents
            .Select((d, index) => (Doc: d, Index: index))
            .Where(x =>
                x.Doc.AutoLoadEnabled
                && !_store.HasMatchConflict(x.Doc)
                && MatchesTerritory(x.Doc, territory)
                && MatchesJob(x.Doc, playerJob)
                && MatchesScene(x.Doc, scene))
            // Exact SceneId match beats SceneId=0; on ties, earlier in list wins.
            .OrderByDescending(x => MatchSpecificity(x.Doc, scene))
            .ThenBy(x => x.Index)
            .Select(x => x.Doc)
            .ToList();

        return candidates.Count == 0 ? null : candidates[0];
    }

    private static bool MatchesTerritory(TimelineDocument doc, uint territory) =>
        doc.TerritoryTypeId != 0 && doc.TerritoryTypeId == territory;

    private static bool MatchesJob(TimelineDocument doc, uint playerJob) =>
        doc.ClassJobId != 0 && playerJob != 0 && doc.ClassJobId == playerJob;

    private static bool MatchesScene(TimelineDocument doc, uint scene) =>
        doc.SceneId == 0 || doc.SceneId == scene;

    private static int MatchSpecificity(TimelineDocument doc, uint scene)
    {
        var score = 0;
        if (doc.SceneId != 0 && doc.SceneId == scene)
            score += 1_000;
        return score;
    }

    private static unsafe uint ReadGameSceneId()
    {
        try
        {
            var env = EnvManager.Instance();
            if (env == null)
                return 0;
            // Undocumented field; Splatoon caches (byte*)(EnvManager + 36).
            return *((byte*)env + 0x24);
        }
        catch
        {
            return 0;
        }
    }

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

    public void StartPreview()
    {
        var doc = ResolveDocumentForPlayer() ?? _store.ActiveDocument;
        if (doc == null)
            return;

        StartClock(doc, clockOffset: -20f, preview: true);
        PluginServices.ChatGui.Print(I18n.Format("engine.chat.preview", doc.Name));
    }

    public void StopPreview()
    {
        if (!_previewMode && !_running)
            return;
        StopClock();
        PluginServices.ChatGui.Print(I18n.Get("engine.chat.preview_stopped"));
    }

    public void InjectCountdown(float remainingSeconds)
    {
        _countdown.Inject(remainingSeconds);
        PluginServices.ChatGui.Print(
            I18n.Format("engine.chat.countdown_inject", remainingSeconds, -remainingSeconds));
    }

    public void Reset()
    {
        ClearTimelineState();
        _combat.Reset();
        _countdown.Reset();
        _actionUse.Reset();
    }

    private void StartClock(TimelineDocument doc, float clockOffset, bool preview)
    {
        // Capture scene at countdown / combat (or preview) start; keep it until StopClock.
        _lockedSceneId = ReadGameSceneId();
        _previewMode = preview;
        Activate(doc, clockOffset);
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
        _clockOffset = 0f;
        _lockedSceneId = null; // resume live scene monitoring after combat end
        _completedCueIds.Clear();
        _startedHighlightIds.Clear();
        _highlights.Clear();
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
        _syncUtc = DateTime.UtcNow;
        _clockOffset = clockOffset;
        _running = true;
        _completedCueIds.Clear();
        _startedHighlightIds.Clear();
        _highlights.Clear();
        _actionUse.Reset();

        var elapsed = ElapsedSeconds;
        foreach (var cue in doc.Cues)
        {
            if (GetDisplayOffset(cue) - elapsed < -0.05f)
                _completedCueIds.Add(cue.Id);
        }
    }

    // --- Update (per frame) ---

    public void Update()
    {
        UpdateHighlights();
        ClearManualLoadOnTerritoryChange();
        ResetLoadOnJobChange();

        if (!SyncActiveDocument())
            return;

        var doc = _activeDoc!;
        ApplyCountdownStart(doc);
        ApplyCombatEdges(doc);

        if (_running)
            ProcessCueFires();

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
        var doc = ResolveDocumentForPlayer();
        if (doc == null)
        {
            if (_running || _activeDoc != null)
                ClearTimelineState();

            return false;
        }

        var changed = _activeDoc == null
            || !string.Equals(_activeDoc.Id, doc.Id, StringComparison.OrdinalIgnoreCase);
        if (changed)
        {
            if (_running)
                StopClock();

            // ManualLoad() already notifies; Sync only reports AutoLoad switches here.
            if (string.IsNullOrEmpty(_manualLoadId))
                NotifyTimelineLoad(manual: false, doc);
        }

        _activeDoc = doc;
        return true;
    }

    private void ApplyCountdownStart(TimelineDocument doc)
    {
        _countdown.Update();
        if (_countdown.JustStarted)
            StartClock(doc, -Math.Abs(_countdown.StartedRemaining), preview: false);
    }

    private void ApplyCombatEdges(TimelineDocument doc)
    {
        _combat.Update();
        if (_combat.JustLeftCombat)
            StopClock();
        else if (_combat.JustEnteredCombat && !_running)
            StartClock(doc, clockOffset: ResolveCombatStartOffset(), preview: false);
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
            if (_completedCueIds.Contains(cue.Id) || _startedHighlightIds.Contains(cue.Id))
                continue;

            if (GetDisplayOffset(cue) - elapsed > 0f)
                continue;

            _startedHighlightIds.Add(cue.Id);
            _highlights.Add(new ActiveHighlight
            {
                CueId = cue.Id,
                StartedUtc = DateTime.UtcNow,
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

            // Only clear cues that are highlighting, or still upcoming within lookahead.
            if (!highlighting && (remaining < -0.5f || remaining > C.LookaheadSeconds))
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
        var now = DateTime.UtcNow;
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
        var now = DateTime.UtcNow;
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

    private static bool IsCueBeforeHighlightActive(float remainingSeconds)
    {
        if (remainingSeconds < 0f)
            return false;

        return C.ActionHighlightBeforeSeconds > 0f
               && remainingSeconds <= C.ActionHighlightBeforeSeconds;
    }
}
