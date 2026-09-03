using Oracle.Models;

namespace Oracle.UI;

// --- Timeline / Major / Hotbar / Action Highlight ---

internal sealed partial class PluginSettingsWindow
{
    // --- Timeline ---

    private static void DrawTimelineSettings()
    {
        MirageUi.Header(I18n.Get("settings.header.timeline"));

        var showOverlay = C.ShowOverlay;
        if (MirageUi.Checkbox(I18n.Get("settings.checkbox.enable"), ref showOverlay))
        {
            C.ShowOverlay = showOverlay;
            C.Save();
        }

        var clickThrough = C.OverlayClickThrough;
        if (MirageUi.Checkbox(I18n.Get("settings.checkbox.click_through"), ref clickThrough))
        {
            C.OverlayClickThrough = clickThrough;
            C.Save();
        }

        MirageUi.Text(I18n.Get("settings.help.click_through_alt"), MirageUi.Color.Secondary);

        var lookahead = C.LookaheadSeconds;
        if (MirageUi.SliderFloat(I18n.Get("settings.slider.lookahead"), ref lookahead, 5f, 60f, "%.0f"))
        {
            C.LookaheadSeconds = lookahead;
            C.Save();
        }

        MirageUi.Text(I18n.Get("settings.help.lookahead"), MirageUi.Color.Secondary);

        var maxRows = C.OverlayMaxRows;
        if (MirageUi.SliderInt(I18n.Get("settings.slider.max_rows"), ref maxRows, 1, 30))
        {
            C.OverlayMaxRows = Math.Clamp(maxRows, 1, 50);
            C.Save();
        }
    }

    // --- Major ---

