using Oracle.Models;

namespace Oracle.Services;

/// <summary>Timeline document storage, sidebar order, and CRUD.</summary>
internal sealed class TimelineStore
{
    private readonly ConfigStore _configStore;
    private readonly List<TimelineDocument> _documents = [];

    public TimelineStore(ConfigStore configStore)
    {
        _configStore = configStore;
        Reload();
    }

    public string TimelinesDirectory => _configStore.TimelinesDirectory;

    public IReadOnlyList<TimelineDocument> Documents => _documents;

    public TimelineDocument? ActiveDocument { get; private set; }

    // --- Display names (new / copy) ---
    public string AllocateNewTimelineName()
    {
        var baseName = I18n.Get("config.default.new_timeline");
        if (!IsDisplayNameStemTaken(baseName, exceptDocumentId: null))
            return baseName;

        for (var n = 1; ; n++)
        {
            var candidate = $"{baseName} {n}";
            if (!IsDisplayNameStemTaken(candidate, exceptDocumentId: null))
                return candidate;
        }
    }

    public string AllocateCopyName(string sourceName)
    {
        var trimmed = string.IsNullOrWhiteSpace(sourceName)
            ? I18n.Get("config.default.untitled")
            : sourceName.Trim();
        var baseName = I18n.Format("config.default.copy_suffix", trimmed);
        if (!IsDisplayNameStemTaken(baseName, exceptDocumentId: null))
            return baseName;

        for (var n = 1; ; n++)
        {
            var candidate = $"{baseName} {n}";
            if (!IsDisplayNameStemTaken(candidate, exceptDocumentId: null))
                return candidate;
        }
    }

    public TimelineDocument? Duplicate(string sourceId)
    {
        var from = _documents.FindIndex(d =>
            string.Equals(d.Id, sourceId, StringComparison.OrdinalIgnoreCase));
        if (from < 0)
            return null;

        var source = _documents[from];
        var copy = new TimelineDocument
        {
            Name = AllocateCopyName(source.Name),
            AutoLoadEnabled = source.AutoLoadEnabled,
            LoadCommand = source.LoadCommand,
            TerritoryTypeId = source.TerritoryTypeId,
            ContentFinderConditionId = source.ContentFinderConditionId,
            SceneId = source.SceneId,
            ClassJobId = source.ClassJobId,
            ClassJobLevel = source.ClassJobLevel,
            Cues = source.Cues
                .Select(c => new TimelineCue
                {
                    TimeOffsetSec = c.TimeOffsetSec,
                    Kind = c.Kind,
                    ActionId = c.ActionId,
                    Label = c.Kind == TimelineCueKind.Memo ? c.Label : string.Empty,
                })
                .ToList(),
        };

        _configStore.Save(copy);
        _documents.Insert(from + 1, copy);
        PersistOrder();
        return copy;
    }

    // --- Load / save / delete ---

