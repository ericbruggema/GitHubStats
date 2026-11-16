using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace GitHubStats;

/// <summary>A line chart drawn directly with WPF drawing primitives (no chart library). Hover shows exact values.</summary>
internal sealed class ChartControl : FrameworkElement
{
    private ChartModel? _model;
    private Rect _plot;
    private ChartLayout? _lay;
    private int _hover = -1;

    private const double L = 54, R = 86, T = 30, B = 24;

    public ChartControl()
    {
        Height = 230;
        MinWidth = 260;
        SnapsToDevicePixels = true;
        Loaded += (s, e) => Theme.Changed += Redraw;
        Unloaded += (s, e) => Theme.Changed -= Redraw;
    }

    private void Redraw() => InvalidateVisual();

    public void SetModel(ChartModel? model)
    {
        _model = model;
        _hover = -1;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new Size(double.IsInfinity(availableSize.Width) ? 480 : availableSize.Width, Height);

    private FormattedText FT(string s, double size, Brush b, bool bold = false) =>
        new FormattedText(s, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal),
            size, b, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h)); // so the whole area gets mouse events
        Brush text = Theme.B("Text"), muted = Theme.B("Muted"), border = Theme.B("Border"), card = Theme.B("Card");

        var m = _model;
        if (m == null || !m.HasData || m.Count < 2)
        {
            var msg = FT("The chart appears once there are at least two data points.", 12, muted);
            dc.DrawText(msg, new Point(Math.Max(0, (w - msg.Width) / 2), h / 2 - 8));
            _lay = null;
            return;
        }

        _plot = new Rect(L, T, Math.Max(20, w - L - R), Math.Max(20, h - T - B));
        var lay = m.Layout(_plot.Height);
        _lay = lay;
        var borderPen = new Pen(border, 1);
        var zeroPen = new Pen(muted, 1) { DashStyle = DashStyles.Solid };

        // legend
        double lx = L;
        for (int si = 0; si < m.Series.Count; si++)
        {
            var s = m.Series[si];
            dc.DrawRoundedRectangle(Theme.Slot(s.Slot), null, new Rect(lx, 8, 10, 10), 2, 2);
            var name = FT(s.Name, 12, muted);
            dc.DrawText(name, new Point(lx + 15, 4));
            lx += 15 + name.Width + 6;
            int li = Array.FindLastIndex(s.Values, v => v != null);
            string val = li < 0 ? "-" : m.Log ? m.ValueText(s, li) : Fmt.N(s.Values.Where(v => v != null).Sum(v => v!.Value)) + " total";
            var valFt = FT(val, 12, text, true);
            dc.DrawText(valFt, new Point(lx, 4));
            lx += valFt.Width + 18;
        }

        // grid + y labels
        foreach (var tk in lay.Ticks)
        {
            double y = T + tk.Y * _plot.Height;
            dc.DrawLine(tk.Zero ? zeroPen : borderPen, new Point(L, y), new Point(L + _plot.Width, y));
            var lb = FT(tk.Label, 11, muted);
            dc.DrawText(lb, new Point(L - 6 - lb.Width, y - lb.Height / 2));
        }

        // x axis: evenly spaced dates (days, weeks or months, whatever fits) with a light vertical line each
        var gridPen = new Pen(border, 1) { DashStyle = new DashStyle(new double[] { 2, 3 }, 0) };
        double lastRight = double.MinValue;
        foreach (var (t, label) in DateTicks(m, _plot.Width))
        {
            double x = L + (double)(t - m.T[0]) / Math.Max(1, m.T[^1] - m.T[0]) * _plot.Width;
            if (x < L - 0.5 || x > L + _plot.Width + 0.5) continue;
            var ft = FT(label, 11, muted);
            double lx2 = Math.Max(L - 4, Math.Min(L + _plot.Width + R - 8 - ft.Width, x - ft.Width / 2));
            if (lx2 < lastRight + 10) continue; // never let two labels touch
            dc.DrawLine(gridPen, new Point(x, T), new Point(x, T + _plot.Height));
            dc.DrawLine(borderPen, new Point(x, T + _plot.Height), new Point(x, T + _plot.Height + 4));
            dc.DrawText(ft, new Point(lx2, h - B + 6));
            lastRight = lx2 + ft.Width;
        }

        // lines
        int n = m.Count;
        for (int si = 0; si < m.Series.Count; si++)
        {
            var geo = new StreamGeometry();
            using (var c = geo.Open())
            {
                bool pen = false;
                for (int i = 0; i < n; i++)
                {
                    double? y = lay.Y[si][i];
                    if (y == null) { pen = false; continue; }
                    var p = new Point(L + lay.X[i] * _plot.Width, T + y.Value * _plot.Height);
                    if (!pen) { c.BeginFigure(p, false, false); pen = true; }
                    else c.LineTo(p, true, true);
                }
            }
            geo.Freeze();
            dc.DrawGeometry(null, new Pen(Theme.Slot(m.Series[si].Slot), 2) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, geo);
        }

        // end markers and labels (labels that would collide are skipped; the legend has the values)
        var ends = new List<(double Y, string Text, Brush Color)>();
        for (int si = 0; si < m.Series.Count; si++)
        {
            double? y = lay.Y[si][n - 1];
            if (y == null) continue;
            double py = T + y.Value * _plot.Height;
            dc.DrawEllipse(Theme.Slot(m.Series[si].Slot), new Pen(card, 2), new Point(L + _plot.Width, py), 4, 4);
            var sr = m.Series[si];
            string lab = m.Log ? Fmt.Pct(sr.Pct![n - 1] ?? 0) : Fmt.N(sr.Values[n - 1] ?? 0);
            ends.Add((py, lab, text));
        }
        double prev = -100;
        foreach (var e in ends.OrderBy(x => x.Y))
        {
            if (e.Y - prev < 13) continue;
            var t2 = FT(e.Text, 11, text);
            dc.DrawText(t2, new Point(L + _plot.Width + 9, e.Y - t2.Height / 2));
            prev = e.Y;
        }

        if (_hover >= 0 && _hover < n) DrawHover(dc, m, lay, _hover, text, muted, border, card, w);
    }

    private static readonly (long Sec, int Months)[] Steps =
    {
        (3600, 0), (2 * 3600, 0), (3 * 3600, 0), (6 * 3600, 0), (12 * 3600, 0),
        (86400, 0), (2 * 86400, 0), (3 * 86400, 0), (7 * 86400, 0), (14 * 86400, 0),
        (0, 1), (0, 2), (0, 3), (0, 6), (0, 12),
    };

    /// <summary>Tick times and labels for the x axis: the smallest step that leaves about 90 px per label.</summary>
    private static List<(long T, string Label)> DateTicks(ChartModel m, double width)
    {
        var list = new List<(long, string)>();
        long t0 = m.T[0], t1 = m.T[^1], span = Math.Max(1, t1 - t0);
        int maxTicks = Math.Max(2, (int)(width / 90));
        var tz = m.Log ? Clock.Tz : TimeZoneInfo.Utc; // same time zone as the hover labels
        var ci = CultureInfo.InvariantCulture;
        DateTime ToLocal(long t) => TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(t), tz).DateTime;
        long ToUnix(DateTime local) => new DateTimeOffset(local, tz.GetUtcOffset(local)).ToUnixTimeSeconds();

        foreach (var (sec, months) in Steps)
        {
            double approx = sec > 0 ? sec : months * 30.4 * 86400;
            if (span / approx > maxTicks) continue;
            var start = ToLocal(t0);
            DateTime cur;
            if (months > 0)
            {
                cur = new DateTime(start.Year, start.Month, 1).AddMonths(1);
                while ((cur.Month - 1) % months != 0) cur = cur.AddMonths(1);
            }
            else if (sec >= 86400)
            {
                cur = start.Date.AddDays(1);
                if (sec == 7 * 86400 || sec == 14 * 86400) while (cur.DayOfWeek != DayOfWeek.Monday) cur = cur.AddDays(1);
            }
            else
            {
                cur = start.Date.AddHours(Math.Ceiling(start.TimeOfDay.TotalHours / (sec / 3600.0)) * (sec / 3600.0));
            }
            string fmt = months >= 12 ? "yyyy" : months > 0 ? (span > 330 * 86400 ? "MMM yyyy" : "MMM") : sec < 86400 ? "HH:mm" : "MMM d";
            for (int guard = 0; guard < 200; guard++)
            {
                long u = ToUnix(cur);
                if (u > t1) break;
                if (u >= t0)
                {
                    string label = sec > 0 && sec < 86400 && cur.Hour == 0 ? cur.ToString("MMM d", ci) : cur.ToString(fmt, ci);
                    list.Add((u, label));
                }
                cur = months > 0 ? cur.AddMonths(months) : cur.AddSeconds(sec);
            }
            if (list.Count >= 2) return list;
            list.Clear();
        }
        // very short ranges: just the start and the end
        list.Add((t0, m.AxisDate(t0)));
        list.Add((t1, m.AxisDate(t1)));
        return list;
    }

    private void DrawHover(DrawingContext dc, ChartModel m, ChartLayout lay, int i, Brush text, Brush muted, Brush border, Brush card, double w)
    {
        double x = L + lay.X[i] * _plot.Width;
        dc.DrawLine(new Pen(muted, 1) { DashStyle = DashStyles.Dash }, new Point(x, T), new Point(x, T + _plot.Height));
        for (int si = 0; si < m.Series.Count; si++)
        {
            double? y = lay.Y[si][i];
            if (y == null) continue;
            dc.DrawEllipse(Theme.Slot(m.Series[si].Slot), new Pen(card, 2), new Point(x, T + y.Value * _plot.Height), 4.5, 4.5);
        }

        var head = FT(m.TimeLabel(m.T[i]), 12, muted);
        var rows = new List<(string Name, FormattedText Val, int Slot)>();
        double nameW = 0, valW = 0;
        foreach (var s in m.Series)
        {
            var nm = FT(s.Name, 12, muted);
            var vl = FT(m.ValueText(s, i), 12, text, true);
            nameW = Math.Max(nameW, nm.Width);
            valW = Math.Max(valW, vl.Width);
            rows.Add((s.Name, vl, s.Slot));
        }
        double bw = Math.Max(head.Width, 14 + nameW + 14 + valW) + 20;
        double bh = 10 + head.Height + 4 + rows.Count * 18 + 6;
        double bx = x + 14;
        if (bx + bw > w - 4) bx = x - 14 - bw;
        if (bx < 4) bx = 4;
        double by = T + 4;
        dc.DrawRoundedRectangle(card, new Pen(border, 1), new Rect(bx, by, bw, bh), 6, 6);
        dc.DrawText(head, new Point(bx + 10, by + 6));
        double ry = by + 10 + head.Height + 4;
        foreach (var r in rows)
        {
            dc.DrawEllipse(Theme.Slot(r.Slot), null, new Point(bx + 15, ry + 9), 4, 4);
            dc.DrawText(FT(r.Name, 12, muted), new Point(bx + 26, ry + 1));
            dc.DrawText(r.Val, new Point(bx + bw - 10 - r.Val.Width, ry + 1));
            ry += 18;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var lay = _lay;
        var m = _model;
        if (lay == null || m == null || lay.X.Length == 0) return;
        var p = e.GetPosition(this);
        int idx = -1;
        if (p.X >= L - 6 && p.X <= L + _plot.Width + 6)
        {
            double fx = (p.X - L) / _plot.Width;
            double best = double.MaxValue;
            for (int i = 0; i < lay.X.Length; i++)
            {
                double dd = Math.Abs(lay.X[i] - fx);
                if (dd < best) { best = dd; idx = i; }
            }
        }
        if (idx != _hover) { _hover = idx; InvalidateVisual(); }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hover != -1) { _hover = -1; InvalidateVisual(); }
    }
}

