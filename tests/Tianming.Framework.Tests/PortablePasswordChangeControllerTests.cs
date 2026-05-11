using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortablePasswordChangeControllerTests
{
    [Fact]
    public async Task ChangePasswordAsync_validates_original_input_branches_before_side_effects()
    {
        using var workspace = new TempDirectory();
        var store = new FilePasswordSecurityStore(workspace.Path);
        var service = new PortableAccountSecurityService(store);
        var api = new RecordingPasswordChangeApi();
        var controller = new PortablePasswordChangeController(service, api, password =>
            password == "weak" ? (false, "密码强度不足，请使用更复杂的密码") : (true, "密码符合要求"));

        var missingOld = await controller.ChangePasswordAsync("", "NewPass-123", "NewPass-123");
        var missingNew = await controller.ChangePasswordAsync("old", "", "");
        var mismatch = await controller.ChangePasswordAsync("old", "NewPass-123", "Different-123");
        var weak = await controller.ChangePasswordAsync("old", "weak", "weak");

        Assert.Equal("请输入旧密码", missingOld.Message);
        Assert.Equal(PortablePasswordChangePresentation.Warning, missingOld.Presentation);
        Assert.Equal("请输入新密码", missingNew.Message);
        Assert.Equal("两次输入的密码不一致", mismatch.Message);
        Assert.Equal("密码强度不足，请使用更复杂的密码", weak.Message);
        Assert.False(api.Called);
        Assert.False(await service.HasPasswordAsync());
    }

    [Fact]
    public async Task ChangePasswordAsync_sets_initial_password_without_server_sync_when_no_local_password_exists()
    {
        using var workspace = new TempDirectory();
        var store = new FilePasswordSecurityStore(workspace.Path);
        var service = new PortableAccountSecurityService(store);
        var api = new RecordingPasswordChangeApi();
        var controller = new PortablePasswordChangeController(service, api, _ => (true, "密码符合要求"));

        var result = await controller.ChangePasswordAsync("ignored-old", "NewPass-123", "NewPass-123");

        Assert.True(result.Success);
        Assert.Equal("设置密码", result.Title);
        Assert.Equal("密码设置成功", result.Message);
        Assert.True(result.ClearPasswordFields);
        Assert.False(api.Called);
        Assert.True(await service.VerifyPasswordAsync("NewPass-123"));
    }

    [Fact]
    public async Task ChangePasswordAsync_rejects_wrong_old_password_before_server_sync()
    {
        using var workspace = new TempDirectory();
        var store = new FilePasswordSecurityStore(workspace.Path);
        var service = new PortableAccountSecurityService(store);
        await service.SetInitialPasswordAsync("OldPass-123");
        var api = new RecordingPasswordChangeApi();
        var controller = new PortablePasswordChangeController(service, api, _ => (true, "密码符合要求"));

        var result = await controller.ChangePasswordAsync("wrong", "NewPass-123", "NewPass-123");

        Assert.False(result.Success);
        Assert.Equal("旧密码验证失败", result.Message);
        Assert.False(api.Called);
        Assert.True(await service.VerifyPasswordAsync("OldPass-123"));
    }

    [Fact]
    public async Task ChangePasswordAsync_maps_server_failure_and_keeps_local_password_unchanged()
    {
        using var workspace = new TempDirectory();
        var store = new FilePasswordSecurityStore(workspace.Path);
        var service = new PortableAccountSecurityService(store);
        await service.SetInitialPasswordAsync("OldPass-123");
        var api = new RecordingPasswordChangeApi
        {
            Response = new PortableApiResponse<object>
            {
                Success = false,
                ErrorCode = "NETWORK_ERROR",
                Message = "server down"
            }
        };
        var controller = new PortablePasswordChangeController(service, api, _ => (true, "密码符合要求"));

        var result = await controller.ChangePasswordAsync("OldPass-123", "NewPass-123", "NewPass-123");

        Assert.False(result.Success);
        Assert.Equal("网络连接失败，请检查网络后重试", result.Message);
        Assert.True(api.Called);
        Assert.Equal("OldPass-123", api.LastOldPassword);
        Assert.Equal("NewPass-123", api.LastNewPassword);
        Assert.True(await service.VerifyPasswordAsync("OldPass-123"));
    }

    [Fact]
    public async Task ChangePasswordAsync_syncs_server_then_updates_local_password_and_clears_fields()
    {
        using var workspace = new TempDirectory();
        var store = new FilePasswordSecurityStore(workspace.Path);
        var service = new PortableAccountSecurityService(store);
        await service.SetInitialPasswordAsync("OldPass-123");
        var api = new RecordingPasswordChangeApi();
        var controller = new PortablePasswordChangeController(service, api, _ => (true, "密码符合要求"));

        var result = await controller.ChangePasswordAsync("OldPass-123", "NewPass-123", "NewPass-123");

        Assert.True(result.Success);
        Assert.Equal("修改密码", result.Title);
        Assert.Equal("密码修改成功", result.Message);
        Assert.True(result.ClearPasswordFields);
        Assert.True(await service.VerifyPasswordAsync("NewPass-123"));
        Assert.False(await service.VerifyPasswordAsync("OldPass-123"));
    }

    [Fact]
    public async Task ChangePasswordAsync_uses_original_local_duplicate_and_exception_messages()
    {
        using var workspace = new TempDirectory();
        var store = new FilePasswordSecurityStore(workspace.Path);
        var service = new PortableAccountSecurityService(store);
        await service.SetInitialPasswordAsync("OldPass-123");
        var api = new RecordingPasswordChangeApi();
        var controller = new PortablePasswordChangeController(service, api, _ => (true, "密码符合要求"));

        var duplicate = await controller.ChangePasswordAsync("OldPass-123", "OldPass-123", "OldPass-123");
        api.Exception = new InvalidOperationException("boom");
        var thrown = await controller.ChangePasswordAsync("OldPass-123", "NewPass-123", "NewPass-123");

        Assert.Equal("新密码与历史密码重复", duplicate.Message);
        Assert.False(duplicate.ClearPasswordFields);
        Assert.Equal("操作失败: boom", thrown.Message);
    }

    private sealed class RecordingPasswordChangeApi : IPortablePasswordChangeApi
    {
        public bool Called { get; private set; }
        public string? LastOldPassword { get; private set; }
        public string? LastNewPassword { get; private set; }
        public Exception? Exception { get; set; }
        public PortableApiResponse<object> Response { get; init; } = new() { Success = true };

        public Task<PortableApiResponse<object>> ChangePasswordAsync(
            string oldPassword,
            string newPassword,
            CancellationToken cancellationToken = default)
        {
            Called = true;
            LastOldPassword = oldPassword;
            LastNewPassword = newPassword;
            if (Exception != null)
            {
                throw Exception;
            }

            return Task.FromResult(Response);
        }
    }
}
