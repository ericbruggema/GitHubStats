using System.Globalization;
using System.Net;

namespace GitHubStats;

internal static class Fmt
{
    public static readonly CultureInfo C = CultureInfo.InvariantCulture;

    public static string H(string? s) => WebUtility.HtmlEncode(s ?? "");

    public static string GhUrl(string? u) =>
        u != null && u.StartsWith("https://github.com/", StringComparison.Ordinal) ? u : "#";

    public static string N(long v) => v.ToString("N0", C);

    public static string F1(double v) => v.ToString("F1", C);

    public static DateTimeOffset Local(long unix) =>
        TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(unix), Clock.Tz);

    public static string Dt(long t) => Local(t).ToString("yyyy-MM-dd HH:mm", C);
    public static string Hm(long t) => Local(t).ToString("HH:mm", C);
    public static string Day(long t) => Local(t).ToString("yyyy-MM-dd", C);
    public static string Iso(long t) => Local(t).ToString("yyyy-MM-dd'T'HH:mm:sszzz", C);

    public static string FDate(string iso)
    {
        if (DateTimeOffset.TryParse(iso, C, DateTimeStyles.AssumeUniversal, out var d))
            return TimeZoneInfo.ConvertTime(d, Clock.Tz).ToString("yyyy-MM-dd", C);
        return "";
    }

    public static string Ago(long t)
    {
        if (t <= 0) return "never";
        long d = Clock.Now() - t;
        if (d < 60) return "just now";
        if (d < 3600) return d / 60 + " min ago";
        if (d < 86400) return d / 3600 + " h ago";
        return d / 86400 + " d ago";
    }

    public static string Bytes(long b)
    {
        double v = b;
        string[] units = { "B", "KB", "MB", "GB" };
        for (int i = 0; i < units.Length; i++)
        {
            if (v < 1024 || i == units.Length - 1)
                return (i == 0 ? ((long)v).ToString(C) : v.ToString("F1", C)) + " " + units[i];
            v /= 1024;
        }
        return "";
    }

    /// <summary>Signed change as coloured HTML; null = no data.</summary>
    public static string Delta(long? v)
    {
        if (v == null) return "<span class=\"zero\">&ndash;</span>";
        if (v > 0) return "<span class=\"up\">+" + N(v.Value) + "</span>";
        if (v < 0) return "<span class=\"down\">&minus;" + N(-v.Value) + "</span>";
        return "<span class=\"zero\">0</span>";
    }

    /// <summary>Signed change as plain text ("+12", "-3", "0"); "" = no data.</summary>
    public static string DeltaText(long? v) =>
        v == null ? "" : v > 0 ? "+" + N(v.Value) : v < 0 ? "-" + N(-v.Value) : "0";

    public static string Pct(double p)
    {
        double a = Math.Abs(p);
        if (a < 0.05) return "0%";
        string s = a < 10 ? a.ToString("N1", C) : Math.Round(a, MidpointRounding.AwayFromZero).ToString("N0", C);
        return (p > 0 ? "+" : "−") + s + "%";
    }

    public static string PctTick(int p)
    {
        if (p == 0) return "0%";
        int a = Math.Abs(p);
        string s = a >= 1000 ? (a / 1000).ToString(C) + "K" : a.ToString(C);
        return (p > 0 ? "+" : "−") + s + "%";
    }

    /// <summary>Short number for axes and badges: 950, 1.2K, 3.4M.</summary>
    public static string Compact(long v)
    {
        long a = Math.Abs(v);
        string s;
        if (a < 1000) s = a.ToString(C);
        else if (a < 1_000_000) s = Trim1(a / 1000.0) + "K";
        else s = Trim1(a / 1_000_000.0) + "M";
        return v < 0 ? "−" + s : s;

        static string Trim1(double d) => d >= 100 ? Math.Round(d).ToString("0", C) : d.ToString("0.#", C);
    }

    /// <summary>Escape text for a Markdown table cell.</summary>
    public static string Md(string? s) =>
        (s ?? "").Replace("\\", "\\\\").Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ").Replace("<", "&lt;").Replace(">", "&gt;").Trim();

    /// <summary>Average per day with one decimal below 10.</summary>
    public static string PerDay(double v) => v < 10 ? v.ToString("0.0", C) : Math.Round(v).ToString("N0", C);
}
