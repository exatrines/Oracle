using System.Text.Json;
using System.Text.Json.Serialization;
using Oracle.Models;
using Oracle.Services;

namespace Oracle.UI;

// --- Shell: lifecycle → two-column draw → public hooks → persistence ---

/// <summary>
/// Timeline settings window.
/// Flow: sidebar timelines → editor (General | Auto Load + cue table) / import.
/// </summary>
internal sealed partial class ConfigWindow : Window
{
    private const string TabFFLogsImport = "fflogs-import";
    private const string TabAutoRecordImport = "auto-record-import";
    private const string TimelineIdPrefix = "timeline:";

    private readonly TimelineStore _store;
    private readonly TimelineEngine _engine;
    private readonly ActionSearchWindow _actionSearch;
    private readonly FFLogsImportPanel _ffLogsImport;
    private readonly AutoRecordImportPanel _autoRecordImport;
    private readonly Action _openPluginSettings;
    private readonly string _pluginIconPath;

    private string _selectedTabId = string.Empty;
    private string _sidebarSearch = string.Empty;
    private string? _entryReorderDragId;
    private readonly HashSet<string> _collapsedFolderIds = new(StringComparer.Ordinal);
    private ImRaii.ColorDisposable? _themeScope;
    private TimelineDocument? _editDoc;
    private string _zoneSearchFilter = string.Empty;
    private string _timelineNameDraft = string.Empty;
    private string? _timelineNameDraftDocId;
    private string _cueTimeDraft = string.Empty;
    private string? _cueTimeDraftId;

    /// <summary>Draft row below the cue table (not yet committed).</summary>
    private string _newCueTimeText = "00:00";
    private TimelineCueKind _newCueKind = TimelineCueKind.Action;
    private string _newCueMemo = string.Empty;
    private uint _newCueActionId;
    private string? _newCueDraftDocId;

    /// <summary>Selected cue ids in the editor table (checkboxes).</summary>
    private readonly HashSet<string> _selectedCueIds = new(StringComparer.Ordinal);

    /// <summary>When set, the next action pick replaces this cue instead of adding.</summary>
    private string? _actionPickCueId;

    /// <summary>When false, cue-table edits stay in-memory (AutoRecord import preview).</summary>
    private bool _cueTablePersist = true;

    private const string ActionPickDraftId = "__draft__";
    private const string CueClipboardFormat = "Oracle.Cues.v1";

    private static readonly JsonSerializerOptions CueClipboardJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Sidebar tint for the timeline currently loaded by the engine (auto or manual).</summary>
    private static readonly Vector4 StandbyMatchGreen = new(0.45f, 0.85f, 0.45f, 1f);

    private const float CueActionIconSize = 22f;

    public ConfigWindow(
        TimelineStore store,
        TimelineEngine engine,
        ActionSearchWindow actionSearch,
        FFLogsImportPanel ffLogsImport,
        AutoRecordImportPanel autoRecordImport,
        Action openPluginSettings)
        : base("Oracle###oracleTimelineConfig", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        _store = store;
        _engine = engine;
        _actionSearch = actionSearch;
        _ffLogsImport = ffLogsImport;
        _autoRecordImport = autoRecordImport;
        _openPluginSettings = openPluginSettings;
        _pluginIconPath = ResolvePluginIconPath();
        MirageWindowDefaults.ApplyTo(this);
        // Sidebar + wide main (meta split + cue table).
        Size = new Vector2(1100f, 630f);
        SizeConstraints = new()
        {
            MinimumSize = Size.Value,
            MaximumSize = Size.Value,
        };
        EnsureDefaultSelection();
    }

    private static string ResolvePluginIconPath()
    {
        var dir = PluginServices.PluginInterface.AssemblyLocation.DirectoryName
                  ?? AppContext.BaseDirectory;
        return Path.Combine(dir, "Data", "plugin-icon.png");
    }

