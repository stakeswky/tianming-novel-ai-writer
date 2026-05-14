using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.StagedChanges;

namespace TM.Services.Framework.AI.SemanticKernel.Conversation.Tools.Write;

public sealed class ContentEditTool : IConversationTool
{
    private readonly IStagedChangeStore _store;

    public ContentEditTool(IStagedChangeStore store)
    {
        _store = store;
    }

    public string Name => "content_edit";

    public string Description =>
        "提议章节正文修改。参数：chapterId（章节 ID），newContent（替换全文）或 patchSnippet（局部替换片段），reason（修改理由）。提议会进入待审核队列，需用户批准。";

    public string ParameterSchemaJson =>
        """
        {
          "type": "object",
          "properties": {
            "chapterId": {"type": "string"},
            "newContent": {"type": "string"},
            "patchSnippet": {"type": "string"},
            "oldSnippet": {"type": "string"},
            "reason": {"type": "string"}
          },
          "required": ["chapterId", "reason"]
        }
        """;

    public async Task<string> InvokeAsync(IReadOnlyDictionary<string, object?> args, CancellationToken ct)
        => (await InvokeStructuredAsync(args, ct).ConfigureAwait(false)).ResultText;

    public async Task<ConversationToolResult> InvokeStructuredAsync(IReadOnlyDictionary<string, object?> args, CancellationToken ct)
    {
        if (!args.TryGetValue("chapterId", out var chapterIdObj) || chapterIdObj is not string chapterId)
        {
            return new ConversationToolResult("错误：缺少 chapterId 参数。");
        }

        var newContent = args.TryGetValue("newContent", out var newContentObj) ? newContentObj as string : null;
        var patchSnippet = args.TryGetValue("patchSnippet", out var patchSnippetObj) ? patchSnippetObj as string : null;
        var oldSnippet = args.TryGetValue("oldSnippet", out var oldSnippetObj) ? oldSnippetObj as string : null;
        var reason = args.TryGetValue("reason", out var reasonObj) ? reasonObj as string : "(no reason)";

        var change = new StagedChange
        {
            ChangeType = StagedChangeType.ContentEdit,
            TargetId = chapterId,
            OldContentSnippet = oldSnippet ?? string.Empty,
            NewContentSnippet = patchSnippet ?? newContent ?? string.Empty,
            Reason = reason ?? string.Empty,
        };

        var id = await _store.StageAsync(change, ct).ConfigureAwait(false);
        return new ConversationToolResult(
            $"已提议修改章节 {chapterId}（待审核：{id}）。请用户在 ToolCallCard 上批准或拒绝。",
            id);
    }
}
