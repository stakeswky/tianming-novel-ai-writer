using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableCardKeyActivationMessagesTests
{
    [Fact]
    public void FromBlankCardKey_returns_original_required_message()
    {
        var result = PortableCardKeyActivationMessages.FromBlankCardKey();

        Assert.False(result.Success);
        Assert.Equal("请输入卡密", result.ErrorMessage);
    }

    [Theory]
    [InlineData(PortableApiErrorCodes.CardKeyInvalid, "卡密无效或已过期")]
    [InlineData(PortableApiErrorCodes.CardKeyUsed, "该卡密已被使用")]
    [InlineData(PortableApiErrorCodes.NetworkError, "网络连接失败，请检查网络后重试")]
    public void FromActivationResponse_localizes_original_card_key_errors(string errorCode, string expectedMessage)
    {
        var result = PortableCardKeyActivationMessages.FromActivationResponse(
            new PortableApiResponse<PortableActivationResult>
            {
                Success = false,
                Message = "server message",
                ErrorCode = errorCode
            });

        Assert.False(result.Success);
        Assert.Equal(expectedMessage, result.ErrorMessage);
        Assert.Equal(errorCode, result.ErrorCode);
    }

    [Fact]
    public void FromActivationResponse_keeps_server_message_for_unknown_error()
    {
        var result = PortableCardKeyActivationMessages.FromActivationResponse(
            new PortableApiResponse<PortableActivationResult>
            {
                Success = false,
                Message = "服务端返回的失败原因",
                ErrorCode = "SERVER_SIDE_RULE"
            });

        Assert.False(result.Success);
        Assert.Equal("服务端返回的失败原因", result.ErrorMessage);
        Assert.Equal("SERVER_SIDE_RULE", result.ErrorCode);
    }

    [Fact]
    public void FromActivationResponse_uses_original_success_message()
    {
        var expireTime = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);

        var result = PortableCardKeyActivationMessages.FromActivationResponse(
            new PortableApiResponse<PortableActivationResult>
            {
                Success = true,
                Data = new PortableActivationResult
                {
                    Success = true,
                    DaysAdded = 30,
                    NewExpireTime = expireTime
                }
            });

        Assert.True(result.Success);
        Assert.Equal(30, result.DaysAdded);
        Assert.Equal(expireTime, result.NewExpireTime);
        Assert.Equal("续费成功！已增加30天会员时长", result.Message);
    }

    [Fact]
    public void FromException_uses_original_failure_prefix()
    {
        var result = PortableCardKeyActivationMessages.FromException(new InvalidOperationException("boom"));

        Assert.False(result.Success);
        Assert.Equal("续费失败: boom", result.ErrorMessage);
    }
}
