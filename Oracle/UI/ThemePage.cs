using Dalamud.Interface;
using Dalamud.Interface.Utility;

namespace Oracle.UI;

internal static class ThemePage
{
    private static bool _confirmDelete;

    public static void Draw()
    {
        var scale = ImGuiHelpers.GlobalScale;
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(16f, 12f) * scale))
        using (var child = ImRaii.Child(
            "##ThemePage"u8,
            ImGui.GetContentRegionAvail(),
            false,
            ImGuiWindowFlags.AlwaysUseWindowPadding))
        {
            if (child)
                DrawBody();
        }

        MirageUi.Message.Draw(
            ref _confirmDelete,
            I18n.Get("theme.delete.confirm"),
            OracleTheme.Selected.Name,
            FontAwesomeIcon.ExclamationTriangle,
            new MirageMessageButton { Label = I18n.Get("theme.cancel") },
            new MirageMessageButton
            {
                Label = I18n.Get("theme.delete"),
                Primary = true,
                OnClick = OracleTheme.DeleteSelected,
            });
    }

    private static void DrawBody()
    {
        MirageUi.Text(I18n.Get("theme.title"), MirageUi.Color.Title, fontSize: MirageUi.FontSize.Large);
        DrawToolbar(ImGuiHelpers.GlobalScale);

        var locked = OracleTheme.IsBuiltin(C.SelectedThemeId);
        if (locked)
            ImGui.BeginDisabled();

        var theme = OracleTheme.Current;
        EditColor(I18n.Get("theme.main"), theme.GetColor(MirageUi.Color.Title), false, value =>
        {
            value.W = 1f;
            theme.SetColor(MirageUi.Color.Title, value);
        });
        EditColor(I18n.Get("theme.accent"), theme.GetColor(MirageUi.Color.Accent), false, theme.ApplyAccent);
        EditColor(I18n.Get("theme.text"), theme.GetColor(MirageUi.Color.Default), false, value =>
        {
            value.W = 1f;
            theme.SetColor(MirageUi.Color.Default, value);
        });
        EditColor(I18n.Get("theme.background"), theme.WindowBg, true, value =>
        {
            theme.WindowBg = value;
            theme.TitleBg = value;
        });
        EditColor(I18n.Get("theme.panels"), theme.ChromeBg, true, value => theme.ChromeBg = value);
        EditColor(I18n.Get("theme.page"), theme.PageBg, true, value => theme.PageBg = value);
        EditColor(I18n.Get("theme.separator"), theme.Separator, true, value => theme.Separator = value);

        var blur = theme.BlurStrength;
        if (MirageUi.SliderFloat(I18n.Get("theme.blur"), ref blur, 0f, MirageColorSettings.MaxBlurStrength, "%.0f"))
        {
            theme.BlurStrength = blur;
            OracleTheme.SaveCurrent();
        }

        var size = theme.UiFontSizePt;
        if (MirageUi.SliderFloat(I18n.Get("theme.size"), ref size, 10f, 18f, "%.0f pt"))
        {
            theme.UiFontSizePt = size;
            OracleTheme.SaveCurrent();
        }

        if (locked)
            ImGui.EndDisabled();
    }

    private static void EditColor(string label, Vector4 color, bool alpha, Action<Vector4> apply)
    {
        var flags = alpha ? ImGuiColorEditFlags.AlphaBar : ImGuiColorEditFlags.NoAlpha;
        if (!MirageUi.ColorEdit4(label, ref color, flags))
            return;

        apply(color);
        OracleTheme.SaveCurrent();
    }

    private static void DrawToolbar(float scale)
    {
        var gap = ImGui.GetStyle().ItemSpacing.X;
        var button = MirageUi.ResolveControlHeight();
        var trailing = button * 5f + gap * 6f + 1f;
        var dropdownWidth = MathF.Max(120f * scale, ImGui.GetContentRegionAvail().X - trailing);
        var selected = OracleTheme.Selected;
        if (MirageUi.BeginDropdown("", OracleTheme.DisplayName(selected), "themePick", dropdownWidth))
        {
            foreach (var theme in C.Themes)
            {
                var current = theme.Id == C.SelectedThemeId;
                if (ImGui.Selectable($"{OracleTheme.DisplayName(theme)}##{theme.Id}", current) && !current)
                    OracleTheme.Select(theme.Id);
                if (current)
                    ImGui.SetItemDefaultFocus();
            }

            MirageUi.EndDropdown();
        }

        ImGui.SameLine(0f, gap);
        if (MirageUi.IconButton(FontAwesomeIcon.Plus, id: "themeAdd", tooltip: I18n.Get("theme.add")))
            OracleTheme.Add();

        ImGui.SameLine(0f, gap);
        if (MirageUi.IconButton(FontAwesomeIcon.Copy, id: "themeDuplicate", tooltip: I18n.Get("theme.duplicate")))
            OracleTheme.DuplicateSelected();

        ImGui.SameLine(0f, gap);
        var builtin = OracleTheme.IsBuiltin(C.SelectedThemeId);
        if (MirageUi.IconButton(
                FontAwesomeIcon.Trash,
                id: "themeDelete",
                tooltip: I18n.Get(builtin ? "theme.delete.builtin" : "theme.delete"),
                enabled: !builtin))
            _confirmDelete = true;

        ImGui.SameLine(0f, gap);
        var lineTop = ImGui.GetCursorScreenPos();
        var inset = 4f * scale;
        ImGui.GetWindowDrawList().AddLine(
            lineTop + new Vector2(0f, inset),
            lineTop + new Vector2(0f, button - inset),
            ImGui.GetColorU32(ImGuiCol.Separator));
        ImGui.Dummy(new Vector2(1f, button));

        ImGui.SameLine(0f, gap);
        if (MirageUi.IconButton(FontAwesomeIcon.FileExport, id: "themeExport", tooltip: I18n.Get("theme.export")))
            OracleTheme.ExportSelected();

        ImGui.SameLine(0f, gap);
        if (MirageUi.IconButton(FontAwesomeIcon.FileImport, id: "themeImport", tooltip: I18n.Get("theme.import")))
            OracleTheme.ImportClipboard();
    }
}
