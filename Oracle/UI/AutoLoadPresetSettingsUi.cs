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
        var tableWidth = Math.Max(1f, ImGui.GetContentRegionAvail().X);
        var live = BossPresence.LiveLabelsSorted();
        if (live.Count == 0)
            MirageUi.Text(I18n.Get("settings.autoload_presets.live_none"), MirageUi.Color.Secondary);
        else
            MirageUi.Text(
                I18n.Format("settings.autoload_presets.live", string.Join(", ", live)),
                MirageUi.Color.Secondary);

        if (ids.Count == 0)
            MirageUi.Text(I18n.Get("settings.autoload_presets.empty"), MirageUi.Color.Secondary);

        DrawDataIdRows(presetId, ids, tableWidth);
        DrawDataIdDraft(presetId, ids, tableWidth);
    }

    private static void DrawDataIdRows(string presetId, List<uint> ids, float tableWidth)
    {
        if (ids.Count == 0)
            return;

        if (!PresetZoneListUi.BeginRowTable("##autoLoadDataIds", tableWidth))
            return;

        for (var i = 0; i < ids.Count; i++)
        {
            ImGui.PushID(i);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            var edit = (int)ids[i];
            if (MirageUi.InputInt(
                    string.Empty,
                    ref edit,
                    step: 0,
                    stepFast: 0,
                    id: "dataId",
                    width: Math.Max(40f, ImGui.GetContentRegionAvail().X)))
            {
                ids[i] = (uint)Math.Max(0, edit);
                AutoLoadPresets.Save(presetId, ids);
            }

            ImGui.TableNextColumn();
            if (MirageUi.IconButton(
                    FontAwesomeIcon.Trash,
                    "##deleteDataId",
                    size: default,
                    tooltip: I18n.Get("config.cue.tooltip.delete_row")))
            {
                ids.RemoveAt(i);
                AutoLoadPresets.Save(presetId, ids);
                ImGui.PopID();
                ImGui.EndTable();
                return;
            }

            ImGui.PopID();
        }

        ImGui.EndTable();
    }

    private static void DrawDataIdDraft(string presetId, List<uint> ids, float tableWidth)
    {
        if (!PresetZoneListUi.BeginRowTable("##autoLoadDataIdDraft", tableWidth))
            return;

        ImGui.PushID("##autoLoadDataIdDraft");
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        var draft = (int)_draftDataId;
        if (MirageUi.InputInt(
                string.Empty,
                ref draft,
                step: 0,
                stepFast: 0,
                id: "draftDataId",
                width: Math.Max(40f, ImGui.GetContentRegionAvail().X)))
            _draftDataId = (uint)Math.Max(0, draft);

        ImGui.TableNextColumn();
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
        ImGui.EndTable();
    }

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
