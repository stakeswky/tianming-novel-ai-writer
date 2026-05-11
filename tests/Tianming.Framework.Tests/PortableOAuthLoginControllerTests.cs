using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableOAuthLoginControllerTests
{
    [Fact]
    public async Task LoginAsync_rejects_unbound_platform_before_authorization()
    {
        var flow = new RecordingOAuthAuthorizationFlow();
        var api = new RecordingOAuthLoginApi();
        var controller = new PortableOAuthLoginController(
            flow,
            api,
            platform => platform != "github",
            _ => true);

        var result = await controller.LoginAsync("github");

        Assert.False(result.Success);
        Assert.Equal(PortableOAuthLoginPresentation.InfoDialog, result.Presentation);
        Assert.Equal("您尚未绑定GitHub账号。\n\n请先登录，然后在【账号绑定】中完成绑定后再使用第三方登录。", result.Message);
        Assert.False(flow.Called);
        Assert.False(api.Called);
    }

    [Fact]
    public async Task LoginAsync_rejects_unconfigured_platform_after_bound_check()
    {
        var flow = new RecordingOAuthAuthorizationFlow();
        var api = new RecordingOAuthLoginApi();
        var controller = new PortableOAuthLoginController(
            flow,
            api,
            _ => true,
            platform => platform != "wechat");

        var result = await controller.LoginAsync("wechat");

        Assert.False(result.Success);
        Assert.Equal(PortableOAuthLoginPresentation.WarningDialog, result.Presentation);
        Assert.Equal("微信登录尚未配置，请联系管理员。", result.Message);
        Assert.False(flow.Called);
        Assert.False(api.Called);
    }

    [Fact]
    public async Task LoginAsync_returns_authorization_failure_without_calling_api()
    {
        var flow = new RecordingOAuthAuthorizationFlow
        {
            Response = new PortableOAuthAuthorizationResult
            {
                Success = false,
                ErrorMessage = "授权失败"
            }
        };
        var api = new RecordingOAuthLoginApi();
        var controller = new PortableOAuthLoginController(flow, api, _ => true, _ => true);

        var result = await controller.LoginAsync("github");

        Assert.False(result.Success);
        Assert.Equal(PortableOAuthLoginPresentation.InlineError, result.Presentation);
        Assert.Equal("授权失败", result.Message);
        Assert.False(api.Called);
    }

    [Fact]
    public async Task LoginAsync_calls_oauth_api_after_authorization_success_and_returns_username()
    {
        var flow = new RecordingOAuthAuthorizationFlow
        {
            Response = new PortableOAuthAuthorizationResult
            {
                Success = true,
                Code = "code-1",
                State = "state-1"
            }
        };
        var api = new RecordingOAuthLoginApi
        {
            Response = new PortableApiResponse<PortableOAuthLoginResult>
            {
                Success = true,
                Data = new PortableOAuthLoginResult
                {
                    User = new PortableUserInfo { Username = "jimmy" },
                    AccessToken = "access-1",
                    RefreshToken = "refresh-1",
                    SessionKey = "session-1",
                    ExpiresAt = new DateTime(2026, 5, 10, 13, 0, 0, DateTimeKind.Utc)
                }
            }
        };
        var controller = new PortableOAuthLoginController(flow, api, _ => true, _ => true);

        var result = await controller.LoginAsync("github");

        Assert.True(result.Success);
        Assert.Equal("jimmy", result.LoggedInUsername);
        Assert.Equal("github", api.LastPlatform);
        Assert.Equal("github", api.LastRequest!.Platform);
        Assert.Equal("code-1", api.LastRequest.Code);
        Assert.Equal("state-1", api.LastRequest.State);
    }

    [Fact]
    public async Task LoginAsync_uses_original_api_failure_fallback_and_exception_prefix()
    {
        var flow = new RecordingOAuthAuthorizationFlow
        {
            Response = new PortableOAuthAuthorizationResult { Success = true, Code = "code-1" }
        };
        var api = new RecordingOAuthLoginApi
        {
            Response = new PortableApiResponse<PortableOAuthLoginResult>
            {
                Success = false,
                Message = null
            }
        };
        var controller = new PortableOAuthLoginController(flow, api, _ => true, _ => true);

        var failed = await controller.LoginAsync("github");
        api.Exception = new InvalidOperationException("boom");
        var thrown = await controller.LoginAsync("github");

        Assert.False(failed.Success);
        Assert.Equal("github登录失败", failed.Message);
        Assert.False(thrown.Success);
        Assert.Equal("登录失败: boom", thrown.Message);
    }

    private sealed class RecordingOAuthAuthorizationFlow : IPortableOAuthAuthorizationFlow
    {
        public bool Called { get; private set; }
        public string? LastPlatform { get; private set; }
        public PortableOAuthAuthorizationResult Response { get; init; } = new()
        {
            Success = true,
            Code = "code-1",
            State = "state-1"
        };

        public Task<PortableOAuthAuthorizationResult> StartAuthorizationAsync(
            string platform,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            Called = true;
            LastPlatform = platform;
            return Task.FromResult(Response);
        }
    }

    private sealed class RecordingOAuthLoginApi : IPortableOAuthLoginApi
    {
        public bool Called { get; private set; }
        public string? LastPlatform { get; private set; }
        public PortableOAuthRequest? LastRequest { get; private set; }
        public Exception? Exception { get; set; }
        public PortableApiResponse<PortableOAuthLoginResult> Response { get; init; } = new() { Success = true };

        public Task<PortableApiResponse<PortableOAuthLoginResult>> OAuthLoginAsync(
            string platform,
            PortableOAuthRequest request,
            CancellationToken cancellationToken = default)
        {
            Called = true;
            LastPlatform = platform;
            LastRequest = request;
            if (Exception != null)
            {
                throw Exception;
            }

            return Task.FromResult(Response);
        }
    }
}
