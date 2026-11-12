using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FoxTail.Core;
using FoxTail.Desktop;

namespace FoxTail.UiTests;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        application.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/FoxTail.Desktop;component/Theme.xaml", UriKind.Relative)
        });
        var owner = new Window(); owner.Show();
        var original = new AppSettings().Rules;
        var editor = new RulesWindow(original) { Owner = owner };
        RunDialog(editor, () =>
        {
            var fields = Descendants(editor).OfType<TextBox>().ToArray();
            Assert(fields.Length == 5, "Editor fields must be available");
            fields[3].Text = "#1E3A5F";
            Click(editor, "↓");
            var list = Descendants(editor).OfType<ListBox>().Single();
            Assert(((HighlightRule)list.Items[0]).Name == "Warnings", "Moving down changes priority");
            Assert(((HighlightRule)list.SelectedItem).Background == "#1E3A5F", "Moving preserves colors");
            Click(editor, "↑");
            Assert(((HighlightRule)list.Items[0]).Name == "Errors", "Moving up restores priority");
            Click(editor, "Apply rules");
        });
        Assert(editor.Result![0].Background == "#1E3A5F", "Apply returns edited colors");
        Assert(original[0].Background == "#451D28", "Editor must not mutate original rules");
        Console.WriteLine("PASS Rule editing, move up/down, apply, isolated edits");

        editor = new RulesWindow(original) { Owner = owner };
        RunDialog(editor, () =>
        {
            Click(editor, "Remove"); Click(editor, "Remove"); Click(editor, "Remove");
            Click(editor, "Add"); Click(editor, "Apply rules");
        });
        Assert(editor.Result!.Count == 1, "Remove all and add must work");
        Console.WriteLine("PASS Remove all rules and add a new rule");

        editor = new RulesWindow(original) { Owner = owner };
        RunDialog(editor, () =>
        {
            var fields = Descendants(editor).OfType<TextBox>().ToArray();
            fields[2].Text = "invalid";
            Click(editor, "Apply rules");
            Assert(editor.IsVisible && editor.Result is null, "Invalid colors block apply");
            Click(editor, "Cancel");
        });
        Assert(editor.Result is null, "Cancel discards invalid edits");
        Console.WriteLine("PASS Validation blocks apply; cancel discards edits");

        var settings = new AppSettings();
        var view = new LogView("unused.log", settings);
        var logList = (ListBox)view.Content;
        view.Accept(new TailBatch(true, Enumerable.Range(1, 25000).Select(i => $"line {i}").ToArray(), "partial", 100, 100, false), false);
        Assert(logList.Items.Count == 20000, "UI retains at most configured line count");
        view.Accept(new TailBatch(false, ["partial completed"], "", 120, 120, false), false);
        Assert(logList.Items.Count == 20000, "Completing partial lines preserves retention");
        view.Accept(new TailBatch(true, ["ERROR rotated"], "", 8, 8, false), false);
        Assert(logList.Items.Count == 1, "Rotation clears old displayed rows");
        var row = (LogView.LogRow)logList.Items[0];
        Assert(row.Foreground.ToString() == "#FFFECACA" && row.Background.ToString() == "#FF451D28", "Winning rule supplies both colors");
        settings.Rules[0].Background = "#123456"; settings.Wrap = true; settings.FontSize = 18;
        view.Restyle();
        Assert(row.Background.ToString() == "#FF123456" && row.Wrapping == TextWrapping.Wrap && logList.FontSize == 18, "Display changes refresh existing rows");
        Console.WriteLine("PASS Retention, partial completion, rotation, row colors and display refresh");
        owner.Close(); application.Shutdown();
        Console.WriteLine("4 WPF UI checks passed.");
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void Click(Window window, string content) =>
        Descendants(window).OfType<Button>().First(b => Equals(b.Content, content)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static void RunDialog(Window window, Action check)
    {
        Exception? failure = null;
        window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            try { check(); }
            catch (Exception ex) { failure = ex; }
            finally { if (window.IsVisible) window.Close(); }
        }));
        window.ShowDialog();
        if (failure is not null) throw new Exception("WPF UI check failed", failure);
    }

    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
}
