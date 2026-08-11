using Dalamud.Game.ClientState.Objects.Enums;

namespace Oracle.Services;

/// <summary>LocalPlayer StatusFlags.InCombat edges.</summary>
internal sealed class CombatSyncDetector
{
    private bool _wasInCombat;
    private bool _initialized;

    public bool JustEnteredCombat { get; private set; }
    public bool JustLeftCombat { get; private set; }

    public void Update()
    {
        var inCombat = ReadPlayerInCombat();

        if (!_initialized)
        {
            _initialized = true;
            _wasInCombat = inCombat;
            JustEnteredCombat = inCombat;
            JustLeftCombat = false;
            return;
        }

        JustEnteredCombat = inCombat && !_wasInCombat;
        JustLeftCombat = !inCombat && _wasInCombat;
        _wasInCombat = inCombat;
    }

    public void Reset()
    {
        _wasInCombat = ReadPlayerInCombat();
        JustEnteredCombat = false;
        JustLeftCombat = false;
        _initialized = true;
    }

    private static bool ReadPlayerInCombat()
    {
        var player = PluginServices.ObjectTable.LocalPlayer;
        return player != null && player.StatusFlags.HasFlag(StatusFlags.InCombat);
    }
}
