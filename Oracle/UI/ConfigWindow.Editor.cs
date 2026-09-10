using Oracle.Models;
using Oracle.Services;

namespace Oracle.UI;

// --- Editor: General | Auto Load meta, then cue table ---

internal sealed partial class ConfigWindow
{
    private void DrawEditor()
    {
        _editDoc ??= _store.ActiveDocument;
        if (_editDoc == null)
        {
            MirageUi.Header(I18n.Get("config.header.editor"));
            MirageUi.Warning(I18n.Get("config.editor.warn.select_or_create"));
            return;
        }

        DrawEditorMeta(_editDoc);
        MirageUi.SubHeader(I18n.Get("config.subheader.cue"));
        DrawCueTable(_editDoc);
    }

    private void DrawEditorMeta(TimelineDocument doc)
    {
        var sepPad = ImGui.GetStyle().ItemSpacing.X;
        var sepColWidth = sepPad * 2f + 1f;

        if (!ImGui.BeginTable(
                "##editorMeta",
                3,
                ImGuiTableFlags.SizingStretchSame | ImGuiTableFlags.NoPadOuterX,
                new Vector2(-1f, 0f)))
            return;

        ImGui.TableSetupColumn("##general", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##vsep", ImGuiTableColumnFlags.WidthFixed, sepColWidth);
        ImGui.TableSetupColumn("##autoLoad", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        ImGui.BeginGroup();
        MirageUi.SubHeader(I18n.Get("config.subheader.general"));
        DrawTimelineName(doc);
        DrawZoneCombo(doc);
        DrawCommandSection(doc);
        ImGui.EndGroup();
        var leftMin = ImGui.GetItemRectMin();
        var leftMax = ImGui.GetItemRectMax();

        ImGui.TableNextColumn();
        var sepX = ImGui.GetCursorScreenPos().X + (sepColWidth * 0.5f);
        ImGui.Dummy(new Vector2(sepColWidth, 1f));

        ImGui.TableNextColumn();
        ImGui.BeginGroup();
        MirageUi.SubHeader(I18n.Get("config.subheader.auto_load"));
        DrawAutoLoadSection(doc);
        ImGui.EndGroup();
        var rightMin = ImGui.GetItemRectMin();
        var rightMax = ImGui.GetItemRectMax();

        ImGui.EndTable();

        var top = Math.Min(leftMin.Y, rightMin.Y);
        var bottom = Math.Max(leftMax.Y, rightMax.Y);
        ImGui.GetWindowDrawList().AddLine(
            new Vector2(sepX, top),
            new Vector2(sepX, bottom),
            ImGui.GetColorU32(ImGuiCol.Separator));
    }

    private void DrawAutoLoadSection(TimelineDocument doc)
    {
        var autoLoad = doc.AutoLoadEnabled;
        if (MirageUi.Checkbox(I18n.Get("config.checkbox.enable_auto_load"), ref autoLoad))
        {
            doc.AutoLoadEnabled = autoLoad;
            PersistDocument(doc);
        }

        DrawMatchConflictMessage(doc);
        DrawAutoLoadZoneLabel(doc);
        DrawJobCombo(doc);
        DrawAutoLoadPreset(doc);
        DrawMissingAnyHint(doc);
    }

    private void DrawCommandSection(TimelineDocument doc)
    {
        MirageUi.SubHeader(I18n.Get("config.label.command"));

        const string prefix = "/oracle load ";
        var command = doc.LoadCommand;
        var hint = string.IsNullOrWhiteSpace(doc.Name) ? I18n.Get("config.hint.timeline_name") : doc.Name;
        var btn = MirageUi.ResolveControlHeight();
        var gap = ImGui.GetStyle().ItemInnerSpacing.X;

        ImGui.AlignTextToFramePadding();
        MirageUi.Text(prefix, wrap: false);
        ImGui.SameLine(0f, gap);

        var inputWidth = Math.Max(40f, ImGui.GetContentRegionAvail().X - btn - gap);
        if (MirageUi.InputText(
                string.Empty,
                ref command,
                80,
                id: "loadCommand",
                hint: hint,
                width: inputWidth))
        {
            doc.LoadCommand = command.Trim();
            PersistDocument(doc);
        }

        ImGui.SameLine(0f, gap);
        if (MirageUi.IconButton(
                FontAwesomeIcon.Copy,
                "##copyLoadCommand",
                new Vector2(btn, btn),
                tooltip: I18n.Get("config.command.tooltip.copy")))
        {
            ImGui.SetClipboardText(prefix + TimelineStore.GetEffectiveLoadCommand(doc));
        }
    }

    private void DrawMatchConflictMessage(TimelineDocument doc)
    {
        if (!doc.AutoLoadEnabled || !_store.HasMatchConflict(doc))
            return;

        var group = _store.GetMatchConflictGroup(doc);
        var list = string.Join("\n", group.Select(d => $"· {d.Name}"));
        MirageUi.Warning(I18n.Format("config.conflict.warning", list));
    }

    private void DrawMissingAnyHint(TimelineDocument doc)
    {
        if (!doc.AutoLoadEnabled)
            return;
        if (doc.TerritoryTypeId == 0 || doc.ClassJobId == 0)
            return;
        if (string.IsNullOrWhiteSpace(doc.AutoLoadPresetId))
            return;
        if (_store.HasUnconflictedAny(doc.TerritoryTypeId, doc.ClassJobId))
            return;

        MirageUi.Warning(I18n.Get("config.autoload.warn.missing_any"));
    }

    private void EnsureTimelineNameDraft(TimelineDocument doc)
    {
        if (string.Equals(_timelineNameDraftDocId, doc.Id, StringComparison.OrdinalIgnoreCase))
            return;

        _timelineNameDraft = doc.Name;
        _timelineNameDraftDocId = doc.Id;
    }

    private void DrawTimelineName(TimelineDocument doc)
    {
        EnsureTimelineNameDraft(doc);

        if (!ImGui.BeginTable(
                "##timelineNameField",
                2,
                ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX,
                new Vector2(-1f, 0f)))
            return;

        ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthFixed, MirageUi.FieldLabelColumnWidth);
        ImGui.TableSetupColumn("##fld", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        MirageUi.Text(I18n.Get("config.label.timeline_name"), wrap: false);

        ImGui.TableNextColumn();
        var btn = MirageUi.ResolveControlHeight();
        var gap = ImGui.GetStyle().ItemInnerSpacing.X;
        var inputWidth = Math.Max(40f, ImGui.GetContentRegionAvail().X - btn - gap);
        MirageUi.InputText(
            string.Empty,
            ref _timelineNameDraft,
            128,
            id: "timelineName",
            width: inputWidth);

        ImGui.SameLine(0f, gap);
        var nameForSave = string.IsNullOrWhiteSpace(_timelineNameDraft)
            ? I18n.Get("config.default.untitled")
            : _timelineNameDraft.Trim();
        var nameConflict = _store.WouldFileNameConflict(nameForSave, doc.Id);
        var saveEnabled = !nameConflict;
        var saveTooltip = nameConflict
            ? I18n.Format("config.save.tooltip.conflict", ConfigStore.ToFileStem(nameForSave))
            : I18n.Get("config.save.tooltip.ok");
        if (MirageUi.IconButton(
                FontAwesomeIcon.Save,
                "##saveTimelineName",
                new Vector2(btn, btn),
                tooltip: saveTooltip,
                enabled: saveEnabled)
            && saveEnabled)
            CommitTimelineName(doc);

        ImGui.EndTable();
    }

    private void CommitTimelineName(TimelineDocument doc)
    {
        var name = _timelineNameDraft.Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = I18n.Get("config.default.untitled");

        if (_store.WouldFileNameConflict(name, doc.Id))
            return;

        doc.Name = name;
        PersistDocument(doc);
        _timelineNameDraft = doc.Name;
        _timelineNameDraftDocId = doc.Id;
        PluginServices.ChatGui.Print(I18n.Format("config.chat.saved", doc.Name));
    }

    private void DrawAutoLoadPreset(TimelineDocument doc)
    {
        var presetId = doc.AutoLoadPresetId ?? string.Empty;
        var presetMatch = _engine.MatchesLivePreset(doc);
        var liveLabel = FormatLiveBossLabels();
        if (!AutoLoadPresetField.Draw(
                I18n.Get("config.label.autoload_preset"),
                "editorPreset",
                doc.TerritoryTypeId,
                ref presetId,
                liveMatch: presetMatch,
                matchTooltip: FormatMatchTooltip(presetMatch, liveLabel)))
            return;

        doc.AutoLoadPresetId = presetId ?? string.Empty;
        PersistDocument(doc);
    }

    private static string FormatLiveBossLabels()
    {
        var live = BossPresence.LiveLabelsSorted();
        if (live.Count == 0)
            return I18n.Get("config.match.none");
        return string.Join(", ", live);
    }

    private static string FormatMatchTooltip(bool matched, string current) =>
        matched
            ? I18n.Format("config.match.matched", current)
            : I18n.Format("config.match.not_matched", current);

    private static string FormatCurrentZoneLabel(uint territory)
    {
        if (territory == 0)
            return I18n.Get("config.match.none");

        var label = DutyContentCatalog.StripZoneLabelPrefix(DutyContentCatalog.ResolveZoneLabel(territory, 0, 0));
        return string.IsNullOrWhiteSpace(label) ? territory.ToString() : label;
    }

    private static string FormatCurrentJobLabel(uint classJobId)
    {
        if (classJobId == 0)
            return I18n.Get("config.match.none");

        var label = JobActionCatalog.GetJobLabel(classJobId);
        return string.IsNullOrWhiteSpace(label) ? classJobId.ToString() : label;
    }

    private void DrawJobCombo(TimelineDocument doc)
    {
        var jobId = doc.ClassJobId;
        var jobMatch = _engine.MatchesLiveJob(doc);
        var playerJob = PluginServices.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;
        if (!JobCombo.Draw(
                I18n.Get("config.label.job"),
                ref jobId,
                id: "editorJob",
                liveMatch: jobMatch,
                matchTooltip: FormatMatchTooltip(jobMatch, FormatCurrentJobLabel(playerJob))))
            return;

        doc.ClassJobId = jobId;
        PersistDocument(doc);
    }

    private void DrawAutoLoadZoneLabel(TimelineDocument doc)
    {
        var zoneMatch = _engine.MatchesLiveZone(doc);
        var territory = PluginServices.ClientState.TerritoryType;
        ZoneCombo.DrawReadonly(
            I18n.Get("config.label.zone"),
            doc.TerritoryTypeId,
            doc.ContentFinderConditionId,
            doc.ClassJobLevel,
            id: "autoLoadZoneReadonly",
            liveMatch: zoneMatch,
            matchTooltip: FormatMatchTooltip(zoneMatch, FormatCurrentZoneLabel(territory)));
    }

    private void DrawZoneCombo(TimelineDocument doc)
    {
        var territoryTypeId = doc.TerritoryTypeId;
        var contentFinderConditionId = doc.ContentFinderConditionId;
        var classJobLevel = doc.ClassJobLevel;
        var zoneLabel = ResolveEditorZoneLabel(doc);
        var previousTerritory = doc.TerritoryTypeId;

        if (!ZoneCombo.Draw(
                I18n.Get("config.label.zone_group"),
                ref territoryTypeId,
                ref contentFinderConditionId,
                ref classJobLevel,
                ref zoneLabel,
                ref _zoneSearchFilter,
                id: "editorZone"))
            return;

        doc.TerritoryTypeId = territoryTypeId;
        doc.ContentFinderConditionId = contentFinderConditionId;
        doc.ClassJobLevel = classJobLevel;
        if (AutoLoadPresets.Find(doc.TerritoryTypeId, doc.AutoLoadPresetId) == null)
            doc.AutoLoadPresetId = string.Empty;
        PersistDocument(doc);
        if (doc.TerritoryTypeId != previousTerritory)
            _store.MoveToEndOfZone(doc.Id);
    }

    private static string ResolveEditorZoneLabel(TimelineDocument doc)
    {
        if (doc.TerritoryTypeId == 0)
            return string.Empty;

        return DutyContentCatalog.ResolveZoneLabel(
            doc.TerritoryTypeId,
            doc.ContentFinderConditionId,
            doc.ClassJobLevel);
    }
}
