using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableAuthResultMessagesTests
{
    [Theory]
    [InlineData("", "secret", "请输入用户名")]
    [InlineData("jimmy", "", "请输入密码")]
    public void ValidateLoginInput_returns_original_required_messages(string username, string password, string expected)
    {
        var result = PortableAuthResultMessages.ValidateLoginInput(username, password);

        Assert.False(result.Success);
        Assert.Equal(expected, result.ErrorMessage);
    }

    [Theory]
    [InlineData("", "secret", null, "用户名不能为空")]
    [InlineData("ji", "secret", null, "用户名至少3个字符")]
    [InlineData("jimmy", "", null, "密码不能为空")]
    [InlineData("jimmy", "12345", null, "密码至少6个字符")]
    [InlineData("jimmy", "secret", "", "请输入卡密")]
    public void ValidateRegisterInput_returns_original_required_messages(
        string username,
        string password,
        string? cardKey,
        string expected)
    {
        var result = PortableAuthResultMessages.ValidateRegisterInput(username, password, cardKey);

        Assert.False(result.Success);
        Assert.Equal(expected, result.ErrorMessage);
    }

    [Theory]
    [InlineData(PortableApiErrorCodes.NetworkError, "server", "网络连接失败，请检查网络后重试")]
    [InlineData(PortableApiErrorCodes.UsernameExists, "server", "用户名已存在")]
    [InlineData("SERVER_RULE", "服务端错误", "服务端错误")]
    [InlineData("SERVER_RULE", null, "注册失败")]
    public void FromRegisterResponse_localizes_original_register_errors(
        string errorCode,
        string? serverMessage,
        string expected)
    {
        var result = PortableAuthResultMessages.FromRegisterResponse(
            new PortableApiResponse<PortableRegisterResult>
            {
                Success = false,
                ErrorCode = errorCode,
                Message = serverMessage
            });

        Assert.False(result.Success);
        Assert.Equal(expected, result.ErrorMessage);
        Assert.Equal(errorCode, result.ErrorCode);
    }

    [Fact]
    public void FromLoginResponse_maps_subscription_none_to_warning_dialog_message()
    {
        var result = PortableAuthResultMessages.FromLoginResponse(
            new PortableApiResponse<PortableLoginResult>
            {
                Success = false,
                ErrorCode = PortableApiErrorCodes.SubscriptionNone,
                Message = "not active"
            });

        Assert.False(result.Success);
        Assert.Equal("账号未激活\n\n请先使用卡密激活后再登录。\n您可以点击「更多选项」→「账号续费」使用卡密激活。", result.ErrorMessage);
        Assert.Equal(PortableAuthMessagePresentation.WarningDialog, result.Presentation);
    }

    [Theory]
    [InlineData(PortableApiErrorCodes.AuthDeviceKicked, "您的账号已在其他设备登录，当前会话已失效", PortableAuthMessagePresentation.InlineError)]
    [InlineData(PortableApiErrorCodes.SubscriptionExpired, "订阅已过期\n\n请点击「更多选项」→「账号续费」使用卡密续费。", PortableAuthMessagePresentation.WarningDialog)]
    [InlineData(PortableApiErrorCodes.NetworkError, "网络连接失败，请检查网络后重试", PortableAuthMessagePresentation.InlineError)]
    public void FromLoginResponse_localizes_original_login_errors(
        string errorCode,
        string expected,
        PortableAuthMessagePresentation presentation)
    {
        var result = PortableAuthResultMessages.FromLoginResponse(
            new PortableApiResponse<PortableLoginResult>
            {
                Success = false,
                ErrorCode = errorCode,
                Message = "订阅已过期"
            });

        Assert.False(result.Success);
        Assert.Equal(expected, result.ErrorMessage);
        Assert.Equal(errorCode, result.ErrorCode);
        Assert.Equal(presentation, result.Presentation);
    }

    [Fact]
    public void FromException_uses_original_login_failure_prefix()
    {
        var result = PortableAuthResultMessages.FromException("登录失败", new InvalidOperationException("boom"));

        Assert.False(result.Success);
        Assert.Equal("登录失败: boom", result.ErrorMessage);
    }
}
