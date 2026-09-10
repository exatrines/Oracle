using Oracle.Models;
using Oracle.Services;
using Oracle.Services.AutoRecord;

namespace Oracle.UI;

/// <summary>Import a recorded combat session from AutoRecord/ into Timelines (FFLogs-like flow).</summary>
internal sealed class AutoRecordImportPanel
{
    private readonly AutoRecordStore _store;
    private readonly Action<TimelineDocument> _onImported;

    private string _selectedPath = string.Empty;
    private string _fileSearchFilter = string.Empty;
    private TimelineDocument? _loaded;
    private string _status = string.Empty;

    private string _title = string.Empty;
    private uint _classJobId;
    private uint _territoryTypeId;
    private uint _contentFinderConditionId;
    private byte _zoneClassJobLevel;
    private string _autoLoadPresetId = string.Empty;
    private bool _autoLoadEnabled = true;
    private bool _importEnemyHitMemos;
    private bool _importSync;
    private uint _syncOptionTerritoryId;
    private string _zoneSearchFilter = string.Empty;
    private string _zoneLabel = string.Empty;

    /// <summary>Job used for Create filter.</summary>
    public uint SelectedClassJobId => _classJobId;

    public AutoRecordImportPanel(AutoRecordStore store, Action<TimelineDocument> onImported)
    {
        _store = store;
        _onImported = onImported;
    }

    public void Draw()
    {
        MirageUi.Header(I18n.Get("autorecord.header.import"));
        DrawFilePicker();

        if (!string.IsNullOrWhiteSpace(_status))
            MirageUi.Text(_status, MirageUi.Color.Secondary);

        if (_loaded == null)
            return;

        DrawLoadedFileEditor();
    }

    private void DrawLoadedFileEditor()
    {
        MirageUi.SubHeader(I18n.Format("autorecord.subheader.file", Path.GetFileName(_selectedPath)));
        MirageUi.Text(
            I18n.Format("autorecord.status.cues", _loaded!.Cues.Count),
            MirageUi.Color.Secondary);

        DrawTimelineMeta();
        DrawImportOptions();
        DrawAutoLoadSection();
        DrawCreateButton();
    }

