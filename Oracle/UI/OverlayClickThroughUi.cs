namespace Oracle.UI;

/// <summary>Click-through overlays: apply pass-through in PreDraw, drag + save in Draw.</summary>
internal static class OverlayClickThroughUi
{
    private static readonly Vector4 AltHighlightColor = new(1f, 0.92f, 0.2f, 0.35f);
    private static readonly Vector4 AltHighlightBorder = new(1f, 0.85f, 0.1f, 0.95f);

    /// <summary>
    /// Click-through blocks all mouse input unless Alt is held (for dragging).
    /// </summary>
    public static void ApplyMousePassThrough(Window window, bool clickThrough)
    {
        const ImGuiWindowFlags noMouse = ImGuiWindowFlags.NoMouseInputs;
        if (clickThrough && !ImGui.GetIO().KeyAlt)
            window.Flags |= noMouse;
        else
            window.Flags &= ~noMouse;
    }

    public static void Handle(
        Func<Vector2> getPos,
        Action<Vector2> setPos,
        bool clickThrough)
    {
        var alt = ImGui.GetIO().KeyAlt;
        var allowDrag = !clickThrough || alt;

        if (allowDrag
            && ImGui.IsItemActive()
            && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            var pos = getPos() + ImGui.GetIO().MouseDelta;
            setPos(pos);
            ImGui.SetWindowPos(pos);
        }

        if (allowDrag && ImGui.IsItemDeactivated())
            C.Save();

        if (alt)
            DrawAltHighlight();
    }

    private static void DrawAltHighlight()
    {
        var min = ImGui.GetWindowPos();
        var max = min + ImGui.GetWindowSize();
        var drawList = ImGui.GetForegroundDrawList();
        drawList.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(AltHighlightColor), 4f);
        drawList.AddRect(min, max, ImGui.ColorConvertFloat4ToU32(AltHighlightBorder), 4f, ImDrawFlags.None, 2f);
    }
}
