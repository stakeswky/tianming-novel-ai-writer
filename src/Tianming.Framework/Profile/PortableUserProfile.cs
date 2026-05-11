using System.Text.Json;
using System.Text.Json.Serialization;
using TM.Framework.Security;

namespace TM.Framework.Profile;

public sealed class PortableUserProfileData
{
    [JsonPropertyName("Username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("DisplayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("RealName")]
    public string RealName { get; set; } = string.Empty;

    [JsonPropertyName("Gender")]
    public string Gender { get; set; } = "保密";

    [JsonPropertyName("Email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("Phone")]
    public string Phone { get; set; } = string.Empty;

    [JsonPropertyName("Country")]
    public string Country { get; set; } = "中国";

    [JsonPropertyName("Province")]
    public string Province { get; set; } = string.Empty;

    [JsonPropertyName("City")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("AvatarPath")]
    public string AvatarPath { get; set; } = string.Empty;

    [JsonPropertyName("Bio")]
    public string Bio { get; set; } = string.Empty;

    [JsonPropertyName("Birthday")]
    public DateTime? Birthday { get; set; }

    [JsonPropertyName("CreatedTime")]
    public DateTime CreatedTime { get; set; } = DateTime.Now;

    [JsonPropertyName("LastUpdatedTime")]
    public DateTime LastUpdatedTime { get; set; } = DateTime.Now;

    public static PortableUserProfileData CreateDefault(DateTime? now = null, string? username = null)
    {
        var timestamp = now ?? DateTime.Now;
        return new PortableUserProfileData
        {
            Username = string.IsNullOrWhiteSpace(username) ? CreateDefaultUsername(timestamp) : username,
            DisplayName = string.Empty,
            RealName = string.Empty,
            Gender = "保密",
            Email = string.Empty,
            Phone = string.Empty,
            Country = "中国",
            Province = string.Empty,
            City = string.Empty,
            AvatarPath = string.Empty,
            Bio = string.Empty,
            Birthday = null,
            CreatedTime = timestamp,
            LastUpdatedTime = timestamp
        };
    }

    private static string CreateDefaultUsername(DateTime timestamp)
    {
        var ticks = timestamp.Ticks.ToString();
        return "User_" + (ticks.Length > 8 ? ticks[8..] : ticks);
    }
}

public sealed class FileUserProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly char[] WindowsInvalidFileNameChars =
    [
        '<', '>', ':', '"', '/', '\\', '|', '?', '*'
    ];

    private readonly string _root;
    private readonly Func<DateTime> _clock;

    public FileUserProfileStore(string root, Func<DateTime>? clock = null)
    {
        _root = root;
        _clock = clock ?? (() => DateTime.Now);
        DefaultProfilePath = Path.Combine(_root, "Framework", "User", "Profile", "BasicInfo", "user_profile.json");
        AvatarDirectory = Path.Combine(_root, "Framework", "User", "Profile", "BasicInfo", "Avatars");
        AvatarPath = Path.Combine(AvatarDirectory, "avatar.png");
    }

    public string DefaultProfilePath { get; }

    public string AvatarDirectory { get; }

    public string AvatarPath { get; }

    public string GetUserProfilePath(string username)
    {
        var safe = SanitizeFileName(username).ToLowerInvariant();
        return Path.Combine(_root, "Framework", "User", "Profile", "BasicInfo", "Profiles", $"{safe}.json");
    }

    public async Task<PortableUserProfileData> EnsureProfileExistsAsync(
        string username,
        CancellationToken cancellationToken = default)
    {
        var normalized = string.IsNullOrWhiteSpace(username) ? string.Empty : username.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return PortableUserProfileData.CreateDefault(_clock());

        var path = GetUserProfilePath(normalized);
        if (File.Exists(path))
            return await LoadAsync(path, cancellationToken).ConfigureAwait(false);

        var now = _clock();
        var profile = PortableUserProfileData.CreateDefault(now, normalized);
        profile.DisplayName = "用户";
        await SaveAsync(profile, path, cancellationToken).ConfigureAwait(false);
        return profile;
    }

    public async Task<PortableUserProfileData> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(path))
                return PortableUserProfileData.CreateDefault(_clock());

            await using var stream = File.OpenRead(path);
            var profile = await JsonSerializer.DeserializeAsync<PortableUserProfileData>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            return profile ?? PortableUserProfileData.CreateDefault(_clock());
        }
        catch (JsonException)
        {
            return PortableUserProfileData.CreateDefault(_clock());
        }
        catch (IOException)
        {
            return PortableUserProfileData.CreateDefault(_clock());
        }
        catch (UnauthorizedAccessException)
        {
            return PortableUserProfileData.CreateDefault(_clock());
        }
    }

    public async Task SaveAsync(
        PortableUserProfileData profile,
        string path,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        profile.LastUpdatedTime = _clock();
        var tempPath = path + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, profile, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, path, overwrite: true);
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "user";

        var result = value.Trim();
        foreach (var c in Path.GetInvalidFileNameChars().Concat(WindowsInvalidFileNameChars).Distinct())
        {
            result = result.Replace(c, '_');
        }

        return string.IsNullOrWhiteSpace(result) ? "user" : result;
    }
}

