namespace Oracle.UI;

internal sealed class PresetPickerState
{
    public string SelectedId = string.Empty;
    public uint TerritoryTypeId;
    public uint ContentFinderConditionId;
    public byte ClassJobLevel;
    public string ZoneLabel = string.Empty;
    public string ZoneSearch = string.Empty;

    private string _boundId = string.Empty;

    public void Bind(NamedZonePreset preset)
    {
        if (_boundId == preset.Id && TerritoryTypeId == preset.TerritoryTypeId)
            return;

        _boundId = preset.Id;
        TerritoryTypeId = preset.TerritoryTypeId;
        ContentFinderConditionId = 0;
        ClassJobLevel = 0;
        ZoneLabel = string.Empty;
        ZoneSearch = string.Empty;
    }
}

/// <summary>Named preset picker: select / add, then name + zone + reset.</summary>
internal static class PresetZoneListUi
{
    public static NamedZonePreset? DrawHeader(
        IReadOnlyList<NamedZonePreset> listed,
        PresetPickerState state,
        string idPrefix,
        string addTooltip,
        string removeTooltip,
        string resetLabel,
        Func<string> onAdd,
        Action<string, string> onRename,
        Action<string, uint> onTerritoryChanged,
        Action<string> onRemove,
        Action<string> onReset,
        Func<string, bool> hasOverride)
    {
        DrawNameRow(listed, ref state.SelectedId, idPrefix, addTooltip, onAdd);
        var current = Find(listed, state.SelectedId);
        if (current == null)
            return null;

        var preset = current.Value;
        state.Bind(preset);
        DrawDetailRow(
            preset,
            NamedZonePresets.UsedTerritoriesExcept(listed, preset.Id),
            removeTooltip,
            resetLabel,
            hasOverride(preset.Id),
            ref state.TerritoryTypeId,
            ref state.ContentFinderConditionId,
            ref state.ClassJobLevel,
            ref state.ZoneLabel,
            ref state.ZoneSearch,
            idPrefix,
            name => onRename(preset.Id, name),
            territory => onTerritoryChanged(preset.Id, territory),
            () =>
            {
                onRemove(preset.Id);
                state.SelectedId = string.Empty;
            },
            () => onReset(preset.Id));
        return preset;
    }

