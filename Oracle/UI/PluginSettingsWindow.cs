namespace Oracle.UI;

// --- Shell: lifecycle → sidebar → page dispatch ---

/// <summary>Plugin settings: General, Overlay, Auto Record, Import.</summary>
internal sealed partial class PluginSettingsWindow : Window
{
    private const string SettingsGeneral = "settings:general";
    private const string SettingsPluginLog = "settings:plugin-log";
    private const string SettingsOverlayFolder = "settings:overlay";
    private const string SettingsTimeline = "settings:timeline";
    private const string SettingsMajor = "settings:major";
    private const string SettingsActionHighlight = "settings:action-highlight";
    private const string SettingsHotbar = "settings:hotbar";
    private const string SettingsImportFolder = "settings:import";
    private const string SettingsFFLogsApi = "settings:fflogs-api";
    private const string SettingsImportAction = "settings:import-action";
    private const string SettingsSyncPresets = "settings:sync-presets";
    private const string SettingsAutoLoadPresets = "settings:autoload-presets";
    private const string SettingsAutoRecordFolder = "settings:auto-record";
    private const string SettingsAutoRecord = "settings:auto-record-page";
    private const string AutoRecordZoneFilterEnabled = "enabled";
    private const string AutoRecordZoneFilterDisabled = "disabled";
    private const string AutoRecordZoneSortZoneId = "zoneId";

    private string _selectedTabId = SettingsGeneral;
    private string _sidebarSearch = string.Empty;
    private uint _importActionJobId;
    private string _autoRecordZoneSearch = string.Empty;
    private readonly HashSet<string> _autoRecordZoneFilterIds = new(StringComparer.Ordinal);
    private bool _autoRecordZoneSortAscending;
    private readonly HashSet<string> _collapsedFolderIds = new(StringComparer.Ordinal);
    private ImRaii.ColorDisposable? _themeScope;

    public PluginSettingsWindow()
        : base("Oracle Settings###oraclePluginSettings", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        MirageWindowDefaults.ApplyTo(this);
        Size = new Vector2(780, 560);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void ToggleAutoRecordPage()
    {
        _selectedTabId = SettingsAutoRecord;
        Toggle();
    }

    public override void PreDraw()
    {
        WindowName = I18n.Get("window.settings.title") + "###oraclePluginSettings";
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
        MirageUi.TwoColumn.Draw(CreateTwoColumnState(), DrawMainContent);
    }

    private MirageTwoColumnState CreateTwoColumnState() =>
        new()
        {
            ShowSidebarHeader = false,
            ShowSidebarFooter = false,
            ShowSearch = true,
            SearchHint = I18n.Get("settings.search_hint"),
            SearchFilter = _sidebarSearch,
            AllowDeselect = false,
            AutoSelectFirstOnSearch = true,
            EnableEntryReorder = false,
            CollapsedFolderIds = _collapsedFolderIds,
            SidebarNodes = BuildSettingsSidebarNodes(),
            SelectedId = _selectedTabId,
            OnSelectionChanged = id =>
            {
                if (!string.IsNullOrEmpty(id))
                    _selectedTabId = id;
            },
            OnSearchFilterChanged = filter => _sidebarSearch = filter,
        };

    private static List<MirageTwoColumnSidebarNode> BuildSettingsSidebarNodes() =>
    [
        new MirageTwoColumnPageNode
        {
            Entry = new MirageTwoColumnEntry
            {
                Id = SettingsGeneral,
                Label = I18n.Get("settings.sidebar.general"),
            },
        },
        new MirageTwoColumnPageNode
        {
            Entry = new MirageTwoColumnEntry
            {
                Id = SettingsPluginLog,
                Label = I18n.Get("settings.sidebar.plugin_log"),
            },
        },
        new MirageTwoColumnFolderNode
        {
            Id = SettingsOverlayFolder,
            Label = I18n.Get("settings.sidebar.overlay"),
            AlwaysExpanded = true,
            Entries =
            [
                new MirageTwoColumnEntry
                {
                    Id = SettingsTimeline,
                    Label = I18n.Get("settings.sidebar.timeline"),
                },
                new MirageTwoColumnEntry
                {
                    Id = SettingsMajor,
                    Label = I18n.Get("settings.sidebar.major"),
                },
                new MirageTwoColumnEntry
                {
                    Id = SettingsHotbar,
                    Label = I18n.Get("settings.sidebar.hotbar"),
                },
                new MirageTwoColumnEntry
                {
                    Id = SettingsActionHighlight,
                    Label = I18n.Get("settings.sidebar.action_highlight"),
                },
            ],
        },
        new MirageTwoColumnFolderNode
        {
            Id = SettingsAutoRecordFolder,
            Label = I18n.Get("settings.sidebar.auto_record"),
            AlwaysExpanded = true,
            Entries =
            [
                new MirageTwoColumnEntry
                {
                    Id = SettingsAutoRecord,
                    Label = I18n.Get("settings.sidebar.auto_record_page"),
                },
            ],
        },
        new MirageTwoColumnFolderNode
        {
            Id = SettingsImportFolder,
            Label = I18n.Get("settings.sidebar.import"),
            AlwaysExpanded = true,
            Entries =
            [
                new MirageTwoColumnEntry
                {
                    Id = SettingsFFLogsApi,
                    Label = I18n.Get("settings.sidebar.import_fflogs"),
                },
                new MirageTwoColumnEntry
                {
                    Id = SettingsImportAction,
                    Label = I18n.Get("settings.sidebar.import_actions"),
                },
                new MirageTwoColumnEntry
                {
                    Id = SettingsSyncPresets,
                    Label = I18n.Get("settings.sidebar.import_sync_presets"),
                },
                new MirageTwoColumnEntry
                {
                    Id = SettingsAutoLoadPresets,
                    Label = I18n.Get("settings.sidebar.autoload_presets"),
                },
            ],
        },
    ];

    // --- Main content (matches sidebar order) ---

    private void DrawMainContent()
    {
        switch (_selectedTabId)
        {
            case SettingsGeneral:
                DrawGeneralSettings();
                return;
            case SettingsPluginLog:
                DrawPluginLogSettings();
                return;
            case SettingsTimeline:
                DrawTimelineSettings();
                return;
            case SettingsMajor:
                DrawMajorOverlaySettings();
                return;
            case SettingsActionHighlight:
                DrawActionHighlightSettings();
                return;
            case SettingsHotbar:
                DrawHotbarSettings();
                return;
            case SettingsAutoRecord:
                DrawAutoRecordSettings();
                return;
            case SettingsFFLogsApi:
                MirageUi.Header(I18n.Get("settings.header.import_fflogs"));
                FFLogsApiCredentialsUi.Draw("fflogsSettings");
                return;
            case SettingsImportAction:
                DrawImportActionSettings();
                return;
            case SettingsSyncPresets:
                DrawSyncPresetSettings();
                return;
            case SettingsAutoLoadPresets:
                DrawAutoLoadPresetSettings();
                return;
            default:
                MirageUi.Header(I18n.Get("settings.header.settings"));
                MirageUi.Text(I18n.Get("settings.empty.select_page"), MirageUi.Color.Secondary);
                return;
        }
    }
}
