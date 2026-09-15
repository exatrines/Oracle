using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Oracle.Services;

/// <summary>
/// Draws a highlight rectangle on visible hotbar slots that match highlighting timeline actions.
/// </summary>
internal sealed unsafe class HotbarHighlightService
{
    private const byte CrossHotbarIdMin = 10;
    private const byte CrossHotbarIdMax = 17;

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

    private static readonly string[] WxhbAddonNames =
    [
        "_ActionDoubleCrossL",
        "_ActionDoubleCrossR",
    ];

    private readonly TimelineEngine _engine;

    public HotbarHighlightFrame LastFrame { get; private set; } = new();

    public HotbarHighlightService(TimelineEngine engine) => _engine = engine;

    public void Draw(bool captureDebug)
    {
        var frame = new HotbarHighlightFrame();
        var visibleMarks = new Dictionary<(byte HotbarId, byte SlotId), HotbarDebugMark>();
        var wxhbMarks = new Dictionary<(string AddonName, byte SlotId), HotbarDebugMark>();

        if (PluginServices.ClientState.IsLoggedIn)
        {
            var hotbar = RaptureHotbarModule.Instance();
            if (hotbar != null)
            {
                if (C.ShowHotbarHighlight && _engine.IsContextMatched)
                    DrawHighlights(hotbar, visibleMarks, wxhbMarks);

                if (captureDebug)
                {
                    FillDebugSlots(hotbar, visibleMarks, frame);
                    FillDebugWxhb(hotbar, wxhbMarks, frame);
                }
            }
        }

        LastFrame = frame;
    }

    private void DrawHighlights(
        RaptureHotbarModule* hotbar,
        Dictionary<(byte, byte), HotbarDebugMark> visibleMarks,
        Dictionary<(string, byte), HotbarDebugMark> wxhbMarks)
    {
        var highlightByAction = CollectHighlightActions();
        if (highlightByAction.Count == 0)
            return;

        var blinkPhaseOn = OverlayView.BlinkPhaseOn;
        var drawList = ImGui.GetForegroundDrawList();

        foreach (var addonName in NormalActionBarAddonNames)
            DrawNormalBar(addonName, hotbar, highlightByAction, drawList, blinkPhaseOn, visibleMarks);

        DrawCrossBar("_ActionCross", hotbar, highlightByAction, drawList, blinkPhaseOn, visibleMarks);

        if (!C.ShowHotbarHighlightDoubleCross)
            return;

        foreach (var addonName in WxhbAddonNames)
            DrawWxhbBar(addonName, hotbar, highlightByAction, drawList, blinkPhaseOn, wxhbMarks);
    }

