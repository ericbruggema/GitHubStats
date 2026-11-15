using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace GitHubStats;

/// <summary>A few seconds of fireworks and a big "+N star" over the whole screen when new stars arrive. Click-through, then it closes itself.</summary>
internal sealed class FireworksWindow : Window
{
    private sealed class Particle
    {
        public double X, Y, Px, Py, Vx, Vy, Life, Age, Size;
        public Color Color;
    }

    private sealed class Canvas2 : FrameworkElement
    {
        public Action<DrawingContext>? Paint;
        protected override void OnRender(DrawingContext dc) => Paint?.Invoke(dc);
    }

    private const double Duration = 4.4;
    private static readonly Color[] Palette =
    {
        Color.FromRgb(0xff, 0xc1, 0x07), Color.FromRgb(0xff, 0x6e, 0x40), Color.FromRgb(0x42, 0xa5, 0xf5),
        Color.FromRgb(0x66, 0xbb, 0x6a), Color.FromRgb(0xec, 0x40, 0x7a), Color.FromRgb(0xab, 0x47, 0xbc), Color.FromRgb(0xff, 0xff, 0xff),
    };

    private readonly List<Particle> _ps = new();
    private readonly Random _rng = new();
    private readonly Canvas2 _canvas = new();
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Stopwatch _sw = new();
    private readonly int _stars;
    private readonly int _bursts;
    private int _launched;
    private double _last;

    public FireworksWindow(int stars)
    {
        _stars = Math.Max(1, stars);
        _bursts = Math.Min(7, 2 + _stars);

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;
        IsHitTestVisible = false;
        Title = "GitHub Stats celebration";
        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;
        Content = _canvas;
        _canvas.Paint = Draw;

        SourceInitialized += (s, e) => Native.Overlay(this, true);
        Loaded += (s, e) =>
        {
            _sw.Start();
            _timer.Tick += (s2, e2) => Step();
            _timer.Start();
        };
    }

    private void Burst()
    {
        double w = ActualWidth, h = ActualHeight;
        double cx = w * (0.18 + _rng.NextDouble() * 0.64), cy = h * (0.12 + _rng.NextDouble() * 0.33);
        var c1 = Palette[_rng.Next(Palette.Length)];
        var c2 = Palette[_rng.Next(Palette.Length)];
        int n = 70;
        for (int i = 0; i < n; i++)
        {
            double ang = _rng.NextDouble() * Math.PI * 2;
            double sp = 90 + _rng.NextDouble() * 300;
            _ps.Add(new Particle
            {
                X = cx, Y = cy, Px = cx, Py = cy,
                Vx = Math.Cos(ang) * sp, Vy = Math.Sin(ang) * sp,
                Life = 1.2 + _rng.NextDouble() * 1.0, Size = 1.8 + _rng.NextDouble() * 2.2,
                Color = i % 3 == 0 ? c2 : c1,
            });
        }
    }

    private void Step()
    {
        double t = _sw.Elapsed.TotalSeconds;
        double dt = Math.Min(0.05, t - _last);
        _last = t;

        while (_launched < _bursts && t >= _launched * 0.42) { Burst(); _launched++; }

        foreach (var p in _ps)
        {
            p.Age += dt;
            p.Px = p.X;
            p.Py = p.Y;
            p.Vy += 260 * dt;
            double drag = Math.Exp(-1.3 * dt);
            p.Vx *= drag;
            p.Vy *= drag;
            p.X += p.Vx * dt;
            p.Y += p.Vy * dt;
        }
        _ps.RemoveAll(p => p.Age >= p.Life);
        _canvas.InvalidateVisual();

        if (t >= Duration)
        {
            _timer.Stop();
            Close();
        }
    }

    private static Geometry StarShape(double r)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            for (int i = 0; i < 10; i++)
            {
                double rr = i % 2 == 0 ? r : r * 0.42;
                double a = -Math.PI / 2 + i * Math.PI / 5;
                var p = new Point(Math.Cos(a) * rr, Math.Sin(a) * rr);
                if (i == 0) c.BeginFigure(p, true, true); else c.LineTo(p, true, false);
            }
        }
        g.Freeze();
        return g;
    }

    private void Draw(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        double t = _sw.Elapsed.TotalSeconds;

        foreach (var p in _ps)
        {
            double k = 1 - p.Age / p.Life;
            byte a = (byte)Math.Max(0, Math.Min(255, 255 * k));
            var col = Color.FromArgb(a, p.Color.R, p.Color.G, p.Color.B);
            var brush = new SolidColorBrush(col);
            var pen = new Pen(brush, Math.Max(0.8, p.Size * 0.6));
            dc.DrawLine(pen, new Point(p.Px - p.Vx * 0.03, p.Py - p.Vy * 0.03), new Point(p.X, p.Y));
            dc.DrawEllipse(brush, null, new Point(p.X, p.Y), p.Size * (0.5 + 0.5 * k), p.Size * (0.5 + 0.5 * k));
        }

        // the big "+N" with a star
        double alpha = t < 0.3 ? t / 0.3 : t < 3.0 ? 1 : Math.Max(0, 1 - (t - 3.0) / 1.0);
        if (alpha <= 0) return;
        double scale = 0.6 + 0.4 * Math.Min(1, t / 0.4) + (t < 0.4 ? 0.15 * Math.Sin(t / 0.4 * Math.PI) : 0);
        string txt = "+" + _stars;
        var ft = new FormattedText(txt, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), 110, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        double starR = 54, gap = 18;
        double total = starR * 2 + gap + ft.Width;
        dc.PushOpacity(alpha);
        dc.PushTransform(new TranslateTransform(w / 2, h * 0.30 - 30 * (1 - Math.Min(1, t / 0.6))));
        dc.PushTransform(new ScaleTransform(scale, scale));
        double x0 = -total / 2;
        var gold = new SolidColorBrush(Color.FromRgb(0xff, 0xc1, 0x07));
        // soft shadow, then the shapes
        dc.PushTransform(new TranslateTransform(x0 + starR + 3, 5));
        dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(110, 0, 0, 0)), null, StarShape(starR));
        dc.Pop();
        dc.PushTransform(new TranslateTransform(x0 + starR, 0));
        dc.DrawGeometry(gold, new Pen(new SolidColorBrush(Color.FromRgb(0xff, 0x8f, 0x00)), 4), StarShape(starR));
        dc.Pop();
        var shadow = new FormattedText(txt, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), 110, new SolidColorBrush(Color.FromArgb(120, 0, 0, 0)), VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(shadow, new Point(x0 + starR * 2 + gap + 4, -ft.Height / 2 + 5));
        dc.DrawText(ft, new Point(x0 + starR * 2 + gap, -ft.Height / 2));
        dc.Pop();
        dc.Pop();
        dc.Pop();
    }
}
