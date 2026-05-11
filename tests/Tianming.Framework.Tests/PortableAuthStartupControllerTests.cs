using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableAuthStartupControllerTests
{
    [Fact]
    public async Task CheckLoginStatus_returns_false_when_no_login_state_exists()
    {
        using var temp = new TempDirectory();
        var tokenStore = CreateStore(temp);
        var api = new RecordingStartupApi();
        var controller = new PortableAuthStartupController(tokenStore, api, () => { });

        var result = await controller.CheckLoginStatusAsync();

        Assert.False(result.IsLoggedIn);
        Assert.Equal(PortableAuthStartupStatus.NotLoggedIn, result.Status);
        Assert.False(api.RefreshCalled);
    }

    [Fact]
    public async Task CheckLoginStatus_returns_valid_when_access_token_is_current()
    {
        using var temp = new TempDirectory();
        var tokenStore = CreateStore(temp);
        tokenStore.SaveTokens(new PortableLoginResult
        {
            AccessToken = "access-1",
            RefreshToken = "refresh-1",
            SessionKey = "session-1",
            ExpiresAt = new DateTime(2026, 5, 10, 13, 0, 0, DateTimeKind.Utc),
            User = new PortableUserInfo { UserId = "u1", Username = "jimmy" }
        });
        var api = new RecordingStartupApi();
        var controller = new PortableAuthStartupController(tokenStore, api, () => { });

        var result = await controller.CheckLoginStatusAsync();

        Assert.True(result.IsLoggedIn);
        Assert.Equal(PortableAuthStartupStatus.Valid, result.Status);
        Assert.Equal("jimmy", result.Username);
        Assert.False(api.RefreshCalled);
    }

    [Fact]
    public async Task CheckLoginStatus_refreshes_expired_access_token_when_refresh_token_exists()
    {
        using var temp = new TempDirectory();
        var tokenStore = CreateStore(temp, nowUtc: new DateTime(2026, 5, 10, 12, 30, 0, DateTimeKind.Utc));
        tokenStore.SaveTokens(new PortableLoginResult
        {
            AccessToken = "old-access",
            RefreshToken = "refresh-1",
            SessionKey = "old-session",
            ExpiresAt = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc),
            User = new PortableUserInfo { UserId = "u1", Username = "jimmy" }
        });
        var api = new RecordingStartupApi
        {
            RefreshResponse = new PortableApiResponse<PortableRefreshTokenResult>
            {
                Success = true,
                Data = new PortableRefreshTokenResult
                {
                    AccessToken = "new-access",
                    RefreshToken = "new-refresh",
                    SessionKey = "new-session",
                    ExpiresAt = new DateTime(2026, 5, 10, 14, 0, 0, DateTimeKind.Utc)
                }
            }
        };
        var controller = new PortableAuthStartupController(tokenStore, api, () => { });

        var result = await controller.CheckLoginStatusAsync();

        Assert.True(result.IsLoggedIn);
        Assert.Equal(PortableAuthStartupStatus.Refreshed, result.Status);
        Assert.Equal("new-access", tokenStore.AccessToken);
        Assert.Equal("new-refresh", tokenStore.RefreshToken);
        Assert.Equal("new-session", tokenStore.SessionKey);
    }

    [Fact]
    public async Task CheckLoginStatus_clears_tokens_when_refresh_reports_device_kicked()
    {
        using var temp = new TempDirectory();
        var tokenStore = CreateExpiredStore(temp);
        var api = new RecordingStartupApi
        {
            RefreshResponse = new PortableApiResponse<PortableRefreshTokenResult>
            {
                Success = false,
                ErrorCode = PortableApiErrorCodes.AuthDeviceKicked,
                Message = "kicked"
            }
        };
        var controller = new PortableAuthStartupController(tokenStore, api, () => { });

        var result = await controller.CheckLoginStatusAsync();

        Assert.False(result.IsLoggedIn);
        Assert.Equal(PortableAuthStartupStatus.DeviceKicked, result.Status);
        Assert.Null(tokenStore.AccessToken);
    }

    [Fact]
    public async Task Logout_clears_tokens_and_subscription_cache_even_when_server_logout_fails()
    {
        using var temp = new TempDirectory();
        var tokenStore = CreateStore(temp);
        tokenStore.SaveTokens(new PortableLoginResult
        {
            AccessToken = "access-1",
            RefreshToken = "refresh-1",
            SessionKey = "session-1",
            ExpiresAt = new DateTime(2026, 5, 10, 13, 0, 0, DateTimeKind.Utc),
            User = new PortableUserInfo { UserId = "u1", Username = "jimmy" }
        });
        var clearedSubscription = false;
        var api = new RecordingStartupApi
        {
            LogoutResponse = new PortableApiResponse<object>
            {
                Success = false,
                Message = "server unavailable"
            }
        };
        var controller = new PortableAuthStartupController(tokenStore, api, () => clearedSubscription = true);

        var result = await controller.LogoutAsync();

        Assert.False(result.Success);
        Assert.True(api.LogoutCalled);
        Assert.Null(tokenStore.AccessToken);
        Assert.True(clearedSubscription);
    }

    private static PortableAuthTokenStore CreateExpiredStore(TempDirectory temp)
    {
        var store = CreateStore(temp, nowUtc: new DateTime(2026, 5, 10, 12, 30, 0, DateTimeKind.Utc));
        store.SaveTokens(new PortableLoginResult
        {
            AccessToken = "old-access",
            RefreshToken = "refresh-1",
            SessionKey = "old-session",
            ExpiresAt = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc),
            User = new PortableUserInfo { UserId = "u1", Username = "jimmy" }
        });
        return store;
    }

    private static PortableAuthTokenStore CreateStore(TempDirectory temp, DateTime? nowUtc = null)
    {
        return new PortableAuthTokenStore(
            Path.Combine(temp.Path, "auth_token.dat"),
            new Base64JsonTokenProtector(),
            () => nowUtc ?? new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc),
            () => "client-1",
            () => "nonce-1");
    }

    private sealed class Base64JsonTokenProtector : IPortableAuthTokenProtector
    {
        public string Protect(byte[] data) => Convert.ToBase64String(data);
        public byte[] Unprotect(string payload) => Convert.FromBase64String(payload);
    }

    private sealed class RecordingStartupApi : IPortableAuthStartupApi
    {
        public bool RefreshCalled { get; private set; }
        public bool LogoutCalled { get; private set; }

        public PortableApiResponse<PortableRefreshTokenResult> RefreshResponse { get; init; } = new()
        {
            Success = false,
            Message = "refresh failed"
        };

        public PortableApiResponse<object> LogoutResponse { get; init; } = new()
        {
            Success = true
        };

        public Task<PortableApiResponse<PortableRefreshTokenResult>> RefreshTokenAsync(CancellationToken cancellationToken = default)
        {
            RefreshCalled = true;
            return Task.FromResult(RefreshResponse);
        }

        public Task<PortableApiResponse<object>> LogoutAsync(CancellationToken cancellationToken = default)
        {
            LogoutCalled = true;
            return Task.FromResult(LogoutResponse);
        }
    }
}
