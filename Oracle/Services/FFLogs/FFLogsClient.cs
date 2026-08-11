using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Oracle.Services.FFLogs;

internal sealed class FFLogsFightInfo
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public double StartTime { get; init; }
    public double EndTime { get; init; }
    public bool Kill { get; init; }
    public IReadOnlyList<int> FriendlyPlayers { get; init; } = [];

    /// <summary>FFLogs <c>gameZone.id</c> (FFXIV TerritoryType id when present).</summary>
    public int GameZoneId { get; init; }

    public string GameZoneName { get; init; } = string.Empty;
}

internal sealed class FFLogsActorInfo
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string SubType { get; init; } = string.Empty;
    public string Server { get; init; } = string.Empty;
}

internal sealed class FFLogsCastEvent
{
    public double Timestamp { get; init; }
    public uint AbilityGameId { get; init; }
}

internal sealed class FFLogsReportMeta
{
    public string Title { get; init; } = string.Empty;
    public IReadOnlyList<FFLogsFightInfo> Fights { get; init; } = [];
    public IReadOnlyList<FFLogsActorInfo> Players { get; init; } = [];
}

internal sealed class FFLogsClient : IDisposable
{
    private const string TokenUrl = "https://www.fflogs.com/oauth/token";
    private const string GraphqlUrl = "https://www.fflogs.com/api/v2/client";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http = new();
    private string? _accessToken;
    private DateTime _tokenExpiresUtc = DateTime.MinValue;
    private string? _tokenClientId;
    private string? _tokenClientSecret;

    public void Dispose() => _http.Dispose();

    public async Task<FFLogsReportMeta> GetReportMetaAsync(
        string code,
        string clientId,
        string clientSecret,
        CancellationToken ct = default)
    {
        await EnsureTokenAsync(clientId, clientSecret, ct).ConfigureAwait(false);

        const string query = """
            query($code: String!) {
              reportData {
                report(code: $code) {
                  title
                  fights {
                    id
                    name
                    startTime
                    endTime
                    kill
                    friendlyPlayers
                    gameZone {
                      id
                      name
                    }
                  }
                  masterData {
                    actors(type: "Player") {
                      id
                      name
                      subType
                      server
                    }
                  }
                }
              }
            }
            """;

        var root = await PostGraphqlAsync(
            query,
            new { code },
            ct).ConfigureAwait(false);

        var report = root?["data"]?["reportData"]?["report"] as JsonObject
            ?? throw new InvalidOperationException(I18n.Get("fflogs.err.report_not_found"));

        var fights = new List<FFLogsFightInfo>();
        if (report["fights"] is JsonArray fightArr)
        {
            foreach (var node in fightArr.OfType<JsonObject>())
            {
                var friendly = new List<int>();
                if (node["friendlyPlayers"] is JsonArray fp)
                {
                    foreach (var idNode in fp)
                    {
                        if (idNode != null && int.TryParse(idNode.ToString(), out var pid))
                            friendly.Add(pid);
                    }
                }

                fights.Add(new FFLogsFightInfo
                {
                    Id = node["id"]?.GetValue<int>() ?? 0,
                    Name = node["name"]?.GetValue<string>() ?? string.Empty,
                    StartTime = node["startTime"]?.GetValue<double>() ?? 0,
                    EndTime = node["endTime"]?.GetValue<double>() ?? 0,
                    Kill = node["kill"]?.GetValue<bool>() ?? false,
                    FriendlyPlayers = friendly,
                    GameZoneId = node["gameZone"]?["id"]?.GetValue<int>() ?? 0,
                    GameZoneName = node["gameZone"]?["name"]?.GetValue<string>() ?? string.Empty,
                });
            }
        }

        var players = new List<FFLogsActorInfo>();
        if (report["masterData"]?["actors"] is JsonArray actors)
        {
            foreach (var node in actors.OfType<JsonObject>())
            {
                players.Add(new FFLogsActorInfo
                {
                    Id = node["id"]?.GetValue<int>() ?? 0,
                    Name = node["name"]?.GetValue<string>() ?? string.Empty,
                    SubType = node["subType"]?.GetValue<string>() ?? string.Empty,
                    Server = node["server"]?.GetValue<string>() ?? string.Empty,
                });
            }
        }

        return new FFLogsReportMeta
        {
            Title = report["title"]?.GetValue<string>() ?? string.Empty,
            Fights = fights.Where(f => f.Id > 0).ToList(),
            Players = players.Where(p => p.Id > 0).ToList(),
        };
    }

