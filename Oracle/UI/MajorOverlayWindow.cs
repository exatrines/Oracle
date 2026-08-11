using Oracle.Models;
using Oracle.Services;

namespace Oracle.UI;

/// <summary>Horizontal "Major" timeline overlay: icons on a seconds axis with a fixed 0s (now) line.</summary>
internal sealed class MajorOverlayWindow : Window
{
    private readonly TimelineEngine _engine;
    private bool _pushedWindowPadding;

    private const float VerticalPadding = 10f;
    private const float LaneGap = 4f;

    private const ImGuiWindowFlags OverlayFlags =
        ImGuiWindowFlags.NoDecoration
        | ImGuiWindowFlags.NoSavedSettings
        | ImGuiWindowFlags.NoFocusOnAppearing
        | ImGuiWindowFlags.NoNav
        | ImGuiWindowFlags.NoDocking
        | ImGuiWindowFlags.NoScrollbar
        | ImGuiWindowFlags.NoScrollWithMouse
        | ImGuiWindowFlags.NoResize
        | ImGuiWindowFlags.NoBackground;

    public MajorOverlayWindow(TimelineEngine engine)
        : base("Oracle Major Overlay##oracleMajorOverlay", OverlayFlags, forceMainWindow: true)
    {
        _engine = engine;
        IsOpen = true;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
    }

    public override bool DrawConditions() => C.ShowMajorOverlay && _engine.IsContextMatched;

    public override void PreDraw()
    {
        WindowName = I18n.Get("window.major_overlay.title") + "##oracleMajorOverlay";
        OverlayClickThroughUi.ApplyMousePassThrough(this, C.MajorOverlayClickThrough);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        _pushedWindowPadding = true;

        ResolveMajorAxis(out _, out _, out _, out _, out var width);
        var iconSize = Math.Clamp(C.MajorIconSize, 12f, 96f);
        Size = new Vector2(width, ComputeWindowHeight(iconSize, ResolveLaneCount()));
        SizeCondition = ImGuiCond.Always;
        ImGui.SetNextWindowPos(new Vector2(C.MajorOverlayPosX, C.MajorOverlayPosY), ImGuiCond.Always);
        ImGui.SetNextWindowSize(Size.Value, ImGuiCond.Always);
    }

    public override void PostDraw()
    {
        if (!_pushedWindowPadding)
            return;

        ImGui.PopStyleVar();
        _pushedWindowPadding = false;
    }

    public override void Draw()
    {
        ResolveMajorAxis(out var pps, out var pastSeconds, out var futureSeconds, out var zeroX, out var width);
        var iconSize = Math.Clamp(C.MajorIconSize, 12f, 96f);
        var laneCount = ResolveLaneCount();
        var upcoming = CollectUpcomingOnAxis(pastSeconds, futureSeconds);
        var layout = ComputeBarLayout(zeroX, iconSize, width, laneCount);

        Size = new Vector2(width, layout.WindowHeight);
        SizeCondition = ImGuiCond.Always;

        var origin = ImGui.GetWindowPos();
        var drawList = ImGui.GetWindowDrawList();

        DrawMajorTitleBand(drawList, origin);
        drawList.AddRectFilled(
            layout.BarMin,
            layout.BarMax,
            ImGui.ColorConvertFloat4ToU32(C.MajorBackgroundColor),
            6f);

        DrawMajorSecondGrid(
            drawList,
            layout.BarMin,
            layout.BarMax,
            layout.AreaTop,
            layout.TrackBottom,
            zeroX,
            pps,
            pastSeconds,
            futureSeconds);

        DrawMajorCueIcons(
            drawList,
            upcoming,
            layout,
            pps,
            iconSize,
            laneCount);

        DrawMajorDragHandle(origin, width, layout.WindowHeight);
    }

    /// <summary>After = left of 0s (past); Before = right of 0s (future).</summary>
    private List<UpcomingCue> CollectUpcomingOnAxis(float pastSeconds, float futureSeconds)
    {
        var fetchLookahead = Math.Max(C.LookaheadSeconds, futureSeconds + 0.5f);
        return _engine.GetUpcoming(fetchLookahead)
            .Where(u =>
            {
                var axisSec = u.RemainingSeconds;
                return axisSec >= -pastSeconds - 0.05f && axisSec <= futureSeconds + 0.05f;
            })
            .OrderBy(u => u.RemainingSeconds)
            .ToList();
    }

    private readonly record struct MajorBarLayout(
        float WindowHeight,
        Vector2 BarMin,
        Vector2 BarMax,
        float AreaTop,
        float TrackBottom,
        float ZeroLineX,
        float AbilityCenterY,
        float SkillCenterY);

