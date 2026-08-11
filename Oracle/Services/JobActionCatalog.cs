using Lumina.Excel.Sheets;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace Oracle.Services;

internal readonly record struct JobInfo(uint Id, string Abbreviation, string Name);

internal enum ActionPickerKind
{
    Action,
    Ability,
    Role,
}

internal enum ActionOccupation
{
    Class,
    Job,
    Role,
}

internal readonly record struct JobActionInfo(
    uint ActionId,
    string Name,
    uint IconId,
    byte ClassJobLevel,
    ActionPickerKind Kind,
    ActionOccupation Occupation);

internal readonly record struct JobSelection(
    uint JobId,
    uint ParentId,
    bool IsAdvancedJob,
    string Abbreviation,
    HashSet<uint> LineageIds);

/// <summary>Combat job list and job-scoped action catalog for pickers/import.</summary>
internal static class JobActionCatalog
{
    // ActionCategory: 2=Spell, 3=Weaponskill, 4=Ability
    private const uint CategorySpell = 2;
    private const uint CategoryWeaponskill = 3;
    private const uint CategoryAbility = 4;

    public static IReadOnlyList<JobInfo> GetCombatJobs()
    {
        try
        {
            var sheet = PluginServices.DataManager.GetExcelSheet<ClassJob>();
            if (sheet == null)
                return FallbackJobs();

            return sheet
                .Where(j => j.RowId > 0)
                .Where(j => j.Role is >= 1 and <= 4)
                .Where(j => !string.IsNullOrWhiteSpace(j.Abbreviation.ToString()))
                .OrderBy(j => j.Role)
                .ThenBy(j => j.RowId)
                .Select(j => new JobInfo(
                    j.RowId,
                    j.Abbreviation.ToString(),
                    j.Name.ToString()))
                .ToList();
        }
        catch (Exception ex)
        {
            PluginServices.Log.Warning(ex, "Failed to load ClassJob sheet");
            return FallbackJobs();
        }
    }

    public static string FormatJobOption(JobInfo job) =>
        $"{job.Id} | {job.Abbreviation} - {job.Name}";

    public static string GetJobLabel(uint classJobId)
    {
        if (classJobId == 0)
            return I18n.Get("job.not_set");

        var job = GetCombatJobs().FirstOrDefault(j => j.Id == classJobId);
        return job.Id == 0 ? I18n.Format("job.unknown", classJobId) : FormatJobOption(job);
    }

    public static string KindLabel(ActionPickerKind kind) => kind switch
    {
        ActionPickerKind.Action => I18n.Get("kind.action"),
        ActionPickerKind.Ability => I18n.Get("kind.ability"),
        ActionPickerKind.Role => I18n.Get("kind.role"),
        _ => kind.ToString(),
    };

    public static string FormatActionLabel(JobActionInfo action) =>
        action.ClassJobLevel > 0
            ? I18n.Format("action.level_label", action.Name, action.ClassJobLevel)
            : action.Name;

    public static IReadOnlyList<JobActionInfo> GetActionsForJob(
        uint classJobId,
        byte classJobLevel = 0,
        bool includeClassActions = false)
    {
        if (classJobId == 0)
            return [];

        try
        {
            var actionSheet = PluginServices.DataManager.GetExcelSheet<LuminaAction>();
            var jobSheet = PluginServices.DataManager.GetExcelSheet<ClassJob>();
            var job = jobSheet?.GetRowOrDefault(classJobId);
            if (actionSheet == null || job == null)
                return FallbackActions(classJobLevel);

            var selection = CreateSelection(job.Value);
            var combatJobs = GetCombatJobs();
            var results = new Dictionary<uint, JobActionInfo>();

            foreach (var action in actionSheet)
            {
                if (!TryClassify(action, selection, combatJobs, requirePlayerAction: true, out var entry))
                    continue;
                results[entry.ActionId] = entry;
            }

            AddFromClassJobActionUi(results, actionSheet, selection, combatJobs);
            AddFromActionIndirection(results, actionSheet, selection, combatJobs);

            var upgradesTo = BuildEvolutionMap(actionSheet, selection.LineageIds);

            var levelFiltered = results.Values
                .Where(a => classJobLevel == 0 || a.ClassJobLevel == 0 || a.ClassJobLevel <= classJobLevel)
                .Where(a => IsVisibleForSelection(a.Occupation, selection, includeClassActions))
                .ToList();

            var superseded = CollectSupersededActionIds(
                levelFiltered,
                upgradesTo,
                actionSheet,
                classJobLevel);

            return levelFiltered
                .Where(a => !superseded.Contains(a.ActionId))
                .OrderBy(a => a.Kind)
                .ThenBy(a => a.ClassJobLevel)
                .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            PluginServices.Log.Warning(ex, "Failed to build job action catalog");
            return FallbackActions(classJobLevel);
        }
    }

