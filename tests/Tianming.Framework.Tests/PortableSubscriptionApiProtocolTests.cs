using System.Net.Http.Headers;
using System.Text.Json;
using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableSubscriptionApiProtocolTests
{
    [Fact]
    public void BuildGetSubscriptionRequest_uses_original_path_and_bearer_header()
    {
        var request = PortableSubscriptionApiProtocol.BuildGetSubscriptionRequest(
            "https://api.example.com/",
            new PortableAuthApiRequestOptions { RequiresAuth = true, AccessToken = "access-1" });

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://api.example.com/api/subscription", request.RequestUri!.ToString());
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "access-1"), request.Headers.Authorization);
        Assert.Null(request.Content);
    }

    [Fact]
    public async Task BuildActivateCardKeyRequest_uses_original_path_and_card_key_json_field()
    {
        var request = PortableSubscriptionApiProtocol.BuildActivateCardKeyRequest(
            "https://api.example.com",
            new PortableActivateCardKeyRequest { CardKey = "CARD-001" },
            new PortableAuthApiRequestOptions { ClientId = "client-1" });

        var json = await request.Content!.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.example.com/api/subscription/activate", request.RequestUri!.ToString());
        Assert.Equal("client-1", request.Headers.GetValues("X-Client-Id").Single());
        Assert.Equal("CARD-001", document.RootElement.GetProperty("cardKey").GetString());
    }

    [Fact]
    public void BuildActivationHistoryRequest_uses_original_history_path()
    {
        var request = PortableSubscriptionApiProtocol.BuildActivationHistoryRequest(
            "https://api.example.com",
            new PortableAuthApiRequestOptions { RequiresAuth = true, AccessToken = "access-1" });

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://api.example.com/api/subscription/history", request.RequestUri!.ToString());
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "access-1"), request.Headers.Authorization);
    }

    [Fact]
    public async Task BuildRenewAccountRequest_uses_original_public_renew_path_and_no_auth_header()
    {
        var request = PortableSubscriptionApiProtocol.BuildRenewAccountRequest(
            "https://api.example.com",
            new PortableRenewAccountRequest { Account = "jimmy", CardKey = "CARD-002" },
            new PortableAuthApiRequestOptions { RequiresAuth = false, AccessToken = "access-1" });

        var json = await request.Content!.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.example.com/api/subscription/renew", request.RequestUri!.ToString());
        Assert.Null(request.Headers.Authorization);
        Assert.Equal("jimmy", document.RootElement.GetProperty("account").GetString());
        Assert.Equal("CARD-002", document.RootElement.GetProperty("cardKey").GetString());
    }

    [Fact]
    public void ParseResponse_maps_subscription_and_activation_result_from_original_envelope()
    {
        var subscriptionJson = """
        {
          "success": true,
          "data": {
            "subscriptionId": 12,
            "userId": "u1",
            "planType": "pro",
            "startTime": "2026-05-01T00:00:00Z",
            "endTime": "2026-06-01T00:00:00Z",
            "isActive": true,
            "remainingDays": 22,
            "source": "card_key"
          }
        }
        """;
        var activationJson = """
        {
          "success": true,
          "message": "ok",
          "data": {
            "success": true,
            "daysAdded": 30,
            "newExpireTime": "2026-06-10T00:00:00Z",
            "subscription": {
              "subscriptionId": 12,
              "userId": "u1",
              "planType": "pro",
              "isActive": true
            }
          }
        }
        """;

        var subscription = PortableSubscriptionApiProtocol.ParseResponse<PortableSubscriptionInfo>(subscriptionJson);
        var activation = PortableSubscriptionApiProtocol.ParseResponse<PortableActivationResult>(activationJson);

        Assert.True(subscription.Success);
        Assert.Equal(12, subscription.Data!.SubscriptionId);
        Assert.Equal("pro", subscription.Data.PlanType);
        Assert.Equal(22, subscription.Data.RemainingDays);
        Assert.Equal("card_key", subscription.Data.Source);
        Assert.True(activation.Success);
        Assert.Equal(30, activation.Data!.DaysAdded);
        Assert.True(activation.Data.Subscription.IsActive);
    }

    [Fact]
    public void ParseResponse_maps_activation_history_records()
    {
        var response = PortableSubscriptionApiProtocol.ParseResponse<PortableActivationHistoryResult>(
            """
            {
              "success": true,
              "data": {
                "records": [
                  {
                    "cardKey": "CARD-001",
                    "durationDays": 30,
                    "activatedTime": "2026-05-10T00:00:00Z"
                  }
                ]
              }
            }
            """);

        Assert.True(response.Success);
        Assert.Single(response.Data!.Records);
        Assert.Equal("CARD-001", response.Data.Records[0].CardKey);
        Assert.Equal(30, response.Data.Records[0].DurationDays);
    }
}
