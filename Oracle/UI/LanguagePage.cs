using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Oracle.UI;

internal static class LanguagePage
{
    public static void Draw()
    {
        var scale = ImGuiHelpers.GlobalScale;
        using var padding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(16f, 12f) * scale);
        using var child = ImRaii.Child(
            "##LanguagePage"u8,
            ImGui.GetContentRegionAvail(),
            false,
            ImGuiWindowFlags.AlwaysUseWindowPadding);
        if (!child)
            return;

        MirageUi.Text(I18n.Get("language.title"), MirageUi.Color.Title, fontSize: MirageUi.FontSize.Large);
        var labels = new[] { I18n.Get("language.en"), I18n.Get("language.ja") };
        var selected = MirageUi.I18n.IsJapanese ? labels[1] : labels[0];
        if (!MirageUi.Dropdown(I18n.Get("language.title"), ref selected, labels, id: "uiLanguage", allowClear: false))
            return;

        I18n.SetLanguage(selected == labels[1] ? "ja" : "en");
    }
}
