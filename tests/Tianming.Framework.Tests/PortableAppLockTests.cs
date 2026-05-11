using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableAppLockTests
{
    [Fact]
    public void Default_config_matches_original_app_lock_defaults()
    {
        var config = PortableAppLockConfig.CreateDefault();

        Assert.Equal(1, config.ConfigVersion);
        Assert.False(config.EnablePasswordLock);
        Assert.False(config.LockOnStartup);
        Assert.False(config.LockOnSwitch);
        Assert.False(config.EnableAutoLock);
        Assert.Equal(5, config.AutoLockMinutes);
        Assert.Null(config.LastActivityTime);
        Assert.Equal(0, config.FailedAttempts);
        Assert.Null(config.LockoutUntil);
        Assert.Null(config.EmergencyCode);
        Assert.Null(config.EmergencyCodeHash);
    }

    [Fact]
    public async Task Store_round_trips_atomically_and_recovers_defaults()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "Framework", "User", "Security", "PasswordProtection", "app_lock_config.json");
        var store = new FileAppLockConfigStore(path);
        var config = PortableAppLockConfig.CreateDefault();
        config.EnablePasswordLock = true;
        config.LockOnStartup = true;
        config.EnableAutoLock = true;
        config.AutoLockMinutes = 15;

        await store.SaveAsync(config);
        var reloaded = await new FileAppLockConfigStore(path).LoadAsync();

        Assert.False(File.Exists(path + ".tmp"));
        Assert.True(reloaded.EnablePasswordLock);
        Assert.True(reloaded.LockOnStartup);
        Assert.True(reloaded.EnableAutoLock);
        Assert.Equal(15, reloaded.AutoLockMinutes);

        await File.WriteAllTextAsync(path, "{ invalid json");
        var recovered = await new FileAppLockConfigStore(path).LoadAsync();

        Assert.False(recovered.EnablePasswordLock);
        Assert.Equal(5, recovered.AutoLockMinutes);
    }

    [Fact]
    public void Policy_requires_enabled_lock_and_password_for_startup_switch_and_auto_lock()
    {
        var now = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);
        var config = PortableAppLockConfig.CreateDefault();
        config.EnablePasswordLock = true;
        config.LockOnStartup = true;
        config.LockOnSwitch = true;
        config.EnableAutoLock = true;
        config.AutoLockMinutes = 5;
        config.LastActivityTime = now.AddMinutes(-6);

        var policy = new PortableAppLockPolicy(() => now, () => true);

        Assert.True(policy.ShouldLockOnStartup(config));
        Assert.True(policy.ShouldLockOnSwitch(config));
        Assert.True(policy.ShouldAutoLock(config));
        Assert.Equal(TimeSpan.Zero, policy.GetTimeUntilAutoLock(config));

        var withoutPassword = new PortableAppLockPolicy(() => now, () => false);

        Assert.False(withoutPassword.ShouldLockOnStartup(config));
        Assert.False(withoutPassword.ShouldAutoLock(config));
    }

    [Fact]
    public void Controller_tracks_lock_state_and_failed_attempt_lockouts()
    {
        var now = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);
        var config = PortableAppLockConfig.CreateDefault();
        var controller = new PortableAppLockController(config, () => now);

        controller.LockApp("测试锁定");
        Assert.True(controller.IsLocked);

        controller.UnlockApp();
        Assert.False(controller.IsLocked);
        Assert.Equal(0, config.FailedAttempts);

        controller.IncrementFailedAttempts();
        controller.IncrementFailedAttempts();
        controller.IncrementFailedAttempts();

        Assert.Equal(3, config.FailedAttempts);
        Assert.Equal(now.AddSeconds(30), config.LockoutUntil);
        Assert.True(controller.IsLockedOut());

        var later = now.AddSeconds(31);
        controller = new PortableAppLockController(config, () => later);

        Assert.False(controller.IsLockedOut());
        Assert.Null(config.LockoutUntil);
    }

    [Fact]
    public void Emergency_code_is_hashed_and_consumed_after_success()
    {
        var config = PortableAppLockConfig.CreateDefault();
        var controller = new PortableAppLockController(config);

        controller.SetEmergencyCode("secret123");

        Assert.True(controller.HasEmergencyCode());
        Assert.Null(config.EmergencyCode);
        Assert.NotNull(config.EmergencyCodeHash);
        Assert.False(controller.VerifyEmergencyCode("wrong"));
        Assert.True(controller.VerifyEmergencyCode("secret123"));
        Assert.False(controller.HasEmergencyCode());
        Assert.False(controller.VerifyEmergencyCode("secret123"));
    }
}
