using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Oracle.Services.FFLogs;

internal sealed class FFLogsClient : IDisposable
{
    private const string TokenUrl = "https://www.fflogs.com/oauth/token";
    private const string GraphqlUrlEn = "https://www.fflogs.com/api/v2/client";
    private const string GraphqlUrlJa = "https://ja.fflogs.com/api/v2/client";
    private const int MaxEventPages = 50;

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
                  masterData(translate: true) {
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

        var root = await PostGraphqlAsync(query, new { code }, ct).ConfigureAwait(false);
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
        await FetchEventPagesAsync(
            query,
            start => new
            {
                code,
                fightIDs = new[] { fightId },
                sourceID = sourceId,
                startTime = start,
                endTime = fightEndTime,
            },
            "fflogs.err.casts_failed",
            data =>
            {
                foreach (var cast in ParseCastEvents(data))
                    all.Add(cast);
            },
            fightStartTime,
            ct).ConfigureAwait(false);
        return all;
    }

    public async Task<IReadOnlyList<FFLogsCastEvent>> GetEnemyCastsAsync(
        string code,
        int fightId,
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
              $startTime: Float
              $endTime: Float
            ) {
              reportData {
                report(code: $code) {
                  events(
                    dataType: Casts
                    fightIDs: $fightIDs
                    hostilityType: Enemies
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
        await FetchEventPagesAsync(
            query,
            start => new
            {
                code,
                fightIDs = new[] { fightId },
                startTime = start,
                endTime = fightEndTime,
            },
            "fflogs.err.casts_failed",
            data =>
            {
                foreach (var cast in ParseCastEvents(data, includeBeginCast: true))
                    all.Add(cast);
            },
            fightStartTime,
            ct).ConfigureAwait(false);
        return all;
    }

    public async Task<IReadOnlyList<FFLogsDamageHit>> GetDamageTakenAsync(
        string code,
        int fightId,
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
              $startTime: Float
              $endTime: Float
            ) {
              reportData {
                report(code: $code) {
                  events(
                    dataType: DamageTaken
                    fightIDs: $fightIDs
                    hostilityType: Friendlies
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

        var hits = new List<FFLogsDamageHit>();
        await FetchEventPagesAsync(
            query,
            start => new
            {
                code,
                fightIDs = new[] { fightId },
                startTime = start,
                endTime = fightEndTime,
            },
            "fflogs.err.damage_taken_failed",
            data =>
            {
                foreach (var hit in ParseDamageHits(data))
                    hits.Add(hit);
            },
            fightStartTime,
            ct).ConfigureAwait(false);

        Dictionary<uint, string> abilityNames;
        try
        {
            abilityNames = await FetchReportAbilityNamesAsync(code, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            PluginServices.Log.Warning(ex, "FFLogs ability names failed");
            abilityNames = [];
        }

        OverlayAbilityNames(hits, abilityNames);
        return hits;
    }

    public async Task<IReadOnlyList<FFLogsStatusEvent>> GetStatusEventsAsync(
        string code,
        int fightId,
        double fightStartTime,
        double fightEndTime,
        IReadOnlyList<uint> statusGameIds,
        string clientId,
        string clientSecret,
        CancellationToken ct = default)
    {
        if (statusGameIds.Count == 0)
            return [];

        await EnsureTokenAsync(clientId, clientSecret, ct).ConfigureAwait(false);

        const string query = """
            query(
              $code: String!
              $fightIDs: [Int]!
              $startTime: Float
              $endTime: Float
              $dataType: EventDataType!
              $filter: String
            ) {
              reportData {
                report(code: $code) {
                  events(
                    dataType: $dataType
                    fightIDs: $fightIDs
                    startTime: $startTime
                    endTime: $endTime
                    filterExpression: $filter
                    limit: 10000
                  ) {
                    nextPageTimestamp
                    data
                  }
                }
              }
            }
            """;

        var filter = BuildStatusAbilityFilter(statusGameIds);
        var all = new List<FFLogsStatusEvent>();
        foreach (var dataType in new[] { "Buffs", "Debuffs" })
        {
            await FetchEventPagesAsync(
                query,
                start => new
                {
                    code,
                    fightIDs = new[] { fightId },
                    startTime = start,
                    endTime = fightEndTime,
                    dataType,
                    filter,
                },
                "fflogs.err.buffs_failed",
                data =>
                {
                    foreach (var ev in ParseStatusEvents(data))
                        all.Add(ev);
                },
                fightStartTime,
                ct).ConfigureAwait(false);
        }

        return all;
    }

    private async Task FetchEventPagesAsync(
        string query,
        Func<double?, object> variables,
        string errorKey,
        Action<JsonNode?> consume,
        double startTime,
        CancellationToken ct)
    {
        double? pageStart = startTime;
        for (var page = 0; page < MaxEventPages; page++)
        {
            var root = await PostGraphqlAsync(query, variables(pageStart), ct).ConfigureAwait(false);
            var eventsNode = root?["data"]?["reportData"]?["report"]?["events"] as JsonObject
                ?? throw new InvalidOperationException(I18n.Get(errorKey));
            consume(eventsNode["data"]);
            if (!TryReadNextPage(eventsNode["nextPageTimestamp"], out pageStart))
                break;
        }
    }

    private async Task<Dictionary<uint, string>> FetchReportAbilityNamesAsync(
        string code,
        CancellationToken ct)
    {
        const string query = """
            query($code: String!) {
              reportData {
                report(code: $code) {
                  masterData(translate: true) {
                    abilities {
                      gameID
                      name
                    }
                  }
                }
              }
            }
            """;

        var names = new Dictionary<uint, string>();
        var root = await PostGraphqlAsync(query, new { code }, ct).ConfigureAwait(false);
        if (root?["data"]?["reportData"]?["report"]?["masterData"]?["abilities"] is not JsonArray abilities)
            return names;

        foreach (var node in abilities.OfType<JsonObject>())
        {
            var gameId = ReadUInt(node, "gameID", "gameId");
            if (gameId == 0)
                continue;

            var name = node["name"]?.GetValue<string>()?.Trim() ?? string.Empty;
            if (ActionLookup.IsPlaceholderName(name))
                continue;

            names[gameId] = name;
        }

        return names;
    }

    private static IEnumerable<FFLogsCastEvent> ParseCastEvents(
        JsonNode? dataNode,
        bool includeBeginCast = false)
    {
        var array = ReadEventArray(dataNode);
        if (array == null)
            yield break;

        foreach (var node in array.OfType<JsonObject>())
        {
            var type = node["type"]?.GetValue<string>() ?? string.Empty;
            if (!string.IsNullOrEmpty(type)
                && !string.Equals(type, "cast", StringComparison.OrdinalIgnoreCase)
                && !(includeBeginCast
                    && string.Equals(type, "begincast", StringComparison.OrdinalIgnoreCase)))
                continue;

            var ability = ReadUInt(node, "abilityGameID", "abilityGameId");
            if (ability == 0)
                continue;

            yield return new FFLogsCastEvent
            {
                Timestamp = node["timestamp"]?.GetValue<double>() ?? 0,
                AbilityGameId = ability,
                TargetId = ReadTargetId(node),
                Type = type,
            };
        }
    }

    private static IEnumerable<FFLogsStatusEvent> ParseStatusEvents(JsonNode? dataNode)
    {
        var array = ReadEventArray(dataNode);
        if (array == null)
            yield break;

        foreach (var node in array.OfType<JsonObject>())
        {
            var type = node["type"]?.GetValue<string>() ?? string.Empty;
            if (!TryParseStatusEdge(type, out var removed))
                continue;

            var ability = ReadAbilityGameId(node);
            var statusId = ToGameStatusId(ability);
            if (statusId == 0)
                continue;

            yield return new FFLogsStatusEvent
            {
                Timestamp = node["timestamp"]?.GetValue<double>() ?? 0,
                StatusId = statusId,
                Removed = removed,
                Type = type,
                SourceIsFriendly = ReadBool(node, "sourceIsFriendly"),
            };
        }
    }

    private static IEnumerable<FFLogsDamageHit> ParseDamageHits(JsonNode? dataNode)
    {
        var array = ReadEventArray(dataNode);
        if (array == null)
            yield break;

        foreach (var node in array.OfType<JsonObject>())
        {
            var ability = ReadUInt(node, "abilityGameID", "abilityGameId");
            if (ability == 0)
                continue;

            yield return new FFLogsDamageHit
            {
                Timestamp = node["timestamp"]?.GetValue<double>() ?? 0,
                AbilityGameId = ability,
                AbilityName = ReadAbilityName(node),
                Type = node["type"]?.GetValue<string>() ?? string.Empty,
                Tick = ReadBool(node, "tick") ?? false,
                SourceIsFriendly = ReadBool(node, "sourceIsFriendly"),
                TargetIsFriendly = ReadBool(node, "targetIsFriendly"),
            };
        }
    }

    private static string ReadAbilityName(JsonObject node)
    {
        var nested = node["ability"]?["name"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(nested))
            return nested.Trim();

        var flat = node["abilityName"]?.GetValue<string>();
        return string.IsNullOrWhiteSpace(flat) ? string.Empty : flat.Trim();
    }

    private static void OverlayAbilityNames(
        List<FFLogsDamageHit> hits,
        IReadOnlyDictionary<uint, string> abilityNames)
    {
        if (abilityNames.Count == 0)
            return;

        for (var i = 0; i < hits.Count; i++)
        {
            var hit = hits[i];
            if (abilityNames.TryGetValue(hit.AbilityGameId, out var mapped)
                && !ActionLookup.IsPlaceholderName(mapped))
                hits[i] = hit with { AbilityName = mapped };
        }
    }

    private static int ReadTargetId(JsonObject node) =>
        ReadInt(node, "targetID", "targetId");

    private static int ReadInt(JsonObject node, params string[] names)
    {
        foreach (var name in names)
        {
            var raw = node[name];
            if (raw == null || raw.GetValueKind() == JsonValueKind.Null)
                continue;

            try
            {
                return raw.GetValueKind() switch
                {
                    JsonValueKind.Number => (int)raw.GetValue<double>(),
                    JsonValueKind.String when int.TryParse(raw.GetValue<string>(), out var parsed) => parsed,
                    _ => raw.GetValue<int>(),
                };
            }
            catch (InvalidOperationException)
            {
            }
            catch (FormatException)
            {
            }
        }

        return 0;
    }

    private static bool? ReadBool(JsonObject node, string name)
    {
        var raw = node[name];
        if (raw == null || raw.GetValueKind() == JsonValueKind.Null)
            return null;

        try
        {
            return raw.GetValue<bool>();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
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

    private async Task<JsonNode?> PostGraphqlAsync(
        string query,
        object variables,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_accessToken))
            throw new InvalidOperationException(I18n.Get("fflogs.err.token_missing"));

        var payload = JsonSerializer.Serialize(new { query, variables });
        using var request = new HttpRequestMessage(HttpMethod.Post, ResolveGraphqlUrl());
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

    private static string ResolveGraphqlUrl() =>
        string.Equals(I18n.CurrentLang, "ja", StringComparison.OrdinalIgnoreCase)
            ? GraphqlUrlJa
            : GraphqlUrlEn;

    private static bool TryReadNextPage(JsonNode? next, out double? pageStart)
    {
        pageStart = null;
        if (next == null || next.GetValueKind() == JsonValueKind.Null)
            return false;

        pageStart = next.GetValue<double>();
        return true;
    }

    private static uint ReadUInt(JsonObject node, string name, string alt) =>
        node[name]?.GetValue<uint>() ?? node[alt]?.GetValue<uint>() ?? 0u;

    private static uint ReadAbilityGameId(JsonObject node)
    {
        var id = ReadUInt(node, "abilityGameID", "abilityGameId");
        if (id != 0)
            return id;

        if (node["ability"] is not JsonObject ability)
            return 0;

        id = ReadUInt(ability, "guid", "id");
        if (id != 0)
            return id;

        return ReadUInt(ability, "gameID", "gameId");
    }

    private static uint ToGameStatusId(uint abilityId) =>
        abilityId >= 1_000_000 ? abilityId - 1_000_000 : abilityId;

    private static uint ToFflogsStatusAbilityId(uint statusId) =>
        statusId >= 1_000_000 ? statusId : statusId + 1_000_000;

    private static string BuildStatusAbilityFilter(IReadOnlyList<uint> statusGameIds)
    {
        var ids = statusGameIds
            .Where(id => id != 0)
            .Select(ToFflogsStatusAbilityId)
            .Distinct()
            .ToList();
        if (ids.Count == 1)
            return $"ability.id = {ids[0]}";
        return $"ability.id in ({string.Join(", ", ids)})";
    }

    private static bool TryParseStatusEdge(string type, out bool removed)
    {
        removed = false;
        if (string.IsNullOrWhiteSpace(type))
            return false;

        if (type.Contains("remove", StringComparison.OrdinalIgnoreCase))
        {
            removed = true;
            return true;
        }

        if (type.Contains("refresh", StringComparison.OrdinalIgnoreCase))
            return false;

        return type.Contains("apply", StringComparison.OrdinalIgnoreCase);
    }

    private static JsonArray? ReadEventArray(JsonNode? dataNode)
    {
        if (dataNode is JsonArray direct)
            return direct;
        if (dataNode is JsonValue value
            && value.TryGetValue<string>(out var jsonText)
            && !string.IsNullOrWhiteSpace(jsonText))
            return JsonNode.Parse(jsonText) as JsonArray;

        return null;
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
