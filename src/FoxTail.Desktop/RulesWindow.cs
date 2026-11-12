using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using FoxTail.Core;

namespace FoxTail.Desktop;

public sealed class RulesWindow : Window
{
    private readonly ObservableCollection<HighlightRule> rules;
    private readonly ListBox list = new();
    private readonly TextBox name = new();
    private readonly TextBox pattern = new();
    private readonly TextBox foreground = new();
    private readonly TextBox background = new();
    private readonly TextBox sample = new() { Text = "2026-11-12 10:42:08 ERROR Connection failed" };
    private readonly CheckBox enabled = new() { Content = "Enabled" };
    private readonly CheckBox regex = new() { Content = "Regular expression" };
    private readonly CheckBox matchCase = new() { Content = "Case sensitive" };
    private readonly TextBlock error = new() { Foreground = Brushes.Salmon, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock preview = new() { TextWrapping = TextWrapping.Wrap, MinHeight = 58 };
    private HighlightRule? editing;
    private bool selecting;
    public List<HighlightRule>? Result { get; private set; }

    public RulesWindow(IEnumerable<HighlightRule> source)
    {
        Style = (Style)Application.Current.FindResource(typeof(Window));
        rules = new ObservableCollection<HighlightRule>(source.Select(r => JsonSerializer.Deserialize<HighlightRule>(JsonSerializer.Serialize(r))!));
        Background = Ui.Brush("#101720");
        Title = "FoxTail · Highlight rules"; Width = 900; Height = 730; MinWidth = 800; MinHeight = 650;
        var root = Ui.Grid("Auto,*,Auto");
        root.Margin = new Thickness(24);
        root.Children.Add(new TextBlock { Text = "Highlight rules\nFirst enabled match wins. Move rules up to give them priority.", FontSize = 18, Margin = new Thickness(0, 0, 0, 20) });
        var body = Ui.Grid(columns: "260,24,*");
        var left = new DockPanel();
        var actions = new WrapPanel();
        actions.Children.Add(MainWindow.Button("Add", () => { if (!Commit()) return; var r = new HighlightRule { Pattern = "text" }; rules.Add(r); Select(r); }));
        actions.Children.Add(MainWindow.Button("Remove", () => { if (editing is null) return; var i = rules.IndexOf(editing); selecting = true; rules.Remove(editing); selecting = false; editing = null; Select(rules.ElementAtOrDefault(Math.Min(i, rules.Count - 1))); }));
        actions.Children.Add(MainWindow.Button("↑", () => Move(-1))); actions.Children.Add(MainWindow.Button("↓", () => Move(1)));
        DockPanel.SetDock(actions, Dock.Bottom); left.Children.Add(actions);
        list.ItemsSource = rules;
        list.ItemTemplate = (DataTemplate)Application.Current.FindResource("RuleTemplate");
        list.SelectionChanged += (_, _) =>
        {
            if (selecting) return;
            var next = list.SelectedItem as HighlightRule;
            if (!Commit()) { selecting = true; list.SelectedItem = editing; selecting = false; return; }
            Select(next);
        };
        left.Children.Add(list); body.Children.Add(left);
        var fields = new StackPanel();
        fields.Children.Add(Label("Rule name", name)); fields.Children.Add(Label("Text or regular expression", pattern));
        var checks = new WrapPanel();
        foreach (var check in new[] { enabled, regex, matchCase }) { check.Margin = new Thickness(0, 0, 14, 0); checks.Children.Add(check); }
        fields.Children.Add(checks);
        fields.Children.Add(Label("Text color · #RRGGBB", foreground)); fields.Children.Add(Label("Background color · #RRGGBB", background));
        fields.Children.Add(Label("Try a sample line", sample)); fields.Children.Add(new Border { Child = preview, Padding = new Thickness(12), Margin = new Thickness(0, 8, 0, 8), Background = Ui.Brush("#0B111A") }); fields.Children.Add(error);
        fields.Children.Add(new TextBlock { Text = "The winning rule supplies both colors for the entire line.\nRegex matches have a 25 ms timeout; timed-out rules are skipped.", Foreground = Ui.Brush("#9CAFC2"), FontSize = 12, TextWrapping = TextWrapping.Wrap });
        foreach (var field in new[] { pattern, foreground, background, sample }) field.TextChanged += (_, _) => Preview();
        foreach (var check in new[] { regex, matchCase, enabled })
        {
            check.Checked += (_, _) => Preview();
            check.Unchecked += (_, _) => Preview();
        }
        var fieldScroll = new ScrollViewer { Content = fields, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetColumn(fieldScroll, 2); body.Children.Add(fieldScroll); Grid.SetRow(body, 1); root.Children.Add(body);
        var footer = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        footer.Children.Add(MainWindow.Button("Import…", async () => await Import()));
        footer.Children.Add(MainWindow.Button("Export…", async () => await Export()));
        footer.Children.Add(MainWindow.Button("Cancel", () => DialogResult = false));
        footer.Children.Add(MainWindow.Button("Apply rules", () => { if (Commit()) { Result = rules.ToList(); DialogResult = true; } }));
        Grid.SetRow(footer, 2); root.Children.Add(footer); Content = root;
        Select(rules.FirstOrDefault());
    }
    private static FrameworkElement Label(string text, Control field)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) }; panel.Children.Add(new TextBlock { Text = text }); panel.Children.Add(field); return panel;
    }
    private HighlightRule Draft() => new()
    {
        Name = name.Text?.Trim() ?? "", Pattern = pattern.Text ?? "", Foreground = foreground.Text?.Trim() ?? "", Background = background.Text?.Trim() ?? "",
        Enabled = enabled.IsChecked == true, IsRegex = regex.IsChecked == true, MatchCase = matchCase.IsChecked == true
    };
    private bool Commit()
    {
        if (editing is null) return true;
        var draft = Draft();
        if (draft.Validate() is { } message) { error.Text = message; return false; }
        editing.Name = draft.Name.Length == 0 ? "Untitled rule" : draft.Name;
        editing.Pattern = draft.Pattern; editing.Foreground = draft.Foreground; editing.Background = draft.Background;
        editing.Enabled = draft.Enabled; editing.IsRegex = draft.IsRegex; editing.MatchCase = draft.MatchCase;
        // Refresh the label without changing the selected rule.
        selecting = true; list.Items.Refresh(); list.SelectedItem = editing; selecting = false;
        error.Text = ""; return true;
    }
    private void Select(HighlightRule? rule)
    {
        editing = rule; selecting = true; list.SelectedItem = rule; selecting = false;
        name.Text = rule?.Name ?? ""; pattern.Text = rule?.Pattern ?? "";
        foreground.Text = rule?.Foreground ?? "#FFFFFF"; background.Text = rule?.Background ?? "#7F1D1D";
        enabled.IsChecked = rule?.Enabled ?? true; regex.IsChecked = rule?.IsRegex ?? false; matchCase.IsChecked = rule?.MatchCase ?? false;
        foreach (var control in new Control[] { name, pattern, foreground, background, enabled, regex, matchCase }) control.IsEnabled = rule is not null;
        error.Text = ""; Preview();
    }
    private void Move(int offset)
    {
        if (!Commit() || editing is null) return;
        var index = rules.IndexOf(editing); var target = index + offset;
        if (target < 0 || target >= rules.Count) return;
        selecting = true; rules.Move(index, target); list.SelectedItem = editing; selecting = false;
    }
    private void Preview()
    {
        var rule = Draft();
        var valid = rule.Validate() is null;
        var matches = valid && rule.Matches(sample.Text ?? "");
        preview.Text = (matches ? "MATCH  ·  " : "NO MATCH  ·  ") + sample.Text;
        preview.Foreground = Ui.Brush(matches ? rule.Foreground : "#D7E0EA");
        preview.Background = Ui.Brush(matches ? rule.Background : "#0B111A");
    }
    private async Task Import()
    {
        try
        {
            var dialog = new OpenFileDialog { Title = "Import FoxTail rules", Filter = "JSON rules|*.json|All files|*.*" };
            if (dialog.ShowDialog(this) != true) return;
            await using var stream = File.OpenRead(dialog.FileName);
            var imported = await JsonSerializer.DeserializeAsync<List<HighlightRule>>(stream) ?? throw new InvalidDataException("Expected a JSON array of rules.");
            if (imported.Any(r => r is null || r.Validate() is not null)) throw new InvalidDataException("One or more rules contain invalid patterns or colors.");
            selecting = true; rules.Clear(); foreach (var rule in imported) rules.Add(rule); selecting = false; Select(rules.FirstOrDefault());
        }
        catch (Exception ex) { error.Text = "Import failed: " + ex.Message; }
    }
    private async Task Export()
    {
        if (!Commit()) return;
        try
        {
            var dialog = new SaveFileDialog { Title = "Export FoxTail rules", FileName = "foxtail.rules.json", Filter = "JSON rules|*.json", DefaultExt = ".json" };
            if (dialog.ShowDialog(this) != true) return;
            await using var stream = File.Create(dialog.FileName);
            await JsonSerializer.SerializeAsync(stream, rules.ToList(), new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex) { error.Text = "Export failed: " + ex.Message; }
    }
}
