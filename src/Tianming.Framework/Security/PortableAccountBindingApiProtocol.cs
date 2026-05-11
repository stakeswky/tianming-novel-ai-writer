using System.Text.Json.Serialization;

namespace TM.Framework.Security;

public sealed class PortableBindingInfo
{
    [JsonPropertyName("bindingId")] public long BindingId { get; set; }
    [JsonPropertyName("platform")] public string Platform { get; set; } = string.Empty;
    [JsonPropertyName("platformUserId")] public string PlatformUserId { get; set; } = string.Empty;
    [JsonPropertyName("displayName")] public string? DisplayName { get; set; }
    [JsonPropertyName("boundTime")] public DateTime BoundTime { get; set; }
}

public sealed class PortableBindingsResult
{
    [JsonPropertyName("bindings")] public List<PortableBindingInfo> Bindings { get; set; } = new();
}

public static class PortableAccountBindingApiProtocol
{
    public const string BindingsPath = "/api/account/bindings";

    public static string GetPlatformBindingPath(string platform)
    {
        return $"{BindingsPath}/{Uri.EscapeDataString(platform)}";
    }

    public static HttpRequestMessage BuildGetBindingsRequest(
        string baseUrl,
        PortableAuthApiRequestOptions options)
    {
        return PortableAuthApiProtocol.BuildJsonRequest(HttpMethod.Get, baseUrl, BindingsPath, body: null, options);
    }

    public static HttpRequestMessage BuildBindAccountRequest(
        string baseUrl,
        string platform,
        PortableOAuthRequest request,
        PortableAuthApiRequestOptions options)
    {
        return PortableAuthApiProtocol.BuildJsonRequest(
            HttpMethod.Post,
            baseUrl,
            GetPlatformBindingPath(platform),
            request,
            options);
    }

    public static HttpRequestMessage BuildUnbindAccountRequest(
        string baseUrl,
        string platform,
        PortableAuthApiRequestOptions options)
    {
        return PortableAuthApiProtocol.BuildJsonRequest(
            HttpMethod.Delete,
            baseUrl,
            GetPlatformBindingPath(platform),
            body: null,
            options);
    }

    public static PortableApiResponse<T> ParseResponse<T>(string json)
    {
        return PortableAuthApiProtocol.ParseResponse<T>(json);
    }
}
