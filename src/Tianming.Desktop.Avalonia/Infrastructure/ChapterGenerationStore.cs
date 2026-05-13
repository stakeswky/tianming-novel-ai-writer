using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace Tianming.Desktop.Avalonia.Infrastructure;

/// <summary>
/// M4.4：追踪哪些章节已生成。存于 &lt;project&gt;/Generate/.generated_chapters.json。
/// </summary>
public sealed class ChapterGenerationStore
{
    private readonly string _filePath;
    private readonly object _lock = new();
    private HashSet<string> _generatedIds = new();

    public ChapterGenerationStore(string projectRoot)
    {
        var dir = Path.Combine(projectRoot, "Generate");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, ".generated_chapters.json");
        Load();
    }

    public bool IsGenerated(string chapterId)
    {
        lock (_lock) return _generatedIds.Contains(chapterId);
    }

    public void MarkGenerated(string chapterId)
    {
        lock (_lock)
        {
            _generatedIds.Add(chapterId);
            Save();
        }
    }

    public IReadOnlySet<string> ListGenerated()
    {
        lock (_lock) return new HashSet<string>(_generatedIds);
    }

    private void Load()
    {
        if (File.Exists(_filePath))
        {
            var json = File.ReadAllText(_filePath);
            var list = JsonSerializer.Deserialize<List<string>>(json);
            if (list != null) _generatedIds = new HashSet<string>(list);
        }
    }

    private void Save()
    {
        var tmp = _filePath + ".tmp";
        var json = JsonSerializer.Serialize(_generatedIds);
        File.WriteAllText(tmp, json);
        File.Move(tmp, _filePath, overwrite: true);
    }
}
