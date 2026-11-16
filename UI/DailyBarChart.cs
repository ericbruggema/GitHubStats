using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace GitHubStats;

/// <summary>
/// Bars per day (e.g. downloads per day) with a dashed 7-day average line, dates along the bottom and the exact
/// number on hover. Days without a measurement are drawn as a faint stub.
/// </summary>
internal sealed class DailyBarChart : FrameworkElement
{
    private List<(DateTime Day, long? Value)> _days = new();
    private string _unit = "downloads";
    private int _hover = -1;
    /// <summary>Optional second series drawn as a line (e.g. unique visitors); null = the 7-day average.</summary>
    private double?[]? _line;
    private string _lineLabel = "7-day average";
    /// <summary>true = one bar per hour (labels HH:mm) instead of one per day.</summary>
    public bool Hourly;
    /// <summary>Raised when a bar is clicked; the bars then show a hand cursor.</summary>
    public event Action<DateTime>? DayClicked;
    private const double L = 40, R = 12, T = 10, B = 22;

    public DailyBarChart()
    {
        Height = 190;
        MinWidth = 240;
        Loaded += (s, e) => Theme.Changed += InvalidateVisual;
        Unloaded += (s, e) => Theme.Changed -= InvalidateVisual;
    }

    public void SetDays(List<(DateTime Day, long? Value)> days, string unit, double?[]? line = null, string? lineLabel = null)
    {
        _days = days;
        _unit = unit;
        _line = line != null && line.Length == days.Count ? line : null;
        _lineLabel = lineLabel ?? "7-day average";
        _hover = -1;
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new Size(double.IsInfinity(availableSize.Width) ? 480 : availableSize.Width, Height);

    private FormattedText FT(string s, double size, Brush b, bool bold = false) =>
        new(s, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal),
            size, b, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private Rect Plot => new(L, T, Math.Max(20, ActualWidth - L - R), Math.Max(20, ActualHeight - T - B));

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));
        Brush text = Theme.B("Text"), muted = Theme.B("Muted"), border = Theme.B("Border"), card = Theme.B("Card");
        int n = _days.Count;
        if (n == 0 || _days.All(d => d.Value == null))
        {
            var msg = FT("Downloads per day appear after a few refreshes.", 12, muted);
            dc.DrawText(msg, new Point(Math.Max(0, (w - msg.Width) / 2), h / 2 - 8));
            return;
        }
        var p = Plot;
        long max = Math.Max(1, Math.Max(_days.Max(d => d.Value ?? 0), (long)Math.Ceiling(_line?.Max(x => x ?? 0) ?? 0)));
        long top = NiceTop(max);

        // y grid: 0, half, top
        var gridPen = new Pen(border, 1);
        foreach (var f in new[] { 0.0, 0.5, 1.0 })
        {
            double y = p.Bottom - f * p.Height;
            dc.DrawLine(gridPen, new Point(p.Left, y), new Point(p.Right, y));
            double lv = top * f;
            var lb = FT(Math.Abs(lv - Math.Round(lv)) < 0.01 ? Fmt.N((long)Math.Round(lv)) : lv.ToString("0.#", CultureInfo.CurrentCulture), 10.5, muted);
            dc.DrawText(lb, new Point(p.Left - 6 - lb.Width, y - lb.Height / 2));
        }

        // bars
        double slot = p.Width / n, bw = Math.Max(1, Math.Min(22, slot * 0.72));
        var bar = Theme.B("S0");
        var hoverBar = Theme.B("Link");
        var stub = Theme.B("Accent");
        for (int i = 0; i < n; i++)
        {
            double cx = p.Left + slot * (i + 0.5);
            var v = _days[i].Value;
            if (v == null) { dc.DrawRectangle(stub, null, new Rect(cx - bw / 2, p.Bottom - 2, bw, 2)); continue; }
            double bh = Math.Max(v > 0 ? 2 : 0, (double)v.Value / top * p.Height);
            dc.DrawRoundedRectangle(i == _hover ? hoverBar : bar, null, new Rect(cx - bw / 2, p.Bottom - bh, bw, bh), Math.Min(3, bw / 3), Math.Min(3, bw / 3));
        }

        // second line: the given series, or the 7-day average
        var avgPen = new Pen(Theme.B("S1"), 1.6) { DashStyle = new DashStyle(new double[] { 3, 2 }, 0), LineJoin = PenLineJoin.Round };
        var geo = new StreamGeometry();
        using (var c = geo.Open())
        {
            bool started = false;
            for (int i = 0; i < n; i++)
            {
                double? lv = LineValue(i);
                if (lv == null) { started = false; continue; }
                var pt = new Point(p.Left + slot * (i + 0.5), p.Bottom - lv.Value / top * p.Height);
                if (!started) { c.BeginFigure(pt, false, false); started = true; } else c.LineTo(pt, true, true);
            }
        }
        geo.Freeze();
        dc.DrawGeometry(null, avgPen, geo);

        // dates: every 1, 2, 7 or 14 days, whatever leaves about 70 px per label (weeks start on Monday)
        int step = Hourly ? new[] { 1, 2, 3, 6, 12 }.First(s => p.Width / n * s >= 50 || s == 12)
            : new[] { 1, 2, 7, 14, 28 }.First(s => p.Width / n * s >= 70 || s == 28);
        double lastRight = double.MinValue;
        for (int i = 0; i < n; i++)
        {
            var day = _days[i].Day;
            bool tick = Hourly ? day.Hour % step == 0 : step >= 7 ? day.DayOfWeek == DayOfWeek.Monday && ((n - 1 - i) / 7) % (step / 7) == 0 : (n - 1 - i) % step == 0;
            if (!tick) continue;
            double cx = p.Left + slot * (i + 0.5);
            var ft = FT(day.ToString(Hourly ? "HH:mm" : "MMM d", CultureInfo.InvariantCulture), 10.5, muted);
            double lx = Math.Max(0, Math.Min(w - ft.Width, cx - ft.Width / 2));
            if (lx < lastRight + 8) continue;
            dc.DrawLine(gridPen, new Point(cx, p.Bottom), new Point(cx, p.Bottom + 3));
            dc.DrawText(ft, new Point(lx, p.Bottom + 4));
            lastRight = lx + ft.Width;
        }

        // hover box
        if (_hover >= 0 && _hover < n)
        {
            var (day, v) = _days[_hover];
            double? lv = LineValue(_hover);
            var l1 = FT(Hourly ? day.ToString("HH:mm", CultureInfo.InvariantCulture) + " - " + day.AddHours(1).ToString("HH:mm", CultureInfo.InvariantCulture)
                : day.ToString("dddd d MMM yyyy", CultureInfo.InvariantCulture) + (DayClicked != null ? "  → click for details" : ""), 11.5, muted);
            var l2 = FT(v == null ? "no measurement" : Fmt.N(v.Value) + " " + _unit, 13, text, true);
            var l3 = FT(lv != null ? _lineLabel + " " + lv.Value.ToString("0.#", CultureInfo.CurrentCulture) : "", 11, muted);
            double bwid = Math.Max(l1.Width, Math.Max(l2.Width, l3.Width)) + 20, bhgt = 12 + l1.Height + l2.Height + (l3.Text.Length > 0 ? l3.Height : 0);
            double cx = p.Left + slot * (_hover + 0.5);
            double bx = cx + 12 + bwid > w ? cx - 12 - bwid : cx + 12;
            dc.DrawRoundedRectangle(card, new Pen(border, 1), new Rect(Math.Max(2, bx), T, bwid, bhgt), 6, 6);
            double ty = T + 6;
            dc.DrawText(l1, new Point(Math.Max(2, bx) + 10, ty)); ty += l1.Height;
            dc.DrawText(l2, new Point(Math.Max(2, bx) + 10, ty)); ty += l2.Height;
            if (l3.Text.Length > 0) dc.DrawText(l3, new Point(Math.Max(2, bx) + 10, ty));
        }
    }

    private double? LineValue(int i)
    {
        if (_line != null) return _line[i];
        var win = _days.Skip(Math.Max(0, i - 6)).Take(Math.Min(7, i + 1)).Where(d => d.Value != null).Select(d => d.Value!.Value).ToList();
        return win.Count >= 3 ? win.Average() : null;
    }

    /// <summary>A round number just above <paramref name="max"/> for the top of the y axis.</summary>
    private static long NiceTop(long max)
    {
        double mag = Math.Pow(10, Math.Floor(Math.Log10(max)));
        foreach (var m in new[] { 1, 2, 2.5, 5, 10 })
            if (m * mag >= max) return (long)Math.Max(2, m * mag);
        return max;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int n = _days.Count;
        if (n == 0) return;
        var p = Plot;
        double x = e.GetPosition(this).X;
        int idx = x < p.Left || x > p.Right ? -1 : Math.Min(n - 1, (int)((x - p.Left) / (p.Width / n)));
        if (idx != _hover) { _hover = idx; InvalidateVisual(); }
        Cursor = DayClicked != null && idx >= 0 ? Cursors.Hand : null;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (DayClicked != null && _hover >= 0 && _hover < _days.Count)
        {
            e.Handled = true; // not also the block around the chart
            DayClicked(_days[_hover].Day);
        }
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hover != -1) { _hover = -1; InvalidateVisual(); }
    }
}