    public async Task<IReadOnlyList<FFLogsCastEvent>> GetCastsAsync(
        string code,
        int fightId,
        int sourceId,
        double fightStartTime,
        double fightEndTime,
        string clientId,
        string clientSecret,
        CancellationToken ct = default)
    {
        await EnsureTokenAsync(clientId, clientSecret, ct).ConfigureAwait(false);

        const string query = """
            query(
              $code: String!
              $fightIDs: [Int]!
              $sourceID: Int!
              $startTime: Float
              $endTime: Float
            ) {
              reportData {
                report(code: $code) {
                  events(
                    dataType: Casts
                    fightIDs: $fightIDs
                    sourceID: $sourceID
                    startTime: $startTime
                    endTime: $endTime
                    limit: 10000
                  ) {
                    nextPageTimestamp
                    data
                  }
                }
              }
            }
            """;

        var all = new List<FFLogsCastEvent>();
        double? pageStart = fightStartTime;
        const int maxPages = 50;

        for (var page = 0; page < maxPages; page++)
        {
            var root = await PostGraphqlAsync(
                query,
                new
                {
                    code,
                    fightIDs = new[] { fightId },
                    sourceID = sourceId,
                    startTime = pageStart,
                    endTime = fightEndTime,
                },
                ct).ConfigureAwait(false);

            var eventsNode = root?["data"]?["reportData"]?["report"]?["events"] as JsonObject
                ?? throw new InvalidOperationException(I18n.Get("fflogs.err.casts_failed"));

            var dataNode = eventsNode["data"];
            foreach (var cast in ParseCastEvents(dataNode))
                all.Add(cast);

            var next = eventsNode["nextPageTimestamp"];
            if (next == null || next.GetValueKind() == JsonValueKind.Null)
                break;

            pageStart = next.GetValue<double>();
        }

        return all;
    }

    private static IEnumerable<FFLogsCastEvent> ParseCastEvents(JsonNode? dataNode)
    {
        JsonArray? array = null;
        if (dataNode is JsonArray direct)
            array = direct;
        else if (dataNode is JsonValue value && value.TryGetValue<string>(out var jsonText)
                 && !string.IsNullOrWhiteSpace(jsonText))
        {
            array = JsonNode.Parse(jsonText) as JsonArray;
        }

        if (array == null)
            yield break;

        foreach (var node in array.OfType<JsonObject>())
        {
            var type = node["type"]?.GetValue<string>() ?? string.Empty;
            // Prefer completed casts; skip begincast / other noise when present.
            if (!string.IsNullOrEmpty(type)
                && !string.Equals(type, "cast", StringComparison.OrdinalIgnoreCase))
                continue;

            var ability = node["abilityGameID"]?.GetValue<uint>()
                ?? node["abilityGameId"]?.GetValue<uint>()
                ?? 0u;
            if (ability == 0)
                continue;

            var ts = node["timestamp"]?.GetValue<double>() ?? 0;
            yield return new FFLogsCastEvent
            {
                Timestamp = ts,
                AbilityGameId = ability,
            };
        }
    }

    private async Task EnsureTokenAsync(string clientId, string clientSecret, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            throw new InvalidOperationException(I18n.Get("fflogs.err.not_configured"));

        if (_accessToken != null
            && string.Equals(_tokenClientId, clientId, StringComparison.Ordinal)
            && string.Equals(_tokenClientSecret, clientSecret, StringComparison.Ordinal)
            && DateTime.UtcNow < _tokenExpiresUtc.AddMinutes(-1))
            return;

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
        });

        using var response = await _http.PostAsync(TokenUrl, content, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                I18n.Format("fflogs.err.oauth_failed", (int)response.StatusCode, TrimError(body)));

        var token = JsonSerializer.Deserialize<TokenResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException(I18n.Get("fflogs.err.oauth_empty"));

        if (string.IsNullOrWhiteSpace(token.AccessToken))
            throw new InvalidOperationException(I18n.Get("fflogs.err.oauth_no_token"));

        _accessToken = token.AccessToken;
        _tokenClientId = clientId;
        _tokenClientSecret = clientSecret;
        _tokenExpiresUtc = DateTime.UtcNow.AddSeconds(token.ExpiresIn > 0 ? token.ExpiresIn : 3600);
    }

    private async Task<JsonNode?> PostGraphqlAsync(string query, object variables, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_accessToken))
            throw new InvalidOperationException(I18n.Get("fflogs.err.token_missing"));

        var payload = JsonSerializer.Serialize(new { query, variables });
        using var request = new HttpRequestMessage(HttpMethod.Post, GraphqlUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                I18n.Format("fflogs.err.graphql_http", (int)response.StatusCode, TrimError(body)));

        var root = JsonNode.Parse(body);
        if (root?["errors"] is JsonArray errors && errors.Count > 0)
        {
            var msg = errors[0]?["message"]?.GetValue<string>() ?? errors.ToJsonString();
            throw new InvalidOperationException(I18n.Format("fflogs.err.graphql", msg));
        }

        return root;
    }

    private static string TrimError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return I18n.Get("fflogs.err.empty_body");
        body = body.Trim();
        return body.Length <= 240 ? body : body[..240] + "…";
    }

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