    private static JobSelection CreateSelection(ClassJob job)
    {
        var jobId = job.RowId;
        var parentId = job.ClassJobParent.RowId;
        var isAdvanced = parentId != 0 && parentId != jobId;
        var abbr = job.Abbreviation.ToString();
        var lineage = new HashSet<uint> { jobId };
        if (isAdvanced)
            lineage.Add(parentId);

        return new JobSelection(jobId, isAdvanced ? parentId : 0, isAdvanced, abbr, lineage);
    }

    private static bool IsVisibleForSelection(
        ActionOccupation occupation,
        JobSelection selection,
        bool includeClassActions)
    {
        if (occupation == ActionOccupation.Role)
            return true;

        if (!selection.IsAdvancedJob)
            return occupation == ActionOccupation.Class;

        return occupation switch
        {
            ActionOccupation.Job => true,
            ActionOccupation.Class => includeClassActions,
            _ => false,
        };
    }

    private static bool TryClassify(
        LuminaAction action,
        JobSelection selection,
        IReadOnlyList<JobInfo> combatJobs,
        bool requirePlayerAction,
        out JobActionInfo entry)
    {
        entry = default;

        if (action.RowId == 0 || action.Icon == 0 || action.IsPvP)
            return false;

        if (requirePlayerAction && !action.IsPlayerAction)
            return false;

        var name = action.Name.ToString();
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var actionCategory = action.ActionCategory.RowId;
        if (actionCategory is not (CategorySpell or CategoryWeaponskill or CategoryAbility))
            return false;

        if (action.IsRoleAction)
        {
            // Role: selected job must be flagged. Never use parent  E            // parent class often shares a different role (e.g. ACN caster vs SCH healer).
            if (!CategoryFlagsAbbreviation(action, selection.Abbreviation))
                return false;

            entry = new JobActionInfo(
                action.RowId,
                name,
                action.Icon,
                action.ClassJobLevel,
                ActionPickerKind.Role,
                ActionOccupation.Role);
            return true;
        }

        if (!TryResolveClassOrJob(action, selection, combatJobs, out var occupation))
            return false;

        entry = new JobActionInfo(
            action.RowId,
            name,
            action.Icon,
            action.ClassJobLevel,
            ResolveKind(actionCategory),
            occupation);
        return true;
    }

    private static bool TryResolveClassOrJob(
        LuminaAction action,
        JobSelection selection,
        IReadOnlyList<JobInfo> combatJobs,
        out ActionOccupation occupation)
    {
        occupation = default;
        var owner = action.ClassJob.RowId;

        if (owner != 0)
        {
            if (!selection.LineageIds.Contains(owner))
                return false;

            occupation = ResolveOccupationFromOwner(owner, selection);
            if (occupation == ActionOccupation.Class
                && selection.IsAdvancedJob
                && !CategoryFlagsAbbreviation(action, selection.Abbreviation))
                return false;

            return true;
        }

        // ClassJob == 0: category-driven. Must not reach outside the lineage
        // (stops sibling jobs that share a parent from leaking each other's skills).
        if (!TryGetFlaggedCombatJobs(action, combatJobs, out var flagged) || flagged.Count == 0)
            return false;

        if (flagged.Any(id => !selection.LineageIds.Contains(id)))
            return false;

        if (flagged.Contains(selection.JobId))
        {
            occupation = selection.IsAdvancedJob ? ActionOccupation.Job : ActionOccupation.Class;
            return true;
        }

        // Parent-only category on an advanced job is not inherited unless the job is flagged
        // (handled above). Reject bare parent-only rows.
        return false;
    }

