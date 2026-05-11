using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableAuthTokenStoreTests
{
    [Fact]
    public void Constructor_creates_client_id_when_file_is_missing()
    {
        using var temp = new TempDirectory();

        var store = CreateStore(temp, clientIds: new Queue<string>(new[] { "client-1" }));

        Assert.Equal("client-1", store.ClientId);
        Assert.False(store.IsLoggedIn);
        Assert.True(File.Exists(Path.Combine(temp.Path, "auth_token.dat")));
    }

    [Fact]
    public void SaveLoginResult_persists_tokens_and_logged_in_state()
    {
        using var temp = new TempDirectory();
        var expiresAt = new DateTime(2026, 5, 10, 12, 30, 0, DateTimeKind.Utc);
        var now = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);
        var store = CreateStore(temp, nowUtc: now);

        store.SaveTokens(new PortableLoginResult
        {
            AccessToken = "access-1",
            RefreshToken = "refresh-1",
            SessionKey = "session-1",
            ExpiresAt = expiresAt,
            User = new PortableUserInfo { UserId = "u1", Username = "jimmy" }
        });
        var reloaded = CreateStore(temp, nowUtc: now);

        Assert.True(reloaded.IsLoggedIn);
        Assert.False(reloaded.IsAccessTokenExpired);
        Assert.True(reloaded.HasRefreshToken);
        Assert.Equal("access-1", reloaded.AccessToken);
        Assert.Equal("refresh-1", reloaded.RefreshToken);
        Assert.Equal("session-1", reloaded.SessionKey);
        Assert.Equal("u1", reloaded.UserId);
        Assert.Equal("jimmy", reloaded.Username);
    }

    [Fact]
    public void SaveRegisterResult_persists_user_identity_without_user_object()
    {
        using var temp = new TempDirectory();
        var expiresAt = new DateTime(2026, 5, 10, 12, 30, 0, DateTimeKind.Utc);
        var store = CreateStore(temp, nowUtc: new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc));

        store.SaveTokens(new PortableRegisterResult
        {
            AccessToken = "access-2",
            RefreshToken = "refresh-2",
            SessionKey = "session-2",
            ExpiresAt = expiresAt,
            UserId = "u2",
            Username = "new-user"
        });

        Assert.True(store.IsLoggedIn);
        Assert.Equal("u2", store.UserId);
        Assert.Equal("new-user", store.Username);
    }

    [Fact]
    public void UpdateTokens_replaces_access_tokens_but_keeps_user_and_client()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp, clientIds: new Queue<string>(new[] { "client-1" }));
        store.SaveTokens(new PortableLoginResult
        {
            AccessToken = "old-access",
            RefreshToken = "old-refresh",
            SessionKey = "old-session",
            ExpiresAt = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc),
            User = new PortableUserInfo { UserId = "u1", Username = "jimmy" }
        });

        store.UpdateTokens(new PortableRefreshTokenResult
        {
            AccessToken = "new-access",
            RefreshToken = "new-refresh",
            SessionKey = "new-session",
            ExpiresAt = new DateTime(2026, 5, 10, 13, 0, 0, DateTimeKind.Utc)
        });

        Assert.Equal("client-1", store.ClientId);
        Assert.Equal("u1", store.UserId);
        Assert.Equal("jimmy", store.Username);
        Assert.Equal("new-access", store.AccessToken);
        Assert.Equal("new-refresh", store.RefreshToken);
        Assert.Equal("new-session", store.SessionKey);
    }

    [Fact]
    public void ClearTokens_preserves_client_id_and_removes_login_state()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp, clientIds: new Queue<string>(new[] { "client-1" }));
        store.SaveTokens(new PortableLoginResult
        {
            AccessToken = "access-1",
            RefreshToken = "refresh-1",
            SessionKey = "session-1",
            ExpiresAt = new DateTime(2026, 5, 10, 13, 0, 0, DateTimeKind.Utc),
            User = new PortableUserInfo { UserId = "u1", Username = "jimmy" }
        });

        store.ClearTokens();

        Assert.Equal("client-1", store.ClientId);
        Assert.False(store.IsLoggedIn);
        Assert.True(store.IsAccessTokenExpired);
        Assert.False(store.HasRefreshToken);
        Assert.Null(store.AccessToken);
    }

    [Fact]
    public void GenerateSignatureHeaders_uses_original_method_path_timestamp_nonce_body_shape()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(
            temp,
            clientIds: new Queue<string>(new[] { "client-1" }),
            nonces: new Queue<string>(new[] { "nonce-1" }),
            nowUtc: DateTimeOffset.FromUnixTimeSeconds(1778400000).UtcDateTime);
        store.SaveTokens(new PortableLoginResult
        {
            AccessToken = "access-1",
            RefreshToken = "refresh-1",
            SessionKey = "session-key",
            ExpiresAt = new DateTime(2026, 5, 10, 13, 0, 0, DateTimeKind.Utc),
            User = new PortableUserInfo { UserId = "u1", Username = "jimmy" }
        });

        var headers = store.GenerateSignatureHeaders("post", "/api/subscription", """{"cardKey":"CARD"}""");

        var signContent = """POST/api/subscription1778400000nonce-1{"cardKey":"CARD"}""";
        var expectedSignature = Convert.ToBase64String(
            new HMACSHA256(Encoding.UTF8.GetBytes("session-key")).ComputeHash(Encoding.UTF8.GetBytes(signContent)));
        Assert.Equal("client-1", headers.ClientId);
        Assert.Equal("1778400000", headers.Timestamp);
        Assert.Equal("nonce-1", headers.Nonce);
        Assert.Equal(expectedSignature, headers.Signature);
    }

    [Fact]
    public void Constructor_migrates_legacy_plain_json_file()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "auth_token.dat");
        File.WriteAllText(path, JsonSerializer.Serialize(new PortableAuthTokenData
        {
            ClientId = "legacy-client",
            AccessToken = "access-1",
            RefreshToken = "refresh-1",
            SessionKey = "session-1",
            ExpiresAt = new DateTime(2026, 5, 10, 13, 0, 0, DateTimeKind.Utc),
            UserId = "u1",
            Username = "jimmy"
        }));

        var store = CreateStore(temp, nowUtc: new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc));

        Assert.True(store.IsLoggedIn);
        Assert.Equal("legacy-client", store.ClientId);
        Assert.NotEqual("{", File.ReadAllText(path)[..1]);
    }

    [Fact]
    public void Constructor_recovers_from_unreadable_file_with_new_client_id()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "auth_token.dat"), "not valid payload");

        var store = CreateStore(temp, clientIds: new Queue<string>(new[] { "client-recovered" }));

        Assert.Equal("client-recovered", store.ClientId);
        Assert.False(store.IsLoggedIn);
    }

    private static PortableAuthTokenStore CreateStore(
        TempDirectory temp,
        Queue<string>? clientIds = null,
        Queue<string>? nonces = null,
        DateTime? nowUtc = null)
    {
        clientIds ??= new Queue<string>(new[] { "client-default" });
        nonces ??= new Queue<string>(new[] { "nonce-default" });
        return new PortableAuthTokenStore(
            Path.Combine(temp.Path, "auth_token.dat"),
            new Base64JsonTokenProtector(),
            () => nowUtc ?? new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc),
            () => clientIds.Dequeue(),
            () => nonces.Dequeue());
    }

    private sealed class Base64JsonTokenProtector : IPortableAuthTokenProtector
    {
        public string Protect(byte[] data)
        {
            return Convert.ToBase64String(data);
        }

        public byte[] Unprotect(string payload)
        {
            return Convert.FromBase64String(payload);
        }
    }
}
