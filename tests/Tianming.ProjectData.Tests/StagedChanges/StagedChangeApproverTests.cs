using System;
using System.IO;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.StagedChanges;
using Xunit;

namespace Tianming.ProjectData.Tests.StagedChanges;

public sealed class StagedChangeApproverTests
{
    [Fact]
    public async Task Approve_dispatches_content_changes()
    {
        var root = CreateTempDir();
        var store = new FileStagedChangeStore(root);
        var id = await store.StageAsync(new StagedChange
        {
            ChangeType = StagedChangeType.ContentEdit,
            TargetId = "ch-001",
            NewContentSnippet = "new chapter text",
        });

        var called = false;
        var approver = new StagedChangeApprover(
            store,
            content: (chapterId, newContent, _) =>
            {
                called = chapterId == "ch-001" && newContent == "new chapter text";
                return Task.CompletedTask;
            },
            data: (_, _, _, _) => Task.CompletedTask,
            workspace: (_, _, _) => Task.CompletedTask);

        var approved = await approver.ApproveAsync(id);

        Assert.True(approved);
        Assert.True(called);
        Assert.Null(await store.GetAsync(id));
    }

    [Fact]
    public async Task Approve_dispatches_data_changes()
    {
        var root = CreateTempDir();
        var store = new FileStagedChangeStore(root);
        var id = await store.StageAsync(new StagedChange
        {
            ChangeType = StagedChangeType.DataEdit,
            TargetId = "Characters:char-001",
            PayloadJson = "{\"name\":\"Lin Heng\"}",
        });

        var called = false;
        var approver = new StagedChangeApprover(
            store,
            content: (_, _, _) => Task.CompletedTask,
            data: (category, dataId, dataJson, _) =>
            {
                called = category == "Characters"
                    && dataId == "char-001"
                    && dataJson == "{\"name\":\"Lin Heng\"}";
                return Task.CompletedTask;
            },
            workspace: (_, _, _) => Task.CompletedTask);

        var approved = await approver.ApproveAsync(id);

        Assert.True(approved);
        Assert.True(called);
        Assert.Null(await store.GetAsync(id));
    }

    [Fact]
    public async Task Approve_dispatches_workspace_changes()
    {
        var root = CreateTempDir();
        var store = new FileStagedChangeStore(root);
        var id = await store.StageAsync(new StagedChange
        {
            ChangeType = StagedChangeType.WorkspaceEdit,
            TargetId = "README.md",
            NewContentSnippet = "# rewritten",
        });

        var called = false;
        var approver = new StagedChangeApprover(
            store,
            content: (_, _, _) => Task.CompletedTask,
            data: (_, _, _, _) => Task.CompletedTask,
            workspace: (relativePath, newContent, _) =>
            {
                called = relativePath == "README.md" && newContent == "# rewritten";
                return Task.CompletedTask;
            });

        var approved = await approver.ApproveAsync(id);

        Assert.True(approved);
        Assert.True(called);
        Assert.Null(await store.GetAsync(id));
    }

    [Fact]
    public async Task Reject_removes_without_applying()
    {
        var root = CreateTempDir();
        var store = new FileStagedChangeStore(root);
        var id = await store.StageAsync(new StagedChange
        {
            ChangeType = StagedChangeType.ContentEdit,
            TargetId = "ch-001",
            NewContentSnippet = "new chapter text",
        });

        var approver = new StagedChangeApprover(
            store,
            content: (_, _, _) => throw new InvalidOperationException("should not be called"),
            data: (_, _, _, _) => throw new InvalidOperationException("should not be called"),
            workspace: (_, _, _) => throw new InvalidOperationException("should not be called"));

        var rejected = await approver.RejectAsync(id);

        Assert.True(rejected);
        Assert.Null(await store.GetAsync(id));
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-approver-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }
}
