using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Oracle.Services;
using Oracle.Services.AutoRecord;
using Oracle.UI;

namespace Oracle;

public sealed class Plugin : IDalamudPlugin
{
    public const string Name = "Oracle";

    private const string CommandName = "/oracle";
    private const string CommandAlias = "/or";

    internal static Configuration C = null!;

    private readonly WindowSystem _windowSystem;
    private readonly ConfigWindow _timelineConfigWindow;
    private readonly PluginSettingsWindow _pluginSettingsWindow;
    private readonly CueOverlayWindow _overlayWindow;
    private readonly MajorOverlayWindow _majorOverlayWindow;
    private readonly AutoRecordOverlayWindow _autoRecordOverlayWindow;
    private readonly FFLogsImportPanel _ffLogsImportPanel;
    private readonly TimelineEngine _engine;
    private readonly AutoRecordService _autoRecord;
    private readonly HotbarHighlightService _hotbarHighlight;
    private readonly CommandInfo _oracleCommand;
    private readonly CommandInfo _aliasCommand;

    public Plugin(
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
        // 1. Dalamud service locator + config + localization + shared UI theme
        RegisterPluginServices(
            pluginInterface,
            commandManager,
            framework,
            clientState,
            objectTable,
            dataManager,
            textureProvider,
            chatGui,
            log,
            gameGui,
            gameInterop,
            notificationManager,
            playerState);

        // 2. Timeline persistence, runtime engine, auto-record
        var configStore = new ConfigStore(pluginInterface);
        var timelineStore = new TimelineStore(configStore);
        _engine = new TimelineEngine(timelineStore);
        var autoRecordStore = new AutoRecordStore(pluginInterface);
        _autoRecord = new AutoRecordService(autoRecordStore, _engine.ActionUse);
        _hotbarHighlight = new HotbarHighlightService(_engine);

        // 3. ImGui windows  EActionSearch / import panels capture ConfigWindow via delayed assign
        _pluginSettingsWindow = new PluginSettingsWindow();

        ConfigWindow? timelineWindow = null;
        var actionSearch = new ActionSearchWindow(
            getClassJobId: () => (timelineWindow?.EditDocument ?? timelineStore.ActiveDocument)?.ClassJobId ?? 0,
            setClassJobId: id => timelineWindow?.SetEditDocumentClassJob(id),
            getTerritoryTypeId: () => (timelineWindow?.EditDocument ?? timelineStore.ActiveDocument)?.TerritoryTypeId ?? 0,
            getContentFinderConditionId: () =>
                (timelineWindow?.EditDocument ?? timelineStore.ActiveDocument)?.ContentFinderConditionId ?? 0,
            getClassJobLevel: () => (timelineWindow?.EditDocument ?? timelineStore.ActiveDocument)?.ClassJobLevel ?? 0,
            onPicked: action => timelineWindow?.ApplyPickedAction(action));

        FFLogsImportPanel? ffLogsImport = null;
        ffLogsImport = new FFLogsImportPanel(onImported: doc => timelineWindow?.SelectImportedTimeline(doc));

        var autoRecordImport = new AutoRecordImportPanel(
            autoRecordStore,
            onImported: doc => timelineWindow?.SelectImportedTimeline(doc));

        timelineWindow = new ConfigWindow(
            timelineStore,
            _engine,
            actionSearch,
            ffLogsImport,
            autoRecordImport,
            openPluginSettings: TogglePluginSettings);

        _timelineConfigWindow = timelineWindow!;
        _ffLogsImportPanel = ffLogsImport!;
        _overlayWindow = new CueOverlayWindow(_engine);
        _majorOverlayWindow = new MajorOverlayWindow(_engine);
        _autoRecordOverlayWindow = new AutoRecordOverlayWindow(
            _autoRecord,
            openAutoRecordZones: () => _pluginSettingsWindow.ToggleAutoRecordPage());
        // 4. Window system + Dalamud UI hooks
        _windowSystem = new WindowSystem("Oracle");
        _windowSystem.AddWindow(_timelineConfigWindow);
        _windowSystem.AddWindow(_pluginSettingsWindow);
        _windowSystem.AddWindow(_overlayWindow);
        _windowSystem.AddWindow(_majorOverlayWindow);
        _windowSystem.AddWindow(_autoRecordOverlayWindow);
        _windowSystem.AddWindow(actionSearch);

        pluginInterface.UiBuilder.Draw += DrawUi;
        pluginInterface.UiBuilder.OpenConfigUi += TogglePluginSettings;
        pluginInterface.UiBuilder.OpenMainUi += ToggleTimelineSettings;

        // 5. Chat commands + per-frame tick
        _oracleCommand = new CommandInfo(OnCommand) { HelpMessage = I18n.Get("cmd.help.oracle") };
        _aliasCommand = new CommandInfo(OnCommand) { HelpMessage = I18n.Get("cmd.help.alias") };
        commandManager.AddHandler(CommandName, _oracleCommand);
        commandManager.AddHandler(CommandAlias, _aliasCommand);
        I18n.Reloaded += OnI18nReloaded;

        framework.Update += OnFrameworkUpdate;
    }