    public static bool BeginRowTable(string id, float tableWidth)
    {
        const ImGuiTableFlags flags =
            ImGuiTableFlags.Borders
            | ImGuiTableFlags.RowBg
            | ImGuiTableFlags.SizingStretchProp
            | ImGuiTableFlags.NoHostExtendX;

        if (!ImGui.BeginTable(id, 2, flags, new Vector2(tableWidth, 0f)))
            return false;

        ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed, MirageUi.ResolveControlHeight() + 6f);
        return true;
    }

    private static NamedZonePreset? Find(IReadOnlyList<NamedZonePreset> listed, string selectedId)
    {
        foreach (var preset in listed)
        {
            if (string.Equals(preset.Id, selectedId, StringComparison.OrdinalIgnoreCase))
                return preset;
        }

        return null;
    }

    private static string BaseLabel(NamedZonePreset preset)
    {
        var name = string.IsNullOrWhiteSpace(preset.Name)
            ? I18n.Get("settings.autoload_presets.unnamed")
            : preset.Name.Trim();
        return preset.IsBuiltIn
            ? I18n.Format("settings.presets.built_in", name)
            : name;
    }

    private static string DisplayName(NamedZonePreset preset, IReadOnlyList<NamedZonePreset> listed)
    {
        var label = BaseLabel(preset);
        var dup = 0;
        foreach (var other in listed)
        {
            if (string.Equals(BaseLabel(other), label, StringComparison.Ordinal))
                dup++;
        }

        if (dup <= 1)
            return label;

        var suffix = preset.Id.Length <= 6 ? preset.Id : preset.Id[..6];
        return $"{label} ({suffix})";
    }

    private static void EnsureSelected(IReadOnlyList<NamedZonePreset> listed, ref string selectedId)
    {
        if (listed.Count == 0)
        {
            selectedId = string.Empty;
            return;
        }

        if (Find(listed, selectedId) != null)
            return;

        var current = PluginServices.ClientState.TerritoryType;
        if (current != 0)
        {
            foreach (var preset in listed)
            {
                if (preset.TerritoryTypeId != current)
                    continue;
                selectedId = preset.Id;
                return;
            }
        }

        selectedId = listed[0].Id;
    }

    private static void DrawNameRow(
        IReadOnlyList<NamedZonePreset> listed,
        ref string selectedId,
        string dropdownId,
        string addTooltip,
        Func<string> onAdd)
    {
        EnsureSelected(listed, ref selectedId);
        var current = Find(listed, selectedId);
        var btn = MirageUi.ResolveControlHeight();
        var gap = ImGui.GetStyle().ItemInnerSpacing.X;
        var width = Math.Max(40f, ImGui.GetContentRegionAvail().X - btn - gap);

        var selected = current == null ? string.Empty : DisplayName(current.Value, listed);
        var items = listed.Select(p => DisplayName(p, listed)).ToList();
        if (MirageUi.Dropdown(
                string.Empty,
                ref selected,
                items,
                placeholder: I18n.Get("config.zone.not_set"),
                id: dropdownId,
                allowClear: false,
                width: width))
            SelectByDisplayName(listed, selected, ref selectedId);

        ImGui.SameLine(0f, gap);
        if (MirageUi.IconButton(
                FontAwesomeIcon.Plus,
                $"##{dropdownId}AddBtn",
                new Vector2(btn, btn),
                tooltip: addTooltip,
                border: true))
            selectedId = onAdd();
    }

    private static void DrawDetailRow(
        NamedZonePreset preset,
        IReadOnlySet<uint> usedTerritories,
        string removeTooltip,
        string resetLabel,
        bool resetEnabled,
        ref uint territoryTypeId,
        ref uint contentFinderConditionId,
        ref byte classJobLevel,
        ref string zoneLabel,
        ref string zoneSearch,
        string idPrefix,
        Action<string> onRename,
        Action<uint> onTerritoryChanged,
        Action onRemove,
        Action onReset)
    {
        MirageUi.PaddedSeparator();

        var gap = ImGui.GetStyle().ItemInnerSpacing.X;
        var btn = MirageUi.ResolveControlHeight();

        if (!preset.IsBuiltIn)
        {
            var nameWidth = Math.Max(40f, ImGui.GetContentRegionAvail().X - btn - gap);
            var name = preset.Name ?? string.Empty;
            if (MirageUi.InputText(string.Empty, ref name, 80, id: idPrefix + "Name", width: nameWidth))
                onRename(name.Trim());

            ImGui.SameLine(0f, gap);
            if (MirageUi.IconButton(
                    FontAwesomeIcon.Trash,
                    $"##{idPrefix}RemoveBtn",
                    new Vector2(btn, btn),
                    tooltip: removeTooltip,
                    border: true))
                onRemove();
        }

        var resetWidth = 0f;
        if (preset.IsBuiltIn)
        {
            var pad = MirageUi.ResolveInputFramePadding();
            resetWidth = ImGui.CalcTextSize(resetLabel).X + (pad.X + 6f) * 2f + gap;
        }

        var zoneWidth = Math.Max(40f, ImGui.GetContentRegionAvail().X - resetWidth);
        IReadOnlySet<uint>? exclude = preset.IsBuiltIn ? null : usedTerritories;
        using (MirageUi.DisabledIf(preset.IsBuiltIn))
        {
            var changed = ZoneCombo.Draw(
                string.Empty,
                ref territoryTypeId,
                ref contentFinderConditionId,
                ref classJobLevel,
                ref zoneLabel,
                ref zoneSearch,
                id: idPrefix + "Zone",
                allowClear: false,
                showSetCurrent: !preset.IsBuiltIn,
                width: zoneWidth,
                excludeTerritoryIds: exclude);
            if (!preset.IsBuiltIn && changed && territoryTypeId != preset.TerritoryTypeId)
                onTerritoryChanged(territoryTypeId);
        }

        if (!preset.IsBuiltIn)
            return;

        ImGui.SameLine(0f, gap);
        using (ImRaii.Disabled(!resetEnabled))
        {
            if (MirageUi.PrimaryButton(resetLabel, enabled: resetEnabled, id: idPrefix + "Reset"))
                onReset();
        }
    }

    private static void SelectByDisplayName(
        IReadOnlyList<NamedZonePreset> listed,
        string selected,
        ref string selectedId)
    {
        foreach (var preset in listed)
        {
            if (!string.Equals(DisplayName(preset, listed), selected, StringComparison.Ordinal))
                continue;
            selectedId = preset.Id;
            return;
        }
    }
}
