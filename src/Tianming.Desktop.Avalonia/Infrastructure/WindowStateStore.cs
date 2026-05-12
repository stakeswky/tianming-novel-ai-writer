using System;
using System.IO;
using System.Text.Json;

namespace Tianming.Desktop.Avalonia.Infrastructure;

public sealed record WindowState(
    double X = double.NaN,
    double Y = double.NaN,
    double Width = 1200,
    double Height = 800,
    double LeftColumnWidth = 240,
    double RightColumnWidth = 360,
    bool IsMaximized = false);

public sealed class WindowStateStore
{
    private readonly string _filePath;

    public WindowStateStore(string filePath) { _filePath = filePath; }

    public WindowState Load()
    {
        if (!File.Exists(_filePath)) return new WindowState();
        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<WindowState>(json) ?? new WindowState();
        }
        catch (JsonException) { return new WindowState(); }
    }

    public void Save(WindowState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath) ?? ".");
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
}
