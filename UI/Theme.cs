using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace GitHubStats;

/// <summary>Light/dark colours. Everything is looked up by name from the application resources, so a change applies at once.</summary>
internal static class Theme
{
    public static bool Dark { get; private set; }
    public static string Mode { get; private set; } = "auto";
    public static event Action? Changed;

    private static readonly Dictionary<string, string> Light = new()
    {
        ["Bg"] = "#f6f8fa", ["Card"] = "#ffffff", ["Text"] = "#1f2328", ["Muted"] = "#656d76", ["Border"] = "#d0d7de",
        ["Link"] = "#0969da", ["Up"] = "#1a7f37", ["Down"] = "#cf222e", ["Accent"] = "#eaeef2",
        ["WarnBg"] = "#fff8c5", ["WarnBorder"] = "#d4a72c",
        ["S0"] = "#2a78d6", ["S1"] = "#eb6834", ["S2"] = "#1baf7a",
    };

    private static readonly Dictionary<string, string> DarkColors = new()
    {
        ["Bg"] = "#0d1117", ["Card"] = "#161b22", ["Text"] = "#e6edf3", ["Muted"] = "#8d96a0", ["Border"] = "#30363d",
        ["Link"] = "#4493f8", ["Up"] = "#3fb950", ["Down"] = "#f85149", ["Accent"] = "#21262d",
        ["WarnBg"] = "#3a2d00", ["WarnBorder"] = "#9e6a03",
        ["S0"] = "#3987e5", ["S1"] = "#d95926", ["S2"] = "#199e70",
    };

    private static bool SystemIsDark()
    {
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return k?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }

    private static SolidColorBrush Make(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    /// <summary>mode: "auto" (follow Windows), "light" or "dark"</summary>
    public static void Apply(string mode)
    {
        Mode = mode is "light" or "dark" ? mode : "auto";
        Dark = Mode == "dark" || (Mode == "auto" && SystemIsDark());
        var res = Application.Current.Resources;
        var pal = Dark ? DarkColors : Light;
        foreach (var kv in pal) res[kv.Key] = Make(kv.Value);
        ApplyAccent(res);

        // Standard controls (DataGrid, menus, text boxes ...) take their colours from the system colours
        res[SystemColors.WindowBrushKey] = res["Card"];
        res[SystemColors.WindowTextBrushKey] = res["Text"];
        res[SystemColors.ControlBrushKey] = res["Card"];
        res[SystemColors.ControlTextBrushKey] = res["Text"];
        res[SystemColors.ControlLightBrushKey] = res["Accent"];
        res[SystemColors.ControlLightLightBrushKey] = res["Card"];
        res[SystemColors.ControlDarkBrushKey] = res["Border"];
        res[SystemColors.ControlDarkDarkBrushKey] = res["Muted"];
        res[SystemColors.GrayTextBrushKey] = res["Muted"];
        res[SystemColors.MenuBrushKey] = res["Card"];
        res[SystemColors.MenuTextBrushKey] = res["Text"];
        res[SystemColors.HighlightBrushKey] = res["Link"];
        res[SystemColors.HighlightTextBrushKey] = Make("#ffffff");
        res[SystemColors.InactiveSelectionHighlightBrushKey] = res["Accent"];
        res[SystemColors.InactiveSelectionHighlightTextBrushKey] = res["Text"];

        Changed?.Invoke();
    }

    /// <summary>Accent colours: name -> (light, dark). Used for links, toggles, buttons and the first chart series.</summary>
    public static readonly (string Name, string Label, string Light, string Dark)[] Accents =
    {
        ("blue", "Blue", "#0969da", "#4493f8"),
        ("purple", "Purple", "#8250df", "#a371f7"),
        ("green", "Green", "#1a7f37", "#3fb950"),
        ("orange", "Orange", "#bc4c00", "#f0883e"),
        ("pink", "Pink", "#bf3989", "#db61a2"),
        ("teal", "Teal", "#0f7b8a", "#39c5cf"),
    };

    public static string Accent { get; set; } = "blue";

    private static void ApplyAccent(ResourceDictionary res)
    {
        foreach (var a in Accents)
        {
            if (a.Name != Accent) continue;
            if (a.Name == "blue") return; // the built-in palette
            res["Link"] = Make(Dark ? a.Dark : a.Light);
            res["S0"] = res["Link"];
            return;
        }
    }

    private static bool _styles;

    /// <summary>Modern thin scrollbars and a flat slider for the whole program (colours follow the theme).</summary>
    public static void InstallStyles()
    {
        if (_styles) return;
        _styles = true;
        const string x = """
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style x:Key="SbPage" TargetType="RepeatButton">
    <Setter Property="Focusable" Value="False"/><Setter Property="IsTabStop" Value="False"/>
    <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="RepeatButton"><Border Background="Transparent"/></ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style x:Key="SbThumb" TargetType="Thumb">
    <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Thumb">
      <Border x:Name="b" CornerRadius="4" Background="{DynamicResource Muted}" Opacity="0.45"/>
      <ControlTemplate.Triggers>
        <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="b" Property="Opacity" Value="0.8"/></Trigger>
        <Trigger Property="IsDragging" Value="True"><Setter TargetName="b" Property="Opacity" Value="1"/></Trigger>
      </ControlTemplate.Triggers>
    </ControlTemplate></Setter.Value></Setter>
  </Style>
  <ControlTemplate x:Key="SbV" TargetType="ScrollBar">
    <Border Background="Transparent" Padding="3,4,3,4">
      <Track x:Name="PART_Track" IsDirectionReversed="True">
        <Track.DecreaseRepeatButton><RepeatButton Command="ScrollBar.PageUpCommand" Style="{StaticResource SbPage}"/></Track.DecreaseRepeatButton>
        <Track.IncreaseRepeatButton><RepeatButton Command="ScrollBar.PageDownCommand" Style="{StaticResource SbPage}"/></Track.IncreaseRepeatButton>
        <Track.Thumb><Thumb Style="{StaticResource SbThumb}" MinHeight="24"/></Track.Thumb>
      </Track>
    </Border>
  </ControlTemplate>
  <ControlTemplate x:Key="SbH" TargetType="ScrollBar">
    <Border Background="Transparent" Padding="4,3,4,3">
      <Track x:Name="PART_Track">
        <Track.DecreaseRepeatButton><RepeatButton Command="ScrollBar.PageLeftCommand" Style="{StaticResource SbPage}"/></Track.DecreaseRepeatButton>
        <Track.IncreaseRepeatButton><RepeatButton Command="ScrollBar.PageRightCommand" Style="{StaticResource SbPage}"/></Track.IncreaseRepeatButton>
        <Track.Thumb><Thumb Style="{StaticResource SbThumb}" MinWidth="24"/></Track.Thumb>
      </Track>
    </Border>
  </ControlTemplate>
  <Style TargetType="ScrollBar">
    <Setter Property="Template" Value="{StaticResource SbV}"/>
    <Setter Property="Width" Value="12"/><Setter Property="MinWidth" Value="12"/>
    <Style.Triggers>
      <Trigger Property="Orientation" Value="Horizontal">
        <Setter Property="Template" Value="{StaticResource SbH}"/>
        <Setter Property="Width" Value="Auto"/><Setter Property="MinWidth" Value="0"/>
        <Setter Property="Height" Value="12"/><Setter Property="MinHeight" Value="12"/>
      </Trigger>
    </Style.Triggers>
  </Style>
  <Style TargetType="ContextMenu">
    <Setter Property="OverridesDefaultStyle" Value="True"/>
    <Setter Property="HasDropShadow" Value="False"/>
    <Setter Property="Foreground" Value="{DynamicResource Text}"/>
    <Setter Property="FontFamily" Value="Segoe UI"/>
    <Setter Property="FontSize" Value="13"/>
    <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ContextMenu">
      <Border Margin="0,0,10,10" Background="{DynamicResource Card}" BorderBrush="{DynamicResource Border}" BorderThickness="1" CornerRadius="8" Padding="0,4" MinWidth="190">
        <Border.Effect><DropShadowEffect BlurRadius="12" ShadowDepth="2" Opacity="0.35"/></Border.Effect>
        <StackPanel IsItemsHost="True" KeyboardNavigation.DirectionalNavigation="Cycle"/>
      </Border>
    </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style x:Key="{x:Static MenuItem.SeparatorStyleKey}" TargetType="Separator">
    <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Separator">
      <Border Height="1" Margin="10,4" Background="{DynamicResource Border}"/>
    </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style TargetType="Separator">
    <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Separator">
      <Border Height="1" Margin="10,4" Background="{DynamicResource Border}"/>
    </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style TargetType="MenuItem">
    <Setter Property="OverridesDefaultStyle" Value="True"/>
    <Setter Property="Foreground" Value="{DynamicResource Text}"/>
    <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="MenuItem">
      <Border x:Name="bd" Background="Transparent" CornerRadius="5" Padding="8,6,12,6" Margin="4,1">
        <Grid>
          <Grid.ColumnDefinitions><ColumnDefinition Width="24"/><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
          <TextBlock x:Name="check" Text="&#xE73E;" FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" FontSize="11" VerticalAlignment="Center" Visibility="Hidden" Foreground="{DynamicResource Link}"/>
          <ContentPresenter Grid.Column="1" ContentSource="Header" RecognizesAccessKey="True" VerticalAlignment="Center"/>
          <TextBlock x:Name="arrow" Grid.Column="2" Text="&#xE76C;" FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" FontSize="10" Margin="18,0,0,0" VerticalAlignment="Center" Visibility="Collapsed" Foreground="{DynamicResource Muted}"/>
          <Popup x:Name="PART_Popup" Placement="Right" HorizontalOffset="6" VerticalOffset="-6" IsOpen="{Binding IsSubmenuOpen, RelativeSource={RelativeSource TemplatedParent}}" AllowsTransparency="True" Focusable="False" PopupAnimation="Fade">
            <Border Margin="0,0,10,10" Background="{DynamicResource Card}" BorderBrush="{DynamicResource Border}" BorderThickness="1" CornerRadius="8" Padding="0,4" MinWidth="120">
              <Border.Effect><DropShadowEffect BlurRadius="12" ShadowDepth="2" Opacity="0.35"/></Border.Effect>
              <StackPanel IsItemsHost="True" KeyboardNavigation.DirectionalNavigation="Cycle"/>
            </Border>
          </Popup>
        </Grid>
      </Border>
      <ControlTemplate.Triggers>
        <Trigger Property="IsHighlighted" Value="True"><Setter TargetName="bd" Property="Background" Value="{DynamicResource Accent}"/></Trigger>
        <Trigger Property="IsChecked" Value="True"><Setter TargetName="check" Property="Visibility" Value="Visible"/></Trigger>
        <Trigger Property="HasItems" Value="True"><Setter TargetName="arrow" Property="Visibility" Value="Visible"/></Trigger>
        <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.45"/></Trigger>
      </ControlTemplate.Triggers>
    </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style x:Key="SlFill" TargetType="RepeatButton">
    <Setter Property="Focusable" Value="False"/><Setter Property="IsTabStop" Value="False"/>
    <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="RepeatButton">
      <Border Height="4" CornerRadius="2" Background="{DynamicResource Link}" VerticalAlignment="Center"/>
    </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style x:Key="SlThumb" TargetType="Thumb">
    <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Thumb">
      <Grid Width="18" Height="18">
        <Ellipse Fill="{DynamicResource Card}" Stroke="{DynamicResource Border}" StrokeThickness="1"/>
        <Ellipse x:Name="dot" Width="10" Height="10" Fill="{DynamicResource Link}"/>
      </Grid>
      <ControlTemplate.Triggers>
        <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="dot" Property="Width" Value="12"/><Setter TargetName="dot" Property="Height" Value="12"/></Trigger>
        <Trigger Property="IsDragging" Value="True"><Setter TargetName="dot" Property="Width" Value="8"/><Setter TargetName="dot" Property="Height" Value="8"/></Trigger>
      </ControlTemplate.Triggers>
    </ControlTemplate></Setter.Value></Setter>
  </Style>
  <Style TargetType="Slider">
    <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Slider">
      <Grid Height="22" Background="Transparent">
        <Border Height="4" CornerRadius="2" Background="{DynamicResource Border}" VerticalAlignment="Center"/>
        <Track x:Name="PART_Track">
          <Track.DecreaseRepeatButton><RepeatButton Command="Slider.DecreaseLarge" Style="{StaticResource SlFill}"/></Track.DecreaseRepeatButton>
          <Track.IncreaseRepeatButton><RepeatButton Command="Slider.IncreaseLarge" Style="{StaticResource SbPage}"/></Track.IncreaseRepeatButton>
          <Track.Thumb><Thumb Style="{StaticResource SlThumb}"/></Track.Thumb>
        </Track>
      </Grid>
    </ControlTemplate></Setter.Value></Setter>
  </Style>
</ResourceDictionary>
""";
        Application.Current.Resources.MergedDictionaries.Add((ResourceDictionary)System.Windows.Markup.XamlReader.Parse(x));
    }

    public static Brush B(string key) => (Brush)Application.Current.Resources[key];

    /// <summary>Series colour 0..2</summary>
    public static Brush Slot(int i) => B("S" + Math.Max(0, Math.Min(2, i)));

    /// <summary>Re-applies "auto" when Windows switches between light and dark.</summary>
    public static void WatchSystem()
    {
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += (s, e) =>
        {
            if (Mode != "auto") return;
            Application.Current?.Dispatcher.BeginInvoke(new Action(() => Apply("auto")));
        };
    }
}
