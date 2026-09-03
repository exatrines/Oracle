namespace Oracle.Models;

/// <summary>Cue clock text: <c>mm:ss.mmm</c>, stored at 0.1s.</summary>
internal static class CueTime
{
    public static float RoundTenths(float seconds) => MathF.Round(seconds, 1);

    public static string Format(float seconds)
    {
        var negative = seconds < 0f;
        var totalTenths = (int)Math.Round(Math.Abs((double)RoundTenths(seconds)) * 10.0);
        var minutes = totalTenths / 600;
        var secs = totalTenths / 10 % 60;
        var milli = totalTenths % 10 * 100;
        var body = $"{minutes:00}:{secs:00}.{milli:000}";
        return negative ? "-" + body : body;
    }

    public static bool TryParse(string text, out float seconds)
    {
        seconds = 0f;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Trim();
        var negative = text.StartsWith('-');
        if (negative)
            text = text[1..].TrimStart();

        var dot = text.IndexOf('.');
        var mmss = dot >= 0 ? text[..dot] : text;
        var milli = 0;
        if (dot >= 0)
        {
            var frac = text[(dot + 1)..];
            if (frac.Length is < 1 or > 3)
                return false;
            if (!int.TryParse(frac, out var fracVal) || fracVal < 0)
                return false;

            milli = frac.Length switch
            {
                1 => fracVal * 100,
                2 => fracVal * 10,
                _ => fracVal,
            };
            if (milli > 999)
                return false;
        }

        var parts = mmss.Split(':');
        if (parts.Length != 2)
            return false;
        if (!int.TryParse(parts[0], out var minutes) || minutes < 0)
            return false;
        if (!int.TryParse(parts[1], out var secs) || secs < 0 || secs > 59)
            return false;

        seconds = RoundTenths((minutes * 60) + secs + (milli / 1000f));
        if (negative)
            seconds = -seconds;
        return true;
    }
}
