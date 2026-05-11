using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableAccountRenewalControllerTests
{
    [Theory]
    [InlineData("", "CARD-1", PortableAccountRenewalField.Account, "请输入账号")]
    [InlineData("jimmy", "", PortableAccountRenewalField.CardKey, "请输入卡密")]
    public async Task RenewAsync_rejects_original_dialog_required_fields_before_calling_api(
        string account,
        string cardKey,
        PortableAccountRenewalField expectedField,
        string expectedMessage)
    {
        var api = new RecordingAccountRenewalApi();
        var controller = new PortableAccountRenewalController(api);

        var result = await controller.RenewAsync(account, cardKey);

        Assert.False(result.Success);
        Assert.Equal(expectedField, result.FocusField);
        Assert.Equal(expectedMessage, result.ErrorMessage);
        Assert.False(api.Called);
    }

    [Fact]
    public async Task RenewAsync_trims_fields_and_returns_original_success_message()
    {
        var api = new RecordingAccountRenewalApi
        {
            Response = new PortableApiResponse<PortableActivationResult>
            {
                Success = true,
                Data = new PortableActivationResult { DaysAdded = 15 }
            }
        };
        var controller = new PortableAccountRenewalController(api);

        var result = await controller.RenewAsync("  jimmy  ", "  CARD-1  ");

        Assert.True(result.Success);
        Assert.Equal("jimmy", result.Account);
        Assert.Equal(15, result.DaysAdded);
        Assert.Equal("续费成功！已为账号增加 15 天会员时长", result.Message);
        Assert.Equal("jimmy", api.LastAccount);
        Assert.Equal("CARD-1", api.LastCardKey);
    }

    [Fact]
    public async Task RenewAsync_uses_original_server_failure_fallback()
    {
        var api = new RecordingAccountRenewalApi
        {
            Response = new PortableApiResponse<PortableActivationResult>
            {
                Success = false,
                ErrorCode = "SERVER_RULE",
                Message = "服务端拒绝续费"
            }
        };
        var controller = new PortableAccountRenewalController(api);

        var result = await controller.RenewAsync("jimmy", "CARD-1");

        Assert.False(result.Success);
        Assert.Equal("服务端拒绝续费", result.ErrorMessage);
        Assert.Equal("SERVER_RULE", result.ErrorCode);
    }

    [Fact]
    public async Task RenewAsync_wraps_exceptions_with_original_failure_prefix()
    {
        var api = new RecordingAccountRenewalApi { Exception = new InvalidOperationException("boom") };
        var controller = new PortableAccountRenewalController(api);

        var result = await controller.RenewAsync("jimmy", "CARD-1");

        Assert.False(result.Success);
        Assert.Equal("续费失败: boom", result.ErrorMessage);
    }

    private sealed class RecordingAccountRenewalApi : IPortableAccountRenewalApi
    {
        public bool Called { get; private set; }
        public string? LastAccount { get; private set; }
        public string? LastCardKey { get; private set; }
        public Exception? Exception { get; init; }
        public PortableApiResponse<PortableActivationResult> Response { get; init; } = new() { Success = true };

        public Task<PortableApiResponse<PortableActivationResult>> RenewAccountWithCardKeyAsync(
            string account,
            string cardKey,
            CancellationToken cancellationToken = default)
        {
            Called = true;
            LastAccount = account;
            LastCardKey = cardKey;
            if (Exception != null)
            {
                throw Exception;
            }

            return Task.FromResult(Response);
        }
    }
}
