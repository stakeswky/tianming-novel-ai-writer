using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Framework.AI.SemanticKernel.Conversation.Tools;

/// <summary>
/// 搜索项目可引用数据。遍历 Design 和 Generate 模块的 JSON 文件，
/// 返回与搜索关键词匹配的项目列表（标题/名称级）。
/// </summary>
public sealed class SearchReferencesTool : IConversationTool
{
    private readonly string _projectRoot;

    public SearchReferencesTool(string projectRoot)
    {
        _projectRoot = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));
    }

    public string Name => "search_references";

    public string Description =>
        "在项目中搜索可引用的条目（角色、地点、势力、章节等）。参数：query（必填，搜索关键词），scope（可选，限定搜索范围：all/design/generate）。";

    public string ParameterSchemaJson =>
        """
        {
          "type": "object",
          "properties": {
            "query": {
              "type": "string",
              "description": "搜索关键词"
            },
            "scope": {
              "type": "string",
              "description": "搜索范围：all, design, generate。默认为 all",
              "default": "all"
            }
          },
          "required": ["query"]
        }
        """;

    public async Task<string> InvokeAsync(IReadOnlyDictionary<string, object?> args, CancellationToken ct)
    {
        if (!args.TryGetValue("query", out var queryObj) || queryObj is not string query || string.IsNullOrWhiteSpace(query))
            return "错误：缺少必填参数 query。";

        var scope = args.TryGetValue("scope", out var scopeObj) && scopeObj is string s ? s : "all";

        var results = new List<string>();

        if (scope is "all" or "design")
            results.AddRange(await SearchDirectoryAsync("Design", query, ct));

        if (scope is "all" or "generate")
            results.AddRange(await SearchDirectoryAsync("Generate", query, ct));

        if (results.Count == 0)
            return $"在项目中未找到包含 '{query}' 的条目。";

        var header = $"搜索 '{query}'，共找到 {results.Count} 条结果：\n";
        return header + string.Join("\n", results.Take(30));
    }

    private async Task<List<string>> SearchDirectoryAsync(string subDir, string query, CancellationToken ct)
    {
        var dir = Path.Combine(_projectRoot, subDir);
        if (!Directory.Exists(dir))
            return new List<string>();

        var results = new List<string>();
        var files = Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var json = await File.ReadAllTextAsync(file, ct);
                if (!json.Contains(query, StringComparison.OrdinalIgnoreCase))
                    continue;

                var relativePath = Path.GetRelativePath(_projectRoot, file);
                var title = ExtractTitleFromJson(json);
                results.Add($"[{subDir}] {title} ({relativePath})");
            }
            catch (IOException)
            {
                // skip unreadable files
            }
        }

        return results;
    }

    /// <summary>尝试从 JSON 中提取 Name 或 Title 字段作为标题。</summary>
    private static string ExtractTitleFromJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("Name", out var name) && name.ValueKind == JsonValueKind.String)
                return name.GetString() ?? json.Substring(0, Math.Min(60, json.Length));
            if (root.TryGetProperty("Title", out var title) && title.ValueKind == JsonValueKind.String)
                return title.GetString() ?? json.Substring(0, Math.Min(60, json.Length));
        }
        catch (JsonException)
        {
            // ignore
        }

        return json.Substring(0, Math.Min(60, json.Length));
    }
}
