namespace TM.Services.Framework.AI.SemanticKernel.Conversation;

/// <summary>
/// 对话内可调用的工具接口。Agent 模式下由 Orchestrator 自动调度。
/// </summary>
public interface IConversationTool
{
    /// <summary>工具名称（英文 snake_case 或 camelCase，与 function calling 的 name 对应）。</summary>
    string Name { get; }

    /// <summary>工具描述，用于 OpenAI tools 声明中的 description。</summary>
    string Description { get; }

    /// <summary>JSON Schema 格式的参数字符串，用于 OpenAI tools 声明中的 parameters。</summary>
    string ParameterSchemaJson { get; }

    /// <summary>执行工具调用，返回结果文本。</summary>
    /// <param name="args">从 OpenAI tool_call arguments 解析出的字典。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>工具执行结果文本。</returns>
    Task<string> InvokeAsync(IReadOnlyDictionary<string, object?> args, CancellationToken ct);
}
