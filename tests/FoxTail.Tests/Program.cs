using System.Text;
using FoxTail.Core;

var folder = Path.Combine(Path.GetTempPath(), "foxtail-tests-" + Guid.NewGuid());
Directory.CreateDirectory(folder);
var passed = 0;
void Test(string name, Action test)
{
    test(); passed++; Console.WriteLine("PASS " + name);
}
void Assert(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
string FileFor(string name, string content) { var path = Path.Combine(folder, name); File.WriteAllText(path, content, new UTF8Encoding(false)); return path; }
try
{
    Test("Initial read, CRLF, and unfinished line", () =>
    {
        var reader = new TailReader(FileFor("initial", "one\r\ntwo\npartial"));
        var b = reader.Poll(); Assert(b.Reset && b.Lines.SequenceEqual(new[] { "one", "two" }) && b.Partial == "partial");
    });
    Test("Appended text completes the existing partial line", () =>
    {
        var path = FileFor("append", "part"); var reader = new TailReader(path); reader.Poll();
        File.AppendAllText(path, "ial\nnext\n"); var b = reader.Poll();
        Assert(!b.Reset && b.Lines.SequenceEqual(new[] { "partial", "next" }) && b.Partial == "");
        Assert(reader.Poll().Lines.Count == 0);
    });
    Test("UTF-8 split between polls preserves multibyte characters", () =>
    {
        var path = FileFor("utf8", ""); var reader = new TailReader(path); reader.Poll();
        var bytes = Encoding.UTF8.GetBytes("🦊\n");
        using (var file = new FileStream(path, FileMode.Append)) file.Write(bytes, 0, 2);
        Assert(reader.Poll().Partial == "");
        using (var file = new FileStream(path, FileMode.Append)) file.Write(bytes, 2, bytes.Length - 2);
        Assert(reader.Poll().Lines.Single() == "🦊");
    });
    Test("Truncation resets state", () =>
    {
        var path = FileFor("truncate", "long original line\n"); var reader = new TailReader(path); reader.Poll();
        File.WriteAllText(path, "new\n"); var b = reader.Poll(); Assert(b.Reset && b.Lines.Single() == "new");
    });
    Test("Same-size replacement with a changed prefix resets state", () =>
    {
        var path = FileFor("replace", "old\n"); var reader = new TailReader(path); reader.Poll();
        File.WriteAllText(path, "new\n"); var b = reader.Poll(); Assert(b.Reset && b.Lines.Single() == "new");
    });
    Test("Renamed rotation reads the new file from its beginning", () =>
    {
        var path = FileFor("rotate", "old\n"); var reader = new TailReader(path); reader.Poll();
        File.Move(path, path + ".1"); File.WriteAllText(path, "replacement\n");
        var b = reader.Poll(); Assert(b.Reset && b.Lines.Single() == "replacement");
    });
    Test("Missing files can recover without replacing the reader", () =>
    {
        var path = Path.Combine(folder, "missing"); var reader = new TailReader(path);
        try { reader.Poll(); throw new Exception("Expected missing file"); } catch (FileNotFoundException) { }
        File.WriteAllText(path, "ready\n"); Assert(reader.Poll().Lines.Single() == "ready");
    });
    Test("Large-file initial I/O is bounded and skips the cut line", () =>
    {
        var path = FileFor("large", string.Concat(Enumerable.Repeat("0123456789abcdef\n", 200000)));
        var length = new FileInfo(path).Length; var reader = new TailReader(path); var b = reader.Poll();
        Assert(b.Position <= length - TailReader.InitialBytes + TailReader.MaxReadBytes && b.MoreAvailable);
        Assert(b.Lines.All(line => line == "0123456789abcdef"));
        while (b.MoreAvailable) b = reader.Poll(); Assert(b.Position == length);
    });
    Test("Very long lines are bounded and explicitly marked", () =>
    {
        var reader = new TailReader(FileFor("long", new string('x', TailReader.MaxLineChars * 2) + "\n"));
        var line = reader.Poll().Lines.Single(); Assert(line.Length < TailReader.MaxLineChars + 100 && line.EndsWith("[line truncated]"));
    });
    Test("First enabled matching rule wins both colors", () =>
    {
        var first = new HighlightRule { Pattern = "error", Foreground = "#FF0000", Background = "#000000" };
        var second = new HighlightRule { Pattern = "timeout" };
        Assert(Highlighter.Match("ERROR timeout", [first, second]) == first);
        Assert(Highlighter.Match("ERROR timeout", [second, first]) == second);
        first.Enabled = false; Assert(Highlighter.Match("ERROR timeout", [first, second]) == second);
    });
    Test("Case-sensitive, literal, and regex matching", () =>
    {
        var rule = new HighlightRule { Pattern = "error", MatchCase = true };
        Assert(!rule.Matches("ERROR")); rule.Pattern = "[42]"; Assert(rule.Matches("value [42]"));
        rule.IsRegex = true; rule.Pattern = @"\bE\d{3}\b"; Assert(rule.Matches("E401")); Assert(!rule.Matches("E40"));
    });
    Test("Invalid patterns and colors are rejected", () =>
    {
        var rule = new HighlightRule { Pattern = "[", IsRegex = true }; Assert(rule.Validate() is not null && !rule.Matches("x"));
        rule.IsRegex = false; rule.Foreground = "red"; Assert(rule.Validate() is not null);
    });
    Test("Settings round trip preserves precedence and display choices", () =>
    {
        var path = Path.Combine(folder, "settings.json"); var settings = new AppSettings { FontSize = 17, Wrap = true };
        settings.Rules.Reverse(); settings.Save(path); var loaded = AppSettings.Load(path);
        Assert(loaded.FontSize == 17 && loaded.Wrap && loaded.Rules[0].Name == "Success");
    });
    Console.WriteLine($"\n{passed} checks passed.");
}
finally { Directory.Delete(folder, true); }
