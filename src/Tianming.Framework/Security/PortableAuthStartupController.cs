namespace TM.Framework.Security;

public enum PortableAuthStartupStatus
{
    NotLoggedIn,
    Valid,
    Refreshed,
    RefreshFailed,
    DeviceKicked,
    NoRefreshToken,
    Error
}

public sealed class PortableAuthStartupResult
{
    public bool IsLoggedIn { get; init; }
    public PortableAuthStartupStatus Status { get; init; }
    public string? Username { get; init; }
    public string? UserId { get; init; }
    public string? Message { get; init; }
}

public interface IPortableAuthStartupApi
{
    Task<PortableApiResponse<PortableRefreshTokenResult>> RefreshTokenAsync(CancellationToken cancellationToken = default);
    Task<PortableApiResponse<object>> LogoutAsync(CancellationToken cancellationToken = default);
}

public sealed class PortableAuthStartupController
{
    private readonly PortableAuthTokenStore _tokenStore;
    private readonly IPortableAuthStartupApi _api;
    private readonly Action _clearSubscriptionCache;

    public PortableAuthStartupController(
        PortableAuthTokenStore tokenStore,
        IPortableAuthStartupApi api,
        Action clearSubscriptionCache)
    {
        _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _clearSubscriptionCache = clearSubscriptionCache ?? throw new ArgumentNullException(nameof(clearSubscriptionCache));
    }

    public async Task<PortableAuthStartupResult> CheckLoginStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_tokenStore.IsLoggedIn && !_tokenStore.HasRefreshToken)
            {
                return Result(false, PortableAuthStartupStatus.NotLoggedIn, "未检测到登录状态");
            }

            if (_tokenStore.IsAccessTokenExpired)
            {
                if (!_tokenStore.HasRefreshToken)
                {
                    return Result(false, PortableAuthStartupStatus.NoRefreshToken, "no refresh");
                }

                var refresh = await _api.RefreshTokenAsync(cancellationToken);
                if (refresh.Success && refresh.Data != null)
                {
                    _tokenStore.UpdateTokens(refresh.Data);
                    return Result(true, PortableAuthStartupStatus.Refreshed);
                }

                if (refresh.ErrorCode == PortableApiErrorCodes.AuthDeviceKicked)
                {
                    _tokenStore.ClearTokens();
                    return Result(false, PortableAuthStartupStatus.DeviceKicked, "您的账号已在其他设备登录");
                }

                return Result(false, PortableAuthStartupStatus.RefreshFailed, refresh.Message ?? "refresh fail");
            }

            return Result(true, PortableAuthStartupStatus.Valid);
        }
        catch (Exception ex)
        {
            return Result(false, PortableAuthStartupStatus.Error, ex.Message);
        }
    }

    public void HandleDeviceKicked()
    {
        _tokenStore.ClearTokens();
        _clearSubscriptionCache();
    }

    public async Task<PortableApiResponse<object>> LogoutAsync(CancellationToken cancellationToken = default)
    {
        PortableApiResponse<object>? response = null;
        try
        {
            response = await _api.LogoutAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            response = new PortableApiResponse<object>
            {
                Success = false,
                Message = ex.Message,
                ErrorCode = PortableApiErrorCodes.NetworkError
            };
        }
        finally
        {
            _tokenStore.ClearTokens();
            _clearSubscriptionCache();
        }

        return response;
    }

    private PortableAuthStartupResult Result(
        bool isLoggedIn,
        PortableAuthStartupStatus status,
        string? message = null)
    {
        return new PortableAuthStartupResult
        {
            IsLoggedIn = isLoggedIn,
            Status = status,
            Username = _tokenStore.Username,
            UserId = _tokenStore.UserId,
            Message = message
        };
    }
}
