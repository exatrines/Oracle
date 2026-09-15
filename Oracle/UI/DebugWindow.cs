using Oracle.Services;

namespace Oracle.UI;

/// <summary>Dev overlay: preview clock and hotbar highlight grid.</summary>
internal sealed class DebugWindow : Window
{
    private const float Icon = 28f;
    private const float MarkRadius = 5f;
    private static readonly Vector4 MarkYellow = new(1f, 0.85f, 0.15f, 1f);
    private static readonly Vector4 MarkRed = new(0.95f, 0.22f, 0.18f, 1f);

    private readonly TimelineEngine _engine;
    private readonly HotbarHighlightService _hotbarHighlight;
    private ImRaii.ColorDisposable? _themeScope;

    public DebugWindow(TimelineEngine engine, HotbarHighlightService hotbarHighlight)
        : base("Oracle Debug###oracleDebug")
    {
        _engine = engine;
        _hotbarHighlight = hotbarHighlight;
        MirageWindowDefaults.ApplyTo(this);
        Flags &= ~ImGuiWindowFlags.NoResize;
        Flags |= ImGuiWindowFlags.NoSavedSettings;
        Size = new Vector2(900f, 640f);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new()
        {
            MinimumSize = new Vector2(480f, 280f),
            MaximumSize = MirageWindowDefaults.MaximumSize,
        };
    }

    public override void PreDraw()
    {
        MirageTheme.EnsureDefaultsCaptured();
        _themeScope = MirageTheme.PushCustom(MirageTheme.ResolveAppliedColors());
    }

    public override void PostDraw()
    {
        MirageTheme.Pop(_themeScope);
        _themeScope = null;
    }

    public override void Draw()
    {
        if (MirageUi.Button("Start", enabled: _engine.CanStartPreview))
            _engine.StartPreview();
        ImGui.SameLine();
        if (MirageUi.Button(_engine.IsPreviewPaused ? "Resume" : "Pause", enabled: _engine.IsRunning && _engine.IsPreview))
            _engine.TryTogglePreviewPause();
        ImGui.SameLine();
        if (MirageUi.Button("Stop", enabled: _engine.IsRunning && _engine.IsPreview))
            _engine.StopPreview();

        var status = !_engine.IsRunning || !_engine.IsPreview
            ? "stopped"
            : _engine.IsPreviewPaused ? "paused" : "running";
        MirageUi.Text($"{status}  {_engine.ElapsedSeconds:0.000}s", MirageUi.Color.Secondary);

        var cells = new Dictionary<(byte, byte), HotbarDebugCell>();
        foreach (var slot in _hotbarHighlight.LastFrame.Slots)
            cells[(slot.HotbarId, slot.SlotId)] = slot;

        DrawTable("HB", 12, 0, 10, cells);
        DrawTable("XHB", 16, 10, 8, cells);
        DrawWxhb();
    }

    private static void DrawTable(
        string title,
        int columns,
        byte startId,
        int rows,
        Dictionary<(byte, byte), HotbarDebugCell> cells)
    {
        MirageUi.Text(title, wrap: false);
        if (!BeginTable($"dbg{title}", columns))
            return;

        for (var i = 0; i < rows; i++)
        {
            var hotbarId = (byte)(startId + i);
            ImGui.TableNextRow();
            Label($"{title}{i + 1}", C.ShowHotbarHighlight && C.IsHotbarHighlightEnabled(hotbarId));
            for (byte slotId = 0; slotId < columns; slotId++)
            {
                cells.TryGetValue((hotbarId, slotId), out var cell);
                Cell(cell?.IconId ?? 0, cell?.Mark ?? HotbarDebugMark.None);
            }
        }

        ImGui.EndTable();
    }

    private void DrawWxhb()
    {
        MirageUi.Text("WXHB", wrap: false);
        if (!BeginTable("dbgWXHB", 8))
            return;

        var enabled = C.ShowHotbarHighlight && C.ShowHotbarHighlightDoubleCross;
        foreach (var (label, addon) in new[] { ("L", "_ActionDoubleCrossL"), ("R", "_ActionDoubleCrossR") })
        {
            var byCol = new HotbarDebugCell?[8];
            foreach (var slot in _hotbarHighlight.LastFrame.WxhbSlots)
            {
                if (slot.AddonName == addon)
                    byCol[slot.SlotId % 8] = slot;
            }

            ImGui.TableNextRow();
            Label($"WXHB {label}", enabled);
            for (var i = 0; i < 8; i++)
                Cell(byCol[i]?.IconId ?? 0, byCol[i]?.Mark ?? HotbarDebugMark.None);
        }

        ImGui.EndTable();
    }

    private static bool BeginTable(string id, int columns)
    {
        if (!ImGui.BeginTable(id, columns + 1, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit))
            return false;
        ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed, 56f);
        for (var i = 0; i < columns; i++)
            ImGui.TableSetupColumn((i + 1).ToString(), ImGuiTableColumnFlags.WidthFixed, 44f);
        ImGui.TableHeadersRow();
        return true;
    }

    private static void Label(string text, bool enabled)
    {
        ImGui.TableNextColumn();
        MirageUi.Text(text, enabled ? MirageUi.Color.Accent : MirageUi.Color.Default, wrap: false);
    }

    private static void Cell(uint iconId, HotbarDebugMark mark)
    {
        ImGui.TableNextColumn();
        if (iconId == 0 || !MirageUi.GameIcon(iconId, Icon, Icon))
            ImGui.Dummy(new Vector2(Icon, Icon));

        ImGui.SameLine(0f, 4f);
        var origin = ImGui.GetCursorScreenPos();
        if (mark != HotbarDebugMark.None)
        {
            var color = mark == HotbarDebugMark.After ? MarkRed : MarkYellow;
            ImGui.GetWindowDrawList().AddCircleFilled(
                origin + new Vector2(MarkRadius, Icon * 0.5f),
                MarkRadius,
                ImGui.ColorConvertFloat4ToU32(color));
        }

        ImGui.Dummy(new Vector2(MarkRadius * 2f, Icon));
    }
}
