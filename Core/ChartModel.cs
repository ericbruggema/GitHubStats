using static GitHubStats.Fmt;

namespace GitHubStats;

internal sealed class ChartSeries
{
    public string Name = "";
    /// <summary>Colour slot 0..2 (the UI maps it to the light/dark palette).</summary>
    public int Slot;
    public long?[] Values = Array.Empty<long?>();
    /// <summary>% change since the first value (log charts); null for linear charts.</summary>
    public double?[]? Pct;
    /// <summary>The value that is plotted (the percentage for log charts, the raw value otherwise).</summary>
    public double? Plot(int i) => Pct != null ? Pct[i] : Values[i];
}

internal sealed class ChartTick
{
    /// <summary>0 = top of the plot, 1 = bottom</summary>
    public double Y;
    public string Label = "";
    public bool Zero;
}

/// <summary>The result of <see cref="ChartModel.Layout"/>: everything as fractions of the plot area, so the UI only has to scale.</summary>
internal sealed class ChartLayout
{
    public List<ChartTick> Ticks = new();
    /// <summary>X per point, 0 = left, 1 = right</summary>
    public double[] X = Array.Empty<double>();
    /// <summary>Y per series and point, 0 = top, 1 = bottom; null = no data</summary>
    public double?[][] Y = Array.Empty<double?[]>();
}

/// <summary>
/// The data of one chart (UI independent): downloads / stars / releases as % change since the first value on a
/// logarithmic axis, or a linear chart for daily traffic.
/// </summary>
internal sealed class ChartModel
{
    public bool Log;
    public bool DateOnly;
    public string Title = "";
    public long[] T = Array.Empty<long>();
    public List<ChartSeries> Series = new();
    public string Caption = "";

    public int Count => T.Length;
    public bool HasData => T.Length >= 1 && Series.Any(s => s.Values.Any(v => v != null));

    private static readonly int[] Candidates =
        { -50, -25, -10, 0, 10, 25, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000, 50000, 100000, 200000, 500000 };

    private static DateTimeOffset Utc(long unix) => DateTimeOffset.FromUnixTimeSeconds(unix);

    /// <summary>Downloads, stars and releases of one repo. Row = [stars, downloads, releases].</summary>
    public static ChartModel Pct3(string title, long[] t, long?[][] v, bool realStars)
    {
        var cols = new (string Name, int Slot, int K)[] { ("Downloads", 0, 1), ("Stars", 1, 0), ("Releases", 2, 2) };
        var m = new ChartModel { Log = true, Title = title, T = t };
        foreach (var c in cols)
        {
            var vals = v.Select(row => row[c.K]).ToArray();
            m.Series.Add(new ChartSeries { Name = c.Name, Slot = c.Slot, Values = vals, Pct = Charts.PctSeries(vals) });
        }
        m.Caption = "Change in % since each line's first value (every line starts at 0%), logarithmic scale. "
            + (realStars ? "Stars use the exact dates from GitHub; downloads and releases are tracked from the first refresh on." : "");
        return m;
    }

    /// <summary>Daily views, unique visitors and clones. Row = [views, uniques, clones].</summary>
    public static ChartModel Traffic(string title, long[] t, long?[][] v)
    {
        var cols = new (string Name, int Slot, int K)[] { ("Views", 0, 0), ("Unique visitors", 1, 1), ("Clones", 2, 2) };
        var m = new ChartModel { Log = false, DateOnly = true, Title = title, T = t };
        foreach (var c in cols)
            m.Series.Add(new ChartSeries { Name = c.Name, Slot = c.Slot, Values = v.Select(row => row[c.K]).ToArray() });
        m.Caption = "Per day (UTC). GitHub only keeps 14 days of traffic; this program stores every refresh, so the history keeps growing.";
        return m;
    }

