using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace GitHubStats;

// ============================================================================ Export

internal sealed class ExportView : View
{
    private readonly StackPanel _root;
    private readonly TextBox _md;
    private readonly TextBlock _status;
    private readonly TextBlock _pub;

    public ExportView()
    {
        Children.Add(Scroll(out _root));
        _root.Children.Add(Title("Export & share"));

        _root.Children.Add(Ui.Heading("Save as file"));
        var csv = new WrapPanel();
        foreach (string kind in Exports.CsvKinds)
        {
            string k = kind;
            csv.Children.Add(Ui.Btn("CSV: " + k, () => SaveCsv(k)));
        }
        _root.Children.Add(csv);
        var other = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        other.Children.Add(Ui.Btn("JSON", () => SaveText("github-stats.json", "JSON|*.json", Page.Export(Host.App, false))));
        other.Children.Add(Ui.Btn("JSON with history", () => SaveText("github-stats-history.json", "JSON|*.json", Page.Export(Host.App, true))));
        other.Children.Add(Ui.Btn("Web page (stats.html)", () => SaveText("stats.html", "HTML|*.html", Exports.StaticHtml(Host.App))));
        _root.Children.Add(other);
        _root.Children.Add(Ui.Text("CSV files open correctly in Excel. The web page is a plain file you can upload or embed anywhere.", 11, "Muted", false, true));

        _root.Children.Add(Ui.Heading("README block (Markdown)"));
        _md = new TextBox
        {
            IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, Height = 170,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"), FontSize = 12, Padding = new Thickness(8),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        _md.Res(Control.BackgroundProperty, "Card");
        _md.Res(Control.ForegroundProperty, "Text");
        _md.Res(Control.BorderBrushProperty, "Border");
        _root.Children.Add(_md);
        var row = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        row.Children.Add(Ui.Btn("Copy", () =>
        {
            try { Clipboard.SetText(_md.Text); _status.Text = "Copied to the clipboard."; }
            catch (Exception ex) { _status.Text = "Could not copy: " + ex.Message; }
        }, true));
        _root.Children.Add(row);

        _root.Children.Add(Ui.Heading("Publish folder"));
        _pub = Ui.Text("", 12, "Muted", false, true);
        _root.Children.Add(_pub);
        var prow = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        prow.Children.Add(Ui.Btn("Write it now", () =>
        {
            Host.App.PublishShareNow();
            _status.Text = Host.App.ShareProblem ?? "Files written.";
        }));
        prow.Children.Add(Ui.Btn("Open folder", () =>
        {
            string? p = Host.App.PublishPath;
            if (p == null) { _status.Text = "The publish folder is switched off in Settings."; return; }
            try { Directory.CreateDirectory(p); Ui.OpenPath(p); } catch (Exception ex) { _status.Text = ex.Message; }
        }));
        _root.Children.Add(prow);

        _status = Ui.Text("", 12, "Muted", false, true);
        _status.Margin = new Thickness(0, 8, 0, 0);
        _root.Children.Add(_status);
    }

    private void SaveCsv(string kind)
    {
        var c = Exports.Csv(Host.App, kind);
        if (c == null) { _status.Text = "Unknown export."; return; }
        Save(c.Value.FileName, "CSV|*.csv", c.Value.Content, true);
    }

    private void SaveText(string name, string filter, string content) => Save(name, filter, content, false);

    private void Save(string name, string filter, string content, bool bom)
    {
        try
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { FileName = name, Filter = filter };
            if (dlg.ShowDialog() != true) return;
            File.WriteAllText(dlg.FileName, content, new UTF8Encoding(bom));
            _status.Text = "Saved " + dlg.FileName;
        }
        catch (Exception ex) { _status.Text = "Could not save: " + ex.Message; }
    }

    public override void Update(Dashboard db)
    {
        string? p = Host.App.PublishPath;
        _pub.Text = p != null
            ? "After every complete refresh, stats.md, stats.html, stats.json, the CSV files and SVG badges are written to " + p + " - ready to upload or commit."
            : "The publish folder is switched off (see Settings).";
        try { _md.Text = db.HasData ? Exports.Markdown(Host.App) : "No data yet."; } catch (Exception ex) { _md.Text = ex.Message; }
    }
}

