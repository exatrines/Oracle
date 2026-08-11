using System.Diagnostics;
using Oracle.Models;
using Oracle.Services;

namespace Oracle.UI;

// --- Sidebar: build tree ↁEselection/reorder ↁEtimeline CRUD ---

internal sealed partial class ConfigWindow
{
    private MirageTwoColumnState CreateTimelineTwoColumnState()
    {
        var sidebarNodes = BuildZoneFolderNodes();
        var iconPath = File.Exists(_pluginIconPath) ? _pluginIconPath : null;

        return new MirageTwoColumnState
        {
            ShowSidebarHeader = true,
            ShowSidebarFooter = false,
            ShowSearch = true,
            SearchHint = I18n.Get("sidebar.search_hint"),
            SearchFilter = _sidebarSearch,
            AllowDeselect = false,
            AutoSelectFirstOnSearch = true,
            EnableEntryReorder = true,
            EntryReorderDragId = _entryReorderDragId,
            OnEntryReorderDragIdChanged = id => _entryReorderDragId = id,
            OnEntryReordered = OnTimelineReordered,
            CollapsedFolderIds = _collapsedFolderIds,
            SidebarHeader = new MirageTwoColumnSidebarHeader
            {
                ImagePath = iconPath,
                ImageWidth = 48f,
                ImageHeight = 48f,
                Title = I18n.Get("window.config.title"),
                Subtitle = $"v{PluginServices.PluginInterface.Manifest.AssemblyVersion}",
                TrailingActions =
                [
                    new MirageTwoColumnTrailingAction
                    {
                        Id = "settings",
                        Icon = FontAwesomeIcon.Cog,
                        Tooltip = I18n.Get("sidebar.tooltip.settings"),
                        OnClick = _openPluginSettings,
                    },
                ],
            },
            SearchTrailingActions =
            [
                new MirageTwoColumnTrailingAction
                {
                    Id = "add",
                    Icon = FontAwesomeIcon.Plus,
                    Tooltip = I18n.Get("sidebar.tooltip.add"),
                    ContextMenuItems =
                    [
                        new MirageTwoColumnContextMenuItem
                        {
                            Id = "new",
                            Label = I18n.Get("sidebar.menu.new"),
                            Icon = FontAwesomeIcon.File,
                            OnClick = CreateTimeline,
                        },
                        new MirageTwoColumnContextMenuItem
                        {
                            Id = "importFflogs",
                            Label = I18n.Get("sidebar.menu.import_fflogs"),
                            Icon = FontAwesomeIcon.CloudDownloadAlt,
                            OnClick = () => _selectedTabId = TabFFLogsImport,
                        },
                        new MirageTwoColumnContextMenuItem
                        {
                            Id = "importAutoRecord",
                            Label = I18n.Get("sidebar.menu.import_auto_record"),
                            Icon = FontAwesomeIcon.History,
                            OnClick = () => _selectedTabId = TabAutoRecordImport,
                        },
                    ],
                },
                new MirageTwoColumnTrailingAction
                {
                    Id = "folder",
                    Icon = FontAwesomeIcon.FolderOpen,
                    Tooltip = I18n.Get("sidebar.tooltip.open_folder"),
                    OnClick = OpenTimelinesFolder,
                },
                new MirageTwoColumnTrailingAction
                {
                    Id = "reload",
                    Icon = FontAwesomeIcon.Sync,
                    Tooltip = I18n.Get("sidebar.tooltip.reload"),
                    OnClick = () => _store.Reload(),
                },
            ],
            SidebarNodes = sidebarNodes,
            SelectedId = IsUtilityTab(_selectedTabId) ? null : _selectedTabId,
            OnSelectionChanged = OnSidebarSelectionChanged,
            OnSearchFilterChanged = filter => _sidebarSearch = filter,
        };
    }

    private List<MirageTwoColumnSidebarNode> BuildZoneFolderNodes()
    {
        var zoneOrder = new List<uint>();
        var byZone = new Dictionary<uint, List<TimelineDocument>>();

        foreach (var doc in _store.Documents)
        {
            if (!byZone.TryGetValue(doc.TerritoryTypeId, out var list))
            {
                list = [];
                byZone[doc.TerritoryTypeId] = list;
                zoneOrder.Add(doc.TerritoryTypeId);
            }

            list.Add(doc);
        }

        var nodes = new List<MirageTwoColumnSidebarNode>(zoneOrder.Count);
        foreach (var territory in zoneOrder)
        {
            var docs = byZone[territory];
            nodes.Add(new MirageTwoColumnFolderNode
            {
                Id = ZoneFolderId(territory),
                Label = ResolveZoneFolderLabel(territory, docs),
                Entries = docs.Select(CreateTimelineEntry).ToList(),
            });
        }

        return nodes;
    }

