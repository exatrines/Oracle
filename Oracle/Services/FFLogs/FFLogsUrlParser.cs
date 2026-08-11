using System.Text.RegularExpressions;

namespace Oracle.Services.FFLogs;

internal readonly record struct FFLogsUrlParts(
    string Code,
    int? FightId,
    int? SourceId);

internal static partial class FFLogsUrlParser
{
    [GeneratedRegex(
        @"fflogs\.com/reports/(?<code>[A-Za-z0-9]{16,})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReportCodeRegex();

    public static bool TryParse(string? urlOrCode, out FFLogsUrlParts parts)
    {
        parts = default;
        if (string.IsNullOrWhiteSpace(urlOrCode))
            return false;

        var text = urlOrCode.Trim();
        string code;
        int? fightId = null;
        int? sourceId = null;

        var match = ReportCodeRegex().Match(text);
        if (match.Success)
        {
            code = match.Groups["code"].Value;
            var queryText = Uri.TryCreate(text, UriKind.Absolute, out var uri)
                ? uri.Query
                : ExtractQuerySuffix(text);
            var query = ParseQuery(queryText);
            if (query.TryGetValue("fight", out var fightRaw) && int.TryParse(fightRaw, out var fight))
                fightId = fight;
            if (query.TryGetValue("source", out var sourceRaw) && int.TryParse(sourceRaw, out var source))
                sourceId = source;
        }
        else if (text.All(char.IsLetterOrDigit) && text.Length is >= 16 and <= 32)
        {
            code = text;
        }
        else
        {
            return false;
        }

        parts = new FFLogsUrlParts(code, fightId, sourceId);
        return true;
    }

    private static string ExtractQuerySuffix(string text)
    {
        var qIndex = text.IndexOf('?', StringComparison.Ordinal);
        return qIndex >= 0 ? text[qIndex..] : string.Empty;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query))
            return result;

        var q = query.StartsWith('?') ? query[1..] : query;
        foreach (var pair in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq <= 0)
                continue;
            var key = Uri.UnescapeDataString(pair[..eq]);
            var value = Uri.UnescapeDataString(pair[(eq + 1)..]);
            result[key] = value;
        }

        return result;
    }
}
