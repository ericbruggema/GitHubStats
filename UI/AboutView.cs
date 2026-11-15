using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace GitHubStats;

// ============================================================================ About

internal sealed class AboutView : View
{
    public const string RepoUrl = "https://github.com/ericbruggema/GitHubStats";
    private const string ProfileUrl = "https://github.com/ericbruggema";
    private const string LinkedInUrl = "https://www.linkedin.com/in/eric-bruggema-28a81a186/";
    private const string FacebookUrl = "https://www.facebook.com/eric.bruggema";

    /// <summary>The GitHub mark (Octicons "mark-github", MIT).</summary>
    private const string GitHubMark = "M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82.64-.18 1.32-.27 2-.27.68 0 1.36.09 2 .27 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.013 8.013 0 0016 8c0-4.42-3.58-8-8-8z";

    private readonly StackPanel _root;
    private readonly TextBlock _starCount;

    public AboutView()
    {
        Children.Add(Scroll(out _root));
        _root.MaxWidth = 860;
        _root.HorizontalAlignment = HorizontalAlignment.Left;

        // ---- hero: icon, name, version, tagline
        var hero = new DockPanel { Margin = new Thickness(0, 4, 0, 6) };
        var logo = new Image { Source = LoadLogo(), Width = 72, Height = 72, Margin = new Thickness(0, 0, 18, 0), VerticalAlignment = VerticalAlignment.Center };
        RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality);
        hero.Children.Add(logo);
        var name = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var title = new StackPanel { Orientation = Orientation.Horizontal };
        title.Children.Add(Ui.Text("GitHub Stats", 28, "Text", true));
        var ver = new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(8, 2, 8, 3), Margin = new Thickness(12, 6, 0, 0), VerticalAlignment = VerticalAlignment.Center, BorderThickness = new Thickness(1) };
        ver.Res(Border.BorderBrushProperty, "Link");
        ver.Child = Ui.Text("v" + Host.CurrentVersion.ToString(3), 12, "Link", true);
        title.Children.Add(ver);
        name.Children.Add(title);
        name.Children.Add(Ui.Text("See who downloads and stars your GitHub projects, right on your Windows desktop.", 13.5, "Muted", false, true));
        hero.Children.Add(name);
        _root.Children.Add(hero);

        // ---- star card (the fancy one)
        _starCount = Ui.Text("", 12, "Muted");
        _root.Children.Add(StarCard());

        // ---- the story
        _root.Children.Add(Head("The story"));
        var story = new StackPanel();
        story.Children.Add(Para(
            "I build small tools in my spare time (a taskbar monitor, Christmas lights for Windows and Android, a garden planner) " +
            "and put them on GitHub for anyone to use. After every release I kept asking myself the same thing: is anybody actually using this?"));
        story.Children.Add(Para(
            "GitHub knows, but it doesn't make it easy. Download counts are buried in the API, " +
            "star dates are not shown anywhere, and traffic data disappears after 14 days. So I was opening the same pages again and again, " +
            "trying to remember yesterday's numbers."));
        story.Children.Add(Para(
            "GitHub Stats does that for me. It quietly collects the numbers a few times a day, keeps the full history on my own PC, " +
            "and lets me know when something happens: a new star, a download milestone, a sudden spike. " +
            "No website, no account, no cloud. It is a single program that keeps its data in its own folder."));
        var sign = Para("Every star, every download and every bit of feedback still makes my day. Thanks for using it!");
        sign.FontStyle = FontStyles.Italic;
        story.Children.Add(sign);
        var by = Ui.Text("Eric Bruggema", 13, "Text", true);
        by.Margin = new Thickness(0, 2, 0, 0);
        story.Children.Add(by);
        _root.Children.Add(Ui.Box(story, 10, new Thickness(22, 18, 22, 18)));

        // ---- links
        _root.Children.Add(Head("Find me"));
        var links = new WrapPanel();
        links.Children.Add(LinkCard("", "GitHub", "@ericbruggema", "All my projects", ProfileUrl, "#24292f"));
        links.Children.Add(LinkCard("", "LinkedIn", "Eric Bruggema", "Let's connect", LinkedInUrl, "#0a66c2"));
        links.Children.Add(LinkCard("", "Facebook", "eric.bruggema", "Say hi", FacebookUrl, "#1877f2"));
        links.Children.Add(LinkCard("", "Feedback", "Issues on GitHub", "Ideas and bug reports", RepoUrl + "/issues", "#d95926"));
        _root.Children.Add(links);

        // ---- credits
        _root.Children.Add(Head("Credits"));
        var cr = new StackPanel();
        cr.Children.Add(Credit("Idea, design and testing", "Eric Bruggema", ProfileUrl));
        cr.Children.Add(Credit("Built with", "Claude Code (Anthropic)", "https://claude.com/claude-code"));
        cr.Children.Add(Credit("Runs on", ".NET and WPF (MIT)", "https://dotnet.microsoft.com"));
        cr.Children.Add(Credit("Installer", "Inno Setup", "https://jrsoftware.org/isinfo.php"));
        cr.Children.Add(Credit("Data", "GitHub REST API", "https://docs.github.com/rest"));
        cr.Children.Add(Credit("License", "MIT - free to use, change and share", RepoUrl + "/blob/main/LICENSE"));
        _root.Children.Add(Ui.Box(cr, 10, new Thickness(22, 12, 22, 12)));
    }

    public override void Update(Dashboard db)
    {
        // our own repository's stars, if the user happens to be me (or tracks it)
        var mine = Host.App.Data.Repos.Values.FirstOrDefault(r => r.Name.Equals("GitHubStats", StringComparison.OrdinalIgnoreCase));
        _starCount.Text = mine != null ? Fmt.N(mine.Stars) + (mine.Stars == 1 ? " star so far" : " stars so far") : "";
    }

    // ------------------------------------------------------------------ pieces

    private FrameworkElement StarCard()
    {
        var gold = (Color)ColorConverter.ConvertFromString("#f2b705");
        var orange = (Color)ColorConverter.ConvertFromString("#e8590c");
        var pink = (Color)ColorConverter.ConvertFromString("#d6336c");

        // spinning, pulsing star
        var star = new TextBlock
        {
            Text = "", // FavoriteStarFill
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 46,
            Foreground = new LinearGradientBrush(gold, orange, 45),
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = new Point(0.5, 0.5),
            Margin = new Thickness(4, 0, 22, 0),
        };
        var rot = new RotateTransform();
        var scale = new ScaleTransform(1, 1);
        var tg = new TransformGroup();
        tg.Children.Add(scale);
        tg.Children.Add(rot);
        star.RenderTransform = tg;
        var pulse = new DoubleAnimation(1, 1.12, TimeSpan.FromSeconds(1.1)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
        var wobble = new DoubleAnimation(-8, 8, TimeSpan.FromSeconds(2.2)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
        rot.BeginAnimation(RotateTransform.AngleProperty, wobble);

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(Ui.Text("Like GitHub Stats? Give it a star!", 17, "Text", true));
        var sub = Ui.Text("It's free and takes two seconds. Stars help other developers find the project, and they keep me building. " +
                          "(And yes: GitHub Stats will tell me when you did.)", 12.5, "Muted", false, true);
        sub.Margin = new Thickness(0, 3, 0, 0);
        text.Children.Add(sub);
        _starCount.Margin = new Thickness(0, 4, 0, 0);
        text.Children.Add(_starCount);

        // gradient button
        var btnText = new StackPanel { Orientation = Orientation.Horizontal };
        btnText.Children.Add(new TextBlock { Text = "", FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 14, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 1, 8, 0) });
        btnText.Children.Add(new TextBlock { Text = "Star on GitHub", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
        var btn = new Border
        {
            Child = btnText,
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(20, 9, 22, 10),
            Background = new LinearGradientBrush(orange, pink, 0),
            Cursor = Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(18, 0, 0, 0),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(1, 1),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = orange, BlurRadius = 18, ShadowDepth = 0, Opacity = 0.45 },
            ToolTip = RepoUrl,
        };
        btn.MouseEnter += (s, e) => Grow(btn, 1.06);
        btn.MouseLeave += (s, e) => Grow(btn, 1.0);
        btn.MouseLeftButtonUp += (s, e) => { Ui.OpenUrl(RepoUrl); Host.Celebrate(1, true); };

        var dock = new DockPanel();
        DockPanel.SetDock(star, Dock.Left);
        DockPanel.SetDock(btn, Dock.Right);
        dock.Children.Add(star);
        dock.Children.Add(btn);
        dock.Children.Add(text);

        // card with a gradient border
        var inner = new Border { Child = dock, CornerRadius = new CornerRadius(11), Padding = new Thickness(20, 18, 22, 18) };
        inner.Res(Border.BackgroundProperty, "Card");
        var outer = new Border
        {
            Child = inner,
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(1.5),
            Margin = new Thickness(0, 18, 0, 4),
            Background = new LinearGradientBrush(new GradientStopCollection { new(gold, 0), new(orange, 0.5), new(pink, 1) }, 0),
        };
        return outer;
    }

    private static void Grow(Border b, double to)
    {
        var st = (ScaleTransform)b.RenderTransform;
        var a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(140)) { EasingFunction = new QuadraticEase() };
        st.BeginAnimation(ScaleTransform.ScaleXProperty, a);
        st.BeginAnimation(ScaleTransform.ScaleYProperty, a);
    }

    private static TextBlock Head(string text)
    {
        var t = Ui.Text(text, 14, "Text", true);
        t.Margin = new Thickness(2, 24, 0, 8);
        return t;
    }

    private static TextBlock Para(string text)
    {
        var t = Ui.Text(text, 13.5, "Text", false, true);
        t.LineHeight = 21;
        t.Margin = new Thickness(0, 0, 0, 10);
        return t;
    }

    private static FrameworkElement LinkCard(string glyph, string title, string handle, string hint, string url, string color)
    {
        var c = (Color)ColorConverter.ConvertFromString(color);
        var badge = new Border { Width = 38, Height = 38, CornerRadius = new CornerRadius(10), Background = new SolidColorBrush(c), Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        badge.Child = new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontSize = 17, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        if (title is "LinkedIn" or "Facebook")
            badge.Child = new TextBlock { Text = title == "LinkedIn" ? "in" : "f", FontFamily = new FontFamily("Segoe UI"), FontWeight = FontWeights.Bold, FontSize = 19, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 2) };
        if (title == "GitHub")
            badge.Child = new System.Windows.Shapes.Path { Data = Geometry.Parse(GitHubMark), Fill = Brushes.White, Width = 20, Height = 20, Stretch = Stretch.Uniform };
        var txt = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        txt.Children.Add(Ui.Text(title, 13.5, "Text", true));
        txt.Children.Add(Ui.Text(handle, 12, "Muted"));
        var row = new DockPanel();
        DockPanel.SetDock(badge, Dock.Left);
        row.Children.Add(badge);
        row.Children.Add(txt);
        var b = Ui.Box(row, 10, new Thickness(14, 12, 18, 12));
        b.Width = 205;
        b.Margin = new Thickness(0, 0, 10, 10);
        b.Cursor = Cursors.Hand;
        b.ToolTip = hint + " - " + url;
        b.MouseEnter += (s, e) => b.BorderBrush = new SolidColorBrush(c);
        b.MouseLeave += (s, e) => b.Res(Border.BorderBrushProperty, "Border");
        b.MouseLeftButtonUp += (s, e) => Ui.OpenUrl(url);
        return b;
    }

    private static FrameworkElement Credit(string what, string who, string url)
    {
        var g = new Grid { Margin = new Thickness(0, 5, 0, 5) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.Children.Add(Ui.Text(what, 12.5, "Muted"));
        var l = S.Link(who, () => Ui.OpenUrl(url));
        l.FontSize = 12.5;
        Grid.SetColumn(l, 1);
        g.Children.Add(l);
        return g;
    }

    private static ImageSource? LoadLogo()
    {
        try
        {
            using var s = typeof(AboutView).Assembly.GetManifestResourceStream("GitHubStats.app.ico");
            if (s == null) return null;
            var dec = new IconBitmapDecoder(s, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            return dec.Frames.OrderByDescending(f => f.PixelWidth).First();
        }
        catch { return null; }
    }
}
