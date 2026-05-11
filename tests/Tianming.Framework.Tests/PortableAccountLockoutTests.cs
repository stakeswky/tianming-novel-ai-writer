using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableAccountLockoutTests
{
    [Fact]
    public void Default_data_matches_original_account_lockout_defaults()
    {
        var data = PortableAccountLockoutData.CreateDefault();

        Assert.Equal(0, data.FailedAttempts);
        Assert.Null(data.LockedUntil);
        Assert.False(data.IsPermanentlyLocked);
        Assert.Empty(data.AttemptHistory);
    }

    [Fact]
    public async Task Store_round_trips_atomically_and_recovers_defaults()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "Framework", "User", "Account", "PasswordSecurity", "lockout_data.json");
        var store = new FileAccountLockoutStore(path);
        var data = PortableAccountLockoutData.CreateDefault();
        data.FailedAttempts = 5;
        data.LockedUntil = new DateTime(2026, 5, 10, 12, 15, 0, DateTimeKind.Utc);
        data.AttemptHistory.Add(new PortableLoginAttemptRecord
        {
            Timestamp = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc),
            IsSuccess = false,
            AttemptNumber = 5
        });

        await store.SaveAsync(data);
        var reloaded = await new FileAccountLockoutStore(path).LoadAsync();

        Assert.False(File.Exists(path + ".tmp"));
        Assert.Equal(5, reloaded.FailedAttempts);
        Assert.Equal(new DateTime(2026, 5, 10, 12, 15, 0, DateTimeKind.Utc), reloaded.LockedUntil);
        Assert.Single(reloaded.AttemptHistory);

        await File.WriteAllTextAsync(path, "{ invalid json");
        var recovered = await new FileAccountLockoutStore(path).LoadAsync();

        Assert.Equal(0, recovered.FailedAttempts);
        Assert.Empty(recovered.AttemptHistory);
    }

    [Fact]
    public void Controller_records_failures_and_applies_original_lockout_thresholds()
    {
        var now = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);
        var data = PortableAccountLockoutData.CreateDefault();
        var controller = new PortableAccountLockoutController(data, () => now);

        for (var i = 0; i < 5; i++)
            controller.RecordFailedAttempt();

        Assert.Equal(5, data.FailedAttempts);
        Assert.Equal(now.AddMinutes(15), data.LockedUntil);
        Assert.False(data.IsPermanentlyLocked);
        Assert.True(controller.IsAccountLocked());
        Assert.Equal("15分钟0秒", controller.GetLockoutTimeRemaining());

        for (var i = 0; i < 5; i++)
            controller.RecordFailedAttempt();

        Assert.Equal(10, data.FailedAttempts);
        Assert.Equal(now.AddHours(1), data.LockedUntil);

        for (var i = 0; i < 5; i++)
            controller.RecordFailedAttempt();

        Assert.True(data.IsPermanentlyLocked);
        Assert.Equal(DateTime.MaxValue, data.LockedUntil);
        Assert.Equal("永久锁定", controller.GetLockoutTimeRemaining());
    }

    [Fact]
    public void Reset_records_success_and_trims_history_to_latest_twenty()
    {
        var now = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);
        var step = 0;
        var data = PortableAccountLockoutData.CreateDefault();
        var controller = new PortableAccountLockoutController(data, () => now.AddMinutes(step++));

        for (var i = 0; i < 25; i++)
            controller.RecordFailedAttempt();

        controller.ResetFailedAttempts();

        Assert.Equal(0, data.FailedAttempts);
        Assert.Null(data.LockedUntil);
        Assert.False(data.IsPermanentlyLocked);
        Assert.Equal(20, data.AttemptHistory.Count);
        Assert.True(data.AttemptHistory[^1].IsSuccess);
        Assert.Equal(0, data.AttemptHistory[^1].AttemptNumber);
    }

    [Fact]
    public void Recent_attempts_and_week_statistics_are_binding_friendly()
    {
        var now = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);
        var data = PortableAccountLockoutData.CreateDefault();
        data.AttemptHistory.Add(new PortableLoginAttemptRecord { Timestamp = now.AddDays(-8), IsSuccess = false, AttemptNumber = 1 });
        data.AttemptHistory.Add(new PortableLoginAttemptRecord { Timestamp = now.AddDays(-2), IsSuccess = false, AttemptNumber = 2 });
        data.AttemptHistory.Add(new PortableLoginAttemptRecord { Timestamp = now.AddDays(-1), IsSuccess = true, AttemptNumber = 0 });
        var controller = new PortableAccountLockoutController(data, () => now);

        var recent = controller.GetRecentAttempts(2);
        var stats = controller.BuildStatistics(days: 7);

        Assert.Equal([true, false], recent.Select(item => item.IsSuccess).ToArray());
        Assert.Equal(2, stats.TotalAttempts);
        Assert.Equal(1, stats.FailedAttempts);
        Assert.Equal(1, stats.SuccessfulAttempts);
    }

    [Fact]
    public async Task ServerController_syncs_lockout_status_and_preserves_local_on_failure()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "Framework", "User", "Account", "PasswordSecurity", "lockout_data.json");
        var store = new FileAccountLockoutStore(path);
        await store.SaveAsync(new PortableAccountLockoutData
        {
            FailedAttempts = 2,
            LockedUntil = null,
            IsPermanentlyLocked = false
        });
        var api = new RecordingAccountLockoutApi
        {
            StatusResponse = new PortableApiResponse<PortableAccountLockoutStatus>
            {
                Success = true,
                Data = new PortableAccountLockoutStatus
                {
                    IsLocked = true,
                    FailedAttempts = 9,
                    LockedUntil = new DateTime(2026, 5, 11, 11, 0, 0, DateTimeKind.Utc),
                    IsPermanentlyLocked = false
                }
            }
        };
        var controller = new PortableAccountLockoutServerController(store, api);

        var synced = await controller.SyncLockoutStatusFromServerAsync();
        api.StatusResponse = new PortableApiResponse<PortableAccountLockoutStatus>
        {
            Success = false,
            Message = "down"
        };
        var failed = await controller.SyncLockoutStatusFromServerAsync();
        var data = await store.LoadAsync();

        Assert.True(synced);
        Assert.False(failed);
        Assert.Equal(9, data.FailedAttempts);
        Assert.Equal(new DateTime(2026, 5, 11, 11, 0, 0, DateTimeKind.Utc), data.LockedUntil);
        Assert.False(data.IsPermanentlyLocked);
    }

    [Fact]
    public async Task ServerController_unlock_clears_local_state_even_when_server_unlock_fails()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "Framework", "User", "Account", "PasswordSecurity", "lockout_data.json");
        var store = new FileAccountLockoutStore(path);
        await store.SaveAsync(new PortableAccountLockoutData
        {
            FailedAttempts = 15,
            LockedUntil = DateTime.MaxValue,
            IsPermanentlyLocked = true
        });
        var api = new RecordingAccountLockoutApi
        {
            UnlockResponse = new PortableApiResponse<object>
            {
                Success = false,
                Message = "server down"
            }
        };
        var controller = new PortableAccountLockoutServerController(store, api);

        var unlocked = await controller.UnlockAccountAsync();
        var data = await store.LoadAsync();

        Assert.True(unlocked);
        Assert.Equal(0, data.FailedAttempts);
        Assert.Null(data.LockedUntil);
        Assert.False(data.IsPermanentlyLocked);
    }

    private sealed class RecordingAccountLockoutApi : IPortableAccountLockoutApi
    {
        public PortableApiResponse<PortableAccountLockoutStatus> StatusResponse { get; set; } = new() { Success = true };
        public PortableApiResponse<object> UnlockResponse { get; set; } = new() { Success = true };

        public Task<PortableApiResponse<PortableAccountLockoutStatus>> GetLockoutStatusAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(StatusResponse);
        }

        public Task<PortableApiResponse<object>> UnlockAccountAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(UnlockResponse);
        }
    }
}