    private static ActionOccupation ResolveOccupationFromOwner(uint ownerJobId, JobSelection selection)
    {
        if (!selection.IsAdvancedJob)
            return ActionOccupation.Class;

        if (ownerJobId == selection.ParentId)
            return ActionOccupation.Class;

        return ActionOccupation.Job;
    }

    private static ActionPickerKind ResolveKind(uint actionCategory) =>
        actionCategory == CategoryAbility ? ActionPickerKind.Ability : ActionPickerKind.Action;

    private static void AddFromClassJobActionUi(
        Dictionary<uint, JobActionInfo> results,
        Lumina.Excel.ExcelSheet<LuminaAction> actionSheet,
        JobSelection selection,
        IReadOnlyList<JobInfo> combatJobs)
    {
        var uiSheet = PluginServices.DataManager.GetSubrowExcelSheet<ClassJobActionUI>();
        if (uiSheet == null)
            return;

        foreach (var jobId in selection.LineageIds)
        {
            if (!uiSheet.TryGetRow(jobId, out var subrows))
                continue;

            foreach (var row in subrows)
            {
                TryAddDerived(results, actionSheet, selection, combatJobs, row.BaseAction.RowId);
                TryAddDerived(results, actionSheet, selection, combatJobs, row.UpgradeAction.RowId);
            }
        }
    }

    private static void AddFromActionIndirection(
        Dictionary<uint, JobActionInfo> results,
        Lumina.Excel.ExcelSheet<LuminaAction> actionSheet,
        JobSelection selection,
        IReadOnlyList<JobInfo> combatJobs)
    {
        var sheet = PluginServices.DataManager.GetExcelSheet<ActionIndirection>();
        if (sheet == null)
            return;

        foreach (var row in sheet)
        {
            if (!selection.LineageIds.Contains(row.ClassJob.RowId))
                continue;

            TryAddDerived(results, actionSheet, selection, combatJobs, row.Name.RowId);
        }
    }

    private static void TryAddDerived(
        Dictionary<uint, JobActionInfo> results,
        Lumina.Excel.ExcelSheet<LuminaAction> actionSheet,
        JobSelection selection,
        IReadOnlyList<JobInfo> combatJobs,
        uint actionId)
    {
        if (actionId == 0 || results.ContainsKey(actionId))
            return;

        var action = actionSheet.GetRowOrDefault(actionId);
        if (action == null)
            return;

        // UI / indirection rows often have IsPlayerAction=false.
        if (!TryClassify(action.Value, selection, combatJobs, requirePlayerAction: false, out var entry))
            return;

        results[actionId] = entry;
    }

