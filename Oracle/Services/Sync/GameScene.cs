using FFXIVClientStructs.FFXIV.Client.Graphics.Environment;

namespace Oracle.Services;

/// <summary>Live EnvManager scene id (Splatoon-compatible offset).</summary>
internal static unsafe class GameScene
{
    public static uint ReadId()
    {
        try
        {
            var env = EnvManager.Instance();
            if (env == null)
                return 0;
            return *((byte*)env + 0x24);
        }
        catch
        {
            return 0;
        }
    }
}
