using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableAccountDeletionTests
{
    [Fact]
    public async Task RequestDeletionAsync_posts_original_payload_with_signed_auth()
    {
        using var temp = new TempDirectory();
        var tokenStore = CreateAuthenticatedStore(temp, new Queue<string>(new[] { "delete-nonce" }));
        var handler = new SequenceHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""
            {
              "success": true,
              "data": {
                "requestId": "del-1",
                "status": "pending",
                "requestTime": "2026-05-10T12:00:00Z",
                "scheduledDeleteTime": "2026-05-17T12:00:00Z",
                "remainingDays": 7
              }
            }
            """)
        });
        using var httpClient = new HttpClient(handler);
        var api = new PortableAuthApiClient(httpClient, "https://api.example.com", tokenStore, "TianMing/1.0");

        var response = await api.RequestDeletionAsync(PortableDeletionRequest.CreateUserInitiated());

        Assert.True(response.Success);
        Assert.Equal("del-1", response.Data!.RequestId);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.example.com/api/account/deletion", request.RequestUri!.ToString());
        Assert.Equal("access-1", request.Headers.Authorization!.Parameter);
        Assert.Equal("delete-nonce", request.Headers.GetValues("X-Nonce").Single());
        Assert.Equal(
            ExpectedSignature($"POST/api/account/deletion1778400000delete-nonce{handler.Bodies.Single()}", "session-key"),
            request.Headers.GetValues("X-Sign").Single());
        using var body = JsonDocument.Parse(handler.Bodies.Single());
        Assert.Equal("用户主动注销", body.RootElement.GetProperty("reasons")[0].GetString());
        Assert.Equal("", body.RootElement.GetProperty("customFeedback").GetString());
        Assert.False(body.RootElement.GetProperty("retainLoginHistory").GetBoolean());
        Assert.False(body.RootElement.GetProperty("retainThemes").GetBoolean());
        Assert.False(body.RootElement.GetProperty("retainSettings").GetBoolean());
    }

    [Fact]
    public async Task CancelDeletionAsync_uses_original_delete_path_with_signed_auth()
    {
        using var temp = new TempDirectory();
        var tokenStore = CreateAuthenticatedStore(temp, new Queue<string>(new[] { "cancel-nonce" }));
        var handler = new SequenceHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""{"success":true,"message":"cancelled"}""")
        });
        using var httpClient = new HttpClient(handler);
        var api = new PortableAuthApiClient(httpClient, "https://api.example.com", tokenStore, "TianMing/1.0");

        var response = await api.CancelDeletionAsync();

        Assert.True(response.Success);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("https://api.example.com/api/account/deletion", request.RequestUri!.ToString());
        Assert.Equal("access-1", request.Headers.Authorization!.Parameter);
        Assert.Equal("cancel-nonce", request.Headers.GetValues("X-Nonce").Single());
        Assert.Equal(
            ExpectedSignature("DELETE/api/account/deletion1778400000cancel-nonce", "session-key"),
            request.Headers.GetValues("X-Sign").Single());
        Assert.Empty(handler.Bodies.Single());
    }

    [Fact]
    public async Task Controller_rejects_wrong_confirmation_before_calling_api()
    {
        using var temp = new TempDirectory();
        var tokenStore = CreateAuthenticatedStore(temp, new Queue<string>(new[] { "unused" }));
        var api = new RecordingDeletionApi();
        var controller = new PortableAccountDeletionController(api, tokenStore);

        var result = await controller.ConfirmDeletionAsync("确认删除", userConfirmed: true);

        Assert.False(result.Success);
        Assert.Equal("请输入「确认注销」", result.Message);
        Assert.False(api.RequestCalled);
        Assert.True(tokenStore.IsLoggedIn);
    }

    [Fact]
    public async Task Controller_clears_tokens_after_successful_confirmed_deletion()
    {
        using var temp = new TempDirectory();
        var tokenStore = CreateAuthenticatedStore(temp, new Queue<string>(new[] { "unused" }));
        var api = new RecordingDeletionApi
        {
            Response = new PortableApiResponse<PortableDeletionStatus>
            {
                Success = true,
                Data = new PortableDeletionStatus { RequestId = "del-1", Status = "pending" }
            }
        };
        var controller = new PortableAccountDeletionController(api, tokenStore);

        var result = await controller.ConfirmDeletionAsync("确认注销", userConfirmed: true);

        Assert.True(result.Success);
        Assert.True(api.RequestCalled);
        Assert.Equal("用户主动注销", api.LastRequest!.Reasons.Single());
        Assert.False(tokenStore.IsLoggedIn);
        Assert.Null(tokenStore.AccessToken);
        Assert.Equal("账号注销成功", result.Message);
    }

    [Fact]
    public async Task Controller_cancel_does_not_clear_tokens_or_call_api()
    {
        using var temp = new TempDirectory();
        var tokenStore = CreateAuthenticatedStore(temp, new Queue<string>(new[] { "unused" }));
        var api = new RecordingDeletionApi();
        var controller = new PortableAccountDeletionController(api, tokenStore);

        var result = await controller.ConfirmDeletionAsync("确认注销", userConfirmed: false);

        Assert.False(result.Success);
        Assert.Equal("注销操作已取消", result.Message);
        Assert.False(api.RequestCalled);
        Assert.True(tokenStore.IsLoggedIn);
    }

    private static PortableAuthTokenStore CreateStore(
        TempDirectory temp,
        Queue<string>? clientIds = null,
        Queue<string>? nonces = null)
    {
        clientIds ??= new Queue<string>(new[] { "client-1" });
        nonces ??= new Queue<string>(new[] { "nonce-default" });
        return new PortableAuthTokenStore(
            Path.Combine(temp.Path, "auth_token.dat"),
            new Base64JsonTokenProtector(),
            () => DateTimeOffset.FromUnixTimeSeconds(1778400000).UtcDateTime,
            () => clientIds.Dequeue(),
            () => nonces.Dequeue());
    }

    private static PortableAuthTokenStore CreateAuthenticatedStore(TempDirectory temp, Queue<string> nonces)
    {
        var tokenStore = CreateStore(temp, nonces: nonces);
        tokenStore.SaveTokens(new PortableLoginResult
        {
            AccessToken = "access-1",
            RefreshToken = "refresh-1",
            SessionKey = "session-key",
            ExpiresAt = new DateTime(2026, 5, 10, 13, 0, 0, DateTimeKind.Utc),
            User = new PortableUserInfo { UserId = "u1", Username = "jimmy" }
        });
        return tokenStore;
    }

    private static StringContent JsonContent(string json)
    {
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private static string ExpectedSignature(string content, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(content)));
    }

    private sealed class Base64JsonTokenProtector : IPortableAuthTokenProtector
    {
        public string Protect(byte[] data) => Convert.ToBase64String(data);
        public byte[] Unprotect(string payload) => Convert.FromBase64String(payload);
    }

    private sealed class RecordingDeletionApi : IPortableAccountDeletionApi
    {
        public bool RequestCalled { get; private set; }
        public PortableDeletionRequest? LastRequest { get; private set; }
        public PortableApiResponse<PortableDeletionStatus> Response { get; set; } = new()
        {
            Success = true,
            Data = new PortableDeletionStatus()
        };

        public Task<PortableApiResponse<PortableDeletionStatus>> RequestDeletionAsync(
            PortableDeletionRequest request,
            CancellationToken cancellationToken = default)
        {
            RequestCalled = true;
            LastRequest = request;
            return Task.FromResult(Response);
        }
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;
        public List<HttpRequestMessage> Requests { get; } = new();
        public List<string> Bodies { get; } = new();

        public SequenceHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            return _responses.Dequeue();
        }
    }
}