    private Dictionary<uint, bool> CollectHighlightActions()
    {
        var map = new Dictionary<uint, bool>();
        var fetchLookahead = Math.Max(0.5f, C.ActionHighlightBeforeSeconds);
        foreach (var item in _engine.GetUpcoming(fetchLookahead))
        {
            if (!item.IsHighlighting || !OverlayView.IsAction(item.Cue) || item.Cue.ActionId == 0)
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
        if (map.TryGetValue(actionId, out var existing) && existing)
            return;
        map[actionId] = isPost;
    }

    private static void DrawNormalBar(
        string addonName,
        RaptureHotbarModule* hotbar,
        Dictionary<uint, bool> highlightByAction,
        ImDrawListPtr drawList,
        bool blinkPhaseOn,
        Dictionary<(byte, byte), HotbarDebugMark> visibleMarks)
    {
        if (!TryGetActionBar(addonName, out var addon) || !IsAddonVisible(addon))
            return;

        var hotbarId = addon->RaptureHotbarId;
        if (hotbarId > 9)
            return;

        var slotCount = AddonSlotCount(addon);
        for (var i = 0; i < slotCount; i++)
        {
            ref var barSlot = ref addon->ActionBarSlotVector[i];
            if (!TryHighlightVisibleSlot(
                    hotbar, hotbarId, (byte)i, ref barSlot, highlightByAction, drawList, blinkPhaseOn,
                    allowHiddenNode: false, out var isPost))
                continue;
            NoteMark(visibleMarks, (hotbarId, (byte)i), isPost);
        }
    }

    private static void DrawCrossBar(
        string addonName,
        RaptureHotbarModule* hotbar,
        Dictionary<uint, bool> highlightByAction,
        ImDrawListPtr drawList,
        bool blinkPhaseOn,
        Dictionary<(byte, byte), HotbarDebugMark> visibleMarks)
    {
        if (!TryGetActionBar(addonName, out var addon) || !IsCrossAddonShown(addon))
            return;

        var slotCount = AddonSlotCount(addon);
        for (var i = 0; i < slotCount; i++)
        {
            if (!TryResolveCrossSlot(addon, hotbar, i, out var hotbarId, out var slotId))
                continue;

            ref var barSlot = ref addon->ActionBarSlotVector[i];
            if (!TryHighlightVisibleSlot(
                    hotbar, hotbarId, slotId, ref barSlot, highlightByAction, drawList, blinkPhaseOn,
                    allowHiddenNode: true, out var isPost))
                continue;
            NoteMark(visibleMarks, (hotbarId, slotId), isPost);
        }
    }

    private static void DrawWxhbBar(
        string addonName,
        RaptureHotbarModule* hotbar,
        Dictionary<uint, bool> highlightByAction,
        ImDrawListPtr drawList,
        bool blinkPhaseOn,
        Dictionary<(string, byte), HotbarDebugMark> wxhbMarks)
    {
        if (!TryGetActionBar(addonName, out var addon))
            return;

        var slotCount = Math.Min(8, AddonSlotCount(addon));
        for (var i = 0; i < slotCount; i++)
        {
            if (!TryResolveWxhbSlot(addon, i, out var hotbarId, out var slotId))
                continue;

            ref var barSlot = ref addon->ActionBarSlotVector[i];
            if (!TryHighlightVisibleSlot(
                    hotbar, hotbarId, slotId, ref barSlot, highlightByAction, drawList, blinkPhaseOn,
                    allowHiddenNode: true, out var isPost))
                continue;
            NoteMark(wxhbMarks, (addonName, slotId), isPost);
        }
    }

    private static bool TryHighlightVisibleSlot(
        RaptureHotbarModule* hotbar,
        byte hotbarId,
        byte slotId,
        ref ActionBarSlot barSlot,
        Dictionary<uint, bool> highlightByAction,
        ImDrawListPtr drawList,
        bool blinkPhaseOn,
        bool allowHiddenNode,
        out bool isPost)
    {
        isPost = false;
        if (!TryGetSlotScreenRect(ref barSlot, allowHiddenNode, out var min, out var max))
            return false;
        if (!C.IsHotbarHighlightEnabled(hotbarId))
            return false;

        var slot = hotbar->GetSlotById(hotbarId, slotId);
        if (slot == null || slot->IsEmpty || !TryMatchSlotPhase(slot, highlightByAction, out isPost))
            return false;

        var blink = isPost ? C.ActionHighlightAfterBlink : C.ActionHighlightBeforeBlink;
        if (blink && !blinkPhaseOn)
            return true;

        var color = ImGui.ColorConvertFloat4ToU32(
            isPost ? C.ActionHighlightAfterLineColor : C.ActionHighlightBeforeLineColor);
        var thickness = Math.Max(
            1f,
            isPost ? C.ActionHighlightAfterLineThickness : C.ActionHighlightBeforeLineThickness);
        drawList.AddRect(min, max, color, 2f, ImDrawFlags.None, thickness);
        return true;
    }

    private static bool TryResolveCrossSlot(
        AddonActionBarBase* addon,
        RaptureHotbarModule* hotbar,
        int vectorIndex,
        out byte hotbarId,
        out byte slotId)
    {
        hotbarId = 0;
        slotId = 0;
        if (vectorIndex is < 0 or > 15)
            return false;

        var currentId = addon->RaptureHotbarId;
        if (currentId is < CrossHotbarIdMin or > CrossHotbarIdMax)
            return false;

        var cross = (AddonActionCross*)addon;
        if (cross->DisplayPetBarCross)
            return false;

        var flags = hotbar->CrossHotbarFlags;
        if (vectorIndex < 8 && flags.HasFlag(CrossHotbarFlags.ExpandedHoldLeftFocus)
            && TryResolveExpandedHalf(cross, cross->ExpandedHoldMapValueRL, vectorIndex, useRightUi: false, out hotbarId, out slotId))
            return true;

        if (vectorIndex >= 8 && flags.HasFlag(CrossHotbarFlags.ExpandedHoldRightFocus)
            && TryResolveExpandedHalf(cross, cross->ExpandedHoldMapValueLR, vectorIndex, useRightUi: true, out hotbarId, out slotId))
            return true;

        hotbarId = currentId;
        slotId = (byte)vectorIndex;
        return true;
    }

    private static bool TryResolveExpandedHalf(
        AddonActionCross* cross,
        uint mapValue,
        int vectorIndex,
        bool useRightUi,
        out byte hotbarId,
        out byte slotId)
    {
        hotbarId = 0;
        slotId = 0;
        if (mapValue == 0)
            mapValue = cross->ExpandedHoldMapValue;
        if (!TryGetAdjustedCrossTarget(cross, mapValue, out var target, out var useLeft))
            return false;

        hotbarId = target;
        if (useRightUi)
            slotId = useLeft ? (byte)(vectorIndex - 8) : (byte)vectorIndex;
        else
            slotId = useLeft ? (byte)vectorIndex : (byte)(vectorIndex + 8);
        return true;
    }

    private static bool TryGetAdjustedCrossTarget(
        AddonActionCross* cross,
        uint mapValue,
        out byte hotbarId,
        out bool useLeftSide)
    {
        hotbarId = 0;
        useLeftSide = false;
        if (mapValue == 0)
            return false;

        var useLeft = false;
        var target = AddonActionCross.GetBarTarget(mapValue, &useLeft);
        var current = cross->RaptureHotbarId;
        if (target == 0x13)
            target = (uint)(current - 1);
        if (target == 0x12)
            target = (uint)(current + 1);
        if (target >= 0x12)
            target = 0xA;
        else if (target < 0xA)
            target = 0x11;

        if (target is < CrossHotbarIdMin or > CrossHotbarIdMax)
            return false;

        hotbarId = (byte)target;
        useLeftSide = useLeft;
        return true;
    }

    private static bool TryResolveWxhbSlot(
        AddonActionBarBase* addon,
        int vectorIndex,
        out byte hotbarId,
        out byte slotId)
    {
        hotbarId = 0;
        slotId = 0;
        if (vectorIndex is < 0 or > 7)
            return false;

        var wxhb = (AddonActionDoubleCrossBase*)addon;
        if (wxhb->BarTarget is < CrossHotbarIdMin or > CrossHotbarIdMax)
            return false;

        hotbarId = wxhb->BarTarget;
        slotId = wxhb->UseLeftSide != 0 ? (byte)vectorIndex : (byte)(vectorIndex + 8);
        return true;
    }

    private static bool TryMatchSlotPhase(
        RaptureHotbarModule.HotbarSlot* slot,
        Dictionary<uint, bool> highlightByAction,
        out bool isPost)
    {
        isPost = false;
        if (slot->CommandType != RaptureHotbarModule.HotbarSlotType.Action)
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

    private static bool TryGetSlotScreenRect(
        ref ActionBarSlot barSlot,
        bool allowHiddenNode,
        out Vector2 min,
        out Vector2 max)
    {
        min = default;
        max = default;

        var node = PickSlotNode(ref barSlot, allowHiddenNode);
        if (node == null)
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

    private static AtkResNode* PickSlotNode(ref ActionBarSlot barSlot, bool allowHiddenNode)
    {
        var iconNode = barSlot.Icon != null ? &barSlot.Icon->AtkResNode : null;
        if (IsUsableSlotNode(barSlot.IconFrame, allowHiddenNode: false))
            return barSlot.IconFrame;
        if (IsUsableSlotNode(iconNode, allowHiddenNode: false))
            return iconNode;
        if (!allowHiddenNode)
            return null;
        if (IsUsableSlotNode(barSlot.IconFrame, allowHiddenNode: true))
            return barSlot.IconFrame;
        if (IsUsableSlotNode(iconNode, allowHiddenNode: true))
            return iconNode;
        return null;
    }

    private static bool IsUsableSlotNode(AtkResNode* node, bool allowHiddenNode)
    {
        if (node == null)
            return false;
        if (node->IsVisible())
            return true;
        return allowHiddenNode && node->Width > 0 && node->Height > 0;
    }

    private static void FillDebugSlots(
        RaptureHotbarModule* hotbar,
        Dictionary<(byte HotbarId, byte SlotId), HotbarDebugMark> visibleMarks,
        HotbarHighlightFrame frame)
    {
        for (byte hotbarId = 0; hotbarId < 18; hotbarId++)
        {
            var slotCount = hotbarId < 10 ? 12 : 16;
            for (byte slotId = 0; slotId < slotCount; slotId++)
            {
                var slot = hotbar->GetSlotById(hotbarId, slotId);
                if (slot == null || slot->IsEmpty)
                    continue;

                visibleMarks.TryGetValue((hotbarId, slotId), out var mark);
                frame.Slots.Add(new HotbarDebugCell
                {
                    HotbarId = hotbarId,
                    SlotId = slotId,
                    IconId = GetSlotIconId(slot),
                    Mark = mark,
                });
            }
        }
    }

    private static void FillDebugWxhb(
        RaptureHotbarModule* hotbar,
        Dictionary<(string AddonName, byte SlotId), HotbarDebugMark> wxhbMarks,
        HotbarHighlightFrame frame)
    {
        foreach (var addonName in WxhbAddonNames)
        {
            if (!TryGetActionBar(addonName, out var addon))
                continue;
            if (!TryResolveWxhbSlot(addon, 0, out var hotbarId, out var firstSlotId))
                continue;

            for (var i = 0; i < 8; i++)
            {
                var slotId = (byte)(firstSlotId + i);
                var slot = hotbar->GetSlotById(hotbarId, slotId);
                if (slot == null || slot->IsEmpty)
                    continue;

                wxhbMarks.TryGetValue((addonName, slotId), out var mark);
                frame.WxhbSlots.Add(new HotbarDebugCell
                {
                    AddonName = addonName,
                    SlotId = slotId,
                    IconId = GetSlotIconId(slot),
                    Mark = mark,
                });
            }
        }
    }

    private static void NoteMark<TKey>(Dictionary<TKey, HotbarDebugMark> marks, TKey key, bool isPost)
        where TKey : notnull
    {
        var mark = isPost ? HotbarDebugMark.After : HotbarDebugMark.Before;
        if (marks.TryGetValue(key, out var existing) && existing == HotbarDebugMark.After)
            return;
        marks[key] = mark;
    }

    private static uint GetSlotIconId(RaptureHotbarModule.HotbarSlot* slot)
    {
        var iconId = (uint)slot->IconId;
        if (iconId != 0)
            return iconId;

        var actionId = slot->ApparentActionId != 0 ? slot->ApparentActionId : slot->CommandId;
        return ActionLookup.GetIconId(actionId);
    }

    private static int AddonSlotCount(AddonActionBarBase* addon) =>
        Math.Min((int)addon->SlotCount, addon->ActionBarSlotVector.Count);

    private static bool TryGetActionBar(string addonName, out AddonActionBarBase* addon)
    {
        addon = null;
        var addonHandle = PluginServices.GameGui.GetAddonByName(addonName, 1);
        if (addonHandle == nint.Zero)
            return false;

        addon = (AddonActionBarBase*)addonHandle.Address;
        return addon != null;
    }

    private static bool IsAddonVisible(AddonActionBarBase* addon) =>
        addon->IsVisible && addon->RootNode != null && addon->RootNode->IsVisible();

    private static bool IsCrossAddonShown(AddonActionBarBase* addon)
    {
        if (addon->RootNode == null || !addon->RootNode->IsVisible())
            return false;
        if (addon->IsVisible)
            return true;
        return ((AddonActionCross*)addon)->OverrideHidden;
    }
}

internal enum HotbarDebugMark : byte
{
    None,
    Before,
    After,
}

internal sealed class HotbarHighlightFrame
{
    public List<HotbarDebugCell> Slots { get; } = [];
    public List<HotbarDebugCell> WxhbSlots { get; } = [];
}

internal sealed class HotbarDebugCell
{
    public string AddonName { get; init; } = string.Empty;
    public byte HotbarId { get; init; }
    public byte SlotId { get; init; }
    public uint IconId { get; init; }
    public HotbarDebugMark Mark { get; init; }
}
