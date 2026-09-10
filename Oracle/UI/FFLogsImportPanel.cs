using Oracle.Models;
using Oracle.Services;
using Oracle.Services.FFLogs;

namespace Oracle.UI;

/// <summary>FFLogs import UI drawn in the config center column.</summary>
internal sealed class FFLogsImportPanel : IDisposable
{
    private readonly Action<TimelineDocument> _onImported;
    private readonly FFLogsClient _client = new();

    private string _url = string.Empty;
    private string _status = string.Empty;
    private bool _busy;

    private FFLogsReportMeta? _meta;
    private string _parsedCode = string.Empty;
    private int _selectedFightId;
    private int _selectedSourceId;

    private string _title = string.Empty;
    private uint _classJobId;
    private uint _territoryTypeId;
    private uint _contentFinderConditionId;
    private byte _zoneClassJobLevel;
    private string _autoLoadPresetId = string.Empty;
    private bool _autoLoadEnabled = true;
    private bool _importEnemyHitMemos;
    private bool _importSync;
    private uint _syncOptionTerritoryId;
    private string _zoneSearchFilter = string.Empty;
    private string _zoneLabel = string.Empty;
    private int _zoneAppliedForFightId = -1;

    private CancellationTokenSource? _cts;
    private Action? _pendingUi;

    /// <summary>Job used for Auto Load and the import-actions pane.</summary>
    public uint SelectedClassJobId => _classJobId;

