using System.Text.Json;
using System.Text.Json.Serialization;
using Oracle.Models;

namespace Oracle.Services.AutoRecord;

/// <summary>Persists combat recordings under ConfigDirectory/AutoRecord (not shown in timeline list).</summary>
internal sealed class AutoRecordStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _directory;

    public AutoRecordStore(IDalamudPluginInterface pluginInterface)
    {
        _directory = Path.Combine(pluginInterface.ConfigDirectory.FullName, "AutoRecord");
        Directory.CreateDirectory(_directory);
    }

    public IReadOnlyList<string> ListFilesNewestFirst()
    {
        if (!Directory.Exists(_directory))
            return [];

        return Directory.EnumerateFiles(_directory, "*.json")
            .OrderByDescending(File.GetCreationTimeUtc)
            .ThenByDescending(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public TimelineDocument? TryLoad(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            var doc = JsonSerializer.Deserialize<TimelineDocument>(File.ReadAllText(path), JsonOptions);
            if (doc == null)
                return null;

            doc.Id = Path.GetFileNameWithoutExtension(path);
            return doc;
        }
        catch (Exception ex)
        {
            PluginServices.Log.Warning(ex, "Failed to load AutoRecord {Path}", path);
            return null;
        }
    }

    public string Save(TimelineDocument document, string fileStem, int maxFiles)
    {
        ArgumentNullException.ThrowIfNull(document);
        Directory.CreateDirectory(_directory);

        var stem = SanitizeStem(fileStem);
        if (string.IsNullOrWhiteSpace(stem))
            stem = I18n.Get("fflogs.title.unknown");

        var path = Path.Combine(_directory, $"{stem}.json");
        document.Id = stem;
        foreach (var cue in document.Cues)
        {
            if (cue.Kind == TimelineCueKind.Action)
                cue.Label = string.Empty;
        }

        File.WriteAllText(path, JsonSerializer.Serialize(document, JsonOptions));
        Prune(maxFiles);
        return path;
    }

    private void Prune(int maxFiles)
    {
        maxFiles = Math.Clamp(maxFiles, 1, 500);
        var files = ListFilesNewestFirst();
        for (var i = maxFiles; i < files.Count; i++)
        {
            try
            {
                File.Delete(files[i]);
            }
            catch (Exception ex)
            {
                PluginServices.Log.Warning(ex, "Failed to prune AutoRecord {Path}", files[i]);
            }
        }
    }

    private static string SanitizeStem(string stem)
    {
        var s = (stem ?? string.Empty).Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        s = s.Replace(' ', '-');
        while (s.Contains("--", StringComparison.Ordinal))
            s = s.Replace("--", "-", StringComparison.Ordinal);
        return s.Trim('-', '_');
    }
}
