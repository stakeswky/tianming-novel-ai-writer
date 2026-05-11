using System.Text.Json;
using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableLoginHistoryStoreTests
{
    [Fact]
    public void Missing_file_returns_empty_history()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp);

        Assert.Empty(store.GetAllRecords());
        Assert.Null(store.GetLastLoginTime());
        Assert.Equal(0, store.GetAbnormalRecordsCount());
    }

    [Fact]
    public void RecordLogin_assesses_first_login_and_keeps_latest_first()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp);

        store.RecordLogin(CreateInput(id: "r1", sessionId: "s1", ip: "10.0.0.1"));

        var record = Assert.Single(store.GetAllRecords());
        Assert.Equal("r1", record.Id);
        Assert.Equal("s1", record.SessionId);
        Assert.Equal(Now, record.LoginTime);
        Assert.True(record.IsSuccess);
        Assert.False(record.IsAbnormal);
        Assert.Equal(PortableLoginRiskLevel.Low, record.RiskLevel);
        Assert.Equal("首次登录", record.RiskReason);
    }

    [Fact]
    public void RecordLogin_flags_new_ip_new_device_and_fast_location_change()
    {
        using var temp = new TempDirectory();
        var times = new Queue<DateTime>(new[] { Now, Now.AddMinutes(20) });
        var store = CreateStore(temp, () => times.Dequeue());
        store.RecordLogin(CreateInput(id: "r1", sessionId: "s1", ip: "10.0.0.1", deviceName: "MacBook", location: "上海"));

        store.RecordLogin(CreateInput(id: "r2", sessionId: "s2", ip: "10.0.0.2", deviceName: "iMac", location: "北京"));

        var record = store.GetAllRecords()[0];
        Assert.True(record.IsAbnormal);
        Assert.Equal(PortableLoginRiskLevel.Critical, record.RiskLevel);
        Assert.Contains("新IP地址", record.RiskReason);
        Assert.Contains("新设备", record.RiskReason);
        Assert.Contains("异地快速登录", record.RiskReason);
    }

    [Fact]
    public void RecordLogin_trims_local_history_to_latest_200()
    {
        using var temp = new TempDirectory();
        var counter = 0;
        var store = CreateStore(temp, () => Now.AddMinutes(counter++));

        for (var i = 0; i < 205; i++)
        {
            store.RecordLogin(CreateInput(id: $"r{i}", sessionId: $"s{i}", ip: "10.0.0.1"));
        }

        var records = store.GetAllRecords();
        Assert.Equal(200, records.Count);
        Assert.Equal("r204", records[0].Id);
        Assert.Equal("r5", records[^1].Id);
    }

    [Fact]
    public void SaveFromServer_merges_records_and_preserves_local_enrichment()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp);
        store.RecordLogin(CreateInput(
            id: "local-1",
            sessionId: "local-session",
            ip: "10.0.0.1",
            location: "",
            riskReason: ""));

        var merged = store.SaveFromServer(new PortableLoginHistoryResult
        {
            Records =
            {
                new PortableLoginLogDto
                {
                    LogId = 7,
                    LoginTime = Now,
                    IpAddress = "10.0.0.1",
                    UserAgent = "server-agent",
                    Result = "success",
                    FailReason = "server-risk",
                    Location = "上海"
                },
                new PortableLoginLogDto
                {
                    LogId = 8,
                    LoginTime = Now.AddHours(-2),
                    IpAddress = "10.0.0.2",
                    UserAgent = "server-agent-2",
                    Result = "failed",
                    FailReason = "密码错误",
                    Location = "北京"
                }
            }
        });

        Assert.Equal(2, merged.Count);
        var matched = merged.Single(record => record.Id == "local-1");
        Assert.Equal("上海", matched.Location);
        Assert.Equal("server-risk", matched.RiskReason);
        Assert.Equal("local", matched.ExtendedInfo["source"]);
        var serverOnly = merged.Single(record => record.Id == "srv:8");
        Assert.False(serverOnly.IsSuccess);
        Assert.Equal("server", serverOnly.ExtendedInfo["source"]);
    }

    [Fact]
    public void GetFilteredRecords_filters_by_date_range_and_device_type()
    {
        using var temp = new TempDirectory();
        var counter = 0;
        var store = CreateStore(temp, () => Now.AddHours(counter++));
        store.RecordLogin(CreateInput(id: "r1", sessionId: "s1", ip: "10.0.0.1", deviceType: "macOS"));
        store.RecordLogin(CreateInput(id: "r2", sessionId: "s2", ip: "10.0.0.2", deviceType: "iOS"));
        store.RecordLogin(CreateInput(id: "r3", sessionId: "s3", ip: "10.0.0.3", deviceType: "macOS"));

        var records = store.GetFilteredRecords(Now.AddMinutes(30), Now.AddHours(3), "macOS");

        var record = Assert.Single(records);
        Assert.Equal("r3", record.Id);
    }

    [Fact]
    public void GetStatistics_matches_original_counts_and_security_score_shape()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp);
        store.SaveRecords(new[]
        {
            CreateRecord("r1", Now, "10.0.0.1", "macOS", "MacBook", "上海", true, false, PortableLoginRiskLevel.Low),
            CreateRecord("r2", Now.AddMinutes(-1), "10.0.0.2", "macOS", "iMac", "北京", false, true, PortableLoginRiskLevel.High),
            CreateRecord("r3", Now.AddMinutes(-2), "10.0.0.2", "iOS", "iPhone", "北京", true, true, PortableLoginRiskLevel.Critical)
        });

        var stats = store.GetStatistics();

        Assert.Equal(3, stats.TotalLogins);
        Assert.Equal(2, stats.SuccessfulLogins);
        Assert.Equal(1, stats.FailedLogins);
        Assert.Equal(2, stats.AbnormalLogins);
        Assert.Equal(2, stats.UniqueIPs);
        Assert.Equal(3, stats.UniqueDevices);
        Assert.Equal(1, stats.LocationDistribution["上海"]);
        Assert.True(stats.SecurityScore < 100);
    }

    [Fact]
    public void Active_sessions_and_end_session_follow_original_24_hour_window()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp);
        store.SaveRecords(new[]
        {
            CreateRecord("r1", Now.AddHours(-2), "10.0.0.1", "macOS", "MacBook", "上海", true, false, PortableLoginRiskLevel.Low, sessionId: "s1"),
            CreateRecord("r2", Now.AddHours(-25), "10.0.0.2", "macOS", "iMac", "上海", true, false, PortableLoginRiskLevel.Low, sessionId: "s2")
        });

        Assert.Equal("s1", Assert.Single(store.GetActiveSessions()).SessionId);

        Assert.True(store.EndSession("s1"));

        var ended = store.GetAllRecords().Single(record => record.SessionId == "s1");
        Assert.Equal(Now, ended.LogoutTime);
        Assert.Equal(7200, ended.SessionDuration);
        Assert.Empty(store.GetActiveSessions());
    }

    [Fact]
    public void ClearHistory_deletes_all_records()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp);
        store.RecordLogin(CreateInput(id: "r1", sessionId: "s1", ip: "10.0.0.1"));

        store.ClearHistory();

        Assert.Empty(store.GetAllRecords());
    }

    [Fact]
    public void Invalid_json_recovers_with_empty_history()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "login_history.json"), "{bad json");

        var store = CreateStore(temp);

        Assert.Empty(store.GetAllRecords());
    }

    [Fact]
    public void Saved_json_uses_original_pascal_case_fields()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp);

        store.RecordLogin(CreateInput(id: "r1", sessionId: "s1", ip: "10.0.0.1"));

        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(temp.Path, "login_history.json")));
        var record = document.RootElement.GetProperty("Records")[0];
        Assert.Equal("r1", record.GetProperty("Id").GetString());
        Assert.Equal("10.0.0.1", record.GetProperty("IpAddress").GetString());
        Assert.Equal("Low", record.GetProperty("RiskLevel").GetString());
        Assert.True(record.GetProperty("IsSuccess").GetBoolean());
    }

    private static readonly DateTime Now = new(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);

    private static PortableLoginHistoryStore CreateStore(TempDirectory temp, Func<DateTime>? now = null)
    {
        return new PortableLoginHistoryStore(Path.Combine(temp.Path, "login_history.json"), now ?? (() => Now));
    }

    private static PortableLoginRecordInput CreateInput(
        string id,
        string sessionId,
        string ip,
        string deviceType = "macOS",
        string deviceName = "MacBook",
        string location = "上海",
        string riskReason = "",
        bool isSuccess = true)
    {
        return new PortableLoginRecordInput
        {
            Id = id,
            SessionId = sessionId,
            IpAddress = ip,
            DeviceType = deviceType,
            DeviceName = deviceName,
            Location = location,
            Browser = "天命客户端",
            OperatingSystem = "macOS",
            IsSuccess = isSuccess,
            RiskReason = riskReason
        };
    }

    private static PortableLoginRecord CreateRecord(
        string id,
        DateTime loginTime,
        string ip,
        string deviceType,
        string deviceName,
        string location,
        bool isSuccess,
        bool isAbnormal,
        PortableLoginRiskLevel riskLevel,
        string sessionId = "")
    {
        return new PortableLoginRecord
        {
            Id = id,
            LoginTime = loginTime,
            IpAddress = ip,
            DeviceType = deviceType,
            DeviceName = deviceName,
            Location = location,
            Browser = "天命客户端",
            OperatingSystem = "macOS",
            IsSuccess = isSuccess,
            IsAbnormal = isAbnormal,
            SessionId = sessionId,
            RiskLevel = riskLevel,
            RiskReason = riskLevel == PortableLoginRiskLevel.Low ? "正常" : "风险"
        };
    }
}