    private static void DrawMajorOverlaySettings()
    {
        MirageUi.Header(I18n.Get("settings.header.major"));

        var show = C.ShowMajorOverlay;
        if (MirageUi.Checkbox(I18n.Get("settings.checkbox.enable"), ref show))
        {
            C.ShowMajorOverlay = show;
            C.Save();
        }

        var clickThrough = C.MajorOverlayClickThrough;
        if (MirageUi.Checkbox(I18n.Get("settings.checkbox.click_through"), ref clickThrough))
        {
            C.MajorOverlayClickThrough = clickThrough;
            C.Save();
        }

        MirageUi.Text(I18n.Get("settings.help.click_through_alt"), MirageUi.Color.Secondary);

        MirageUi.SubHeader(I18n.Get("settings.subheader.layout"));
        MirageUi.Text(
            I18n.Get("settings.major.help.visible_range"),
            MirageUi.Color.Secondary);

        var laneSingle = I18n.Get("settings.major.lane.single");
        var laneAbilitySkill = I18n.Get("settings.major.lane.ability_skill");
        var laneSelected = C.MajorLaneMode == MajorOverlayLaneMode.AbilityAndSkill
            ? laneAbilitySkill
            : laneSingle;
        if (MirageUi.Dropdown(
                I18n.Get("settings.major.lane_mode"),
                ref laneSelected,
                [laneSingle, laneAbilitySkill],
                allowClear: false,
                id: "MajorLaneMode"))
        {
            C.MajorLaneMode = string.Equals(laneSelected, laneAbilitySkill, StringComparison.Ordinal)
                ? MajorOverlayLaneMode.AbilityAndSkill
                : MajorOverlayLaneMode.Single;
            C.Save();
        }

        MirageUi.Text(I18n.Get("settings.major.help.lane"), MirageUi.Color.Secondary);

        var before = C.MajorBeforeSeconds;
        if (MirageUi.SliderFloat(
                I18n.Get("settings.slider.before_sec"),
                ref before,
                0f,
                30f,
                "%.1f",
                id: "MajorLayoutBefore"))
        {
            C.MajorBeforeSeconds = Math.Clamp(before, 0f, 60f);
            C.Save();
        }

        var after = C.MajorAfterSeconds;
        if (MirageUi.SliderFloat(
                I18n.Get("settings.slider.after_sec"),
                ref after,
                0f,
                30f,
                "%.1f",
                id: "MajorLayoutAfter"))
        {
            C.MajorAfterSeconds = Math.Clamp(after, 0f, 60f);
            C.Save();
        }

        var pps = C.MajorPixelsPerSecond;
        if (MirageUi.SliderFloat(I18n.Get("settings.slider.width_per_sec"), ref pps, 10f, 120f, "%.0f"))
        {
            C.MajorPixelsPerSecond = Math.Clamp(pps, 4f, 200f);
            C.Save();
        }

        var iconSize = C.MajorIconSize;
        if (MirageUi.SliderFloat(I18n.Get("settings.slider.icon_size"), ref iconSize, 16f, 64f, "%.0f"))
        {
            C.MajorIconSize = Math.Clamp(iconSize, 12f, 96f);
            C.Save();
        }

        MirageUi.Text(
            I18n.Format(
                "settings.major.visible_range_live",
                Math.Max(0f, C.MajorAfterSeconds),
                Math.Max(0f, C.MajorBeforeSeconds)),
            MirageUi.Color.Secondary);

        MirageUi.SubHeader(I18n.Get("settings.subheader.display"));

        var showTitle = C.MajorShowTitle;
        if (MirageUi.Checkbox(I18n.Get("settings.checkbox.show_title"), ref showTitle))
        {
            C.MajorShowTitle = showTitle;
            C.Save();
        }

        var showLabels = C.MajorShowSecondLabels;
        if (MirageUi.Checkbox(I18n.Get("settings.checkbox.show_second_labels"), ref showLabels))
        {
            C.MajorShowSecondLabels = showLabels;
            C.Save();
        }

        var showGrid = C.MajorShowGrid;
        if (MirageUi.Checkbox(I18n.Get("settings.checkbox.show_grid"), ref showGrid))
        {
            C.MajorShowGrid = showGrid;
            C.Save();
        }

        MirageUi.SubHeader(I18n.Get("settings.subheader.colors"));

        var bg = C.MajorBackgroundColor;
        if (MirageUi.ColorEdit4(I18n.Get("settings.color.background"), ref bg))
        {
            C.MajorBackgroundColor = bg;
            C.Save();
        }

        var grid = C.MajorGridLineColor;
        if (MirageUi.ColorEdit4(I18n.Get("settings.color.grid_line"), ref grid))
        {
            C.MajorGridLineColor = grid;
            C.Save();
        }

        var zeroColor = C.MajorZeroLineColor;
        if (MirageUi.ColorEdit4(I18n.Get("settings.color.zero_line"), ref zeroColor))
        {
            C.MajorZeroLineColor = zeroColor;
            C.Save();
        }

        var zeroThickness = C.MajorZeroLineThickness;
        if (MirageUi.SliderFloat(I18n.Get("settings.slider.zero_thickness"), ref zeroThickness, 1f, 8f, "%.1f"))
        {
            C.MajorZeroLineThickness = Math.Clamp(zeroThickness, 1f, 8f);
            C.Save();
        }

        var labelColor = C.MajorLabelColor;
        if (MirageUi.ColorEdit4(I18n.Get("settings.color.label"), ref labelColor))
        {
            C.MajorLabelColor = labelColor;
            C.Save();
        }
    }

    // --- Action Highlight ---

    private static void DrawActionHighlightSettings()
    {
        MirageUi.Header(I18n.Get("settings.header.action_highlight"));

        var completeWindow = C.ActionCompleteWindowSeconds;
        if (MirageUi.SliderFloat(
                I18n.Get("settings.slider.complete_window"),
                ref completeWindow,
                5f,
                60f,
                "%.0f"))
        {
            C.ActionCompleteWindowSeconds = Math.Clamp(completeWindow, 5f, 60f);
            C.Save();
        }

        MirageUi.Text(I18n.Get("settings.help.complete_window"), MirageUi.Color.Secondary);

        DrawHighlightPhaseSettings(
            I18n.Get("settings.subheader.before"),
            idSuffix: "ActionHighlightBefore",
            getSeconds: () => C.ActionHighlightBeforeSeconds,
            setSeconds: v => C.ActionHighlightBeforeSeconds = v,
            getThickness: () => C.ActionHighlightBeforeLineThickness,
            setThickness: v => C.ActionHighlightBeforeLineThickness = v,
            getColor: () => C.ActionHighlightBeforeLineColor,
            setColor: v => C.ActionHighlightBeforeLineColor = v,
            getBlink: () => C.ActionHighlightBeforeBlink,
            setBlink: v => C.ActionHighlightBeforeBlink = v,
            minSeconds: 0f,
            maxSeconds: 30f);

        DrawHighlightPhaseSettings(
            I18n.Get("settings.subheader.after"),
            idSuffix: "ActionHighlightAfter",
            getSeconds: () => C.ActionHighlightAfterSeconds,
            setSeconds: v => C.ActionHighlightAfterSeconds = v,
            getThickness: () => C.ActionHighlightAfterLineThickness,
            setThickness: v => C.ActionHighlightAfterLineThickness = v,
            getColor: () => C.ActionHighlightAfterLineColor,
            setColor: v => C.ActionHighlightAfterLineColor = v,
            getBlink: () => C.ActionHighlightAfterBlink,
            setBlink: v => C.ActionHighlightAfterBlink = v,
            minSeconds: 0f,
            maxSeconds: 30f);
    }

