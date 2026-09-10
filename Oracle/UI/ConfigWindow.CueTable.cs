using System.Text.Json;
using Oracle.Models;
using Oracle.Services;

namespace Oracle.UI;

// --- Cue table: list, toolbar, draft row, clipboard ---

internal sealed partial class ConfigWindow
{
    private void DrawCueTable(TimelineDocument doc)
    {
        EnsureNewCueDraft(doc);
        PruneCueSelection(doc);

        DrawCueToolbar(doc);

        // Fill remaining right-panel height; leave room for the fixed draft row below.
        var spacing = ImGui.GetStyle().ItemSpacing.Y;
        var draftReserve = MirageUi.ResolveControlHeight() + ImGui.GetStyle().CellPadding.Y * 2f + 8f;
        var bodyHeight = Math.Max(64f, ImGui.GetContentRegionAvail().Y - draftReserve - spacing);

        // Always reserve the scrollbar gutter; measure inner width so the draft row matches exactly
        // (child padding alone would leave the draft wider than list buttons).
        var tableWidth = Math.Max(1f, ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ScrollbarSize);
        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero))
        using (var scroll = ImRaii.Child(
                   "##cueTableScroll",
                   new Vector2(-1f, bodyHeight),
                   false,
                   ImGuiWindowFlags.AlwaysVerticalScrollbar | ImGuiWindowFlags.AlwaysUseWindowPadding))
        {
            if (scroll)
            {
                tableWidth = Math.Max(1f, ImGui.GetContentRegionAvail().X);

                const ImGuiTableFlags flags =
                    ImGuiTableFlags.Borders
                    | ImGuiTableFlags.RowBg
                    | ImGuiTableFlags.SizingStretchProp
                    | ImGuiTableFlags.NoHostExtendX;

                if (ImGui.BeginTable("##cueTable", 5, flags, new Vector2(tableWidth, 0f)))
                {
                    SetupCueTableColumns();
                    ImGui.TableHeadersRow();

                    var dirty = false;
                    foreach (var cue in doc.Cues.OrderBy(c => c.TimeOffsetSec).ToList())
                    {
                        ImGui.PushID(cue.Id);
                        if (DrawCueTableRow(doc, cue, tableWidth, ref dirty))
                        {
                            ImGui.PopID();
                            break;
                        }

                        ImGui.PopID();
                    }

                    ImGui.EndTable();

                    if (dirty)
                        PersistDocument(doc);
                }
            }
        }

        DrawCueDraftRow(doc, tableWidth);
    }

    /// <returns>True when the row was deleted and the table loop should stop.</returns>
    private bool DrawCueTableRow(TimelineDocument doc, TimelineCue cue, float tableWidth, ref bool dirty)
    {
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        var rowMin = ImGui.GetCursorScreenPos();

        var selected = _selectedCueIds.Contains(cue.Id);
        if (MirageUi.Checkbox("##sel", ref selected))
        {
            if (selected)
                _selectedCueIds.Add(cue.Id);
            else
                _selectedCueIds.Remove(cue.Id);
        }

        ImGui.TableNextColumn();
        DrawCueTimeCell(cue, ref dirty);

        ImGui.TableNextColumn();
        var kindLabel = CueKindLabel(cue.Kind);
        if (MirageUi.Dropdown(
                string.Empty,
                ref kindLabel,
                CueKindLabels,
                id: "kind",
                allowClear: false,
                width: MirageUi.InputWidthFill))
        {
            var next = ParseCueKindLabel(kindLabel);
            if (next != cue.Kind)
            {
                cue.Kind = next;
                if (cue.Kind != TimelineCueKind.Memo)
                    cue.Label = string.Empty;
                if (cue.Kind is not (TimelineCueKind.Action or TimelineCueKind.Sync))
                    cue.ActionId = 0;
                if (cue.Kind != TimelineCueKind.Action)
                    CueTargetCatalog.Clear(cue);
                if (cue.Kind != TimelineCueKind.Sync)
                {
                    cue.Effected = false;
                    cue.SyncType = default;
                }

                dirty = true;
            }
        }

        ImGui.TableNextColumn();
        if (cue.Kind == TimelineCueKind.Memo)
        {
            var memo = cue.Label;
            if (MirageUi.InputText(
                    string.Empty,
                    ref memo,
                    256,
                    id: "memo",
                    width: MirageUi.InputWidthFill))
            {
                cue.Label = memo;
                dirty = true;
            }
        }
        else if (cue.Kind == TimelineCueKind.Sync)
        {
            if (SyncCueFields.DrawRow(cue, "##rowCastSync"))
                dirty = true;
        }
        else
        {
            if (DrawCueActionContents(cue, () => OpenActionPicker(replaceCueId: cue.Id)))
                dirty = true;
        }

        ImGui.TableNextColumn();
        if (MirageUi.IconButton(
                FontAwesomeIcon.Trash,
                "##deleteCue",
                size: default,
                tooltip: I18n.Get("config.cue.tooltip.delete_row")))
        {
            DeleteCueRow(doc, cue.Id);
            return true;
        }

        var rowMax = new Vector2(rowMin.X + tableWidth, ImGui.GetItemRectMax().Y);
        DrawCueRowContextMenu(doc, cue, rowMin, rowMax);
        return false;
    }

    private void DrawCueTimeCell(TimelineCue cue, ref bool dirty)
    {
        var savedTimeText = CueTime.Format(cue.TimeOffsetSec);
        var editingThis = string.Equals(_cueTimeDraftId, cue.Id, StringComparison.Ordinal);
        var timeText = editingThis ? _cueTimeDraft : savedTimeText;
        var gap = ImGui.GetStyle().ItemSpacing.X;
        var showCheck = CanApplyCueTimeDraft(timeText, savedTimeText, out _);
        var inputWidth = showCheck
            ? Math.Max(40f, ImGui.GetContentRegionAvail().X - MirageUi.ResolveControlHeight() - gap)
            : MirageUi.InputWidthFill;

        var timeChanged = MirageUi.InputText(
            string.Empty,
            ref timeText,
            12,
            id: "time",
            hint: I18n.Get("config.cue.hint.time"),
            width: inputWidth);

        if (ImGui.IsItemActivated())
        {
            _cueTimeDraftId = cue.Id;
            _cueTimeDraft = savedTimeText;
        }

        if (timeChanged)
        {
            _cueTimeDraftId = cue.Id;
            _cueTimeDraft = timeText;
        }

        var submitEnter = ImGui.IsItemFocused()
                          && (ImGui.IsKeyPressed(ImGuiKey.Enter) || ImGui.IsKeyPressed(ImGuiKey.KeypadEnter));

        var draftText = string.Equals(_cueTimeDraftId, cue.Id, StringComparison.Ordinal)
            ? _cueTimeDraft
            : savedTimeText;
        var canApply = CanApplyCueTimeDraft(draftText, savedTimeText, out var parsedTime);

        var clickedCheck = false;
        if (showCheck)
        {
            ImGui.SameLine(0f, gap);
            clickedCheck = MirageUi.IconButton(
                FontAwesomeIcon.Check,
                "##applyTime",
                size: default,
                tooltip: I18n.Get("config.cue.tooltip.submit_time"));
        }

        if ((clickedCheck || submitEnter) && canApply)
        {
            cue.TimeOffsetSec = parsedTime;
            if (string.Equals(_cueTimeDraftId, cue.Id, StringComparison.Ordinal))
                _cueTimeDraftId = null;
            dirty = true;
        }
    }

    private static bool CanApplyCueTimeDraft(string draftText, string savedTimeText, out float parsedTime) =>
        CueTime.TryParse(draftText, out parsedTime)
        && !string.Equals(draftText, savedTimeText, StringComparison.Ordinal);

    private void DrawCueRowContextMenu(TimelineDocument doc, TimelineCue cue, Vector2 rowMin, Vector2 rowMax)
    {
        if (ImGui.IsMouseHoveringRect(rowMin, rowMax)
            && ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem)
            && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            ImGui.OpenPopup("##cueRowMenu");

        var insertAbove = I18n.Get("config.cue.menu.insert_above");
        var insertBelow = I18n.Get("config.cue.menu.insert_below");
        var labels = new[] { insertAbove, insertBelow };
        var style = MirageContextMenuStyle.CreateDefault();
        if (!MirageUi.ContextMenu.Begin("##cueRowMenu", labels, style))
            return;

        if (MirageUi.ContextMenu.DrawItem(insertAbove, FontAwesomeIcon.ArrowUp, "insertAbove", style))
        {
            InsertCueRow(doc, cue, above: true);
            ImGui.CloseCurrentPopup();
        }

        if (MirageUi.ContextMenu.DrawItem(insertBelow, FontAwesomeIcon.ArrowDown, "insertBelow", style))
        {
            InsertCueRow(doc, cue, above: false);
            ImGui.CloseCurrentPopup();
        }

        MirageUi.ContextMenu.End();
    }

    private void InsertCueRow(TimelineDocument doc, TimelineCue relative, bool above)
    {
        var index = doc.Cues.FindIndex(c => string.Equals(c.Id, relative.Id, StringComparison.Ordinal));
        if (index < 0)
            return;

        var blank = new TimelineCue
        {
            TimeOffsetSec = relative.TimeOffsetSec,
            Kind = TimelineCueKind.Action,
        };
        doc.Cues.Insert(above ? index : index + 1, blank);
        PersistDocument(doc);
    }

    private void DrawCueToolbar(TimelineDocument doc)
    {
        var hasSelection = _selectedCueIds.Count > 0;
        var gap = ImGui.GetStyle().ItemSpacing.X;

        if (MirageUi.IconButton(
                FontAwesomeIcon.Copy,
                "##cueCopy",
                size: default,
                tooltip: I18n.Get("config.cue.tooltip.copy"),
                enabled: hasSelection)
            && hasSelection)
            CopySelectedCues(doc);

        ImGui.SameLine(0f, gap);
        if (MirageUi.IconButton(
                FontAwesomeIcon.Paste,
                "##cuePaste",
                size: default,
                tooltip: I18n.Get("config.cue.tooltip.paste")))
            PasteClipboardCues(doc);

        ImGui.SameLine(0f, gap);
        if (MirageUi.IconButton(
                FontAwesomeIcon.Trash,
                "##cueDelete",
                size: default,
                tooltip: I18n.Get("config.cue.tooltip.delete"),
                enabled: hasSelection)
            && hasSelection)
            DeleteSelectedCues(doc);
    }

    private void CopySelectedCues(TimelineDocument doc)
    {
        var cues = doc.Cues
            .Where(c => _selectedCueIds.Contains(c.Id))
            .OrderBy(c => c.TimeOffsetSec)
            .Select(CloneCueRow)
            .ToList();
        if (cues.Count == 0)
            return;

        var payload = new CueClipboardPayload
        {
            Format = CueClipboardFormat,
            Cues = cues,
        };
        ImGui.SetClipboardText(JsonSerializer.Serialize(payload, TimelineJson.CompactOptions));
    }

    private void PasteClipboardCues(TimelineDocument doc)
    {
        if (!TryReadCueClipboard(out var cues) || cues.Count == 0)
            return;

        foreach (var cue in cues)
            doc.Cues.Add(CloneCueRow(cue));

        PersistDocument(doc);
    }

    private static bool TryReadCueClipboard(out List<TimelineCue> cues)
    {
        cues = [];
        var text = ImGui.GetClipboardText();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        try
        {
            var payload = TimelineJson.DeserializePayload<CueClipboardPayload>(text);
            if (payload?.Cues == null
                || !string.Equals(payload.Format, CueClipboardFormat, StringComparison.Ordinal)
                || payload.Cues.Count == 0)
                return false;

            cues = payload.Cues;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private void DeleteSelectedCues(TimelineDocument doc)
    {
        if (_selectedCueIds.Count == 0)
            return;

        doc.Cues.RemoveAll(c => _selectedCueIds.Contains(c.Id));
        if (_cueTimeDraftId != null && _selectedCueIds.Contains(_cueTimeDraftId))
            _cueTimeDraftId = null;
        _selectedCueIds.Clear();
        PersistDocument(doc);
    }

    private void DeleteCueRow(TimelineDocument doc, string cueId)
    {
        doc.Cues.RemoveAll(c => string.Equals(c.Id, cueId, StringComparison.Ordinal));
        _selectedCueIds.Remove(cueId);
        if (string.Equals(_cueTimeDraftId, cueId, StringComparison.Ordinal))
            _cueTimeDraftId = null;
        PersistDocument(doc);
    }

    private void PruneCueSelection(TimelineDocument doc)
    {
        if (_selectedCueIds.Count == 0)
            return;

        _selectedCueIds.RemoveWhere(id => doc.Cues.TrueForAll(c =>
            !string.Equals(c.Id, id, StringComparison.Ordinal)));
    }

    private static TimelineCue CloneCueRow(TimelineCue source) =>
        source.CopyForDocument();

    private sealed class CueClipboardPayload
    {
        public string Format { get; set; } = string.Empty;
        public List<TimelineCue> Cues { get; set; } = [];
    }

    /// <summary>Icon slot + pick button; empty action still reserves icon width so draft matches list.</summary>
    private static void DrawCueActionPickButton(uint actionId, string label, Action onClick, float buttonWidth)
    {
        var iconId = ActionLookup.GetIconId(actionId);
        if (iconId == 0 || !MirageUi.GameIcon(iconId, CueActionIconSize, CueActionIconSize))
            ImGui.Dummy(new Vector2(CueActionIconSize, CueActionIconSize));
        ImGui.SameLine();

        var btnWidth = Math.Max(1f, buttonWidth);
        if (MirageUi.PrimaryButton(label, width: btnWidth, id: "pickAction"))
            onClick();
    }

    private static string CueActionContentsLabel(TimelineCue cue) =>
        cue.ActionId == 0
            ? I18n.Get("config.cue.pick_action")
            : ActionLookup.GetName(cue.ActionId);

    private bool DrawCueActionContents(TimelineCue cue, Action onPick)
    {
        var kind = cue.TargetKind;
        var jobId = cue.TargetJobId;
        var role = cue.TargetRole;
        DrawCueActionContents(
            cue.ActionId,
            CueActionContentsLabel(cue),
            onPick,
            ref kind,
            ref jobId,
            ref role);
        if (kind == cue.TargetKind && jobId == cue.TargetJobId && role == cue.TargetRole)
            return false;

        cue.TargetKind = kind;
        cue.TargetJobId = jobId;
        cue.TargetRole = role;
        return true;
    }

    private static void DrawCueActionContents(
        uint actionId,
        string label,
        Action onPick,
        ref CueTargetKind targetKind,
        ref uint targetJobId,
        ref CueTargetRole targetRole)
    {
        var gap = ImGui.GetStyle().ItemSpacing.X;
        var pickWidth = Math.Max(
            1f,
            ImGui.GetContentRegionAvail().X - CueActionIconSize - gap - CueActionIconSize - gap);
        DrawCueActionPickButton(actionId, label, onPick, pickWidth);
        ImGui.SameLine(0f, gap);
        DrawCueTargetPicker(ref targetKind, ref targetJobId, ref targetRole);
    }

    private static void DrawCueTargetPicker(
        ref CueTargetKind kind,
        ref uint jobId,
        ref CueTargetRole role)
    {
        var size = new Vector2(CueActionIconSize, CueActionIconSize);
        var tooltip = I18n.Get("config.cue.tooltip.target") + ": " + TargetPickerLabel(kind, jobId, role);
        if (ImGui.InvisibleButton("##targetPick", size))
            ImGui.OpenPopup("##targetPopup");
        if (ImGui.IsItemHovered())
            MirageUi.Tooltip(tooltip);

        DrawTargetSlotIcon(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), kind, jobId, role, selected: kind != CueTargetKind.None);

        if (!MirageUi.BeginAccentPopup("##targetPopup"))
            return;

        if (DrawTargetNoneOption(kind == CueTargetKind.None))
        {
            kind = CueTargetKind.None;
            jobId = 0;
            role = CueTargetRole.None;
            ImGui.CloseCurrentPopup();
        }

        ImGui.Separator();
        foreach (var item in CueTargetCatalog.Roles)
        {
            ImGui.PushID((int)item);
            if (DrawTargetIconOption(
                    CueTargetCatalog.GetRoleIconId(item),
                    CueTargetCatalog.GetRoleLabel(item),
                    kind == CueTargetKind.Role && role == item))
            {
                kind = CueTargetKind.Role;
                role = item;
                jobId = 0;
                ImGui.CloseCurrentPopup();
            }

            ImGui.PopID();
            ImGui.SameLine(0f, 4f);
        }

        ImGui.NewLine();
        ImGui.Separator();

        var jobs = JobActionCatalog.GetCombatJobs();
        var col = 0;
        const int jobsPerRow = 10;
        foreach (var job in jobs)
        {
            ImGui.PushID((int)job.Id);
            if (DrawTargetIconOption(
                    CueTargetCatalog.GetJobIconId(job.Id),
                    $"{job.Abbreviation} - {job.Name}",
                    kind == CueTargetKind.Job && jobId == job.Id))
            {
                kind = CueTargetKind.Job;
                jobId = job.Id;
                role = CueTargetRole.None;
                ImGui.CloseCurrentPopup();
            }

            ImGui.PopID();
            col++;
            if (col < jobsPerRow && job.Id != jobs[^1].Id)
                ImGui.SameLine(0f, 4f);
            else
                col = 0;
        }

        MirageUi.EndAccentPopup();
    }

    private static string TargetPickerLabel(CueTargetKind kind, uint jobId, CueTargetRole role) =>
        kind switch
        {
            CueTargetKind.Job => CueTargetCatalog.GetJobLabel(jobId),
            CueTargetKind.Role => CueTargetCatalog.GetRoleLabel(role),
            _ => I18n.Get("config.cue.target.none"),
        };

    private static bool DrawTargetNoneOption(bool selected)
    {
        var size = new Vector2(CueActionIconSize, CueActionIconSize);
        var clicked = ImGui.InvisibleButton("##targetNone", size);
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRect(min, max, ImGui.GetColorU32(ImGuiCol.Border), 3f);
        if (selected)
            drawList.AddRect(min, max, ImGui.GetColorU32(ImGuiCol.CheckMark), 3f, ImDrawFlags.None, 2f);

        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var icon = FontAwesomeIcon.Ban.ToIconString();
            var textSize = ImGui.CalcTextSize(icon);
            drawList.AddText(
                min + (max - min - textSize) * 0.5f,
                ImGui.GetColorU32(ImGuiCol.TextDisabled),
                icon);
        }

        if (ImGui.IsItemHovered())
            MirageUi.Tooltip(I18n.Get("config.cue.target.none"));
        return clicked;
    }

    private static bool DrawTargetIconOption(uint iconId, string tooltip, bool selected)
    {
        var size = new Vector2(CueActionIconSize, CueActionIconSize);
        var clicked = ImGui.InvisibleButton("##targetOpt", size);
        DrawTargetIconRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), iconId, selected);
        if (ImGui.IsItemHovered())
            MirageUi.Tooltip(tooltip);
        return clicked;
    }

    private static void DrawTargetSlotIcon(
        Vector2 min,
        Vector2 max,
        CueTargetKind kind,
        uint jobId,
        CueTargetRole role,
        bool selected)
    {
        if (kind == CueTargetKind.None)
        {
            var drawList = ImGui.GetWindowDrawList();
            drawList.AddRect(min, max, ImGui.GetColorU32(ImGuiCol.Border), 3f);
            using (ImRaii.PushFont(UiBuilder.IconFont))
            {
                var icon = FontAwesomeIcon.Ban.ToIconString();
                var textSize = ImGui.CalcTextSize(icon);
                drawList.AddText(
                    min + (max - min - textSize) * 0.5f,
                    ImGui.GetColorU32(ImGuiCol.TextDisabled),
                    icon);
            }

            return;
        }

        var iconId = kind == CueTargetKind.Job
            ? CueTargetCatalog.GetJobIconId(jobId)
            : CueTargetCatalog.GetRoleIconId(role);
        DrawTargetIconRect(min, max, iconId, selected);
    }

    private static void DrawTargetIconRect(Vector2 min, Vector2 max, uint iconId, bool selected)
    {
        var drawList = ImGui.GetWindowDrawList();
        if (iconId != 0 && MirageUi.TryGetGameIcon(iconId, out var texture))
            drawList.AddImage(texture.Handle, min, max);
        else
            drawList.AddRectFilled(min, max, ImGui.GetColorU32(ImGuiCol.FrameBg), 3f);

        if (selected)
            drawList.AddRect(min, max, ImGui.GetColorU32(ImGuiCol.CheckMark), 3f, ImDrawFlags.None, 2f);
    }

    private static void SetupCueTableColumns()
    {
        var timeCol = MirageUi.ResolveControlHeight() + 116f + ImGui.GetStyle().ItemSpacing.X;
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, MirageUi.ResolveControlHeight() + 4f);
        ImGui.TableSetupColumn(I18n.Get("config.cue.col.time"), ImGuiTableColumnFlags.WidthFixed, timeCol);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 96f);
        ImGui.TableSetupColumn(I18n.Get("config.cue.col.contents"), ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, MirageUi.ResolveControlHeight() + 6f);
    }

    private void EnsureNewCueDraft(TimelineDocument doc)
    {
        if (string.Equals(_newCueDraftDocId, doc.Id, StringComparison.OrdinalIgnoreCase))
            return;

        ResetNewCueDraft();
        _selectedCueIds.Clear();
        _newCueDraftDocId = doc.Id;
    }

    private void ResetNewCueDraft()
    {
        _newCueTimeText = CueTime.Format(0f);
        _newCueKind = TimelineCueKind.Action;
        _newCueMemo = string.Empty;
        _newCueActionId = 0;
        _newCueTargetKind = CueTargetKind.None;
        _newCueTargetJobId = 0;
        _newCueTargetRole = CueTargetRole.None;
        _newCueEffected = false;
        _newCueSyncType = EnemySyncType.Cast;
    }

    /// <summary>Fixed input row under the cue table; up-arrow commits into the list.</summary>
    private void DrawCueDraftRow(TimelineDocument doc, float tableWidth)
    {
        const ImGuiTableFlags flags =
            ImGuiTableFlags.Borders
            | ImGuiTableFlags.RowBg
            | ImGuiTableFlags.SizingStretchProp
            | ImGuiTableFlags.NoHostExtendX;

        if (!ImGui.BeginTable("##cueDraftRow", 5, flags, new Vector2(Math.Max(1f, tableWidth), 0f)))
            return;

        SetupCueTableColumns();

        ImGui.PushID("##cueDraft");
        ImGui.TableNextRow();

        // Spacer matching the selection checkbox column.
        ImGui.TableNextColumn();

        ImGui.TableNextColumn();
        MirageUi.InputText(
            string.Empty,
            ref _newCueTimeText,
            12,
            id: "time",
            hint: I18n.Get("config.cue.hint.time"),
            width: MirageUi.InputWidthFill);

        ImGui.TableNextColumn();
        var kindLabel = CueKindLabel(_newCueKind);
        if (MirageUi.Dropdown(
                string.Empty,
                ref kindLabel,
                CueKindLabels,
                id: "kind",
                allowClear: false,
                width: MirageUi.InputWidthFill))
        {
            var next = ParseCueKindLabel(kindLabel);
            if (next != _newCueKind)
            {
                _newCueKind = next;
                if (_newCueKind != TimelineCueKind.Sync)
                {
                    _newCueEffected = false;
                    _newCueSyncType = EnemySyncType.Cast;
                }
            }
        }

        ImGui.TableNextColumn();
        var isMemo = _newCueKind == TimelineCueKind.Memo;
        var isSync = _newCueKind == TimelineCueKind.Sync;
        if (isMemo)
        {
            MirageUi.InputText(
                string.Empty,
                ref _newCueMemo,
                256,
                id: "memo",
                width: MirageUi.InputWidthFill);
        }
        else if (isSync)
        {
            SyncCueFields.DrawRow(
                ref _newCueSyncType,
                ref _newCueActionId,
                ref _newCueEffected,
                "draftCastSync");
        }
        else
        {
            DrawCueActionContents(
                _newCueActionId,
                _newCueActionId == 0
                    ? I18n.Get("config.cue.pick_action")
                    : ActionLookup.GetName(_newCueActionId),
                () => OpenActionPicker(replaceCueId: ActionPickDraftId),
                ref _newCueTargetKind,
                ref _newCueTargetJobId,
                ref _newCueTargetRole);
        }

        ImGui.TableNextColumn();
        var canSubmit = CueTime.TryParse(_newCueTimeText, out _)
                        && (isMemo || _newCueActionId != 0);
        if (MirageUi.IconButton(
                FontAwesomeIcon.ArrowUpFromBracket,
                "##submitCue",
                size: default,
                tooltip: I18n.Get("config.cue.tooltip.submit"),
                enabled: canSubmit)
            && canSubmit)
            SubmitNewCueRow(doc);

        ImGui.PopID();
        ImGui.EndTable();
    }

    private void SubmitNewCueRow(TimelineDocument doc)
    {
        if (!CueTime.TryParse(_newCueTimeText, out var time))
            return;

        if (_newCueKind == TimelineCueKind.Action && _newCueActionId == 0)
            return;
        if (_newCueKind == TimelineCueKind.Sync && _newCueActionId == 0)
            return;

        var cue = new TimelineCue
        {
            TimeOffsetSec = time,
            Kind = _newCueKind,
            ActionId = _newCueKind is TimelineCueKind.Action or TimelineCueKind.Sync
                ? _newCueActionId
                : 0,
            Label = _newCueKind == TimelineCueKind.Memo
                ? _newCueMemo
                : string.Empty,
            SyncType = _newCueKind == TimelineCueKind.Sync
                ? _newCueSyncType
                : default,
            Effected = _newCueKind == TimelineCueKind.Sync && _newCueEffected,
        };
        if (_newCueKind == TimelineCueKind.Action)
        {
            cue.TargetKind = _newCueTargetKind;
            cue.TargetJobId = _newCueTargetJobId;
            cue.TargetRole = _newCueTargetRole;
        }

        doc.Cues.Add(cue);
        PersistDocument(doc);
        ResetNewCueDraft();
        _newCueDraftDocId = doc.Id;
    }

    private static string[] CueKindLabels =>
    [
        I18n.Get("config.cue.kind.action"),
        I18n.Get("config.cue.kind.memo"),
        I18n.Get("config.cue.kind.sync"),
    ];

    private static string CueKindLabel(TimelineCueKind kind) =>
        kind switch
        {
            TimelineCueKind.Memo => I18n.Get("config.cue.kind.memo"),
            TimelineCueKind.Sync => I18n.Get("config.cue.kind.sync"),
            _ => I18n.Get("config.cue.kind.action"),
        };

    private static TimelineCueKind ParseCueKindLabel(string label)
    {
        if (string.Equals(label, I18n.Get("config.cue.kind.memo"), StringComparison.Ordinal))
            return TimelineCueKind.Memo;
        if (string.Equals(label, I18n.Get("config.cue.kind.sync"), StringComparison.Ordinal))
            return TimelineCueKind.Sync;
        return TimelineCueKind.Action;
    }

}
