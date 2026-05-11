using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.Security;

public sealed class PortableAuthApiRequestOptions
{
    public string ClientId { get; init; } = string.Empty;
    public string UserAgent { get; init; } = string.Empty;
    public bool RequiresAuth { get; init; }
    public string? AccessToken { get; init; }
}

public sealed class PortableLoginRequest
{
    [JsonPropertyName("username")] public string Username { get; set; } = string.Empty;
    [JsonPropertyName("password")] public string Password { get; set; } = string.Empty;
}

public sealed class PortableRegisterRequest
{
    [JsonPropertyName("username")] public string Username { get; set; } = string.Empty;
    [JsonPropertyName("password")] public string Password { get; set; } = string.Empty;
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("cardKey")] public string CardKey { get; set; } = string.Empty;
}

public sealed class PortableOAuthRequest
{
    [JsonPropertyName("platform")] public string Platform { get; set; } = string.Empty;
    [JsonPropertyName("code")] public string Code { get; set; } = string.Empty;
    [JsonPropertyName("state")] public string? State { get; set; }
}

public sealed class PortableChangePasswordRequest
{
    [JsonPropertyName("oldPassword")] public string OldPassword { get; set; } = string.Empty;
    [JsonPropertyName("newPassword")] public string NewPassword { get; set; } = string.Empty;
}

public sealed class PortableLoginResult
{
    [JsonPropertyName("accessToken")] public string AccessToken { get; set; } = string.Empty;
    [JsonPropertyName("refreshToken")] public string RefreshToken { get; set; } = string.Empty;
    [JsonPropertyName("sessionKey")] public string SessionKey { get; set; } = string.Empty;
    [JsonPropertyName("expiresAt")] public DateTime ExpiresAt { get; set; }
    [JsonPropertyName("user")] public PortableUserInfo User { get; set; } = new();
}

public sealed class PortableRegisterResult
{
    [JsonPropertyName("userId")] public string UserId { get; set; } = string.Empty;
    [JsonPropertyName("username")] public string Username { get; set; } = string.Empty;
    [JsonPropertyName("accessToken")] public string AccessToken { get; set; } = string.Empty;
    [JsonPropertyName("refreshToken")] public string RefreshToken { get; set; } = string.Empty;
    [JsonPropertyName("sessionKey")] public string SessionKey { get; set; } = string.Empty;
    [JsonPropertyName("expiresAt")] public DateTime ExpiresAt { get; set; }
}

public sealed class PortableOAuthLoginResult
{
    [JsonPropertyName("isNewUser")] public bool IsNewUser { get; set; }
    [JsonPropertyName("accessToken")] public string AccessToken { get; set; } = string.Empty;
    [JsonPropertyName("refreshToken")] public string RefreshToken { get; set; } = string.Empty;
    [JsonPropertyName("sessionKey")] public string SessionKey { get; set; } = string.Empty;
    [JsonPropertyName("expiresAt")] public DateTime ExpiresAt { get; set; }
    [JsonPropertyName("user")] public PortableUserInfo User { get; set; } = new();
}

public sealed class PortableUserInfo
{
    [JsonPropertyName("userId")] public string UserId { get; set; } = string.Empty;
    [JsonPropertyName("username")] public string Username { get; set; } = string.Empty;
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("displayName")] public string? DisplayName { get; set; }
    [JsonPropertyName("avatarUrl")] public string? AvatarUrl { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = "active";
}

public sealed class PortableApiResponse<T>
{
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("errorCode")] public string? ErrorCode { get; set; }
    [JsonPropertyName("traceId")] public string? TraceId { get; set; }
    [JsonPropertyName("serverTime")] public DateTime ServerTime { get; set; }
    [JsonPropertyName("data")] public T? Data { get; set; }
}

public static class PortableAuthApiProtocol
{
    public const string LoginPath = "/api/auth/login";
    public const string RegisterPath = "/api/auth/register";
    public const string AccountPasswordPath = "/api/account/password";
    public const string AccountProfilePath = "/api/account/profile";
    public const string AccountLockoutPath = "/api/account/lockout";
    public const string AccountUnlockPath = "/api/account/unlock";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string GetOAuthLoginPath(string platform)
    {
        return $"/api/auth/oauth/{Uri.EscapeDataString(platform)}";
    }

    public static string GetFeatureAuthorizationPath(string featureId)
    {
        return $"/api/auth/feature/{Uri.EscapeDataString(featureId)}";
    }

    public static HttpRequestMessage BuildJsonRequest(
        HttpMethod method,
        string baseUrl,
        string path,
        object? body,
        PortableAuthApiRequestOptions options)
    {
        var request = new HttpRequestMessage(method, CombineUrl(baseUrl, path));
        if (!string.IsNullOrWhiteSpace(options.ClientId))
        {
            request.Headers.Add("X-Client-Id", options.ClientId);
        }

        if (!string.IsNullOrWhiteSpace(options.UserAgent))
        {
            request.Headers.UserAgent.ParseAdd(options.UserAgent);
        }

        if (options.RequiresAuth && !string.IsNullOrWhiteSpace(options.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);
        }

        if (body != null)
        {
            var bodyJson = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
        }

        return request;
    }

    public static PortableApiResponse<T> ParseResponse<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<PortableApiResponse<T>>(json, JsonOptions)
                   ?? Fail<T>("响应解析失败");
        }
        catch (JsonException ex)
        {
            return Fail<T>($"响应解析失败: {ex.Message}");
        }
    }

    private static PortableApiResponse<T> Fail<T>(string message)
    {
        return new PortableApiResponse<T>
        {
            Success = false,
            Message = message
        };
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        return baseUrl.TrimEnd('/') + (path.StartsWith('/') ? path : "/" + path);
    }
}
