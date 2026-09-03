using MirageUI.Ui;

namespace Oracle.UI;

// --- Auto Record ---

internal sealed partial class PluginSettingsWindow
{
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
}
