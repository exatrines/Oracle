using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Oracle.Models;

namespace Oracle.Services;

/// <summary>
/// Draws a configurable highlight rectangle on visible hotbar slots that match highlighting timeline actions.
/// </summary>
internal sealed unsafe class HotbarHighlightService
{
    private static readonly string[] NormalActionBarAddonNames =
    [
        "_ActionBar",
        "_ActionBar01",
        "_ActionBar02",
        "_ActionBar03",
        "_ActionBar04",
        "_ActionBar05",
        "_ActionBar06",
        "_ActionBar07",
        "_ActionBar08",
        "_ActionBar09",
    ];

    private static readonly string[] CrossActionBarAddonNames =
    [
        "_ActionCross",
    ];

    private static readonly string[] DoubleCrossActionBarAddonNames =
    [
        "_ActionDoubleCrossL",
        "_ActionDoubleCrossR",
    ];

    private readonly TimelineEngine _engine;

    public HotbarHighlightService(TimelineEngine engine) => _engine = engine;

    public void Draw()
    {
        if (!C.ShowHotbarHighlight)
            return;

        if (!_engine.IsContextMatched)
            return;

        if (!PluginServices.ClientState.IsLoggedIn)
            return;

        // 1. Upcoming cues ↁEaction ids currently in hotbar highlight window
        var highlightByAction = CollectHighlightActions();
        if (highlightByAction.Count == 0)
            return;

        var hotbar = RaptureHotbarModule.Instance();
        if (hotbar == null)
            return;

        // 2. Map hotbar slots that hold those actions
        var matchingSlots = CollectMatchingSlots(hotbar, highlightByAction);
        if (matchingSlots.Count == 0)
            return;

        // 3. Draw rects on visible action bar addons
        var blinkPhaseOn = (DateTime.UtcNow.Millisecond / 250) % 2 == 0;
        var drawList = ImGui.GetForegroundDrawList();

        foreach (var addonName in NormalActionBarAddonNames)
            DrawAddonMatches(addonName, matchingSlots, drawList, blinkPhaseOn, requireCrossHotbarId: false);

        foreach (var addonName in CrossActionBarAddonNames)
            DrawAddonMatches(addonName, matchingSlots, drawList, blinkPhaseOn, requireCrossHotbarId: true);

        if (C.ShowHotbarHighlightDoubleCross)
        {
            foreach (var addonName in DoubleCrossActionBarAddonNames)
                DrawAddonMatches(addonName, matchingSlots, drawList, blinkPhaseOn, requireCrossHotbarId: true);
        }
    }

    private Dictionary<uint, bool> CollectHighlightActions()
    {
        var map = new Dictionary<uint, bool>();
        foreach (var item in _engine.GetUpcoming(C.LookaheadSeconds))
        {
            if (!item.IsHighlighting)
                continue;
            if (item.Cue.Kind != TimelineCueKind.Action)
                continue;
            if (item.Cue.ActionId == 0)
                continue;

            AddActionAndAdjusted(map, item.Cue.ActionId, item.IsPostHighlight);
        }

        return map;
    }

    private static void AddActionAndAdjusted(Dictionary<uint, bool> map, uint actionId, bool isPost)
    {
        SetHighlightPhase(map, actionId, isPost);

        var actionManager = ActionManager.Instance();
        if (actionManager == null)
            return;

        var adjusted = actionManager->GetAdjustedActionId(actionId);
        if (adjusted != 0)
            SetHighlightPhase(map, adjusted, isPost);
    }

    private static void SetHighlightPhase(Dictionary<uint, bool> map, uint actionId, bool isPost)
    {
        // Prefer after when the same action is both pre- and post-highlighted.
        if (map.TryGetValue(actionId, out var existing) && existing)
            return;
        map[actionId] = isPost;
    }

    private static Dictionary<(byte HotbarId, byte SlotId), bool> CollectMatchingSlots(
        RaptureHotbarModule* hotbar,
        Dictionary<uint, bool> highlightByAction)
    {
        var matches = new Dictionary<(byte, byte), bool>();
        var actionIds = highlightByAction.Keys.ToHashSet();

        // 0 E normal, 10 E7 cross
        for (byte hotbarId = 0; hotbarId < 18; hotbarId++)
        {
            if (!C.IsHotbarHighlightEnabled(hotbarId))
                continue;

            for (byte slotId = 0; slotId < 16; slotId++)
            {
                var slot = hotbar->GetSlotById(hotbarId, slotId);
                if (slot == null || slot->IsEmpty)
                    continue;

                if (!TryMatchSlotPhase(slot, highlightByAction, actionIds, out var isPost))
                    continue;

                var key = (hotbarId, slotId);
                if (matches.TryGetValue(key, out var existing) && existing)
                    continue;
                matches[key] = isPost;
            }
        }

        return matches;
    }

