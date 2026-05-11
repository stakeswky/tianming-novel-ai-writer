using System.Text.Json.Serialization;

namespace TM.Framework.Security;

public sealed class PortableActivateCardKeyRequest
{
    [JsonPropertyName("cardKey")] public string CardKey { get; set; } = string.Empty;
}

public sealed class PortableRenewAccountRequest
{
    [JsonPropertyName("account")] public string Account { get; set; } = string.Empty;
    [JsonPropertyName("cardKey")] public string CardKey { get; set; } = string.Empty;
}

public sealed class PortableSubscriptionInfo
{
    [JsonPropertyName("subscriptionId")] public int? SubscriptionId { get; set; }
    [JsonPropertyName("userId")] public string UserId { get; set; } = string.Empty;
    [JsonPropertyName("planType")] public string? PlanType { get; set; } = "free";
    [JsonPropertyName("startTime")] public DateTime? StartTime { get; set; }
    [JsonPropertyName("endTime")] public DateTime? EndTime { get; set; }
    [JsonPropertyName("isActive")] public bool IsActive { get; set; }
    [JsonPropertyName("remainingDays")] public int RemainingDays { get; set; }
    [JsonPropertyName("source")] public string? Source { get; set; } = string.Empty;
}

public sealed class PortableActivationResult
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("daysAdded")] public int DaysAdded { get; set; }
    [JsonPropertyName("newExpireTime")] public DateTime NewExpireTime { get; set; }
    [JsonPropertyName("subscription")] public PortableSubscriptionInfo Subscription { get; set; } = new();
}

public sealed class PortableActivationHistoryItem
{
    [JsonPropertyName("cardKey")] public string CardKey { get; set; } = string.Empty;
    [JsonPropertyName("durationDays")] public int DurationDays { get; set; }
    [JsonPropertyName("activatedTime")] public DateTime ActivatedTime { get; set; }
}

public sealed class PortableActivationHistoryResult
{
    [JsonPropertyName("records")] public List<PortableActivationHistoryItem> Records { get; set; } = new();
}

public static class PortableSubscriptionApiProtocol
{
    public const string SubscriptionPath = "/api/subscription";
    public const string ActivatePath = "/api/subscription/activate";
    public const string HistoryPath = "/api/subscription/history";
    public const string RenewPath = "/api/subscription/renew";

    public static HttpRequestMessage BuildGetSubscriptionRequest(
        string baseUrl,
        PortableAuthApiRequestOptions options)
    {
        return PortableAuthApiProtocol.BuildJsonRequest(HttpMethod.Get, baseUrl, SubscriptionPath, body: null, options);
    }

    public static HttpRequestMessage BuildActivateCardKeyRequest(
        string baseUrl,
        PortableActivateCardKeyRequest request,
        PortableAuthApiRequestOptions options)
    {
        return PortableAuthApiProtocol.BuildJsonRequest(HttpMethod.Post, baseUrl, ActivatePath, request, options);
    }

    public static HttpRequestMessage BuildActivationHistoryRequest(
        string baseUrl,
        PortableAuthApiRequestOptions options)
    {
        return PortableAuthApiProtocol.BuildJsonRequest(HttpMethod.Get, baseUrl, HistoryPath, body: null, options);
    }

    public static HttpRequestMessage BuildRenewAccountRequest(
        string baseUrl,
        PortableRenewAccountRequest request,
        PortableAuthApiRequestOptions options)
    {
        return PortableAuthApiProtocol.BuildJsonRequest(HttpMethod.Post, baseUrl, RenewPath, request, options);
    }

    public static PortableApiResponse<T> ParseResponse<T>(string json)
    {
        return PortableAuthApiProtocol.ParseResponse<T>(json);
    }
}