    public FFLogsImportPanel(Action<TimelineDocument> onImported) =>
        _onImported = onImported;

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _client.Dispose();
    }

    public void Draw()
    {
        var pending = Interlocked.Exchange(ref _pendingUi, null);
        pending?.Invoke();

        MirageUi.Header(I18n.Get("fflogs.header.import"));
        DrawCredentialsWarning();
        DrawReportUrlRow();

        if (!string.IsNullOrWhiteSpace(_status))
            MirageUi.Text(_status, MirageUi.Color.Secondary);

        if (_meta == null)
            return;

        DrawLoadedReport();
    }

    private void DrawLoadedReport()
    {
        MirageUi.SubHeader(I18n.Format("fflogs.subheader.report", _meta!.Title));

        var fights = _meta.Fights.ToList();
        if (fights.Count == 0)
        {
            MirageUi.Text(I18n.Get("fflogs.empty.no_fights"), MirageUi.Color.Secondary);
            return;
        }

        if (!TryDrawFightAndPlayer(fights, out var fight, out var player))
            return;

        DrawTimelineMeta();
        DrawImportOptions();
        DrawAutoLoadSection();
        DrawCreateButton(fight, player);
    }

    private void DrawCredentialsWarning()
    {
        if (string.IsNullOrWhiteSpace(C.FFLogsClientId)
            || string.IsNullOrWhiteSpace(C.FFLogsClientSecret))
        {
            MirageUi.Warning(I18n.Get("fflogs.warn.credentials"));
        }
    }

    private void DrawReportUrlRow()
    {
        if (!ImGui.BeginTable(
                "##fflogsUrlRow",
                2,
                ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX,
                new Vector2(-1f, 0f)))
            return;

        ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthFixed, MirageUi.FieldLabelColumnWidth);
        ImGui.TableSetupColumn("##fld", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        MirageUi.Text(I18n.Get("fflogs.label.report_url"), wrap: false);

        ImGui.TableNextColumn();
        var btn = MirageUi.ResolveControlHeight();
        var gap = ImGui.GetStyle().ItemInnerSpacing.X;
        var inputWidth = Math.Max(40f, ImGui.GetContentRegionAvail().X - (btn * 2f) - (gap * 2f));
        MirageUi.InputText(
            string.Empty,
            ref _url,
            512,
            id: "fflogsUrl",
            hint: I18n.Get("fflogs.hint.report_url"),
            width: inputWidth);

        ImGui.SameLine(0f, gap);
        using (ImRaii.Disabled(_busy))
        {
            if (MirageUi.IconButton(
                    FontAwesomeIcon.Paste,
                    "##fflogsPasteUrl",
                    new Vector2(btn, btn),
                    tooltip: I18n.Get("fflogs.tooltip.paste_url"))
                && !_busy)
            {
                var clip = ImGui.GetClipboardText();
                if (!string.IsNullOrWhiteSpace(clip))
                    _url = clip.Trim();
            }

            ImGui.SameLine(0f, gap);
            if (MirageUi.IconButton(
                    FontAwesomeIcon.CloudDownloadAlt,
                    "##fflogsLoad",
                    new Vector2(btn, btn),
                    tooltip: I18n.Get("fflogs.tooltip.import_report"))
                && !_busy)
                _ = LoadReportAsync();
        }

        ImGui.EndTable();
    }

    private bool TryDrawFightAndPlayer(
        List<FFLogsFightInfo> fights,
        out FFLogsFightInfo fight,
        out FFLogsActorInfo player)
    {
        fight = fights[0];
        player = default!;

        var fightLabels = fights.Select(FormatFightLabel).ToList();
        var fightIndex = fights.FindIndex(f => f.Id == _selectedFightId);
        if (fightIndex < 0)
            fightIndex = 0;

        var fightLabel = fightLabels[fightIndex];
        if (MirageUi.Dropdown(I18n.Get("fflogs.label.report"), ref fightLabel, fightLabels, id: "fflogsFight", allowClear: false))
        {
            var idx = fightLabels.IndexOf(fightLabel);
            if (idx >= 0)
            {
                _selectedFightId = fights[idx].Id;
                ApplyZoneFromFight(fights[idx]);
                var playersForFight = FFLogsImportService.PlayersForFight(_meta!, fights[idx]).ToList();
                if (playersForFight.Count > 0 && playersForFight.All(p => p.Id != _selectedSourceId))
                {
                    _selectedSourceId = playersForFight[0].Id;
                    ApplyPlayerJob(playersForFight[0]);
                }

                RefreshAutoTitle();
            }
        }

        fight = fights.FirstOrDefault(f => f.Id == _selectedFightId) ?? fights[0];
        _selectedFightId = fight.Id;
        if (_zoneAppliedForFightId != fight.Id)
        {
            ApplyZoneFromFight(fight);
            RefreshAutoTitle();
        }

        var players = FFLogsImportService.PlayersForFight(_meta!, fight).ToList();
        if (players.Count == 0)
        {
            MirageUi.Text(I18n.Get("fflogs.empty.no_players"), MirageUi.Color.Secondary);
            return false;
        }

        if (players.All(p => p.Id != _selectedSourceId))
        {
            _selectedSourceId = players[0].Id;
            ApplyPlayerJob(players[0]);
            RefreshAutoTitle();
        }

        var playerLabels = players.Select(FormatPlayerLabel).ToList();
        var playerIndex = players.FindIndex(p => p.Id == _selectedSourceId);
        if (playerIndex < 0)
            playerIndex = 0;
        var playerLabel = playerLabels[playerIndex];
        if (MirageUi.Dropdown(I18n.Get("fflogs.label.player"), ref playerLabel, playerLabels, id: "fflogsPlayer", allowClear: false))
        {
            var idx = playerLabels.IndexOf(playerLabel);
            if (idx >= 0)
            {
                _selectedSourceId = players[idx].Id;
                ApplyPlayerJob(players[idx]);
                RefreshAutoTitle();
            }
        }

        player = players.FirstOrDefault(p => p.Id == _selectedSourceId) ?? players[0];
        _selectedSourceId = player.Id;
        return true;
    }

    private void DrawTimelineMeta()
    {
        ImportTimelineUi.DrawName(ref _title, "fflogsTitle");
        ImportTimelineUi.DrawZoneField(
            editable: true,
            id: "fflogsZoneGroup",
            ref _territoryTypeId,
            ref _contentFinderConditionId,
            ref _zoneClassJobLevel,
            ref _zoneLabel,
            ref _zoneSearchFilter);
    }

    private void DrawImportOptions() =>
        ImportTimelineUi.DrawImportOptions(
            ref _importEnemyHitMemos,
            ref _importSync,
            ref _syncOptionTerritoryId,
            _territoryTypeId);

    private void DrawAutoLoadSection() =>
        ImportTimelineUi.DrawAutoLoad(
            "fflogs",
            ref _autoLoadEnabled,
            ref _territoryTypeId,
            ref _contentFinderConditionId,
            ref _zoneClassJobLevel,
            ref _zoneLabel,
            ref _zoneSearchFilter,
            ref _classJobId,
            ref _autoLoadPresetId);

    private void DrawCreateButton(FFLogsFightInfo fight, FFLogsActorInfo player)
    {
        MirageUi.PaddedSeparator();
        var canCreate = ImportTimelineUi.CanCreate(
            ResolveClassJobId(player),
            _territoryTypeId,
            _importEnemyHitMemos,
            _importSync);
        using (ImRaii.Disabled(_busy || !canCreate))
        {
            if (MirageUi.PrimaryButton(I18n.Get("fflogs.button.create_timeline"), id: "fflogsCreate"))
                _ = CreateTimelineAsync(fight, player);
        }

        if (!_busy && !canCreate)
        {
            MirageUi.Text(
                I18n.Get("fflogs.empty.no_import_actions"),
                MirageUi.Color.Secondary);
        }
    }

    // --- Load / create ---

    private async Task LoadReportAsync()
    {
        if (_busy)
            return;

        if (!FFLogsUrlParser.TryParse(_url, out var parts))
        {
            _status = I18n.Get("fflogs.status.invalid_url");
            return;
        }

        _busy = true;
        _status = I18n.Get("fflogs.status.loading");
        _zoneAppliedForFightId = -1;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            var meta = await _client.GetReportMetaAsync(
                parts.Code,
                C.FFLogsClientId,
                C.FFLogsClientSecret,
                ct).ConfigureAwait(false);

            PostToUi(() =>
            {
                _meta = meta;
                _parsedCode = parts.Code;
                _selectedFightId = parts.FightId
                    ?? meta.Fights.FirstOrDefault()?.Id
                    ?? 0;
                _selectedSourceId = parts.SourceId ?? 0;
                InitDraftDefaults();
                _status = I18n.Format("fflogs.status.loaded", meta.Fights.Count, meta.Players.Count);
                _busy = false;
            });
        }
        catch (OperationCanceledException)
        {
            PostToUi(() =>
            {
                _status = I18n.Get("fflogs.status.cancelled");
                _busy = false;
            });
        }
        catch (Exception ex)
        {
            PluginServices.Log.Warning(ex, "FFLogs load failed");
            PostToUi(() =>
            {
                _meta = null;
                _status = ex.Message;
                _busy = false;
            });
        }
    }

    private async Task CreateTimelineAsync(FFLogsFightInfo fight, FFLogsActorInfo player)
    {
        if (_busy || _meta == null || string.IsNullOrWhiteSpace(_parsedCode))
            return;

        var classJobId = ResolveClassJobId(player);
        var allowed = C.GetFFLogsImportActionIds(classJobId);
        var syncSpecs = _importSync
            ? EnemySyncPresets.SpecsFor(_territoryTypeId)
            : [];
        var castSyncSpecs = syncSpecs.Where(s => s.SyncType == EnemySyncType.Cast).ToList();
        var statusSyncSpecs = syncSpecs.Where(s => s.SyncType == EnemySyncType.Status).ToList();
        if (allowed.Count == 0
            && !_importEnemyHitMemos
            && syncSpecs.Count == 0)
        {
            _status = I18n.Get("fflogs.status.select_actions");
            return;
        }

        var code = _parsedCode;
        var options = new FFLogsImportOptions
        {
            Name = _title,
            ClassJobId = classJobId,
            TerritoryTypeId = _territoryTypeId,
            ContentFinderConditionId = _contentFinderConditionId,
            ClassJobLevel = _zoneClassJobLevel,
            AutoLoadPresetId = _autoLoadPresetId,
            AutoLoadEnabled = _autoLoadEnabled,
        };

        _busy = true;
        _status = I18n.Get("fflogs.status.fetching");
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        var importMemos = _importEnemyHitMemos;
        var importSync = _importSync;

        try
        {
            var castsTask = allowed.Count == 0
                ? Task.FromResult<IReadOnlyList<FFLogsCastEvent>>([])
                : _client.GetCastsAsync(
                    code,
                    fight.Id,
                    player.Id,
                    fight.StartTime,
                    fight.EndTime,
                    C.FFLogsClientId,
                    C.FFLogsClientSecret,
                    ct);
            var hitsTask = importMemos
                ? _client.GetDamageTakenAsync(
                    code,
                    fight.Id,
                    fight.StartTime,
                    fight.EndTime,
                    C.FFLogsClientId,
                    C.FFLogsClientSecret,
                    ct)
                : Task.FromResult<IReadOnlyList<FFLogsDamageHit>>([]);
            var enemyCastsTask = castSyncSpecs.Count > 0
                ? _client.GetEnemyCastsAsync(
                    code,
                    fight.Id,
                    fight.StartTime,
                    fight.EndTime,
                    C.FFLogsClientId,
                    C.FFLogsClientSecret,
                    ct)
                : Task.FromResult<IReadOnlyList<FFLogsCastEvent>>([]);
            var statusEventsTask = statusSyncSpecs.Count > 0
                ? _client.GetStatusEventsAsync(
                    code,
                    fight.Id,
                    fight.StartTime,
                    fight.EndTime,
                    statusSyncSpecs.Select(s => s.ActionId).ToList(),
                    C.FFLogsClientId,
                    C.FFLogsClientSecret,
                    ct)
                : Task.FromResult<IReadOnlyList<FFLogsStatusEvent>>([]);

            await Task.WhenAll(castsTask, hitsTask, enemyCastsTask, statusEventsTask).ConfigureAwait(false);

            PostToUi(() => FinishCreate(
                castsTask.Result,
                hitsTask.Result,
                enemyCastsTask.Result,
                statusEventsTask.Result,
                importMemos,
                importSync,
                castSyncSpecs,
                statusSyncSpecs,
                fight,
                player,
                options,
                allowed,
                code));
        }
        catch (OperationCanceledException)
        {
            PostToUi(() =>
            {
                _status = I18n.Get("fflogs.status.cancelled");
                _busy = false;
            });
        }
        catch (Exception ex)
        {
            PluginServices.Log.Warning(ex, "FFLogs create timeline failed");
            PostToUi(() =>
            {
                _status = ex.Message;
                _busy = false;
            });
        }
    }

    private void FinishCreate(
        IReadOnlyList<FFLogsCastEvent> casts,
        IReadOnlyList<FFLogsDamageHit> hits,
        IReadOnlyList<FFLogsCastEvent> enemyCasts,
        IReadOnlyList<FFLogsStatusEvent> statusEvents,
        bool importMemos,
        bool importSync,
        IReadOnlyList<EnemySyncSpec> castSyncSpecs,
        IReadOnlyList<EnemySyncSpec> statusSyncSpecs,
        FFLogsFightInfo fight,
        FFLogsActorInfo player,
        FFLogsImportOptions options,
        HashSet<uint> allowed,
        string code)
    {
        try
        {
            var players = _meta == null
                ? []
                : FFLogsImportService.PlayersForFight(_meta, fight);
            var allCues = allowed.Count == 0
                ? []
                : FFLogsImportService.BuildAllCues(fight, casts, players, player.Id);
            var actionCues = allCues
                .Where(cue => allowed.Contains(cue.ActionId))
                .ToList();
            var memos = importMemos
                ? FFLogsBossMemoImport.BuildMemos(fight, hits)
                : [];
            var castSyncCues = importSync
                ? FFLogsEnemySyncImport.BuildCastCues(fight, enemyCasts, castSyncSpecs)
                : [];
            var statusSyncCues = importSync
                ? FFLogsEnemySyncImport.BuildStatusCues(fight, statusEvents, statusSyncSpecs)
                : [];
            var cues = actionCues
                .Concat(memos)
                .Concat(castSyncCues)
                .Concat(statusSyncCues)
                .OrderBy(c => c.TimeOffsetSec)
                .ThenBy(c => c.Kind)
                .ToList();
            if (cues.Count == 0)
            {
                if (allCues.Count > 0)
                    _status = I18n.Format("fflogs.status.no_match", allCues.Count);
                else if (importSync)
                    _status = I18n.Get("fflogs.status.no_enemy_sync");
                else if (importMemos)
                    _status = I18n.Get("fflogs.status.no_enemy_hits");
                else
                    _status = I18n.Get("fflogs.status.no_casts");
                return;
            }

            var document = FFLogsImportService.BuildDocument(
                code,
                fight,
                player,
                options,
                cues);
            if (document.ClassJobId == 0)
            {
                document.ClassJobId =
                    PluginServices.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;
            }

            _onImported(document);
            _status = FormatCreatedStatus(
                document.Name,
                actionCues.Count,
                allCues.Count,
                memos.Count,
                castSyncCues.Count + statusSyncCues.Count);
            PluginServices.ChatGui.Print(
                I18n.Format("fflogs.chat.imported", document.Name));
        }
        finally
        {
            _busy = false;
        }
    }

    private static string FormatCreatedStatus(
        string name,
        int actionCount,
        int allCastCount,
        int memoCount,
        int syncCount)
    {
        var status = memoCount > 0
            ? I18n.Format("fflogs.status.created_with_memos", name, actionCount, memoCount)
            : I18n.Format("fflogs.status.created", name, actionCount, allCastCount);
        return ImportTimelineUi.WithSyncCount(status, syncCount);
    }

    private void InitDraftDefaults()
    {
        if (_meta == null)
            return;

        var fight = _meta.Fights.FirstOrDefault(f => f.Id == _selectedFightId)
            ?? _meta.Fights.FirstOrDefault();
        if (fight == null)
            return;

        var players = FFLogsImportService.PlayersForFight(_meta, fight);
        var player = players.FirstOrDefault(p => p.Id == _selectedSourceId)
            ?? players.FirstOrDefault();
        if (player == null)
            return;

        _selectedFightId = fight.Id;
        _selectedSourceId = player.Id;
        _autoLoadPresetId = string.Empty;
        _classJobId = FFLogsImportService.ResolveClassJobId(player.SubType);
        if (_classJobId == 0)
            _classJobId = PluginServices.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;
        ApplyZoneFromFight(fight);
        RefreshAutoTitle();
    }

    private void PostToUi(Action action) =>
        Interlocked.Exchange(ref _pendingUi, action);

    // --- Helpers ---

    private void ApplyZoneFromFight(FFLogsFightInfo fight)
    {
        _zoneAppliedForFightId = fight.Id;
        if (fight.GameZoneId <= 0)
        {
            ZoneCombo.ApplyCurrent(
                ref _territoryTypeId,
                ref _contentFinderConditionId,
                ref _zoneClassJobLevel,
                ref _zoneLabel);
        }
        else
        {
            var territory = (uint)fight.GameZoneId;
            var preferName = !string.IsNullOrWhiteSpace(fight.Name)
                ? fight.Name
                : fight.GameZoneName;

            if (DutyContentCatalog.TryResolveZoneFromTerritory(territory, preferName, out var option))
            {
                _territoryTypeId = option.TerritoryTypeId;
                _contentFinderConditionId = option.ContentFinderConditionId;
                _zoneClassJobLevel = option.ClassJobLevel;
                _zoneLabel = option.Label;
            }
            else
            {
                _territoryTypeId = territory;
                _contentFinderConditionId = 0;
                _zoneClassJobLevel = 0;
                _zoneLabel = !string.IsNullOrWhiteSpace(fight.GameZoneName)
                    ? $"{territory} | {fight.GameZoneName}"
                    : DutyContentCatalog.ResolveZoneLabel(territory, 0, 0);
            }
        }
    }

    private void ApplyPlayerJob(FFLogsActorInfo player)
    {
        var resolved = FFLogsImportService.ResolveClassJobId(player.SubType);
        if (resolved != 0)
            _classJobId = resolved;
    }

    /// <summary>
    /// Overwrite on Report/Player change: <c>{content} {jobFullName}</c>
    /// </summary>
    private void RefreshAutoTitle()
    {
        if (string.IsNullOrWhiteSpace(_parsedCode))
            return;

        var content = ResolveContentNameFromZonePicker();
        var job = ResolveJobFullName(_classJobId);
        var name = $"{content} {job}".Trim();
        if (name.Length > 80)
            name = name[..80];
        _title = name;
    }

    private static string FormatFightLabel(FFLogsFightInfo fight)
    {
        var kill = I18n.Get(fight.Kill ? "fflogs.fight.kill" : "fflogs.fight.wipe");
        var name = string.IsNullOrWhiteSpace(fight.Name)
            ? I18n.Format("fflogs.fight.unnamed", fight.Id)
            : fight.Name;
        return I18n.Format("fflogs.fight.label", fight.Id, name, kill);
    }

    private static string FormatPlayerLabel(FFLogsActorInfo player)
    {
        var job = string.IsNullOrWhiteSpace(player.SubType) ? "?" : player.SubType;
        var server = string.IsNullOrWhiteSpace(player.Server) ? string.Empty : $" @{player.Server}";
        return $"{player.Id} | {player.Name}{server} ({job})";
    }

    /// <summary>Content label from Oracle zone picker (Territory / CFC), not FFLogs fight name.</summary>
    private string ResolveContentNameFromZonePicker()
    {
        var label = !string.IsNullOrWhiteSpace(_zoneLabel)
            ? _zoneLabel
            : DutyContentCatalog.ResolveZoneLabel(
                _territoryTypeId,
                _contentFinderConditionId,
                _zoneClassJobLevel);

        var body = DutyContentCatalog.StripZoneLabelPrefix(label);
        // Drop trailing level suffix from duty picker labels.
        var levelMarker = I18n.Get("zone.level_marker");
        var dash = body.LastIndexOf(levelMarker, StringComparison.OrdinalIgnoreCase);
        if (dash > 0)
            body = body[..dash].Trim();
        return string.IsNullOrWhiteSpace(body) ? I18n.Get("fflogs.title.unknown") : body;
    }

    private static string ResolveJobFullName(uint classJobId)
    {
        if (classJobId == 0)
            return I18n.Get("fflogs.title.unknown");

        foreach (var job in JobActionCatalog.GetCombatJobs())
        {
            if (job.Id == classJobId)
                return string.IsNullOrWhiteSpace(job.Name) ? job.Abbreviation : job.Name;
        }

        return I18n.Format("job.unknown", classJobId);
    }

    private uint ResolveClassJobId(FFLogsActorInfo player)
    {
        if (_classJobId != 0)
            return _classJobId;
        var resolved = FFLogsImportService.ResolveClassJobId(player.SubType);
        if (resolved != 0)
            return resolved;
        return PluginServices.ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;
    }
}