    private static void RegisterPluginServices(
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
        PluginServices.Init(
            pluginInterface,
            commandManager,
            framework,
            clientState,
            objectTable,
            dataManager,
            textureProvider,
            chatGui,
            log,
            gameGui,
            gameInterop,
            notificationManager,
            playerState);

        C = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        C.Initialize(pluginInterface);
        C.ThemeColors ??= MirageColorSettings.CreateDefault();

        I18n.Init(pluginInterface);

        MirageUi.ConfigureTheme(() => C.ThemeColors ?? MirageColorSettings.CreateDefault());
        MirageUi.Init(pluginInterface, textureProvider, log);
    }

    private void OnI18nReloaded()
    {
        _oracleCommand.HelpMessage = I18n.Get("cmd.help.oracle");
        _aliasCommand.HelpMessage = I18n.Get("cmd.help.alias");
    }

    private void OnCommand(string command, string args)
    {
        var parts = (args ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            ToggleTimelineSettings();
            return;
        }

        switch (parts[0].ToLowerInvariant())
        {
            case "s":
            case "setting":
            case "settings":
                HandleSettingsCommand(parts.Skip(1).ToArray());
                break;
            case "toggle":
            case "config":
                ToggleTimelineSettings();
                break;
            case "overlay":
                ToggleOverlay();
                break;
            case "ar":
            case "autorecord":
                ToggleAutoRecordOverlay();
                break;
            case "countdown":
            case "cd":
                if (parts.Length < 2 || !float.TryParse(parts[1], out var sec) || sec <= 0f)
                {
                    PluginServices.ChatGui.PrintError(I18n.Get("cmd.err.countdown_usage"));
                    return;
                }
                _engine.InjectCountdown(sec);
                break;
            case "reset":
                _engine.Reset();
                PluginServices.ChatGui.Print(I18n.Get("cmd.chat.reset"));
                break;
            case "preview":
                if (parts.Length > 1 && parts[1].Equals("stop", StringComparison.OrdinalIgnoreCase))
                    _engine.StopPreview();
                else
                    _engine.StartPreview();
                break;
            case "load":
                LoadTimelineByToken(parts.Length > 1 ? string.Join(' ', parts.Skip(1)) : string.Empty);
                break;
            case "help":
                PluginServices.ChatGui.Print(I18n.Get("cmd.chat.help"));
                break;
            default:
                PluginServices.ChatGui.PrintError(I18n.Format("cmd.err.unknown", parts[0]));
                break;
        }
    }