/// <summary>A tiny line without axes (for the desktop widget).</summary>
internal sealed class Sparkline : FrameworkElement
{
    private long[] _v = Array.Empty<long>();

    public Sparkline()
    {
        Height = 34;
        Loaded += (s, e) => Theme.Changed += Redraw;
        Unloaded += (s, e) => Theme.Changed -= Redraw;
    }

    private void Redraw() => InvalidateVisual();

    public void SetValues(long[] v)
    {
        _v = v;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new Size(double.IsInfinity(availableSize.Width) ? 120 : availableSize.Width, Height);

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (_v.Length < 2 || w < 10) return;
        long min = _v.Min(), max = _v.Max();
        double span = Math.Max(1, max - min);
        var geo = new StreamGeometry();
        Point lastPt = default;
        using (var c = geo.Open())
        {
            for (int i = 0; i < _v.Length; i++)
            {
                var p = new Point(3 + (w - 6) * i / (_v.Length - 1), 3 + (h - 6) * (1 - (_v[i] - min) / span));
                if (i == 0) c.BeginFigure(p, false, false); else c.LineTo(p, true, true);
                lastPt = p;
            }
        }
        geo.Freeze();
        dc.DrawGeometry(null, new Pen(Theme.Slot(0), 2) { LineJoin = PenLineJoin.Round }, geo);
        dc.DrawEllipse(Theme.Slot(0), null, lastPt, 3, 3);
    }
}