    public bool WouldFileNameConflict(string displayName, string? exceptDocumentId)
    {
        var stem = ConfigStore.ToFileStem(displayName);
        if (_documents.Any(d =>
                !string.Equals(d.Id, exceptDocumentId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(d.Id, stem, StringComparison.OrdinalIgnoreCase)))
            return true;

        return _configStore.IsStemTaken(stem, exceptDocumentId);
    }

    private bool IsDisplayNameStemTaken(string displayName, string? exceptDocumentId)
    {
        var stem = ConfigStore.ToFileStem(displayName);
        if (_documents.Any(d =>
                !string.Equals(d.Id, exceptDocumentId, StringComparison.OrdinalIgnoreCase)
                && (string.Equals(d.Id, stem, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(ConfigStore.ToFileStem(d.Name), stem, StringComparison.OrdinalIgnoreCase))))
            return true;

        return _configStore.IsStemTaken(stem, exceptDocumentId);
    }

    public void Reload()
    {
        _documents.Clear();
        _documents.AddRange(_configStore.LoadAll());
        ApplyConfigOrder(/* persistIfChanged */ true);
        ResolveActive(C.ActiveTimelineId);
    }

    public bool HasMatchConflict(TimelineDocument doc)
    {
        if (!doc.AutoLoadEnabled)
            return false;

        return _documents.Count(d => d.AutoLoadEnabled && SameMatchKey(d, doc)) > 1;
    }

    public IReadOnlyList<TimelineDocument> GetMatchConflictGroup(TimelineDocument doc) =>
        _documents.Where(d => d.AutoLoadEnabled && SameMatchKey(d, doc)).ToList();

    private static bool SameMatchKey(TimelineDocument a, TimelineDocument b) =>
        a.TerritoryTypeId == b.TerritoryTypeId
        && a.ClassJobId == b.ClassJobId
        && a.SceneId == b.SceneId;

    public void SetActive(string? id)
    {
        C.ActiveTimelineId = id ?? string.Empty;
        C.Save();
        ResolveActive(C.ActiveTimelineId);
    }

    public TimelineDocument? FindByLoadToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        token = token.Trim();

        var byCommand = _documents.FirstOrDefault(d =>
            !string.IsNullOrWhiteSpace(d.LoadCommand)
            && string.Equals(d.LoadCommand.Trim(), token, StringComparison.OrdinalIgnoreCase));
        if (byCommand != null)
            return byCommand;

        var byName = _documents.FirstOrDefault(d =>
            string.Equals(d.Name.Trim(), token, StringComparison.OrdinalIgnoreCase));
        if (byName != null)
            return byName;

        return _documents.FirstOrDefault(d =>
            string.Equals(d.Id, token, StringComparison.OrdinalIgnoreCase));
    }

    public static string GetEffectiveLoadCommand(TimelineDocument doc)
    {
        if (!string.IsNullOrWhiteSpace(doc.LoadCommand))
            return doc.LoadCommand.Trim();
        return string.IsNullOrWhiteSpace(doc.Name) ? doc.Id : doc.Name.Trim();
    }

    public void SaveDocument(TimelineDocument document)
    {
        var previousId = document.Id;
        _configStore.Save(document);

        var idx = _documents.FindIndex(d =>
            ReferenceEquals(d, document)
            || string.Equals(d.Id, previousId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(d.Id, document.Id, StringComparison.OrdinalIgnoreCase));
        var isNew = idx < 0;
        if (idx >= 0)
            _documents[idx] = document;
        else
            _documents.Add(document);

        SyncOrderId(previousId, document.Id, appendIfMissing: isNew);

        if (!string.Equals(previousId, document.Id, StringComparison.OrdinalIgnoreCase)
            && string.Equals(C.ActiveTimelineId, previousId, StringComparison.OrdinalIgnoreCase))
        {
            C.ActiveTimelineId = document.Id;
            C.Save();
        }

        if (ActiveDocument != null
            && (ReferenceEquals(ActiveDocument, document)
                || string.Equals(ActiveDocument.Id, previousId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(ActiveDocument.Id, document.Id, StringComparison.OrdinalIgnoreCase)))
            ActiveDocument = document;
    }

    public void Reorder(string id, int insertIndex)
    {
        var from = _documents.FindIndex(d =>
            string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
        if (from < 0)
            return;

        var doc = _documents[from];
        var zone = doc.TerritoryTypeId;
        var peers = _documents.Where(d => d.TerritoryTypeId == zone).ToList();
        var peerFrom = peers.FindIndex(d =>
            string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
        if (peerFrom < 0)
            return;

        peers.RemoveAt(peerFrom);
        if (insertIndex > peerFrom)
            insertIndex--;

        insertIndex = Math.Clamp(insertIndex, 0, peers.Count);
        peers.Insert(insertIndex, doc);

        RebuildDocumentsPreservingZoneBlocks(zone, peers);
        PersistOrder();
    }

    public void MoveToEndOfZone(string id)
    {
        var from = _documents.FindIndex(d =>
            string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
        if (from < 0)
            return;

        var doc = _documents[from];
        var zone = doc.TerritoryTypeId;
        var peers = _documents
            .Where(d => d.TerritoryTypeId == zone
                        && !string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase))
            .ToList();
        peers.Add(doc);

        RebuildDocumentsPreservingZoneBlocks(zone, peers);
        PersistOrder();
    }

    private void RebuildDocumentsPreservingZoneBlocks(
        uint zone,
        List<TimelineDocument> peersInOrder)
    {
        var others = new List<TimelineDocument>(_documents.Count);
        var insertAt = -1;
        for (var i = 0; i < _documents.Count; i++)
        {
            var d = _documents[i];
            if (d.TerritoryTypeId == zone)
            {
                if (insertAt < 0)
                    insertAt = others.Count;
                continue;
            }

            others.Add(d);
        }

        if (insertAt < 0)
            insertAt = others.Count;

        others.InsertRange(insertAt, peersInOrder);
        _documents.Clear();
        _documents.AddRange(others);
    }

    public bool DeleteDocument(string id)
    {
        if (!_configStore.Delete(id))
            return false;

        _documents.RemoveAll(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
        PersistOrder();
        if (ActiveDocument != null
            && string.Equals(ActiveDocument.Id, id, StringComparison.OrdinalIgnoreCase))
        {
            ActiveDocument = null;
            C.ActiveTimelineId = string.Empty;
            C.Save();
        }

        return true;
    }

    public TimelineDocument CreateNew(string name, uint classJobId = 0)
    {
        var doc = new TimelineDocument
        {
            Name = name,
            ClassJobId = classJobId,
            ClassJobLevel = 0,
            Cues =
            [
                new TimelineCue
                {
                    TimeOffsetSec = -15f,
                    Kind = TimelineCueKind.Action,
                    ActionId = 7531,
                },
            ],
        };
        SaveDocument(doc);
        return doc;
    }

    private void ApplyConfigOrder(bool persistIfChanged)
    {
        var byId = new Dictionary<string, TimelineDocument>(StringComparer.OrdinalIgnoreCase);
        foreach (var doc in _documents)
            byId[doc.Id] = doc;

        var ordered = new List<TimelineDocument>(_documents.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var id in C.TimelineOrder)
        {
            if (!byId.TryGetValue(id, out var doc) || !seen.Add(doc.Id))
                continue;
            ordered.Add(doc);
        }

        foreach (var doc in _documents
                     .Where(d => !seen.Contains(d.Id))
                     .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
            ordered.Add(doc);

        _documents.Clear();
        _documents.AddRange(ordered);

        if (!persistIfChanged || OrderEquals(C.TimelineOrder, _documents))
            return;

        PersistOrder();
    }

    private void SyncOrderId(string previousId, string newId, bool appendIfMissing)
    {
        var order = C.TimelineOrder;
        var index = order.FindIndex(id =>
            string.Equals(id, previousId, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
        {
            if (!string.Equals(order[index], newId, StringComparison.Ordinal))
            {
                order[index] = newId;
                C.Save();
            }

            return;
        }

        if (!appendIfMissing
            && order.Exists(id => string.Equals(id, newId, StringComparison.OrdinalIgnoreCase)))
            return;

        if (order.Exists(id => string.Equals(id, newId, StringComparison.OrdinalIgnoreCase)))
            return;

        order.Add(newId);
        C.Save();
    }

    private void PersistOrder()
    {
        C.TimelineOrder = _documents.Select(d => d.Id).ToList();
        C.Save();
    }

    private static bool OrderEquals(IReadOnlyList<string> order, IReadOnlyList<TimelineDocument> docs)
    {
        if (order.Count != docs.Count)
            return false;

        for (var i = 0; i < docs.Count; i++)
        {
            if (!string.Equals(order[i], docs[i].Id, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private void ResolveActive(string? id)
    {
        ActiveDocument = null;
        if (string.IsNullOrWhiteSpace(id))
            return;

        ActiveDocument = _documents.FirstOrDefault(d =>
            string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase));
    }
}
