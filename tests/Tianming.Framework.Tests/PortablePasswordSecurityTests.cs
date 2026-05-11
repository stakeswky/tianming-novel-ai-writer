using System.Security.Cryptography;
using System.Text;
using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortablePasswordSecurityTests
{
    [Fact]
    public async Task Store_round_trips_password_history_and_two_factor_data()
    {
        using var workspace = new TempDirectory();
        var store = new FilePasswordSecurityStore(workspace.Path);
        var data = new PortablePasswordData
        {
            PasswordHash = "hash",
            Salt = "salt",
            Iterations = 100000,
            HashAlgorithm = "PBKDF2",
            LastModifiedTime = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc)
        };

        await store.SavePasswordDataAsync(data);
        await store.SavePasswordHistoryAsync(["a", "b"]);
        await store.SaveTwoFactorDataAsync(new PortableTwoFactorAuthData
        {
            Secret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ",
            IsEnabled = true,
            EnabledTime = new DateTime(2026, 5, 10, 13, 0, 0, DateTimeKind.Utc)
        });

        Assert.Equal("hash", (await store.LoadPasswordDataAsync())?.PasswordHash);
        Assert.Equal(["a", "b"], await store.LoadPasswordHistoryAsync());
        Assert.True((await store.LoadTwoFactorDataAsync())?.IsEnabled);
    }

    [Fact]
    public async Task Service_sets_and_verifies_initial_password_with_pbkdf2()
    {
        using var workspace = new TempDirectory();
        var store = new FilePasswordSecurityStore(workspace.Path);
        var service = new PortableAccountSecurityService(store, () => new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc));

        await service.SetInitialPasswordAsync("Stronger-123");

        var data = await store.LoadPasswordDataAsync();

        Assert.NotNull(data);
        Assert.Equal("PBKDF2", data.HashAlgorithm);
        Assert.Equal(100000, data.Iterations);
        Assert.True(await service.VerifyPasswordAsync("Stronger-123"));
        Assert.False(await service.VerifyPasswordAsync("wrong"));
        Assert.True(await service.HasPasswordAsync());
        Assert.Single(await store.LoadPasswordHistoryAsync());
    }

    [Fact]
    public async Task ChangePasswordAsync_verifies_old_password_rejects_current_password_and_updates_hash()
    {
        using var workspace = new TempDirectory();
        var store = new FilePasswordSecurityStore(workspace.Path);
        var service = new PortableAccountSecurityService(store, () => new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc));
        await service.SetInitialPasswordAsync("OldPass-123");

        Assert.False(await service.ChangePasswordAsync("wrong", "NewPass-123"));
        Assert.False(await service.ChangePasswordAsync("OldPass-123", "OldPass-123"));

        Assert.True(await service.ChangePasswordAsync("OldPass-123", "NewPass-123"));
        Assert.True(await service.VerifyPasswordAsync("NewPass-123"));
        Assert.False(await service.VerifyPasswordAsync("OldPass-123"));
        Assert.Equal(2, (await store.LoadPasswordHistoryAsync()).Count);
    }

    [Fact]
    public async Task VerifyPassword_upgrades_legacy_sha256_hash_to_pbkdf2()
    {
        using var workspace = new TempDirectory();
        var store = new FilePasswordSecurityStore(workspace.Path);
        var salt = Convert.ToBase64String(Encoding.UTF8.GetBytes("legacy-salt"));
        await store.SavePasswordDataAsync(new PortablePasswordData
        {
            PasswordHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("old-password" + salt))),
            Salt = salt,
            HashAlgorithm = "SHA256",
            Iterations = 1
        });
        var service = new PortableAccountSecurityService(store, () => new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc));

        Assert.True(await service.VerifyPasswordAsync("old-password"));

        var upgraded = await store.LoadPasswordDataAsync();
        Assert.NotNull(upgraded);
        Assert.Equal("PBKDF2", upgraded.HashAlgorithm);
        Assert.Equal(100000, upgraded.Iterations);
    }

    [Fact]
    public async Task Totp_verification_accepts_neighboring_time_steps()
    {
        using var workspace = new TempDirectory();
        var store = new FilePasswordSecurityStore(workspace.Path);
        var service = new PortableAccountSecurityService(store);
        const string secret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

        await service.EnableTwoFactorAuthAsync(secret, new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc));
        var code = PortableTotp.GenerateCode(secret, timeStep: 1);

        Assert.Equal("287082", code);
        Assert.True(await service.VerifyTotpCodeAsync(code, timeStep: 2));
        Assert.False(await service.VerifyTotpCodeAsync("000000", timeStep: 2));
    }

    [Fact]
    public async Task TwoFactor_status_secret_and_disable_match_original_service_behavior()
    {
        using var workspace = new TempDirectory();
        var store = new FilePasswordSecurityStore(workspace.Path);
        var service = new PortableAccountSecurityService(store);
        const string secret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";

        Assert.False(await service.IsTwoFactorEnabledAsync());
        Assert.Null(await service.GetTwoFactorSecretAsync());

        var enabledSecret = await service.EnableTwoFactorAuthAsync(secret, new DateTime(2026, 5, 11, 8, 0, 0, DateTimeKind.Utc));

        Assert.Equal(secret, enabledSecret);
        Assert.True(await service.IsTwoFactorEnabledAsync());
        Assert.Equal(secret, await service.GetTwoFactorSecretAsync());

        await service.DisableTwoFactorAuthAsync();

        Assert.False(await service.IsTwoFactorEnabledAsync());
        Assert.Equal(secret, await service.GetTwoFactorSecretAsync());
    }
}
