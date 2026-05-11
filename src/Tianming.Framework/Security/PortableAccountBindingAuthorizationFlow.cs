namespace TM.Framework.Security;

public sealed class PortableAccountBindingAuthorizationResult
{
    public bool Success { get; set; }
    public string Platform { get; set; } = string.Empty;
    public PortableBindingInfo? Binding { get; set; }
    public string? ErrorMessage { get; set; }
}

public interface IPortableAccountBindingApiClient
{
    Task<PortableApiResponse<PortableBindingInfo>> BindAccountAsync(
        string platform,
        PortableOAuthRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class PortableAuthApiClientAccountBindingAdapter : IPortableAccountBindingApiClient
{
    private readonly PortableAuthApiClient _apiClient;

    public PortableAuthApiClientAccountBindingAdapter(PortableAuthApiClient apiClient)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    }

    public Task<PortableApiResponse<PortableBindingInfo>> BindAccountAsync(
        string platform,
        PortableOAuthRequest request,
        CancellationToken cancellationToken = default)
    {
        return _apiClient.BindAccountAsync(platform, request, cancellationToken);
    }
}

public sealed class PortableAccountBindingAuthorizationFlow
{
    private readonly IPortableOAuthAuthorizationFlow _authorizationFlow;
    private readonly IPortableAccountBindingApiClient _apiClient;
    private readonly PortableAccountBindingStore _bindingStore;

    public PortableAccountBindingAuthorizationFlow(
        IPortableOAuthAuthorizationFlow authorizationFlow,
        IPortableAccountBindingApiClient apiClient,
        PortableAccountBindingStore bindingStore)
    {
        _authorizationFlow = authorizationFlow ?? throw new ArgumentNullException(nameof(authorizationFlow));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _bindingStore = bindingStore ?? throw new ArgumentNullException(nameof(bindingStore));
    }

    public async Task<PortableAccountBindingAuthorizationResult> BindPlatformAsync(
        string platform,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var authorization = await _authorizationFlow.StartAuthorizationAsync(platform, timeout, cancellationToken);
        if (!authorization.Success)
        {
            return new PortableAccountBindingAuthorizationResult
            {
                Success = false,
                Platform = platform,
                ErrorMessage = authorization.ErrorMessage
            };
        }

        var request = new PortableOAuthRequest
        {
            Platform = platform,
            Code = authorization.Code,
            State = authorization.State
        };
        var response = await _apiClient.BindAccountAsync(platform, request, cancellationToken);
        if (!response.Success || response.Data == null)
        {
            return new PortableAccountBindingAuthorizationResult
            {
                Success = false,
                Platform = platform,
                ErrorMessage = GetErrorMessage(response)
            };
        }

        var binding = response.Data;
        if (TryParsePlatform(string.IsNullOrWhiteSpace(binding.Platform) ? platform : binding.Platform, out var bindingPlatform))
        {
            _bindingStore.BindAccount(
                bindingPlatform,
                binding.PlatformUserId,
                binding.DisplayName ?? string.Empty);
        }

        return new PortableAccountBindingAuthorizationResult
        {
            Success = true,
            Platform = platform,
            Binding = binding
        };
    }

    private static string GetErrorMessage(PortableApiResponse<PortableBindingInfo> response)
    {
        if (!string.IsNullOrWhiteSpace(response.Message))
        {
            return response.Message;
        }

        if (!string.IsNullOrWhiteSpace(response.ErrorCode))
        {
            return response.ErrorCode;
        }

        return "账号绑定失败";
    }

    private static bool TryParsePlatform(string platform, out PortableBindingPlatform result)
    {
        switch (platform.Trim().ToLowerInvariant())
        {
            case "wechat":
                result = PortableBindingPlatform.WeChat;
                return true;
            case "qq":
                result = PortableBindingPlatform.QQ;
                return true;
            case "github":
                result = PortableBindingPlatform.GitHub;
                return true;
            case "google":
                result = PortableBindingPlatform.Google;
                return true;
            case "microsoft":
                result = PortableBindingPlatform.Microsoft;
                return true;
            case "baidu":
                result = PortableBindingPlatform.Baidu;
                return true;
            case "weibo":
                result = PortableBindingPlatform.Weibo;
                return true;
            case "twitter":
                result = PortableBindingPlatform.Twitter;
                return true;
            default:
                result = default;
                return false;
        }
    }
}
