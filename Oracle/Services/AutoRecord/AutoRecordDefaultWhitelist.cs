namespace Oracle.Services.AutoRecord;

/// <summary>Built-in default AutoRecord enabled zones (TerritoryType ids).</summary>
internal static class AutoRecordDefaultWhitelist
{
    public static IReadOnlyList<uint> TerritoryTypeIds { get; } =
    [
        733,
        777,
        887,
        968,
        1122,
        1196,
        1201,
        1226,
        1228,
        1230,
        1232,
        1238,
        1241,
        1243,
        1257,
        1259,
        1261,
        1263,
        1271,
        1296,
        1306,
        1308,
        1311,
        1321,
        1323,
        1325,
        1327,
        1362,
        1363,
    ];

    public static List<uint> CreateList() => TerritoryTypeIds.ToList();
}
