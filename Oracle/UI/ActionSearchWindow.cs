using Oracle.Services;

namespace Oracle.UI;

/// <summary>
/// Picks an action for the editor. Job is linked to the timeline;
/// job level comes from the timeline's content-derived ClassJobLevel.
/// Sized to match Mirage two-column main (right) panel width.
/// </summary>
internal sealed class ActionSearchWindow : Window
{
    private readonly Func<uint> _getClassJobId;
    private readonly Action<uint> _setClassJobId;
    private readonly Func<uint> _getTerritoryTypeId;
    private readonly Func<uint> _getContentFinderConditionId;
    private readonly Func<byte> _getClassJobLevel;
    private readonly Action<JobActionInfo> _onPicked;

    private string _filter = string.Empty;
    private bool _showAction = true;
    private bool _showAbility = true;
    private bool _showRole = true;
    private uint _cachedJobId = uint.MaxValue;
    private byte _cachedJobLevel = byte.MaxValue;
    private IReadOnlyList<JobActionInfo> _actions = [];
    private uint _highlightActionId;
    private readonly HashSet<uint> _selected = [];
    private ImRaii.ColorDisposable? _themeScope;

    public ActionSearchWindow(
        Func<uint> getClassJobId,
        Action<uint> setClassJobId,
        Func<uint> getTerritoryTypeId,
        Func<uint> getContentFinderConditionId,
        Func<byte> getClassJobLevel,
        Action<JobActionInfo> onPicked)
        : base(
            "Select action###oracleActionSearch",
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        _getClassJobId = getClassJobId;
        _setClassJobId = setClassJobId;
        _getTerritoryTypeId = getTerritoryTypeId;
        _getContentFinderConditionId = getContentFinderConditionId;
        _getClassJobLevel = getClassJobLevel;
        _onPicked = onPicked;
        MirageWindowDefaults.ApplyTo(this);

        // Match Mirage two-column right (main) panel: full default width minus sidebar.
        var size = ResolveMainPanelSize();
        Size = size;
        SizeConstraints = new()
        {
            MinimumSize = size,
            MaximumSize = size,
        };
    }

    /// <summary>Optional current cue action to highlight when the window opens.</summary>
    public void SetHighlightActionId(uint actionId)
    {
        _highlightActionId = actionId;
        _selected.Clear();
        if (actionId != 0)
            _selected.Add(actionId);
    }

    public override void OnOpen()
    {
        _filter = string.Empty;
        RefreshCache(force: true);
        _selected.Clear();
        if (_highlightActionId != 0)
            _selected.Add(_highlightActionId);
    }

    public override void PreDraw()
    {
        WindowName = I18n.Get("window.action_search.title") + "###oracleActionSearch";
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        MirageTheme.EnsureDefaultsCaptured();
        _themeScope = MirageTheme.PushCustom(MirageTheme.ResolveAppliedColors());
    }

    public override void PostDraw()
    {
        MirageTheme.Pop(_themeScope);
        _themeScope = null;
        ImGui.PopStyleVar();
    }

    public override void Draw()
    {
        MirageUi.TwoColumn.Draw(
            new MirageTwoColumnState
            {
                ShowSidebar = false,
                ShowSidebarHeader = false,
                ShowSidebarFooter = false,
                ShowSearch = false,
            },
            DrawContent);
    }

    // --- Content ---

    private void DrawContent()
    {
        RefreshCache(force: false);

        MirageUi.Header(I18n.Get("action_search.header"));
        DrawJobAndFilterTable();
        DrawActionList();
    }

    private void DrawJobAndFilterTable()
    {
        if (!ImGui.BeginTable(
                "##actionSearchFields",
                2,
                ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX,
                new Vector2(-1f, 0f)))
            return;

        ImGui.TableSetupColumn("##label", ImGuiTableColumnFlags.WidthFixed, MirageUi.FieldLabelColumnWidth);
        ImGui.TableSetupColumn("##field", ImGuiTableColumnFlags.WidthStretch);

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        MirageUi.Text(I18n.Get("config.label.job"), wrap: false);
        ImGui.TableNextColumn();
        DrawJobCombo();

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        MirageUi.Text(I18n.Get("config.label.zone_group"), wrap: false);
        ImGui.TableNextColumn();
        ZoneCombo.DrawReadonly(
            string.Empty,
            _getTerritoryTypeId(),
            _getContentFinderConditionId(),
            _getClassJobLevel(),
            id: "actionSearchZoneReadonly");

        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        DrawCategoryDropdown();
        ImGui.TableNextColumn();
        MirageUi.SearchFilter(
            "actionFilter",
            ref _filter,
            I18n.Get("action_search.hint.filter"),
            maxLength: 64,
            width: MirageUi.InputWidthFill);

        ImGui.EndTable();
    }

