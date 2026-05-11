using TM.Services.Framework.AI.SemanticKernel;
using Xunit;

namespace Tianming.AI.Tests;

public class FileSessionStoreTests
{
    [Fact]
    public void CreateSession_persists_index_and_empty_messages_then_reload_cleans_empty_session()
    {
        using var workspace = new TempDirectory();
        var store = new FileSessionStore(workspace.Path, () => "CH-1");

        var sessionId = store.CreateSession("草稿讨论");
        var session = Assert.Single(store.GetAllSessions());

        Assert.Equal(sessionId, session.Id);
        Assert.Equal("草稿讨论", session.Title);
        Assert.Equal("CH-1", session.ContextChapterId);
        Assert.True(File.Exists(System.IO.Path.Combine(workspace.Path, $"{sessionId}.messages.json")));
        Assert.Empty(store.LoadMessages(sessionId));

        var reloaded = new FileSessionStore(workspace.Path);
        Assert.Empty(reloaded.GetAllSessions());
    }

    [Fact]
    public void SaveMessages_updates_message_count_context_and_recent_order()
    {
        using var workspace = new TempDirectory();
        var currentChapter = "CH-1";
        var store = new FileSessionStore(workspace.Path, () => currentChapter);
        var oldSession = store.CreateSession("旧会话");
        Thread.Sleep(10);
        currentChapter = "CH-2";
        var newSession = store.CreateSession("新会话");

        store.SaveMessages(newSession,
        [
            new SerializedMessageRecord { Role = "user", Summary = "请写第一章" },
            new SerializedMessageRecord { Role = "assistant", Summary = "好的" }
        ]);

        var sessions = store.GetAllSessions();
        var loaded = store.LoadMessages(newSession);

        Assert.Equal([newSession, oldSession], sessions.Select(session => session.Id).ToArray());
        Assert.Equal(2, sessions[0].MessageCount);
        Assert.Equal("CH-2", sessions[0].ContextChapterId);
        Assert.Equal(["user", "assistant"], loaded.Select(message => message.Role).ToArray());
    }

    [Fact]
    public void Rename_and_mode_changes_are_persisted()
    {
        using var workspace = new TempDirectory();
        var store = new FileSessionStore(workspace.Path);
        var sessionId = store.CreateSession("原名");
        store.SaveMessages(sessionId, [new SerializedMessageRecord { Role = "user", Summary = "保留会话" }]);

        store.RenameSession(sessionId, "新名");
        store.UpdateSessionMode(sessionId, "plan");

        var reloaded = new FileSessionStore(workspace.Path);
        var session = Assert.Single(reloaded.GetAllSessions());

        Assert.Equal("新名", session.Title);
        Assert.Equal("plan", reloaded.GetSessionMode(sessionId));
    }

    [Fact]
    public void DeleteSession_removes_index_and_message_file_and_updates_current_session()
    {
        using var workspace = new TempDirectory();
        var store = new FileSessionStore(workspace.Path);
        var first = store.CreateSession("first");
        var second = store.CreateSession("second");

        store.DeleteSession(second);

        Assert.Equal(first, store.GetCurrentSessionIdOrNull());
        Assert.DoesNotContain(store.GetAllSessions(), session => session.Id == second);
        Assert.False(File.Exists(System.IO.Path.Combine(workspace.Path, $"{second}.messages.json")));
    }

    [Fact]
    public void Constructor_removes_empty_sessions_from_index()
    {
        using var workspace = new TempDirectory();
        var store = new FileSessionStore(workspace.Path);
        var empty = store.CreateSession("empty");
        var kept = store.CreateSession("kept");
        store.SaveMessages(kept, [new SerializedMessageRecord { Role = "user", Summary = "保留" }]);

        var reloaded = new FileSessionStore(workspace.Path);

        Assert.DoesNotContain(reloaded.GetAllSessions(), session => session.Id == empty);
        Assert.Contains(reloaded.GetAllSessions(), session => session.Id == kept);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-ai-sessions-{Guid.NewGuid():N}");

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
