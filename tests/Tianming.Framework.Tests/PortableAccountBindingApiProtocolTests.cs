using System.Net.Http.Headers;
using System.Text.Json;
using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableAccountBindingApiProtocolTests
{
    [Fact]
    public void BuildGetBindingsRequest_uses_original_path_and_bearer_header()
    {
        var request = PortableAccountBindingApiProtocol.BuildGetBindingsRequest(
            "https://api.example.com/",
            new PortableAuthApiRequestOptions { RequiresAuth = true, AccessToken = "access-1" });

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://api.example.com/api/account/bindings", request.RequestUri!.ToString());
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "access-1"), request.Headers.Authorization);
        Assert.Null(request.Content);
    }

    [Fact]
    public async Task BuildBindAccountRequest_uses_platform_path_and_oauth_json_fields()
    {
        var request = PortableAccountBindingApiProtocol.BuildBindAccountRequest(
            "https://api.example.com",
            "github",
            new PortableOAuthRequest { Platform = "github", Code = "code-1", State = "state-1" },
            new PortableAuthApiRequestOptions
            {
                RequiresAuth = true,
                AccessToken = "access-1",
                ClientId = "client-1"
            });

        var json = await request.Content!.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.example.com/api/account/bindings/github", request.RequestUri!.ToString());
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "access-1"), request.Headers.Authorization);
        Assert.Equal("client-1", request.Headers.GetValues("X-Client-Id").Single());
        Assert.Equal("github", document.RootElement.GetProperty("platform").GetString());
        Assert.Equal("code-1", document.RootElement.GetProperty("code").GetString());
        Assert.Equal("state-1", document.RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public void BuildUnbindAccountRequest_uses_delete_platform_path_without_body()
    {
        var request = PortableAccountBindingApiProtocol.BuildUnbindAccountRequest(
            "https://api.example.com",
            "github",
            new PortableAuthApiRequestOptions { RequiresAuth = true, AccessToken = "access-1" });

        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("https://api.example.com/api/account/bindings/github", request.RequestUri!.ToString());
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "access-1"), request.Headers.Authorization);
        Assert.Null(request.Content);
    }

    [Fact]
    public void BuildPlatformBindingPath_escapes_platform_segment()
    {
        var path = PortableAccountBindingApiProtocol.GetPlatformBindingPath("github enterprise");

        Assert.Equal("/api/account/bindings/github%20enterprise", path);
    }

    [Fact]
    public void ParseResponse_maps_bindings_result_from_original_envelope()
    {
        var response = PortableAccountBindingApiProtocol.ParseResponse<PortableBindingsResult>(
            """
            {
              "success": true,
              "data": {
                "bindings": [
                  {
                    "bindingId": 7,
                    "platform": "github",
                    "platformUserId": "gh-1",
                    "displayName": "Jimmy",
                    "boundTime": "2026-05-10T00:00:00Z"
                  }
                ]
              }
            }
            """);

        Assert.True(response.Success);
        Assert.Single(response.Data!.Bindings);
        Assert.Equal(7, response.Data.Bindings[0].BindingId);
        Assert.Equal("github", response.Data.Bindings[0].Platform);
        Assert.Equal("gh-1", response.Data.Bindings[0].PlatformUserId);
        Assert.Equal("Jimmy", response.Data.Bindings[0].DisplayName);
    }
}
