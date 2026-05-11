namespace TM.Framework.Security;

public sealed class PortableOAuthProviderConfig
{
    public string AuthUrl { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
}

public sealed class PortableOAuthAuthorizationResult
{
    public bool Success { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string? AuthorizationUrl { get; set; }
    public string? ErrorMessage { get; set; }
}

public static class PortableOAuthAuthorizationCore
{
    public static string GetCallbackUrl(int callbackPort)
    {
        return $"http://localhost:{callbackPort}/oauth/callback";
    }

    public static Dictionary<string, PortableOAuthProviderConfig> CreateDefaultConfigs()
    {
        return new Dictionary<string, PortableOAuthProviderConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["github"] = new()
            {
                AuthUrl = "https://github.com/login/oauth/authorize",
                ClientId = string.Empty,
                Scope = "user:email"
            },
            ["google"] = new()
            {
                AuthUrl = "https://accounts.google.com/o/oauth2/v2/auth",
                ClientId = string.Empty,
                Scope = "openid email profile"
            },
            ["microsoft"] = new()
            {
                AuthUrl = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize",
                ClientId = string.Empty,
                Scope = "openid email profile"
            },
            ["wechat"] = new()
            {
                AuthUrl = "https://open.weixin.qq.com/connect/qrconnect",
                ClientId = string.Empty,
                Scope = "snsapi_login"
            },
            ["qq"] = new()
            {
                AuthUrl = "https://graph.qq.com/oauth2.0/authorize",
                ClientId = string.Empty,
                Scope = "get_user_info"
            },
            ["baidu"] = new()
            {
                AuthUrl = "https://openapi.baidu.com/oauth/2.0/authorize",
                ClientId = string.Empty,
                Scope = "basic"
            }
        };
    }

    public static PortableOAuthAuthorizationResult PrepareAuthorization(
        string platform,
        IReadOnlyDictionary<string, PortableOAuthProviderConfig> configs,
        string state,
        int callbackPort)
    {
        var platformLower = platform.ToLowerInvariant();
        if (!configs.TryGetValue(platformLower, out var config))
        {
            return new PortableOAuthAuthorizationResult
            {
                Success = false,
                Platform = platform,
                State = state,
                ErrorMessage = $"不支持的平台: {platform}"
            };
        }

        if (string.IsNullOrEmpty(config.ClientId))
        {
            return new PortableOAuthAuthorizationResult
            {
                Success = false,
                Platform = platform,
                State = state,
                ErrorMessage = $"{platform} 尚未配置ClientId，请在设置中配置"
            };
        }

        return new PortableOAuthAuthorizationResult
        {
            Success = true,
            Platform = platform,
            State = state,
            AuthorizationUrl = BuildAuthorizationUrl(platformLower, config, state, callbackPort)
        };
    }

    public static string BuildAuthorizationUrl(
        string platform,
        PortableOAuthProviderConfig config,
        string state,
        int callbackPort)
    {
        var queryParams = new List<KeyValuePair<string, string>>
        {
            new("response_type", "code"),
            new("client_id", config.ClientId),
            new("redirect_uri", GetCallbackUrl(callbackPort)),
            new("scope", config.Scope),
            new("state", state)
        };

        if (string.Equals(platform, "wechat", StringComparison.OrdinalIgnoreCase))
        {
            queryParams.RemoveAll(item => item.Key == "client_id");
            queryParams.Insert(1, new KeyValuePair<string, string>("appid", config.ClientId));
        }

        var query = string.Join("&", queryParams.Select(item =>
            $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}"));
        return $"{config.AuthUrl}?{query}";
    }

    public static PortableOAuthAuthorizationResult ParseCallback(string pathAndQuery, string expectedState)
    {
        var uri = new Uri(pathAndQuery, UriKind.RelativeOrAbsolute);
        if (!uri.IsAbsoluteUri)
        {
            uri = new Uri(new Uri("http://localhost"), uri);
        }

        var query = ParseQuery(uri.Query);
        var code = GetValue(query, "code");
        var state = GetValue(query, "state");
        var error = GetValue(query, "error");
        var errorDescription = GetValue(query, "error_description");

        if (!string.IsNullOrEmpty(error))
        {
            return new PortableOAuthAuthorizationResult
            {
                Success = false,
                ErrorMessage = string.IsNullOrEmpty(errorDescription) ? error : errorDescription
            };
        }

        if (state != expectedState)
        {
            return new PortableOAuthAuthorizationResult
            {
                Success = false,
                ErrorMessage = "State不匹配，可能存在CSRF攻击"
            };
        }

        if (string.IsNullOrEmpty(code))
        {
            return new PortableOAuthAuthorizationResult
            {
                Success = false,
                State = state ?? string.Empty,
                ErrorMessage = "未获取到授权码"
            };
        }

        return new PortableOAuthAuthorizationResult
        {
            Success = true,
            Code = code,
            State = state ?? string.Empty
        };
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var trimmed = query.TrimStart('?');
        if (string.IsNullOrEmpty(trimmed))
        {
            return result;
        }

        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0].Replace("+", "%20", StringComparison.Ordinal));
            var value = parts.Length > 1
                ? Uri.UnescapeDataString(parts[1].Replace("+", "%20", StringComparison.Ordinal))
                : string.Empty;
            result[key] = value;
        }

        return result;
    }

    private static string? GetValue(Dictionary<string, string> query, string key)
    {
        return query.TryGetValue(key, out var value) ? value : null;
    }
}
