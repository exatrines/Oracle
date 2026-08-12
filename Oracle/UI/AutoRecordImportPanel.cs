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
    private int _sceneId;
    private bool _sceneFilterEnabled;
    private bool _autoLoadEnabled = true;
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
        MirageUi.SubHeader(I18n.Get("fflogs.subheader.timeline"));
        MirageUi.InputText(I18n.Get("fflogs.label.name"), ref _title, 80, id: "autoRecordTitle");
        DrawZoneField(editable: true, id: "autoRecordZoneGroup");
    }

    private void DrawAutoLoadSection()
    {
        MirageUi.SubHeader(I18n.Get("config.subheader.auto_load"));
        var autoLoad = _autoLoadEnabled;
        if (MirageUi.Checkbox(I18n.Get("config.checkbox.enable_auto_load"), ref autoLoad))
            _autoLoadEnabled = autoLoad;

        DrawZoneField(editable: false, id: "autoRecordZoneReadonly");
        DrawJobField();
        DrawSceneField();
    }

    private void DrawZoneField(bool editable, string id)
    {
        if (!editable)
        {
            ZoneCombo.DrawReadonly(
                I18n.Get("config.label.zone"),
                _territoryTypeId,
                _contentFinderConditionId,
                _zoneClassJobLevel,
                id: id);
            return;
        }

        ZoneCombo.Draw(
            I18n.Get("config.label.zone_group"),
            ref _territoryTypeId,
            ref _contentFinderConditionId,
            ref _zoneClassJobLevel,
            ref _zoneLabel,
            ref _zoneSearchFilter,
            id: id);
    }

    private void DrawJobField()
    {
        var jobId = _classJobId;
        if (!JobCombo.Draw(I18n.Get("config.label.job"), ref jobId, id: "autoRecordJob"))
            return;

        _classJobId = jobId;
    }

    private void DrawSceneField()
    {
        var sceneId = _sceneId;
        var filter = _sceneFilterEnabled;
        if (SceneFilterField.DrawLabeled(
                I18n.Get("config.label.scene_id"),
                "autoRecordScene",
                ref filter,
                ref sceneId))
        {
            _sceneFilterEnabled = filter;
            _sceneId = Math.Max(0, sceneId);
        }
    }

    private void DrawCreateButton()
    {
        MirageUi.PaddedSeparator();
        var allowed = C.GetFFLogsImportActionIds(_classJobId);
        using (ImRaii.Disabled(_loaded == null || allowed.Count == 0))
        {
            if (MirageUi.PrimaryButton(I18n.Get("fflogs.button.create_timeline"), id: "autoRecordCreate"))
                CreateTimeline();
        }

        if (_loaded != null && allowed.Count == 0)
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
        _sceneId = (int)doc.SceneId;
        _sceneFilterEnabled = doc.SceneFilterEnabled;
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
        if (allowed.Count == 0)
        {
            _status = I18n.Get("fflogs.status.select_actions");
            return;
        }

        var cues = _loaded.Cues
            .Where(c => c.Kind == TimelineCueKind.Action && allowed.Contains(c.ActionId))
            .Select(c => new TimelineCue
            {
                TimeOffsetSec = c.TimeOffsetSec,
                Kind = TimelineCueKind.Action,
                ActionId = c.ActionId,
            })
            .ToList();

        if (cues.Count == 0)
        {
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
            SceneId = (uint)_sceneId,
            SceneFilterEnabled = _sceneFilterEnabled,
            Cues = cues,
        };

        _status = I18n.Format("autorecord.status.created", document.Name, cues.Count, _loaded.Cues.Count);
        PluginServices.ChatGui.Print(I18n.Format("autorecord.chat.imported", document.Name));
        _onImported(document);
    }
}
