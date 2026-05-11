using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableRegisterControllerTests
{
    [Theory]
    [InlineData("", "secret", "secret", "CARD-1", PortableRegisterField.Username, "请输入账号")]
    [InlineData("ji", "secret", "secret", "CARD-1", PortableRegisterField.Username, "账号至少3个字符")]
    [InlineData("jimmy", "", "", "CARD-1", PortableRegisterField.Password, "请输入密码")]
    [InlineData("jimmy", "12345", "12345", "CARD-1", PortableRegisterField.Password, "密码至少6位")]
    [InlineData("jimmy", "secret", "different", "CARD-1", PortableRegisterField.ConfirmPassword, "两次输入的密码不一致")]
    [InlineData("jimmy", "secret", "secret", "", PortableRegisterField.CardKey, "请输入卡密")]
    public async Task SubmitAsync_rejects_original_window_validation_errors_before_calling_api(
        string username,
        string password,
        string confirmPassword,
        string cardKey,
        PortableRegisterField expectedField,
        string expectedMessage)
    {
        var api = new RecordingRegisterApi();
        var controller = new PortableRegisterController(api);

        var result = await controller.SubmitAsync(username, password, confirmPassword, cardKey);

        Assert.False(result.Success);
        Assert.Equal(expectedField, result.FocusField);
        Assert.Equal(expectedMessage, result.Message);
        Assert.False(api.Called);
    }

    [Fact]
    public async Task SubmitAsync_trims_text_fields_and_returns_registered_credentials_after_success()
    {
        var api = new RecordingRegisterApi
        {
            Response = new PortableApiResponse<PortableRegisterResult>
            {
                Success = true,
                Data = new PortableRegisterResult { UserId = "u1", Username = "jimmy" }
            }
        };
        var controller = new PortableRegisterController(api);

        var result = await controller.SubmitAsync("  jimmy  ", "secret", "secret", "  CARD-1  ");

        Assert.True(result.Success);
        Assert.Equal("jimmy", result.RegisteredUsername);
        Assert.Equal("secret", result.RegisteredPassword);
        Assert.Equal("jimmy", api.LastRequest!.Username);
        Assert.Equal("secret", api.LastRequest.Password);
        Assert.Equal("CARD-1", api.LastRequest.CardKey);
    }

    [Fact]
    public async Task SubmitAsync_maps_register_api_failure_to_original_message()
    {
        var api = new RecordingRegisterApi
        {
            Response = new PortableApiResponse<PortableRegisterResult>
            {
                Success = false,
                ErrorCode = PortableApiErrorCodes.UsernameExists,
                Message = "server"
            }
        };
        var controller = new PortableRegisterController(api);

        var result = await controller.SubmitAsync("jimmy", "secret", "secret", "CARD-1");

        Assert.False(result.Success);
        Assert.Equal("用户名已存在", result.Message);
        Assert.Equal(PortableApiErrorCodes.UsernameExists, result.ErrorCode);
    }

    [Fact]
    public async Task SubmitAsync_wraps_exceptions_with_original_failure_prefix()
    {
        var api = new RecordingRegisterApi { Exception = new InvalidOperationException("boom") };
        var controller = new PortableRegisterController(api);

        var result = await controller.SubmitAsync("jimmy", "secret", "secret", "CARD-1");

        Assert.False(result.Success);
        Assert.Equal("注册失败: boom", result.Message);
    }

    private sealed class RecordingRegisterApi : IPortableRegisterApi
    {
        public bool Called { get; private set; }
        public PortableRegisterRequest? LastRequest { get; private set; }
        public Exception? Exception { get; init; }
        public PortableApiResponse<PortableRegisterResult> Response { get; init; } = new() { Success = true };

        public Task<PortableApiResponse<PortableRegisterResult>> RegisterAsync(
            PortableRegisterRequest request,
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
}
