using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.StagedChanges;

namespace TM.Services.Framework.AI.SemanticKernel.Conversation.Tools.Write;

public sealed class WorkspaceEditTool : IConversationTool
{
    private readonly IStagedChangeStore _store;

    public WorkspaceEditTool(IStagedChangeStore store)
    {
        _store = store;
    }

    public string Name => "workspace_edit";

    public string Description =>
        "提议工作区文件修改。参数：relativePath（相对路径），newContent（完整新内容），reason（修改理由）。提议会进入待审核队列，需用户批准。";

    public string ParameterSchemaJson =>
        """
        {
          "type": "object",
          "properties": {
            "relativePath": {"type": "string"},
            "newContent": {"type": "string"},
            "reason": {"type": "string"}
          },
          "required": ["relativePath", "newContent", "reason"]
        }
        """;

    public async Task<string> InvokeAsync(IReadOnlyDictionary<string, object?> args, CancellationToken ct)
    {
        if (!args.TryGetValue("relativePath", out var relativePathObj) || relativePathObj is not string relativePath)
        {
            return "错误：缺少 relativePath 参数。";
        }

        if (!args.TryGetValue("newContent", out var newContentObj) || newContentObj is not string newContent)
        {
            return "错误：缺少 newContent 参数。";
        }

        var reason = args.TryGetValue("reason", out var reasonObj) ? reasonObj as string : "(no reason)";
        var change = new StagedChange
        {
            ChangeType = StagedChangeType.WorkspaceEdit,
            TargetId = relativePath,
            NewContentSnippet = newContent,
            Reason = reason ?? string.Empty,
        };

        var id = await _store.StageAsync(change, ct).ConfigureAwait(false);
        return $"已提议修改文件 {relativePath}（待审核：{id}）。请用户在 ToolCallCard 上批准或拒绝。";
    }
}
