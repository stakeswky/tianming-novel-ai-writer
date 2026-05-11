using System.Net.Http.Headers;
using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableLoginHistoryApiProtocolTests
{
    [Fact]
    public void BuildLoginHistoryPath_uses_original_page_query()
    {
        var path = PortableLoginHistoryApiProtocol.BuildLoginHistoryPath(2, 50);

        Assert.Equal("/api/account/login-history?page=2&pageSize=50", path);
    }

    [Fact]
    public void BuildLoginHistoryPath_clamps_non_positive_paging_values()
    {
        var path = PortableLoginHistoryApiProtocol.BuildLoginHistoryPath(0, -1);

        Assert.Equal("/api/account/login-history?page=1&pageSize=20", path);
    }

    [Fact]
    public void BuildGetLoginHistoryRequest_uses_original_path_and_bearer_header()
    {
        var request = PortableLoginHistoryApiProtocol.BuildGetLoginHistoryRequest(
            "https://api.example.com/",
            page: 2,
            pageSize: 50,
            new PortableAuthApiRequestOptions { RequiresAuth = true, AccessToken = "access-1" });

        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("https://api.example.com/api/account/login-history?page=2&pageSize=50", request.RequestUri!.ToString());
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "access-1"), request.Headers.Authorization);
        Assert.Null(request.Content);
    }

    [Fact]
    public void ParseResponse_maps_login_history_result_from_original_envelope()
    {
        var response = PortableLoginHistoryApiProtocol.ParseResponse<PortableLoginHistoryResult>(
            """
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
                    "deviceId": "device-1",
                    "result": "success",
                    "failReason": null,
                    "location": "上海"
                  }
                ],
                "totalCount": 1
              }
            }
            """);

        Assert.True(response.Success);
        Assert.Equal(1, response.Data!.TotalCount);
        var record = Assert.Single(response.Data.Records);
        Assert.Equal(7, record.LogId);
        Assert.Equal("10.0.0.1", record.IpAddress);
        Assert.Equal("success", record.Result);
    }
}
