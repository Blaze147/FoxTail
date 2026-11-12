using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FoxTail.Core;

namespace FoxTail.Desktop;

public sealed partial class LogView : UserControl
{
    public string Path { get; }
    public TailReader Reader { get; }
    public string? Error { get; set; }
    public string Progress { get; private set; } = "Waiting for first read";
    public int Count => rows.Count;
    public string? SelectedText => (LogList.SelectedItem as LogRow)?.Text;
    private readonly AppSettings settings;
    private readonly ObservableCollection<LogRow> rows = [];
    private long number;
    private long characters;
    private LogRow? partial;
    private long lastPosition = -1;

    public LogView(string path, AppSettings settings)
    {
        InitializeComponent();
        Path = path;
        Reader = new TailReader(path);
        this.settings = settings;
        LogList.ItemsSource = rows;
        Restyle();
    }

    public void Restyle()
    {
        LogList.FontSize = settings.FontSize;
        ScrollViewer.SetHorizontalScrollBarVisibility(LogList, settings.Wrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto);
        foreach (var row in rows) row.Restyle(settings);
        LogList.Items.Refresh();
    }

    public void Accept(TailBatch batch, bool follow)
    {
        Error = null;
        Progress = $"{batch.Position:N0} / {batch.Length:N0} bytes" + (batch.MoreAvailable ? " · catching up" : " · up to date");
        if (!batch.Reset && batch.Position == lastPosition)
        {
            if (follow && rows.Count > 0) LogList.ScrollIntoView(rows[^1]);
            return;
        }
        lastPosition = batch.Position;
        if (batch.Reset) { rows.Clear(); number = 0; characters = 0; partial = null; }
        if (partial is not null) { rows.Remove(partial); characters -= partial.Text.Length; partial = null; }
        foreach (var line in batch.Lines)
        {
            rows.Add(new LogRow(++number, line, settings));
            characters += line.Length;
        }
        if (batch.Partial.Length > 0)
        {
            partial = new LogRow(number + 1, batch.Partial, settings);
            rows.Add(partial);
            characters += batch.Partial.Length;
        }
        while (rows.Count > settings.RetainedLines || characters > 16 * 1024 * 1024)
        {
            characters -= rows[0].Text.Length;
            rows.RemoveAt(0);
        }
        if (follow && rows.Count > 0) LogList.ScrollIntoView(rows[^1]);
    }

    public sealed class LogRow
    {
        public long Number { get; }
        public string Text { get; }
        public Brush Foreground { get; private set; } = Brushes.White;
        public Brush Background { get; private set; } = Brushes.Black;
        public TextWrapping Wrapping { get; private set; }

        public LogRow(long number, string text, AppSettings settings)
        {
            Number = number;
            Text = text;
            Restyle(settings);
        }

        public void Restyle(AppSettings settings)
        {
            var rule = Highlighter.Match(Text, settings.Rules);
            Foreground = Ui.Brush(rule?.Foreground ?? "#D7E0EA");
            Background = Ui.Brush(rule?.Background ?? "#0B111A");
            Wrapping = settings.Wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        }
    }
}
