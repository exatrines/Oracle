using Oracle.Models;
using Oracle.Services;
using Oracle.Services.AutoRecord;
using MirageUI.Ui;

namespace Oracle.UI;

/// <summary>
/// Auto Record status window (standard ImGui title bar: drag / collapse / close).
/// Draw order: sync layout state ↁEstatus rows ↁEhorizontal major action strip.
/// </summary>
internal sealed class AutoRecordOverlayWindow : Window
{
    private readonly AutoRecordService _autoRecord;
    private readonly Action _openAutoRecordZones;
    private ImRaii.ColorDisposable? _themeScope;
    private int _pushedTitleColors;

    private const float PanelWidth = 280f;
    private const float MajorVerticalPadding = 8f;
    private const float MajorBarRounding = 6f;
    private const float MajorPastSeconds = 7.4f;
    private const float MajorFutureSeconds = 1.4f;
    private const float MajorPixelsPerSecond = 30f;
    private const float MajorIconSize = 32f;
    private const float MajorZeroLineThickness = 2f;
    private const bool MajorShowGrid = true;

    private static readonly Vector4 MajorBackgroundColor = new(0.05f, 0.05f, 0.05f, 0.72f);
    private static readonly Vector4 MajorGridLineColor = new(1f, 1f, 1f, 0.22f);
    private static readonly Vector4 MajorZeroLineColor = new(0.2f, 0.95f, 0.35f, 1f);

    private const ImGuiWindowFlags OverlayFlags =
        ImGuiWindowFlags.AlwaysAutoResize
        | ImGuiWindowFlags.NoFocusOnAppearing
        | ImGuiWindowFlags.NoNav
        | ImGuiWindowFlags.NoDocking
        | ImGuiWindowFlags.NoSavedSettings
        | ImGuiWindowFlags.NoScrollbar
        | ImGuiWindowFlags.NoScrollWithMouse;

    public AutoRecordOverlayWindow(
        AutoRecordService autoRecord,
        Action openAutoRecordZones)
        : base("Oracle AutoRecord##oracleAutoRecordOverlay", OverlayFlags, forceMainWindow: true)
    {
        _autoRecord = autoRecord;
        _openAutoRecordZones = openAutoRecordZones;
        IsOpen = true;
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
    }

    public override void OnClose()
    {
        C.AutoRecordOverlayVisible = false;
        C.Save();
    }

    public override bool DrawConditions() =>
        PluginServices.ClientState.IsLoggedIn
        && C.AutoRecordEnabled
        && C.AutoRecordOverlayVisible;

    public override void PreDraw()
    {
        IsOpen = PluginServices.ClientState.IsLoggedIn
                 && C.AutoRecordEnabled
                 && C.AutoRecordOverlayVisible;
        WindowName = I18n.Get("window.autorecord_overlay.title") + "##oracleAutoRecordOverlay";

        var panelW = PanelWidth;
        // Appearing only  EAlways would cancel title-bar dragging.
        ImGui.SetNextWindowPos(
            new Vector2(C.AutoRecordOverlayPosX, C.AutoRecordOverlayPosY),
            ImGuiCond.Appearing);
        ImGui.SetNextWindowCollapsed(C.AutoRecordOverlayCollapsed, ImGuiCond.Appearing);
        ImGui.SetNextWindowSizeConstraints(
            new Vector2(panelW, 0f),
            new Vector2(panelW, float.MaxValue));

        MirageTheme.EnsureDefaultsCaptured();
        _themeScope = MirageTheme.PushCustom(MirageTheme.ResolveAppliedColors());

        _pushedTitleColors = 0;
        if (_autoRecord.IsRecording || _autoRecord.HasPendingSave)
        {
            var theme = MirageTheme.ResolveAppliedColors();
            var warning = MirageUi.GetColor(MirageUi.Color.Warning);
            var title = MirageUi.MixColors(theme.TitleBg, warning, 0.35f);
            ImGui.PushStyleColor(ImGuiCol.TitleBg, title);
            ImGui.PushStyleColor(ImGuiCol.TitleBgActive, MirageUi.MixColors(title, warning, 0.15f));
            _pushedTitleColors = 2;
        }
    }

    public override void PostDraw()
    {
        if (_pushedTitleColors > 0)
        {
            ImGui.PopStyleColor(_pushedTitleColors);
            _pushedTitleColors = 0;
        }

        MirageTheme.Pop(_themeScope);
        _themeScope = null;
    }

