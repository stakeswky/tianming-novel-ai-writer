using TM.Framework.Profile;
using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableUserProfileTests
{
    [Fact]
    public void Default_profile_matches_original_basic_info_defaults()
    {
        var now = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);
        var profile = PortableUserProfileData.CreateDefault(now, "User_123456");

        Assert.Equal("User_123456", profile.Username);
        Assert.Equal(string.Empty, profile.DisplayName);
        Assert.Equal(string.Empty, profile.RealName);
        Assert.Equal("保密", profile.Gender);
        Assert.Equal(string.Empty, profile.Email);
        Assert.Equal(string.Empty, profile.Phone);
        Assert.Equal("中国", profile.Country);
        Assert.Equal(string.Empty, profile.Province);
        Assert.Equal(string.Empty, profile.City);
        Assert.Equal(string.Empty, profile.AvatarPath);
        Assert.Equal(string.Empty, profile.Bio);
        Assert.Null(profile.Birthday);
        Assert.Equal(now, profile.CreatedTime);
        Assert.Equal(now, profile.LastUpdatedTime);
    }

    [Fact]
    public async Task Store_sanitizes_user_profile_paths_and_creates_missing_profiles()
    {
        using var workspace = new TempDirectory();
        var now = new DateTime(2026, 5, 10, 13, 0, 0, DateTimeKind.Utc);
        var store = new FileUserProfileStore(workspace.Path, () => now);

        var path = store.GetUserProfilePath(" Alice:One? ");
        Assert.EndsWith(Path.Combine("Profiles", "alice_one_.json"), path);

        var created = await store.EnsureProfileExistsAsync(" Alice:One? ");

        Assert.Equal("Alice:One?", created.Username);
        Assert.Equal("用户", created.DisplayName);
        Assert.True(File.Exists(path));

        var reloaded = await store.LoadAsync(path);

        Assert.Equal("Alice:One?", reloaded.Username);
        Assert.Equal("用户", reloaded.DisplayName);
    }

    [Fact]
    public async Task Store_round_trips_atomically_and_recovers_defaults()
    {
        using var workspace = new TempDirectory();
        var now = new DateTime(2026, 5, 10, 14, 0, 0, DateTimeKind.Utc);
        var store = new FileUserProfileStore(workspace.Path, () => now);
        var path = store.DefaultProfilePath;
        var profile = PortableUserProfileData.CreateDefault(now, "alice");
        profile.DisplayName = "Alice";
        profile.Email = "alice@example.test";

        await store.SaveAsync(profile, path);
        var reloaded = await new FileUserProfileStore(workspace.Path, () => now).LoadAsync(path);

        Assert.False(File.Exists(path + ".tmp"));
        Assert.Equal("alice", reloaded.Username);
        Assert.Equal("Alice", reloaded.DisplayName);
        Assert.Equal("alice@example.test", reloaded.Email);
        Assert.Equal(now, reloaded.LastUpdatedTime);

        await File.WriteAllTextAsync(path, "{ invalid json");
        var recovered = await new FileUserProfileStore(workspace.Path, () => now).LoadAsync(path);

        Assert.StartsWith("User_", recovered.Username);
        Assert.Equal("中国", recovered.Country);
    }

    [Fact]
    public async Task Service_saves_deletes_avatar_and_exports_imports_profile()
    {
        using var workspace = new TempDirectory();
        var now = new DateTime(2026, 5, 10, 15, 0, 0, DateTimeKind.Utc);
        var store = new FileUserProfileStore(workspace.Path, () => now);
        var service = new PortableUserProfileService(store);
        var sourceAvatar = Path.Combine(workspace.Path, "source.png");
        await File.WriteAllBytesAsync(sourceAvatar, [1, 2, 3]);

        var avatarPath = service.SaveAvatar(sourceAvatar);

        Assert.True(File.Exists(avatarPath));
        Assert.Equal(avatarPath, service.GetAvatarPath());

        var profile = PortableUserProfileData.CreateDefault(now, "alice");
        profile.DisplayName = "Alice";
        await service.ExportProfileAsync(profile, Path.Combine(workspace.Path, "exports", "profile.json"));
        var imported = await service.ImportProfileAsync(Path.Combine(workspace.Path, "exports", "profile.json"));

        Assert.Equal("alice", imported?.Username);
        Assert.Equal("Alice", imported?.DisplayName);
        Assert.True(service.DeleteAvatar());
        Assert.Equal(string.Empty, service.GetAvatarPath());
    }

    [Fact]
    public async Task SyncController_builds_original_server_profile_dto_and_writes_pending_on_failure()
    {
        using var workspace = new TempDirectory();
        var now = new DateTime(2026, 5, 11, 9, 0, 0, DateTimeKind.Utc);
        var store = new FileUserProfileStore(workspace.Path, () => now);
        var api = new RecordingUserProfileApi
        {
            UpdateResponse = new PortableApiResponse<object>
            {
                Success = false,
                ErrorCode = "NETWORK_ERROR",
                Message = "down"
            }
        };
        var controller = new PortableUserProfileSyncController(store, api, () => "u1", () => true, () => now);
        var profile = PortableUserProfileData.CreateDefault(now, "jimmy");
        profile.DisplayName = "Jimmy";
        profile.Email = string.Empty;
        profile.Gender = "保密";
        profile.Bio = "bio";
        profile.Birthday = new DateTime(1990, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        profile.Country = "中国";
        profile.Province = "上海";
        profile.City = "浦东";

        var result = await controller.SyncToServerAsync(profile);

        Assert.False(result.Success);
        Assert.True(result.PendingWritten);
        Assert.Equal("网络连接失败，请检查网络后重试", result.Message);
        Assert.Equal("u1", api.LastUpdatedProfile!.UserId);
        Assert.Equal("jimmy", api.LastUpdatedProfile.Username);
        Assert.Null(api.LastUpdatedProfile.Email);
        Assert.Equal("中国/上海/浦东", api.LastUpdatedProfile.Location);
        var pending = await controller.LoadPendingSyncAsync();
        Assert.NotNull(pending);
        Assert.Equal("Jimmy", pending.DisplayName);
        Assert.Null(pending.Email);
        Assert.Equal("中国/上海/浦东", pending.Location);
        Assert.Equal(now, pending.UpdatedAt);
    }

    [Fact]
    public async Task SyncController_applies_server_profile_and_skips_pull_when_pending_exists()
    {
        using var workspace = new TempDirectory();
        var now = new DateTime(2026, 5, 11, 10, 0, 0, DateTimeKind.Utc);
        var store = new FileUserProfileStore(workspace.Path, () => now);
        var api = new RecordingUserProfileApi
        {
            GetResponse = new PortableApiResponse<PortableServerUserProfile>
            {
                Success = true,
                Data = new PortableServerUserProfile
                {
                    DisplayName = "Server Name",
                    Email = "server@example.com",
                    Gender = "女",
                    Bio = "server bio",
                    Birthday = new DateTime(1992, 3, 4, 0, 0, 0, DateTimeKind.Utc),
                    Location = "中国/浙江/杭州"
                }
            }
        };
        var controller = new PortableUserProfileSyncController(store, api, () => "u1", () => true, () => now);
        var local = PortableUserProfileData.CreateDefault(now, "jimmy");

        var pull = await controller.PullFromServerAsync(local);
        var pulledProfile = pull.Profile!;
        await controller.WritePendingSyncAsync(new PortablePendingProfileSync { DisplayName = "local pending", UpdatedAt = now });
        var skipped = await controller.PullFromServerAsync(pulledProfile);

        Assert.True(pull.Success);
        Assert.Equal("Server Name", pulledProfile.DisplayName);
        Assert.Equal("server@example.com", pulledProfile.Email);
        Assert.Equal("女", pulledProfile.Gender);
        Assert.Equal("server bio", pulledProfile.Bio);
        Assert.Equal(new DateTime(1992, 3, 4, 0, 0, 0, DateTimeKind.Utc), pulledProfile.Birthday);
        Assert.Equal("中国", pulledProfile.Country);
        Assert.Equal("浙江", pulledProfile.Province);
        Assert.Equal("杭州", pulledProfile.City);
        Assert.False(skipped.Success);
        Assert.True(skipped.SkippedDueToPendingSync);
        Assert.Equal(1, api.GetCalls);
    }

    private sealed class RecordingUserProfileApi : IPortableUserProfileApi
    {
        public int GetCalls { get; private set; }
        public PortableServerUserProfile? LastUpdatedProfile { get; private set; }
        public PortableApiResponse<PortableServerUserProfile> GetResponse { get; init; } = new() { Success = true };
        public PortableApiResponse<object> UpdateResponse { get; init; } = new() { Success = true };

        public Task<PortableApiResponse<PortableServerUserProfile>> GetProfileAsync(CancellationToken cancellationToken = default)
        {
            GetCalls++;
            return Task.FromResult(GetResponse);
        }

        public Task<PortableApiResponse<object>> UpdateProfileAsync(
            PortableServerUserProfile profile,
            CancellationToken cancellationToken = default)
        {
            LastUpdatedProfile = profile;
            return Task.FromResult(UpdateResponse);
        }
    }
}
