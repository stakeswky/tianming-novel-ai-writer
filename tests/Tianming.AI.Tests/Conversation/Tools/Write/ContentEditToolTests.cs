using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Tools.Write;
using TM.Services.Modules.ProjectData.StagedChanges;
using Xunit;

namespace Tianming.AI.Tests.Conversation.Tools.Write;

public sealed class ContentEditToolTests
{
    [Fact]
    public async Task Invoke_stages_change_with_chapter_id()
    {
        var root = CreateTempDir();
        var store = new FileStagedChangeStore(root);
        var tool = new ContentEditTool(store);

        var result = await tool.InvokeStructuredAsync(new Dictionary<string, object?>
        {
            ["chapterId"] = "ch-001",
            ["newContent"] = "new text",
            ["reason"] = "rewrite for clarity",
        }, default);

        Assert.Contains("ch-001", result.ResultText);
        Assert.Contains("待审核", result.ResultText);
        Assert.False(string.IsNullOrWhiteSpace(result.StagedId));

        var pending = await store.ListPendingAsync();
        var change = Assert.Single(pending);
        Assert.Equal(result.StagedId, change.Id);
        Assert.Equal(StagedChangeType.ContentEdit, change.ChangeType);
        Assert.Equal("ch-001", change.TargetId);
        Assert.Equal("new text", change.NewContentSnippet);
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-cetool-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