    public override void Draw()
    {
        SyncWindowStateToConfig();
        if (ImGui.IsWindowCollapsed())
            return;

        var recording = _autoRecord.IsRecording;
        var pending = _autoRecord.HasPendingSave;
        var blocked = !_autoRecord.IsCurrentZoneEnabled;

        var textPrimary = MirageUi.GetColor(MirageUi.Color.Default);
        var textMuted = MirageUi.GetColor(MirageUi.Color.Secondary);
        var warning = MirageUi.GetColor(MirageUi.Color.Warning);

        DrawJobZoneRow(textPrimary, textMuted);
        DrawStatusAndMajorStrip(
            FormatStatusText(recording, pending, blocked),
            recording || pending || blocked ? warning : textMuted);

        if (pending)
            DrawPendingSaveButtons();
    }

    // --- window state ---

    private void SyncWindowStateToConfig()
    {
        var collapsed = ImGui.IsWindowCollapsed();
        if (collapsed != C.AutoRecordOverlayCollapsed)
        {
            C.AutoRecordOverlayCollapsed = collapsed;
            C.Save();
        }

        var pos = ImGui.GetWindowPos();
        if (Math.Abs(pos.X - C.AutoRecordOverlayPosX) <= 0.5f
            && Math.Abs(pos.Y - C.AutoRecordOverlayPosY) <= 0.5f)
            return;

        C.AutoRecordOverlayPosX = pos.X;
        C.AutoRecordOverlayPosY = pos.Y;
        if (!ImGui.GetIO().MouseDown[(int)ImGuiMouseButton.Left])
            C.Save();
    }

    private string FormatStatusText(bool recording, bool pending, bool blocked)
    {
        if (recording)
            return I18n.Format("autorecord.overlay.recording_clock", _autoRecord.SessionElapsedSeconds);
        if (pending)
            return I18n.Get("autorecord.overlay.status_pending_save");
        if (blocked)
            return I18n.Get("autorecord.overlay.status_zone_disabled");
        return I18n.Get("autorecord.overlay.status_stopped");
    }

    // --- status rows ---

    /// <summary>Row 1: job | zone | scene · gear</summary>
    private void DrawJobZoneRow(Vector4 textPrimary, Vector4 textMuted)
    {
        var job = ResolveJobLabel();
        var zone = ResolveZoneLabel();
        var sceneText = I18n.Format("autorecord.overlay.scene", _autoRecord.CurrentGameSceneId);
        var sep = " | ";
        var btnSize = MirageUi.ResolveControlHeight();
        var rowMin = ImGui.GetCursorScreenPos();
        var rowWidth = ImGui.GetContentRegionAvail().X;
        var gap = MirageLayout.Style.ItemInnerSpacing.X;

        ImGui.AlignTextToFramePadding();
        MirageUi.Text(job, textPrimary, wrap: false);
        ImGui.SameLine(0f, 0f);
        MirageUi.Text(sep, textMuted, wrap: false);
        ImGui.SameLine(0f, 0f);

        var used = ImGui.CalcTextSize(job).X
                   + ImGui.CalcTextSize(sep).X
                   + ImGui.CalcTextSize(sep).X
                   + ImGui.CalcTextSize(sceneText).X;
        var zoneMaxW = MathF.Max(20f, rowWidth - used - btnSize - gap);
        MirageUi.Text(TruncateToWidth(zone, zoneMaxW), textPrimary, wrap: false);
        if (ImGui.IsItemHovered())
            MirageUi.Tooltip(zone);

        ImGui.SameLine(0f, 0f);
        MirageUi.Text(sep, textMuted, wrap: false);
        ImGui.SameLine(0f, 0f);
        MirageUi.Text(sceneText, textPrimary, wrap: false);

        ImGui.SetCursorScreenPos(
            new Vector2(
                rowMin.X + rowWidth - btnSize,
                rowMin.Y + (ImGui.GetFrameHeight() - btnSize) * 0.5f));
        DrawZoneSettingsButton();
    }

