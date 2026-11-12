using System.Text.Json;

namespace FoxTail.Core;

public sealed class AppSettings
{
    public double FontSize { get; set; } = 13;
    public bool Wrap { get; set; }
    public int RetainedLines { get; set; } = 20000;
    public List<HighlightRule> Rules { get; set; } =
    [
        new() { Name = "Errors", Pattern = "error", Foreground = "#FECACA", Background = "#451D28" },
        new() { Name = "Warnings", Pattern = "warn", Foreground = "#FDE68A", Background = "#40351D" },
        new() { Name = "Success", Pattern = "success", Foreground = "#A7F3D0", Background = "#163C35" }
    ];

    public static AppSettings Load(string path)
    {
        if (!File.Exists(path)) return new();
        var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new();
        settings.FontSize = Math.Clamp(settings.FontSize, 10, 24);
        settings.RetainedLines = Math.Clamp(settings.RetainedLines, 1000, 100000);
        settings.Rules = (settings.Rules ?? []).Where(r => r is not null && r.Validate() is null).ToList();
        return settings;
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, true);
    }
}
