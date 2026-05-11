using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableOAuthAuthorizationCoreTests
{
    [Fact]
    public void GetCallbackUrl_uses_original_local_callback_shape()
    {
        Assert.Equal("http://localhost:23456/oauth/callback", PortableOAuthAuthorizationCore.GetCallbackUrl(23456));
    }

    [Fact]
    public void CreateDefaultConfigs_contains_original_platform_urls_and_scopes()
    {
        var configs = PortableOAuthAuthorizationCore.CreateDefaultConfigs();

        Assert.Equal("https://github.com/login/oauth/authorize", configs["github"].AuthUrl);
        Assert.Equal("user:email", configs["github"].Scope);
        Assert.Equal("https://accounts.google.com/o/oauth2/v2/auth", configs["google"].AuthUrl);
        Assert.Equal("openid email profile", configs["google"].Scope);
        Assert.Equal("https://open.weixin.qq.com/connect/qrconnect", configs["wechat"].AuthUrl);
        Assert.Equal("snsapi_login", configs["wechat"].Scope);
    }

    [Fact]
    public void BuildAuthorizationUrl_uses_standard_oauth_query_fields()
    {
        var url = PortableOAuthAuthorizationCore.BuildAuthorizationUrl(
            "github",
            new PortableOAuthProviderConfig
            {
                AuthUrl = "https://github.com/login/oauth/authorize",
                ClientId = "client 1",
                Scope = "user:email"
            },
            state: "state-1",
            callbackPort: 23456);

        Assert.StartsWith("https://github.com/login/oauth/authorize?", url, StringComparison.Ordinal);
        Assert.Contains("response_type=code", url, StringComparison.Ordinal);
        Assert.Contains("client_id=client%201", url, StringComparison.Ordinal);
        Assert.Contains("redirect_uri=http%3A%2F%2Flocalhost%3A23456%2Foauth%2Fcallback", url, StringComparison.Ordinal);
        Assert.Contains("scope=user%3Aemail", url, StringComparison.Ordinal);
        Assert.Contains("state=state-1", url, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildAuthorizationUrl_uses_appid_for_wechat()
    {
        var url = PortableOAuthAuthorizationCore.BuildAuthorizationUrl(
            "wechat",
            new PortableOAuthProviderConfig
            {
                AuthUrl = "https://open.weixin.qq.com/connect/qrconnect",
                ClientId = "wx-client",
                Scope = "snsapi_login"
            },
            state: "state-1",
            callbackPort: 23456);

        Assert.Contains("appid=wx-client", url, StringComparison.Ordinal);
        Assert.DoesNotContain("client_id=", url, StringComparison.Ordinal);
    }

    [Fact]
    public void PrepareAuthorization_rejects_unknown_platform()
    {
        var result = PortableOAuthAuthorizationCore.PrepareAuthorization(
            "unknown",
            new Dictionary<string, PortableOAuthProviderConfig>(),
            state: "state-1",
            callbackPort: 23456);

        Assert.False(result.Success);
        Assert.Equal("不支持的平台: unknown", result.ErrorMessage);
    }

    [Fact]
    public void PrepareAuthorization_rejects_missing_client_id()
    {
        var result = PortableOAuthAuthorizationCore.PrepareAuthorization(
            "github",
            PortableOAuthAuthorizationCore.CreateDefaultConfigs(),
            state: "state-1",
            callbackPort: 23456);

        Assert.False(result.Success);
        Assert.Equal("github 尚未配置ClientId，请在设置中配置", result.ErrorMessage);
    }

    [Fact]
    public void ParseCallback_returns_success_for_matching_state_and_code()
    {
        var result = PortableOAuthAuthorizationCore.ParseCallback(
            "/oauth/callback?code=abc%20123&state=state-1",
            expectedState: "state-1");

        Assert.True(result.Success);
        Assert.Equal("abc 123", result.Code);
        Assert.Equal("state-1", result.State);
    }

    [Theory]
    [InlineData("/oauth/callback?error=access_denied&error_description=user%20denied", "user denied")]
    [InlineData("/oauth/callback?code=abc&state=wrong", "State不匹配，可能存在CSRF攻击")]
    [InlineData("/oauth/callback?state=state-1", "未获取到授权码")]
    public void ParseCallback_returns_original_error_messages(string pathAndQuery, string expected)
    {
        var result = PortableOAuthAuthorizationCore.ParseCallback(pathAndQuery, expectedState: "state-1");

        Assert.False(result.Success);
        Assert.Equal(expected, result.ErrorMessage);
    }
}
