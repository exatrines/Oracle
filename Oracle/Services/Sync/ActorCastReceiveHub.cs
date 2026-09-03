using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Network;
using FFXIVClientStructs.FFXIV.Client.Network;

namespace Oracle.Services;

internal unsafe delegate void ActorCastReceived(uint casterEntityId, ActorCastPacket* packet);

/// <summary>Single ActorCast hook (cast start). Enemy-cast subscribers attach here.</summary>
internal sealed unsafe class ActorCastReceiveHub : IDisposable
{
    public event ActorCastReceived? Received;

    private Hook<PacketDispatcher.Delegates.HandleActorCastPacket>? _hook;

    public void Subscribe()
    {
        if (_hook != null)
            return;

        try
        {
            var address = PacketDispatcher.Addresses.HandleActorCastPacket.Value;
            if (address == nint.Zero)
            {
                PluginServices.Log.Error("ActorCast address not found; cast hub disabled");
                return;
            }

            _hook = PluginServices.GameInterop.HookFromAddress<PacketDispatcher.Delegates.HandleActorCastPacket>(
                address,
                Detour);
            _hook.Enable();
            PluginServices.Log.Information("ActorCast hook enabled");
        }
        catch (Exception ex)
        {
            PluginServices.Log.Error(ex, "Failed to enable ActorCast hook");
        }
    }

    public void Dispose()
    {
        _hook?.Disable();
        _hook?.Dispose();
        _hook = null;
        Received = null;
    }

    private void Detour(uint casterEntityId, ActorCastPacket* packet)
    {
        _hook!.Original(casterEntityId, packet);

        var handlers = Received;
        if (handlers == null || packet == null)
            return;

        foreach (var handler in handlers.GetInvocationList())
        {
            try
            {
                ((ActorCastReceived)handler)(casterEntityId, packet);
            }
            catch (Exception ex)
            {
                PluginServices.Log.Error(ex, "ActorCast subscriber failed");
            }
        }
    }
}
