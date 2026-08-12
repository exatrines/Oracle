using MirageUI.Ui;

namespace Oracle.UI;

/// <summary>Auto Load scene filter: checkbox enables a specific SceneId (0 is valid).</summary>
internal static class SceneFilterField
{
    public static bool Draw(
        string id,
        ref bool filterEnabled,
        ref int sceneId,
        bool? liveMatch = null,
        string? matchTooltip = null)
    {
        var dirty = false;
        var gap = ImGui.GetStyle().ItemInnerSpacing.X;

        ImGui.PushID(id);
        if (MirageUi.Checkbox("##filter", ref filterEnabled))
            dirty = true;

        ImGui.SameLine(0f, gap);
        var inputWidth = Math.Max(40f, ImGui.GetContentRegionAvail().X);
        using (ImRaii.Disabled(!filterEnabled))
        {
            var edit = sceneId;
            if (MirageUi.InputInt(
                    string.Empty,
                    ref edit,
                    step: 0,
                    stepFast: 0,
                    id: "value",
                    width: inputWidth,
                    liveMatch: filterEnabled ? liveMatch : null,
                    matchTooltip: filterEnabled ? matchTooltip : null))
            {
                sceneId = Math.Max(0, edit);
                dirty = true;
            }
            else
            {
                sceneId = Math.Max(0, sceneId);
            }
        }

        ImGui.PopID();
        return dirty;
    }

    public static bool DrawLabeled(
        string label,
        string id,
        ref bool filterEnabled,
        ref int sceneId)
    {
        if (!ImGui.BeginTable(
                "##" + id + "SceneFilter",
                2,
                ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoPadOuterX,
                new Vector2(-1f, 0f)))
            return false;

        ImGui.TableSetupColumn("##lbl", ImGuiTableColumnFlags.WidthFixed, MirageUi.FieldLabelColumnWidth);
        ImGui.TableSetupColumn("##fld", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        MirageUi.Text(label, wrap: false);
        ImGui.TableNextColumn();
        var dirty = Draw(id, ref filterEnabled, ref sceneId);
        ImGui.EndTable();
        return dirty;
    }
}
