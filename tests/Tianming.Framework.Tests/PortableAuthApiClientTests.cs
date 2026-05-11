using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TM.Framework.Profile;
using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableAuthApiClientTests
{
    [Fact]
    public async Task LoginAsync_posts_login_request_and_saves_tokens()
    {
        using var temp = new TempDirectory();
        var tokenStore = CreateStore(temp);
        var handler = new SequenceHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""
            {
              "success": true,
              "data": {
                "accessToken": "access-1",
                "refreshToken": "refresh-1",
                "sessionKey": "session-1",
                "expiresAt": "2026-05-10T13:00:00Z",
                "user": {
                  "userId": "u1",
                  "username": "jimmy"
                }
              }
            }
            """)
        });
        using var httpClient = new HttpClient(handler);
        var api = new PortableAuthApiClient(httpClient, "https://api.example.com/", tokenStore, "TianMing/1.0");

        var response = await api.LoginAsync(new PortableLoginRequest { Username = "jimmy", Password = "secret" });

        Assert.True(response.Success);
        Assert.Equal("access-1", tokenStore.AccessToken);
        Assert.Equal("refresh-1", tokenStore.RefreshToken);
        Assert.Equal("session-1", tokenStore.SessionKey);
        Assert.Equal("jimmy", tokenStore.Username);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.example.com/api/auth/login", request.RequestUri!.ToString());
        Assert.Null(request.Headers.Authorization);
        Assert.False(request.Headers.Contains("X-Sign"));
        Assert.Equal("client-1", request.Headers.GetValues("X-Client-Id").Single());
        Assert.Equal("TianMing/1.0", request.Headers.UserAgent.ToString());
        using var document = JsonDocument.Parse(handler.Bodies.Single());
        Assert.Equal("jimmy", document.RootElement.GetProperty("username").GetString());
        Assert.Equal("secret", document.RootElement.GetProperty("password").GetString());
    }

    [Fact]
    public async Task GetSubscriptionAsync_adds_bearer_and_original_signature_headers()
    {
        using var temp = new TempDirectory();
        var tokenStore = CreateStore(temp, nonces: new Queue<string>(new[] { "nonce-1" }));
        tokenStore.SaveTokens(new PortableLoginResult
        {
            AccessToken = "access-1",
            RefreshToken = "refresh-1",
            SessionKey = "session-key",
            ExpiresAt = new DateTime(2026, 5, 10, 13, 0, 0, DateTimeKind.Utc),
            User = new PortableUserInfo { UserId = "u1", Username = "jimmy" }
        });
        var handler = new SequenceHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""{"success":true,"data":{"subscriptionId":1,"userId":"u1","planType":"pro","isActive":true}}""")
        });
        using var httpClient = new HttpClient(handler);
        var api = new PortableAuthApiClient(httpClient, "https://api.example.com", tokenStore, "TianMing/1.0");

        var response = await api.GetSubscriptionAsync();

        Assert.True(response.Success);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://api.example.com/api/subscription", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("access-1", request.Headers.Authorization.Parameter);
        Assert.Equal("1778400000", request.Headers.GetValues("X-Timestamp").Single());
        Assert.Equal("nonce-1", request.Headers.GetValues("X-Nonce").Single());
        Assert.Equal(ExpectedSignature("GET/api/subscription1778400000nonce-1", "session-key"), request.Headers.GetValues("X-Sign").Single());
    }

    [Fact]
    public async Task Authenticated_request_refreshes_expired_access_token_before_business_call()
    {
        using var temp = new TempDirectory();
        var tokenStore = CreateStore(temp, nonces: new Queue<string>(new[] { "refresh-nonce", "business-nonce" }));
        tokenStore.SaveTokens(new PortableLoginResult
        {
            AccessToken = "expired-access",
            RefreshToken = "refresh-old",
            SessionKey = "old-session",
            ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(1778400000).UtcDateTime.AddMinutes(-1),
            User = new PortableUserInfo { UserId = "u1", Username = "jimmy" }
        });
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""
                {
                  "success": true,
                  "data": {
                    "accessToken": "access-new",
                    "refreshToken": "refresh-new",
                    "sessionKey": "session-new",
                    "expiresAt": "2026-05-10T13:00:00Z"
                  }
                }
                """)
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""{"success":true,"data":{"subscriptionId":1,"userId":"u1","planType":"pro","isActive":true}}""")
            });
        using var httpClient = new HttpClient(handler);
        var api = new PortableAuthApiClient(httpClient, "https://api.example.com", tokenStore, "TianMing/1.0");

        var response = await api.GetSubscriptionAsync();

        Assert.True(response.Success);
        Assert.Equal("access-new", tokenStore.AccessToken);
        Assert.Equal("refresh-new", tokenStore.RefreshToken);
        Assert.Equal("session-new", tokenStore.SessionKey);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("https://api.example.com/api/auth/refresh", handler.Requests[0].RequestUri!.ToString());
        Assert.Null(handler.Requests[0].Headers.Authorization);
        Assert.Equal("refresh-nonce", handler.Requests[0].Headers.GetValues("X-Nonce").Single());
        Assert.Equal("https://api.example.com/api/subscription", handler.Requests[1].RequestUri!.ToString());
        Assert.Equal("access-new", handler.Requests[1].Headers.Authorization!.Parameter);
        Assert.Equal("business-nonce", handler.Requests[1].Headers.GetValues("X-Nonce").Single());
    }

    [Fact]
    public async Task RegisterAndOAuthLoginAsync_save_returned_tokens()
    {
        using var temp = new TempDirectory();
        var tokenStore = CreateStore(temp);
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""
                {
                  "success": true,
                  "data": {
                    "userId": "u2",
                    "username": "new-user",
                    "accessToken": "register-access",
                    "refreshToken": "register-refresh",
                    "sessionKey": "register-session",
                    "expiresAt": "2026-05-10T13:00:00Z"
                  }
                }
                """)
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""
                {
                  "success": true,
                  "data": {
                    "isNewUser": false,
                    "accessToken": "oauth-access",
                    "refreshToken": "oauth-refresh",
                    "sessionKey": "oauth-session",
                    "expiresAt": "2026-05-10T14:00:00Z",
                    "user": {
                      "userId": "u3",
                      "username": "oauth-user"
                    }
                  }
                }
                """)
            });
        using var httpClient = new HttpClient(handler);
        var api = new PortableAuthApiClient(httpClient, "https://api.example.com", tokenStore, "TianMing/1.0");

        await api.RegisterAsync(new PortableRegisterRequest { Username = "new-user", Password = "secret", CardKey = "CARD-1" });
        await api.OAuthLoginAsync("github", new PortableOAuthRequest { Platform = "github", Code = "code-1", State = "state-1" });

        Assert.Equal("oauth-access", tokenStore.AccessToken);
        Assert.Equal("oauth-refresh", tokenStore.RefreshToken);
        Assert.Equal("oauth-session", tokenStore.SessionKey);
        Assert.Equal("oauth-user", tokenStore.Username);
        Assert.Equal("https://api.example.com/api/auth/register", handler.Requests[0].RequestUri!.ToString());
        Assert.Equal("https://api.example.com/api/auth/oauth/github", handler.Requests[1].RequestUri!.ToString());
        Assert.Null(handler.Requests[0].Headers.Authorization);
        Assert.Null(handler.Requests[1].Headers.Authorization);
    }

    [Fact]
    public async Task Subscription_mutation_methods_use_original_paths_and_signed_auth_requests()
    {
        using var temp = new TempDirectory();
        var tokenStore = CreateAuthenticatedStore(temp, new Queue<string>(new[] { "activate-nonce", "history-nonce", "renew-nonce" }));
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent("""{"success":true,"data":{"success":true,"daysAdded":30,"newExpireTime":"2026-06-10T00:00:00Z","subscription":{"isActive":true}}}""") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent("""{"success":true,"data":{"records":[{"cardKey":"CARD-1","durationDays":30,"activatedTime":"2026-05-10T00:00:00Z"}]}}""") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent("""{"success":true,"data":{"success":true,"daysAdded":15,"newExpireTime":"2026-05-25T00:00:00Z","subscription":{"isActive":true}}}""") });
        using var httpClient = new HttpClient(handler);
        var api = new PortableAuthApiClient(httpClient, "https://api.example.com", tokenStore, "TianMing/1.0");

        await api.ActivateCardKeyAsync("CARD-1");
        await api.GetActivationHistoryAsync();
        await api.RenewAccountWithCardKeyAsync("target-account", "CARD-2");

        Assert.Equal("https://api.example.com/api/subscription/activate", handler.Requests[0].RequestUri!.ToString());
        Assert.Equal("https://api.example.com/api/subscription/history", handler.Requests[1].RequestUri!.ToString());
        Assert.Equal("https://api.example.com/api/subscription/renew", handler.Requests[2].RequestUri!.ToString());
        Assert.All(handler.Requests, request => Assert.Equal("access-1", request.Headers.Authorization!.Parameter));
        Assert.Equal("activate-nonce", handler.Requests[0].Headers.GetValues("X-Nonce").Single());
        Assert.Equal("history-nonce", handler.Requests[1].Headers.GetValues("X-Nonce").Single());
        Assert.Equal("renew-nonce", handler.Requests[2].Headers.GetValues("X-Nonce").Single());
        using var activateBody = JsonDocument.Parse(handler.Bodies[0]);
        using var renewBody = JsonDocument.Parse(handler.Bodies[2]);
        Assert.Equal("CARD-1", activateBody.RootElement.GetProperty("cardKey").GetString());
        Assert.Equal("target-account", renewBody.RootElement.GetProperty("account").GetString());
        Assert.Equal("CARD-2", renewBody.RootElement.GetProperty("cardKey").GetString());
    }

    [Fact]
    public async Task Account_binding_methods_use_original_paths_and_signed_auth_requests()
    {
        using var temp = new TempDirectory();
        var tokenStore = CreateAuthenticatedStore(temp, new Queue<string>(new[] { "list-nonce", "bind-nonce", "unbind-nonce" }));
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent("""{"success":true,"data":{"bindings":[]}}""") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent("""{"success":true,"data":{"bindingId":7,"platform":"github","platformUserId":"gh-1","displayName":"Jimmy","boundTime":"2026-05-10T00:00:00Z"}}""") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent("""{"success":true,"message":"ok"}""") });
        using var httpClient = new HttpClient(handler);
        var api = new PortableAuthApiClient(httpClient, "https://api.example.com", tokenStore, "TianMing/1.0");

        await api.GetBindingsAsync();
        await api.BindAccountAsync("github", new PortableOAuthRequest { Platform = "github", Code = "code-1", State = "state-1" });
        await api.UnbindAccountAsync("github");

        Assert.Equal("https://api.example.com/api/account/bindings", handler.Requests[0].RequestUri!.ToString());
        Assert.Equal("https://api.example.com/api/account/bindings/github", handler.Requests[1].RequestUri!.ToString());
        Assert.Equal("https://api.example.com/api/account/bindings/github", handler.Requests[2].RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
        Assert.Equal(HttpMethod.Delete, handler.Requests[2].Method);
        Assert.All(handler.Requests, request => Assert.Equal("access-1", request.Headers.Authorization!.Parameter));
        Assert.Equal("list-nonce", handler.Requests[0].Headers.GetValues("X-Nonce").Single());
        Assert.Equal("bind-nonce", handler.Requests[1].Headers.GetValues("X-Nonce").Single());
        Assert.Equal("unbind-nonce", handler.Requests[2].Headers.GetValues("X-Nonce").Single());
        using var bindBody = JsonDocument.Parse(handler.Bodies[1]);
        Assert.Equal("github", bindBody.RootElement.GetProperty("platform").GetString());
        Assert.Equal("code-1", bindBody.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetLoginHistoryAsync_uses_original_path_query_and_signed_auth_request()
    {
        using var temp = new TempDirectory();
        var tokenStore = CreateAuthenticatedStore(temp, new Queue<string>(new[] { "history-nonce" }));
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""
                {
                  "success": true,
                  "data": {
                    "records": [
                      {
                        "logId": 7,
                        "userId": "u1",
                        "loginTime": "2026-05-10T12:00:00Z",
                        "ipAddress": "10.0.0.1",
                        "userAgent": "TianMing/1.0",
                        "result": "success",
                        "location": "上海"
                      }
                    ],
                    "totalCount": 1
                  }
                }
                """)
            });
        using var httpClient = new HttpClient(handler);
        var api = new PortableAuthApiClient(httpClient, "https://api.example.com", tokenStore, "TianMing/1.0");

        var response = await api.GetLoginHistoryAsync(page: 2, pageSize: 50);

        Assert.True(response.Success);
        Assert.Equal(1, response.Data!.TotalCount);
        Assert.Equal("https://api.example.com/api/account/login-history?page=2&pageSize=50", handler.Requests[0].RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal("access-1", handler.Requests[0].Headers.Authorization!.Parameter);
        Assert.Equal("history-nonce", handler.Requests[0].Headers.GetValues("X-Nonce").Single());
        Assert.Equal(
            ExpectedSignature("GET/api/account/login-history?page=2&pageSize=501778400000history-nonce", "session-key"),
            handler.Requests[0].Headers.GetValues("X-Sign").Single());
    }

    [Fact]
    public async Task LogoutAsync_uses_original_logout_path_and_signed_auth_request()
    {
        using var temp = new TempDirectory();
        var tokenStore = CreateAuthenticatedStore(temp, new Queue<string>(new[] { "logout-nonce" }));
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""{"success":true,"message":"ok"}""")
            });
        using var httpClient = new HttpClient(handler);
        var api = new PortableAuthApiClient(httpClient, "https://api.example.com", tokenStore, "TianMing/1.0");

        var response = await api.LogoutAsync();

        Assert.True(response.Success);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.example.com/api/auth/logout", request.RequestUri!.ToString());
        Assert.Equal("access-1", request.Headers.Authorization!.Parameter);
        Assert.Equal("logout-nonce", request.Headers.GetValues("X-Nonce").Single());
        Assert.Equal(
            ExpectedSignature("POST/api/auth/logout1778400000logout-nonce", "session-key"),
            request.Headers.GetValues("X-Sign").Single());
        Assert.Empty(handler.Bodies.Single());
    }

    [Fact]
    public async Task Profile_methods_use_original_account_profile_path_and_signed_auth_requests()
    {
        using var temp = new TempDirectory();
        var tokenStore = CreateAuthenticatedStore(temp, new Queue<string>(new[] { "get-profile-nonce", "update-profile-nonce" }));
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""
                {
                  "success": true,
                  "data": {
                    "userId": "u1",
                    "username": "jimmy",
                    "displayName": "Jimmy",
                    "email": "jimmy@example.com",
                    "location": "中国/上海/浦东"
                  }
                }
                """)
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""{"success":true,"message":"ok"}""")
            });
        using var httpClient = new HttpClient(handler);
        var api = new PortableAuthApiClient(httpClient, "https://api.example.com", tokenStore, "TianMing/1.0");

        var getResponse = await api.GetProfileAsync();
        await api.UpdateProfileAsync(new PortableServerUserProfile
        {
            UserId = "u1",
            Username = "jimmy",
            DisplayName = "Jimmy",
            Email = null,
            Gender = "保密",
            Bio = "hello",
            Location = "中国/上海/浦东"
        });

        Assert.True(getResponse.Success);
        Assert.Equal("Jimmy", getResponse.Data!.DisplayName);
        Assert.Equal("https://api.example.com/api/account/profile", handler.Requests[0].RequestUri!.ToString());
        Assert.Equal("https://api.example.com/api/account/profile", handler.Requests[1].RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal(HttpMethod.Put, handler.Requests[1].Method);
        Assert.All(handler.Requests, request => Assert.Equal("access-1", request.Headers.Authorization!.Parameter));
        Assert.Equal("get-profile-nonce", handler.Requests[0].Headers.GetValues("X-Nonce").Single());
        Assert.Equal("update-profile-nonce", handler.Requests[1].Headers.GetValues("X-Nonce").Single());
        using var body = JsonDocument.Parse(handler.Bodies[1]);
        Assert.Equal("jimmy", body.RootElement.GetProperty("username").GetString());
        Assert.Equal("中国/上海/浦东", body.RootElement.GetProperty("location").GetString());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("email").ValueKind);
    }

    [Fact]
    public async Task Lockout_methods_use_original_paths_and_signed_auth_requests()
    {
        using var temp = new TempDirectory();
        var tokenStore = CreateAuthenticatedStore(temp, new Queue<string>(new[] { "lockout-nonce", "unlock-nonce" }));
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""
                {
                  "success": true,
                  "data": {
                    "isLocked": true,
                    "failedAttempts": 7,
                    "lockedUntil": "2026-05-11T10:30:00Z",
                    "isPermanentlyLocked": false
                  }
                }
                """)
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""{"success":true,"message":"ok"}""")
            });
        using var httpClient = new HttpClient(handler);
        var api = new PortableAuthApiClient(httpClient, "https://api.example.com", tokenStore, "TianMing/1.0");

        var status = await api.GetLockoutStatusAsync();
        var unlock = await api.UnlockAccountAsync();

        Assert.True(status.Success);
        Assert.True(status.Data!.IsLocked);
        Assert.Equal(7, status.Data.FailedAttempts);
        Assert.Equal(new DateTime(2026, 5, 11, 10, 30, 0, DateTimeKind.Utc), status.Data.LockedUntil);
        Assert.True(unlock.Success);
        Assert.Equal("https://api.example.com/api/account/lockout", handler.Requests[0].RequestUri!.ToString());
        Assert.Equal("https://api.example.com/api/account/unlock", handler.Requests[1].RequestUri!.ToString());
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
        Assert.All(handler.Requests, request => Assert.Equal("access-1", request.Headers.Authorization!.Parameter));
        Assert.Equal("lockout-nonce", handler.Requests[0].Headers.GetValues("X-Nonce").Single());
        Assert.Equal("unlock-nonce", handler.Requests[1].Headers.GetValues("X-Nonce").Single());
        Assert.Empty(handler.Bodies[1]);
    }

    [Fact]
    public async Task Feature_authorization_uses_original_path_and_signed_auth_request()
    {
        using var temp = new TempDirectory();
        var tokenStore = CreateAuthenticatedStore(temp, new Queue<string>(new[] { "feature-nonce" }));
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""
                {
                  "success": true,
                  "message": "allowed",
                  "data": {
                    "authorized": true,
                    "expiresAt": 1778400300
                  }
                }
                """)
            });
        using var httpClient = new HttpClient(handler);
        var api = new PortableAuthApiClient(httpClient, "https://api.example.com", tokenStore, "TianMing/1.0");

        var response = await api.CheckFeatureAuthorizationAsync("writing.ai");

        Assert.True(response.Success);
        Assert.True(response.Data!.Authorized);
        Assert.Equal(1778400300, response.Data.ExpiresAt);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://api.example.com/api/auth/feature/writing.ai", request.RequestUri!.ToString());
        Assert.Equal("access-1", request.Headers.Authorization!.Parameter);
        Assert.Equal("feature-nonce", request.Headers.GetValues("X-Nonce").Single());
        Assert.Equal(
            ExpectedSignature("GET/api/auth/feature/writing.ai1778400000feature-nonce", "session-key"),
            request.Headers.GetValues("X-Sign").Single());
        Assert.Empty(handler.Bodies.Single());
    }

    [Fact]
    public async Task Feature_authorization_without_login_denies_without_network_request()
    {
        using var temp = new TempDirectory();
        var tokenStore = CreateStore(temp);
        var handler = new SequenceHandler();
        using var httpClient = new HttpClient(handler);
        var api = new PortableAuthApiClient(httpClient, "https://api.example.com", tokenStore, "TianMing/1.0");

        var response = await api.CheckFeatureAuthorizationAsync("writing.ai");

        Assert.False(response.Success);
        Assert.Equal(PortableApiErrorCodes.AuthInvalid, response.ErrorCode);
        Assert.Equal("未检测到登录状态", response.Message);
        Assert.Empty(handler.Requests);
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
