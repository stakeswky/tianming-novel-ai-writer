using System.Text.Json;
using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableSubscriptionStoreTests
{
    [Fact]
    public void Constructor_returns_default_free_state_when_file_is_missing()
    {
        using var temp = new TempDirectory();

        var store = CreateStore(temp);

        Assert.False(store.IsActive);
        Assert.Equal("free", store.PlanType);
        Assert.Equal(0, store.RemainingDays);
        Assert.Null(store.EndTime);
        Assert.Null(store.GetSubscriptionInfo());
    }

    [Fact]
    public void SaveFromServer_persists_subscription_snapshot()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp);
        var endTime = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc);

        store.SaveFromServer(new PortableSubscriptionInfo
        {
            SubscriptionId = 12,
            UserId = "u1",
            PlanType = "pro",
            StartTime = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            EndTime = endTime,
            IsActive = true,
            Source = "server"
        });
        var reloaded = CreateStore(temp);

        Assert.True(reloaded.IsActive);
        Assert.Equal("pro", reloaded.PlanType);
        Assert.Equal(10, reloaded.RemainingDays);
        Assert.Equal(endTime, reloaded.EndTime);
        Assert.Equal("u1", reloaded.GetSubscriptionInfo()!.UserId);
        Assert.Equal("server", reloaded.GetSubscriptionInfo()!.Source);
    }

    [Fact]
    public void SaveActivationResult_updates_cache_using_original_card_key_source()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp);
        var newExpireTime = new DateTime(2026, 6, 9, 12, 0, 0, DateTimeKind.Utc);

        store.SaveActivationResult(new PortableActivationResult
        {
            Success = true,
            DaysAdded = 30,
            NewExpireTime = newExpireTime,
            Subscription = new PortableSubscriptionInfo
            {
                SubscriptionId = 42,
                UserId = "u2",
                PlanType = "vip",
                IsActive = true,
                Source = "ignored"
            }
        });
        var snapshot = store.GetSubscriptionInfo()!;

        Assert.True(store.IsActive);
        Assert.Equal("vip", store.PlanType);
        Assert.Equal(30, store.RemainingDays);
        Assert.Equal(newExpireTime, store.EndTime);
        Assert.Equal(42, snapshot.SubscriptionId);
        Assert.Equal("u2", snapshot.UserId);
        Assert.Equal("card_key", snapshot.Source);
    }

    [Fact]
    public void RemainingDays_is_zero_when_subscription_has_expired()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp);

        store.SaveFromServer(new PortableSubscriptionInfo
        {
            PlanType = "pro",
            EndTime = new DateTime(2026, 5, 9, 12, 0, 0, DateTimeKind.Utc),
            IsActive = true
        });

        Assert.Equal(0, store.RemainingDays);
    }

    [Fact]
    public void ClearCache_removes_cached_data_and_file()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp);
        store.SaveFromServer(new PortableSubscriptionInfo
        {
            PlanType = "pro",
            EndTime = new DateTime(2026, 5, 20, 12, 0, 0, DateTimeKind.Utc),
            IsActive = true
        });

        store.ClearCache();

        Assert.False(store.IsActive);
        Assert.Equal("free", store.PlanType);
        Assert.Null(store.GetSubscriptionInfo());
        Assert.False(File.Exists(Path.Combine(temp.Path, "subscription.json")));
    }

    [Fact]
    public void Constructor_recovers_from_invalid_json()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "subscription.json"), "{bad json");

        var store = CreateStore(temp);

        Assert.False(store.IsActive);
        Assert.Equal("free", store.PlanType);
        Assert.Null(store.GetSubscriptionInfo());
    }

    [Fact]
    public void SaveFromServer_writes_pascal_case_local_cache_fields()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp);

        store.SaveFromServer(new PortableSubscriptionInfo
        {
            SubscriptionId = 3,
            UserId = "u3",
            PlanType = "pro",
            IsActive = true
        });

        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(temp.Path, "subscription.json")));
        Assert.Equal(3, document.RootElement.GetProperty("SubscriptionId").GetInt32());
        Assert.Equal("u3", document.RootElement.GetProperty("UserId").GetString());
        Assert.Equal("pro", document.RootElement.GetProperty("PlanType").GetString());
        Assert.True(document.RootElement.GetProperty("IsActive").GetBoolean());
    }

    private static PortableSubscriptionStore CreateStore(TempDirectory temp)
    {
        return new PortableSubscriptionStore(
            Path.Combine(temp.Path, "subscription.json"),
            () => new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc));
    }
}