    private void DrawFilePicker()
    {
        var files = _store.ListFilesNewestFirst();
        var labels = files.Select(Path.GetFileName).Where(n => !string.IsNullOrEmpty(n)).Cast<string>().ToList();
        var selectedLabel = string.IsNullOrEmpty(_selectedPath)
            ? string.Empty
            : Path.GetFileName(_selectedPath) ?? string.Empty;

        if (!MirageUi.SearchableDropdown(
                I18n.Get("autorecord.label.file"),
                ref selectedLabel,
                labels,
                ref _fileSearchFilter,
                placeholder: I18n.Get("autorecord.file.not_set"),
                id: "autoRecordFile",
                allowClear: true,
                emptyMessage: I18n.Get("autorecord.empty.no_files"),
                searchHint: I18n.Get("autorecord.file.search_hint"),
                width: MirageUi.InputWidthFill))
            return;

        if (string.IsNullOrWhiteSpace(selectedLabel))
        {
            ClearLoaded();
            return;
        }

        var path = files.FirstOrDefault(p =>
            string.Equals(Path.GetFileName(p), selectedLabel, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(path))
            return;

        if (string.Equals(path, _selectedPath, StringComparison.OrdinalIgnoreCase) && _loaded != null)
            return;

        LoadFile(path);
    }

    private void DrawTimelineMeta()
    {
        ImportTimelineUi.DrawName(ref _title, "autoRecordTitle");
        ImportTimelineUi.DrawZoneField(
            editable: true,
            id: "autoRecordZoneGroup",
            ref _territoryTypeId,
            ref _contentFinderConditionId,
            ref _zoneClassJobLevel,
            ref _zoneLabel,
            ref _zoneSearchFilter);
    }

    private void DrawImportOptions() =>
        ImportTimelineUi.DrawImportOptions(
            ref _importEnemyHitMemos,
            ref _importSync,
            ref _syncOptionTerritoryId,
            _territoryTypeId);

    private void DrawAutoLoadSection() =>
        ImportTimelineUi.DrawAutoLoad(
            "autoRecord",
            ref _autoLoadEnabled,
            ref _territoryTypeId,
            ref _contentFinderConditionId,
            ref _zoneClassJobLevel,
            ref _zoneLabel,
            ref _zoneSearchFilter,
            ref _classJobId,
            ref _autoLoadPresetId);

    private void DrawCreateButton()
    {
        MirageUi.PaddedSeparator();
        var canCreate = ImportTimelineUi.CanCreate(
            _classJobId,
            _territoryTypeId,
            _importEnemyHitMemos,
            _importSync);
        using (ImRaii.Disabled(_loaded == null || !canCreate))
        {
            if (MirageUi.PrimaryButton(I18n.Get("fflogs.button.create_timeline"), id: "autoRecordCreate"))
                CreateTimeline();
        }

        if (_loaded != null && !canCreate)
        {
            MirageUi.Text(
                I18n.Get("fflogs.empty.no_import_actions"),
                MirageUi.Color.Secondary);
        }
    }

    private void LoadFile(string path)
    {
        var doc = _store.TryLoad(path);
        if (doc == null)
        {
            ClearLoaded();
            _status = I18n.Get("autorecord.status.load_failed");
            return;
        }

        _selectedPath = path;
        _loaded = doc;
        _title = string.IsNullOrWhiteSpace(doc.Name) ? Path.GetFileNameWithoutExtension(path) : doc.Name;
        _classJobId = doc.ClassJobId;
        _territoryTypeId = doc.TerritoryTypeId;
        _contentFinderConditionId = doc.ContentFinderConditionId;
        _zoneClassJobLevel = doc.ClassJobLevel;
        _autoLoadPresetId = doc.AutoLoadPresetId ?? string.Empty;
        _autoLoadEnabled = doc.AutoLoadEnabled;
        _zoneLabel = _territoryTypeId == 0
            ? string.Empty
            : DutyContentCatalog.ResolveZoneLabel(
                _territoryTypeId,
                _contentFinderConditionId,
                _zoneClassJobLevel);
        _status = I18n.Format("autorecord.status.loaded", doc.Cues.Count);
    }

    private void ClearLoaded()
    {
        _selectedPath = string.Empty;
        _loaded = null;
        _title = string.Empty;
        _status = string.Empty;
    }

    private void CreateTimeline()
    {
        if (_loaded == null)
            return;

        var allowed = C.GetFFLogsImportActionIds(_classJobId);
        var syncSpecs = _importSync
            ? EnemySyncPresets.SpecsFor(_territoryTypeId)
            : [];
        if (allowed.Count == 0
            && !_importEnemyHitMemos
            && syncSpecs.Count == 0)
        {
            _status = I18n.Get("fflogs.status.select_actions");
            return;
        }

        var actionCues = allowed.Count == 0
            ? new List<TimelineCue>()
            : _loaded.Cues
                .Where(c => c.Kind == TimelineCueKind.Action && allowed.Contains(c.ActionId))
                .Select(c =>
                {
                    var copy = new TimelineCue
                    {
                        TimeOffsetSec = c.TimeOffsetSec,
                        Kind = TimelineCueKind.Action,
                        ActionId = c.ActionId,
                    };
                    CueTargetCatalog.Copy(c, copy);
                    return copy;
                })
                .ToList();
        var memos = _importEnemyHitMemos
            ? _loaded.Cues
                .Where(c => c.Kind == TimelineCueKind.Memo)
                .Select(c => new TimelineCue
                {
                    TimeOffsetSec = c.TimeOffsetSec,
                    Kind = TimelineCueKind.Memo,
                    ActionId = c.ActionId,
                    Label = c.Label,
                })
                .ToList()
            : new List<TimelineCue>();
        var syncCues = EnemySyncPresets.TakeFirstCues(_loaded.Cues, syncSpecs);
        var cues = actionCues
            .Concat(memos)
            .Concat(syncCues)
            .OrderBy(c => c.TimeOffsetSec)
            .ThenBy(c => c.Kind)
            .ToList();

        if (cues.Count == 0)
        {
            if (_importSync)
                _status = I18n.Get("fflogs.status.no_enemy_sync");
            else if (_importEnemyHitMemos)
                _status = I18n.Get("fflogs.status.no_enemy_hits");
            else
                _status = I18n.Format("fflogs.status.no_match", _loaded.Cues.Count);
            return;
        }

        var name = string.IsNullOrWhiteSpace(_title)
            ? I18n.Get("config.default.untitled")
            : _title.Trim();
        if (name.Length > 80)
            name = name[..80];

        var document = new TimelineDocument
        {
            Name = name,
            AutoLoadEnabled = _autoLoadEnabled,
            TerritoryTypeId = _territoryTypeId,
            ContentFinderConditionId = _contentFinderConditionId,
            ClassJobLevel = _zoneClassJobLevel,
            ClassJobId = _classJobId,
            AutoLoadPresetId = _autoLoadPresetId,
            Cues = cues,
        };

        _status = FormatCreatedStatus(
            document.Name,
            actionCues.Count,
            memos.Count,
            syncCues.Count,
            _loaded.Cues.Count);
        PluginServices.ChatGui.Print(I18n.Format("autorecord.chat.imported", document.Name));
        _onImported(document);
    }

    private static string FormatCreatedStatus(
        string name,
        int actionCount,
        int memoCount,
        int syncCount,
        int recordedCount)
    {
        var status = memoCount > 0
            ? I18n.Format("fflogs.status.created_with_memos", name, actionCount, memoCount)
            : I18n.Format("autorecord.status.created", name, actionCount, recordedCount);
        return ImportTimelineUi.WithSyncCount(status, syncCount);
    }
}
