using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Framework.AI.SemanticKernel.Conversation.Tools;

/// <summary>
/// 读取指定章节内容。从项目目录下的 Generate/Chapters/ 读取 .md 文件。
/// </summary>
public sealed class ReadChapterTool : IConversationTool
{
    private readonly string _projectRoot;

    public ReadChapterTool(string projectRoot)
    {
        _projectRoot = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));
    }

    public string Name => "read_chapter";

    public string Description =>
        "读取指定章节的内容。参数：chapterId（必填，章节 ID，如 ch0032），或 chapterNumber（可选，章节号，如 32）。";

    public string ParameterSchemaJson =>
        """
        {
          "type": "object",
          "properties": {
            "chapterId": {
              "type": "string",
              "description": "章节 ID（如 ch0032）"
            },
            "chapterNumber": {
              "type": "integer",
              "description": "章节号（如 32）"
            }
          }
        }
        """;

    public async Task<string> InvokeAsync(IReadOnlyDictionary<string, object?> args, CancellationToken ct)
    {
        var chaptersDir = Path.Combine(_projectRoot, "Generate", "Chapters");
        if (!Directory.Exists(chaptersDir))
            return "提示：章节目录不存在（Generate/Chapters），项目可能尚未生成章节内容。";

        string? targetFile = null;

        if (args.TryGetValue("chapterId", out var chapterIdObj) && chapterIdObj is string chapterId)
        {
            targetFile = FindChapterById(chaptersDir, chapterId);
        }
        else if (args.TryGetValue("chapterNumber", out var chapterNumObj) && TryGetIntFromObject(chapterNumObj, out var chapterNumber))
        {
            targetFile = FindChapterByNumber(chaptersDir, chapterNumber);
        }

        if (targetFile == null)
        {
            var available = ListAvailableChapters(chaptersDir);
            return $"未找到指定章节。可用章节：{available}";
        }

        try
        {
            var content = await File.ReadAllTextAsync(targetFile, ct);
            var relativePath = Path.GetRelativePath(_projectRoot, targetFile);
            return $"--- {relativePath} ---\n{content}";
        }
        catch (IOException ex)
        {
            return $"读取章节文件失败：{ex.Message}";
        }
    }

    private static string? FindChapterById(string dir, string chapterId)
    {
        var pattern = $"*{chapterId}*";
        var matches = Directory.GetFiles(dir, pattern, SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return matches.Length > 0 ? matches[0] : null;
    }

    private static string? FindChapterByNumber(string dir, int chapterNumber)
    {
        var padded = chapterNumber.ToString("D4");
        var matches = Directory.GetFiles(dir, $"*{padded}*.md", SearchOption.AllDirectories)
            .ToArray();
        if (matches.Length > 0) return matches[0];

        // fallback: try unpadded
        matches = Directory.GetFiles(dir, $"*{chapterNumber}*.md", SearchOption.AllDirectories)
            .ToArray();
        return matches.Length > 0 ? matches[0] : null;
    }

    private static string ListAvailableChapters(string dir)
    {
        var files = Directory.GetFiles(dir, "*.md", SearchOption.AllDirectories)
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Take(20)
            .ToArray();
        return files.Length == 0 ? "（无）" : string.Join(", ", files);
    }

    private static bool TryGetIntFromObject(object? value, out int result)
    {
        result = 0;
        if (value is int i) { result = i; return true; }
        if (value is long l) { result = (int)l; return true; }
        if (value is JsonElement je && je.TryGetInt32(out var jeVal)) { result = jeVal; return true; }
        if (value is string s && int.TryParse(s, out var sVal)) { result = sVal; return true; }
        return false;
    }
}
