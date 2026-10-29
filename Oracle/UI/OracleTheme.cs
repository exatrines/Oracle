using System.Text.Json;
using MirageUI.Theme;

namespace Oracle.UI;

// Built-in MirageUI themes plus saved custom themes.
internal static class OracleTheme
{
    public const string WhiteId = "mirageui-white";
    public const string BlackId = "mirageui-black";

    private static readonly JsonSerializerOptions EnvelopeOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static MirageColorSettings Current { get; private set; } = MirageColorSettings.CreateWhite();

    public static bool IsBuiltin(string id) => id is WhiteId or BlackId;

    public static SavedTheme Selected =>
        C.Themes.Find(theme => theme.Id == C.SelectedThemeId) ?? C.Themes[0];

    public static string DisplayName(SavedTheme theme) =>
        IsBuiltin(theme.Id) ? $"{theme.Name} ( Default )" : theme.Name;

    public static void Bind()
    {
        C.Themes ??= [];
        var customs = C.Themes.Where(theme => !IsBuiltin(theme.Id) && !string.IsNullOrWhiteSpace(theme.Id)).ToList();
        C.Themes =
        [
            Builtin(WhiteId, "MirageUI White", MirageColorSettings.CreateWhite()),
            Builtin(BlackId, "MirageUI Black", MirageColorSettings.CreateBlack()),
            .. customs,
        ];
        if (C.Themes.All(theme => theme.Id != C.SelectedThemeId))
            C.SelectedThemeId = WhiteId;
        Apply();
    }

    public static void Select(string id)
    {
        if (C.Themes.All(theme => theme.Id != id))
            return;

        C.SelectedThemeId = id;
        Apply();
        C.Save();
    }

    public static void SaveCurrent()
    {
        var entry = C.Themes.Find(theme => theme.Id == C.SelectedThemeId);
        if (entry == null || IsBuiltin(entry.Id))
            return;

        entry.Json = MirageColorSettingsJson.Export(Current);
        C.Save();
    }

    public static void Add() =>
        AddTheme("Theme", MirageColorSettingsJson.Export(MirageColorSettings.CreateWhite()));

    public static void DuplicateSelected() =>
        AddTheme(Selected.Name, MirageColorSettingsJson.Export(Current));

    public static void DeleteSelected()
    {
        if (IsBuiltin(C.SelectedThemeId))
            return;

        var index = C.Themes.FindIndex(theme => theme.Id == C.SelectedThemeId);
        if (index < 0)
            return;

        C.Themes.RemoveAt(index);
        C.SelectedThemeId = C.Themes[Math.Max(0, index - 1)].Id;
        Apply();
        C.Save();
    }

    public static void ExportSelected()
    {
        using var colors = JsonDocument.Parse(MirageColorSettingsJson.Export(Current));
        var envelope = new ThemeEnvelope
        {
            Name = Selected.Name,
            Theme = colors.RootElement.Clone(),
        };
        ImGui.SetClipboardText(JsonSerializer.Serialize(envelope, EnvelopeOptions));
    }

    public static void ImportClipboard()
    {
        var text = ImGui.GetClipboardText();
        if (string.IsNullOrWhiteSpace(text) || !TryParse(text, out var name, out var themeJson))
        {
            PluginServices.Log.Warning("Theme clipboard is empty or not a theme.");
            return;
        }

        AddTheme(name, themeJson);
    }

    private static void Apply()
    {
        var entry = C.Themes.Find(theme => theme.Id == C.SelectedThemeId) ?? C.Themes[0];
        C.SelectedThemeId = entry.Id;
        if (MirageColorSettingsJson.TryImport(entry.Json, out var loaded))
            Current = loaded;
    }

    private static void AddTheme(string name, string json)
    {
        var added = new SavedTheme
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = UniqueName(name),
            Json = json,
        };
        C.Themes.Add(added);
        C.SelectedThemeId = added.Id;
        Apply();
        C.Save();
    }

    private static SavedTheme Builtin(string id, string name, MirageColorSettings factory) =>
        new()
        {
            Id = id,
            Name = name,
            Json = MirageColorSettingsJson.Export(factory),
        };

    private static string UniqueName(string name)
    {
        var taken = new HashSet<string>(C.Themes.Select(theme => theme.Name), StringComparer.Ordinal);
        var stem = string.IsNullOrWhiteSpace(name) ? "Theme" : name.Trim();
        if (!taken.Contains(stem))
            return stem;

        for (var number = 2; ; number++)
        {
            var candidate = $"{stem} {number}";
            if (!taken.Contains(candidate))
                return candidate;
        }
    }

    private static bool TryParse(string text, out string name, out string themeJson)
    {
        name = "";
        themeJson = "";
        try
        {
            var envelope = JsonSerializer.Deserialize<ThemeEnvelope>(text, EnvelopeOptions);
            if (envelope?.Theme.ValueKind == JsonValueKind.Object)
            {
                var raw = envelope.Theme.GetRawText();
                if (!MirageColorSettingsJson.TryImport(raw, out _))
                    return false;

                name = string.IsNullOrWhiteSpace(envelope.Name) ? "Theme" : envelope.Name;
                themeJson = raw;
                return true;
            }
        }
        catch (JsonException)
        {
        }

        if (!MirageColorSettingsJson.TryImport(text, out _))
            return false;

        name = "Theme";
        themeJson = text;
        return true;
    }

    private sealed class ThemeEnvelope
    {
        public string? Name { get; set; }

        public JsonElement Theme { get; set; }
    }
}