    private static void DrawAddonMatches(
        string addonName,
        Dictionary<(byte HotbarId, byte SlotId), bool> matchingSlots,
        ImDrawListPtr drawList,
        bool blinkPhaseOn,
        bool requireCrossHotbarId)
    {
        var addonHandle = PluginServices.GameGui.GetAddonByName(addonName, 1);
        if (addonHandle == nint.Zero)
            return;

        var addon = (AddonActionBarBase*)addonHandle.Address;
        if (addon == null || !addon->IsVisible || addon->RootNode == null || !addon->RootNode->IsVisible())
            return;

        var slotCount = Math.Min((int)addon->SlotCount, addon->ActionBarSlotVector.Count);
        if (slotCount <= 0)
            return;

        for (var i = 0; i < slotCount; i++)
        {
            ref var barSlot = ref addon->ActionBarSlotVector[i];
            if (!TryResolveHotbarId(addon, ref barSlot, requireCrossHotbarId, out var hotbarId))
                continue;

            if (!C.IsHotbarHighlightEnabled(hotbarId))
                continue;
            if (!matchingSlots.TryGetValue((hotbarId, (byte)i), out var isPost))
                continue;

            var blink = isPost ? C.ActionHighlightAfterBlink : C.ActionHighlightBeforeBlink;
            if (blink && !blinkPhaseOn)
                continue;

            if (!TryGetSlotScreenRect(ref barSlot, out var min, out var max))
                continue;

            var color = ImGui.ColorConvertFloat4ToU32(
                isPost ? C.ActionHighlightAfterLineColor : C.ActionHighlightBeforeLineColor);
            var thickness = Math.Max(
                1f,
                isPost ? C.ActionHighlightAfterLineThickness : C.ActionHighlightBeforeLineThickness);
            drawList.AddRect(min, max, color, 2f, ImDrawFlags.None, thickness);
        }
    }

    private static bool TryMatchSlotPhase(
        RaptureHotbarModule.HotbarSlot* slot,
        Dictionary<uint, bool> highlightByAction,
        HashSet<uint> actionIds,
        out bool isPost)
    {
        isPost = false;
        if (!SlotMatchesAction(slot, actionIds))
            return false;

        var found = false;

        if (slot->CommandId != 0 && highlightByAction.TryGetValue(slot->CommandId, out var postCmd))
        {
            isPost = postCmd;
            found = true;
        }

        if (slot->ApparentActionId != 0
            && highlightByAction.TryGetValue(slot->ApparentActionId, out var postApp)
            && (!found || postApp))
        {
            isPost = postApp;
            found = true;
        }

        var actionManager = ActionManager.Instance();
        if (actionManager != null && slot->CommandId != 0)
        {
            var adjusted = actionManager->GetAdjustedActionId(slot->CommandId);
            if (adjusted != 0
                && highlightByAction.TryGetValue(adjusted, out var postAdj)
                && (!found || postAdj))
            {
                isPost = postAdj;
                found = true;
            }
        }

        return found;
    }

    private static bool TryResolveHotbarId(
        AddonActionBarBase* addon,
        ref ActionBarSlot barSlot,
        bool requireCrossHotbarId,
        out byte hotbarId)
    {
        if (requireCrossHotbarId)
        {
            if (barSlot.HotbarId is >= 10 and <= 17)
            {
                hotbarId = (byte)barSlot.HotbarId;
                return true;
            }

            if (addon->RaptureHotbarId is >= 10 and <= 17)
            {
                hotbarId = addon->RaptureHotbarId;
                return true;
            }

            hotbarId = 0;
            return false;
        }

        hotbarId = addon->RaptureHotbarId;
        return hotbarId <= 9;
    }

    private static bool SlotMatchesAction(RaptureHotbarModule.HotbarSlot* slot, HashSet<uint> actionIds)
    {
        if (slot->CommandType != RaptureHotbarModule.HotbarSlotType.Action)
            return false;

        if (slot->CommandId != 0 && actionIds.Contains(slot->CommandId))
            return true;

        if (slot->ApparentActionId != 0 && actionIds.Contains(slot->ApparentActionId))
            return true;

        var actionManager = ActionManager.Instance();
        if (actionManager == null || slot->CommandId == 0)
            return false;

        var adjusted = actionManager->GetAdjustedActionId(slot->CommandId);
        return adjusted != 0 && actionIds.Contains(adjusted);
    }

    private static bool TryGetSlotScreenRect(ref ActionBarSlot barSlot, out Vector2 min, out Vector2 max)
    {
        min = default;
        max = default;

        var node = barSlot.IconFrame;
        if (node == null && barSlot.Icon != null)
            node = &barSlot.Icon->AtkResNode;

        if (node == null || !node->IsVisible())
            return false;

        var scaleX = 1f;
        var scaleY = 1f;
        for (var p = node; p != null; p = p->ParentNode)
        {
            scaleX *= p->ScaleX;
            scaleY *= p->ScaleY;
        }

        min = new Vector2(node->ScreenX, node->ScreenY);
        max = min + new Vector2(node->Width * scaleX, node->Height * scaleY);
        return max.X > min.X && max.Y > min.Y;
    }
}
