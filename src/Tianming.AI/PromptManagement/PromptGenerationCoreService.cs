using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TM.Framework.Common.Helpers.AI;

public sealed record PromptGenerationAiResult(bool Success, string Content, string? ErrorMessage = null);

public interface IPromptTextGenerator
{
    Task<PromptGenerationAiResult> GenerateAsync(string prompt);
}

public sealed class PromptGenerationCoreService : IPromptGenerationService
{
    private readonly IPromptTextGenerator _generator;

    public PromptGenerationCoreService(IPromptTextGenerator generator)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
    }

    public async Task<PromptGenerationResult> GenerateModulePromptAsync(PromptGenerationContext context)
    {
        var result = new PromptGenerationResult();

        if (context == null)
        {
            result.Success = false;
            result.ErrorMessage = "缺少模块标识或名称";
            return result;
        }

        if (string.IsNullOrWhiteSpace(context.ModuleKey) && string.IsNullOrWhiteSpace(context.ModuleDisplayName))
        {
            result.Success = false;
            result.ErrorMessage = "缺少模块标识或名称";
            return result;
        }

        try
        {
            var metaPrompt = BuildMetaPrompt(context);
            var aiResult = await _generator.GenerateAsync(metaPrompt);
            if (!aiResult.Success || string.IsNullOrWhiteSpace(aiResult.Content))
            {
                result.Success = false;
                result.ErrorMessage = string.IsNullOrWhiteSpace(aiResult.ErrorMessage)
                    ? "AI未返回有效内容"
                    : aiResult.ErrorMessage;
                return result;
            }

            var raw = aiResult.Content.Trim();
            if (TryParseJsonResult(raw, context, result))
                return result;

            result.Success = true;
            result.Content = raw;
            result.Description = context.ExtraRequirement;
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    public static string BuildMetaPrompt(PromptGenerationContext context)
    {
        return (context.PromptRootCategory ?? string.Empty) switch
        {
            "Spec" => BuildSpecMetaPrompt(context),
            "校验" => BuildValidateMetaPrompt(context),
            "AIGC" => BuildAigcMetaPrompt(context),
            "统一提示词" => BuildUnifiedMetaPrompt(context),
            _ => BuildBusinessMetaPrompt(context)
        };
    }

    private static bool TryParseJsonResult(string raw, PromptGenerationContext context, PromptGenerationResult result)
    {
        if (!TryExtractJsonObject(raw, out var jsonText))
            return false;

        try
        {
            using var document = JsonDocument.Parse(jsonText);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            if (root.TryGetProperty("content", out var contentProp) && contentProp.ValueKind == JsonValueKind.String)
                result.Content = contentProp.GetString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(result.Content))
                return false;

            if (root.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
                result.Name = nameProp.GetString();

            if (root.TryGetProperty("description", out var descProp) && descProp.ValueKind == JsonValueKind.String)
                result.Description = descProp.GetString();

            if (root.TryGetProperty("tags", out var tagsProp))
                result.Tags = ParseTags(tagsProp);

            if (string.IsNullOrWhiteSpace(result.Description))
                result.Description = context.ExtraRequirement;

            result.Success = true;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static IReadOnlyList<string> ParseTags(JsonElement tagsProp)
    {
        var tags = new List<string>();
        if (tagsProp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in tagsProp.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                    continue;
                var text = item.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                    tags.Add(text);
            }
        }
        else if (tagsProp.ValueKind == JsonValueKind.String)
        {
            foreach (var part in (tagsProp.GetString() ?? string.Empty).Split(new[] { ',', '，' }))
            {
                var text = part.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                    tags.Add(text);
            }
        }

        return tags;
    }

    private static bool TryExtractJsonObject(string text, out string jsonText)
    {
        jsonText = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
            return false;

        jsonText = text.Substring(start, end - start + 1);
        return true;
    }

    private static string BuildBusinessMetaPrompt(PromptGenerationContext context)
    {
        var sb = new StringBuilder();
        var moduleLabel = context.ModuleType?.ToLowerInvariant() switch
        {
            "design" => "design module",
            "generate" => "generate module",
            "validate" => "validate module",
            _ => "novel writing software"
        };

        sb.AppendLine($"<role>Expert prompt engineer specializing in novel writing software {moduleLabel}. Task: generate a high-quality System Prompt.</role>");
        AppendTaskSection(sb, context);
        AppendInputVariablesSection(sb, context);
        AppendFieldAlignmentSection(sb, context);
        AppendOutputFormat(sb);
        return sb.ToString();
    }

    private static string BuildSpecMetaPrompt(PromptGenerationContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<role>Expert prompt engineer specializing in novel writing software Spec (creative spec) module. Task: generate a high-quality Spec System Prompt.</role>");
        AppendTaskSection(sb, context);
        sb.AppendLine();
        sb.AppendLine("<spec_core_purpose>");
        sb.AppendLine("Spec 模板是整个创作流水线的「0层强约束」，所有下游模块都会以 Spec 内容作为最高优先级约束。");
        sb.AppendLine("</spec_core_purpose>");
        AppendOutputFormat(sb);
        return sb.ToString();
    }

    private static string BuildValidateMetaPrompt(PromptGenerationContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<role>Expert prompt engineer specializing in novel writing software validation modules. Task: generate a System Prompt for the validation module.</role>");
        AppendTaskSection(sb, context);
        sb.AppendLine();
        sb.AppendLine("<placeholder_rules>不需要任何占位符。上下文和规则清单由系统运行时单独注入。</placeholder_rules>");
        AppendOutputFormat(sb);
        return sb.ToString();
    }

    private static string BuildAigcMetaPrompt(PromptGenerationContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<role>Expert prompt engineer specializing in novel writing software AIGC inline editing modules. Task: generate a complete System Prompt for the AIGC module.</role>");
        AppendTaskSection(sb, context);
        sb.AppendLine();
        sb.AppendLine("<strict_constraints>");
        sb.AppendLine("- 不需要结构化输出格式（不需要 JSON），AI 运行时直接返回修改后的文本。");
        sb.AppendLine("- 不需要占位符，用户选中的文本会作为用户消息直接发送。");
        sb.AppendLine("</strict_constraints>");
        AppendOutputFormat(sb);
        return sb.ToString();
    }

    private static string BuildUnifiedMetaPrompt(PromptGenerationContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<role>Expert prompt engineer. Task: generate a System Prompt for unified template use in business runtime.</role>");
        AppendTaskSection(sb, context);
        AppendInputVariablesSection(sb, context);
        AppendOutputFormat(sb);
        return sb.ToString();
    }

    private static void AppendTaskSection(StringBuilder sb, PromptGenerationContext context)
    {
        sb.AppendLine();
        sb.AppendLine("<task>");
        sb.AppendLine($"为「{context.ModuleDisplayName}」功能编写 System Prompt。");
        if (!string.IsNullOrWhiteSpace(context.Description))
            sb.AppendLine($"功能说明：{context.Description}");
        if (!string.IsNullOrWhiteSpace(context.ExtraRequirement))
            sb.AppendLine($"额外需求：{context.ExtraRequirement}");
        sb.AppendLine("</task>");
    }

    private static void AppendInputVariablesSection(StringBuilder sb, PromptGenerationContext context)
    {
        sb.AppendLine();
        sb.AppendLine("<input_variables>");
        sb.AppendLine("Prompt 中可以包含以下占位符，运行时会被替换：");
        if (context.InputVariableNames != null)
        {
            foreach (var variable in context.InputVariableNames)
            {
                if (!string.IsNullOrWhiteSpace(variable))
                    sb.AppendLine($"- {{{variable}}}：用户输入");
            }
        }
        sb.AppendLine("注意：不得使用 {上下文数据} 占位符（上下文由系统运行时单独注入，不需要占位符）。");
        sb.AppendLine("</input_variables>");
    }

    private static void AppendFieldAlignmentSection(StringBuilder sb, PromptGenerationContext context)
    {
        sb.AppendLine();
        sb.AppendLine("<field_alignment_rules mandatory=\"true\">");
        if (context.OutputFieldNames != null && context.OutputFieldNames.Count > 0)
        {
            sb.AppendLine("生成的业务 System Prompt（content）必须严格对齐本次提供的字段清单：");
            foreach (var field in context.OutputFieldNames)
            {
                if (!string.IsNullOrWhiteSpace(field))
                    sb.AppendLine($"- {field}");
            }
            sb.AppendLine("System Prompt（content）中不得包含字段清单原文，也不得规定任何输出格式。");
        }
        else
        {
            sb.AppendLine("本次未提供固定输出字段清单（该分类为跨模块模板）。");
        }
        sb.AppendLine("</field_alignment_rules>");
    }

    private static void AppendOutputFormat(StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine("<output_format type=\"json\">");
        sb.AppendLine("只输出纯 JSON（不要用```代码块包裹）。");
        sb.AppendLine("{");
        sb.AppendLine("  \"content\": \"完整的 System Prompt（必填，换行用\\\\n）\",");
        sb.AppendLine("  \"name\": \"模板名称\",");
        sb.AppendLine("  \"description\": \"一句话描述\",");
        sb.AppendLine("  \"tags\": [\"标签1\", \"标签2\"]");
        sb.AppendLine("}");
        sb.AppendLine("</output_format>");
    }
}
