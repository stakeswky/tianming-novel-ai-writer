namespace TM.Framework.Security;

public enum PortableAuthMessagePresentation
{
    InlineError,
    WarningDialog
}

public sealed class PortableAuthResultMessage
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }
    public PortableAuthMessagePresentation Presentation { get; init; } = PortableAuthMessagePresentation.InlineError;
}

public static class PortableAuthResultMessages
{
    public static PortableAuthResultMessage ValidateLoginInput(string? username, string? password)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return Fail("请输入用户名");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return Fail("请输入密码");
        }

        return Success();
    }

    public static PortableAuthResultMessage ValidateRegisterInput(
        string? username,
        string? password,
        string? cardKey)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return Fail("用户名不能为空");
        }

        if (username.Length < 3)
        {
            return Fail("用户名至少3个字符");
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return Fail("密码不能为空");
        }

        if (password.Length < 6)
        {
            return Fail("密码至少6个字符");
        }

        if (string.IsNullOrWhiteSpace(cardKey))
        {
            return Fail("请输入卡密");
        }

        return Success();
    }

    public static PortableAuthResultMessage FromLoginResponse(
        PortableApiResponse<PortableLoginResult> response)
    {
        if (response.Success)
        {
            return Success();
        }

        return response.ErrorCode switch
        {
            PortableApiErrorCodes.AuthDeviceKicked => Fail(
                "您的账号已在其他设备登录，当前会话已失效",
                response.ErrorCode),
            PortableApiErrorCodes.SubscriptionNone => Fail(
                "账号未激活\n\n请先使用卡密激活后再登录。\n您可以点击「更多选项」→「账号续费」使用卡密激活。",
                response.ErrorCode,
                PortableAuthMessagePresentation.WarningDialog),
            PortableApiErrorCodes.SubscriptionExpired => Fail(
                $"{Fallback(response.Message, "订阅已过期")}\n\n请点击「更多选项」→「账号续费」使用卡密续费。",
                response.ErrorCode,
                PortableAuthMessagePresentation.WarningDialog),
            PortableApiErrorCodes.NetworkError => Fail("网络连接失败，请检查网络后重试", response.ErrorCode),
            _ => Fail(Fallback(response.Message, "登录失败"), response.ErrorCode)
        };
    }

    public static PortableAuthResultMessage FromRegisterResponse(
        PortableApiResponse<PortableRegisterResult> response)
    {
        if (response.Success)
        {
            return Success();
        }

        var message = response.ErrorCode switch
        {
            PortableApiErrorCodes.NetworkError => "网络连接失败，请检查网络后重试",
            PortableApiErrorCodes.UsernameExists => "用户名已存在",
            _ => Fallback(response.Message, "注册失败")
        };
        return Fail(message, response.ErrorCode);
    }

    public static PortableAuthResultMessage FromException(string operation, Exception exception)
    {
        return Fail($"{operation}: {exception.Message}");
    }

    private static PortableAuthResultMessage Success()
    {
        return new PortableAuthResultMessage { Success = true };
    }

    private static PortableAuthResultMessage Fail(
        string message,
        string? errorCode = null,
        PortableAuthMessagePresentation presentation = PortableAuthMessagePresentation.InlineError)
    {
        return new PortableAuthResultMessage
        {
            Success = false,
            ErrorMessage = message,
            ErrorCode = errorCode,
            Presentation = presentation
        };
    }

    private static string Fallback(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
