using System.Net.Http.Headers;
using System.Text.Json;
using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableAuthApiProtocolTests
{
    [Fact]
    public async Task BuildLoginRequest_uses_original_path_json_fields_and_client_header()
    {
        var request = PortableAuthApiProtocol.BuildJsonRequest(
            HttpMethod.Post,
            "https://api.example.com/",
            PortableAuthApiProtocol.LoginPath,
            new PortableLoginRequest { Username = "jimmy", Password = "secret" },
            new PortableAuthApiRequestOptions
            {
                ClientId = "client-1",
                UserAgent = "TianMing/1.0"
            });

        var json = await request.Content!.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal("https://api.example.com/api/auth/login", request.RequestUri!.ToString());
        Assert.Equal("client-1", request.Headers.GetValues("X-Client-Id").Single());
        Assert.Equal("TianMing/1.0", request.Headers.UserAgent.ToString());
        Assert.Equal("jimmy", document.RootElement.GetProperty("username").GetString());
        Assert.Equal("secret", document.RootElement.GetProperty("password").GetString());
    }

    [Fact]
    public async Task BuildRegisterRequest_uses_original_register_path_and_card_key_field()
    {
        var request = PortableAuthApiProtocol.BuildJsonRequest(
            HttpMethod.Post,
            "https://api.example.com",
            PortableAuthApiProtocol.RegisterPath,
            new PortableRegisterRequest
            {
                Username = "new-user",
                Password = "secret",
                Email = "user@example.com",
                CardKey = "CARD-001"
            },
            new PortableAuthApiRequestOptions());

        var json = await request.Content!.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal("https://api.example.com/api/auth/register", request.RequestUri!.ToString());
        Assert.Equal("CARD-001", document.RootElement.GetProperty("cardKey").GetString());
        Assert.Equal("user@example.com", document.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task BuildOAuthLoginRequest_uses_platform_path_and_no_auth_header_by_default()
    {
        var request = PortableAuthApiProtocol.BuildJsonRequest(
            HttpMethod.Post,
            "https://api.example.com",
            PortableAuthApiProtocol.GetOAuthLoginPath("github"),
            new PortableOAuthRequest { Platform = "github", Code = "code-1", State = "state-1" },
            new PortableAuthApiRequestOptions { AccessToken = "token", RequiresAuth = false });

        var json = await request.Content!.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal("https://api.example.com/api/auth/oauth/github", request.RequestUri!.ToString());
        Assert.Null(request.Headers.Authorization);
        Assert.Equal("github", document.RootElement.GetProperty("platform").GetString());
        Assert.Equal("code-1", document.RootElement.GetProperty("code").GetString());
        Assert.Equal("state-1", document.RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public void BuildJsonRequest_adds_bearer_header_when_auth_is_required()
    {
        var request = PortableAuthApiProtocol.BuildJsonRequest(
            HttpMethod.Get,
            "https://api.example.com",
            "/api/account/profile",
            body: null,
            new PortableAuthApiRequestOptions { RequiresAuth = true, AccessToken = "access-1" });

        Assert.Equal(new AuthenticationHeaderValue("Bearer", "access-1"), request.Headers.Authorization);
        Assert.Null(request.Content);
    }

    [Fact]
    public void ParseResponse_maps_login_result_from_original_api_envelope()
    {
        var json = """
        {
          "success": true,
          "message": "ok",
          "data": {
            "accessToken": "access-1",
            "refreshToken": "refresh-1",
            "sessionKey": "session-1",
            "expiresAt": "2026-05-10T12:00:00Z",
            "user": {
              "userId": "u1",
              "username": "jimmy",
              "email": "jimmy@example.com"
            }
          }
        }
        """;

        var response = PortableAuthApiProtocol.ParseResponse<PortableLoginResult>(json);

        Assert.True(response.Success);
        Assert.Equal("ok", response.Message);
        Assert.Equal("access-1", response.Data!.AccessToken);
        Assert.Equal("refresh-1", response.Data.RefreshToken);
        Assert.Equal("session-1", response.Data.SessionKey);
        Assert.Equal("jimmy", response.Data.User.Username);
    }

    [Fact]
    public void ParseResponse_maps_error_envelope_without_data()
    {
        var response = PortableAuthApiProtocol.ParseResponse<PortableLoginResult>(
            """{"success":false,"message":"账号被禁用","errorCode":"ACCOUNT_DISABLED","traceId":"t1"}""");

        Assert.False(response.Success);
        Assert.Equal("账号被禁用", response.Message);
        Assert.Equal("ACCOUNT_DISABLED", response.ErrorCode);
        Assert.Equal("t1", response.TraceId);
        Assert.Null(response.Data);
    }
}
