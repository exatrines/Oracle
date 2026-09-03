using Oracle.Models;

namespace Oracle.Services;

/// <summary>Timeline JSON files under the plugin config directory.</summary>
internal sealed class ConfigStore
{
    private readonly string _timelinesDir;

    public ConfigStore(IDalamudPluginInterface pluginInterface)
    {
        _timelinesDir = Path.Combine(pluginInterface.ConfigDirectory.FullName, "Timelines");
        Directory.CreateDirectory(_timelinesDir);
    }

    public string TimelinesDirectory => _timelinesDir;

    // --- Load all timelines from disk ---

    public IReadOnlyList<TimelineDocument> LoadAll()
    {
        var docs = new List<TimelineDocument>();
        foreach (var path in Directory.EnumerateFiles(_timelinesDir, "*.json"))
        {
            try
            {
                var doc = TimelineJson.TryLoadFile(path, out var dropped);
                if (doc == null)
                    continue;

                if (dropped > 0)
                    PluginServices.Log.Information("Ignored {Count} retired cue(s) in {Path}", dropped, path);
                docs.Add(doc);
            }
            catch (Exception ex)
            {
                PluginServices.Log.Warning(ex, "Failed to load timeline {Path}", path);
            }
        }

        return docs;
    }

    // --- Save / delete ---

    public void Save(TimelineDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var desiredStem = ToFileStem(document.Name);
        var stem = AllocateUniqueStem(desiredStem, document.Id);
        var newPath = Path.Combine(_timelinesDir, $"{stem}.json");
        var oldPath = Path.Combine(_timelinesDir, $"{SanitizeFileName(document.Id)}.json");

        document.Id = stem;
        SanitizeCueLabels(document);
        File.WriteAllText(newPath, TimelineJson.SerializeDocument(document));

        if (!string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase)
            && File.Exists(oldPath))
            File.Delete(oldPath);
    }

    private static void SanitizeCueLabels(TimelineDocument document)
    {
        foreach (var cue in document.Cues)
        {
            if (cue.Kind == TimelineCueKind.Action)
            {
                cue.Label = string.Empty;
                cue.Effected = false;
                cue.SyncType = default;
            }
            else
            {
                CueTargetCatalog.Clear(cue);
                if (cue.Kind != TimelineCueKind.Sync)
                {
                    cue.Effected = false;
                    cue.SyncType = default;
                }
            }
        }
    }

    public bool Delete(string documentId)
    {
        if (string.IsNullOrWhiteSpace(documentId))
            return false;

        var path = Path.Combine(_timelinesDir, $"{SanitizeFileName(documentId)}.json");
        if (!File.Exists(path))
            return false;

        File.Delete(path);
        return true;
    }

    internal static string ToFileStem(string? name)
    {
        var stem = (name ?? string.Empty).Trim();
        if (stem.Length == 0)
            stem = I18n.Get("config.default.untitled");

        stem = stem.Replace(' ', '-');
        foreach (var c in Path.GetInvalidFileNameChars())
            stem = stem.Replace(c, '_');

        return stem.Length == 0 ? I18n.Get("config.default.untitled") : stem;
    }

    private string AllocateUniqueStem(string desiredStem, string? currentId)
    {
        var currentStem = string.IsNullOrWhiteSpace(currentId)
            ? null
            : SanitizeFileName(currentId);

        var stem = desiredStem;
        var n = 1;
        while (true)
        {
            var path = Path.Combine(_timelinesDir, $"{stem}.json");
            if (!File.Exists(path)
                || (currentStem != null
                    && string.Equals(stem, currentStem, StringComparison.OrdinalIgnoreCase)))
                return stem;

            stem = $"{desiredStem}-{n++}";
        }
    }

    public bool IsStemTaken(string stem, string? exceptDocumentId = null)
    {
        if (string.IsNullOrWhiteSpace(stem))
            return true;

        var path = Path.Combine(_timelinesDir, $"{stem}.json");
        if (!File.Exists(path))
            return false;

        if (string.IsNullOrWhiteSpace(exceptDocumentId))
            return true;

        return !string.Equals(stem, SanitizeFileName(exceptDocumentId), StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeFileName(string id)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            id = id.Replace(c, '_');
        return id;
    }
}
