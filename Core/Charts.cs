using System.Text;
using static GitHubStats.Fmt;

namespace GitHubStats;

internal sealed class Ser
{
    public string Cls = "";
    public string Name = "";
    public long?[] V = Array.Empty<long?>();
    /// <summary>% change since the first value (log charts); null for linear charts.</summary>
    public double?[]? P;
    /// <summary>y position in the SVG per point (filled in while drawing)</summary>
    public double?[] Y = Array.Empty<double?>();
}

internal sealed class ChartOut
{
    public string Html = "";
    /// <summary>JSON-able data for the hover script.</summary>
    public object Data = new();
}

/// <summary>
/// Charts: downloads / stars / releases as % change since the first value on one log axis, and a
/// linear chart for daily traffic. Plain inline SVG, hover handled by the script at the bottom of the page.
/// </summary>
internal static class Charts
{
    private const int VW = 640, VH = 190, L = 48, R = 70, T = 10, B = 24;
    private const int PW = VW - L - R, PH = VH - T - B;

    // ------------------------------------------------------------------ data preparation

    private static int UpperBound(long[] a, long x)
    {
        int lo = 0, hi = a.Length;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (a[mid] <= x) lo = mid + 1; else hi = mid;
        }
        return lo;
    }

    /// <summary>
    /// The points of one repo's chart: every completed refresh, plus (when known) the exact dates of the stars,
    /// so the star line reaches back to the very first star. Row = [stars, downloads, releases] (null = unknown).
    /// </summary>
    public static (long[] T, long?[][] V, bool RealStars) RepoPoints(List<Snapshot> snaps, string name, List<StarRec>? stars, long repoStars)
    {
        var sp = new List<(long T, long S, long D, long? R)>();
        foreach (var sn in snaps)
            if (sn.Repos.TryGetValue(name, out var e) && e.Length >= 2)
                sp.Add((sn.T, e[0], e[1], e.Length > 2 ? e[2] : (long?)null));

        long[] starT = Array.Empty<long>();
        bool real = false;
        long offset = 0;
        if (stars != null && stars.Count > 0)
        {
            starT = stars.Select(x => x.T).Where(x => x > 0).OrderBy(x => x).ToArray();
            real = starT.Length > 0;
            if (real) offset = Math.Max(0, repoStars - starT.Length); // stars older than the stored list
        }

        var times = new SortedSet<long>(sp.Select(p => p.T));
        if (real)
        {
            if (starT.Length <= 100) foreach (var x in starT) times.Add(x);
            else for (int k = 0; k < 100; k++) times.Add(starT[(int)Math.Round(k * (starT.Length - 1) / 99.0)]);
        }
        var tl = times.ToList();
        if (tl.Count > 200)
        {
            var pick = new List<long>();
            for (int k = 0; k < 200; k++) pick.Add(tl[(int)Math.Round(k * (tl.Count - 1) / 199.0)]);
            tl = pick.Distinct().ToList();
        }

        var tArr = tl.ToArray();
        var v = new long?[tArr.Length][];
        int si = -1; // latest snapshot at or before the point
        for (int i = 0; i < tArr.Length; i++)
        {
            while (si + 1 < sp.Count && sp[si + 1].T <= tArr[i]) si++;
            long? stars_ = real ? offset + UpperBound(starT, tArr[i]) : (si >= 0 ? sp[si].S : (long?)null);
            long? dl = si >= 0 ? sp[si].D : (long?)null;
            long? rel = si >= 0 ? sp[si].R : (long?)null;
            v[i] = new[] { stars_, dl, rel };
        }
        return (tArr, v, real);
    }

    public static double?[] PctSeries(long?[] vals)
    {
        var p = new double?[vals.Length];
        long? bas = null;
        for (int i = 0; i < vals.Length; i++)
        {
            long? val = vals[i];
            if (val == null) continue;
            if (bas == null && val > 0) bas = val;
            p[i] = bas == null ? 0.0 : Math.Max(-99.0, ((double)val.Value / bas.Value - 1) * 100);
        }
        return p;
    }

    // ------------------------------------------------------------------ public builders

    /// <summary>Downloads, stars and releases as % change. Row = [stars, downloads, releases].</summary>
    public static ChartOut Pct3(string id, string name, long[] t, long?[][] v, bool realStars)
    {
        var cols = new (string Cls, string Name, int K)[] { ("k-d", "Downloads", 1), ("k-s", "Stars", 0), ("k-r", "Releases", 2) };
        var ser = cols.Select(c =>
        {
            var vals = v.Select(row => row[c.K]).ToArray();
            return new Ser { Cls = c.Cls, Name = c.Name, V = vals, P = PctSeries(vals) };
        }).ToList();
        string caption = "Change in % since each line's first value (every line starts at 0%). The scale is logarithmic, so slow and fast growers both stay readable. "
            + (realStars ? "Stars use the exact dates from GitHub; downloads and releases are tracked from the first refresh on. " : "")
            + "Hover or use the arrow keys for exact values.";
        return Build(id, t, ser, true, false,
            "Downloads, stars and releases of " + name + ", percent change since the first value. Use the table view below for the values.", caption);
    }

    /// <summary>Daily views, unique visitors and clones. Row = [views, uniques, clones].</summary>
    public static ChartOut Traffic(string id, string name, long[] t, long?[][] v)
    {
        var cols = new (string Cls, string Name, int K)[] { ("k-1", "Views", 0), ("k-2", "Unique visitors", 1), ("k-3", "Clones", 2) };
        var ser = cols.Select(c => new Ser { Cls = c.Cls, Name = c.Name, V = v.Select(row => row[c.K]).ToArray() }).ToList();
        return Build(id, t, ser, false, true,
            "Daily views, unique visitors and clones of " + name + ". Use the table view below for the values.",
            "Per day (UTC). GitHub only keeps 14 days of traffic; this program stores every refresh, so this history keeps growing.");
    }

    // ------------------------------------------------------------------ assembly

    private static double R1(double d) => Math.Round(d, 1);

    private static DateTimeOffset Utc(long unix) => DateTimeOffset.FromUnixTimeSeconds(unix);

    private static ChartOut Build(string id, long[] t, List<Ser> ser, bool log, bool dateOnly, string aria, string caption)
    {
        int n = t.Length;
        string svg = Svg(id, t, ser, log, aria, out double[] xs);
        string[] labels = t.Select(x => dateOnly ? Utc(x).ToString("yyyy-MM-dd", C) : Dt(x)).ToArray();

        var sb = new StringBuilder(4096);
        sb.Append("<div class=\"chartbox\"><div class=\"lgd\">");
        foreach (var s in ser)
        {
            sb.Append("<span class=\"lg\"><i class=\"k ").Append(s.Cls).Append("\"></i>").Append(H(s.Name)).Append(" <b>");
            if (log)
            {
                int li = Array.FindLastIndex(s.V, x => x != null);
                sb.Append(li < 0 ? "&ndash;" : N(s.V[li]!.Value)).Append("</b>");
                if (li >= 0 && s.P != null && s.P[li] != null) sb.Append(" <span class=\"pc\">").Append(H(Pct(s.P[li]!.Value))).Append("</span>");
            }
            else
            {
                long sum = s.V.Where(x => x != null).Sum(x => x!.Value);
                sb.Append(N(sum)).Append("</b> <span class=\"pc\">total</span>");
            }
            sb.Append("</span>");
        }
        sb.Append("</div>").Append(svg);
        sb.Append("<div class=\"cap\">").Append(H(caption)).Append("</div>");

        sb.Append("<details class=\"tv\"><summary>Table view</summary><div class=\"tvwrap\"><table><tr><th>").Append(dateOnly ? "Date" : "Time").Append("</th>");
        foreach (var s in ser) sb.Append("<th class=\"num\">").Append(H(s.Name)).Append("</th>");
        sb.Append("</tr>");
        int shown = 0;
        for (int i = n - 1; i >= 0 && shown < 30; i--)
        {
            if (ser.All(s => s.V[i] == null)) continue;
            shown++;
            sb.Append("<tr><td>").Append(H(labels[i])).Append("</td>");
            foreach (var s in ser) sb.Append("<td class=\"num\">").Append(s.V[i] == null ? "&ndash;" : N(s.V[i]!.Value)).Append("</td>");
            sb.Append("</tr>");
        }
        sb.Append("</table></div><div class=\"cap\">Latest 30 points; the complete history is in the CSV and JSON exports.</div></details></div>");

        var data = new
        {
            x = xs.Select(R1).ToArray(),
            l = labels,
            s = ser.Select(s => new { c = s.Cls, n = s.Name, v = s.V, p = s.P?.Select(x => x == null ? (double?)null : Math.Round(x.Value, 1)).ToArray(), y = s.Y }).ToArray(),
        };
        return new ChartOut { Html = sb.ToString(), Data = data };
    }

    private static readonly int[] Candidates =
        { -50, -25, -10, 0, 10, 25, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000, 50000, 100000, 200000, 500000 };

    private static string Svg(string id, long[] t, List<Ser> ser, bool log, string aria, out double[] xs)
    {
        int n = t.Length;
        long t0 = t[0];
        double span = Math.Max(1, t[n - 1] - t0);
        var xa = t.Select(ts => L + (ts - t0) / span * PW).ToArray();
        xs = xa;

        // y scale: log of (1 + %/100) for the percent charts, 0..max for counts
        Func<double, double> Y;
        var ticks = new List<(double Value, string Label)>();
        Func<Ser, int, double?> val = log ? (s, i) => s.P![i] : (s, i) => s.V[i];

        if (log)
        {
            static double Ly(double p) => Math.Log(Math.Max(0.01, 1 + p / 100));
            double lo = 0, hi = 0;
            foreach (var s in ser)
                for (int i = 0; i < n; i++)
                    if (s.P![i] != null) { double y = Ly(s.P[i]!.Value); lo = Math.Min(lo, y); hi = Math.Max(hi, y); }
            double dlo = lo, dhi = hi; // range of the data itself (ticks are only drawn inside it)
            if (hi - lo < 0.1) // (nearly) flat: centre the line instead of hugging an edge
            {
                double mid = (hi + lo) / 2;
                lo = mid - 0.05;
                hi = mid + 0.05;
            }
            double pad = (hi - lo) * 0.08;
            lo -= pad;
            hi += pad;
            Y = p => T + (hi - Ly(p)) / (hi - lo) * PH;

            // big values first; small ones near 0% drop out when crowded
            var cand = Candidates.Where(p => p != 0 && Ly(p) >= dlo - 1e-9 && Ly(p) <= dhi + 1e-9).OrderByDescending(Math.Abs).ToList();
            var chosen = new List<int> { 0 };
            foreach (int p in cand)
                if (chosen.All(q => Math.Abs(Y(p) - Y(q)) >= 18)) chosen.Add(p);
            chosen.Sort();
            foreach (int p in chosen) ticks.Add((p, PctTick(p)));
        }
        else
        {
            double max = 0;
            foreach (var s in ser) foreach (var v in s.V) if (v != null) max = Math.Max(max, v.Value);
            if (max <= 0) max = 1;
            double raw = max / 4.0;
            double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double step = new[] { 1.0, 2, 5, 10 }.Select(m => m * mag).First(x => x >= raw);
            step = Math.Max(1, step);
            double top = Math.Ceiling(max / step - 1e-9) * step;
            Y = v => T + (1 - v / top) * PH;
            for (double x = 0; x <= top + 1e-9; x += step) ticks.Add((x, Compact((long)Math.Round(x))));
        }

        // the y position of each point is handed to the hover script
        foreach (var s in ser)
        {
            s.Y = new double?[n];
            for (int i = 0; i < n; i++)
            {
                double? v = val(s, i);
                s.Y[i] = v == null ? null : R1(Y(v.Value));
            }
        }

        var sb = new StringBuilder(4096);
        sb.Append("<svg class=\"hchart\" viewBox=\"0 0 ").Append(VW).Append(' ').Append(VH)
          .Append("\" role=\"img\" tabindex=\"0\" data-id=\"").Append(H(id))
          .Append("\" aria-label=\"").Append(H(aria)).Append("\">");

        foreach (var (v, label) in ticks)
        {
            double y = Y(v);
            sb.Append("<line class=\"g").Append(v == 0 ? " z" : "").Append("\" x1=\"").Append(L).Append("\" x2=\"").Append(L + PW)
              .Append("\" y1=\"").Append(F1(y)).Append("\" y2=\"").Append(F1(y)).Append("\"/>");
            sb.Append("<text x=\"").Append(L - 6).Append("\" y=\"").Append(F1(y + 4)).Append("\" text-anchor=\"end\">").Append(H(label)).Append("</text>");
        }
        string fmt = span > 200 * 86400 ? "MMM d, yyyy" : "MMM d";
        sb.Append("<text x=\"").Append(L).Append("\" y=\"").Append(VH - 6).Append("\" text-anchor=\"start\">").Append(H((log ? Local(t0) : Utc(t0)).ToString(fmt, C))).Append("</text>");
        sb.Append("<text x=\"").Append(L + PW).Append("\" y=\"").Append(VH - 6).Append("\" text-anchor=\"end\">").Append(H((log ? Local(t0 + (long)span) : Utc(t0 + (long)span)).ToString(fmt, C))).Append("</text>");

        // lines (a gap where a series has no data)
        foreach (var s in ser)
        {
            var d = new StringBuilder();
            bool pen = false;
            for (int i = 0; i < n; i++)
            {
                double? v = val(s, i);
                if (v == null) { pen = false; continue; }
                d.Append(pen ? 'L' : 'M').Append(F1(xa[i])).Append(',').Append(F1(Y(v.Value)));
                pen = true;
            }
            if (d.Length > 0) sb.Append("<path class=\"ln ").Append(s.Cls).Append("\" d=\"").Append(d).Append("\"/>");
        }

        // end markers + selective direct labels (skipped when they would collide; the legend carries the values)
        var ends = new List<(double Y, string Text)>();
        foreach (var s in ser)
        {
            int li = -1;
            for (int i = n - 1; i >= 0; i--) if (val(s, i) != null) { li = i; break; }
            if (li != n - 1) continue; // only series that reach the right edge get an end marker
            double v = val(s, li)!.Value;
            double y = Y(v);
            sb.Append("<circle class=\"ed ").Append(s.Cls).Append("\" cx=\"").Append(F1(L + PW)).Append("\" cy=\"").Append(F1(y)).Append("\" r=\"4\"/>");
            ends.Add((y, log ? Pct(v) : N((long)Math.Round(v))));
        }
        double prev = -100;
        foreach (var (y, text) in ends.OrderBy(e => e.Y))
        {
            if (y - prev < 13) continue;
            sb.Append("<text class=\"dl\" x=\"").Append(L + PW + 9).Append("\" y=\"").Append(F1(y + 4)).Append("\">").Append(H(text)).Append("</text>");
            prev = y;
        }

        // hover layer (positioned by the script at the bottom of the page)
        sb.Append("<g class=\"hov\" style=\"display:none\"><line class=\"cross\" x1=\"0\" x2=\"0\" y1=\"").Append(T).Append("\" y2=\"").Append(T + PH).Append("\"/>");
        foreach (var s in ser) sb.Append("<circle class=\"hd ").Append(s.Cls).Append("\" r=\"4\"/>");
        sb.Append("</g></svg>");
        return sb.ToString();
    }
}
