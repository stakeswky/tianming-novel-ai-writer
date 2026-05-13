using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Framework.AI.SemanticKernel.Conversation.Tools;

/// <summary>
/// 查询项目设计数据工具。
/// 从项目目录下的 Design/Elements/* 读取 JSON 文件，返回角色/世界观/势力/地点/剧情等数据。
/// </summary>
public sealed class LookupDataTool : IConversationTool
{
    private readonly string _projectRoot;

    public LookupDataTool(string projectRoot)
    {
        _projectRoot = projectRoot ?? throw new ArgumentNullException(nameof(projectRoot));
    }

    public string Name => "lookup_data";

    public string Description =>
        "查询项目设计数据（角色、世界观、势力、地点、剧情等）。参数：category（必填，如 Characters/WorldRules/Factions/Locations/Plot），query（可选，模糊搜索关键词）。";

    public string ParameterSchemaJson =>
        """
        {
          "type": "object",
          "properties": {
            "category": {
              "type": "string",
              "description": "数据类别：Characters, WorldRules, Factions, Locations, Plot, CreativeMaterials"
            },
            "query": {
              "type": "string",
              "description": "可选的搜索关键词，用于模糊匹配"
            }
          },
          "required": ["category"]
        }
        """;

    public async Task<string> InvokeAsync(IReadOnlyDictionary<string, object?> args, CancellationToken ct)
    {
        if (!args.TryGetValue("category", out var categoryObj) || categoryObj is not string category)
            return "错误：缺少必填参数 category。可选值：Characters, WorldRules, Factions, Locations, Plot, CreativeMaterials。";

        var subPath = category switch
        {
            "Characters" or "characters" => "Design/Elements/Characters",
            "WorldRules" or "world" or "worldrules" => "Design/GlobalSettings/WorldRules",
            "Factions" or "factions" => "Design/Elements/Factions",
            "Locations" or "locations" => "Design/Elements/Locations",
            "Plot" or "plot" => "Design/Elements/Plot",
            "CreativeMaterials" or "materials" => "Design/Templates/CreativeMaterials",
            _ => null
        };

        if (subPath == null)
            return $"错误：未知的 category '{category}'。可选值：Characters, WorldRules, Factions, Locations, Plot, CreativeMaterials。";

        var dir = Path.Combine(_projectRoot, subPath);
        if (!Directory.Exists(dir))
            return $"提示：目录不存在（{subPath}），项目可能尚未创建该类别的数据。";

        var files = Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories);
        if (files.Length == 0)
            return $"提示：{subPath} 目录下没有 JSON 文件。";

        var query = args.TryGetValue("query", out var q) ? q as string : null;
        var results = new List<string>();

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var json = await File.ReadAllTextAsync(file, ct);
                if (!string.IsNullOrEmpty(query) && !json.Contains(query, StringComparison.OrdinalIgnoreCase))
                    continue;

                var relativePath = Path.GetRelativePath(_projectRoot, file);
                results.Add($"--- {relativePath} ---\n{Truncate(json, 2000)}");
            }
            catch (IOException)
            {
                // skip unreadable files
            }
        }

        if (results.Count == 0)
            return !string.IsNullOrEmpty(query)
                ? $"在 {subPath} 中未找到包含 '{query}' 的数据。"
                : $"{subPath} 目录下没有可读的数据。";

        return string.Join("\n\n", results);
    }

    private static string Truncate(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return text[..maxLength] + "\n...（已截断）";
    }
}
