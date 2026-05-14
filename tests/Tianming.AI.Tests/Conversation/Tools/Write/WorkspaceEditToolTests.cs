using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Tools.Write;
using TM.Services.Modules.ProjectData.StagedChanges;
using Xunit;

namespace Tianming.AI.Tests.Conversation.Tools.Write;

public sealed class WorkspaceEditToolTests
{
    [Fact]
    public async Task Invoke_stages_workspace_file_change()
    {
        var root = CreateTempDir();
        var store = new FileStagedChangeStore(root);
        var tool = new WorkspaceEditTool(store);

        var result = await tool.InvokeAsync(new Dictionary<string, object?>
        {
            ["relativePath"] = "README.md",
            ["newContent"] = "# rewritten",
            ["reason"] = "refresh intro",
        }, default);

        Assert.Contains("待审核", result);

        var pending = await store.ListPendingAsync();
        var change = Assert.Single(pending);
        Assert.Equal(StagedChangeType.WorkspaceEdit, change.ChangeType);
        Assert.Equal("README.md", change.TargetId);
        Assert.Equal("# rewritten", change.NewContentSnippet);
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-wetool-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
