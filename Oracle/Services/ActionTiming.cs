using Dalamud.Game.Player;
using Oracle.Models;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace Oracle.Services;

/// <summary>
/// Action cast/recast from the sheet, scaled by the local player's Skill/Spell Speed.
/// </summary>
internal static class ActionTiming
{
    // ActionCategory: 2=Spell, 3=Weaponskill, 4=Ability
    private const uint CategorySpell = 2;
    private const uint CategoryWeaponskill = 3;

    /// <summary>Spell cast length (0 for non-spells / instant).</summary>
    public static float GetCastSeconds(TimelineCue cue) =>
        cue.Kind == TimelineCueKind.Action ? GetCastSeconds(cue.ActionId) : 0f;

    public static float GetCastSeconds(uint actionId)
    {
        if (!TryGetSheetTimes(actionId, out var category, out var cast, out _))
            return 0f;
        if (category != CategorySpell || cast <= 0f)
            return 0f;

        return ScaleBySpeed(cast, GetSpellSpeed(), GetEffectiveLevel());
    }

    /// <summary>
    /// Weaponskill / spell recast (GCD window). 0 for abilities.
    /// </summary>
    public static float GetRecastSeconds(TimelineCue cue) =>
        cue.Kind == TimelineCueKind.Action ? GetRecastSeconds(cue.ActionId) : 0f;

    public static float GetRecastSeconds(uint actionId)
    {
        if (!TryGetSheetTimes(actionId, out var category, out _, out var recast))
            return 0f;
        if (category is not (CategorySpell or CategoryWeaponskill) || recast <= 0f)
            return 0f;

        var speed = category == CategorySpell ? GetSpellSpeed() : GetSkillSpeed();
        return ScaleBySpeed(recast, speed, GetEffectiveLevel());
    }

    /// <summary>
    /// Allagan Studies speed scaling (no haste):
    /// ⌁E1000 ∁E⌁E30ÁESpeed−SUB)/DIV⌁E ÁEbase⌁E/ 1000
    /// </summary>
    public static float ScaleBySpeed(float baseSeconds, int speed, int level)
    {
        if (baseSeconds <= 0f)
            return 0f;

        GetLevelMods(level, out var sub, out var div);
        if (speed < sub)
            speed = sub;

        var trait = Math.Floor(130.0 * (speed - sub) / div);
        var scaled = Math.Floor((1000.0 - trait) * baseSeconds) / 1000.0;
        return (float)Math.Max(0.0, scaled);
    }

    private static bool TryGetSheetTimes(
        uint actionId,
        out uint category,
        out float castSeconds,
        out float recastSeconds)
    {
        category = 0;
        castSeconds = 0f;
        recastSeconds = 0f;
        if (actionId == 0)
            return false;

        try
        {
            var row = PluginServices.DataManager.GetExcelSheet<LuminaAction>()?.GetRowOrDefault(actionId);
            if (row == null)
                return false;

            category = row.Value.ActionCategory.RowId;
            castSeconds = (row.Value.Cast100ms + row.Value.ExtraCastTime100ms) / 10f;
            recastSeconds = row.Value.Recast100ms / 10f;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void GetLevelMods(int level, out int sub, out int div)
    {
        // Level mods (Allagan Studies). Coarse bands are enough for display timing.
        if (level >= 100)
        {
            sub = 420;
            div = 2780;
            return;
        }

        if (level >= 90)
        {
            sub = 400;
            div = 1900;
            return;
        }

        if (level >= 80)
        {
            sub = 380;
            div = 1650;
            return;
        }

        if (level >= 70)
        {
            sub = 360;
            div = 1300;
            return;
        }

        sub = 340;
        div = 1000;
    }

    private static int GetEffectiveLevel()
    {
        try
        {
            var state = PluginServices.PlayerState;
            if (state is { IsLoaded: true })
                return Math.Clamp((int)state.EffectiveLevel, 1, 100);
        }
        catch
        {
            // ignored
        }

        var player = PluginServices.ObjectTable.LocalPlayer;
        return player != null ? Math.Clamp((int)player.Level, 1, 100) : 100;
    }

    private static int GetSkillSpeed() => GetSpeedAttribute(PlayerAttribute.SkillSpeed);

    private static int GetSpellSpeed() => GetSpeedAttribute(PlayerAttribute.SpellSpeed);

    private static int GetSpeedAttribute(PlayerAttribute attribute)
    {
        try
        {
            var state = PluginServices.PlayerState;
            if (state is { IsLoaded: true })
                return Math.Max(0, state.GetAttribute(attribute));
        }
        catch
        {
            // ignored
        }

        return 0;
    }
}
