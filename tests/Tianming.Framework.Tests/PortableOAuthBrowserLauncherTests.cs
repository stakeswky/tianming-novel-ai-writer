using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableOAuthBrowserLauncherTests
{
    [Fact]
    public void OpenAuthorizationUrl_invokes_macos_open_with_authorization_url()
    {
        var runner = new CapturingBrowserCommandRunner();
        var launcher = new PortableOAuthBrowserLauncher(runner);

        var result = launcher.OpenAuthorizationUrl("https://github.com/login/oauth/authorize?client_id=client-1");

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        var command = Assert.Single(runner.Commands);
        Assert.Equal("/usr/bin/open", command.FileName);
        Assert.Equal(["https://github.com/login/oauth/authorize?client_id=client-1"], command.Arguments);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ftp://example.test/oauth")]
    [InlineData("file:///tmp/token")]
    [InlineData("not a url")]
    public void OpenAuthorizationUrl_rejects_empty_or_non_http_urls(string url)
    {
        var runner = new CapturingBrowserCommandRunner();
        var launcher = new PortableOAuthBrowserLauncher(runner);

        var result = launcher.OpenAuthorizationUrl(url);

        Assert.False(result.Success);
        Assert.Equal("OAuth 授权地址无效", result.ErrorMessage);
        Assert.Empty(runner.Commands);
    }

    [Fact]
    public void OpenAuthorizationUrl_maps_command_failure_to_error_result()
    {
        var runner = new CapturingBrowserCommandRunner
        {
            NextResult = new PortableBrowserCommandResult(1, "", "open failed")
        };
        var launcher = new PortableOAuthBrowserLauncher(runner);

        var result = launcher.OpenAuthorizationUrl("https://accounts.google.com/o/oauth2/v2/auth?client_id=client-1");

        Assert.False(result.Success);
        Assert.Equal("无法打开 OAuth 授权浏览器: open failed", result.ErrorMessage);
    }

    [Fact]
    public void OpenAuthorizationUrl_uses_stdout_when_stderr_is_empty()
    {
        var runner = new CapturingBrowserCommandRunner
        {
            NextResult = new PortableBrowserCommandResult(1, "no handler", "")
        };
        var launcher = new PortableOAuthBrowserLauncher(runner);

        var result = launcher.OpenAuthorizationUrl("http://localhost:23456/oauth/callback?code=abc");

        Assert.False(result.Success);
        Assert.Equal("无法打开 OAuth 授权浏览器: no handler", result.ErrorMessage);
    }

    [Fact]
    public void Constructor_rejects_missing_runner()
    {
        Assert.Throws<ArgumentNullException>(() => new PortableOAuthBrowserLauncher(null!));
    }

    private sealed class CapturingBrowserCommandRunner : IPortableBrowserCommandRunner
    {
        public List<PortableBrowserCommandInvocation> Commands { get; } = new();
        public PortableBrowserCommandResult NextResult { get; set; } = new(0, "", "");

        public PortableBrowserCommandResult Run(string fileName, IReadOnlyList<string> arguments)
        {
            Commands.Add(new PortableBrowserCommandInvocation(fileName, arguments.ToArray()));
            return NextResult;
        }
    }
}
