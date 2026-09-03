using System.Text.RegularExpressions;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;

namespace Oracle.Services;

/// <summary>Party/duty countdown from system chat.</summary>
internal sealed class CountdownSyncDetector : IDisposable
{
    private static readonly Regex[] ChatPatterns =
    [
        new(@"Battle commencing in\s+(\d+)\s+seconds?", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"戦闘開始まで\s*(?:あと\s*)?(\d+)\s*秒", RegexOptions.Compiled),
        new(@"Commencing in\s+(\d+)\s+seconds?", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    private bool _pendingChat;
    private float _pendingChatRemaining;

    public bool JustStarted { get; private set; }
    public float StartedRemaining { get; private set; }

    public void Subscribe() => PluginServices.ChatGui.ChatMessage += OnChatMessage;

    public void Dispose() => PluginServices.ChatGui.ChatMessage -= OnChatMessage;

    public void Update()
    {
        JustStarted = false;
        if (!_pendingChat)
            return;

        _pendingChat = false;
        StartedRemaining = Math.Max(0.1f, _pendingChatRemaining);
        JustStarted = true;
    }

    private void OnChatMessage(IHandleableChatMessage message)
    {
        if (message.LogKind is not (XivChatType.SystemMessage or XivChatType.Urgent or XivChatType.Notice))
            return;

        var text = message.Message.TextValue;
        if (string.IsNullOrWhiteSpace(text))
            return;

        foreach (var pattern in ChatPatterns)
        {
            var m = pattern.Match(text);
            if (!m.Success || !float.TryParse(m.Groups[1].Value, out var sec) || sec is <= 0f or > 60f)
                continue;

            _pendingChat = true;
            _pendingChatRemaining = sec;
            return;
        }
    }
}
