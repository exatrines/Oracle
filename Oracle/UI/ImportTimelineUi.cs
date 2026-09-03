using Oracle.Services;

namespace Oracle.UI;

/// <summary>Shared FFLogs / AutoRecord import fields (name, options, Auto Load).</summary>
internal static class ImportTimelineUi
{
    public static void DrawName(ref string title, string id)
    {
        MirageUi.SubHeader(I18n.Get("fflogs.subheader.timeline"));
        MirageUi.InputText(I18n.Get("fflogs.label.name"), ref title, 80, id: id);
    }

    public static void DrawImportOptions(
        ref bool importMemos,
        ref bool importSync,
        ref uint syncOptionTerritoryId,
        uint territoryTypeId)
    {
        MirageUi.Checkbox(I18n.Get("fflogs.checkbox.enemy_hit_memos"), ref importMemos);
        SyncCueFields.DrawImportOption(ref importSync, ref syncOptionTerritoryId, territoryTypeId);
    }

    public static void DrawAutoLoad(
        string idPrefix,
        ref bool autoLoadEnabled,
        ref uint territoryTypeId,
        ref uint contentFinderConditionId,
        ref byte zoneClassJobLevel,
        ref string zoneLabel,
        ref string zoneSearchFilter,
        ref uint classJobId,
        ref int sceneId,
        ref bool sceneFilterEnabled)
    {
        MirageUi.SubHeader(I18n.Get("config.subheader.auto_load"));
        MirageUi.Checkbox(I18n.Get("config.checkbox.enable_auto_load"), ref autoLoadEnabled);

        DrawZoneField(
            editable: false,
            id: idPrefix + "ZoneReadonly",
            ref territoryTypeId,
            ref contentFinderConditionId,
            ref zoneClassJobLevel,
            ref zoneLabel,
            ref zoneSearchFilter);

        var jobId = classJobId;
        if (JobCombo.Draw(I18n.Get("config.label.job"), ref jobId, id: idPrefix + "Job"))
            classJobId = jobId;

        var scene = sceneId;
        var filter = sceneFilterEnabled;
        if (SceneFilterField.DrawLabeled(
                I18n.Get("config.label.scene_id"),
                idPrefix + "Scene",
                ref filter,
                ref scene))
        {
            sceneFilterEnabled = filter;
            sceneId = Math.Max(0, scene);
        }
    }

    public static void DrawZoneField(
        bool editable,
        string id,
        ref uint territoryTypeId,
        ref uint contentFinderConditionId,
        ref byte zoneClassJobLevel,
        ref string zoneLabel,
        ref string zoneSearchFilter)
    {
        if (!editable)
        {
            ZoneCombo.DrawReadonly(
                I18n.Get("config.label.zone"),
                territoryTypeId,
                contentFinderConditionId,
                zoneClassJobLevel,
                id: id);
            return;
        }

        ZoneCombo.Draw(
            I18n.Get("config.label.zone_group"),
            ref territoryTypeId,
            ref contentFinderConditionId,
            ref zoneClassJobLevel,
            ref zoneLabel,
            ref zoneSearchFilter,
            id: id);
    }

    public static bool CanCreate(
        uint classJobId,
        uint territoryTypeId,
        bool importMemos,
        bool importSync) =>
        C.GetFFLogsImportActionIds(classJobId).Count > 0
        || importMemos
        || (importSync && EnemySyncPresets.SpecsFor(territoryTypeId).Count > 0);

    public static string WithSyncCount(string status, int syncCount)
    {
        if (syncCount > 0)
            status += I18n.Format("fflogs.status.created_sync_suffix", syncCount);
        return status;
    }
}
