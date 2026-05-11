namespace TM.Framework.Security;

public enum PortableLoginField
{
    None,
    Username,
    Password
}

public sealed class PortableLoginRememberOptions
{
    public bool RememberAccount { get; init; }
    public bool RememberPassword { get; init; }
}

public sealed class PortableLoginControllerResult
{
    public bool Success { get; init; }
    public string? LoggedInUsername { get; init; }
    public string? Message { get; init; }
    public string? ErrorCode { get; init; }
    public bool ShouldClearPassword { get; init; }
    public PortableLoginField FocusField { get; init; } = PortableLoginField.None;
    public PortableAuthMessagePresentation Presentation { get; init; } = PortableAuthMessagePresentation.InlineError;
}

public interface IPortableLoginApi
{
    Task<PortableApiResponse<PortableLoginResult>> LoginAsync(
        PortableLoginRequest request,
        CancellationToken cancellationToken = default);
}

public interface IPortableLoginRememberStore
{
    void SaveRememberedAccount(
        string username,
        bool rememberAccount,
        bool rememberPassword,
        string? encryptedPassword);

    void ClearRememberedAccount();
}

public sealed class PortableLoginController
{
    private readonly IPortableLoginApi _api;
    private readonly IPortableLoginRememberStore _rememberStore;
    private readonly Func<string, string?> _protectPassword;

    public PortableLoginController(
        IPortableLoginApi api,
        IPortableLoginRememberStore rememberStore,
        Func<string, string?> protectPassword)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _rememberStore = rememberStore ?? throw new ArgumentNullException(nameof(rememberStore));
        _protectPassword = protectPassword ?? throw new ArgumentNullException(nameof(protectPassword));
    }

    public async Task<PortableLoginControllerResult> LoginAsync(
        string? username,
        string? password,
        PortableLoginRememberOptions rememberOptions,
        CancellationToken cancellationToken = default)
    {
        var normalizedUsername = (username ?? string.Empty).Trim();
        var normalizedPassword = password ?? string.Empty;
        rememberOptions ??= new PortableLoginRememberOptions();

        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            return Fail("请输入账号", PortableLoginField.Username);
        }

        if (string.IsNullOrWhiteSpace(normalizedPassword))
        {
            return Fail("请输入密码", PortableLoginField.Password);
        }

        try
        {
            var response = await _api.LoginAsync(
                new PortableLoginRequest
                {
                    Username = normalizedUsername,
                    Password = normalizedPassword
                },
                cancellationToken).ConfigureAwait(false);

            if (response.Success)
            {
                PersistRememberedAccount(normalizedUsername, normalizedPassword, rememberOptions);
                return new PortableLoginControllerResult
                {
                    Success = true,
                    LoggedInUsername = normalizedUsername
                };
            }

            var message = PortableAuthResultMessages.FromLoginResponse(response);
            return new PortableLoginControllerResult
            {
                Success = false,
                Message = message.ErrorMessage ?? "登录失败",
                ErrorCode = message.ErrorCode,
                ShouldClearPassword = true,
                FocusField = PortableLoginField.Password,
                Presentation = message.Presentation
            };
        }
        catch (Exception ex)
        {
            var message = PortableAuthResultMessages.FromException("登录失败", ex);
            return Fail(message.ErrorMessage ?? "登录失败", PortableLoginField.None, message.ErrorCode);
        }
    }

    private void PersistRememberedAccount(
        string username,
        string password,
        PortableLoginRememberOptions rememberOptions)
    {
        if (!rememberOptions.RememberAccount)
        {
            _rememberStore.ClearRememberedAccount();
            return;
        }

        string? encryptedPassword = null;
        if (rememberOptions.RememberPassword)
        {
            try
            {
                encryptedPassword = _protectPassword(password);
            }
            catch
            {
                encryptedPassword = null;
            }
        }

        _rememberStore.SaveRememberedAccount(
            username,
            rememberAccount: true,
            rememberPassword: rememberOptions.RememberPassword,
            encryptedPassword);
    }

    private static PortableLoginControllerResult Fail(
        string message,
        PortableLoginField focusField,
        string? errorCode = null)
    {
        return new PortableLoginControllerResult
        {
            Success = false,
            Message = message,
            ErrorCode = errorCode,
            FocusField = focusField
        };
    }
}
