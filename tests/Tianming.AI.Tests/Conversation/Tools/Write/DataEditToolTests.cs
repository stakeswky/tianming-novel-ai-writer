using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Tools.Write;
using TM.Services.Modules.ProjectData.StagedChanges;
using Xunit;

namespace Tianming.AI.Tests.Conversation.Tools.Write;

public sealed class DataEditToolTests
{
    [Fact]
    public async Task Invoke_stages_design_data_change()
    {
        var root = CreateTempDir();
        var store = new FileStagedChangeStore(root);
        var tool = new DataEditTool(store);

        var result = await tool.InvokeStructuredAsync(new Dictionary<string, object?>
        {
            ["category"] = "Characters",
            ["dataId"] = "char-001",
            ["dataJson"] = "{\"name\":\"Lin Heng\"}",
            ["reason"] = "update profile",
        }, default);

        Assert.Contains("待审核", result.ResultText);
        Assert.False(string.IsNullOrWhiteSpace(result.StagedId));

        var pending = await store.ListPendingAsync();
        var change = Assert.Single(pending);
        Assert.Equal(result.StagedId, change.Id);
        Assert.Equal(StagedChangeType.DataEdit, change.ChangeType);
        Assert.Equal("Characters:char-001", change.TargetId);
        Assert.Equal("{\"name\":\"Lin Heng\"}", change.PayloadJson);
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-detool-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
