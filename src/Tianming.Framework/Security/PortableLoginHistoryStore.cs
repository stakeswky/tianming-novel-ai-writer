using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.Security;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PortableLoginRiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

public sealed class PortableLoginHistoryData
{
    [JsonPropertyName("Records")] public List<PortableLoginRecord> Records { get; set; } = new();
}

public sealed class PortableLoginRecord
{
    [JsonPropertyName("Id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("LoginTime")] public DateTime LoginTime { get; set; }
    [JsonPropertyName("IpAddress")] public string IpAddress { get; set; } = string.Empty;
    [JsonPropertyName("DeviceType")] public string DeviceType { get; set; } = string.Empty;
    [JsonPropertyName("DeviceName")] public string DeviceName { get; set; } = string.Empty;
    [JsonPropertyName("Location")] public string Location { get; set; } = string.Empty;
    [JsonPropertyName("Browser")] public string Browser { get; set; } = string.Empty;
    [JsonPropertyName("OperatingSystem")] public string OperatingSystem { get; set; } = string.Empty;
    [JsonPropertyName("IsSuccess")] public bool IsSuccess { get; set; }
    [JsonPropertyName("IsAbnormal")] public bool IsAbnormal { get; set; }
    [JsonPropertyName("SessionId")] public string SessionId { get; set; } = string.Empty;
    [JsonPropertyName("LogoutTime")] public DateTime? LogoutTime { get; set; }
    [JsonPropertyName("SessionDuration")] public int SessionDuration { get; set; }
    [JsonPropertyName("RiskLevel")] public PortableLoginRiskLevel RiskLevel { get; set; } = PortableLoginRiskLevel.Low;
    [JsonPropertyName("RiskReason")] public string RiskReason { get; set; } = string.Empty;
    [JsonPropertyName("ExtendedInfo")] public Dictionary<string, string> ExtendedInfo { get; set; } = new();
}

public sealed class PortableLoginRecordInput
{
    public string Id { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Browser { get; set; } = string.Empty;
    public string OperatingSystem { get; set; } = string.Empty;
    public bool IsSuccess { get; set; } = true;
    public string RiskReason { get; set; } = string.Empty;
}

public sealed class PortableLoginLogDto
{
    [JsonPropertyName("logId")] public long LogId { get; set; }
    [JsonPropertyName("userId")] public string UserId { get; set; } = string.Empty;
    [JsonPropertyName("loginTime")] public DateTime LoginTime { get; set; }
    [JsonPropertyName("ipAddress")] public string? IpAddress { get; set; }
    [JsonPropertyName("userAgent")] public string? UserAgent { get; set; }
    [JsonPropertyName("deviceId")] public string? DeviceId { get; set; }
    [JsonPropertyName("result")] public string Result { get; set; } = string.Empty;
    [JsonPropertyName("failReason")] public string? FailReason { get; set; }
    [JsonPropertyName("location")] public string? Location { get; set; }
}

public sealed class PortableLoginHistoryResult
{
    [JsonPropertyName("records")] public List<PortableLoginLogDto> Records { get; set; } = new();
    [JsonPropertyName("totalCount")] public int TotalCount { get; set; }
}

public static class PortableLoginHistoryApiProtocol
{
    public const string LoginHistoryPath = "/api/account/login-history";

    public static string BuildLoginHistoryPath(int page = 1, int pageSize = 20)
    {
        var normalizedPage = page <= 0 ? 1 : page;
        var normalizedPageSize = pageSize <= 0 ? 20 : pageSize;
        return $"{LoginHistoryPath}?page={normalizedPage}&pageSize={normalizedPageSize}";
    }

    public static HttpRequestMessage BuildGetLoginHistoryRequest(
        string baseUrl,
        int page,
        int pageSize,
        PortableAuthApiRequestOptions options)
    {
        return PortableAuthApiProtocol.BuildJsonRequest(
            HttpMethod.Get,
            baseUrl,
            BuildLoginHistoryPath(page, pageSize),
            body: null,
            options);
    }

    public static PortableApiResponse<T> ParseResponse<T>(string json)
    {
        return PortableAuthApiProtocol.ParseResponse<T>(json);
    }
}

public sealed class PortableLoginStatistics
{
    [JsonPropertyName("TotalLogins")] public int TotalLogins { get; set; }
    [JsonPropertyName("SuccessfulLogins")] public int SuccessfulLogins { get; set; }
    [JsonPropertyName("FailedLogins")] public int FailedLogins { get; set; }
    [JsonPropertyName("AbnormalLogins")] public int AbnormalLogins { get; set; }
    [JsonPropertyName("UniqueIPs")] public int UniqueIPs { get; set; }
    [JsonPropertyName("UniqueDevices")] public int UniqueDevices { get; set; }
    [JsonPropertyName("LocationDistribution")] public Dictionary<string, int> LocationDistribution { get; set; } = new();
    [JsonPropertyName("HourlyDistribution")] public Dictionary<int, int> HourlyDistribution { get; set; } = new();
    [JsonPropertyName("DeviceTypeDistribution")] public Dictionary<string, int> DeviceTypeDistribution { get; set; } = new();
    [JsonPropertyName("SecurityScore")] public int SecurityScore { get; set; }
}

public sealed class PortableLoginHistoryStore
{
    private const int MaxLocalRecords = 200;
    private const int MaxMergedRecords = 400;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _filePath;
    private readonly Func<DateTime> _now;
    private PortableLoginHistoryData _data;

    public PortableLoginHistoryStore(string filePath, Func<DateTime> now)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("Login history file path cannot be empty.", nameof(filePath))
            : filePath;
        _now = now ?? throw new ArgumentNullException(nameof(now));
        _data = Load();
    }

    public void RecordLogin(PortableLoginRecordInput input)
    {
        var record = new PortableLoginRecord
        {
            Id = input.Id,
            LoginTime = _now(),
            IpAddress = input.IpAddress,
            DeviceType = input.DeviceType,
            DeviceName = input.DeviceName,
            Location = input.Location,
            Browser = input.Browser,
            OperatingSystem = input.OperatingSystem,
            IsSuccess = input.IsSuccess,
            SessionId = input.SessionId,
            RiskReason = input.RiskReason
        };

        var risk = AssessLoginRisk(record);
        record.IsAbnormal = risk.IsAbnormal;
        record.RiskLevel = risk.RiskLevel;
        record.RiskReason = risk.Reason;

        _data.Records.Insert(0, record);
        if (_data.Records.Count > MaxLocalRecords)
        {
            _data.Records = _data.Records.Take(MaxLocalRecords).ToList();
        }

        Save();
    }

    public void SaveRecords(IEnumerable<PortableLoginRecord> records)
    {
        _data = new PortableLoginHistoryData
        {
            Records = records.Select(CloneRecord).ToList()
        };
        Save();
    }

    public List<PortableLoginRecord> SaveFromServer(PortableLoginHistoryResult result)
    {
        var local = GetAllRecords();
        var server = result.Records.Select(log =>
        {
            var record = new PortableLoginRecord
            {
                Id = $"srv:{log.LogId}",
                LoginTime = log.LoginTime,
                IpAddress = log.IpAddress ?? string.Empty,
                DeviceType = string.Empty,
                DeviceName = string.Empty,
                Location = log.Location ?? string.Empty,
                Browser = log.UserAgent ?? string.Empty,
                OperatingSystem = string.Empty,
                IsSuccess = string.Equals(log.Result, "success", StringComparison.OrdinalIgnoreCase),
                RiskReason = log.FailReason ?? string.Empty
            };
            record.ExtendedInfo["source"] = "server";
            return record;
        }).ToList();

        foreach (var record in local)
        {
            record.ExtendedInfo["source"] = "local";
        }

        static string Key(PortableLoginRecord record)
        {
            return $"{record.LoginTime:O}|{record.IpAddress}|{record.IsSuccess}";
        }

        var localMap = local.GroupBy(Key).ToDictionary(group => group.Key, group => group.First());
        var serverMap = server.GroupBy(Key).ToDictionary(group => group.Key, group => group.First());
        var merged = new List<PortableLoginRecord>();

        foreach (var item in serverMap)
        {
            if (localMap.TryGetValue(item.Key, out var localItem))
            {
                if (string.IsNullOrWhiteSpace(localItem.Location))
                {
                    localItem.Location = item.Value.Location;
                }

                if (string.IsNullOrWhiteSpace(localItem.RiskReason)
                    || localItem.RiskReason == "首次登录"
                    || localItem.RiskReason == "正常")
                {
                    localItem.RiskReason = item.Value.RiskReason;
                }

                merged.Add(localItem);
            }
            else
            {
                merged.Add(item.Value);
            }
        }

        foreach (var item in localMap)
        {
            if (!serverMap.ContainsKey(item.Key))
            {
                merged.Add(item.Value);
            }
        }

        merged = merged
            .OrderByDescending(record => record.LoginTime)
            .Take(MaxMergedRecords)
            .Select(CloneRecord)
            .ToList();
        _data = new PortableLoginHistoryData { Records = merged };
        Save();
        return GetAllRecords();
    }

    public List<PortableLoginRecord> GetAllRecords()
    {
        return _data.Records.Select(CloneRecord).ToList();
    }

    public List<PortableLoginRecord> GetFilteredRecords(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? deviceType = null)
    {
        var records = _data.Records.AsEnumerable();
        if (startDate.HasValue)
        {
            records = records.Where(record => record.LoginTime >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            records = records.Where(record => record.LoginTime <= endDate.Value);
        }

        if (!string.IsNullOrEmpty(deviceType))
        {
            records = records.Where(record => record.DeviceType == deviceType);
        }

        return records.Select(CloneRecord).ToList();
    }

    public int GetAbnormalRecordsCount()
    {
        return _data.Records.Count(record => record.IsAbnormal);
    }

    public DateTime? GetLastLoginTime()
    {
        return _data.Records.FirstOrDefault()?.LoginTime;
    }

    public void ClearHistory()
    {
        _data = new PortableLoginHistoryData();
        Save();
    }

    public PortableLoginStatistics GetStatistics(DateTime? startDate = null, DateTime? endDate = null)
    {
        var records = GetFilteredRecords(startDate, endDate);
        var stats = new PortableLoginStatistics
        {
            TotalLogins = records.Count,
            SuccessfulLogins = records.Count(record => record.IsSuccess),
            FailedLogins = records.Count(record => !record.IsSuccess),
            AbnormalLogins = records.Count(record => record.IsAbnormal),
            UniqueIPs = records.Select(record => record.IpAddress).Distinct().Count(),
            UniqueDevices = records.Select(record => record.DeviceName).Distinct().Count(),
            LocationDistribution = records.GroupBy(record => record.Location).ToDictionary(group => group.Key, group => group.Count()),
            HourlyDistribution = records.GroupBy(record => record.LoginTime.Hour).ToDictionary(group => group.Key, group => group.Count()),
            DeviceTypeDistribution = records.GroupBy(record => record.DeviceType).ToDictionary(group => group.Key, group => group.Count())
        };
        stats.SecurityScore = CalculateSecurityScore(records);
        return stats;
    }

    public List<PortableLoginRecord> GetRiskRecords(PortableLoginRiskLevel minRiskLevel = PortableLoginRiskLevel.Medium)
    {
        return _data.Records
            .Where(record => record.RiskLevel >= minRiskLevel)
            .Select(CloneRecord)
            .ToList();
    }

    public List<PortableLoginRecord> GetActiveSessions()
    {
        var now = _now();
        return _data.Records
            .Where(record => record.LogoutTime == null && (now - record.LoginTime).TotalHours < 24)
            .Select(CloneRecord)
            .ToList();
    }

    public bool EndSession(string sessionId)
    {
        var record = _data.Records.FirstOrDefault(item => item.SessionId == sessionId);
        if (record == null)
        {
            return false;
        }

        var now = _now();
        record.LogoutTime = now;
        record.SessionDuration = (int)(now - record.LoginTime).TotalSeconds;
        Save();
        return true;
    }

    private (bool IsAbnormal, PortableLoginRiskLevel RiskLevel, string Reason) AssessLoginRisk(
        PortableLoginRecord currentRecord)
    {
        var records = _data.Records;
        if (records.Count == 0)
        {
            return (false, PortableLoginRiskLevel.Low, "首次登录");
        }

        var reasons = new List<string>();
        var riskScore = 0;
        var hour = currentRecord.LoginTime.Hour;
        if (hour >= 2 && hour < 6)
        {
            reasons.Add("深夜登录");
            riskScore += 20;
        }

        var recentRecords = records.Take(10).ToList();
        var recentIps = recentRecords.Select(record => record.IpAddress).Distinct().ToList();
        if (!recentIps.Contains(currentRecord.IpAddress))
        {
            reasons.Add("新IP地址");
            riskScore += 30;
        }

        var recentDevices = recentRecords.Select(record => record.DeviceName).Distinct().ToList();
        if (!recentDevices.Contains(currentRecord.DeviceName))
        {
            reasons.Add("新设备");
            riskScore += 25;
        }

        var lastRecord = records.FirstOrDefault();
        if (lastRecord != null && lastRecord.Location != currentRecord.Location)
        {
            var timeDiff = (currentRecord.LoginTime - lastRecord.LoginTime).TotalHours;
            if (timeDiff < 1)
            {
                reasons.Add("异地快速登录");
                riskScore += 40;
            }
        }

        var recentHourLogins = records.Count(record => (currentRecord.LoginTime - record.LoginTime).TotalHours < 1);
        if (recentHourLogins > 5)
        {
            reasons.Add("频繁登录");
            riskScore += 15;
        }

        var riskLevel = riskScore switch
        {
            >= 60 => PortableLoginRiskLevel.Critical,
            >= 40 => PortableLoginRiskLevel.High,
            >= 20 => PortableLoginRiskLevel.Medium,
            _ => PortableLoginRiskLevel.Low
        };
        return (riskScore >= 30, riskLevel, reasons.Count > 0 ? string.Join(", ", reasons) : "正常");
    }

    private static int CalculateSecurityScore(List<PortableLoginRecord> records)
    {
        if (records.Count == 0)
        {
            return 100;
        }

        var score = 100;
        var abnormalRate = (double)records.Count(record => record.IsAbnormal) / records.Count;
        score -= (int)(abnormalRate * 30);

        var highRiskCount = records.Count(record => record.RiskLevel >= PortableLoginRiskLevel.High);
        score -= highRiskCount * 5;

        var uniqueIps = records.Select(record => record.IpAddress).Distinct().Count();
        if (uniqueIps > records.Count / 2)
        {
            score -= 15;
        }

        var uniqueDevices = records.Select(record => record.DeviceName).Distinct().Count();
        if (uniqueDevices > 3)
        {
            score -= 10;
        }

        return Math.Max(0, Math.Min(100, score));
    }

    private PortableLoginHistoryData Load()
    {
        if (!File.Exists(_filePath))
        {
            return new PortableLoginHistoryData();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<PortableLoginHistoryData>(json, JsonOptions) ?? new PortableLoginHistoryData();
        }
        catch (JsonException)
        {
            return new PortableLoginHistoryData();
        }
        catch (IOException)
        {
            return new PortableLoginHistoryData();
        }
    }

    private void Save()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _filePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(_data, JsonOptions));
        File.Move(tempPath, _filePath, overwrite: true);
    }

    private static PortableLoginRecord CloneRecord(PortableLoginRecord record)
    {
        return new PortableLoginRecord
        {
            Id = record.Id,
            LoginTime = record.LoginTime,
            IpAddress = record.IpAddress,
            DeviceType = record.DeviceType,
            DeviceName = record.DeviceName,
            Location = record.Location,
            Browser = record.Browser,
            OperatingSystem = record.OperatingSystem,
            IsSuccess = record.IsSuccess,
            IsAbnormal = record.IsAbnormal,
            SessionId = record.SessionId,
            LogoutTime = record.LogoutTime,
            SessionDuration = record.SessionDuration,
            RiskLevel = record.RiskLevel,
            RiskReason = record.RiskReason,
            ExtendedInfo = new Dictionary<string, string>(record.ExtendedInfo)
        };
    }
}
