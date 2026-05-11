using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableAccountBindingAuthorizationFlowTests
{
    [Fact]
    public async Task BindPlatformAsync_authorizes_binds_on_server_and_updates_local_store()
    {
        using var temp = new TempDirectory();
        var authorization = new FakeOAuthAuthorizationFlow
        {
            Result = new PortableOAuthAuthorizationResult
            {
                Success = true,
                Platform = "github",
                Code = "code-1",
                State = "state-1"
            }
        };
        var api = new FakeAccountBindingApiClient
        {
            Response = new PortableApiResponse<PortableBindingInfo>
            {
                Success = true,
                Data = new PortableBindingInfo
                {
                    BindingId = 7,
                    Platform = "github",
                    PlatformUserId = "gh-1",
                    DisplayName = "Jimmy",
                    BoundTime = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc)
                }
            }
        };
        var store = CreateStore(temp);
        var flow = new PortableAccountBindingAuthorizationFlow(authorization, api, store);

        var result = await flow.BindPlatformAsync("github", TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("github", result.Platform);
        Assert.Equal("gh-1", result.Binding!.PlatformUserId);
        Assert.Equal("code-1", api.Request!.Code);
        Assert.Equal("state-1", api.Request.State);
        Assert.Equal("github", api.Platform);
        var binding = store.GetBinding(PortableBindingPlatform.GitHub)!;
        Assert.Equal("gh-1", binding.AccountId);
        Assert.Equal("Jimmy", binding.Nickname);
        Assert.True(binding.IsActive);
    }

    [Fact]
    public async Task BindPlatformAsync_returns_authorization_failure_without_calling_bind_api()
    {
        using var temp = new TempDirectory();
        var authorization = new FakeOAuthAuthorizationFlow
        {
            Result = new PortableOAuthAuthorizationResult
            {
                Success = false,
                Platform = "github",
                ErrorMessage = "授权超时，请重试"
            }
        };
        var api = new FakeAccountBindingApiClient();
        var flow = new PortableAccountBindingAuthorizationFlow(authorization, api, CreateStore(temp));

        var result = await flow.BindPlatformAsync("github", TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("授权超时，请重试", result.ErrorMessage);
        Assert.False(api.Called);
    }

    [Fact]
    public async Task BindPlatformAsync_returns_bind_failure_without_updating_local_store()
    {
        using var temp = new TempDirectory();
        var authorization = new FakeOAuthAuthorizationFlow
        {
            Result = new PortableOAuthAuthorizationResult
            {
                Success = true,
                Platform = "github",
                Code = "code-1",
                State = "state-1"
            }
        };
        var api = new FakeAccountBindingApiClient
        {
            Response = new PortableApiResponse<PortableBindingInfo>
            {
                Success = false,
                Message = "绑定失败"
            }
        };
        var store = CreateStore(temp);
        var flow = new PortableAccountBindingAuthorizationFlow(authorization, api, store);

        var result = await flow.BindPlatformAsync("github", TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("绑定失败", result.ErrorMessage);
        Assert.False(store.IsBound(PortableBindingPlatform.GitHub));
    }

    [Fact]
    public async Task BindPlatformAsync_uses_error_code_when_bind_message_is_empty()
    {
        using var temp = new TempDirectory();
        var authorization = new FakeOAuthAuthorizationFlow
        {
            Result = new PortableOAuthAuthorizationResult
            {
                Success = true,
                Platform = "github",
                Code = "code-1",
                State = "state-1"
            }
        };
        var api = new FakeAccountBindingApiClient
        {
            Response = new PortableApiResponse<PortableBindingInfo>
            {
                Success = false,
                ErrorCode = "BINDING_EXISTS"
            }
        };
        var flow = new PortableAccountBindingAuthorizationFlow(authorization, api, CreateStore(temp));

        var result = await flow.BindPlatformAsync("github", TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("BINDING_EXISTS", result.ErrorMessage);
    }

    [Fact]
    public void Constructor_rejects_missing_dependencies()
    {
        using var temp = new TempDirectory();
        var authorization = new FakeOAuthAuthorizationFlow();
        var api = new FakeAccountBindingApiClient();
        var store = CreateStore(temp);

        Assert.Throws<ArgumentNullException>(() => new PortableAccountBindingAuthorizationFlow(null!, api, store));
        Assert.Throws<ArgumentNullException>(() => new PortableAccountBindingAuthorizationFlow(authorization, null!, store));
        Assert.Throws<ArgumentNullException>(() => new PortableAccountBindingAuthorizationFlow(authorization, api, null!));
    }

    private static PortableAccountBindingStore CreateStore(TempDirectory temp)
    {
        return new PortableAccountBindingStore(
            Path.Combine(temp.Path, "bindings.json"),
            () => new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc));
    }

    private sealed class FakeOAuthAuthorizationFlow : IPortableOAuthAuthorizationFlow
    {
        public PortableOAuthAuthorizationResult Result { get; set; } = new() { Success = false };

        public Task<PortableOAuthAuthorizationResult> StartAuthorizationAsync(
            string platform,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeAccountBindingApiClient : IPortableAccountBindingApiClient
    {
        public bool Called { get; private set; }
        public string? Platform { get; private set; }
        public PortableOAuthRequest? Request { get; private set; }
        public PortableApiResponse<PortableBindingInfo> Response { get; set; } = new();

        public Task<PortableApiResponse<PortableBindingInfo>> BindAccountAsync(
            string platform,
            PortableOAuthRequest request,
            CancellationToken cancellationToken = default)
        {
            Called = true;
            Platform = platform;
            Request = request;
            return Task.FromResult(Response);
        }
    }
}
