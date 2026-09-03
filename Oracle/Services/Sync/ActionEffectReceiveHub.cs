using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace Oracle.Services;

internal unsafe delegate void ActionEffectReceived(
    uint casterEntityId,
    Character* casterPtr,
    ActionEffectHandler.Header* header,
    ActionEffectHandler.TargetEffects* effects,
    GameObjectId* targetEntityIds);

/// <summary>Single ActionEffect Receive hook. Player-use and enemy-hit subscribe separately.</summary>
internal sealed unsafe class ActionEffectReceiveHub : IDisposable
{
    public event ActionEffectReceived? Received;

    private Hook<ActionEffectHandler.Delegates.Receive>? _receiveHook;

    public void Subscribe()
    {
        if (_receiveHook != null)
            return;

        try
        {
            var address = ActionEffectHandler.Addresses.Receive.Value;
            if (address == nint.Zero)
            {
                PluginServices.Log.Error("ActionEffect Receive address not found; receive hub disabled");
                return;
            }

            _receiveHook = PluginServices.GameInterop.HookFromAddress<ActionEffectHandler.Delegates.Receive>(
                address,
                ReceiveDetour);
            _receiveHook.Enable();
            PluginServices.Log.Information("ActionEffect Receive hook enabled");
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
        Received = null;
    }

    private void ReceiveDetour(
        uint casterEntityId,
        Character* casterPtr,
        Vector3* targetPos,
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targetEntityIds)
    {
        _receiveHook!.Original(casterEntityId, casterPtr, targetPos, header, effects, targetEntityIds);

        var handlers = Received;
        if (handlers == null)
            return;

        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((ActionEffectReceived)handler)(casterEntityId, casterPtr, header, effects, targetEntityIds);
            }
            catch (Exception ex)
            {
                PluginServices.Log.Error(ex, "ActionEffect Receive subscriber failed");
            }
        }
    }
}