    // --- Hotbar ---

    private static void DrawHotbarSettings()
    {
        MirageUi.Header(I18n.Get("settings.header.hotbar"));

        var showHotbarHighlight = C.ShowHotbarHighlight;
        if (MirageUi.Checkbox(I18n.Get("settings.checkbox.enable"), ref showHotbarHighlight))
        {
            C.ShowHotbarHighlight = showHotbarHighlight;
            C.Save();
        }

        C.EnsureHotbarHighlightDefaults();

        MirageUi.Text(
            I18n.Get("settings.hotbar.help.select"),
            MirageUi.Color.Secondary);

        MirageUi.Text(I18n.Get("settings.label.hotbars"), wrap: false);
        DrawHotbarIdCheckboxRow(startId: 0, count: 10, formatKey: "settings.hotbar.hb");

        ImGui.Spacing();
        MirageUi.Text(I18n.Get("settings.label.cross_hotbars"), wrap: false);
        DrawHotbarIdCheckboxRow(startId: 10, count: 8, formatKey: "settings.hotbar.xhb");

        ImGui.Spacing();
        var doubleCross = C.ShowHotbarHighlightDoubleCross;
        if (MirageUi.Checkbox(I18n.Get("settings.checkbox.double_cross"), ref doubleCross))
        {
            C.ShowHotbarHighlightDoubleCross = doubleCross;
            C.Save();
        }
    }

    // --- Helpers ---

    private static void DrawHighlightPhaseSettings(
        string header,
        string idSuffix,
        Func<float> getSeconds,
        Action<float> setSeconds,
        Func<float> getThickness,
        Action<float> setThickness,
        Func<Vector4> getColor,
        Action<Vector4> setColor,
        Func<bool> getBlink,
        Action<bool> setBlink,
        float minSeconds,
        float maxSeconds)
    {
        MirageUi.SubHeader(header);

        var seconds = getSeconds();
        if (MirageUi.SliderFloat(
                I18n.Get("settings.slider.duration_sec"),
                ref seconds,
                minSeconds,
                maxSeconds,
                "%.1f",
                id: $"{idSuffix}_duration"))
        {
            setSeconds(Math.Clamp(seconds, minSeconds, maxSeconds));
            C.Save();
        }

        var thickness = getThickness();
        if (MirageUi.SliderFloat(
                I18n.Get("settings.slider.line_thickness"),
                ref thickness,
                1f,
                12f,
                "%.1f",
                id: $"{idSuffix}_thickness"))
        {
            setThickness(Math.Clamp(thickness, 1f, 12f));
            C.Save();
        }

        var lineColor = getColor();
        if (MirageUi.ColorEdit4(
                I18n.Get("settings.color.line"),
                ref lineColor,
                id: $"{idSuffix}_color"))
        {
            setColor(lineColor);
            C.Save();
        }

        var blink = getBlink();
        if (MirageUi.Checkbox($"{I18n.Get("settings.checkbox.blink")}##{idSuffix}", ref blink))
        {
            setBlink(blink);
            C.Save();
        }
    }

    private static void DrawHotbarIdCheckboxRow(byte startId, int count, string formatKey)
    {
        const int perRow = 5;
        for (var i = 0; i < count; i++)
        {
            if (i > 0 && i % perRow != 0)
                ImGui.SameLine();

            var hotbarId = (byte)(startId + i);
            var enabled = C.IsHotbarHighlightEnabled(hotbarId);
            var label = $"{I18n.Format(formatKey, i + 1)}##hotbarHighlight{hotbarId}";
            if (MirageUi.Checkbox(label, ref enabled))
                C.SetHotbarHighlightEnabled(hotbarId, enabled);
        }
    }
}
