namespace Oracle.UI;

// MirageUI 2.0 title bar and left rail. The rail mark opens the plugin page.
internal static class OracleWindowChrome
{
    public const string MainPageId = "main";
    public const string SettingsPageId = "settings";

    public static readonly Vector2 DefaultSize = new(900f, 630f);

    private static readonly MirageWindowFeature[] PluginFeature =
    [
        new()
        {
            Id = MirageWindowHeader.PluginPageId,
            PageId = MirageWindowHeader.PluginPageId,
            Icon = FontAwesomeIcon.Heart,
            ShowOnTitle = true,
            TitleKind = MirageWindowFeatureKind.OpenPage,
        },
    ];

    public static bool IsPluginPage(string pageId) =>
        pageId == MirageWindowHeader.PluginPageId;

    public static void Draw(Window window, string title, ref bool windowCollapsed)
    {
        var pageId = MainPageId;
        MirageWindowHeader.Draw(window, title, ref windowCollapsed, 0f, ref pageId, []);
        if (windowCollapsed)
            return;

        ImGui.SetCursorPos(new Vector2(0f, MirageWindowHeader.BarHeight));
        MirageWindowHeader.FillContentPage(ImDrawFlags.RoundCornersBottom);
    }

    public static void Draw(
        Window window,
        string title,
        ref bool windowCollapsed,
        ref bool railCollapsed,
        ref string pageId,
        string sectionLabel,
        FontAwesomeIcon sectionIcon)
    {
        MirageWindowHeader.Section[] sections = [new(MainPageId, sectionLabel, sectionIcon)];
        Draw(window, title, ref windowCollapsed, ref railCollapsed, ref pageId, sections);
    }

    public static MirageWindowFeature[] MainWindowFeatures() =>
    [
        PluginFeature[0],
        ThemeFeature(),
        LanguageFeature(),
    ];

    public static void Draw(
        Window window,
        string title,
        ref bool windowCollapsed,
        ref bool railCollapsed,
        ref string pageId,
        ReadOnlySpan<MirageWindowHeader.Section> sections,
        IReadOnlyList<MirageWindowFeature>? features = null)
    {
        if (string.IsNullOrEmpty(pageId))
            pageId = MainPageId;

        features ??= PluginFeature;
        var rail = MirageWindowHeader.RailWidth(railCollapsed, sections, features);
        MirageWindowHeader.Draw(window, title, ref windowCollapsed, 0f, ref pageId, features);
        MirageWindowHeader.DrawRail(
            sections,
            ref pageId,
            ref railCollapsed,
            showSections: !windowCollapsed,
            features);
        if (windowCollapsed)
            return;

        ImGui.SetCursorPos(new Vector2(rail, MirageWindowHeader.BarHeight));
        MirageWindowHeader.FillContentPage();
        MirageWindowHeader.DrawRailDivider(rail);
    }

    public static void ApplyFixedSize(Window window, bool collapsed, Vector2 expanded)
    {
        var size = collapsed
            ? new Vector2(expanded.X, MirageWindowHeader.BarHeight)
            : expanded;
        window.Size = size;
        window.SizeCondition = ImGuiCond.Always;
        window.SizeConstraints = new()
        {
            MinimumSize = size,
            MaximumSize = size,
        };
    }

    private static MirageWindowFeature ThemeFeature() =>
        new()
        {
            Id = "theme",
            PageId = MirageWindowHeader.ThemePageId,
            Icon = FontAwesomeIcon.Palette,
            Tooltip = MirageUi.I18n.Get("window.theme"),
            OpenPageLabel = I18n.Get("theme.settings"),
            ShowOnTitle = true,
            TitleKind = MirageWindowFeatureKind.Shortcut,
            TitleOpensPage = true,
            TitleOrder = 1,
            ShowOnRail = true,
            RailWhen = MirageWindowRailWhen.Both,
            RailKind = MirageWindowFeatureKind.OpenPage,
            RailOrder = 1,
            PopupTextWidth = () =>
            {
                var width = 0f;
                foreach (var theme in C.Themes)
                    width = MathF.Max(width, ImGui.CalcTextSize(OracleTheme.DisplayName(theme)).X);
                return width;
            },
            DrawPopup = _ =>
            {
                foreach (var theme in C.Themes)
                {
                    var current = theme.Id == C.SelectedThemeId;
                    if (MirageWindowHeader.PopupRow(OracleTheme.DisplayName(theme), current, theme.Id) && !current)
                        OracleTheme.Select(theme.Id);
                }
            },
        };

    private static MirageWindowFeature LanguageFeature() =>
        new()
        {
            Id = "language",
            PageId = MirageWindowHeader.LanguagePageId,
            Icon = FontAwesomeIcon.Language,
            Tooltip = MirageUi.I18n.Get("window.language"),
            OpenPageLabel = I18n.Get("language.settings"),
            ShowOnTitle = true,
            TitleKind = MirageWindowFeatureKind.Shortcut,
            TitleOpensPage = true,
            TitleOrder = 2,
            ShowOnRail = true,
            RailWhen = MirageWindowRailWhen.Both,
            RailKind = MirageWindowFeatureKind.OpenPage,
            RailOrder = 0,
            PopupTextWidth = () =>
            {
                var english = ImGui.CalcTextSize(I18n.Get("language.en")).X;
                var japanese = ImGui.CalcTextSize(I18n.Get("language.ja")).X;
                return MathF.Max(english, japanese);
            },
            DrawPopup = _ =>
            {
                if (MirageWindowHeader.PopupRow(I18n.Get("language.en"), !MirageUi.I18n.IsJapanese, "en"))
                    I18n.SetLanguage("en");
                if (MirageWindowHeader.PopupRow(I18n.Get("language.ja"), MirageUi.I18n.IsJapanese, "ja"))
                    I18n.SetLanguage("ja");
            },
        };
}