    private void DrawZoneSettingsButton()
    {
        var btnSize = new Vector2(MirageUi.ResolveControlHeight());
        if (!MirageUi.IconButton(
                FontAwesomeIcon.Cog,
                id: "autoRecordZoneSettings",
                size: btnSize,
                tooltip: I18n.Get("autorecord.overlay.open_record_zones")))
            return;

        _openAutoRecordZones();
    }

    private void DrawPendingSaveButtons()
    {
        MirageLayout.Cursor.Y += MirageLayout.Style.ItemSpacing.Y;
        var gap = MirageLayout.Style.ItemInnerSpacing.X;
        var avail = ImGui.GetContentRegionAvail().X;
        var btnW = MathF.Max(60f, (avail - gap) * 0.5f);

        if (MirageUi.PrimaryButton(
                I18n.Get("autorecord.overlay.button_save"),
                width: btnW,
                id: "autoRecordPendingSave"))
        {
            _autoRecord.ConfirmPendingSave();
        }

        ImGui.SameLine(0f, gap);
        if (MirageUi.SecondaryButton(
                I18n.Get("autorecord.overlay.button_cancel"),
                width: btnW,
                id: "autoRecordPendingCancel"))
        {
            _autoRecord.CancelPendingSave();
        }
    }

    // --- status + major strip as one block ---

    private void DrawStatusAndMajorStrip(string status, Vector4 statusColor)
    {
        ResolveAxis(out var pps, out var pastSeconds, out var futureSeconds, out var zeroX, out var width);
        var iconSize = MajorIconSize;
        var elapsed = _autoRecord.SessionElapsedSecondsPrecise;
        var items = CollectStripItems(elapsed, pastSeconds, futureSeconds);

        var statusH = ImGui.GetTextLineHeight();
        var statusPadX = 6f;
        var statusPadTop = 3f;
        var statusPadBottom = 2f;
        var trackPad = MajorVerticalPadding;
        var barHeight = statusPadTop + statusH + statusPadBottom + trackPad + iconSize + trackPad;

        var origin = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(width, barHeight));

        var barMin = origin;
        var barMax = origin + new Vector2(width, barHeight);
        var statusY = barMin.Y + statusPadTop;
        var areaTop = barMin.Y + statusPadTop + statusH + statusPadBottom;
        var trackCenterY = areaTop + trackPad + iconSize * 0.5f;
        var trackBottom = barMax.Y - trackPad;
        var zeroLineX = barMin.X + zeroX;
        var dl = ImGui.GetWindowDrawList();

        dl.AddRectFilled(barMin, barMax, MirageUi.ToUInt(MajorBackgroundColor), MajorBarRounding);
        dl.AddText(
            new Vector2(barMin.X + statusPadX, statusY),
            MirageUi.ToUInt(statusColor),
            status);

        DrawSecondGrid(
            dl,
            barMin,
            barMax,
            areaTop,
            trackBottom,
            zeroLineX,
            pps,
            pastSeconds,
            futureSeconds);