    /// <summary>Total downloads (slot 0) and stars (slot 1) of all repositories over time, one point per refresh.</summary>
    public static ChartModel AllRepos(List<Snapshot> snaps)
    {
        var s = snaps.OrderBy(x => x.T).ToList();
        var m = new ChartModel { Log = true, Title = "All repositories", T = s.Select(x => x.T).ToArray() };
        var dl = s.Select(x => (long?)x.Downloads).ToArray();
        var st = s.Select(x => (long?)x.Stars).ToArray();
        m.Series.Add(new ChartSeries { Name = "Downloads", Slot = 0, Values = dl, Pct = Charts.PctSeries(dl) });
        m.Series.Add(new ChartSeries { Name = "Stars", Slot = 1, Values = st, Pct = Charts.PctSeries(st) });
        m.Caption = "Change in % since the first refresh, logarithmic scale.";
        return m;
    }

    public string TimeLabel(long t) => DateOnly ? Utc(t).ToString("yyyy-MM-dd", C) : Dt(t);

    public string AxisDate(long t)
    {
        long span = T.Length > 1 ? T[^1] - T[0] : 0;
        string fmt = span > 200 * 86400 ? "MMM d, yyyy" : "MMM d";
        return (Log ? Local(t) : Utc(t)).ToString(fmt, C);
    }

    public string ValueText(ChartSeries s, int i)
    {
        long? v = s.Values[i];
        if (v == null) return "-";
        string txt = N(v.Value);
        if (Log && s.Pct != null && s.Pct[i] != null) txt += "  (" + Pct(s.Pct[i]!.Value) + ")";
        return txt;
    }

    /// <summary>Positions for a plot area that is <paramref name="plotHeight"/> pixels high (needed so tick labels never overlap).</summary>
    public ChartLayout Layout(double plotHeight)
    {
        int n = T.Length;
        var lay = new ChartLayout { X = new double[n], Y = new double?[Series.Count][] };
        if (n == 0) return lay;
        double span = Math.Max(1, T[n - 1] - T[0]);
        for (int i = 0; i < n; i++) lay.X[i] = n == 1 ? 0.5 : (T[i] - T[0]) / span;

        Func<double, double> Y;
        if (Log)
        {
            static double Ly(double p) => Math.Log(Math.Max(0.01, 1 + p / 100));
            double lo = 0, hi = 0;
            foreach (var s in Series)
                for (int i = 0; i < n; i++)
                    if (s.Pct![i] != null) { double y = Ly(s.Pct[i]!.Value); lo = Math.Min(lo, y); hi = Math.Max(hi, y); }
            double dlo = lo, dhi = hi;
            if (hi - lo < 0.1) { double mid = (hi + lo) / 2; lo = mid - 0.05; hi = mid + 0.05; }
            double pad = (hi - lo) * 0.08;
            lo -= pad; hi += pad;
            Y = p => (hi - Ly(p)) / (hi - lo);

            var cand = Candidates.Where(p => p != 0 && Ly(p) >= dlo - 1e-9 && Ly(p) <= dhi + 1e-9).OrderByDescending(Math.Abs).ToList();
            var chosen = new List<int> { 0 };
            foreach (int p in cand)
                if (chosen.All(q => Math.Abs(Y(p) - Y(q)) * plotHeight >= 18)) chosen.Add(p);
            chosen.Sort();
            foreach (int p in chosen) lay.Ticks.Add(new ChartTick { Y = Y(p), Label = PctTick(p), Zero = p == 0 });
        }
        else
        {
            double max = 0;
            foreach (var s in Series) foreach (var v in s.Values) if (v != null) max = Math.Max(max, v.Value);
            if (max <= 0) max = 1;
            double raw = max / 4.0;
            double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double step = new[] { 1.0, 2, 5, 10 }.Select(m => m * mag).First(x => x >= raw);
            step = Math.Max(1, step);
            double top = Math.Ceiling(max / step - 1e-9) * step;
            Y = v => 1 - v / top;
            for (double x = 0; x <= top + 1e-9; x += step) lay.Ticks.Add(new ChartTick { Y = Y(x), Label = Compact((long)Math.Round(x)), Zero = x == 0 });
        }

        for (int si = 0; si < Series.Count; si++)
        {
            lay.Y[si] = new double?[n];
            for (int i = 0; i < n; i++)
            {
                double? v = Series[si].Plot(i);
                lay.Y[si][i] = v == null ? null : Y(v.Value);
            }
        }
        return lay;
    }
}
