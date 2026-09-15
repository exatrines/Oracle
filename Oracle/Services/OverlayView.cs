using Oracle.Models;

namespace Oracle.Services;

/// <summary>Shared overlay header, Action filter, and highlight stroke.</summary>
internal static class OverlayView
{
    public static bool BlinkPhaseOn =>
        (DateTime.UtcNow.Millisecond / 250) % 2 == 0;

    public static bool IsAction(TimelineCue cue) =>
        cue.Kind == TimelineCueKind.Action;

    public static bool TryHighlightLine(
        UpcomingCue item,
        bool blinkPhaseOn,
        out Vector4 color,
        out float thickness)
    {
        var post = item.IsPostHighlight;
        color = post ? C.ActionHighlightAfterLineColor : C.ActionHighlightBeforeLineColor;
        thickness = Math.Max(
            1f,
            post ? C.ActionHighlightAfterLineThickness : C.ActionHighlightBeforeLineThickness);
        if (!item.IsHighlighting)
            return false;

        var blink = post ? C.ActionHighlightAfterBlink : C.ActionHighlightBeforeBlink;
        return !blink || blinkPhaseOn;
    }

    public static string HeaderText(TimelineEngine engine)
    {
        var header = engine.IsPreview
            ? I18n.Get(engine.IsPreviewPaused ? "overlay.preview_paused" : "overlay.preview")
            : engine.ActiveDocument?.Name ?? I18n.Get("overlay.fallback_timeline");
        var clock = engine.IsRunning
            ? I18n.Format("overlay.seconds", engine.ElapsedSeconds)
            : I18n.Get("overlay.stopped");
        return $"{header}  {clock}";
    }
}