        DrawActionIcons(
            dl,
            items,
            zeroLineX,
            trackCenterY,
            barMin,
            barMax,
            pps,
            iconSize);
    }

    private readonly record struct StripItem(uint ActionId, float AxisSeconds);

    private List<StripItem> CollectStripItems(float elapsed, float pastSeconds, float futureSeconds)
    {
        if (!_autoRecord.IsRecording && !_autoRecord.HasPendingSave)
            return [];

        var cues = _autoRecord.GetRecordedCuesSnapshot();
        var list = new List<StripItem>(cues.Count);
        foreach (var cue in cues)
        {
            if (cue.Kind != TimelineCueKind.Action || cue.ActionId == 0)
                continue;

            // Cast time relative to now: 0 at cast, then moves left (negative / past).
            var axisSec = cue.TimeOffsetSec - elapsed;
            if (axisSec < -pastSeconds - 0.05f || axisSec > futureSeconds + 0.05f)
                continue;

            list.Add(new StripItem(cue.ActionId, axisSec));
        }

        // Oldest first so later casts paint on top when icons overlap on the single row.
        list.Sort((a, b) => a.AxisSeconds.CompareTo(b.AxisSeconds));
        return list;
    }

    private static void DrawSecondGrid(
        ImDrawListPtr drawList,
        Vector2 barMin,
        Vector2 barMax,
        float areaTop,
        float trackBottom,
        float zeroLineX,
        float pps,
        float pastSeconds,
        float futureSeconds)
    {
        var gridColor = MirageUi.ToUInt(MajorGridLineColor);

        var firstSec = (int)Math.Floor(-pastSeconds);
        var lastSec = (int)Math.Ceiling(futureSeconds);
        for (var sec = firstSec; sec <= lastSec; sec++)
        {
            if (sec == 0 || !MajorShowGrid)
                continue;

            var x = zeroLineX + sec * pps;
            if (x < barMin.X - 1f || x > barMax.X + 1f)
                continue;

            drawList.AddLine(
                new Vector2(x, areaTop),
                new Vector2(x, trackBottom),
                gridColor,
                1f);
        }

        var zeroThickness = Math.Max(1f, MajorZeroLineThickness);
        drawList.AddLine(
            new Vector2(zeroLineX, areaTop),
            new Vector2(zeroLineX, trackBottom),
            MirageUi.ToUInt(MajorZeroLineColor),
            zeroThickness);
    }

    private static void DrawActionIcons(
        ImDrawListPtr drawList,
        IReadOnlyList<StripItem> items,
        float zeroLineX,
        float trackCenterY,
        Vector2 barMin,
        Vector2 barMax,
        float pps,
        float iconSize)
    {
        // Single row: items are oldest→newest so later casts draw on top when X overlaps.
        foreach (var item in items)
        {
            var centerX = zeroLineX + item.AxisSeconds * pps;
            if (centerX < barMin.X - iconSize || centerX > barMax.X + iconSize)
                continue;

            var iconMin = new Vector2(centerX - iconSize * 0.5f, trackCenterY - iconSize * 0.5f);
            var iconMax = iconMin + new Vector2(iconSize, iconSize);
            var icon = ActionLookup.GetIconWrap(item.ActionId);
            if (icon != null)
                drawList.AddImage(icon.Handle, iconMin, iconMax);
            else
                drawList.AddRectFilled(iconMin, iconMax, MirageUi.ToUInt(new Vector4(0.2f, 0.2f, 0.2f, 0.9f)), 3f);
        }
    }

    private static void ResolveAxis(
        out float pps,
        out float pastSeconds,
        out float futureSeconds,
        out float zeroX,
        out float width)
    {
        pps = MajorPixelsPerSecond;
        pastSeconds = MajorPastSeconds;
        futureSeconds = MajorFutureSeconds;
        zeroX = pastSeconds * pps;
        width = (pastSeconds + futureSeconds) * pps;
    }

    // --- labels ---

    private string ResolveZoneLabel()
    {
        if (_autoRecord.IsRecording || _autoRecord.HasPendingSave)
        {
            var session = _autoRecord.SessionZoneLabel;
            return string.IsNullOrWhiteSpace(session)
                ? I18n.Get("fflogs.title.unknown")
                : session;
        }

        var territory = PluginServices.ClientState.TerritoryType;
        if (territory == 0)
            return I18n.Get("config.zone.not_set");

        var label = DutyContentCatalog.StripZoneLabelPrefix(
            DutyContentCatalog.ResolveZoneLabel(territory, 0, 0));
        var levelMarker = I18n.Get("zone.level_marker");
        var dash = label.LastIndexOf(levelMarker, StringComparison.OrdinalIgnoreCase);
        if (dash > 0)
            label = label[..dash].Trim();

        return string.IsNullOrWhiteSpace(label) ? territory.ToString() : label;
    }

    private string ResolveJobLabel()
    {
        var jobId = _autoRecord.IsRecording || _autoRecord.HasPendingSave
            ? _autoRecord.SessionClassJobId
            : PluginServices.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;

        if (jobId == 0)
            return I18n.Get("job.not_set");

        var job = JobActionCatalog.GetCombatJobs().FirstOrDefault(j => j.Id == jobId);
        if (job.Id == 0)
            return I18n.Format("job.unknown", jobId);

        return string.IsNullOrWhiteSpace(job.Abbreviation) ? job.Name : job.Abbreviation;
    }

    // --- small utils ---

    private static string TruncateToWidth(string text, float maxWidth)
    {
        if (maxWidth <= 0f || ImGui.CalcTextSize(text).X <= maxWidth)
            return text;

        var draw = text;
        while (draw.Length > 1 && ImGui.CalcTextSize(draw + "…").X > maxWidth)
            draw = draw[..^1];
        return draw + "…";
    }
}
