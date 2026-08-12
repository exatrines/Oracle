using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Oracle.Models;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace Oracle.Services;

internal static class ActionLookup
{
    // ActionCategory: 2=Spell, 3=Weaponskill, 4=Ability
    private const uint CategorySpell = 2;
    private const uint CategoryWeaponskill = 3;

    // --- Names & icons from Lumina Action sheet ---

    public static string GetName(uint actionId)
    {
        if (actionId == 0)
            return I18n.Get("config.match.none");

        try
        {
            var row = PluginServices.DataManager.GetExcelSheet<LuminaAction>()?.GetRowOrDefault(actionId);
            var name = row?.Name.ToString();
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }
        catch
        {
            // ignored
        }

        return $"#{actionId}";
    }

    public static uint GetIconId(uint actionId)
    {
        if (actionId == 0)
            return 0;

        try
        {
            var row = PluginServices.DataManager.GetExcelSheet<LuminaAction>()?.GetRowOrDefault(actionId);
            return row?.Icon ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    public static IDalamudTextureWrap? GetIconWrap(uint actionId)
    {
        var iconId = GetIconId(actionId);
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

    /// <summary>
    /// Major two-lane layout: Ability / Role / Memo ↁEupper; Spell / Weaponskill ↁElower.
    /// </summary>
    public static bool IsMajorAbilityLane(TimelineCue cue)
    {
        if (cue.Kind is TimelineCueKind.Memo or TimelineCueKind.SceneTransition)
            return true;

        if (cue.ActionId == 0)
            return true;

        return !IsGcdSkill(cue.ActionId);
    }

    public static string GetOverlayLabel(TimelineCue cue)
    {
        return cue.Kind switch
        {
            TimelineCueKind.Memo => string.IsNullOrWhiteSpace(cue.Label)
                ? I18n.Get("overlay.memo_fallback")
                : cue.Label,
            TimelineCueKind.SceneTransition => I18n.Format(
                "overlay.scene_transition",
                cue.SceneBefore,
                cue.SceneAfter),
            _ => GetName(cue.ActionId),
        };
    }

    public static string GetMajorAbbrev(TimelineCue cue)
    {
        if (cue.Kind == TimelineCueKind.SceneTransition)
        {
            var text = $"{cue.SceneBefore}->{cue.SceneAfter}";
            return text.Length > 4 ? text[..4] : text;
        }

        if (cue.Kind == TimelineCueKind.Memo)
        {
            var memo = string.IsNullOrWhiteSpace(cue.Label)
                ? I18n.Get("overlay.memo_abbrev")
                : cue.Label;
            return memo.Length > 4 ? memo[..4] : memo;
        }

        return string.Empty;
    }

    /// <summary>
    /// Weaponskill / Spell (global cooldown). Abilities and role abilities are excluded.
    /// </summary>
    public static bool IsGcdSkill(TimelineCue cue)
    {
        if (cue.Kind != TimelineCueKind.Action || cue.ActionId == 0)
            return false;

        return IsGcdSkill(cue.ActionId);
    }

    public static bool IsGcdSkill(uint actionId)
    {
        if (actionId == 0)
            return false;

        try
        {
            var row = PluginServices.DataManager.GetExcelSheet<LuminaAction>()?.GetRowOrDefault(actionId);
            if (row == null)
                return false;

            var category = row.Value.ActionCategory.RowId;
            return category is CategorySpell or CategoryWeaponskill;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsSpell(TimelineCue cue)
    {
        if (cue.Kind != TimelineCueKind.Action || cue.ActionId == 0)
            return false;

        return IsSpell(cue.ActionId);
    }

    public static bool IsSpell(uint actionId)
    {
        if (actionId == 0)
            return false;

        try
        {
            var row = PluginServices.DataManager.GetExcelSheet<LuminaAction>()?.GetRowOrDefault(actionId);
            if (row == null)
                return false;

            return row.Value.ActionCategory.RowId == CategorySpell;
        }
        catch
        {
            return false;
        }
    }
}
