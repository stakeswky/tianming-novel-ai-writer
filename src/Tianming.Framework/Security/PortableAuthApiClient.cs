using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TM.Framework.Profile;

namespace TM.Framework.Security;

public sealed class PortableRefreshTokenRequest
{
    [JsonPropertyName("refreshToken")] public string RefreshToken { get; set; } = string.Empty;
}

public sealed class PortableAuthApiClient :
    IPortableAccountDeletionApi,
    IPortableAccountLockoutApi,
    IPortableAccountRenewalApi,
    IPortableAuthStartupApi,
    IPortableFeatureAuthorizationApi,
    IPortableLoginApi,
    IPortableOAuthLoginApi,
    IPortablePasswordChangeApi,
    IPortableRegisterApi,
    IPortableUserProfileApi
{
    private const string RefreshPath = "/api/auth/refresh";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly PortableAuthTokenStore _tokenStore;
    private readonly string _userAgent;

    public PortableAuthApiClient(
        HttpClient httpClient,
        string baseUrl,
        PortableAuthTokenStore tokenStore,
        string userAgent)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _baseUrl = string.IsNullOrWhiteSpace(baseUrl)
            ? throw new ArgumentException("Base URL cannot be empty.", nameof(baseUrl))
            : baseUrl;
        _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        _userAgent = userAgent;
    }

    public async Task<PortableApiResponse<PortableLoginResult>> LoginAsync(
        PortableLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<PortableLoginResult>(
            HttpMethod.Post,
            PortableAuthApiProtocol.LoginPath,
            request,
            requiresAuth: false,
            requiresSign: false,
            allowRefresh: false,
            cancellationToken);
        if (response.Success && response.Data != null)
        {
            _tokenStore.SaveTokens(response.Data);
        }

        return response;
    }

    public async Task<PortableApiResponse<PortableRegisterResult>> RegisterAsync(
        PortableRegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<PortableRegisterResult>(
            HttpMethod.Post,
            PortableAuthApiProtocol.RegisterPath,
            request,
            requiresAuth: false,
            requiresSign: false,
            allowRefresh: false,
            cancellationToken);
        if (response.Success && response.Data != null)
        {
            _tokenStore.SaveTokens(response.Data);
        }

        return response;
    }

    public async Task<PortableApiResponse<PortableOAuthLoginResult>> OAuthLoginAsync(
        string platform,
        PortableOAuthRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<PortableOAuthLoginResult>(
            HttpMethod.Post,
            PortableAuthApiProtocol.GetOAuthLoginPath(platform),
            request,
            requiresAuth: false,
            requiresSign: false,
            allowRefresh: false,
            cancellationToken);
        if (response.Success && response.Data != null)
        {
            _tokenStore.SaveTokens(new PortableLoginResult
            {
                AccessToken = response.Data.AccessToken,
                RefreshToken = response.Data.RefreshToken,
                SessionKey = response.Data.SessionKey,
                ExpiresAt = response.Data.ExpiresAt,
                User = response.Data.User
            });
        }

        return response;
    }

    public Task<PortableApiResponse<PortableSubscriptionInfo>> GetSubscriptionAsync(
        CancellationToken cancellationToken = default)
    {
        return SendAsync<PortableSubscriptionInfo>(
            HttpMethod.Get,
            PortableSubscriptionApiProtocol.SubscriptionPath,
            body: null,
            requiresAuth: true,
            requiresSign: true,
            allowRefresh: true,
            cancellationToken);
    }

    public Task<PortableApiResponse<PortableActivationResult>> ActivateCardKeyAsync(
        string cardKey,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<PortableActivationResult>(
            HttpMethod.Post,
            PortableSubscriptionApiProtocol.ActivatePath,
            new PortableActivateCardKeyRequest { CardKey = cardKey },
            requiresAuth: true,
            requiresSign: true,
            allowRefresh: true,
            cancellationToken);
    }

    public Task<PortableApiResponse<PortableActivationHistoryResult>> GetActivationHistoryAsync(
        CancellationToken cancellationToken = default)
    {
        return SendAsync<PortableActivationHistoryResult>(
            HttpMethod.Get,
            PortableSubscriptionApiProtocol.HistoryPath,
            body: null,
            requiresAuth: true,
            requiresSign: true,
            allowRefresh: true,
            cancellationToken);
    }

    public Task<PortableApiResponse<PortableActivationResult>> RenewAccountWithCardKeyAsync(
        string account,
        string cardKey,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<PortableActivationResult>(
            HttpMethod.Post,
            PortableSubscriptionApiProtocol.RenewPath,
            new PortableRenewAccountRequest { Account = account, CardKey = cardKey },
            requiresAuth: true,
            requiresSign: true,
            allowRefresh: true,
            cancellationToken);
    }

    public Task<PortableApiResponse<object>> ChangePasswordAsync(
        string oldPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<object>(
            HttpMethod.Put,
            PortableAuthApiProtocol.AccountPasswordPath,
            new PortableChangePasswordRequest
            {
                OldPassword = oldPassword,
                NewPassword = newPassword
            },
            requiresAuth: true,
            requiresSign: true,
            allowRefresh: true,
            cancellationToken);
    }

    public Task<PortableApiResponse<PortableServerUserProfile>> GetProfileAsync(
        CancellationToken cancellationToken = default)
    {
        return SendAsync<PortableServerUserProfile>(
            HttpMethod.Get,
            PortableAuthApiProtocol.AccountProfilePath,
            body: null,
            requiresAuth: true,
            requiresSign: true,
            allowRefresh: true,
            cancellationToken);
    }

    public Task<PortableApiResponse<object>> UpdateProfileAsync(
        PortableServerUserProfile profile,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<object>(
            HttpMethod.Put,
            PortableAuthApiProtocol.AccountProfilePath,
            profile,
            requiresAuth: true,
            requiresSign: true,
            allowRefresh: true,
            cancellationToken);
    }

    public Task<PortableApiResponse<PortableAccountLockoutStatus>> GetLockoutStatusAsync(
        CancellationToken cancellationToken = default)
    {
        return SendAsync<PortableAccountLockoutStatus>(
            HttpMethod.Get,
            PortableAuthApiProtocol.AccountLockoutPath,
            body: null,
            requiresAuth: true,
            requiresSign: true,
            allowRefresh: true,
            cancellationToken);
    }

    public Task<PortableApiResponse<object>> UnlockAccountAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync<object>(
            HttpMethod.Post,
            PortableAuthApiProtocol.AccountUnlockPath,
            body: null,
            requiresAuth: true,
            requiresSign: true,
            allowRefresh: true,
            cancellationToken);
    }

    public Task<PortableApiResponse<PortableFeatureAuthorizationStatus>> CheckFeatureAuthorizationAsync(
        string featureId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_tokenStore.AccessToken) && !_tokenStore.HasRefreshToken)
        {
            return Task.FromResult(Fail<PortableFeatureAuthorizationStatus>(
                "未检测到登录状态",
                PortableApiErrorCodes.AuthInvalid));
        }

        return SendAsync<PortableFeatureAuthorizationStatus>(
            HttpMethod.Get,
            PortableAuthApiProtocol.GetFeatureAuthorizationPath(featureId),
            body: null,
            requiresAuth: true,
            requiresSign: true,
            allowRefresh: true,
            cancellationToken);
    }

    public Task<PortableApiResponse<PortableBindingsResult>> GetBindingsAsync(
        CancellationToken cancellationToken = default)
    {
        return SendAsync<PortableBindingsResult>(
            HttpMethod.Get,
            PortableAccountBindingApiProtocol.BindingsPath,
            body: null,
            requiresAuth: true,
            requiresSign: true,
            allowRefresh: true,
            cancellationToken);
    }

    public Task<PortableApiResponse<PortableBindingInfo>> BindAccountAsync(
        string platform,
        PortableOAuthRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<PortableBindingInfo>(
            HttpMethod.Post,
            PortableAccountBindingApiProtocol.GetPlatformBindingPath(platform),
            request,
            requiresAuth: true,
            requiresSign: true,
            allowRefresh: true,
            cancellationToken);
    }

    public Task<PortableApiResponse<object>> UnbindAccountAsync(
        string platform,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<object>(
            HttpMethod.Delete,
            PortableAccountBindingApiProtocol.GetPlatformBindingPath(platform),
            body: null,
            requiresAuth: true,
            requiresSign: true,
            allowRefresh: true,
            cancellationToken);
    }

    public Task<PortableApiResponse<PortableLoginHistoryResult>> GetLoginHistoryAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<PortableLoginHistoryResult>(
            HttpMethod.Get,
            PortableLoginHistoryApiProtocol.BuildLoginHistoryPath(page, pageSize),
            body: null,
            requiresAuth: true,
            requiresSign: true,
            allowRefresh: true,
            cancellationToken);
    }

    public Task<PortableApiResponse<PortableDeletionStatus>> RequestDeletionAsync(
        PortableDeletionRequest request,
        CancellationToken cancellationToken = default)
    {
        return SendAsync<PortableDeletionStatus>(
            HttpMethod.Post,
            PortableAccountDeletionApiProtocol.DeletionPath,
            request,
            requiresAuth: true,
            requiresSign: true,
            allowRefresh: true,
            cancellationToken);
    }

    public Task<PortableApiResponse<object>> CancelDeletionAsync(
        CancellationToken cancellationToken = default)
    {
        return SendAsync<object>(
            HttpMethod.Delete,
            PortableAccountDeletionApiProtocol.DeletionPath,
            body: null,
            requiresAuth: true,
            requiresSign: true,
            allowRefresh: true,
            cancellationToken);
    }

    public Task<PortableApiResponse<object>> LogoutAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync<object>(
            HttpMethod.Post,
            "/api/auth/logout",
            body: null,
            requiresAuth: true,
            requiresSign: true,
            allowRefresh: false,
            cancellationToken);
    }

    public async Task<PortableApiResponse<PortableRefreshTokenResult>> RefreshTokenAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await SendAsync<PortableRefreshTokenResult>(
            HttpMethod.Post,
            RefreshPath,
            new PortableRefreshTokenRequest { RefreshToken = _tokenStore.RefreshToken ?? string.Empty },
            requiresAuth: false,
            requiresSign: true,
            allowRefresh: false,
            cancellationToken);
        if (response.Success && response.Data != null)
        {
            _tokenStore.UpdateTokens(response.Data);
        }

        return response;
    }

    private async Task<PortableApiResponse<T>> SendAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        bool requiresAuth,
        bool requiresSign,
        bool allowRefresh,
        CancellationToken cancellationToken)
    {
        if (requiresAuth && allowRefresh && _tokenStore.IsAccessTokenExpired && _tokenStore.HasRefreshToken)
        {
            var refresh = await RefreshTokenAsync(cancellationToken);
            if (!refresh.Success)
            {
                return Fail<T>(refresh.Message ?? "登录已过期，请重新登录", refresh.ErrorCode);
            }
        }

        using var request = BuildRequest(method, path, body, requiresAuth, requiresSign);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var parsed = PortableAuthApiProtocol.ParseResponse<T>(responseJson);

        if (response.StatusCode == HttpStatusCode.Unauthorized && requiresAuth && allowRefresh && _tokenStore.HasRefreshToken)
        {
            var refresh = await RefreshTokenAsync(cancellationToken);
            if (refresh.Success)
            {
                return await SendAsync<T>(method, path, body, requiresAuth, requiresSign, allowRefresh: false, cancellationToken);
            }
        }

        return parsed;
    }

    private HttpRequestMessage BuildRequest(
        HttpMethod method,
        string path,
        object? body,
        bool requiresAuth,
        bool requiresSign)
    {
        var bodyJson = body == null ? null : JsonSerializer.Serialize(body, JsonOptions);
        var request = PortableAuthApiProtocol.BuildJsonRequest(
            method,
            _baseUrl,
            path,
            body,
            new PortableAuthApiRequestOptions
            {
                ClientId = _tokenStore.ClientId,
                UserAgent = _userAgent,
                RequiresAuth = requiresAuth,
                AccessToken = _tokenStore.AccessToken
            });

        if (requiresSign)
        {
            var signHeaders = _tokenStore.GenerateSignatureHeaders(method.Method, path, bodyJson);
            request.Headers.Add("X-Timestamp", signHeaders.Timestamp);
            request.Headers.Add("X-Nonce", signHeaders.Nonce);
            request.Headers.Add("X-Sign", signHeaders.Signature);
        }

        return request;
    }

    private static PortableApiResponse<T> Fail<T>(string message, string? errorCode)
    {
        return new PortableApiResponse<T>
        {
            Success = false,
            Message = message,
            ErrorCode = errorCode
        };
    }
}
