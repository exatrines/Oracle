using System.Text.Json;
using Oracle.Models;
using Oracle.Services;

namespace Oracle.UI;

// --- Cue table: list, toolbar, draft row, clipboard, time formatting ---

internal sealed partial class ConfigWindow
{
    private void DrawCueTable(TimelineDocument doc, bool persist = true)
    {
        _cueTablePersist = persist;
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
                        if (DrawCueTableRow(doc, cue, ref dirty))
                        {
                            ImGui.PopID();
                            break;
                        }

                        ImGui.PopID();
                    }

                    ImGui.EndTable();

                    if (dirty)
                        PersistCueDocument(doc);
                }
            }
        }

        DrawCueDraftRow(doc, tableWidth);
    }

    /// <returns>True when the row was deleted and the table loop should stop.</returns>
    private bool DrawCueTableRow(TimelineDocument doc, TimelineCue cue, ref bool dirty)
    {
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        var selected = _selectedCueIds.Contains(cue.Id);
        if (MirageUi.Checkbox("##sel", ref selected))
        {
            if (selected)
                _selectedCueIds.Add(cue.Id);
            else
                _selectedCueIds.Remove(cue.Id);
        }

        ImGui.TableNextColumn();
        var editingThis = string.Equals(_cueTimeDraftId, cue.Id, StringComparison.Ordinal);
        var timeText = editingThis ? _cueTimeDraft : FormatCueTimeMmSs(cue.TimeOffsetSec);
        var timeChanged = MirageUi.InputText(
            string.Empty,
            ref timeText,
            16,
            id: "time",
            hint: I18n.Get("config.cue.hint.time"),
            width: MirageUi.InputWidthFill);

        if (ImGui.IsItemActivated())
        {
            _cueTimeDraftId = cue.Id;
            _cueTimeDraft = FormatCueTimeMmSs(cue.TimeOffsetSec);
        }

        if (timeChanged)
        {
            _cueTimeDraftId = cue.Id;
            _cueTimeDraft = timeText;
            if (TryParseCueTimeMmSs(timeText, out var parsed))
            {
                cue.TimeOffsetSec = parsed;
                dirty = true;
            }
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            if (TryParseCueTimeMmSs(_cueTimeDraft, out var parsed))
            {
                cue.TimeOffsetSec = parsed;
                dirty = true;
            }

            if (string.Equals(_cueTimeDraftId, cue.Id, StringComparison.Ordinal))
                _cueTimeDraftId = null;
        }

        ImGui.TableNextColumn();
        var actionKind = I18n.Get("config.cue.kind.action");
        var memoKind = I18n.Get("config.cue.kind.memo");
        var kindLabel = cue.Kind == TimelineCueKind.Memo ? memoKind : actionKind;
        if (MirageUi.Dropdown(
                string.Empty,
                ref kindLabel,
                [actionKind, memoKind],
                id: "kind",
                allowClear: false,
                width: MirageUi.InputWidthFill))
        {
            cue.Kind = string.Equals(kindLabel, memoKind, StringComparison.Ordinal)
                ? TimelineCueKind.Memo
                : TimelineCueKind.Action;
            if (cue.Kind == TimelineCueKind.Action)
                cue.Label = string.Empty;
            dirty = true;
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
        else
        {
            DrawCueActionPickButton(
                cue.ActionId,
                cue.ActionId == 0
                    ? I18n.Get("config.cue.pick_action")
                    : ActionLookup.GetName(cue.ActionId),
                () => OpenActionPicker(replaceCueId: cue.Id));
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

        return false;
    }

    private void PersistCueDocument(TimelineDocument doc)
    {
        if (!_cueTablePersist)
            return;
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
        ImGui.SetClipboardText(JsonSerializer.Serialize(payload, CueClipboardJsonOptions));
    }

    private void PasteClipboardCues(TimelineDocument doc)
    {
        if (!TryReadCueClipboard(out var cues) || cues.Count == 0)
            return;

        foreach (var cue in cues)
            doc.Cues.Add(CloneCueRow(cue));

        PersistCueDocument(doc);
    }

    private static bool TryReadCueClipboard(out List<TimelineCue> cues)
    {
        cues = [];
        var text = ImGui.GetClipboardText();
        if (string.IsNullOrWhiteSpace(text))
            return false;

        try
        {
            var payload = JsonSerializer.Deserialize<CueClipboardPayload>(text, CueClipboardJsonOptions);
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
        PersistCueDocument(doc);
    }

    private void DeleteCueRow(TimelineDocument doc, string cueId)
    {
        doc.Cues.RemoveAll(c => string.Equals(c.Id, cueId, StringComparison.Ordinal));
        _selectedCueIds.Remove(cueId);
        if (string.Equals(_cueTimeDraftId, cueId, StringComparison.Ordinal))
            _cueTimeDraftId = null;
        PersistCueDocument(doc);
    }

    private void PruneCueSelection(TimelineDocument doc)
    {
        if (_selectedCueIds.Count == 0)
            return;

        _selectedCueIds.RemoveWhere(id => doc.Cues.TrueForAll(c =>
            !string.Equals(c.Id, id, StringComparison.Ordinal)));
    }

    private static TimelineCue CloneCueRow(TimelineCue source) =>
        new()
        {
            TimeOffsetSec = source.TimeOffsetSec,
            Kind = source.Kind,
            ActionId = source.ActionId,
            Label = source.Kind == TimelineCueKind.Memo ? source.Label : string.Empty,
        };

    private sealed class CueClipboardPayload
    {
        public string Format { get; set; } = string.Empty;
        public List<TimelineCue> Cues { get; set; } = [];
    }

    /// <summary>Icon slot + pick button; empty action still reserves icon width so draft matches list.</summary>
    private static void DrawCueActionPickButton(uint actionId, string label, Action onClick)
    {
        var iconId = ActionLookup.GetIconId(actionId);
        if (iconId == 0 || !MirageUi.GameIcon(iconId, CueActionIconSize, CueActionIconSize))
            ImGui.Dummy(new Vector2(CueActionIconSize, CueActionIconSize));
        ImGui.SameLine();

        var btnWidth = Math.Max(1f, ImGui.GetContentRegionAvail().X);
        if (MirageUi.PrimaryButton(label, width: btnWidth, id: "pickAction"))
            onClick();
    }

    private static void SetupCueTableColumns()
    {
        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, MirageUi.ResolveControlHeight() + 4f);
        ImGui.TableSetupColumn(I18n.Get("config.cue.col.time"), ImGuiTableColumnFlags.WidthFixed, 80f);
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
        _newCueTimeText = "00:00";
        _newCueKind = TimelineCueKind.Action;
        _newCueMemo = string.Empty;
        _newCueActionId = 0;
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
            16,
            id: "time",
            hint: I18n.Get("config.cue.hint.time"),
            width: MirageUi.InputWidthFill);

        ImGui.TableNextColumn();
        var actionKind = I18n.Get("config.cue.kind.action");
        var memoKind = I18n.Get("config.cue.kind.memo");
        var kindLabel = _newCueKind == TimelineCueKind.Memo ? memoKind : actionKind;
        if (MirageUi.Dropdown(
                string.Empty,
                ref kindLabel,
                [actionKind, memoKind],
                id: "kind",
                allowClear: false,
                width: MirageUi.InputWidthFill))
        {
            _newCueKind = string.Equals(kindLabel, memoKind, StringComparison.Ordinal)
                ? TimelineCueKind.Memo
                : TimelineCueKind.Action;
        }

        ImGui.TableNextColumn();
        var isMemo = _newCueKind == TimelineCueKind.Memo;
        if (isMemo)
        {
            MirageUi.InputText(
                string.Empty,
                ref _newCueMemo,
                256,
                id: "memo",
                width: MirageUi.InputWidthFill);
        }
        else
        {
            DrawCueActionPickButton(
                _newCueActionId,
                _newCueActionId == 0
                    ? I18n.Get("config.cue.pick_action")
                    : ActionLookup.GetName(_newCueActionId),
                () => OpenActionPicker(replaceCueId: ActionPickDraftId));
        }

        ImGui.TableNextColumn();
        var canSubmit = TryParseCueTimeMmSs(_newCueTimeText, out _)
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
        if (!TryParseCueTimeMmSs(_newCueTimeText, out var time))
            return;

        var isMemo = _newCueKind == TimelineCueKind.Memo;
        if (!isMemo && _newCueActionId == 0)
            return;

        doc.Cues.Add(new TimelineCue
        {
            TimeOffsetSec = time,
            Kind = isMemo ? TimelineCueKind.Memo : TimelineCueKind.Action,
            ActionId = isMemo ? 0 : _newCueActionId,
            Label = isMemo ? _newCueMemo : string.Empty,
        });
        PersistCueDocument(doc);
        ResetNewCueDraft();
        _newCueDraftDocId = doc.Id;
    }

    /// <summary>Seconds ↁE<c>mm:ss</c> (negative times keep a leading minus). No milliseconds.</summary>
    private static string FormatCueTimeMmSs(float seconds)
    {
        var negative = seconds < 0f;
        var totalSec = (int)Math.Round(Math.Abs((double)seconds));
        var minutes = totalSec / 60;
        var secs = totalSec % 60;
        var body = $"{minutes:00}:{secs:00}";
        return negative ? "-" + body : body;
    }

    /// <summary>Parses <c>mm:ss</c> or <c>-mm:ss</c> into seconds.</summary>
    private static bool TryParseCueTimeMmSs(string text, out float seconds)
    {
        seconds = 0f;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();
        var negative = text.StartsWith('-');
        if (negative)
            text = text[1..].TrimStart();

        var parts = text.Split(':');
        if (parts.Length != 2)
            return false;
        if (!int.TryParse(parts[0], out var minutes) || minutes < 0)
            return false;
        if (!int.TryParse(parts[1], out var secs) || secs < 0 || secs > 59)
            return false;

        seconds = (minutes * 60) + secs;
        if (negative)
            seconds = -seconds;
        return true;
    }
}