    private void EnsureDefaultSelection()
    {
        if (!string.IsNullOrEmpty(_selectedTabId))
            return;

        var active = _store.ActiveDocument ?? _store.Documents.FirstOrDefault();
        if (active == null)
            return;

        _selectedTabId = TimelinePageId(active.Id);
        _editDoc = active;
        _store.SetActive(active.Id);
    }

    public override void PreDraw()
    {
        WindowName = I18n.Get("window.config.title") + "###oracleTimelineConfig";
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        MirageTheme.EnsureDefaultsCaptured();
        _themeScope = MirageTheme.PushCustom(MirageTheme.ResolveAppliedColors());
    }

    public override void PostDraw()
    {
        MirageTheme.Pop(_themeScope);
        _themeScope = null;
        ImGui.PopStyleVar();
    }

    public override void Draw()
    {
        EnsureDefaultSelection();
        MirageUi.TwoColumn.Draw(CreateTimelineTwoColumnState(), DrawMainContent);
    }

    private void DrawMainContent()
    {
        if (_selectedTabId == TabFFLogsImport)
        {
            DrawImportSplit(
                () => _ffLogsImport.Draw(),
                () => DrawImportActionsColumn(
                    _ffLogsImport.SelectedClassJobId,
                    "fflogsImportPreviewActions"));
            return;
        }

        if (_selectedTabId == TabAutoRecordImport)
        {
            DrawImportSplit(
                () => _autoRecordImport.Draw(),
                () => DrawImportActionsColumn(
                    _autoRecordImport.SelectedClassJobId,
                    "autoRecordImportActions"));
            return;
        }

        if (TryParseTimelinePageId(_selectedTabId, out _))
        {
            DrawEditor();
            return;
        }

        MirageUi.Header(I18n.Get("config.header.timelines"));
        MirageUi.Text(I18n.Get("config.empty.select_or_create"), MirageUi.Color.Secondary);
    }

    private static void DrawImportSplit(Action drawLeft, Action drawRight)
    {
        var height = Math.Max(64f, ImGui.GetContentRegionAvail().Y);
        if (!ImGui.BeginTable(
                "##importSplit",
                2,
                ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoPadOuterX,
                new Vector2(-1f, height)))
        {
            drawLeft();
            return;
        }

        ImGui.TableSetupColumn("##importMain");
        ImGui.TableSetupColumn("##importActions");
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        using (ImRaii.Child("##importSplitLeft", new Vector2(-1f, height), false))
            drawLeft();

        ImGui.TableNextColumn();
        using (ImRaii.Child("##importSplitRight", new Vector2(-1f, height), false))
            drawRight();

        ImGui.EndTable();
    }

    private static void DrawImportActionsColumn(uint jobId, string idPrefix)
    {
        MirageUi.SubHeader(I18n.Get("config.subheader.import_actions"));
        if (jobId == 0)
        {
            MirageUi.Text(
                I18n.Get("config.fflogs.import_actions.hint"),
                MirageUi.Color.Secondary);
            return;
        }

        FFLogsImportActionsUi.DrawForJob(jobId, idPrefix: idPrefix);
    }

    private static bool IsUtilityTab(string tabId) =>
        tabId is TabFFLogsImport or TabAutoRecordImport;

    /// <summary>Select an imported timeline in the editor.</summary>
    public void SelectImportedTimeline(TimelineDocument doc)
    {
        if (doc.TerritoryTypeId == 0)
            ApplyCurrentZone(doc);
        PersistDocument(doc);
        _store.MoveToEndOfZone(doc.Id);
        _store.SetActive(doc.Id);
        _editDoc = doc;
        _selectedTabId = TimelinePageId(doc.Id);
        IsOpen = true;
    }

    private static string TimelinePageId(string documentId) => TimelineIdPrefix + documentId;

