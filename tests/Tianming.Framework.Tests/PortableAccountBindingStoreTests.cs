using System.Text.Json;
using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableAccountBindingStoreTests
{
    [Fact]
    public void Missing_file_returns_empty_bindings_and_history()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp);

        Assert.Empty(store.GetAllBindings());
        Assert.Empty(store.GetHistory());
        Assert.False(store.IsBound(PortableBindingPlatform.GitHub));
        Assert.Null(store.GetBinding(PortableBindingPlatform.GitHub));
    }

    [Fact]
    public void SaveFromServer_maps_api_bindings_to_synced_local_cache()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp);
        var boundTime = new DateTime(2026, 5, 9, 8, 0, 0, DateTimeKind.Utc);

        store.SaveFromServer(new PortableBindingsResult
        {
            Bindings =
            {
                new PortableBindingInfo
                {
                    BindingId = 7,
                    Platform = "github",
                    PlatformUserId = "gh-1",
                    DisplayName = "Jimmy",
                    BoundTime = boundTime
                },
                new PortableBindingInfo
                {
                    BindingId = 8,
                    Platform = "qq",
                    PlatformUserId = "qq-1",
                    DisplayName = null,
                    BoundTime = boundTime.AddHours(1)
                }
            }
        });

        var reloaded = CreateStore(temp);
        var bindings = reloaded.GetAllBindings();

        Assert.Equal(2, bindings.Count);
        Assert.Equal(PortableBindingPlatform.GitHub, bindings[0].Platform);
        Assert.Equal("gh-1", bindings[0].AccountId);
        Assert.Equal("Jimmy", bindings[0].Nickname);
        Assert.Equal(boundTime, bindings[0].BindTime);
        Assert.True(bindings[0].IsActive);
        Assert.Equal(PortableBindingSyncStatus.Synced, bindings[0].SyncStatus);
        Assert.Equal(PortableBindingPlatform.QQ, bindings[1].Platform);
        Assert.Equal(string.Empty, bindings[1].Nickname);
    }

    [Fact]
    public void BindAccount_adds_binding_with_default_permissions_and_history()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp);

        var result = store.BindAccount(PortableBindingPlatform.GitHub, "gh-1", "Jimmy");

        Assert.True(result);
        var binding = Assert.Single(store.GetAllBindings());
        Assert.Equal(PortableBindingPlatform.GitHub, binding.Platform);
        Assert.Equal("gh-1", binding.AccountId);
        Assert.Equal("Jimmy", binding.Nickname);
        Assert.Equal(new[] { "basic_info", "profile" }, binding.Permissions);
        Assert.Equal(PortableBindingSyncStatus.Synced, binding.SyncStatus);
        Assert.Equal(Now, binding.BindTime);
        Assert.Equal(Now, binding.LastUseTime);
        var history = Assert.Single(store.GetHistory());
        Assert.Equal(PortableBindingAction.Bind, history.Action);
        Assert.Equal("首次绑定账号", history.Details);
    }

    [Fact]
    public void BindAccount_updates_existing_platform_and_records_update_history()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp);
        store.BindAccount(PortableBindingPlatform.GitHub, "gh-1", "Jimmy");

        store.BindAccount(
            PortableBindingPlatform.GitHub,
            "gh-2",
            "James",
            email: "jimmy@example.com",
            avatarUrl: "https://example.com/a.png",
            permissions: new[] { "repo", "email" });

        var binding = Assert.Single(store.GetAllBindings());
        Assert.Equal("gh-2", binding.AccountId);
        Assert.Equal("James", binding.Nickname);
        Assert.Equal("jimmy@example.com", binding.Email);
        Assert.Equal("https://example.com/a.png", binding.AvatarUrl);
        Assert.Equal(new[] { "repo", "email" }, binding.Permissions);
        var history = store.GetHistory().ToArray();
        Assert.Equal(2, history.Length);
        Assert.Equal(PortableBindingAction.Update, history[0].Action);
        Assert.Equal("更新账号信息", history[0].Details);
    }

    [Fact]
    public void UnbindAccount_marks_existing_binding_inactive_and_records_history()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp);
        store.BindAccount(PortableBindingPlatform.GitHub, "gh-1", "Jimmy");

        var result = store.UnbindAccount(PortableBindingPlatform.GitHub);

        Assert.True(result);
        Assert.False(store.IsBound(PortableBindingPlatform.GitHub));
        Assert.False(Assert.Single(store.GetAllBindings()).IsActive);
        var history = store.GetHistory().ToArray();
        Assert.Equal(2, history.Length);
        Assert.Equal(PortableBindingAction.Unbind, history[0].Action);
        Assert.Equal("用户主动解绑", history[0].Details);
    }

    [Fact]
    public void UpdateSyncStatus_and_permissions_change_active_binding()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp);
        store.BindAccount(PortableBindingPlatform.GitHub, "gh-1", "Jimmy");

        Assert.True(store.UpdateSyncStatus(PortableBindingPlatform.GitHub, PortableBindingSyncStatus.Failed));
        Assert.True(store.UpdatePermissions(PortableBindingPlatform.GitHub, new[] { "email" }));

        var binding = Assert.Single(store.GetAllBindings());
        Assert.Equal(PortableBindingSyncStatus.Failed, binding.SyncStatus);
        Assert.Equal(Now, binding.LastSyncTime);
        Assert.Equal(new[] { "email" }, binding.Permissions);
        Assert.Contains(store.GetHistory(), record =>
            record.Action == PortableBindingAction.PermissionChange
            && record.Details == "权限更新: email");
    }

    [Fact]
    public void RecordUsage_updates_active_binding_last_use_time()
    {
        using var temp = new TempDirectory();
        var times = new Queue<DateTime>(new[]
        {
            Now,
            Now,
            Now.AddHours(2)
        });
        var store = CreateStore(temp, () => times.Dequeue());
        store.BindAccount(PortableBindingPlatform.GitHub, "gh-1", "Jimmy");

        store.RecordUsage(PortableBindingPlatform.GitHub);

        Assert.Equal(Now.AddHours(2), Assert.Single(store.GetAllBindings()).LastUseTime);
    }

    [Fact]
    public void GetHistory_filters_platform_orders_descending_and_applies_limit()
    {
        using var temp = new TempDirectory();
        var counter = 0;
        var store = CreateStore(temp, () => Now.AddMinutes(counter++));

        store.BindAccount(PortableBindingPlatform.GitHub, "gh-1", "Jimmy");
        store.BindAccount(PortableBindingPlatform.QQ, "qq-1", "QQ");
        store.UpdatePermissions(PortableBindingPlatform.GitHub, new[] { "email" });

        var history = store.GetHistory(PortableBindingPlatform.GitHub, limit: 1);

        var record = Assert.Single(history);
        Assert.Equal(PortableBindingPlatform.GitHub, record.Platform);
        Assert.Equal(PortableBindingAction.PermissionChange, record.Action);
    }

    [Fact]
    public void History_is_trimmed_to_latest_100_records()
    {
        using var temp = new TempDirectory();
        var counter = 0;
        var store = CreateStore(temp, () => Now.AddMinutes(counter++));

        for (var i = 0; i < 105; i++)
        {
            store.BindAccount(PortableBindingPlatform.GitHub, $"gh-{i}", $"Jimmy {i}");
        }

        var history = store.GetHistory(limit: 200);

        Assert.Equal(100, history.Count);
        Assert.Equal("gh-104", history[0].AccountId);
        Assert.Equal("gh-5", history[^1].AccountId);
    }

    [Fact]
    public void Invalid_json_recovers_with_empty_data()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "bindings.json"), "{bad json");

        var store = CreateStore(temp);

        Assert.Empty(store.GetAllBindings());
        Assert.Empty(store.GetHistory());
    }

    [Fact]
    public void Saved_json_uses_original_pascal_case_fields()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp);

        store.BindAccount(PortableBindingPlatform.GitHub, "gh-1", "Jimmy");

        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(temp.Path, "bindings.json")));
        var binding = document.RootElement.GetProperty("Bindings")[0];
        var history = document.RootElement.GetProperty("History")[0];
        Assert.Equal("GitHub", binding.GetProperty("Platform").GetString());
        Assert.Equal("gh-1", binding.GetProperty("AccountId").GetString());
        Assert.Equal("Synced", binding.GetProperty("SyncStatus").GetString());
        Assert.Equal("Bind", history.GetProperty("Action").GetString());
    }

    private static readonly DateTime Now = new(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);

    private static PortableAccountBindingStore CreateStore(TempDirectory temp, Func<DateTime>? now = null)
    {
        return new PortableAccountBindingStore(Path.Combine(temp.Path, "bindings.json"), now ?? (() => Now));
    }
}
