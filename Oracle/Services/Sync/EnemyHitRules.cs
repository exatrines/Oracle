using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace Oracle.Services;

internal readonly struct EnemyHitInfo
{
    public EnemyHitInfo(uint actionId, string actionName, string casterName, int friendlyTargetCount)
    {
        ActionId = actionId;
        ActionName = actionName;
        CasterName = casterName;
        FriendlyTargetCount = friendlyTargetCount;
    }

    public uint ActionId { get; }
    public string ActionName { get; }
    public string CasterName { get; }
    public int FriendlyTargetCount { get; }
}

/// <summary>Enemy-vs-friendly ActionEffect / ActorCast filter. Raw events, no clustering.</summary>
internal static unsafe class EnemyHitRules
{
    private const uint InvalidObjectId = 0xE0000000;
    private const uint ActionCategoryAutoAttack = 1;

    public static bool TryMatch(
        uint casterEntityId,
        ActionEffectHandler.Header* header,
        GameObjectId* targetEntityIds,
        out EnemyHitInfo hit)
    {
        hit = default;
        if (header == null)
            return false;

        var actionId = header->ActionId;
        if (actionId == 0)
            return false;

        if ((ActionType)header->ActionType != ActionType.Action)
            return false;

        var caster = PluginServices.ObjectTable.SearchByEntityId(casterEntityId);
        if (caster == null || IsFriendlyActor(caster))
            return false;

        var friendlyCount = CountFriendlyTargets(header->NumTargets, targetEntityIds);
        if (friendlyCount == 0)
            return false;

        var actionName = ActionLookup.GetName(actionId);
        if (IsAutoAttack(actionId, actionName))
            return false;

        hit = new EnemyHitInfo(actionId, actionName, ResolveCasterName(caster, casterEntityId), friendlyCount);
        return true;
    }

    public static bool TryMatchCastStart(
        uint casterEntityId,
        uint actionId,
        byte actionType,
        out EnemyHitInfo hit)
    {
        hit = default;
        if ((ActionType)actionType != ActionType.Action)
            return false;
        return TryResolveEnemyAbility(casterEntityId, actionId, requireKnownCaster: true, out hit);
    }

    public static bool TryMatchCastEffected(
        uint casterEntityId,
        ActionEffectHandler.Header* header,
        out EnemyHitInfo hit)
    {
        hit = default;
        if (header == null)
            return false;
        if ((ActionType)header->ActionType != ActionType.Action)
            return false;
        return TryResolveEnemyAbility(casterEntityId, header->ActionId, requireKnownCaster: true, out hit);
    }

    public static bool TryDescribeEnemyCast(
        uint casterEntityId,
        uint actionId,
        ushort spellId,
        out EnemyHitInfo hit)
    {
        var id = actionId != 0 ? actionId : spellId;
        return TryResolveEnemyAbility(casterEntityId, id, requireKnownCaster: false, out hit);
    }

    private static bool TryResolveEnemyAbility(
        uint casterEntityId,
        uint actionId,
        bool requireKnownCaster,
        out EnemyHitInfo hit)
    {
        hit = default;
        if (actionId == 0)
            return false;

        var caster = PluginServices.ObjectTable.SearchByEntityId(casterEntityId);
        if (requireKnownCaster)
        {
            if (caster == null || IsFriendlyActor(caster))
                return false;
        }
        else if (caster != null && IsFriendlyActor(caster))
        {
            return false;
        }

        var actionName = ActionLookup.GetName(actionId);
        if (IsAutoAttack(actionId, actionName))
            return false;

        hit = new EnemyHitInfo(actionId, actionName, ResolveCasterName(caster, casterEntityId), 0);
        return true;
    }

    private static string ResolveCasterName(IGameObject? caster, uint casterEntityId)
    {
        var name = caster?.Name.TextValue;
        return string.IsNullOrWhiteSpace(name) ? $"#{casterEntityId}" : name;
    }

    private static int CountFriendlyTargets(byte numTargets, GameObjectId* targetEntityIds)
    {
        if (targetEntityIds == null || numTargets == 0)
            return 0;

        var count = 0;
        for (var i = 0; i < numTargets; i++)
        {
            var entityId = targetEntityIds[i].ObjectId;
            if (entityId == 0 || entityId == InvalidObjectId)
                continue;
            if (IsFriendlyEntity(entityId))
                count++;
        }

        return count;
    }

    internal static bool IsFriendlyEntity(uint entityId)
    {
        var local = PluginServices.ObjectTable.LocalPlayer;
        if (local != null && local.EntityId == entityId)
            return true;

        var obj = PluginServices.ObjectTable.SearchByEntityId(entityId);
        return obj != null && IsFriendlyActor(obj);
    }

    private static bool IsFriendlyActor(IGameObject obj)
    {
        // 1 = Player / Pc in both Dalamud and FFXIVClientStructs ObjectKind.
        if ((int)obj.ObjectKind == 1)
            return true;

        if (obj.OwnerId == 0 || obj.OwnerId == InvalidObjectId)
            return false;

        var owner = PluginServices.ObjectTable.SearchByEntityId(obj.OwnerId);
        return owner != null && (int)owner.ObjectKind == 1;
    }

    private static bool IsAutoAttack(uint actionId, string name)
    {
        if (actionId is 7 or 8)
            return true;
        if (IsAutoAttackName(name))
            return true;

        try
        {
            var row = PluginServices.DataManager.GetExcelSheet<LuminaAction>()?.GetRowOrDefault(actionId);
            return row?.ActionCategory.RowId == ActionCategoryAutoAttack;
        }
        catch
        {
            return false;
        }
    }

    internal static bool IsAutoAttackName(string name) =>
        name.Equals("Attack", StringComparison.OrdinalIgnoreCase)
        || name.Equals("攻撃", StringComparison.Ordinal);
}
