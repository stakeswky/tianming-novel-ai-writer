namespace TM.Framework.Security;

public static class PortableApiErrorCodes
{
    public const string AuthInvalid = "AUTH_INVALID";
    public const string AuthExpired = "AUTH_EXPIRED";
    public const string AuthDeviceKicked = "AUTH_DEVICE_KICKED";
    public const string AccountLocked = "ACCOUNT_LOCKED";
    public const string AccountDisabled = "ACCOUNT_DISABLED";
    public const string UsernameExists = "USERNAME_EXISTS";
    public const string SubscriptionNone = "SUBSCRIPTION_NONE";
    public const string SubscriptionExpired = "SUBSCRIPTION_EXPIRED";
    public const string CardKeyInvalid = "CARDKEY_INVALID";
    public const string CardKeyUsed = "CARDKEY_USED";
    public const string NetworkError = "NETWORK_ERROR";
    public const string ServerError = "SERVER_ERROR";
    public const string InvalidRequest = "INVALID_REQUEST";
}

public sealed class PortableCardKeyActivationMessage
{
    public bool Success { get; set; }
    public int DaysAdded { get; set; }
    public DateTime NewExpireTime { get; set; }
    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorCode { get; set; }
}

public static class PortableCardKeyActivationMessages
{
    public static PortableCardKeyActivationMessage FromBlankCardKey()
    {
        return new PortableCardKeyActivationMessage
        {
            Success = false,
            ErrorMessage = "请输入卡密"
        };
    }

    public static PortableCardKeyActivationMessage FromActivationResponse(
        PortableApiResponse<PortableActivationResult> response)
    {
        if (response.Success && response.Data != null)
        {
            return new PortableCardKeyActivationMessage
            {
                Success = true,
                DaysAdded = response.Data.DaysAdded,
                NewExpireTime = response.Data.NewExpireTime,
                Message = $"续费成功！已增加{response.Data.DaysAdded}天会员时长"
            };
        }

        return new PortableCardKeyActivationMessage
        {
            Success = false,
            ErrorMessage = LocalizeError(response.ErrorCode, response.Message),
            ErrorCode = response.ErrorCode
        };
    }

    public static PortableCardKeyActivationMessage FromException(Exception exception)
    {
        return new PortableCardKeyActivationMessage
        {
            Success = false,
            ErrorMessage = $"续费失败: {exception.Message}"
        };
    }

    private static string LocalizeError(string? errorCode, string? message)
    {
        return errorCode switch
        {
            PortableApiErrorCodes.CardKeyInvalid => "卡密无效或已过期",
            PortableApiErrorCodes.CardKeyUsed => "该卡密已被使用",
            PortableApiErrorCodes.NetworkError => "网络连接失败，请检查网络后重试",
            _ => string.IsNullOrWhiteSpace(message) ? "续费失败" : message
        };
    }
}
