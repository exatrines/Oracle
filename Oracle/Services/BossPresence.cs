using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;

namespace Oracle.Services;

/// <summary>Visible and targetable battle NPCs by DataId (Auto Load AND match).</summary>
internal static class BossPresence
{
    public static bool IsLiveBattleNpc(IGameObject? obj)
    {
        if (obj == null || obj.ObjectKind != ObjectKind.BattleNpc)
            return false;
        if (obj.IsDead || !obj.IsTargetable || obj.DataId == 0)
            return false;
        return true;
    }

    public static bool AllTargetable(IReadOnlyList<uint> dataIds, HashSet<uint> live)
    {
        if (dataIds == null || dataIds.Count == 0 || live == null)
            return false;

        foreach (var id in dataIds)
        {
            if (id == 0 || !live.Contains(id))
                return false;
        }

        return true;
    }

    public static HashSet<uint> LiveDataIds()
    {
        var ids = new HashSet<uint>();
        foreach (var obj in PluginServices.ObjectTable)
        {
            if (!IsLiveBattleNpc(obj))
                continue;
            ids.Add(obj.DataId);
        }

        return ids;
    }

    public static List<string> LiveLabelsSorted()
    {
        var names = new Dictionary<uint, string>();
        foreach (var obj in PluginServices.ObjectTable)
        {
            if (!IsLiveBattleNpc(obj))
                continue;
            if (names.TryGetValue(obj.DataId, out var existing) && !string.IsNullOrWhiteSpace(existing))
                continue;

            var name = obj.Name.TextValue;
            names[obj.DataId] = string.IsNullOrWhiteSpace(name) ? string.Empty : name.Trim();
        }

        var ids = names.Keys.ToList();
        ids.Sort();
        var labels = new List<string>(ids.Count);
        foreach (var id in ids)
        {
            var name = names[id];
            labels.Add(string.IsNullOrWhiteSpace(name) ? id.ToString() : $"{id} {name}");
        }

        return labels;
    }
}
