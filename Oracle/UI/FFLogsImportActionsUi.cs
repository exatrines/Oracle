using Oracle.Services;

namespace Oracle.UI;

/// <summary>Per-job FFLogs import action allow-list editor (Settings / Import).</summary>
internal static class FFLogsImportActionsUi
{
    public static void DrawForJob(uint classJobId, string idPrefix = "fflogsImportActions")
    {
        if (classJobId == 0)
        {
            MirageUi.Text(I18n.Get("fflogs.actions.select_job"), MirageUi.Color.Secondary);
            return;
        }

        var catalog = JobActionCatalog.GetActionsForJob(
            classJobId,
            classJobLevel: 0,
            includeClassActions: true);
        var selected = C.GetFFLogsImportActionIds(classJobId);
        var snapshot = selected.ToHashSet();

        if (DrawToolbar(classJobId, idPrefix, catalog, selected))
            return;

        if (catalog.Count == 0)
        {
            MirageUi.Text(I18n.Get("fflogs.actions.none"), MirageUi.Color.Secondary);
            return;
        }

        DrawActionGroups(classJobId, idPrefix, catalog, selected);

        if (!selected.SetEquals(snapshot))
            C.SetFFLogsImportActionIds(classJobId, selected);
    }

    private static bool DrawToolbar(
        uint classJobId,
        string idPrefix,
        IReadOnlyList<JobActionInfo> catalog,
        HashSet<uint> selected)
    {
        using (ImRaii.Disabled(catalog.Count == 0))
        {
            if (MirageUi.PrimaryButton(I18n.Get("fflogs.actions.button.select_all"), id: $"{idPrefix}All_{classJobId}"))
            {
                selected.Clear();
                foreach (var a in catalog)
                    selected.Add(a.ActionId);
                C.SetFFLogsImportActionIds(classJobId, selected);
                return true;
            }

            ImGui.SameLine();
            if (MirageUi.PrimaryButton(I18n.Get("fflogs.actions.button.clear_all"), id: $"{idPrefix}None_{classJobId}"))
            {
                C.SetFFLogsImportActionIds(classJobId, []);
                return true;
            }

            ImGui.SameLine();
            if (MirageUi.PrimaryButton(I18n.Get("fflogs.actions.button.reset_defaults"), id: $"{idPrefix}Defaults_{classJobId}"))
            {
                C.ResetFFLogsImportActionIds(classJobId);
                return true;
            }
        }

        return false;
    }

    private static void DrawActionGroups(
        uint classJobId,
        string idPrefix,
        IReadOnlyList<JobActionInfo> catalog,
        HashSet<uint> selected)
    {
        foreach (var kind in new[]
                 {
                     ActionPickerKind.Action,
                     ActionPickerKind.Ability,
                     ActionPickerKind.Role,
                 })
        {
            var group = catalog.Where(a => a.Kind == kind).ToList();
            if (group.Count == 0)
                continue;

            MirageUi.SubHeader(JobActionCatalog.KindLabel(kind));
            var tiles = group
                .Select(a => (a.ActionId, a.IconId, JobActionCatalog.FormatActionLabel(a)))
                .ToList();
            MirageUi.IconLabelToggleGrid($"##{idPrefix}_{classJobId}_{kind}", tiles, selected, columns: 2);
        }
    }
}
