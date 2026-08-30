using System.Collections.Concurrent;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace Oracle.Services;

/// <summary>
/// Confirms local-player ability execution via ActionEffectHandler.Receive (server ActionEffect).
/// Unlike UseAction, this ignores hotbar spam / queue that does not actually fire.
/// </summary>
internal sealed unsafe class ActionUseDetector : IDisposable
{
    private readonly ConcurrentQueue<uint> _pendingActionIds = new();
    private Hook<ActionEffectHandler.Delegates.Receive>? _receiveHook;

    public event Action<uint, uint>? ActionUsed;

    public void Subscribe()
    {
        if (_receiveHook != null)
            return;

        try
        {
            // Hook ActionEffect Receive so only server-confirmed casts count (not queue spam).
            var address = ActionEffectHandler.Addresses.Receive.Value;
            if (address == nint.Zero)
            {
                PluginServices.Log.Error("ActionEffect Receive address not found; action detect disabled");
                return;
            }

            _receiveHook = PluginServices.GameInterop.HookFromAddress<ActionEffectHandler.Delegates.Receive>(
                address,
                ReceiveDetour);
            _receiveHook.Enable();
            PluginServices.Log.Information("ActionEffect Receive hook enabled for confirmed action use");
        }
        catch (Exception ex)
        {
            PluginServices.Log.Error(ex, "Failed to enable ActionEffect Receive hook");
        }
    }

    public void Dispose()
    {
        _receiveHook?.Disable();
        _receiveHook?.Dispose();
        _receiveHook = null;
        Reset();
    }

    public void Reset()
    {
        while (_pendingActionIds.TryDequeue(out _))
        {
        }
    }

    public bool TryDequeue(out uint actionId) => _pendingActionIds.TryDequeue(out actionId);

    private void ReceiveDetour(
        uint casterEntityId,
        Character* casterPtr,
        Vector3* targetPos,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targetEntityIds)
    {
        _receiveHook!.Original(casterEntityId, casterPtr, targetPos, header, effects, targetEntityIds);

        try
        {
            if (header == null)
                return;

            var local = PluginServices.ObjectTable.LocalPlayer;
            if (local == null || local.EntityId != casterEntityId)
                return;

            // Skip server-driven effects (autos / non-client casts). Hotbar uses have SourceSequence != 0.
            if (header->SourceSequence == 0)
                return;

            // Header.ActionType is a byte; ActionType enum is uint-backed.
            var actionType = (ActionType)header->ActionType;
            if (actionType is not (ActionType.Action or ActionType.GeneralAction))
                return;

            var actionId = header->ActionId;
            if (actionId == 0)
                return;

            // Prefer adjusted id so combo/stance variants match timeline cues.
            var recordId = actionId;
            var am = ActionManager.Instance();
            if (am != null)
            {
                var adjusted = am->GetAdjustedActionId(actionId);
                if (adjusted != 0)
                    recordId = adjusted;
            }

            EnqueueActionId(actionId);
            if (recordId != actionId)
                EnqueueActionId(recordId);

            var targetJobId = ResolveOtherPlayerTargetJobId(casterEntityId, header, targetEntityIds);
            ActionUsed?.Invoke(recordId, targetJobId);
        }
        catch (Exception ex)
        {
            PluginServices.Log.Error(ex, "ActionEffect Receive detour failed");
        }
    }

    private static uint ResolveOtherPlayerTargetJobId(
        uint casterEntityId,
        ActionEffectHandler.Header* header,
        GameObjectId* targetEntityIds)
    {
        if (targetEntityIds == null)
            return 0;

        var count = header->NumTargets;
        if (count <= 0)
            return 0;

        uint foundEntityId = 0;
        uint foundJobId = 0;

        for (var i = 0; i < count; i++)
        {
            var entityId = targetEntityIds[i].ObjectId;
            if (entityId == 0 || entityId == 0xE0000000 || entityId == casterEntityId)
                continue;
            if (entityId == foundEntityId)
                continue;

            var jobId = ReadOtherPlayerJobId(entityId);
            if (jobId == 0)
                continue;

            if (foundEntityId != 0)
                return 0;

            foundEntityId = entityId;
            foundJobId = jobId;
        }

        return foundJobId;
    }

    private static uint ReadOtherPlayerJobId(uint entityId)
    {
        foreach (var obj in PluginServices.ObjectTable)
        {
            if (obj == null || obj.EntityId != entityId)
                continue;
            // 1 = Player / Pc in both Dalamud and FFXIVClientStructs ObjectKind.
            if ((int)obj.ObjectKind != 1)
                continue;

            return ReadClassJobId(obj);
        }

        return 0;
    }

    private static uint ReadClassJobId(IGameObject obj)
    {
        if (obj is IBattleChara battle)
            return battle.ClassJob.RowId;

        var ch = (Character*)obj.Address;
        if (ch == null)
            return 0;
        return ch->CharacterData.ClassJob;
    }

    private void EnqueueActionId(uint actionId)
    {
        if (actionId == 0)
            return;

        if (_pendingActionIds.Count > 64)
            _pendingActionIds.TryDequeue(out _);

        _pendingActionIds.Enqueue(actionId);
    }
}
