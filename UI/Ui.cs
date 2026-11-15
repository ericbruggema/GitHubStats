using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;

namespace GitHubStats;

/// <summary>Small helpers that build the controls (the whole UI is written in code, no XAML files).</summary>
internal static class Ui
{
    private const string ButtonXaml =
        "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
        "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'>" +
        "<Border x:Name='bd' CornerRadius='6' BorderThickness='1' Padding='{TemplateBinding Padding}' " +
        "Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}'>" +
        "<ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border>" +
        "<ControlTemplate.Triggers>" +
        "<Trigger Property='IsMouseOver' Value='True'><Setter TargetName='bd' Property='Background' Value='{DynamicResource Accent}'/></Trigger>" +
        "<Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.5'/></Trigger>" +
        "</ControlTemplate.Triggers></ControlTemplate>";

    private static ControlTemplate? _buttonTemplate;

    public static void Res(this FrameworkElement e, DependencyProperty p, string key) => e.SetResourceReference(p, key);

    public static TextBlock Text(string text, double size = 13, string brush = "Text", bool bold = false, bool wrap = false)
    {
        var t = new TextBlock { Text = text, FontSize = size, VerticalAlignment = VerticalAlignment.Center };
        t.Res(TextBlock.ForegroundProperty, brush);
        if (bold) t.FontWeight = FontWeights.SemiBold;
        if (wrap) t.TextWrapping = TextWrapping.Wrap;
        return t;
    }

    public static Border Box(UIElement child, double radius = 8, Thickness? pad = null, string bg = "Card")
    {
        var b = new Border { Child = child, CornerRadius = new CornerRadius(radius), BorderThickness = new Thickness(1), Padding = pad ?? new Thickness(14, 10, 14, 10) };
        b.Res(Border.BackgroundProperty, bg);
        b.Res(Border.BorderBrushProperty, "Border");
        return b;
    }

    public static Button Btn(string text, Action click, bool primary = false)
    {
        _buttonTemplate ??= (ControlTemplate)XamlReader.Parse(ButtonXaml);
        var b = new Button { Content = text, Template = _buttonTemplate, Padding = new Thickness(14, 6, 14, 6), Margin = new Thickness(0, 0, 8, 0), Cursor = System.Windows.Input.Cursors.Hand };
        b.Res(Control.BackgroundProperty, primary ? "Link" : "Card");
        b.Res(Control.ForegroundProperty, primary ? "Card" : "Text");
        b.Res(Control.BorderBrushProperty, primary ? "Link" : "Border");
        b.Click += (s, e) => click();
        return b;
    }

    public static CheckBox Check(string text, bool value)
    {
        var c = new CheckBox { Content = text, IsChecked = value, Margin = new Thickness(0, 4, 0, 4) };
        c.Res(Control.ForegroundProperty, "Text");
        return c;
    }

    public static TextBox Input(string value, double width = 320)
    {
        var t = new TextBox { Text = value, Width = width, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(6, 4, 6, 4) };
        t.Res(Control.BackgroundProperty, "Card");
        t.Res(Control.ForegroundProperty, "Text");
        t.Res(Control.BorderBrushProperty, "Border");
        t.Res(System.Windows.Controls.Primitives.TextBoxBase.CaretBrushProperty, "Text");
        return t;
    }

