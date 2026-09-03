using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using CSCharacter = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;

namespace Oracle.Services;

internal unsafe delegate void StatusChanged(uint statusId, bool removed, uint sourceEntityId);

/// <summary>Local-player StatusManager.SetStatus. Apply when a slot fills; remove when it clears.</summary>
internal sealed unsafe class StatusManagerReceiveHub : IDisposable
{
    public event StatusChanged? Received;

    private Hook<StatusManager.Delegates.SetStatus>? _hook;

    public void Subscribe()
    {
        if (_hook != null)
            return;

        try
        {
            var address = StatusManager.Addresses.SetStatus.Value;
            if (address == nint.Zero)
            {
                PluginServices.Log.Error("StatusManager.SetStatus address not found; status hub disabled");
                return;
            }

            _hook = PluginServices.GameInterop.HookFromAddress<StatusManager.Delegates.SetStatus>(
                address,
                Detour);
            _hook.Enable();
            PluginServices.Log.Information("StatusManager.SetStatus hook enabled");
        }
        catch (Exception ex)
        {
            PluginServices.Log.Error(ex, "Failed to enable StatusManager.SetStatus hook");
        }
    }

    public void Dispose()
    {
        _hook?.Disable();
        _hook?.Dispose();
        _hook = null;
        Received = null;
    }

    private bool Detour(
        StatusManager* manager,
        int statusIndex,
        ushort statusId,
        float remaining,
        ushort param,
        GameObjectId sourceObject,
        bool refreshFlags)
    {
        uint prevId = 0;
        uint prevSource = 0;
        var local = IsLocalPlayerManager(manager);
        if (local && manager != null && statusIndex >= 0)
        {
            prevId = manager->GetStatusId(statusIndex);
            prevSource = manager->GetSourceId(statusIndex);
        }

        var result = _hook!.Original(manager, statusIndex, statusId, remaining, param, sourceObject, refreshFlags);

        if (!local)
            return result;

        var handlers = Received;
        if (handlers == null)
            return result;

        if (prevId != 0 && (statusId == 0 || statusId != prevId))
            Raise(handlers, prevId, removed: true, prevSource);
        if (statusId != 0 && prevId != statusId)
            Raise(handlers, statusId, removed: false, sourceObject.ObjectId);

        return result;
    }

    private static void Raise(StatusChanged handlers, uint statusId, bool removed, uint sourceEntityId)
    {
        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((StatusChanged)handler)(statusId, removed, sourceEntityId);
            }
            catch (Exception ex)
            {
                PluginServices.Log.Error(ex, "StatusManager subscriber failed");
            }
        }
    }

    private static bool IsLocalPlayerManager(StatusManager* manager)
    {
        if (manager == null)
            return false;

        var local = PluginServices.ObjectTable.LocalPlayer;
        if (local == null)
            return false;

        var character = (CSCharacter*)local.Address;
        if (character == null)
            return false;

        return character->GetStatusManager() == manager;
    }
}