    private static MajorBarLayout ComputeBarLayout(
        float zeroX,
        float iconSize,
        float width,
        int laneCount)
    {
        var vPad = VerticalPadding;
        var titleBand = TitleBandHeight();
        var barHeaderH = BarHeaderHeight();
        var lanesH = LaneBlockHeight(iconSize, laneCount);
        var barHeight = barHeaderH + vPad + lanesH + vPad;
        var origin = ImGui.GetWindowPos();
        var barMin = origin + new Vector2(0f, titleBand);
        var barMax = barMin + new Vector2(width, barHeight);
        var areaTop = barMin.Y + barHeaderH;
        var stackTop = areaTop + vPad;
        var trackBottom = barMax.Y - vPad;

        float abilityCenterY;
        float skillCenterY;
        if (laneCount >= 2)
        {
            abilityCenterY = stackTop + iconSize * 0.5f;
            skillCenterY = stackTop + iconSize + LaneGap + iconSize * 0.5f;
        }
        else
        {
            abilityCenterY = skillCenterY = stackTop + iconSize * 0.5f;
        }

        return new MajorBarLayout(
            titleBand + barHeight,
            barMin,
            barMax,
            areaTop,
            trackBottom,
            barMin.X + zeroX,
            abilityCenterY,
            skillCenterY);
    }

    private void DrawMajorTitleBand(ImDrawListPtr drawList, Vector2 origin)
    {
        if (!C.MajorShowTitle)
            return;

        var headerText = BuildHeaderText();
        if (string.IsNullOrEmpty(headerText))
            return;

        drawList.AddText(
            origin + new Vector2(8f, 0f),
            ImGui.ColorConvertFloat4ToU32(C.MajorLabelColor),
            headerText);
    }

    private static void DrawMajorSecondGrid(
        ImDrawListPtr drawList,
        Vector2 barMin,
        Vector2 barMax,
        float areaTop,
        float trackBottom,
        float zeroX,
        float pps,
        float pastSeconds,
        float futureSeconds)
    {
        var gridColor = ImGui.ColorConvertFloat4ToU32(C.MajorGridLineColor);
        var labelColor = C.MajorLabelColor;
        var zeroLineX = barMin.X + zeroX;

        var firstSec = (int)Math.Floor(-pastSeconds);
        var lastSec = (int)Math.Ceiling(futureSeconds);
        for (var sec = firstSec; sec <= lastSec; sec++)
        {
            var x = zeroLineX + sec * pps;
            if (x < barMin.X - 1f || x > barMax.X + 1f)
                continue;

            if (C.MajorShowGrid && sec != 0)
            {
                drawList.AddLine(
                    new Vector2(x, areaTop),
                    new Vector2(x, trackBottom),
                    gridColor,
                    1f);
            }

            if (C.MajorShowSecondLabels)
            {
                var text = I18n.Format("overlay.seconds_int", sec);
                var textSize = ImGui.CalcTextSize(text);
                var textPos = new Vector2(x - textSize.X * 0.5f, barMin.Y + 4f);
                if (textPos.X < barMin.X + 4f)
                    continue;
                if (textPos.X + textSize.X > barMax.X - 4f)
                    continue;
                drawList.AddText(textPos, ImGui.ColorConvertFloat4ToU32(labelColor), text);
            }
        }

        var zeroThickness = Math.Max(1f, C.MajorZeroLineThickness);
        drawList.AddLine(
            new Vector2(zeroLineX, areaTop),
            new Vector2(zeroLineX, trackBottom),
            ImGui.ColorConvertFloat4ToU32(C.MajorZeroLineColor),
            zeroThickness);
    }