public sealed class PortableServerUserProfile
{
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "active";

    [JsonPropertyName("registerTime")]
    public DateTime RegisterTime { get; set; }

    [JsonPropertyName("lastLoginTime")]
    public DateTime? LastLoginTime { get; set; }

    [JsonPropertyName("avatarUrl")]
    public string? AvatarUrl { get; set; }

    [JsonPropertyName("bio")]
    public string? Bio { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("birthday")]
    public DateTime? Birthday { get; set; }

    [JsonPropertyName("gender")]
    public string? Gender { get; set; }
}

public sealed class PortablePendingProfileSync
{
    [JsonPropertyName("DisplayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("Email")]
    public string? Email { get; set; }

    [JsonPropertyName("Gender")]
    public string Gender { get; set; } = "保密";

    [JsonPropertyName("Bio")]
    public string? Bio { get; set; }

    [JsonPropertyName("Birthday")]
    public DateTime? Birthday { get; set; }

    [JsonPropertyName("Location")]
    public string? Location { get; set; }

    [JsonPropertyName("UpdatedAt")]
    public DateTime UpdatedAt { get; set; }
}

public sealed class PortableUserProfileSyncResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool PendingWritten { get; init; }
    public bool SkippedDueToNoRefreshToken { get; init; }
    public bool SkippedDueToPendingSync { get; init; }
    public PortableUserProfileData? Profile { get; init; }
}

public interface IPortableUserProfileApi
{
    Task<PortableApiResponse<PortableServerUserProfile>> GetProfileAsync(CancellationToken cancellationToken = default);

    Task<PortableApiResponse<object>> UpdateProfileAsync(
        PortableServerUserProfile profile,
        CancellationToken cancellationToken = default);
}

public sealed class PortableUserProfileSyncController
{
    private static readonly JsonSerializerOptions SyncJsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly FileUserProfileStore _store;
    private readonly IPortableUserProfileApi _api;
    private readonly Func<string> _userIdProvider;
    private readonly Func<bool> _hasRefreshToken;
    private readonly Func<DateTime> _clock;

    public PortableUserProfileSyncController(
        FileUserProfileStore store,
        IPortableUserProfileApi api,
        Func<string>? userIdProvider = null,
        Func<bool>? hasRefreshToken = null,
        Func<DateTime>? clock = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _userIdProvider = userIdProvider ?? (() => string.Empty);
        _hasRefreshToken = hasRefreshToken ?? (() => true);
        _clock = clock ?? (() => DateTime.Now);
    }

    public string PendingSyncPath
    {
        get
        {
            var directory = Path.GetDirectoryName(_store.DefaultProfilePath) ?? string.Empty;
            return Path.Combine(directory, "profile_pending_sync.json");
        }
    }

    public async Task<PortableUserProfileSyncResult> PullFromServerAsync(
        PortableUserProfileData localProfile,
        CancellationToken cancellationToken = default)
    {
        if (!_hasRefreshToken())
            return new PortableUserProfileSyncResult { SkippedDueToNoRefreshToken = true, Profile = localProfile };

        if (File.Exists(PendingSyncPath))
            return new PortableUserProfileSyncResult { SkippedDueToPendingSync = true, Profile = localProfile };

        try
        {
            var response = await _api.GetProfileAsync(cancellationToken).ConfigureAwait(false);
            if (!response.Success)
                return new PortableUserProfileSyncResult { Message = response.Message ?? "服务器同步失败", Profile = localProfile };

            return new PortableUserProfileSyncResult
            {
                Success = true,
                Profile = response.Data == null ? localProfile : ApplyServerProfileToLocal(localProfile, response.Data)
            };
        }
        catch (Exception ex)
        {
            return new PortableUserProfileSyncResult { Message = ex.Message, Profile = localProfile };
        }
    }

    public async Task<PortableUserProfileSyncResult> SyncToServerAsync(
        PortableUserProfileData localProfile,
        CancellationToken cancellationToken = default)
    {
        if (!_hasRefreshToken())
            return new PortableUserProfileSyncResult { SkippedDueToNoRefreshToken = true, Profile = localProfile };

        try
        {
            var serverProfile = BuildServerProfile(localProfile);
            var response = await _api.UpdateProfileAsync(serverProfile, cancellationToken).ConfigureAwait(false);
            if (response.Success)
            {
                DeletePendingSync();
                return new PortableUserProfileSyncResult { Success = true, Profile = localProfile };
            }

            var message = string.Equals(response.ErrorCode, "NETWORK_ERROR", StringComparison.OrdinalIgnoreCase)
                ? "网络连接失败，请检查网络后重试"
                : response.Message ?? "服务器同步失败";
            await WritePendingSyncAsync(CreatePendingSync(serverProfile), cancellationToken).ConfigureAwait(false);
            return new PortableUserProfileSyncResult
            {
                Message = message,
                PendingWritten = true,
                Profile = localProfile
            };
        }
        catch (Exception ex)
        {
            await WritePendingSyncAsync(CreatePendingSync(BuildServerProfile(localProfile)), cancellationToken).ConfigureAwait(false);
            return new PortableUserProfileSyncResult
            {
                Message = ex.Message,
                PendingWritten = true,
                Profile = localProfile
            };
        }
    }