    private static bool TryParseTimelinePageId(string pageId, out string documentId)
    {
        if (pageId.StartsWith(TimelineIdPrefix, StringComparison.Ordinal))
        {
            documentId = pageId[TimelineIdPrefix.Length..];
            return documentId.Length > 0;
        }

        documentId = string.Empty;
        return false;
    }

    /// <summary>Write timeline JSON. Does not apply the name draft (file rename is Save-icon only).</summary>
    private void PersistDocument(TimelineDocument doc)
    {
        var previousId = doc.Id;
        _store.SaveDocument(doc);
        if (!string.Equals(previousId, doc.Id, StringComparison.OrdinalIgnoreCase))
        {
            _selectedTabId = TimelinePageId(doc.Id);
            if (string.Equals(_timelineNameDraftDocId, previousId, StringComparison.OrdinalIgnoreCase))
                _timelineNameDraftDocId = doc.Id;
        }
    }

    private static void ApplyCurrentZone(TimelineDocument doc)
    {
        var territoryTypeId = doc.TerritoryTypeId;
        var contentFinderConditionId = doc.ContentFinderConditionId;
        var classJobLevel = doc.ClassJobLevel;
        var zoneLabel = string.Empty;
        if (!ZoneCombo.ApplyCurrent(
                ref territoryTypeId,
                ref contentFinderConditionId,
                ref classJobLevel,
                ref zoneLabel))
            return;

        doc.TerritoryTypeId = territoryTypeId;
        doc.ContentFinderConditionId = contentFinderConditionId;
        doc.ClassJobLevel = classJobLevel;
    }

    /// <summary>Persist ClassJob change from the action picker dropdown.</summary>
    public void SetEditDocumentClassJob(uint classJobId)
    {
        var doc = EditDocument;
        if (doc == null)
            return;

        doc.ClassJobId = classJobId;
        PersistDocument(doc);
    }

    /// <summary>Apply an action picked in ActionSearchWindow onto the cue being edited.</summary>
    public void ApplyPickedAction(JobActionInfo action)
    {
        _editDoc ??= _store.ActiveDocument;
        if (_editDoc == null || _actionPickCueId == null)
            return;

        if (string.Equals(_actionPickCueId, ActionPickDraftId, StringComparison.Ordinal))
        {
            ApplyDraftPickedAction(action);
            return;
        }

        ApplyCuePickedAction(action);
    }

    private void ApplyDraftPickedAction(JobActionInfo action)
    {
        _actionPickCueId = null;
        _newCueActionId = action.ActionId;
        _newCueKind = TimelineCueKind.Action;
        _newCueMemo = string.Empty;
        _selectedTabId = TimelinePageId(_editDoc!.Id);
    }

    private void ApplyCuePickedAction(JobActionInfo action)
    {
        var cue = _editDoc!.Cues.FirstOrDefault(c => c.Id == _actionPickCueId);
        _actionPickCueId = null;
        if (cue == null)
            return;

        cue.Kind = TimelineCueKind.Action;
        cue.ActionId = action.ActionId;
        cue.Label = string.Empty;
        PersistDocument(_editDoc);
        _selectedTabId = TimelinePageId(_editDoc.Id);
    }

    private void OpenActionPicker(string replaceCueId)
    {
        _actionPickCueId = replaceCueId;
        var highlightId = 0u;
        if (string.Equals(replaceCueId, ActionPickDraftId, StringComparison.Ordinal))
            highlightId = _newCueActionId;
        else
        {
            var doc = _editDoc ?? _store.ActiveDocument;
            var cue = doc?.Cues.FirstOrDefault(c => c.Id == replaceCueId);
            if (cue is { Kind: TimelineCueKind.Action, ActionId: not 0 })
                highlightId = cue.ActionId;
        }

        _actionSearch.SetHighlightActionId(highlightId);
        _actionSearch.IsOpen = true;
    }

    public TimelineDocument? EditDocument => _editDoc ?? _store.ActiveDocument;
}
