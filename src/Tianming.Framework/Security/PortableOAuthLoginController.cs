namespace TM.Framework.Security;

public enum PortableOAuthLoginPresentation
{
    InlineError,
    InfoDialog,
    WarningDialog
}

public sealed class PortableOAuthLoginControllerResult
{
    public bool Success { get; init; }
    public string? LoggedInUsername { get; init; }
    public string? Message { get; init; }
    public string Platform { get; init; } = string.Empty;
    public PortableOAuthLoginPresentation Presentation { get; init; } = PortableOAuthLoginPresentation.InlineError;
}

public interface IPortableOAuthLoginApi
{
    Task<PortableApiResponse<PortableOAuthLoginResult>> OAuthLoginAsync(
        string platform,
        PortableOAuthRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class PortableOAuthLoginController
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

    private readonly IPortableOAuthAuthorizationFlow _authorizationFlow;
    private readonly IPortableOAuthLoginApi _api;
    private readonly Func<string, bool> _isPlatformBound;
    private readonly Func<string, bool> _isPlatformConfigured;

    public PortableOAuthLoginController(
        IPortableOAuthAuthorizationFlow authorizationFlow,
        IPortableOAuthLoginApi api,
        Func<string, bool> isPlatformBound,
        Func<string, bool> isPlatformConfigured)
    {
        _authorizationFlow = authorizationFlow ?? throw new ArgumentNullException(nameof(authorizationFlow));
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _isPlatformBound = isPlatformBound ?? throw new ArgumentNullException(nameof(isPlatformBound));
        _isPlatformConfigured = isPlatformConfigured ?? throw new ArgumentNullException(nameof(isPlatformConfigured));
    }

    public async Task<PortableOAuthLoginControllerResult> LoginAsync(
        string platform,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPlatform = (platform ?? string.Empty).Trim();
        var platformName = GetPlatformDisplayName(normalizedPlatform);

        try
        {
            if (!_isPlatformBound(normalizedPlatform))
            {
                return Fail(
                    $"您尚未绑定{platformName}账号。\n\n请先登录，然后在【账号绑定】中完成绑定后再使用第三方登录。",
                    normalizedPlatform,
                    PortableOAuthLoginPresentation.InfoDialog);
            }

            if (!_isPlatformConfigured(normalizedPlatform))
            {
                return Fail(
                    $"{platformName}登录尚未配置，请联系管理员。",
                    normalizedPlatform,
                    PortableOAuthLoginPresentation.WarningDialog);
            }

            var authorization = await _authorizationFlow.StartAuthorizationAsync(
                normalizedPlatform,
                timeout ?? DefaultTimeout,
                cancellationToken).ConfigureAwait(false);
            if (!authorization.Success)
            {
                return Fail(
                    string.IsNullOrWhiteSpace(authorization.ErrorMessage) ? "授权失败" : authorization.ErrorMessage,
                    normalizedPlatform);
            }

            var response = await _api.OAuthLoginAsync(
                normalizedPlatform,
                new PortableOAuthRequest
                {
                    Platform = normalizedPlatform,
                    Code = authorization.Code ?? string.Empty,
                    State = authorization.State
                },
                cancellationToken).ConfigureAwait(false);

            if (response.Success && response.Data != null)
            {
                return new PortableOAuthLoginControllerResult
                {
                    Success = true,
                    Platform = normalizedPlatform,
                    LoggedInUsername = response.Data.User.Username
                };
            }

            return Fail(
                string.IsNullOrWhiteSpace(response.Message) ? $"{normalizedPlatform}登录失败" : response.Message,
                normalizedPlatform);
        }
        catch (Exception ex)
        {
            return Fail($"登录失败: {ex.Message}", normalizedPlatform);
        }
    }

    public static string GetPlatformDisplayName(string platform)
    {
        return (platform ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "wechat" => "微信",
            "qq" => "QQ",
            "github" => "GitHub",
            "google" => "Google",
            "microsoft" => "Microsoft",
            "baidu" => "百度",
            _ => platform ?? string.Empty
        };
    }

    private static PortableOAuthLoginControllerResult Fail(
        string message,
        string platform,
        PortableOAuthLoginPresentation presentation = PortableOAuthLoginPresentation.InlineError)
    {
        return new PortableOAuthLoginControllerResult
        {
            Success = false,
            Message = message,
            Platform = platform,
            Presentation = presentation
        };
    }
}