    public PortableServerUserProfile BuildServerProfile(PortableUserProfileData localProfile)
    {
        var locationParts = new[] { localProfile.Country, localProfile.Province, localProfile.City }
            .Where(part => !string.IsNullOrWhiteSpace(part));
        var location = string.Join("/", locationParts);
        return new PortableServerUserProfile
        {
            UserId = _userIdProvider(),
            Username = localProfile.Username,
            DisplayName = localProfile.DisplayName,
            Email = string.IsNullOrWhiteSpace(localProfile.Email) ? null : localProfile.Email,
            Gender = localProfile.Gender,
            Bio = string.IsNullOrWhiteSpace(localProfile.Bio) ? null : localProfile.Bio,
            Birthday = localProfile.Birthday,
            Location = string.IsNullOrWhiteSpace(location) ? null : location
        };
    }

    public PortableUserProfileData ApplyServerProfileToLocal(
        PortableUserProfileData localProfile,
        PortableServerUserProfile serverProfile)
    {
        if (!string.IsNullOrWhiteSpace(serverProfile.DisplayName))
            localProfile.DisplayName = serverProfile.DisplayName;
        if (!string.IsNullOrWhiteSpace(serverProfile.Email))
            localProfile.Email = serverProfile.Email;
        if (!string.IsNullOrWhiteSpace(serverProfile.Gender))
            localProfile.Gender = serverProfile.Gender;
        if (!string.IsNullOrWhiteSpace(serverProfile.Bio))
            localProfile.Bio = serverProfile.Bio;
        if (serverProfile.Birthday.HasValue)
            localProfile.Birthday = serverProfile.Birthday;
        if (!string.IsNullOrWhiteSpace(serverProfile.Location))
        {
            var parts = serverProfile.Location.Split('/');
            if (parts.Length >= 1 && !string.IsNullOrWhiteSpace(parts[0]))
                localProfile.Country = parts[0];
            if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[1]))
                localProfile.Province = parts[1];
            if (parts.Length >= 3 && !string.IsNullOrWhiteSpace(parts[2]))
                localProfile.City = parts[2];
        }

        return localProfile;
    }

    public async Task<PortablePendingProfileSync?> LoadPendingSyncAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(PendingSyncPath))
                return null;

            await using var stream = File.OpenRead(PendingSyncPath);
            return await JsonSerializer.DeserializeAsync<PortablePendingProfileSync>(
                stream,
                SyncJsonOptions,
                cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    public async Task WritePendingSyncAsync(
        PortablePendingProfileSync pending,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(PendingSyncPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tempPath = PendingSyncPath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, pending, SyncJsonOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, PendingSyncPath, overwrite: true);
    }

    private void DeletePendingSync()
    {
        try
        {
            if (File.Exists(PendingSyncPath))
                File.Delete(PendingSyncPath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private PortablePendingProfileSync CreatePendingSync(PortableServerUserProfile profile)
    {
        return new PortablePendingProfileSync
        {
            DisplayName = profile.DisplayName ?? string.Empty,
            Email = profile.Email,
            Gender = profile.Gender ?? "保密",
            Bio = profile.Bio,
            Birthday = profile.Birthday,
            Location = profile.Location,
            UpdatedAt = _clock()
        };
    }
}

public sealed class PortableUserProfileService
{
    private readonly FileUserProfileStore _store;

    public PortableUserProfileService(FileUserProfileStore store)
    {
        _store = store;
    }

    public string SaveAvatar(string sourceImagePath)
    {
        try
        {
            if (!File.Exists(sourceImagePath))
                return string.Empty;

            Directory.CreateDirectory(_store.AvatarDirectory);
            File.Copy(sourceImagePath, _store.AvatarPath, overwrite: true);
            return _store.AvatarPath;
        }
        catch (IOException)
        {
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    public string GetAvatarPath()
    {
        return File.Exists(_store.AvatarPath) ? _store.AvatarPath : string.Empty;
    }

    public bool DeleteAvatar()
    {
        try
        {
            if (!File.Exists(_store.AvatarPath))
                return false;

            File.Delete(_store.AvatarPath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public async Task ExportProfileAsync(
        PortableUserProfileData profile,
        string exportPath,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(exportPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        await using var stream = File.Create(exportPath);
        await JsonSerializer.SerializeAsync(stream, profile, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PortableUserProfileData?> ImportProfileAsync(
        string importPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(importPath))
                return null;

            await using var stream = File.OpenRead(importPath);
            return await JsonSerializer.DeserializeAsync<PortableUserProfileData>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
}