    private MirageTwoColumnEntry CreateTimelineEntry(TimelineDocument doc) =>
        new()
        {
            Id = TimelinePageId(doc.Id),
            Label = doc.Name,
            LabelColor = ResolveSidebarLabelColor(doc),
            ContextMenuItems =
            [
                new MirageTwoColumnContextMenuItem
                {
                    Id = "load",
                    Label = I18n.Get("sidebar.menu.load"),
                    Icon = FontAwesomeIcon.Play,
                    OnClick = () => LoadTimelineFromSidebar(doc),
                },
                new MirageTwoColumnContextMenuItem
                {
                    Id = "duplicate",
                    Label = I18n.Get("sidebar.menu.duplicate"),
                    Icon = FontAwesomeIcon.Copy,
                    OnClick = () => DuplicateTimeline(doc.Id),
                },
                new MirageTwoColumnContextMenuItem
                {
                    Id = "delete",
                    Label = I18n.Get("sidebar.menu.delete"),
                    Icon = FontAwesomeIcon.Trash,
                    OnClick = () => DeleteTimeline(doc.Id),
                },
            ],
        };

    private void LoadTimelineFromSidebar(TimelineDocument doc)
    {
        _engine.ManualLoad(doc);
        PluginServices.ChatGui.Print(I18n.Format("config.chat.loaded", doc.Name));
    }

    private void DuplicateTimeline(string id)
    {
        var created = _store.Duplicate(id);
        if (created == null)
            return;

        _store.SetActive(created.Id);
        _editDoc = created;
        _selectedTabId = TimelinePageId(created.Id);
        PluginServices.ChatGui.Print(I18n.Format("config.chat.duplicated", created.Name));
    }

    private Vector4? ResolveSidebarLabelColor(TimelineDocument doc)
    {
        // Green = currently loaded by the engine (Manual Load or Auto Load match).
        var loaded = _engine.ActiveDocument;
        if (loaded != null
            && string.Equals(loaded.Id, doc.Id, StringComparison.OrdinalIgnoreCase))
            return StandbyMatchGreen;

        return null;
    }

    private static string ZoneFolderId(uint territoryTypeId) => $"zone:{territoryTypeId}";

    private static string ResolveZoneFolderLabel(uint territoryTypeId, IReadOnlyList<TimelineDocument> docs)
    {
        if (territoryTypeId == 0)
            return I18n.Get("sidebar.folder.not_set");

        var sample = docs[0];
        var label = DutyContentCatalog.StripZoneLabelPrefix(DutyContentCatalog.ResolveZoneLabel(
            territoryTypeId,
            sample.ContentFinderConditionId,
            sample.ClassJobLevel));

        return string.IsNullOrWhiteSpace(label) ? I18n.Get("sidebar.folder.unknown_zone") : label;
    }

    private void OnTimelineReordered(string pageId, int insertIndex)
    {
        if (!TryParseTimelinePageId(pageId, out var timelineId))
            return;

        _store.Reorder(timelineId, insertIndex);
    }

    private void OnSidebarSelectionChanged(string id)
    {
        if (string.IsNullOrEmpty(id))
            return;

        _selectedTabId = id;
        if (TryParseTimelinePageId(id, out var timelineId))
        {
            _store.SetActive(timelineId);
            _editDoc = _store.Documents.FirstOrDefault(d =>
                string.Equals(d.Id, timelineId, StringComparison.OrdinalIgnoreCase));
        }
    }

    private void CreateTimeline()
    {
        var playerJob = PluginServices.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 19;
        var created = _store.CreateNew(_store.AllocateNewTimelineName(), playerJob);
        var previousTerritory = created.TerritoryTypeId;
        ApplyCurrentZone(created);
        PersistDocument(created);
        if (created.TerritoryTypeId != previousTerritory)
            _store.MoveToEndOfZone(created.Id);
        _store.SetActive(created.Id);
        _editDoc = created;
        _selectedTabId = TimelinePageId(created.Id);
    }

    private void DeleteTimeline(string id)
    {
        if (!_store.DeleteDocument(id))
            return;

        if (_editDoc != null && string.Equals(_editDoc.Id, id, StringComparison.OrdinalIgnoreCase))
            _editDoc = null;

        if (TryParseTimelinePageId(_selectedTabId, out var selectedId)
            && string.Equals(selectedId, id, StringComparison.OrdinalIgnoreCase))
        {
            var next = _store.Documents.FirstOrDefault();
            _selectedTabId = next != null ? TimelinePageId(next.Id) : string.Empty;
            _editDoc = next;
            if (next != null)
                _store.SetActive(next.Id);
        }

        PluginServices.ChatGui.Print(I18n.Get("config.chat.deleted"));
    }

    private void OpenTimelinesFolder()
    {
        try
        {
            Directory.CreateDirectory(_store.TimelinesDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = _store.TimelinesDirectory,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            PluginServices.Log.Warning(ex, "Failed to open timelines folder");
        }
    }
}
