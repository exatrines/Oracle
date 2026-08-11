using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Oracle.Models;

namespace Oracle.Services;

/// <summary>Timeline JSON files under the plugin config directory.</summary>
internal sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

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
                var json = File.ReadAllText(path);
                var doc = DeserializeDocument(json);
                if (doc == null)
                    continue;

                // Keep Id aligned with the on-disk file stem (name-based).
                doc.Id = Path.GetFileNameWithoutExtension(path);
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
        File.WriteAllText(newPath, JsonSerializer.Serialize(document, JsonOptions));

        if (!string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase)
            && File.Exists(oldPath))
            File.Delete(oldPath);
    }

    private static void SanitizeCueLabels(TimelineDocument document)
    {
        foreach (var cue in document.Cues)
        {
            if (cue.Kind == TimelineCueKind.Action)
                cue.Label = string.Empty;
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

    private static TimelineDocument? DeserializeDocument(string json)
    {
        var node = JsonNode.Parse(json);
        if (node is not JsonObject obj)
            return null;

        if ((obj["Cues"] is not JsonArray cues || cues.Count == 0) &&
            obj["Scenes"] is JsonArray { Count: > 0 } scenes &&
            scenes[0] is JsonObject firstScene &&
            firstScene["Cues"] is JsonArray legacyCues)
        {
            obj["Cues"] = legacyCues.DeepClone();
        }

        obj.Remove("Scenes");
        return obj.Deserialize<TimelineDocument>(JsonOptions);
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
