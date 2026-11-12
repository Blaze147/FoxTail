using System.Text.RegularExpressions;

namespace FoxTail.Core;

public sealed class HighlightRule
{
    public string Name { get; set; } = "New rule";
    public string Pattern { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool IsRegex { get; set; }
    public bool MatchCase { get; set; }
    public string Foreground { get; set; } = "#FFFFFF";
    public string Background { get; set; } = "#7F1D1D";

    public string? Validate()
    {
        if (string.IsNullOrEmpty(Pattern)) return "Enter a pattern.";
        if (!Regex.IsMatch(Foreground, "^#[0-9a-fA-F]{6}$") || !Regex.IsMatch(Background, "^#[0-9a-fA-F]{6}$"))
            return "Colors must use #RRGGBB format.";
        if (IsRegex)
            try { _ = new Regex(Pattern, Options, TimeSpan.FromMilliseconds(25)); }
            catch (ArgumentException ex) { return ex.Message; }
        return null;
    }

    private RegexOptions Options => RegexOptions.CultureInvariant | (MatchCase ? RegexOptions.None : RegexOptions.IgnoreCase);
    public bool Matches(string text)
    {
        if (!Enabled || Pattern.Length == 0) return false;
        if (!IsRegex) return text.Contains(Pattern, MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
        try { return Regex.IsMatch(text, Pattern, Options, TimeSpan.FromMilliseconds(25)); }
        catch (ArgumentException) { return false; }
        catch (RegexMatchTimeoutException) { return false; }
    }
}

public static class Highlighter
{
    public static HighlightRule? Match(string text, IEnumerable<HighlightRule> rules) => rules.FirstOrDefault(rule => rule.Matches(text));
}
