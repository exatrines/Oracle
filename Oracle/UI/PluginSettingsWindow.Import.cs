namespace Oracle.UI;

// --- Import actions / Timer Sync Presets / Phase Presets ---

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

    // --- Timer Sync Presets ---

    private void DrawSyncPresetSettings()
    {
        MirageUi.Header(I18n.Get("settings.header.import_sync_presets"));
        SyncPresetSettingsUi.Draw();
    }

    private void DrawAutoLoadPresetSettings()
    {
        MirageUi.Header(I18n.Get("settings.header.autoload_presets"));
        AutoLoadPresetSettingsUi.Draw();
    }

    private static uint ResolveDefaultImportJobId()
    {
        var playerJob = PluginServices.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;
        if (playerJob != 0 && JobActionCatalog.GetCombatJobs().Any(j => j.Id == playerJob))
            return playerJob;

        return JobActionCatalog.GetCombatJobs().FirstOrDefault().Id;
    }
}