    private static Dictionary<uint, uint> BuildEvolutionMap(
        Lumina.Excel.ExcelSheet<LuminaAction> actionSheet,
        HashSet<uint> lineageIds)
    {
        var map = new Dictionary<uint, uint>();
        var uiSheet = PluginServices.DataManager.GetSubrowExcelSheet<ClassJobActionUI>();
        if (uiSheet == null)
            return map;

        var families = new Dictionary<uint, HashSet<uint>>();

        foreach (var jobId in lineageIds)
        {
            if (!uiSheet.TryGetRow(jobId, out var subrows))
                continue;

            foreach (var row in subrows)
            {
                var upgrade = row.UpgradeAction.RowId;
                var baseAction = row.BaseAction.RowId;
                if (baseAction == 0 || upgrade == 0)
                    continue;

                if (!families.TryGetValue(baseAction, out var members))
                {
                    members = [baseAction];
                    families[baseAction] = members;
                }

                members.Add(upgrade);
            }
        }

        foreach (var members in families.Values)
        {
            var byLevel = members
                .Select(id => (Id: id, Row: actionSheet.GetRowOrDefault(id)))
                .Where(x => x.Row != null)
                .GroupBy(x => x.Row!.Value.ClassJobLevel)
                .OrderBy(g => g.Key)
                .Select(g => g.OrderBy(x => x.Id).First().Id)
                .ToList();

            for (var i = 0; i < byLevel.Count - 1; i++)
                map[byLevel[i]] = byLevel[i + 1];
        }

        return map;
    }

    private static HashSet<uint> CollectSupersededActionIds(
        IReadOnlyList<JobActionInfo> levelFiltered,
        Dictionary<uint, uint> upgradesTo,
        Lumina.Excel.ExcelSheet<LuminaAction> actionSheet,
        byte classJobLevel)
    {
        var hide = new HashSet<uint>();
        if (classJobLevel == 0 || levelFiltered.Count == 0 || upgradesTo.Count == 0)
            return hide;

        var present = levelFiltered.Select(a => a.ActionId).ToHashSet();

        foreach (var action in levelFiltered)
        {
            var current = action.ActionId;
            while (upgradesTo.TryGetValue(current, out var nextId))
            {
                var next = actionSheet.GetRowOrDefault(nextId);
                if (next == null)
                    break;

                if (next.Value.ClassJobLevel <= classJobLevel && present.Contains(nextId))
                {
                    hide.Add(action.ActionId);
                    break;
                }

                if (next.Value.ClassJobLevel > classJobLevel)
                    break;

                current = nextId;
            }
        }

        return hide;
    }

    private static bool CategoryFlagsAbbreviation(LuminaAction action, string abbreviation)
    {
        if (string.IsNullOrWhiteSpace(abbreviation))
            return false;

        try
        {
            var cat = action.ClassJobCategory.ValueNullable;
            return cat != null && CategoryHasAbbreviation(cat.Value, abbreviation);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetFlaggedCombatJobs(
        LuminaAction action,
        IReadOnlyList<JobInfo> combatJobs,
        out HashSet<uint> flagged)
    {
        flagged = [];
        try
        {
            var cat = action.ClassJobCategory.ValueNullable;
            if (cat == null)
                return false;

            foreach (var j in combatJobs)
            {
                if (CategoryHasAbbreviation(cat.Value, j.Abbreviation))
                    flagged.Add(j.Id);
            }

            return true;
        }
        catch
        {
            flagged = [];
            return false;
        }
    }

    private static bool CategoryHasAbbreviation(ClassJobCategory cat, string abbreviation)
    {
        var prop = cat.GetType().GetProperty(abbreviation);
        if (prop?.PropertyType != typeof(bool))
            return false;

        return (bool)(prop.GetValue(cat) ?? false);
    }

    private static IReadOnlyList<JobInfo> FallbackJobs() =>
    [
        new(19, "PLD", "Paladin"),
        new(21, "WAR", "Warrior"),
        new(32, "DRK", "Dark Knight"),
        new(37, "GNB", "Gunbreaker"),
    ];

    private static IReadOnlyList<JobActionInfo> FallbackActions(byte classJobLevel) =>
        new JobActionInfo[]
        {
            new(7531, "Rampart", 0, 8, ActionPickerKind.Role, ActionOccupation.Role),
            new(7535, "Reprisal", 0, 22, ActionPickerKind.Role, ActionOccupation.Role),
            new(7548, "Arm's Length", 0, 32, ActionPickerKind.Role, ActionOccupation.Role),
        }
        .Where(a => classJobLevel == 0 || a.ClassJobLevel <= classJobLevel)
        .ToList();
}
