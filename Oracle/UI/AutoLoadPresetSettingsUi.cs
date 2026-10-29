using Oracle.Services;

namespace Oracle.UI;

/// <summary>Named Auto Load boss DataID editor (Settings / Import).</summary>
internal static class AutoLoadPresetSettingsUi
{
    private static readonly PresetPickerState Picker = new();
    private static string _draftPresetId = string.Empty;
    private static uint _draftDataId;

    public static void Draw()
    {
        var listed = AutoLoadPresets.Listed();
        var current = PresetZoneListUi.DrawHeader(
            listed,
            Picker,
            "autoLoadPreset",
            I18n.Get("settings.autoload_presets.add"),
            I18n.Get("settings.autoload_presets.delete"),
            I18n.Get("settings.autoload_presets.button.reset_defaults"),
            AutoLoadPresets.AddCustom,
            AutoLoadPresets.SetName,
            AutoLoadPresets.SetTerritory,
            AutoLoadPresets.Remove,
            AutoLoadPresets.Reset,
            AutoLoadPresets.HasOverride);
        if (current == null)
            return;

        DrawDataIds(current.Value.Id);
    }

    private static void DrawDataIds(string presetId)
    {
        EnsureDraft(presetId);
        var entry = AutoLoadPresets.Resolve(presetId);
        var ids = entry?.DataIds ?? [];
        var live = BossPresence.LiveLabelsSorted();
        if (live.Count == 0)
            MirageUi.Text(I18n.Get("settings.autoload_presets.live_none"), MirageUi.Color.Secondary);
        else
            MirageUi.Text(
                I18n.Format("settings.autoload_presets.live", string.Join(", ", live)),
                MirageUi.Color.Secondary);

        if (ids.Count == 0)
            MirageUi.Text(I18n.Get("settings.autoload_presets.empty"), MirageUi.Color.Secondary);

        MirageUi.Table.Draw(
            "##autoLoadDataIds",
            DataIdColumns(),
            ids.Count,
            index => DrawDataIdRow(presetId, ids, index),
            () => DrawDataIdDraft(presetId, ids));
    }

    private static bool DrawDataIdRow(string presetId, List<uint> ids, int index)
    {
        ImGui.PushID(index);
        MirageUi.Table.NextColumn();
        var edit = (int)ids[index];
        if (MirageUi.InputInt(
                string.Empty,
                ref edit,
                step: 0,
                stepFast: 0,
                id: "dataId",
                width: MirageUi.InputWidthFill))
        {
            ids[index] = (uint)Math.Max(0, edit);
            AutoLoadPresets.Save(presetId, ids);
        }

        MirageUi.Table.NextColumn();
        var deleted = MirageUi.IconButton(
            FontAwesomeIcon.Trash,
            "##deleteDataId",
            size: default,
            tooltip: I18n.Get("config.cue.tooltip.delete_row"));
        if (deleted)
        {
            ids.RemoveAt(index);
            AutoLoadPresets.Save(presetId, ids);
        }

        ImGui.PopID();
        return deleted;
    }

    private static void DrawDataIdDraft(string presetId, List<uint> ids)
    {
        ImGui.PushID("##autoLoadDataIdDraft");
        MirageUi.Table.NextColumn();
        var draft = (int)_draftDataId;
        if (MirageUi.InputInt(
                string.Empty,
                ref draft,
                step: 0,
                stepFast: 0,
                id: "draftDataId",
                width: MirageUi.InputWidthFill))
            _draftDataId = (uint)Math.Max(0, draft);

        MirageUi.Table.NextColumn();
        var canSubmit = _draftDataId != 0;
        if (MirageUi.IconButton(
                FontAwesomeIcon.ArrowUpFromBracket,
                "##submitDataId",
                size: default,
                tooltip: I18n.Get("config.cue.tooltip.submit"),
                enabled: canSubmit)
            && canSubmit)
        {
            ids.Add(_draftDataId);
            AutoLoadPresets.Save(presetId, ids);
            _draftDataId = 0;
        }

        ImGui.PopID();
    }

    private static MirageTableColumn[] DataIdColumns() =>
    [
        new() { Label = I18n.Get("settings.autoload_presets.col.data_id"), Width = 160f, Stretch = true },
        new() { Width = MirageUi.ResolveControlHeight() + 6f },
    ];

    private static void EnsureDraft(string presetId)
    {
        if (string.Equals(_draftPresetId, presetId, StringComparison.OrdinalIgnoreCase))
            return;

        ResetDraft(presetId);
    }

    private static void ResetDraft(string presetId)
    {
        _draftPresetId = presetId ?? string.Empty;
        _draftDataId = 0;
    }
}
