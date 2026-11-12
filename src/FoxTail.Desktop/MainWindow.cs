using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using FoxTail.Core;

namespace FoxTail.Desktop;

public sealed class MainWindow : Window
{
    private readonly AppSettings settings;
    private readonly string settingsPath;
    private readonly TabControl tabs = new();
    private readonly List<LogView> views = [];
    private readonly TextBlock status = new() { Foreground = Ui.Brush("#9CAFC2"), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly CheckBox follow = new() { Content = "Follow tail", IsChecked = true, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button pause;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private bool busy;
    private bool closed;
    private bool paused;
    private string? settingsWarning;

    public MainWindow()
    {
        Style = (Style)Application.Current.FindResource(typeof(Window));
        Title = "FoxTail · Live log viewer";
        Width = 1280; Height = 820; MinWidth = 900; MinHeight = 560;
        Background = Ui.Brush("#101720");
        settingsPath = Path.Combine(AppContext.BaseDirectory, "foxtail.settings.json");
        string? startupWarning = null;
        try { settings = AppSettings.Load(settingsPath); }
        catch (Exception ex) { settings = new(); startupWarning = "Could not load settings: " + ex.Message; }
        var root = Ui.Grid("Auto,Auto,*,Auto");
        root.Margin = new Thickness(22, 18, 22, 18);
        var heading = Ui.Grid(columns: "*,Auto");
        heading.Margin = new Thickness(0, 0, 0, 20);
        var title = new StackPanel();
        title.Children.Add(new TextBlock { Text = "FoxTail", FontSize = 28, FontWeight = FontWeights.Bold, Foreground = Ui.Brush("#F3A76C") });
        title.Children.Add(new TextBlock { Text = "A clear view of what's happening.", Foreground = Ui.Brush("#9CAFC2") });
        heading.Children.Add(title);
        var badge = new TextBlock { Text = "LIVE LOG WORKSPACE", FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Foreground = Ui.Brush("#86CFC1") };
        Grid.SetColumn(badge, 1); heading.Children.Add(badge); root.Children.Add(heading);
        var tools = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
        tools.Children.Add(Button("Open files…", OpenFiles));
        pause = Button("Pause monitoring", () => { paused = !paused; pause!.Content = paused ? "Resume monitoring" : "Pause monitoring"; UpdateStatus(); });
        tools.Children.Add(pause);
        tools.Children.Add(Button("Highlight rules…", () =>
        {
            var editor = new RulesWindow(settings.Rules) { Owner = this };
            if (editor.ShowDialog() == true && editor.Result is { } result)
            {
                settings.Rules = result;
                foreach (var view in views) view.Restyle();
                Save();
            }
        }));
        tools.Children.Add(Button("Close tab", CloseTab));
        tools.Children.Add(Button("Copy line", () =>
        {
            if (Active?.SelectedText is not { } text) return;
            try { Clipboard.SetText(text); }
            catch (ExternalException ex) { MessageBox.Show(this, "Could not access the clipboard: " + ex.Message, "FoxTail"); }
        }));
        follow.Margin = new Thickness(12, 0, 14, 0); tools.Children.Add(follow);
        var wrap = new CheckBox { Content = "Wrap", IsChecked = settings.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 14, 0) };
        RoutedEventHandler updateWrap = (_, _) => { settings.Wrap = wrap.IsChecked == true; foreach (var view in views) view.Restyle(); Save(); };
        wrap.Checked += updateWrap;
        wrap.Unchecked += updateWrap;
        tools.Children.Add(wrap);
        tools.Children.Add(Button("A−", () => Font(-1))); tools.Children.Add(Button("A+", () => Font(1)));
        Grid.SetRow(tools, 1); root.Children.Add(tools);
        Grid.SetRow(tabs, 2); root.Children.Add(tabs);
        tabs.SelectionChanged += (_, _) => UpdateStatus();
        var bottom = Ui.Grid(columns: "*,Auto");
        bottom.Margin = new Thickness(0, 12, 0, 0);
        bottom.Children.Add(status);
        var hint = new TextBlock { Text = "First matching rule wins  ·  UTF-8", FontSize = 11, Foreground = Ui.Brush("#75879B") };
        Grid.SetColumn(hint, 1); bottom.Children.Add(hint); Grid.SetRow(bottom, 3); root.Children.Add(bottom);
        Content = root;
        timer.Tick += async (_, _) => await Poll();
        Loaded += (_, _) =>
        {
            foreach (var path in Program.Arguments.Where(arg => !arg.StartsWith('-'))) AddFile(path);
            timer.Start();
            UpdateStatus();
            if (startupWarning is not null) MessageBox.Show(this, startupWarning, "FoxTail", MessageBoxButton.OK, MessageBoxImage.Warning);
        };
        Closed += (_, _) => { closed = true; timer.Stop(); Save(); };
    }

    private LogView? Active => (tabs.SelectedItem as TabItem)?.Content as LogView;
    internal static Button Button(string text, Action action)
    {
        var button = new Button { Content = text, Margin = new Thickness(0, 0, 8, 4), Padding = new Thickness(12, 7, 12, 7) };
        button.Click += (_, _) => action(); return button;
    }
    private void Font(int change) { settings.FontSize = Math.Clamp(settings.FontSize + change, 10, 24); foreach (var view in views) view.Restyle(); Save(); }
    private void OpenFiles()
    {
        var dialog = new OpenFileDialog { Title = "Open logs to monitor", Multiselect = true, Filter = "All text and log files|*.*" };
        if (dialog.ShowDialog(this) == true)
            foreach (var path in dialog.FileNames) AddFile(path);
    }
    private void AddFile(string path)
    {
        path = Path.GetFullPath(path);
        var existing = views.FirstOrDefault(v => string.Equals(v.Path, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) { tabs.SelectedItem = tabs.Items.Cast<TabItem>().First(t => t.Content == existing); return; }
        var view = new LogView(path, settings);
        views.Add(view);
        var tab = new TabItem { Header = System.IO.Path.GetFileName(path), Content = view };
        tab.ToolTip = path; tabs.Items.Add(tab); tabs.SelectedItem = tab;
    }
    private void CloseTab()
    {
        if (tabs.SelectedItem is not TabItem tab) return;
        views.Remove((LogView)tab.Content!); tabs.Items.Remove(tab); UpdateStatus();
    }
    private async Task Poll()
    {
        if (busy || paused || closed) return;
        busy = true;
        try
        {
            foreach (var view in views.ToArray())
            {
                try
                {
                    var batch = await Task.Run(view.Reader.Poll);
                    if (closed || !views.Contains(view)) continue;
                    view.Accept(batch, follow.IsChecked == true && view == Active);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { view.Error = ex.Message; }
            }
            UpdateStatus();
        }
        finally { busy = false; }
    }
    private void UpdateStatus()
    {
        status.Text = Active is { } v
            ? $"{(paused ? "PAUSED" : v.Error is not null ? "RETRYING" : "MONITORING")}  ·  {v.Path}  ·  {v.Count:N0} lines  ·  {v.Progress}" + (v.Error is null ? "" : "  ·  " + v.Error)
            : "Open a text or log file to begin. Files stay on your computer.";
        if (settingsWarning is not null) status.Text += "  ·  " + settingsWarning;
        status.ToolTip = status.Text;
    }
    private void Save()
    {
        try { settings.Save(settingsPath); settingsWarning = null; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { settingsWarning = "Settings could not be saved. Move FoxTail to a writable folder. " + ex.Message; UpdateStatus(); }
    }
}
