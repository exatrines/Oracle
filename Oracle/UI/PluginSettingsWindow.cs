using Oracle.Models;
using Oracle.Services;
using MirageUI.Ui;

namespace Oracle.UI;

/// <summary>Plugin settings window (Overlay Settings / FFLogs Import Settings).</summary>
internal sealed class PluginSettingsWindow : Window
{
    private const string SettingsGeneral = "settings:general";
    private const string SettingsOverlayFolder = "settings:overlay";
    private const string SettingsTimeline = "settings:timeline";
    private const string SettingsMajor = "settings:major";
    private const string SettingsActionHighlight = "settings:action-highlight";
    private const string SettingsHotbar = "settings:hotbar";
    private const string SettingsImportFolder = "settings:import";
    private const string SettingsFFLogsApi = "settings:fflogs-api";
    private const string SettingsImportAction = "settings:import-action";
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
            default:
                MirageUi.Header(I18n.Get("settings.header.settings"));
                MirageUi.Text(I18n.Get("settings.empty.select_page"), MirageUi.Color.Secondary);
                return;
        }
    }

    // --- General ---

    private static void DrawGeneralSettings()
    {
        MirageUi.Header(I18n.Get("settings.header.general"));

        var selected = NormalizeUiLanguageSetting(C.UiLanguage);
        var labels = new[]
        {
            I18n.Get("settings.language.client"),
            I18n.Get("settings.language.en"),
            I18n.Get("settings.language.ja"),
        };
        var values = new[] { I18n.FollowClient, "en", "ja" };
        var selectedLabel = labels[Array.IndexOf(values, selected)];

        if (!MirageUi.Dropdown(
                I18n.Get("settings.label.ui_language"),
                ref selectedLabel,
                labels,
                id: "uiLanguage",
                allowClear: false))
            return;

        var index = Array.IndexOf(labels, selectedLabel);
        if (index < 0)
            return;

        var next = values[index];
        if (string.Equals(C.UiLanguage, next, StringComparison.OrdinalIgnoreCase))
            return;

        C.UiLanguage = next;
        C.Save();
        I18n.ApplyFromConfig();
    }

    private static string NormalizeUiLanguageSetting(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || string.Equals(value, I18n.FollowClient, StringComparison.OrdinalIgnoreCase))
            return I18n.FollowClient;

        var lang = value.Trim().ToLowerInvariant();
        if (lang.Length > 2)
            lang = lang[..2];
        return lang is "en" or "ja" ? lang : I18n.FollowClient;
    }

    // --- Timeline ---

    private static void DrawTimelineSettings()
    {
        MirageUi.Header(I18n.Get("settings.header.timeline"));

        var showOverlay = C.ShowOverlay;
        if (MirageUi.Checkbox(I18n.Get("settings.checkbox.enable"), ref showOverlay))
        {
            C.ShowOverlay = showOverlay;
            C.Save();
        }

        var clickThrough = C.OverlayClickThrough;
        if (MirageUi.Checkbox(I18n.Get("settings.checkbox.click_through"), ref clickThrough))
        {
            C.OverlayClickThrough = clickThrough;
            C.Save();
        }

        MirageUi.Text(I18n.Get("settings.help.click_through_alt"), MirageUi.Color.Secondary);

        var lookahead = C.LookaheadSeconds;
        if (MirageUi.SliderFloat(I18n.Get("settings.slider.lookahead"), ref lookahead, 5f, 60f, "%.0f"))
        {
            C.LookaheadSeconds = lookahead;
            C.Save();
        }

        var maxRows = C.OverlayMaxRows;
        if (MirageUi.SliderInt(I18n.Get("settings.slider.max_rows"), ref maxRows, 1, 30))
        {
            C.OverlayMaxRows = Math.Clamp(maxRows, 1, 50);
            C.Save();
        }
    }

    // --- Major ---

    private static void DrawMajorOverlaySettings()
    {
        MirageUi.Header(I18n.Get("settings.header.major"));

        var show = C.ShowMajorOverlay;
        if (MirageUi.Checkbox(I18n.Get("settings.checkbox.enable"), ref show))
        {
            C.ShowMajorOverlay = show;
            C.Save();
        }

        var clickThrough = C.MajorOverlayClickThrough;
        if (MirageUi.Checkbox(I18n.Get("settings.checkbox.click_through"), ref clickThrough))
        {
            C.MajorOverlayClickThrough = clickThrough;
            C.Save();
        }

        MirageUi.Text(I18n.Get("settings.help.click_through_alt"), MirageUi.Color.Secondary);

        MirageUi.SubHeader(I18n.Get("settings.subheader.layout"));
        MirageUi.Text(
            I18n.Get("settings.major.help.visible_range"),
            MirageUi.Color.Secondary);

        var laneSingle = I18n.Get("settings.major.lane.single");
        var laneAbilitySkill = I18n.Get("settings.major.lane.ability_skill");
        var laneSelected = C.MajorLaneMode == MajorOverlayLaneMode.AbilityAndSkill
            ? laneAbilitySkill
            : laneSingle;
        if (MirageUi.Dropdown(
                I18n.Get("settings.major.lane_mode"),
                ref laneSelected,
                [laneSingle, laneAbilitySkill],
                allowClear: false,
                id: "MajorLaneMode"))
        {
            C.MajorLaneMode = string.Equals(laneSelected, laneAbilitySkill, StringComparison.Ordinal)
                ? MajorOverlayLaneMode.AbilityAndSkill
                : MajorOverlayLaneMode.Single;
            C.Save();
        }

        MirageUi.Text(I18n.Get("settings.major.help.lane"), MirageUi.Color.Secondary);

        var before = C.MajorBeforeSeconds;
        if (MirageUi.SliderFloat(
                I18n.Get("settings.slider.before_sec"),
                ref before,
                0f,
                30f,
                "%.1f",
                id: "MajorLayoutBefore"))
        {
            C.MajorBeforeSeconds = Math.Clamp(before, 0f, 60f);
            C.Save();
        }

        var after = C.MajorAfterSeconds;
        if (MirageUi.SliderFloat(
                I18n.Get("settings.slider.after_sec"),
                ref after,
                0f,
                30f,
                "%.1f",
                id: "MajorLayoutAfter"))
        {
            C.MajorAfterSeconds = Math.Clamp(after, 0f, 60f);
            C.Save();
        }

        var pps = C.MajorPixelsPerSecond;
        if (MirageUi.SliderFloat(I18n.Get("settings.slider.width_per_sec"), ref pps, 10f, 120f, "%.0f"))
        {
            C.MajorPixelsPerSecond = Math.Clamp(pps, 4f, 200f);
            C.Save();
        }

        var iconSize = C.MajorIconSize;
        if (MirageUi.SliderFloat(I18n.Get("settings.slider.icon_size"), ref iconSize, 16f, 64f, "%.0f"))
        {
            C.MajorIconSize = Math.Clamp(iconSize, 12f, 96f);
            C.Save();
        }

        MirageUi.Text(
            I18n.Format(
                "settings.major.visible_range_live",
                Math.Max(0f, C.MajorAfterSeconds),
                Math.Max(0f, C.MajorBeforeSeconds)),
            MirageUi.Color.Secondary);

        MirageUi.SubHeader(I18n.Get("settings.subheader.display"));

        var showTitle = C.MajorShowTitle;
        if (MirageUi.Checkbox(I18n.Get("settings.checkbox.show_title"), ref showTitle))
        {
            C.MajorShowTitle = showTitle;
            C.Save();
        }

        var showLabels = C.MajorShowSecondLabels;
        if (MirageUi.Checkbox(I18n.Get("settings.checkbox.show_second_labels"), ref showLabels))
        {
            C.MajorShowSecondLabels = showLabels;
            C.Save();
        }

        var showGrid = C.MajorShowGrid;
        if (MirageUi.Checkbox(I18n.Get("settings.checkbox.show_grid"), ref showGrid))
        {
            C.MajorShowGrid = showGrid;
            C.Save();
        }

        MirageUi.SubHeader(I18n.Get("settings.subheader.colors"));

        var bg = C.MajorBackgroundColor;
        if (MirageUi.ColorEdit4(I18n.Get("settings.color.background"), ref bg))
        {
            C.MajorBackgroundColor = bg;
            C.Save();
        }

        var grid = C.MajorGridLineColor;
        if (MirageUi.ColorEdit4(I18n.Get("settings.color.grid_line"), ref grid))
        {
            C.MajorGridLineColor = grid;
            C.Save();
        }

        var zeroColor = C.MajorZeroLineColor;
        if (MirageUi.ColorEdit4(I18n.Get("settings.color.zero_line"), ref zeroColor))
        {
            C.MajorZeroLineColor = zeroColor;
            C.Save();
        }

        var zeroThickness = C.MajorZeroLineThickness;
        if (MirageUi.SliderFloat(I18n.Get("settings.slider.zero_thickness"), ref zeroThickness, 1f, 8f, "%.1f"))
        {
            C.MajorZeroLineThickness = Math.Clamp(zeroThickness, 1f, 8f);
            C.Save();
        }

        var labelColor = C.MajorLabelColor;
        if (MirageUi.ColorEdit4(I18n.Get("settings.color.label"), ref labelColor))
        {
            C.MajorLabelColor = labelColor;
            C.Save();
        }
    }

    // --- Action Highlight ---

    private static void DrawActionHighlightSettings()
    {
        MirageUi.Header(I18n.Get("settings.header.action_highlight"));

        DrawHighlightPhaseSettings(
            I18n.Get("settings.subheader.before"),
            idSuffix: "ActionHighlightBefore",
            getSeconds: () => C.ActionHighlightBeforeSeconds,
            setSeconds: v => C.ActionHighlightBeforeSeconds = v,
            getThickness: () => C.ActionHighlightBeforeLineThickness,
            setThickness: v => C.ActionHighlightBeforeLineThickness = v,
            getColor: () => C.ActionHighlightBeforeLineColor,
            setColor: v => C.ActionHighlightBeforeLineColor = v,
            getBlink: () => C.ActionHighlightBeforeBlink,
            setBlink: v => C.ActionHighlightBeforeBlink = v,
            minSeconds: 0f,
            maxSeconds: 30f);

        DrawHighlightPhaseSettings(
            I18n.Get("settings.subheader.after"),
            idSuffix: "ActionHighlightAfter",
            getSeconds: () => C.ActionHighlightAfterSeconds,
            setSeconds: v => C.ActionHighlightAfterSeconds = v,
            getThickness: () => C.ActionHighlightAfterLineThickness,
            setThickness: v => C.ActionHighlightAfterLineThickness = v,
            getColor: () => C.ActionHighlightAfterLineColor,
            setColor: v => C.ActionHighlightAfterLineColor = v,
            getBlink: () => C.ActionHighlightAfterBlink,
            setBlink: v => C.ActionHighlightAfterBlink = v,
            minSeconds: 0f,
            maxSeconds: 30f);
    }

    // --- Hotbar ---

    private static void DrawHotbarSettings()
    {
        MirageUi.Header(I18n.Get("settings.header.hotbar"));

        var showHotbarHighlight = C.ShowHotbarHighlight;
        if (MirageUi.Checkbox(I18n.Get("settings.checkbox.enable"), ref showHotbarHighlight))
        {
            C.ShowHotbarHighlight = showHotbarHighlight;
            C.Save();
        }

        C.EnsureHotbarHighlightDefaults();

        MirageUi.Text(
            I18n.Get("settings.hotbar.help.select"),
            MirageUi.Color.Secondary);

        MirageUi.Text(I18n.Get("settings.label.hotbars"), wrap: false);
        DrawHotbarIdCheckboxRow(startId: 0, count: 10, formatKey: "settings.hotbar.hb");

        ImGui.Spacing();
        MirageUi.Text(I18n.Get("settings.label.cross_hotbars"), wrap: false);
        DrawHotbarIdCheckboxRow(startId: 10, count: 8, formatKey: "settings.hotbar.xhb");

        ImGui.Spacing();
        var doubleCross = C.ShowHotbarHighlightDoubleCross;
        if (MirageUi.Checkbox(I18n.Get("settings.checkbox.double_cross"), ref doubleCross))
        {
            C.ShowHotbarHighlightDoubleCross = doubleCross;
            C.Save();
        }
    }

    // --- Auto Record ---

    private void DrawAutoRecordSettings()
    {
        MirageUi.Header(I18n.Get("settings.header.auto_record"));

        var enabled = C.AutoRecordEnabled;
        if (MirageUi.Checkbox(I18n.Get("settings.checkbox.auto_record_enable"), ref enabled))
        {
            C.AutoRecordEnabled = enabled;
            C.Save();
        }

        MirageUi.Text(I18n.Get("settings.help.auto_record"), MirageUi.Color.Secondary);

        var manualSave = C.AutoRecordManualSave;
        using (var group = MirageUi.CheckboxGroup(
                   I18n.Get("settings.checkbox.auto_record_manual_save"),
                   ref manualSave))
        {
            if (group.Changed)
            {
                C.AutoRecordManualSave = manualSave;
                C.Save();
            }

            MirageUi.Text(I18n.Get("settings.help.auto_record_manual_save"), MirageUi.Color.Secondary);

            using (ImRaii.Disabled(!C.AutoRecordManualSave))
            {
                var saveOnNext = C.AutoRecordSavePendingOnNextCombat;
                if (MirageUi.Checkbox(
                        I18n.Get("settings.checkbox.auto_record_save_pending_on_next"),
                        ref saveOnNext))
                {
                    C.AutoRecordSavePendingOnNextCombat = saveOnNext;
                    C.Save();
                }

                MirageUi.Text(I18n.Get("settings.help.auto_record_save_pending_on_next"), MirageUi.Color.Secondary);
            }
        }

        var autoOpen = C.AutoRecordOverlayAutoOpenOnEffectiveZone;
        if (MirageUi.Checkbox(I18n.Get("settings.checkbox.auto_record_overlay_auto_open"), ref autoOpen))
        {
            C.AutoRecordOverlayAutoOpenOnEffectiveZone = autoOpen;
            C.Save();
        }

        MirageUi.Text(I18n.Get("settings.help.auto_record_overlay_auto_open"), MirageUi.Color.Secondary);

        var maxFiles = Math.Clamp(C.AutoRecordMaxFiles, 1, 200);
        if (MirageUi.SliderInt(I18n.Get("settings.slider.auto_record_max_files"), ref maxFiles, 1, 200))
        {
            C.AutoRecordMaxFiles = maxFiles;
            C.Save();
        }

        MirageUi.Text(I18n.Get("settings.help.auto_record_folder"), MirageUi.Color.Secondary);

        MirageUi.SubHeader(I18n.Get("settings.header.auto_record_zones"));
        MirageUi.Text(I18n.Get("settings.help.auto_record_zones"), MirageUi.Color.Secondary);

        C.EnsureAutoRecordZoneWhitelist();
        DrawAutoRecordZonePickerToolbar();
        MirageUi.PaddedSeparator();
        DrawAutoRecordZonePickerList(C.AutoRecordZoneWhitelist!.ToHashSet());
    }

    private void DrawAutoRecordZonePickerToolbar()
    {
        var toolbar = new MirageIconToolbarState
        {
            Search = _autoRecordZoneSearch,
            SearchHint = I18n.Get("config.zone.search_hint"),
            SearchId = "##autoRecordZoneSearch",
            Filter = new MirageToolbarFilterConfig
            {
                Id = "autoRecordZoneFilter",
                Tooltip = I18n.Get("settings.tooltip.auto_record_zone_filter"),
                Mode = MirageChecklistMode.Unique,
                SelectedIds = _autoRecordZoneFilterIds,
                Options =
                [
                    new MirageChecklistOption
                    {
                        Id = AutoRecordZoneFilterEnabled,
                        Label = I18n.Get("settings.filter.auto_record_zone_enabled_only"),
                    },
                    new MirageChecklistOption
                    {
                        Id = AutoRecordZoneFilterDisabled,
                        Label = I18n.Get("settings.filter.auto_record_zone_disabled_only"),
                    },
                ],
            },
            Sort = new MirageToolbarSortConfig
            {
                Id = "autoRecordZoneSort",
                AscIcon = FontAwesomeIcon.SortNumericUp,
                DescIcon = FontAwesomeIcon.SortNumericDown,
                AscTooltip = I18n.Get("settings.tooltip.auto_record_zone_sort_asc"),
                DescTooltip = I18n.Get("settings.tooltip.auto_record_zone_sort_desc"),
                Ascending = _autoRecordZoneSortAscending,
                SelectedTargetId = AutoRecordZoneSortZoneId,
                Targets =
                [
                    new MirageChecklistOption
                    {
                        Id = AutoRecordZoneSortZoneId,
                        Label = "Zone ID",
                    },
                ],
            },
        };

        toolbar.Actions =
        [
            new MirageToolbarAction
            {
                Icon = FontAwesomeIcon.MapPin,
                Id = "autoRecordZoneCurrent",
                Tooltip = I18n.Get("settings.tooltip.auto_record_zone_current"),
                OnClick = () =>
                {
                    var territory = PluginServices.ClientState.TerritoryType;
                    if (territory != 0)
                        toolbar.Search = territory.ToString();
                },
            },
            new MirageToolbarAction
            {
                Icon = FontAwesomeIcon.Undo,
                Id = "autoRecordZonesReset",
                Tooltip = I18n.Get("settings.tooltip.auto_record_zones_reset"),
                OnClick = C.ResetAutoRecordZoneEnabledToDefault,
            },
        ];

        MirageUi.IconToolbar(toolbar, "autoRecordZoneToolbar");
        _autoRecordZoneSearch = toolbar.Search;
        _autoRecordZoneSortAscending = toolbar.Sort.Ascending;
    }

    private void DrawAutoRecordZonePickerList(HashSet<uint> enabledSet)
    {
        var options = DutyContentCatalog.GetZoneOptions();
        var filter = _autoRecordZoneSearch;
        var filterEnabledOnly = _autoRecordZoneFilterIds.Contains(AutoRecordZoneFilterEnabled);
        var filterDisabledOnly = _autoRecordZoneFilterIds.Contains(AutoRecordZoneFilterDisabled);
        var rows = new List<ZoneOption>(options.Count);
        foreach (var option in options)
        {
            if (option.TerritoryTypeId == 0)
                continue;

            var enabled = enabledSet.Contains(option.TerritoryTypeId);
            if (filterEnabledOnly && !enabled)
                continue;
            if (filterDisabledOnly && enabled)
                continue;
            if (!MirageUi.MatchesFilter(
                    option.TerritoryTypeId.ToString(),
                    option.Label,
                    filter))
                continue;

            rows.Add(option);
        }

        rows.Sort((a, b) =>
        {
            var cmp = a.TerritoryTypeId.CompareTo(b.TerritoryTypeId);
            if (cmp != 0)
                return _autoRecordZoneSortAscending ? cmp : -cmp;

            cmp = a.ClassJobLevel.CompareTo(b.ClassJobLevel);
            if (cmp != 0)
                return _autoRecordZoneSortAscending ? cmp : -cmp;

            return string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase);
        });

        var selectedKeys = enabledSet.Select(id => id.ToString()).ToHashSet(StringComparer.Ordinal);
        var items = rows
            .Select(row => new MirageMultiSelectItem(
                $"arZone{row.TerritoryTypeId}_{row.ContentFinderConditionId}",
                row.Label,
                row.TerritoryTypeId.ToString()))
            .ToList();

        MirageUi.MultiSelectHighlightList(
            "##autoRecordZoneList",
            items,
            selectedKeys,
            emptyText: I18n.Get("settings.auto_record_zones.empty"),
            onSelectionToggled: key =>
            {
                if (!uint.TryParse(key, out var territoryId))
                    return;

                if (selectedKeys.Contains(key))
                {
                    enabledSet.Add(territoryId);
                    C.AddAutoRecordZoneEnabled(territoryId);
                }
                else
                {
                    enabledSet.Remove(territoryId);
                    C.RemoveAutoRecordZoneEnabled(territoryId);
                }
            });
    }

    // --- Import Actions ---

    private void DrawImportActionSettings()
    {
        MirageUi.Header(I18n.Get("settings.header.import_actions"));

        if (_importActionJobId == 0)
            _importActionJobId = ResolveDefaultImportJobId();

        JobCombo.Draw(I18n.Get("config.label.job"), ref _importActionJobId, id: "importActionJob");

        if (_importActionJobId == 0)
        {
            MirageUi.Text(I18n.Get("fflogs.actions.select_job"), MirageUi.Color.Secondary);
            return;
        }

        FFLogsImportActionsUi.DrawForJob(_importActionJobId);
    }

    private static uint ResolveDefaultImportJobId()
    {
        var playerJob = PluginServices.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;
        if (playerJob != 0 && JobActionCatalog.GetCombatJobs().Any(j => j.Id == playerJob))
            return playerJob;

        return JobActionCatalog.GetCombatJobs().FirstOrDefault().Id;
    }

    // --- Helpers ---

    private static void DrawHighlightPhaseSettings(
        string header,
        string idSuffix,
        Func<float> getSeconds,
        Action<float> setSeconds,
        Func<float> getThickness,
        Action<float> setThickness,
        Func<Vector4> getColor,
        Action<Vector4> setColor,
        Func<bool> getBlink,
        Action<bool> setBlink,
        float minSeconds,
        float maxSeconds)
    {
        MirageUi.SubHeader(header);

        var seconds = getSeconds();
        if (MirageUi.SliderFloat(
                I18n.Get("settings.slider.duration_sec"),
                ref seconds,
                minSeconds,
                maxSeconds,
                "%.1f",
                id: $"{idSuffix}_duration"))
        {
            setSeconds(Math.Clamp(seconds, minSeconds, maxSeconds));
            C.Save();
        }

        var thickness = getThickness();
        if (MirageUi.SliderFloat(
                I18n.Get("settings.slider.line_thickness"),
                ref thickness,
                1f,
                12f,
                "%.1f",
                id: $"{idSuffix}_thickness"))
        {
            setThickness(Math.Clamp(thickness, 1f, 12f));
            C.Save();
        }

        var lineColor = getColor();
        if (MirageUi.ColorEdit4(
                I18n.Get("settings.color.line"),
                ref lineColor,
                id: $"{idSuffix}_color"))
        {
            setColor(lineColor);
            C.Save();
        }

        var blink = getBlink();
        if (MirageUi.Checkbox($"{I18n.Get("settings.checkbox.blink")}##{idSuffix}", ref blink))
        {
            setBlink(blink);
            C.Save();
        }
    }

    private static void DrawHotbarIdCheckboxRow(byte startId, int count, string formatKey)
    {
        const int perRow = 5;
        for (var i = 0; i < count; i++)
        {
            if (i > 0 && i % perRow != 0)
                ImGui.SameLine();

            var hotbarId = (byte)(startId + i);
            var enabled = C.IsHotbarHighlightEnabled(hotbarId);
            var label = $"{I18n.Format(formatKey, i + 1)}##hotbarHighlight{hotbarId}";
            if (MirageUi.Checkbox(label, ref enabled))
                C.SetHotbarHighlightEnabled(hotbarId, enabled);
        }
    }
}
