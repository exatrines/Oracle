namespace Oracle.UI;

/// <summary>FFLogs API credential fields + setup steps (Settings).</summary>
internal static class FFLogsApiCredentialsUi
{
    public static void Draw(string idPrefix)
    {
        MirageUi.Info(I18n.Get("fflogs.api.setup_guide"));
        DrawCredentialFields(idPrefix);
    }

    private static void DrawCredentialFields(string idPrefix)
    {
        var clientId = C.FFLogsClientId;
        if (MirageUi.InputText(I18n.Get("fflogs.api.label.client_id"), ref clientId, 128, id: $"{idPrefix}ClientId"))
        {
            C.FFLogsClientId = clientId.Trim();
            C.Save();
        }

        var clientSecret = C.FFLogsClientSecret;
        if (MirageUi.InputText(
                I18n.Get("fflogs.api.label.client_secret"),
                ref clientSecret,
                256,
                id: $"{idPrefix}ClientSecret",
                flags: ImGuiInputTextFlags.Password))
        {
            C.FFLogsClientSecret = clientSecret.Trim();
            C.Save();
        }
    }
}
