using Oracle.Models;
using Oracle.Services;

namespace Oracle.UI;

internal sealed class CueOverlayWindow : Window
{
    private readonly TimelineEngine _engine;

    private const float CueRowWidth = 280f;
    private const float CueRowHeight = 30f;
    private const float CueRowGap = 3f;

    private const ImGuiWindowFlags OverlayFlags =
        ImGuiWindowFlags.NoDecoration
        | ImGuiWindowFlags.NoSavedSettings
        | ImGuiWindowFlags.NoFocusOnAppearing
        | ImGuiWindowFlags.NoNav
        | ImGuiWindowFlags.NoDocking
        | ImGuiWindowFlags.AlwaysAutoResize
        | ImGuiWindowFlags.NoBackground;

    public CueOverlayWindow(TimelineEngine engine)
        : base("Oracle Overlay##oracleOverlay", OverlayFlags, forceMainWindow: true)
    {
        _engine = engine;
        IsOpen = true;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
    }

    public override void PreDraw()
    {
        WindowName = I18n.Get("window.overlay.title") + "##oracleOverlay";
        OverlayClickThroughUi.ApplyMousePassThrough(this, C.OverlayClickThrough);
        ImGui.SetNextWindowPos(new Vector2(C.OverlayPosX, C.OverlayPosY), ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar();
    }

    public override bool DrawConditions() => C.ShowOverlay && _engine.IsContextMatched;

    public override void Draw()
    {
        var upcoming = GetUpcomingForOverlay();
        var contentStart = ImGui.GetCursorScreenPos();
        var headerSize = DrawOverlayHeader(contentStart, out var rowsTop);
        var rowsBottom = upcoming.Count > 0
            ? DrawCueRows(upcoming, new Vector2(contentStart.X, rowsTop))
            : rowsTop;
        DrawOverlayDragHandle(contentStart, headerSize, rowsBottom);
    }

    private IReadOnlyList<UpcomingCue> GetUpcomingForOverlay()
    {
        var upcoming = _engine.GetUpcoming(C.LookaheadSeconds);
        var maxRows = Math.Clamp(C.OverlayMaxRows, 1, 50);
        if (upcoming.Count <= maxRows)
            return upcoming;
        return upcoming.Take(maxRows).ToList();
    }

    private Vector2 DrawOverlayHeader(Vector2 contentStart, out float rowsTop)
    {
        var headerText = BuildOverlayHeaderText();
        var headerSize = ImGui.CalcTextSize(headerText);
        ImGui.GetWindowDrawList().AddText(contentStart, ImGui.GetColorU32(ImGuiCol.Text), headerText);
        rowsTop = contentStart.Y + headerSize.Y + ImGui.GetStyle().ItemSpacing.Y;
        return headerSize;
    }

    private string BuildOverlayHeaderText()
    {
        var header = _engine.IsPreview
            ? I18n.Get("overlay.preview")
            : _engine.ActiveDocument?.Name ?? I18n.Get("overlay.fallback_timeline");
        var clock = _engine.IsRunning
            ? I18n.Format("overlay.seconds", _engine.ElapsedSeconds)
            : I18n.Get("overlay.stopped");
        return $"{header}  {clock}";
    }

    private static void DrawOverlayDragHandle(Vector2 contentStart, Vector2 headerSize, float rowsBottom)
    {
        var hitWidth = Math.Max(CueRowWidth, headerSize.X);
        var hitHeight = Math.Max(headerSize.Y, rowsBottom - contentStart.Y);
        ImGui.SetCursorScreenPos(contentStart);
        ImGui.InvisibleButton("##oracleOverlayDrag", new Vector2(hitWidth, hitHeight));
        OverlayClickThroughUi.Handle(
            () => new Vector2(C.OverlayPosX, C.OverlayPosY),
            pos =>
            {
                C.OverlayPosX = pos.X;
                C.OverlayPosY = pos.Y;
            },
            C.OverlayClickThrough);
    }

    /// <returns>Bottom Y of the last cue row.</returns>
    private static float DrawCueRows(IReadOnlyList<UpcomingCue> upcoming, Vector2 origin)
    {
        var drawList = ImGui.GetWindowDrawList();
        var y = origin.Y;
        var blinkPhaseOn = (DateTime.UtcNow.Millisecond / 250) % 2 == 0;

        foreach (var item in upcoming)
        {
            var label = item.Cue.Kind == TimelineCueKind.Memo
                ? (string.IsNullOrWhiteSpace(item.Cue.Label) ? I18n.Get("overlay.memo_fallback") : item.Cue.Label)
                : ActionLookup.GetName(item.Cue.ActionId);

            var remain = item.RemainingSeconds;
            var highlighting = item.IsHighlighting;
            var isPost = item.IsPostHighlight;
            var lineColorVec = isPost ? C.ActionHighlightAfterLineColor : C.ActionHighlightBeforeLineColor;
            var lineThickness = Math.Max(
                1f,
                isPost ? C.ActionHighlightAfterLineThickness : C.ActionHighlightBeforeLineThickness);
            var blink = isPost ? C.ActionHighlightAfterBlink : C.ActionHighlightBeforeBlink;
            var showLine = highlighting && (!blink || blinkPhaseOn);
            var color = highlighting
                ? lineColorVec
                : new Vector4(0.92f, 0.92f, 0.92f, 1f);
            var lineColor = ImGui.ColorConvertFloat4ToU32(lineColorVec);

            var rowMin = new Vector2(origin.X, y);
            var rowMax = new Vector2(origin.X + CueRowWidth, y + CueRowHeight);

            drawList.AddRectFilled(rowMin, rowMax, ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.5f)), 4f);

            if (showLine)
            {
                drawList.AddRect(
                    rowMin,
                    rowMax,
                    lineColor,
                    4f,
                    ImDrawFlags.None,
                    lineThickness);
            }

            var textX = origin.X + 8f;
            if (item.Cue.Kind == TimelineCueKind.Action)
            {
                var icon = ActionLookup.GetIconWrap(item.Cue.ActionId);
                if (icon != null)
                {
                    var iconSize = 24f;
                    var iconPos = new Vector2(origin.X + 4f, y + 3f);
                    drawList.AddImage(icon.Handle, iconPos, iconPos + new Vector2(iconSize, iconSize));

                    if (showLine)
                    {
                        drawList.AddRect(
                            iconPos,
                            iconPos + new Vector2(iconSize, iconSize),
                            lineColor,
                            2f,
                            ImDrawFlags.None,
                            Math.Max(1f, lineThickness * 0.85f));
                    }

                    textX = origin.X + 34f;
                }
            }

            var text = item.IsPostHighlight
                ? I18n.Format(
                    "overlay.cue_row_now",
                    I18n.Get("overlay.now"),
                    label,
                    item.HighlightRemainingSec)
                : I18n.Format("overlay.cue_row", remain, label);
            drawList.AddText(
                new Vector2(textX, y + 6f),
                ImGui.ColorConvertFloat4ToU32(color),
                text);

            y += CueRowHeight + CueRowGap;
        }

        return upcoming.Count == 0 ? origin.Y : y - CueRowGap;
    }
}
