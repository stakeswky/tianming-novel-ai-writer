namespace TM.Framework.Security;

public enum PortableRegisterField
{
    None,
    Username,
    Password,
    ConfirmPassword,
    CardKey
}

public sealed class PortableRegisterResultView
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? ErrorCode { get; init; }
    public PortableRegisterField FocusField { get; init; } = PortableRegisterField.None;
    public string? RegisteredUsername { get; init; }
    public string? RegisteredPassword { get; init; }
}

public interface IPortableRegisterApi
{
    Task<PortableApiResponse<PortableRegisterResult>> RegisterAsync(
        PortableRegisterRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class PortableRegisterController
{
    private readonly IPortableRegisterApi _api;

    public PortableRegisterController(IPortableRegisterApi api)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
    }

    public async Task<PortableRegisterResultView> SubmitAsync(
        string? username,
        string? password,
        string? confirmPassword,
        string? cardKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedUsername = (username ?? string.Empty).Trim();
        var normalizedPassword = password ?? string.Empty;
        var normalizedConfirmPassword = confirmPassword ?? string.Empty;
        var normalizedCardKey = (cardKey ?? string.Empty).Trim();

        var validation = Validate(
            normalizedUsername,
            normalizedPassword,
            normalizedConfirmPassword,
            normalizedCardKey);
        if (validation != null)
        {
            return validation;
        }

        try
        {
            var response = await _api.RegisterAsync(
                new PortableRegisterRequest
                {
                    Username = normalizedUsername,
                    Password = normalizedPassword,
                    CardKey = normalizedCardKey
                },
                cancellationToken).ConfigureAwait(false);

            var message = PortableAuthResultMessages.FromRegisterResponse(response);
            if (!message.Success)
            {
                return Fail(
                    message.ErrorMessage ?? "注册失败",
                    PortableRegisterField.None,
                    message.ErrorCode);
            }

            return new PortableRegisterResultView
            {
                Success = true,
                RegisteredUsername = normalizedUsername,
                RegisteredPassword = normalizedPassword
            };
        }
        catch (Exception ex)
        {
            var message = PortableAuthResultMessages.FromException("注册失败", ex);
            return Fail(message.ErrorMessage ?? "注册失败", PortableRegisterField.None, message.ErrorCode);
        }
    }

    private static PortableRegisterResultView? Validate(
        string username,
        string password,
        string confirmPassword,
        string cardKey)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return Fail("请输入账号", PortableRegisterField.Username);
        }

        if (username.Length < 3)
        {
            return Fail("账号至少3个字符", PortableRegisterField.Username);
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return Fail("请输入密码", PortableRegisterField.Password);
        }

        if (password.Length < 6)
        {
            return Fail("密码至少6位", PortableRegisterField.Password);
        }

        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            return Fail("两次输入的密码不一致", PortableRegisterField.ConfirmPassword);
        }

        if (string.IsNullOrWhiteSpace(cardKey))
        {
            return Fail("请输入卡密", PortableRegisterField.CardKey);
        }

        return null;
    }

    private static PortableRegisterResultView Fail(
        string message,
        PortableRegisterField focusField,
        string? errorCode = null)
    {
        return new PortableRegisterResultView
        {
            Success = false,
            Message = message,
            ErrorCode = errorCode,
            FocusField = focusField
        };
    }
}
