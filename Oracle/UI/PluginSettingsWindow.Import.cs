namespace Oracle.UI;

// --- Import actions / Content Sync Presets ---

internal sealed partial class PluginSettingsWindow
{
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

    // --- Content Sync Presets ---

    private void DrawSyncPresetSettings()
    {
        MirageUi.Header(I18n.Get("settings.header.import_sync_presets"));
        MirageUi.Text(I18n.Get("settings.help.import_sync_presets"), MirageUi.Color.Secondary);

        if (!_syncPresetZoneInitialized)
        {
            _syncPresetZoneInitialized = true;
            ZoneCombo.ApplyCurrent(
                ref _syncPresetTerritoryId,
                ref _syncPresetContentFinderConditionId,
                ref _syncPresetClassJobLevel,
                ref _syncPresetZoneLabel);
        }

        ZoneCombo.Draw(
            I18n.Get("config.label.zone"),
            ref _syncPresetTerritoryId,
            ref _syncPresetContentFinderConditionId,
            ref _syncPresetClassJobLevel,
            ref _syncPresetZoneLabel,
            ref _syncPresetZoneSearch,
            id: "syncPresetZone");

        SyncPresetSettingsUi.Draw(_syncPresetTerritoryId);
    }

    private static uint ResolveDefaultImportJobId()
    {
        var playerJob = PluginServices.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;
        if (playerJob != 0 && JobActionCatalog.GetCombatJobs().Any(j => j.Id == playerJob))
            return playerJob;

        return JobActionCatalog.GetCombatJobs().FirstOrDefault().Id;
    }
}
