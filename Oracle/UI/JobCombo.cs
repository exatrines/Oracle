using Oracle.Services;

namespace Oracle.UI;

internal static class JobCombo
{
    /// <summary>
    /// Job dropdown: <c>{id} | {abbr} - {name}</c>. Returns true when selection changes.
    /// Job is required  Eempty selection shows <c>(not set)</c>.
    /// </summary>
    public static bool Draw(
        string label,
        ref uint classJobId,
        string id,
        bool? liveMatch = null,
        string? matchTooltip = null)
    {
        var jobs = JobActionCatalog.GetCombatJobs();
        var items = jobs.Select(JobActionCatalog.FormatJobOption).ToList();
        var currentId = classJobId;
        var selected = currentId == 0
            ? string.Empty
            : items.FirstOrDefault(i => i.StartsWith($"{currentId} |", StringComparison.Ordinal))
              ?? string.Empty;

        if (!MirageUi.Dropdown(
                label,
                ref selected,
                items,
                placeholder: I18n.Get("job.not_set"),
                id: id,
                allowClear: false,
                liveMatch: liveMatch,
                matchTooltip: matchTooltip))
            return false;

        if (string.IsNullOrWhiteSpace(selected))
            return false;

        var sep = selected.IndexOf('|');
        var idText = (sep > 0 ? selected[..sep] : selected).Trim();
        classJobId = uint.TryParse(idText, out var parsed) ? parsed : 0u;
        return classJobId != 0;
    }
}
