using Oracle.Models;
using Oracle.Services;

namespace Oracle.UI;

internal sealed class CueOverlayWindow : Window
{
    private readonly TimelineEngine _engine;

    private const float CueRowGap = 3f;
    private const float CueRowIconPadX = 4f;
    private const float CueRowIconGap = 6f;
    private const float CueRowTextPadX = 8f;
    private const float CueRowTargetGap = 4f;
    private const float CueRowMinIconPad = 6f;
    private const float CueRowTargetIconRatio = 20f / 24f;
    private const float CueRowRounding = 4f;

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

    private static float RowWidth =>
        Math.Clamp(C.OverlayRowWidth, Configuration.MinOverlayRowWidth, Configuration.MaxOverlayRowWidth);

    private static float TextSizePx
    {
        get
        {
            var px = C.OverlayTextSizePx;
            return px > 0f
                ? Math.Clamp(px, Configuration.MinOverlayTextSizePx, Configuration.MaxOverlayTextSizePx)
                : ImGui.GetFontSize();
        }
    }

    private static float IconSize
    {
        get
        {
            var font = ImGui.GetFontSize();
            if (font <= 0f)
                return Configuration.DefaultOverlayIconSize;
            return Math.Clamp(
                Configuration.DefaultOverlayIconSize * (TextSizePx / font),
                Configuration.MinOverlayIconSize,
                Configuration.MaxOverlayIconSize);
        }
    }

    private static float RowHeight =>
        Math.Max(
            Math.Clamp(C.OverlayRowHeight, Configuration.MinOverlayRowHeight, Configuration.MaxOverlayRowHeight),
            Math.Max(IconSize, TextSizePx) + CueRowMinIconPad);