    private static void DrawMajorCueIcons(
        ImDrawListPtr drawList,
        IReadOnlyList<UpcomingCue> upcoming,
        MajorBarLayout layout,
        float pps,
        float iconSize,
        int laneCount)
    {
        var blinkPhaseOn = (DateTime.UtcNow.Millisecond / 250) % 2 == 0;
        var twoLane = laneCount >= 2;

        // Ascending RemainingSeconds: later cues paint on top when X overlaps.
        foreach (var item in upcoming)
        {
            var centerX = layout.ZeroLineX + item.RemainingSeconds * pps;
            if (centerX < layout.BarMin.X - iconSize || centerX > layout.BarMax.X + iconSize)
                continue;

            var abilityLane = !twoLane || ActionLookup.IsMajorAbilityLane(item.Cue);
            var centerY = abilityLane ? layout.AbilityCenterY : layout.SkillCenterY;

            var iconMin = new Vector2(centerX - iconSize * 0.5f, centerY - iconSize * 0.5f);
            var iconMax = iconMin + new Vector2(iconSize, iconSize);

            var castSec = ActionTiming.GetCastSeconds(item.Cue);
            var recastSec = ActionTiming.GetRecastSeconds(item.Cue);
            if (recastSec > 0f)
            {
                var recastEndX = centerX + recastSec * pps;
                drawList.AddLine(
                    new Vector2(centerX, centerY),
                    new Vector2(recastEndX, centerY),
                    ImGui.ColorConvertFloat4ToU32(C.MajorGridLineColor),
                    Math.Max(1f, iconSize * 0.08f));
            }

            if (castSec > 0f)
            {
                var castEndX = centerX + castSec * pps;
                drawList.AddLine(
                    new Vector2(centerX, centerY),
                    new Vector2(castEndX, centerY),
                    ImGui.ColorConvertFloat4ToU32(C.MajorLabelColor),
                    Math.Max(1.5f, iconSize * 0.14f));
            }

            var highlighting = item.IsHighlighting;
            var isPost = item.IsPostHighlight;
            var lineColorVec = isPost ? C.ActionHighlightAfterLineColor : C.ActionHighlightBeforeLineColor;
            var lineThickness = Math.Max(
                1f,
                isPost ? C.ActionHighlightAfterLineThickness : C.ActionHighlightBeforeLineThickness);
            var blink = isPost ? C.ActionHighlightAfterBlink : C.ActionHighlightBeforeBlink;
            var showLine = highlighting && (!blink || blinkPhaseOn);

            if (item.Cue.Kind == TimelineCueKind.Action)
            {
                var icon = ActionLookup.GetIconWrap(item.Cue.ActionId);
                if (icon != null)
                    drawList.AddImage(icon.Handle, iconMin, iconMax);
                else
                    drawList.AddRectFilled(iconMin, iconMax, ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 0.2f, 0.2f, 0.9f)), 3f);
            }
            else
            {
                drawList.AddRectFilled(iconMin, iconMax, ImGui.ColorConvertFloat4ToU32(new Vector4(0.15f, 0.25f, 0.4f, 0.95f)), 3f);
                var memo = string.IsNullOrWhiteSpace(item.Cue.Label)
                    ? I18n.Get("overlay.memo_abbrev")
                    : item.Cue.Label;
                if (memo.Length > 4)
                    memo = memo[..4];
                var memoSize = ImGui.CalcTextSize(memo);
                drawList.AddText(
                    iconMin + new Vector2((iconSize - memoSize.X) * 0.5f, (iconSize - memoSize.Y) * 0.5f),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 1f, 1f)),
                    memo);
            }

            if (showLine)
            {
                drawList.AddRect(
                    iconMin,
                    iconMax,
                    ImGui.ColorConvertFloat4ToU32(lineColorVec),
                    3f,
                    ImDrawFlags.None,
                    lineThickness);
            }
        }
    }

    private static void DrawMajorDragHandle(Vector2 origin, float width, float height)
    {
        ImGui.SetCursorScreenPos(origin);
        ImGui.InvisibleButton("##oracleMajorDrag", new Vector2(width, height));
        OverlayClickThroughUi.Handle(
            () => new Vector2(C.MajorOverlayPosX, C.MajorOverlayPosY),
            pos =>
            {
                C.MajorOverlayPosX = pos.X;
                C.MajorOverlayPosY = pos.Y;
            },
            C.MajorOverlayClickThrough);
    }

    private string BuildHeaderText()
    {
        var name = _engine.IsPreview
            ? I18n.Get("overlay.preview")
            : _engine.ActiveDocument?.Name ?? I18n.Get("overlay.fallback_timeline");
        var clock = _engine.IsRunning
            ? I18n.Format("overlay.seconds", _engine.ElapsedSeconds)
            : I18n.Get("overlay.stopped");
        return $"{name}  {clock}";
    }

    private static float TitleBandHeight()
    {
        if (!C.MajorShowTitle)
            return 0f;
        return ImGui.GetTextLineHeight() + 4f;
    }

    /// <summary>Space inside the bar for second labels (title is outside above the bar).</summary>
    private static float BarHeaderHeight() =>
        C.MajorShowSecondLabels ? 18f : 4f;

    private static int ResolveLaneCount() =>
        C.MajorLaneMode == MajorOverlayLaneMode.AbilityAndSkill ? 2 : 1;

    private static float LaneBlockHeight(float iconSize, int laneCount) =>
        laneCount >= 2
            ? iconSize * 2f + LaneGap
            : iconSize;

    /// <summary>
    /// Layout from Major Before (right / future) and After (left / past) ÁEpixels per second.
    /// </summary>
    private static void ResolveMajorAxis(
        out float pps,
        out float pastSeconds,
        out float futureSeconds,
        out float zeroX,
        out float width)
    {
        pps = Math.Max(4f, C.MajorPixelsPerSecond);
        pastSeconds = Math.Max(0f, C.MajorAfterSeconds);
        futureSeconds = Math.Max(0f, C.MajorBeforeSeconds);
        if (pastSeconds <= 0f && futureSeconds <= 0f)
        {
            pastSeconds = 1f;
            futureSeconds = 1f;
        }

        zeroX = pastSeconds * pps;
        width = Math.Max(40f, (pastSeconds + futureSeconds) * pps);
    }

    private static float ComputeWindowHeight(float iconSize, int laneCount) =>
        TitleBandHeight()
        + BarHeaderHeight()
        + VerticalPadding
        + LaneBlockHeight(iconSize, laneCount)
        + VerticalPadding;
}