    private void LoadTimelineByToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            PluginServices.ChatGui.PrintError(I18n.Get("cmd.err.load_usage"));
            return;
        }

        if (!_engine.TryManualLoadByToken(token, out var doc) || doc == null)
        {
            PluginServices.ChatGui.PrintError(I18n.Format("cmd.err.not_found", token));
            return;
        }

        PluginServices.ChatGui.Print(I18n.Format("config.chat.loaded", doc.Name));
    }

    private void ToggleTimelineSettings() => _timelineConfigWindow.Toggle();

    private void TogglePluginSettings() => _pluginSettingsWindow.Toggle();

    private void HandleSettingsCommand(string[] parts)
    {
        if (parts.Length == 0)
        {
            TogglePluginSettings();
            return;
        }

        switch (parts[0].ToLowerInvariant())
        {
            case "autorecord":
            case "ar":
                HandleAutoRecordSettingsCommand(parts.Skip(1).ToArray());
                break;
            default:
                PluginServices.ChatGui.PrintError(I18n.Format("cmd.err.unknown", parts[0]));
                break;
        }
    }

    private void HandleAutoRecordSettingsCommand(string[] parts)
    {
        if (parts.Length == 0)
        {
            _pluginSettingsWindow.ToggleAutoRecordPage();
            return;
        }

        switch (parts[0].ToLowerInvariant())
        {
            case "enable-record":
            case "toggle-recording":
            case "toggle-autorecord":
            {
                C.AutoRecordEnabled = !C.AutoRecordEnabled;
                C.Save();
                PluginServices.ChatGui.Print(
                    I18n.Get(
                        C.AutoRecordEnabled
                            ? "cmd.chat.autorecord_enabled"
                            : "cmd.chat.autorecord_disabled"));
                break;
            }
            case "enable-current":
            case "toggle-current-zone":
            {
                var territory = PluginServices.ClientState.TerritoryType;
                if (territory == 0)
                {
                    PluginServices.ChatGui.PrintError(I18n.Get("cmd.err.autorecord_no_zone"));
                    return;
                }

                var enabled = C.IsAutoRecordZoneEnabled(territory);
                if (enabled)
                    C.RemoveAutoRecordZoneEnabled(territory);
                else
                    C.AddAutoRecordZoneEnabled(territory);

                PluginServices.ChatGui.Print(
                    I18n.Format(
                        enabled
                            ? "cmd.chat.autorecord_zone_disabled"
                            : "cmd.chat.autorecord_zone_enabled",
                        territory));
                break;
            }
            default:
                PluginServices.ChatGui.PrintError(I18n.Format("cmd.err.unknown", parts[0]));
                break;
        }
    }

    private void ToggleOverlay()
    {
        C.ShowOverlay = !C.ShowOverlay;
        C.Save();
        PluginServices.ChatGui.Print(
            I18n.Format(
                "cmd.chat.overlay",
                I18n.Get(C.ShowOverlay ? "cmd.chat.overlay.shown" : "cmd.chat.overlay.hidden")));
    }

    private void ToggleAutoRecordOverlay()
    {
        if (!C.AutoRecordEnabled)
        {
            PluginServices.ChatGui.PrintError(I18n.Get("cmd.err.autorecord_disabled"));
            return;
        }

        C.AutoRecordOverlayVisible = !C.AutoRecordOverlayVisible;
        C.Save();
        PluginServices.ChatGui.Print(
            I18n.Format(
                "cmd.chat.autorecord_overlay",
                I18n.Get(
                    C.AutoRecordOverlayVisible
                        ? "cmd.chat.overlay.shown"
                        : "cmd.chat.overlay.hidden")));
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        _engine.Update();
        _autoRecord.Update();
    }

    private void DrawUi()
    {
        _windowSystem.Draw();
        _hotbarHighlight.Draw();
    }

    public void Dispose()
    {
        // Reverse of ctor: tick ↁEcommands ↁEUI ↁEservices ↁEconfig
        PluginServices.Framework.Update -= OnFrameworkUpdate;

        I18n.Reloaded -= OnI18nReloaded;
        PluginServices.CommandManager.RemoveHandler(CommandName);
        PluginServices.CommandManager.RemoveHandler(CommandAlias);

        PluginServices.PluginInterface.UiBuilder.Draw -= DrawUi;
        PluginServices.PluginInterface.UiBuilder.OpenConfigUi -= TogglePluginSettings;
        PluginServices.PluginInterface.UiBuilder.OpenMainUi -= ToggleTimelineSettings;

        _autoRecord.Dispose();
        _engine.Dispose();
        _ffLogsImportPanel.Dispose();

        C.Save();
        MirageUi.Dispose();

        _windowSystem.RemoveAllWindows();
        I18n.Dispose();
        PluginServices.Clear();
        C = null!;
    }
}
