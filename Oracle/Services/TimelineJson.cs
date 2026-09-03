using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Oracle.Models;

namespace Oracle.Services;

/// <summary>Timeline JSON wire format (FormatVersion, Kind aliases, retired cues).</summary>
internal static class TimelineJson
{
    public const int CurrentFormatVersion = 1;

    public static JsonSerializerOptions FileOptions { get; } = CreateOptions(writeIndented: true);
    public static JsonSerializerOptions CompactOptions { get; } = CreateOptions(writeIndented: false);

    public static string SerializeDocument(TimelineDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.FormatVersion = CurrentFormatVersion;
        return JsonSerializer.Serialize(document, FileOptions);
    }

    public static TimelineDocument? TryLoadFile(string path, out int droppedCueCount)
    {
        droppedCueCount = 0;
        var json = File.ReadAllText(path);
        var doc = DeserializeDocument(json, out droppedCueCount);
        if (doc == null)
            return null;

        doc.Id = Path.GetFileNameWithoutExtension(path);
        return doc;
    }

    public static TimelineDocument? DeserializeDocument(string json, out int droppedCueCount)
    {
        droppedCueCount = 0;
        var node = JsonNode.Parse(json);
        if (node is not JsonObject obj)
            return null;

        PrepareObject(obj);
        droppedCueCount = DropRetiredCues(FindArray(obj, "Cues"));
        var hadVersion = FindProperty(obj, "FormatVersion") != null;
        var doc = obj.Deserialize<TimelineDocument>(FileOptions);
        if (doc == null)
            return null;

        if (!hadVersion || doc.FormatVersion < 1)
            doc.FormatVersion = 1;
        return doc;
    }

    public static T? DeserializePayload<T>(string json)
    {
        var node = JsonNode.Parse(json);
        if (node is JsonObject obj)
        {
            PrepareObject(obj);
            DropRetiredCues(FindArray(obj, "Cues"));
        }

        return node.Deserialize<T>(CompactOptions);
    }

    private static JsonSerializerOptions CreateOptions(bool writeIndented) =>
        new()
        {
            WriteIndented = writeIndented,
            PropertyNameCaseInsensitive = true,
            Converters =
            {
                new TimelineCueKindConverter(),
                new EnemySyncTypeConverter(),
                new JsonStringEnumConverter(),
            },
        };

    private static void PrepareObject(JsonObject obj)
    {
        var cues = FindArray(obj, "Cues");
        if (cues is not { Count: > 0 })
        {
            var scenes = FindArray(obj, "Scenes");
            if (scenes is { Count: > 0 } && scenes[0] is JsonObject firstScene)
            {
                var legacyCues = FindArray(firstScene, "Cues");
                if (legacyCues is { Count: > 0 })
                    SetProperty(obj, "Cues", legacyCues.DeepClone());
            }
        }

        RemoveProperty(obj, "Scenes");
    }

    private static int DropRetiredCues(JsonArray? cues)
    {
        if (cues == null)
            return 0;

        var dropped = 0;
        for (var i = cues.Count - 1; i >= 0; i--)
        {
            if (cues[i] is not JsonObject cue)
            {
                cues.RemoveAt(i);
                dropped++;
                continue;
            }

            var kind = FindProperty(cue, "Kind");
            if (kind == null)
                continue;
            if (TimelineCueKindConverter.IsLiveKind(kind))
                continue;

            cues.RemoveAt(i);
            dropped++;
        }

        return dropped;
    }

    private static JsonArray? FindArray(JsonObject obj, string name) =>
        FindProperty(obj, name) as JsonArray;

    private static JsonNode? FindProperty(JsonObject obj, string name)
    {
        foreach (var kv in obj)
        {
            if (string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        }

        return null;
    }

    private static void SetProperty(JsonObject obj, string name, JsonNode? value)
    {
        foreach (var kv in obj.ToList())
        {
            if (!string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase))
                continue;
            obj[kv.Key] = value;
            return;
        }

        obj[name] = value;
    }

    private static void RemoveProperty(JsonObject obj, string name)
    {
        foreach (var kv in obj.ToList())
        {
            if (string.Equals(kv.Key, name, StringComparison.OrdinalIgnoreCase))
                obj.Remove(kv.Key);
        }
    }
}

/// <summary>Kind wire: PascalCase names or numbers (0/1/4). Write Sync. 2 and 3 are retired.</summary>
internal sealed class TimelineCueKindConverter : JsonConverter<TimelineCueKind>
{
    public override TimelineCueKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (TryRead(ref reader, out var kind))
            return kind;

        throw new JsonException("Unknown timeline cue Kind.");
    }

    public override void Write(Utf8JsonWriter writer, TimelineCueKind value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            TimelineCueKind.Memo => "Memo",
            TimelineCueKind.Sync => "Sync",
            _ => "Action",
        });
    }

    public static bool IsLiveKind(JsonNode node)
    {
        if (node is not JsonValue value)
            return false;
        if (value.TryGetValue<int>(out var n))
            return IsLiveNumber(n);
        if (value.TryGetValue<long>(out var l) && l is >= int.MinValue and <= int.MaxValue)
            return IsLiveNumber((int)l);
        if (value.TryGetValue<string>(out var s))
            return TryParseNameOrNumber(s, out _);
        return false;
    }

    private static bool TryRead(ref Utf8JsonReader reader, out TimelineCueKind kind)
    {
        kind = TimelineCueKind.Action;
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return reader.TryGetInt32(out var n) && TryFromNumber(n, out kind);
            case JsonTokenType.String:
                return TryParseNameOrNumber(reader.GetString(), out kind);
            default:
                return false;
        }
    }

    private static bool TryParseNameOrNumber(string? text, out TimelineCueKind kind)
    {
        kind = TimelineCueKind.Action;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
            return TryFromNumber(n, out kind);

        if (text.Equals("Action", StringComparison.OrdinalIgnoreCase))
            return true;
        if (text.Equals("Memo", StringComparison.OrdinalIgnoreCase))
        {
            kind = TimelineCueKind.Memo;
            return true;
        }

        if (text.Equals("Sync", StringComparison.OrdinalIgnoreCase)
            || text.Equals("EnemyCastSync", StringComparison.OrdinalIgnoreCase))
        {
            kind = TimelineCueKind.Sync;
            return true;
        }

        return false;
    }

    private static bool TryFromNumber(int n, out TimelineCueKind kind)
    {
        switch (n)
        {
            case 0:
                kind = TimelineCueKind.Action;
                return true;
            case 1:
                kind = TimelineCueKind.Memo;
                return true;
            case 4:
                kind = TimelineCueKind.Sync;
                return true;
            default:
                kind = TimelineCueKind.Action;
                return false;
        }
    }

    private static bool IsLiveNumber(int n) => n is 0 or 1 or 4;
}

/// <summary>SyncType wire: Cast/Status names or 0/1.</summary>
internal sealed class EnemySyncTypeConverter : JsonConverter<EnemySyncType>
{
    public override EnemySyncType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                return reader.TryGetInt32(out var n) && n == 1
                    ? EnemySyncType.Status
                    : EnemySyncType.Cast;
            case JsonTokenType.String:
                var text = reader.GetString()?.Trim() ?? string.Empty;
                if (text.Equals("Status", StringComparison.OrdinalIgnoreCase)
                    || text.Equals("1", StringComparison.Ordinal))
                    return EnemySyncType.Status;
                return EnemySyncType.Cast;
            default:
                return EnemySyncType.Cast;
        }
    }

    public override void Write(Utf8JsonWriter writer, EnemySyncType value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value == EnemySyncType.Status ? "Status" : "Cast");
}
