using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableLoginControllerTests
{
    [Theory]
    [InlineData("", "secret", PortableLoginField.Username, "请输入账号")]
    [InlineData("jimmy", "", PortableLoginField.Password, "请输入密码")]
    public async Task LoginAsync_rejects_original_window_required_fields_before_calling_api(
        string username,
        string password,
        PortableLoginField expectedField,
        string expectedMessage)
    {
        var api = new RecordingLoginApi();
        var rememberStore = new RecordingRememberStore();
        var controller = new PortableLoginController(api, rememberStore, password => $"enc:{password}");

        var result = await controller.LoginAsync(username, password, new PortableLoginRememberOptions());

        Assert.False(result.Success);
        Assert.Equal(expectedField, result.FocusField);
        Assert.Equal(expectedMessage, result.Message);
        Assert.False(api.Called);
    }

    [Fact]
    public async Task LoginAsync_trims_username_and_saves_remembered_account_after_success()
    {
        var api = new RecordingLoginApi
        {
            Response = new PortableApiResponse<PortableLoginResult> { Success = true }
        };
        var rememberStore = new RecordingRememberStore();
        var controller = new PortableLoginController(api, rememberStore, password => $"enc:{password}");

        var result = await controller.LoginAsync(
            "  jimmy  ",
            "secret",
            new PortableLoginRememberOptions { RememberAccount = true, RememberPassword = true });

        Assert.True(result.Success);
        Assert.Equal("jimmy", result.LoggedInUsername);
        Assert.Equal("jimmy", api.LastRequest!.Username);
        Assert.Equal("secret", api.LastRequest.Password);
        Assert.Equal("jimmy", rememberStore.SavedUsername);
        Assert.True(rememberStore.SavedRememberAccount);
        Assert.True(rememberStore.SavedRememberPassword);
        Assert.Equal("enc:secret", rememberStore.SavedEncryptedPassword);
        Assert.False(rememberStore.Cleared);
    }

    [Fact]
    public async Task LoginAsync_clears_remembered_account_when_remember_account_is_off()
    {
        var api = new RecordingLoginApi
        {
            Response = new PortableApiResponse<PortableLoginResult> { Success = true }
        };
        var rememberStore = new RecordingRememberStore();
        var controller = new PortableLoginController(api, rememberStore, password => $"enc:{password}");

        var result = await controller.LoginAsync(
            "jimmy",
            "secret",
            new PortableLoginRememberOptions { RememberAccount = false, RememberPassword = true });

        Assert.True(result.Success);
        Assert.True(rememberStore.Cleared);
        Assert.Null(rememberStore.SavedUsername);
    }

    [Fact]
    public async Task LoginAsync_maps_subscription_failure_to_warning_and_clears_password()
    {
        var api = new RecordingLoginApi
        {
            Response = new PortableApiResponse<PortableLoginResult>
            {
                Success = false,
                ErrorCode = PortableApiErrorCodes.SubscriptionExpired,
                Message = "订阅已过期"
            }
        };
        var controller = new PortableLoginController(api, new RecordingRememberStore(), password => $"enc:{password}");

        var result = await controller.LoginAsync("jimmy", "secret", new PortableLoginRememberOptions());

        Assert.False(result.Success);
        Assert.True(result.ShouldClearPassword);
        Assert.Equal(PortableAuthMessagePresentation.WarningDialog, result.Presentation);
        Assert.Equal("订阅已过期\n\n请点击「更多选项」→「账号续费」使用卡密续费。", result.Message);
    }

    [Fact]
    public async Task LoginAsync_wraps_exceptions_with_original_failure_prefix()
    {
        var api = new RecordingLoginApi { Exception = new InvalidOperationException("boom") };
        var controller = new PortableLoginController(api, new RecordingRememberStore(), password => $"enc:{password}");

        var result = await controller.LoginAsync("jimmy", "secret", new PortableLoginRememberOptions());

        Assert.False(result.Success);
        Assert.Equal("登录失败: boom", result.Message);
        Assert.False(result.ShouldClearPassword);
    }

    private sealed class RecordingLoginApi : IPortableLoginApi
    {
        public bool Called { get; private set; }
        public PortableLoginRequest? LastRequest { get; private set; }
        public Exception? Exception { get; init; }
        public PortableApiResponse<PortableLoginResult> Response { get; init; } = new() { Success = true };

        public Task<PortableApiResponse<PortableLoginResult>> LoginAsync(
            PortableLoginRequest request,
            CancellationToken cancellationToken = default)
        {
            Called = true;
            LastRequest = request;
            if (Exception != null)
            {
                throw Exception;
            }

            return Task.FromResult(Response);
        }
    }

    private sealed class RecordingRememberStore : IPortableLoginRememberStore
    {
        public string? SavedUsername { get; private set; }
        public bool SavedRememberAccount { get; private set; }
        public bool SavedRememberPassword { get; private set; }
        public string? SavedEncryptedPassword { get; private set; }
        public bool Cleared { get; private set; }

        public void SaveRememberedAccount(
            string username,
            bool rememberAccount,
            bool rememberPassword,
            string? encryptedPassword)
        {
            SavedUsername = username;
            SavedRememberAccount = rememberAccount;
            SavedRememberPassword = rememberPassword;
            SavedEncryptedPassword = encryptedPassword;
        }

        public void ClearRememberedAccount()
        {
            Cleared = true;
        }
    }
}
