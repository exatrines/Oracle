using Oracle.Models;
using Oracle.Services;

namespace Oracle.UI;

/// <summary>Named Cast/Status sync preset editor (Settings / Import).</summary>
internal static class SyncPresetSettingsUi
{
    private static readonly PresetPickerState Picker = new();
    private static string _draftPresetId = string.Empty;
    private static EnemySyncType _draftSyncType;
    private static uint _draftActionId;
    private static bool _draftEffected;

    public static void Draw()
    {
        var listed = EnemySyncPresets.Listed();
        var current = PresetZoneListUi.DrawHeader(
            listed,
            Picker,
            "syncPreset",
            I18n.Get("settings.sync_presets.add"),
            I18n.Get("settings.sync_presets.remove"),
            I18n.Get("settings.sync_presets.button.reset_defaults"),
            EnemySyncPresets.AddCustom,
            EnemySyncPresets.SetName,
            EnemySyncPresets.SetTerritory,
            EnemySyncPresets.Remove,
            id =>
            {
                EnemySyncPresets.Reset(id);
                ResetDraft(id);
            },
            EnemySyncPresets.HasOverride);
        if (current == null)
            return;

        DrawRows(current.Value.Id);
    }

    private static void DrawRows(string presetId)
    {
        EnsureDraft(presetId);
        var rows = EnemySyncPresets.EditableSpecsFor(presetId);
        var tableWidth = Math.Max(1f, ImGui.GetContentRegionAvail().X);
        DrawExisting(presetId, rows, tableWidth);
        DrawDraftRow(presetId, rows, tableWidth);
    }

    private static void DrawExisting(string presetId, List<EnemySyncSpec> rows, float tableWidth)
    {
        if (rows.Count == 0)
        {
            MirageUi.Text(I18n.Get("settings.sync_presets.empty"), MirageUi.Color.Secondary);
            return;
        }

        if (!PresetZoneListUi.BeginRowTable("##syncPresetRows", tableWidth))
            return;

        for (var i = 0; i < rows.Count; i++)
        {
            ImGui.PushID(i);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            var spec = rows[i];
            var syncType = spec.SyncType;
            var actionId = spec.ActionId;
            var effected = spec.Effected;
            if (SyncCueFields.DrawRow(ref syncType, ref actionId, ref effected, "row"))
            {
                rows[i] = new EnemySyncSpec(syncType, actionId, effected);
                EnemySyncPresets.Save(presetId, rows);
            }

            ImGui.TableNextColumn();
            if (MirageUi.IconButton(
                    FontAwesomeIcon.Trash,
                    "##deleteSyncPreset",
                    size: default,
                    tooltip: I18n.Get("config.cue.tooltip.delete_row")))
            {
                rows.RemoveAt(i);
                EnemySyncPresets.Save(presetId, rows);
                ImGui.PopID();
                ImGui.EndTable();
                return;
            }

            ImGui.PopID();
        }

        ImGui.EndTable();
    }

    private static void DrawDraftRow(string presetId, List<EnemySyncSpec> rows, float tableWidth)
    {
        if (!PresetZoneListUi.BeginRowTable("##syncPresetDraft", tableWidth))
            return;

        ImGui.PushID("##syncPresetDraft");
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        SyncCueFields.DrawRow(
            ref _draftSyncType,
            ref _draftActionId,
            ref _draftEffected,
            "draft");

        ImGui.TableNextColumn();
        var canSubmit = _draftActionId != 0;
        if (MirageUi.IconButton(
                FontAwesomeIcon.ArrowUpFromBracket,
                "##submitSyncPreset",
                size: default,
                tooltip: I18n.Get("config.cue.tooltip.submit"),
                enabled: canSubmit)
            && canSubmit)
        {
            rows.Add(new EnemySyncSpec(_draftSyncType, _draftActionId, _draftEffected));
            EnemySyncPresets.Save(presetId, rows);
            ResetDraft(presetId);
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
        _draftSyncType = EnemySyncType.Cast;
        _draftActionId = 0;
        _draftEffected = false;
    }
}
