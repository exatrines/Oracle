using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Lumina.Excel.Sheets;
using Oracle.Models;

namespace Oracle.Services;

/// <summary>Job / role target icons and labels for cue rows and overlays.</summary>
internal static class CueTargetCatalog
{
    // Party-finder / HUD role icons (Icon sheet 062581–062585).
    private const uint RoleIconTank = 62581;
    private const uint RoleIconHealer = 62582;
    private const uint RoleIconMelee = 62583;
    private const uint RoleIconRanged = 62584;
    private const uint RoleIconCaster = 62585;

    public static readonly CueTargetRole[] Roles =
    [
        CueTargetRole.Tank,
        CueTargetRole.Healer,
        CueTargetRole.Melee,
        CueTargetRole.Ranged,
        CueTargetRole.Caster,
    ];

    public static void Clear(TimelineCue cue)
    {
        cue.TargetKind = CueTargetKind.None;
        cue.TargetJobId = 0;
        cue.TargetRole = CueTargetRole.None;
    }

    public static void Copy(TimelineCue source, TimelineCue dest)
    {
        if (source.Kind != TimelineCueKind.Action)
        {
            Clear(dest);
            return;
        }

        dest.TargetKind = source.TargetKind;
        dest.TargetJobId = source.TargetJobId;
        dest.TargetRole = source.TargetRole;
    }

    public static void SetJob(TimelineCue cue, uint classJobId)
    {
        if (classJobId == 0)
        {
            Clear(cue);
            return;
        }

        cue.TargetKind = CueTargetKind.Job;
        cue.TargetJobId = classJobId;
        cue.TargetRole = CueTargetRole.None;
    }

    public static uint GetIconId(TimelineCue cue) =>
        cue.TargetKind switch
        {
            CueTargetKind.Job => GetJobIconId(cue.TargetJobId),
            CueTargetKind.Role => GetRoleIconId(cue.TargetRole),
            _ => 0,
        };

    public static IDalamudTextureWrap? GetIconWrap(TimelineCue cue)
    {
        var iconId = GetIconId(cue);
        if (iconId == 0)
            return null;

        try
        {
            return PluginServices.TextureProvider
                .GetFromGameIcon(new GameIconLookup(iconId))
                .GetWrapOrDefault();
        }
        catch
        {
            return null;
        }
    }

    public static CueTargetRole GetRole(TimelineCue cue) =>
        cue.TargetKind switch
        {
            CueTargetKind.Job => RoleFromJob(cue.TargetJobId),
            CueTargetKind.Role => cue.TargetRole,
            _ => CueTargetRole.None,
        };

    public static Vector4 GetRoleColor(CueTargetRole role) => role switch
    {
        CueTargetRole.Tank => new(0.25f, 0.55f, 0.95f, 1f),
        CueTargetRole.Healer => new(0.28f, 0.78f, 0.45f, 1f),
        CueTargetRole.Melee => new(0.90f, 0.28f, 0.28f, 1f),
        CueTargetRole.Ranged => new(0.95f, 0.80f, 0.25f, 1f),
        CueTargetRole.Caster => new(0.78f, 0.38f, 0.95f, 1f),
        _ => Vector4.Zero,
    };

    public static string GetRoleLabel(CueTargetRole role) => role switch
    {
        CueTargetRole.Tank => I18n.Get("config.cue.target.tank"),
        CueTargetRole.Healer => I18n.Get("config.cue.target.healer"),
        CueTargetRole.Melee => I18n.Get("config.cue.target.melee"),
        CueTargetRole.Ranged => I18n.Get("config.cue.target.ranged"),
        CueTargetRole.Caster => I18n.Get("config.cue.target.caster"),
        _ => I18n.Get("config.cue.target.none"),
    };

    public static uint GetRoleIconId(CueTargetRole role) => role switch
    {
        CueTargetRole.Tank => RoleIconTank,
        CueTargetRole.Healer => RoleIconHealer,
        CueTargetRole.Melee => RoleIconMelee,
        CueTargetRole.Ranged => RoleIconRanged,
        CueTargetRole.Caster => RoleIconCaster,
        _ => 0,
    };

    /// <summary>Job icons with a plate background (062100). 062000 is the transparent glow set.</summary>
    public static uint GetJobIconId(uint classJobId) =>
        classJobId == 0 ? 0 : 62100u + classJobId;

    public static string GetJobLabel(uint classJobId)
    {
        if (classJobId == 0)
            return I18n.Get("config.cue.target.none");

        var job = JobActionCatalog.GetCombatJobs().FirstOrDefault(j => j.Id == classJobId);
        return job.Id == 0
            ? I18n.Format("job.unknown", classJobId)
            : $"{job.Abbreviation} - {job.Name}";
    }

    private static CueTargetRole RoleFromJob(uint classJobId)
    {
        if (classJobId == 0)
            return CueTargetRole.None;

        try
        {
            var row = PluginServices.DataManager.GetExcelSheet<ClassJob>()?.GetRowOrDefault(classJobId);
            if (row == null)
                return CueTargetRole.None;

            return row.Value.Role switch
            {
                1 => CueTargetRole.Tank,
                2 => CueTargetRole.Melee,
                4 => CueTargetRole.Healer,
                3 => IsCasterJob(classJobId) ? CueTargetRole.Caster : CueTargetRole.Ranged,
                _ => CueTargetRole.None,
            };
        }
        catch
        {
            return CueTargetRole.None;
        }
    }

    private static bool IsCasterJob(uint classJobId) =>
        classJobId is 7 or 25 or 26 or 27 or 35 or 36 or 42;
}
