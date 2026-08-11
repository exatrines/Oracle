using Oracle.Models;

namespace Oracle.Services.FFLogs;

internal sealed class FFLogsImportOptions
{
    public string Name { get; init; } = string.Empty;
    public uint ClassJobId { get; init; }
    public uint TerritoryTypeId { get; init; }
    public uint ContentFinderConditionId { get; init; }
    public byte ClassJobLevel { get; init; }
    public uint SceneId { get; init; }
    public bool AutoLoadEnabled { get; init; } = true;
}

internal static class FFLogsImportService
{
    /// <summary>Build one cue per cast (no Import Action filter).</summary>
    public static List<TimelineCue> BuildAllCues(
        FFLogsFightInfo fight,
        IReadOnlyList<FFLogsCastEvent> casts)
    {
        var cues = new List<TimelineCue>(casts.Count);
        foreach (var cast in casts.OrderBy(c => c.Timestamp))
        {
            var offsetSec = (float)((cast.Timestamp - fight.StartTime) / 1000.0);
            offsetSec = MathF.Round(offsetSec);
            cues.Add(new TimelineCue
            {
                TimeOffsetSec = offsetSec,
                Kind = TimelineCueKind.Action,
                ActionId = cast.AbilityGameId,
            });
        }

        return cues;
    }

    public static TimelineDocument BuildDocument(
        string reportCode,
        FFLogsFightInfo fight,
        FFLogsActorInfo player,
        FFLogsImportOptions options,
        IReadOnlyList<TimelineCue> cues)
    {
        var classJobId = options.ClassJobId != 0
            ? options.ClassJobId
            : ResolveClassJobId(player.SubType);

        var name = string.IsNullOrWhiteSpace(options.Name)
            ? DefaultName(reportCode, fight, player)
            : options.Name.Trim();
        if (name.Length > 80)
            name = name[..80];

        return new TimelineDocument
        {
            Name = name,
            AutoLoadEnabled = options.AutoLoadEnabled,
            ClassJobId = classJobId,
            TerritoryTypeId = options.TerritoryTypeId,
            ContentFinderConditionId = options.ContentFinderConditionId,
            ClassJobLevel = options.ClassJobLevel,
            SceneId = options.SceneId,
            Cues = cues
                .Select(c => new TimelineCue
                {
                    TimeOffsetSec = c.TimeOffsetSec,
                    Kind = c.Kind,
                    ActionId = c.ActionId,
                    Label = c.Kind == TimelineCueKind.Memo ? c.Label : string.Empty,
                })
                .ToList(),
        };
    }

    private static string DefaultName(
        string reportCode,
        FFLogsFightInfo fight,
        FFLogsActorInfo player)
    {
        var playerLabel = string.IsNullOrWhiteSpace(player.Name)
            ? I18n.Format("fflogs.default_source", player.Id)
            : player.Name;
        var name = I18n.Format("fflogs.default_name", reportCode, fight.Id, playerLabel);
        return name.Length > 80 ? name[..80] : name;
    }

    public static IReadOnlyList<FFLogsActorInfo> PlayersForFight(
        FFLogsReportMeta meta,
        FFLogsFightInfo fight)
    {
        if (fight.FriendlyPlayers.Count == 0)
            return meta.Players;

        var allowed = fight.FriendlyPlayers.ToHashSet();
        var filtered = meta.Players.Where(p => allowed.Contains(p.Id)).ToList();
        return filtered.Count > 0 ? filtered : meta.Players;
    }

    /// <summary>Map FFLogs actor subType (e.g. Scholar) to ClassJob row id.</summary>
    public static uint ResolveClassJobId(string? subType)
    {
        if (string.IsNullOrWhiteSpace(subType))
            return 0;

        var key = subType.Trim();
        foreach (var job in JobActionCatalog.GetCombatJobs())
        {
            if (string.Equals(job.Name, key, StringComparison.OrdinalIgnoreCase)
                || string.Equals(job.Abbreviation, key, StringComparison.OrdinalIgnoreCase))
                return job.Id;
        }

        return key.ToLowerInvariant() switch
        {
            "paladin" => 19,
            "warrior" => 21,
            "darkknight" or "dark knight" => 32,
            "gunbreaker" => 37,
            "whitemage" or "white mage" => 24,
            "scholar" => 28,
            "astrologian" => 33,
            "sage" => 40,
            "monk" => 20,
            "dragoon" => 22,
            "ninja" => 30,
            "samurai" => 34,
            "reaper" => 39,
            "viper" => 41,
            "bard" => 23,
            "machinist" => 31,
            "dancer" => 38,
            "blackmage" or "black mage" => 25,
            "summoner" => 27,
            "redmage" or "red mage" => 35,
            "pictomancer" => 42,
            "bluemage" or "blue mage" => 36,
            _ => 0,
        };
    }
}
