using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableTwoFactorControllerTests
{
    [Fact]
    public async Task EnableAsync_enables_two_factor_and_returns_original_uri_and_formatted_secret()
    {
        using var workspace = new TempDirectory();
        var store = new FilePasswordSecurityStore(workspace.Path);
        var service = new PortableAccountSecurityService(store);
        var controller = new PortableTwoFactorController(service, _ => "ABCD1234EFGH5678");

        var result = await controller.EnableAsync();

        Assert.True(result.Success);
        Assert.Equal("双因素认证", result.Title);
        Assert.Equal("已启用双因素认证，请扫描二维码", result.Message);
        Assert.Equal("ABCD1234EFGH5678", result.Secret);
        Assert.Equal("ABCD 1234 EFGH 5678", result.FormattedSecret);
        Assert.Equal("otpauth://totp/%E5%A4%A9%E5%91%BD:%E5%A4%A9%E5%91%BD%E7%94%A8%E6%88%B7?secret=ABCD1234EFGH5678&issuer=%E5%A4%A9%E5%91%BD", result.TotpUri);
        Assert.True(await service.IsTwoFactorEnabledAsync());
    }

    [Fact]
    public async Task DisableAsync_disables_two_factor_and_clears_presented_secret()
    {
        using var workspace = new TempDirectory();
        var store = new FilePasswordSecurityStore(workspace.Path);
        var service = new PortableAccountSecurityService(store);
        await service.EnableTwoFactorAuthAsync("ABCD1234EFGH5678");
        var controller = new PortableTwoFactorController(service);

        var result = await controller.DisableAsync();

        Assert.True(result.Success);
        Assert.Equal("双因素认证", result.Title);
        Assert.Equal("已禁用双因素认证", result.Message);
        Assert.Equal(string.Empty, result.Secret);
        Assert.Null(result.TotpUri);
        Assert.False(await service.IsTwoFactorEnabledAsync());
    }

    [Fact]
    public async Task VerifyCodeAsync_maps_empty_valid_invalid_and_exception_branches()
    {
        using var workspace = new TempDirectory();
        var store = new FilePasswordSecurityStore(workspace.Path);
        var service = new PortableAccountSecurityService(store);
        const string secret = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";
        await service.EnableTwoFactorAuthAsync(secret);
        var code = PortableTotp.GenerateCode(secret, timeStep: 1);
        var controller = new PortableTwoFactorController(service, timeStepProvider: () => 1);

        var empty = await controller.VerifyCodeAsync("");
        var valid = await controller.VerifyCodeAsync(code);
        var invalid = await controller.VerifyCodeAsync("000000");

        Assert.False(empty.Success);
        Assert.Equal(PortableTwoFactorPresentation.Warning, empty.Presentation);
        Assert.Equal("请输入验证码", empty.Message);
        Assert.False(empty.ClearVerificationCode);
        Assert.True(valid.Success);
        Assert.Equal("验证成功", valid.Message);
        Assert.True(valid.ClearVerificationCode);
        Assert.False(invalid.Success);
        Assert.Equal("验证码错误", invalid.Message);
        Assert.True(invalid.ClearVerificationCode);
    }

    [Fact]
    public async Task EnableAsync_reports_original_failure_message()
    {
        using var workspace = new TempDirectory();
        var store = new FilePasswordSecurityStore(workspace.Path);
        var service = new PortableAccountSecurityService(store);
        var controller = new PortableTwoFactorController(service, _ => throw new InvalidOperationException("boom"));

        var result = await controller.EnableAsync();

        Assert.False(result.Success);
        Assert.Equal("启用失败: boom", result.Message);
        Assert.False(await service.IsTwoFactorEnabledAsync());
    }
}