    private static bool IsRtl =>
        C.OverlayRowDirection == OverlayRowDirection.RightToLeft;

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
        var upcoming = _engine.GetUpcoming(C.LookaheadSeconds)
            .Where(u => OverlayView.IsAction(u.Cue))
            .ToList();
        var maxRows = Math.Clamp(C.OverlayMaxRows, 1, 50);
        if (upcoming.Count <= maxRows)
            return upcoming;
        return upcoming.Take(maxRows).ToList();
    }

    private Vector2 DrawOverlayHeader(Vector2 contentStart, out float rowsTop)
    {
        var headerText = OverlayView.HeaderText(_engine);
        var fontSize = TextSizePx;
        var headerSize = MeasureText(headerText, fontSize);
        var headerPos = contentStart;
        if (IsRtl)
            headerPos.X += Math.Max(0f, RowWidth - headerSize.X);
        ImGui.GetWindowDrawList().AddText(
            ImGui.GetFont(),
            fontSize,
            headerPos,
            ImGui.ColorConvertFloat4ToU32(C.OverlayTextColor),
            headerText);
        rowsTop = contentStart.Y + headerSize.Y + ImGui.GetStyle().ItemSpacing.Y;
        return headerSize;
    }

    private static void DrawOverlayDragHandle(Vector2 contentStart, Vector2 headerSize, float rowsBottom)
    {
        var hitWidth = Math.Max(RowWidth, headerSize.X);
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

    private static float DrawCueRows(IReadOnlyList<UpcomingCue> upcoming, Vector2 origin)
    {
        var metrics = new CueRowMetrics
        {
            RowWidth = RowWidth,
            RowHeight = RowHeight,
            IconSize = IconSize,
            TargetSize = IconSize * CueRowTargetIconRatio,
            FontSize = TextSizePx,
            Font = ImGui.GetFont(),
            RowBg = ImGui.ColorConvertFloat4ToU32(C.OverlayRowBackgroundColor),
            TextColor = C.OverlayTextColor,
            Rtl = IsRtl,
        };

        var drawList = ImGui.GetWindowDrawList();
        var blinkPhaseOn = OverlayView.BlinkPhaseOn;
        var y = origin.Y;
        foreach (var item in upcoming)
        {
            DrawCueRow(drawList, item, origin.X, y, metrics, blinkPhaseOn);
            y += metrics.RowHeight + CueRowGap;
        }

        return upcoming.Count == 0 ? origin.Y : y - CueRowGap;
    }

    private static void DrawCueRow(
        ImDrawListPtr drawList,
        UpcomingCue item,
        float originX,
        float y,
        in CueRowMetrics metrics,
        bool blinkPhaseOn)
    {
        var showLine = OverlayView.TryHighlightLine(
            item,
            blinkPhaseOn,
            out var lineColorVec,
            out var lineThickness);
        var lineColor = ImGui.ColorConvertFloat4ToU32(lineColorVec);
        var textColor = ImGui.ColorConvertFloat4ToU32(item.IsHighlighting ? lineColorVec : metrics.TextColor);

        var rowMin = new Vector2(originX, y);
        var rowMax = new Vector2(originX + metrics.RowWidth, y + metrics.RowHeight);
        drawList.AddRectFilled(rowMin, rowMax, metrics.RowBg, CueRowRounding);
        if (showLine)
        {
            drawList.AddRect(
                rowMin,
                rowMax,
                lineColor,
                CueRowRounding,
                ImDrawFlags.None,
                lineThickness);
        }

        var icon = ActionLookup.GetIconWrap(item.Cue.ActionId);
        var targetIcon = CueTargetCatalog.GetIconWrap(item.Cue);
        var timeText = ResolveCueRowTimeText(item);
        var nameText = C.OverlayShowActionNames
            ? ActionLookup.GetName(item.Cue.ActionId)
            : string.Empty;
        var timeSize = MeasureText(timeText, metrics.FontSize);
        var nameSize = nameText.Length == 0 ? Vector2.Zero : MeasureText(nameText, metrics.FontSize);
        var layout = PlaceCueRow(
            originX,
            metrics,
            hasIcon: icon != null,
            hasTarget: targetIcon != null,
            timeWidth: timeSize.X,
            nameWidth: nameSize.X);

        if (icon != null)
        {
            var iconPos = new Vector2(layout.IconX, y + (metrics.RowHeight - metrics.IconSize) * 0.5f);
            var iconExtent = new Vector2(metrics.IconSize, metrics.IconSize);
            drawList.AddImage(icon.Handle, iconPos, iconPos + iconExtent);
            if (showLine)
            {
                drawList.AddRect(
                    iconPos,
                    iconPos + iconExtent,
                    lineColor,
                    2f,
                    ImDrawFlags.None,
                    Math.Max(1f, lineThickness * 0.85f));
            }
        }

        if (targetIcon != null)
        {
            var targetPos = new Vector2(
                layout.TargetX,
                y + (metrics.RowHeight - metrics.TargetSize) * 0.5f);
            drawList.AddImage(
                targetIcon.Handle,
                targetPos,
                targetPos + new Vector2(metrics.TargetSize, metrics.TargetSize));
        }

        DrawRowLabel(drawList, metrics, timeText, timeSize, layout.TimeX, y, textColor);
        if (nameText.Length > 0)
            DrawRowLabel(drawList, metrics, nameText, nameSize, layout.NameX, y, textColor);
    }

    private static CueRowPlacement PlaceCueRow(
        float originX,
        in CueRowMetrics metrics,
        bool hasIcon,
        bool hasTarget,
        float timeWidth,
        float nameWidth)
    {
        var layout = new CueRowPlacement();
        if (metrics.Rtl)
        {
            var x = originX + metrics.RowWidth - CueRowIconPadX;
            if (hasIcon)
            {
                layout.IconX = x - metrics.IconSize;
                x = layout.IconX - CueRowIconGap;
            }
            else
                x = originX + metrics.RowWidth - CueRowTextPadX;

            if (hasTarget)
            {
                layout.TargetX = x - metrics.TargetSize;
                x = layout.TargetX - CueRowTargetGap;
            }

            layout.TimeX = x - timeWidth;
            x = layout.TimeX - CueRowIconGap;
            layout.NameX = nameWidth <= 0f ? x : x - nameWidth;
        }
        else
        {
            var x = originX + CueRowTextPadX;
            if (hasIcon)
            {
                layout.IconX = originX + CueRowIconPadX;
                x = layout.IconX + metrics.IconSize + CueRowIconGap;
            }

            if (hasTarget)
            {
                layout.TargetX = x;
                x += metrics.TargetSize + CueRowTargetGap;
            }

            layout.TimeX = x;
            x += timeWidth + CueRowIconGap;
            layout.NameX = x;
        }

        return layout;
    }

    private static void DrawRowLabel(
        ImDrawListPtr drawList,
        in CueRowMetrics metrics,
        string text,
        Vector2 size,
        float x,
        float rowY,
        uint color)
    {
        drawList.AddText(
            metrics.Font,
            metrics.FontSize,
            new Vector2(x, rowY + (metrics.RowHeight - size.Y) * 0.5f),
            color,
            text);
    }

    private static string ResolveCueRowTimeText(UpcomingCue item) =>
        item.IsPostHighlight
            ? I18n.Format(
                "overlay.cue_row_now_time",
                I18n.Get("overlay.now"),
                item.HighlightRemainingSec)
            : I18n.Format("overlay.cue_row_time", item.RemainingSeconds);

    private static Vector2 MeasureText(string text, float fontSize)
    {
        var baseSize = ImGui.GetFontSize();
        var size = ImGui.CalcTextSize(text);
        if (baseSize <= 0f)
            return size;
        return size * (fontSize / baseSize);
    }

    private struct CueRowMetrics
    {
        public float RowWidth;
        public float RowHeight;
        public float IconSize;
        public float TargetSize;
        public float FontSize;
        public ImFontPtr Font;
        public uint RowBg;
        public Vector4 TextColor;
        public bool Rtl;
    }

    private struct CueRowPlacement
    {
        public float IconX;
        public float TargetX;
        public float TimeX;
        public float NameX;
    }
}
