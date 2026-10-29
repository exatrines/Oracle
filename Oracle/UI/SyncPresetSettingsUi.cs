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
        if (rows.Count == 0)
            MirageUi.Text(I18n.Get("settings.sync_presets.empty"), MirageUi.Color.Secondary);

        MirageUi.Table.Draw(
            "##syncPresetRows",
            SyncColumns(),
            rows.Count,
            index => DrawSyncRow(presetId, rows, index),
            () => DrawSyncDraft(presetId, rows));
    }

    private static bool DrawSyncRow(string presetId, List<SyncPresetEntry> rows, int index)
    {
        ImGui.PushID(index);
        var spec = rows[index];
        var syncType = spec.SyncType;
        var actionId = spec.ActionId;
        var effected = spec.Effected;
        var dirty = false;

        MirageUi.Table.NextColumn();
        if (SyncCueFields.DrawTypeField(ref syncType, "rowType"))
        {
            actionId = 0;
            effected = false;
            dirty = true;
        }

        MirageUi.Table.NextColumn();
        if (SyncCueFields.DrawNameField(ref actionId, syncType, "rowName"))
            dirty = true;

        MirageUi.Table.NextColumn();
        if (SyncCueFields.DrawEdgeField(ref effected, syncType, "rowEdge"))
            dirty = true;

        if (dirty)
        {
            rows[index] = new SyncPresetEntry
            {
                SyncType = syncType,
                ActionId = actionId,
                Effected = effected,
            };
            EnemySyncPresets.Save(presetId, rows);
        }

        MirageUi.Table.NextColumn();
        var deleted = MirageUi.IconButton(
            FontAwesomeIcon.Trash,
            "##deleteSyncPreset",
            size: default,
            tooltip: I18n.Get("config.cue.tooltip.delete_row"));
        if (deleted)
        {
            rows.RemoveAt(index);
            EnemySyncPresets.Save(presetId, rows);
        }

        ImGui.PopID();
        return deleted;
    }

    private static void DrawSyncDraft(string presetId, List<SyncPresetEntry> rows)
    {
        ImGui.PushID("##syncPresetDraft");
        MirageUi.Table.NextColumn();
        if (SyncCueFields.DrawTypeField(ref _draftSyncType, "draftType"))
        {
            _draftActionId = 0;
            _draftEffected = false;
        }

        MirageUi.Table.NextColumn();
        SyncCueFields.DrawNameField(ref _draftActionId, _draftSyncType, "draftName");

        MirageUi.Table.NextColumn();
        SyncCueFields.DrawEdgeField(ref _draftEffected, _draftSyncType, "draftEdge");

        MirageUi.Table.NextColumn();
        var canSubmit = _draftActionId != 0;
        if (MirageUi.IconButton(
                FontAwesomeIcon.ArrowUpFromBracket,
                "##submitSyncPreset",
                size: default,
                tooltip: I18n.Get("config.cue.tooltip.submit"),
                enabled: canSubmit)
            && canSubmit)
        {
            rows.Add(new SyncPresetEntry
            {
                SyncType = _draftSyncType,
                ActionId = _draftActionId,
                Effected = _draftEffected,
            });
            EnemySyncPresets.Save(presetId, rows);
            ResetDraft(presetId);
        }

        ImGui.PopID();
    }

    private static MirageTableColumn[] SyncColumns()
    {
        var trash = MirageUi.ResolveControlHeight() + 6f;
        return
        [
            new() { Label = I18n.Get("settings.sync_presets.col.type"), Width = 96f },
            new() { Label = I18n.Get("settings.sync_presets.col.name"), Width = 220f, Stretch = true },
            new() { Label = I18n.Get("settings.sync_presets.col.edge"), Width = 120f },
            new() { Width = trash },
        ];
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
