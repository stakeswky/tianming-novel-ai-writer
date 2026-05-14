using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.StagedChanges;

namespace TM.Services.Framework.AI.SemanticKernel.Conversation.Tools.Write;

public sealed class DataEditTool : IConversationTool
{
    private readonly IStagedChangeStore _store;

    public DataEditTool(IStagedChangeStore store)
    {
        _store = store;
    }

    public string Name => "data_edit";

    public string Description =>
        "提议设计数据修改。参数：category（数据类别），dataId（条目 ID），dataJson（完整 JSON），reason（修改理由）。提议会进入待审核队列，需用户批准。";

    public string ParameterSchemaJson =>
        """
        {
          "type": "object",
          "properties": {
            "category": {"type": "string"},
            "dataId": {"type": "string"},
            "dataJson": {"type": "string"},
            "reason": {"type": "string"}
          },
          "required": ["category", "dataId", "dataJson", "reason"]
        }
        """;

    public async Task<string> InvokeAsync(IReadOnlyDictionary<string, object?> args, CancellationToken ct)
        => (await InvokeStructuredAsync(args, ct).ConfigureAwait(false)).ResultText;

    public async Task<ConversationToolResult> InvokeStructuredAsync(IReadOnlyDictionary<string, object?> args, CancellationToken ct)
    {
        if (!args.TryGetValue("category", out var categoryObj) || categoryObj is not string category)
        {
            return new ConversationToolResult("错误：缺少 category 参数。");
        }

        if (!args.TryGetValue("dataId", out var dataIdObj) || dataIdObj is not string dataId)
        {
            return new ConversationToolResult("错误：缺少 dataId 参数。");
        }

        if (!args.TryGetValue("dataJson", out var dataJsonObj) || dataJsonObj is not string dataJson)
        {
            return new ConversationToolResult("错误：缺少 dataJson 参数。");
        }

        var reason = args.TryGetValue("reason", out var reasonObj) ? reasonObj as string : "(no reason)";
        var change = new StagedChange
        {
            ChangeType = StagedChangeType.DataEdit,
            TargetId = $"{category}:{dataId}",
            PayloadJson = dataJson,
            Reason = reason ?? string.Empty,
            NewContentSnippet = dataJson,
        };

        var id = await _store.StageAsync(change, ct).ConfigureAwait(false);
        return new ConversationToolResult(
            $"已提议修改 {category}/{dataId}（待审核：{id}）。请用户在 ToolCallCard 上批准或拒绝。",
            id);
    }
}
