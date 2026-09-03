using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Oracle.Models;
using LuminaAction = Lumina.Excel.Sheets.Action;
using LuminaStatus = Lumina.Excel.Sheets.Status;

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

    public static bool IsPlaceholderName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return true;

        var trimmed = name.Trim();
        return trimmed.StartsWith('#')
            || trimmed.StartsWith("_rsv_", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("unknown_", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetStatusName(uint statusId)
    {
        if (statusId == 0)
            return I18n.Get("config.match.none");

        try
        {
            var row = PluginServices.DataManager.GetExcelSheet<LuminaStatus>()?.GetRowOrDefault(statusId);
            var name = row?.Name.ToString();
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }
        catch
        {
            // ignored
        }

        return $"#{statusId}";
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
    /// Major two-lane layout: Ability / Role on the upper lane; Spell / Weaponskill on the lower.
    /// </summary>
    public static bool IsMajorAbilityLane(TimelineCue cue) =>
        cue.ActionId == 0 || !IsGcdSkill(cue.ActionId);

    /// <summary>
    /// Weaponskill / Spell (global cooldown). Abilities and role abilities are excluded.
    /// </summary>
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
