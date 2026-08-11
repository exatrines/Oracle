using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;

namespace Oracle.Services;

/// <summary>Static access to Dalamud services injected at plugin startup.</summary>
internal static class PluginServices
{
    internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    internal static ICommandManager CommandManager { get; private set; } = null!;
    internal static IFramework Framework { get; private set; } = null!;
    internal static IClientState ClientState { get; private set; } = null!;
    internal static IObjectTable ObjectTable { get; private set; } = null!;
    internal static IDataManager DataManager { get; private set; } = null!;
    internal static ITextureProvider TextureProvider { get; private set; } = null!;
    internal static IChatGui ChatGui { get; private set; } = null!;
    internal static IPluginLog Log { get; private set; } = null!;
    internal static IGameGui GameGui { get; private set; } = null!;
    internal static IGameInteropProvider GameInterop { get; private set; } = null!;
    internal static INotificationManager NotificationManager { get; private set; } = null!;
    internal static IPlayerState PlayerState { get; private set; } = null!;

    internal static void Init(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IFramework framework,
        IClientState clientState,
        IObjectTable objectTable,
        IDataManager dataManager,
        ITextureProvider textureProvider,
        IChatGui chatGui,
        IPluginLog log,
        IGameGui gameGui,
        IGameInteropProvider gameInterop,
        INotificationManager notificationManager,
        IPlayerState playerState)
    {
        PluginInterface = pluginInterface;
        CommandManager = commandManager;
        Framework = framework;
        ClientState = clientState;
        ObjectTable = objectTable;
        DataManager = dataManager;
        TextureProvider = textureProvider;
        ChatGui = chatGui;
        Log = log;
        GameGui = gameGui;
        GameInterop = gameInterop;
        NotificationManager = notificationManager;
        PlayerState = playerState;
    }

    internal static void Clear()
    {
        PluginInterface = null!;
        CommandManager = null!;
        Framework = null!;
        ClientState = null!;
        ObjectTable = null!;
        DataManager = null!;
        TextureProvider = null!;
        ChatGui = null!;
        Log = null!;
        GameGui = null!;
        GameInterop = null!;
        NotificationManager = null!;
        PlayerState = null!;
    }
}