    /// <summary>A row of radio buttons; returns the selected key via the getter.</summary>
    public static (Panel Panel, Func<string> Get) Choice(string group, string[] keys, string[] labels, string selected)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        var buttons = new List<RadioButton>();
        for (int i = 0; i < keys.Length; i++)
        {
            var r = new RadioButton { Content = labels[i], GroupName = group, Tag = keys[i], IsChecked = keys[i] == selected, Margin = new Thickness(0, 0, 16, 0) };
            r.Res(Control.ForegroundProperty, "Text");
            buttons.Add(r);
            p.Children.Add(r);
        }
        string Get()
        {
            foreach (var b in buttons) if (b.IsChecked == true) return (string)b.Tag;
            return keys[0];
        }
        return (p, Get);
    }

    /// <summary>A yellow-ish note (problems, progress).</summary>
    public static Border Banner(string text)
    {
        var t = Text(text, 13, "Text", false, true);
        var b = new Border { Child = t, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Padding = new Thickness(14, 9, 14, 9), Margin = new Thickness(0, 0, 0, 10) };
        b.Res(Border.BackgroundProperty, "WarnBg");
        b.Res(Border.BorderBrushProperty, "WarnBorder");
        return b;
    }

    public static Border Card(string label, string value, string detail, int trend)
    {
        var sp = new StackPanel();
        sp.Children.Add(Text(label.ToUpperInvariant(), 11, "Muted"));
        sp.Children.Add(Text(value, 24, "Text", true));
        if (detail.Length > 0) sp.Children.Add(Text(detail, 12, trend > 0 ? "Up" : trend < 0 ? "Down" : "Muted"));
        var b = Box(sp);
        b.MinWidth = 150;
        b.Margin = new Thickness(0, 0, 10, 10);
        return b;
    }

    public static TextBlock Heading(string text)
    {
        var t = Text(text, 15, "Text", true);
        t.Margin = new Thickness(0, 14, 0, 6);
        return t;
    }

    /// <summary>Rows of "name | bar | value".</summary>
    public static StackPanel BarList(IEnumerable<NameValue> items, Func<NameValue, string> valueText, int slot = 0)
    {
        var host = new StackPanel();
        foreach (var it in items)
        {
            var g = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 70 });

            var name = Text(it.Name, 13);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.ToolTip = it.Detail.Length > 0 ? it.Name + " - " + it.Detail : it.Name;
            g.Children.Add(name);

            var bar = new Grid { Height = 8, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            double p = Math.Max(0.5, Math.Min(100, it.Percent));
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(p, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100 - p, GridUnitType.Star) });
            var fill = new Border { CornerRadius = new CornerRadius(4) };
            fill.Res(Border.BackgroundProperty, "S" + slot);
            bar.Children.Add(fill);
            Grid.SetColumn(bar, 1);
            g.Children.Add(bar);

            var val = Text(valueText(it), 13);
            val.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(val, 2);
            g.Children.Add(val);
            host.Children.Add(g);
        }
        return host;
    }

    public static void OpenUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return;
        if (!url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { Logger.Write("Could not open " + url + ": " + ex.Message); }
    }

    public static void OpenPath(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { Logger.Write("Could not open " + path + ": " + ex.Message); }
    }

    // ---- DataGrid helpers

    private static Style? _rightStyle;

    private static Style RightStyle()
    {
        if (_rightStyle != null) return _rightStyle;
        var s = new Style(typeof(TextBlock));
        s.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right));
        s.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0)));
        _rightStyle = s;
        return s;
    }

    public static DataGridTextColumn Col(string header, string path, double width = 0, bool right = false, string? format = null, string? sortPath = null)
    {
        var b = new Binding(path) { Mode = BindingMode.OneWay };
        if (format != null) b.StringFormat = format;
        var c = new DataGridTextColumn { Header = header, Binding = b };
        if (width > 0) c.Width = new DataGridLength(width); else c.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
        if (right) c.ElementStyle = RightStyle();
        if (sortPath != null) c.SortMemberPath = sortPath;
        return c;
    }

    public static DataGrid Grid2(bool selectable = true)
    {
        var g = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            CanUserResizeRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            RowHeaderWidth = 0,
            BorderThickness = new Thickness(1),
            ColumnHeaderHeight = 30,
            RowHeight = 28,
            FontSize = 13,
            EnableRowVirtualization = true,
        };
        g.Res(Control.BackgroundProperty, "Card");
        g.Res(Control.ForegroundProperty, "Text");
        g.Res(Control.BorderBrushProperty, "Border");
        g.Res(DataGrid.RowBackgroundProperty, "Card");
        g.Res(DataGrid.AlternatingRowBackgroundProperty, "Card");
        g.Res(DataGrid.HorizontalGridLinesBrushProperty, "Border");
        g.Res(DataGrid.VerticalGridLinesBrushProperty, "Border");

        var head = new Style(typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));
        head.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("Accent")));
        head.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("Muted")));
        head.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension("Border")));
        head.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        head.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 0, 8, 0)));
        head.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
        g.ColumnHeaderStyle = head;

        var cell = new Style(typeof(DataGridCell));
        cell.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
        cell.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 0, 8, 0)));
        cell.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        cell.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty, null));
        g.CellStyle = cell;
        if (!selectable) g.IsHitTestVisible = true;
        return g;
    }
}
