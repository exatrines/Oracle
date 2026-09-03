using Oracle.Models;
using Oracle.Services;

namespace Oracle.UI;

/// <summary>Per-zone Cast/Status sync preset editor (Settings / Import).</summary>
internal static class SyncPresetSettingsUi
{
    private static uint _draftTerritory;
    private static EnemySyncType _draftSyncType;
    private static uint _draftActionId;
    private static bool _draftEffected;

    public static void Draw(uint territoryTypeId)
    {
        if (territoryTypeId == 0)
        {
            MirageUi.Text(I18n.Get("settings.sync_presets.select_zone"), MirageUi.Color.Secondary);
            return;
        }

        EnsureDraft(territoryTypeId);

        if (EnemySyncPresets.HasBuiltIn(territoryTypeId))
        {
            using (ImRaii.Disabled(!C.HasSyncPresetOverride(territoryTypeId)))
            {
                if (MirageUi.PrimaryButton(
                        I18n.Get("settings.sync_presets.button.reset_defaults"),
                        id: "syncPresetReset"))
                {
                    C.ResetSyncPresets(territoryTypeId);
                    ResetDraft(territoryTypeId);
                    return;
                }
            }
        }

        var rows = EnemySyncPresets.EditableSpecsFor(territoryTypeId);
        var tableWidth = Math.Max(1f, ImGui.GetContentRegionAvail().X);
        DrawRows(territoryTypeId, rows, tableWidth);
        DrawDraftRow(territoryTypeId, rows, tableWidth);
    }

    private static void DrawRows(uint territoryTypeId, List<EnemySyncSpec> rows, float tableWidth)
    {
        if (rows.Count == 0)
        {
            MirageUi.Text(I18n.Get("settings.sync_presets.empty"), MirageUi.Color.Secondary);
            return;
        }

        if (!BeginPresetTable("##syncPresetRows", tableWidth))
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
                EnemySyncPresets.Save(territoryTypeId, rows);
            }

            ImGui.TableNextColumn();
            if (MirageUi.IconButton(
                    FontAwesomeIcon.Trash,
                    "##deleteSyncPreset",
                    size: default,
                    tooltip: I18n.Get("config.cue.tooltip.delete_row")))
            {
                rows.RemoveAt(i);
                EnemySyncPresets.Save(territoryTypeId, rows);
                ImGui.PopID();
                ImGui.EndTable();
                return;
            }

            ImGui.PopID();
        }

        ImGui.EndTable();
    }

    private static void DrawDraftRow(uint territoryTypeId, List<EnemySyncSpec> rows, float tableWidth)
    {
        if (!BeginPresetTable("##syncPresetDraft", tableWidth))
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
            EnemySyncPresets.Save(territoryTypeId, rows);
            ResetDraft(territoryTypeId);
        }

        ImGui.PopID();
        ImGui.EndTable();
    }

    private static bool BeginPresetTable(string id, float tableWidth)
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

    private static void EnsureDraft(uint territoryTypeId)
    {
        if (_draftTerritory == territoryTypeId)
            return;

        ResetDraft(territoryTypeId);
    }

    private static void ResetDraft(uint territoryTypeId)
    {
        _draftTerritory = territoryTypeId;
        _draftSyncType = EnemySyncType.Cast;
        _draftActionId = 0;
        _draftEffected = false;
    }
}
