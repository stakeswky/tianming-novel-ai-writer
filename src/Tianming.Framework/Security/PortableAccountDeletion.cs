using System.Text.Json.Serialization;

namespace TM.Framework.Security;

public sealed class PortableDeletionRequest
{
    [JsonPropertyName("reasons")] public List<string> Reasons { get; set; } = new();
    [JsonPropertyName("customFeedback")] public string? CustomFeedback { get; set; }
    [JsonPropertyName("retainLoginHistory")] public bool RetainLoginHistory { get; set; }
    [JsonPropertyName("retainThemes")] public bool RetainThemes { get; set; }
    [JsonPropertyName("retainSettings")] public bool RetainSettings { get; set; }

    public static PortableDeletionRequest CreateUserInitiated()
    {
        return new PortableDeletionRequest
        {
            Reasons = new List<string> { "用户主动注销" },
            CustomFeedback = string.Empty,
            RetainLoginHistory = false,
            RetainThemes = false,
            RetainSettings = false
        };
    }
}

public sealed class PortableDeletionStatus
{
    [JsonPropertyName("requestId")] public string RequestId { get; set; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("requestTime")] public DateTime RequestTime { get; set; }
    [JsonPropertyName("scheduledDeleteTime")] public DateTime ScheduledDeleteTime { get; set; }
    [JsonPropertyName("remainingDays")] public int RemainingDays { get; set; }
}

public sealed class PortableAccountDeletionResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public PortableDeletionStatus? Status { get; init; }

    public static PortableAccountDeletionResult Ok(PortableDeletionStatus? status)
    {
        return new PortableAccountDeletionResult
        {
            Success = true,
            Message = "账号注销成功",
            Status = status
        };
    }

    public static PortableAccountDeletionResult Fail(string message)
    {
        return new PortableAccountDeletionResult
        {
            Success = false,
            Message = message
        };
    }
}

public interface IPortableAccountDeletionApi
{
    Task<PortableApiResponse<PortableDeletionStatus>> RequestDeletionAsync(
        PortableDeletionRequest request,
        CancellationToken cancellationToken = default);
}

public static class PortableAccountDeletionApiProtocol
{
    public const string DeletionPath = "/api/account/deletion";

    public static HttpRequestMessage BuildRequestDeletionRequest(
        string baseUrl,
        PortableDeletionRequest request,
        PortableAuthApiRequestOptions options)
    {
        return PortableAuthApiProtocol.BuildJsonRequest(HttpMethod.Post, baseUrl, DeletionPath, request, options);
    }

    public static HttpRequestMessage BuildCancelDeletionRequest(
        string baseUrl,
        PortableAuthApiRequestOptions options)
    {
        return PortableAuthApiProtocol.BuildJsonRequest(HttpMethod.Delete, baseUrl, DeletionPath, body: null, options);
    }
}

public sealed class PortableAccountDeletionController
{
    public const string RequiredConfirmation = "确认注销";

    private readonly IPortableAccountDeletionApi _api;
    private readonly PortableAuthTokenStore _tokenStore;

    public PortableAccountDeletionController(IPortableAccountDeletionApi api, PortableAuthTokenStore tokenStore)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
    }

    public async Task<PortableAccountDeletionResult> ConfirmDeletionAsync(
        string confirmationText,
        bool userConfirmed,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(confirmationText, RequiredConfirmation, StringComparison.Ordinal))
        {
            return PortableAccountDeletionResult.Fail("请输入「确认注销」");
        }

        if (!userConfirmed)
        {
            return PortableAccountDeletionResult.Fail("注销操作已取消");
        }

        var response = await _api.RequestDeletionAsync(PortableDeletionRequest.CreateUserInitiated(), cancellationToken);
        if (!response.Success)
        {
            return PortableAccountDeletionResult.Fail(response.Message ?? "请稍后重试");
        }

        _tokenStore.ClearTokens();
        return PortableAccountDeletionResult.Ok(response.Data);
    }
}
