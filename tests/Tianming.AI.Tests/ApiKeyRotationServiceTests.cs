using TM.Services.Framework.AI.Core;
using Xunit;

namespace Tianming.AI.Tests;

public class ApiKeyRotationServiceTests
{
    [Fact]
    public void GetNextKey_rotates_across_enabled_keys_and_skips_blank_or_disabled_keys()
    {
        var service = new ApiKeyRotationService();
        service.UpdateKeyPool("openai", new List<ApiKeyEntry>
        {
            new() { Id = "K1", Key = "key-1", Remark = "primary" },
            new() { Id = "K2", Key = "key-2", Remark = "backup" },
            new() { Id = "K3", Key = "", Remark = "blank" },
            new() { Id = "K4", Key = "key-4", IsEnabled = false }
        });

        var selections = Enumerable.Range(0, 4)
            .Select(_ =>
            {
                var selection = service.GetNextKey("openai");
                Assert.NotNull(selection);
                return selection.KeyId;
            })
            .ToArray();

        Assert.Equal(["K2", "K1", "K2", "K1"], selections);
    }

    [Fact]
    public void ReportKeyResult_rate_limit_temporarily_skips_key_and_updates_status()
    {
        var service = new ApiKeyRotationService();
        service.UpdateKeyPool("openai", EnabledKeys("K1", "K2"));

        service.ReportKeyResult("openai", "K1", KeyUseResult.RateLimited, "429");

        var selectedIds = Enumerable.Range(0, 4)
            .Select(_ => service.GetNextKey("openai")?.KeyId)
            .ToList();
        var status = service.GetPoolStatus("openai");

        Assert.All(selectedIds, id => Assert.Equal("K2", id));
        Assert.NotNull(status);
        var k1 = Assert.Single(status.Entries, entry => entry.KeyId == "K1");
        Assert.Equal(KeyEntryStatus.TemporarilyDisabled, k1.Status);
        Assert.Equal(KeyUseResult.RateLimited, k1.LastFailureReason);
        Assert.Equal(1, k1.TotalFailures);
    }

    [Fact]
    public void ReportKeyResult_auth_failure_permanently_disables_key_and_raises_state_changed()
    {
        var changedProviders = new List<string>();
        var service = new ApiKeyRotationService();
        service.KeyStateChanged += changedProviders.Add;
        service.UpdateKeyPool("deepseek", EnabledKeys("K1", "K2"));

        service.ReportKeyResult("deepseek", "K1", KeyUseResult.AuthFailure, "invalid key");

        var status = service.GetPoolStatus("deepseek");
        var selectedIds = Enumerable.Range(0, 4)
            .Select(_ => service.GetNextKey("deepseek")?.KeyId)
            .ToList();

        Assert.Equal(["deepseek"], changedProviders);
        Assert.All(selectedIds, id => Assert.Equal("K2", id));
        Assert.NotNull(status);
        var k1 = Assert.Single(status.Entries, entry => entry.KeyId == "K1");
        Assert.Equal(KeyEntryStatus.PermanentlyDisabled, k1.Status);
        Assert.Equal(1, status.ActiveKeys);
    }

    [Fact]
    public void GetNextKey_respects_explicit_exclusion_and_server_error_cooldown_threshold()
    {
        var service = new ApiKeyRotationService();
        service.UpdateKeyPool("claude", EnabledKeys("K1", "K2", "K3"));

        Assert.Equal("K3", service.GetNextKey("claude", new HashSet<string> { "K1", "K2" })?.KeyId);

        service.ReportKeyResult("claude", "K3", KeyUseResult.ServerError);
        service.ReportKeyResult("claude", "K3", KeyUseResult.ServerError);
        service.ReportKeyResult("claude", "K3", KeyUseResult.ServerError);

        var status = service.GetPoolStatus("claude");

        Assert.NotNull(status);
        var k3 = Assert.Single(status.Entries, entry => entry.KeyId == "K3");
        Assert.Equal(KeyEntryStatus.TemporarilyDisabled, k3.Status);
        Assert.Equal(3, k3.TotalFailures);
        Assert.Equal(KeyUseResult.ServerError, k3.LastFailureReason);
    }

    private static List<ApiKeyEntry> EnabledKeys(params string[] ids)
    {
        return ids
            .Select(id => new ApiKeyEntry { Id = id, Key = $"secret-{id}", Remark = id })
            .ToList();
    }
}