    private void DrawJobCombo()
    {
        var jobId = _getClassJobId();
        if (!JobCombo.Draw(string.Empty, ref jobId, id: "actionSearchJob"))
            return;

        _setClassJobId(jobId);
        RefreshCache(force: true);
    }

    private void DrawCategoryDropdown()
    {
        var preview = FormatCategoryPreview();
        var hasValue = _showAction || _showAbility || _showRole;
        if (!MirageUi.BeginDropdown(
                string.Empty,
                preview,
                id: "actionKindFilter",
                width: MirageUi.InputWidthFill,
                hasValue: hasValue))
            return;

        MirageUi.Checkbox(I18n.Get("kind.action"), ref _showAction);
        MirageUi.Checkbox(I18n.Get("kind.ability"), ref _showAbility);
        MirageUi.Checkbox(I18n.Get("kind.role"), ref _showRole);
        MirageUi.EndDropdown();
    }

    private string FormatCategoryPreview()
    {
        var labels = new List<string>(3);
        if (_showAction)
            labels.Add(I18n.Get("kind.action"));
        if (_showAbility)
            labels.Add(I18n.Get("kind.ability"));
        if (_showRole)
            labels.Add(I18n.Get("kind.role"));

        return labels.Count switch
        {
            0 => I18n.Get("action_search.kind.none"),
            3 => I18n.Get("action_search.kind.all"),
            1 => labels[0],
            _ => string.Join(", ", labels),
        };
    }

    // --- List ---

    private void DrawActionList()
    {
        if (_getClassJobId() == 0)
        {
            MirageUi.Text(I18n.Get("action_search.empty.select_job"), MirageUi.Color.Secondary);
            return;
        }

        var filtered = _actions
            .Where(a => IsKindEnabled(a.Kind))
            .Where(a =>
                MirageUi.MatchesFilter(a.ActionId.ToString(), a.Name, _filter)
                || (!string.IsNullOrWhiteSpace(_filter)
                    && a.ClassJobLevel.ToString().Contains(_filter.Trim(), StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (filtered.Count == 0)
        {
            MirageUi.Text(I18n.Get("action_search.empty.no_actions"), MirageUi.Color.Secondary);
            return;
        }

        if (!ImGui.BeginChild("##actionList", new Vector2(0f, 0f), false))
            return;

        foreach (var kind in new[]
                 {
                     ActionPickerKind.Action,
                     ActionPickerKind.Ability,
                     ActionPickerKind.Role,
                 })
        {
            if (!IsKindEnabled(kind))
                continue;

            var group = filtered.Where(a => a.Kind == kind).ToList();
            if (group.Count == 0)
                continue;

            MirageUi.SubHeader(JobActionCatalog.KindLabel(kind));
            var tiles = group
                .Select(a => (a.ActionId, a.IconId, JobActionCatalog.FormatActionLabel(a)))
                .ToList();

            MirageUi.IconLabelToggleGrid(
                $"##actionPick_{kind}",
                tiles,
                _selected,
                out var clickedId,
                columns: 2,
                singleSelect: true);

            if (clickedId is not uint id)
                continue;

            var picked = group.FirstOrDefault(a => a.ActionId == id);
            if (picked.ActionId == 0)
                picked = _actions.FirstOrDefault(a => a.ActionId == id);
            if (picked.ActionId == 0)
                continue;

            _onPicked(picked);
            IsOpen = false;
            break;
        }

        ImGui.EndChild();
    }

    private bool IsKindEnabled(ActionPickerKind kind) => kind switch
    {
        ActionPickerKind.Action => _showAction,
        ActionPickerKind.Ability => _showAbility,
        ActionPickerKind.Role => _showRole,
        _ => false,
    };

    // --- Cache ---

    private void RefreshCache(bool force)
    {
        var jobId = _getClassJobId();
        var level = _getClassJobLevel();
        if (!force && _cachedJobId == jobId && _cachedJobLevel == level && _actions.Count > 0)
            return;

        _cachedJobId = jobId;
        _cachedJobLevel = level;
        _actions = JobActionCatalog.GetActionsForJob(jobId, level);
    }

    private static Vector2 ResolveMainPanelSize()
    {
        var sidebarWidth = new MirageTwoColumnState().SidebarWidth;
        return new Vector2(
            MirageWindowDefaults.DefaultSize.X - sidebarWidth,
            MirageWindowDefaults.DefaultSize.Y);
    }
}
