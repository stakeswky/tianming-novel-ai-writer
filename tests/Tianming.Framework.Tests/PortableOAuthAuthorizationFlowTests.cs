using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableOAuthAuthorizationFlowTests
{
    [Fact]
    public async Task StartAuthorizationAsync_opens_browser_waits_for_callback_and_returns_result()
    {
        var runner = new CapturingBrowserCommandRunner();
        var source = new QueueCallbackRequestSource(
            new PortableOAuthCallbackRequest("/oauth/callback", "/oauth/callback?code=abc%20123&state=state-1"));
        var flow = CreateFlow(runner, _ => source);

        var result = await flow.StartAuthorizationAsync("github", TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("github", result.Platform);
        Assert.Equal("abc 123", result.Code);
        Assert.Equal("state-1", result.State);
        Assert.True(source.Stopped);
        var command = Assert.Single(runner.Commands);
        Assert.Equal("/usr/bin/open", command.FileName);
        Assert.Contains("https://github.com/login/oauth/authorize?", command.Arguments[0], StringComparison.Ordinal);
        Assert.Contains("client_id=client-1", command.Arguments[0], StringComparison.Ordinal);
        Assert.Contains("state=state-1", command.Arguments[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartAuthorizationAsync_returns_prepare_error_without_opening_browser_or_listener()
    {
        var runner = new CapturingBrowserCommandRunner();
        var sourceCreated = false;
        var flow = CreateFlow(runner, _ =>
        {
            sourceCreated = true;
            return new QueueCallbackRequestSource();
        });

        var result = await flow.StartAuthorizationAsync("unknown", TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("不支持的平台: unknown", result.ErrorMessage);
        Assert.Equal("unknown", result.Platform);
        Assert.Equal("state-1", result.State);
        Assert.False(sourceCreated);
        Assert.Empty(runner.Commands);
    }

    [Fact]
    public async Task StartAuthorizationAsync_returns_missing_client_id_without_opening_browser_or_listener()
    {
        var runner = new CapturingBrowserCommandRunner();
        var flow = new PortableOAuthAuthorizationFlow(
            () => PortableOAuthAuthorizationCore.CreateDefaultConfigs(),
            new PortableOAuthBrowserLauncher(runner),
            _ => throw new InvalidOperationException("listener should not start"),
            () => "state-1",
            callbackPort: 23456);

        var result = await flow.StartAuthorizationAsync("github", TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("github 尚未配置ClientId，请在设置中配置", result.ErrorMessage);
        Assert.Equal("github", result.Platform);
        Assert.Equal("state-1", result.State);
        Assert.Empty(runner.Commands);
    }

    [Fact]
    public async Task StartAuthorizationAsync_stops_listener_when_browser_open_fails()
    {
        var runner = new CapturingBrowserCommandRunner
        {
            NextResult = new PortableBrowserCommandResult(1, "", "open failed")
        };
        var source = new BlockingCallbackRequestSource();
        var flow = CreateFlow(runner, _ => source);

        var result = await flow.StartAuthorizationAsync("github", TimeSpan.FromSeconds(30), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("无法打开 OAuth 授权浏览器: open failed", result.ErrorMessage);
        Assert.Equal("github", result.Platform);
        Assert.Equal("state-1", result.State);
        Assert.True(source.Stopped);
    }

    [Fact]
    public async Task StartAuthorizationAsync_maps_cancellation_to_timeout_message()
    {
        var runner = new CapturingBrowserCommandRunner();
        var source = new BlockingCallbackRequestSource();
        var flow = CreateFlow(runner, _ => source);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await flow.StartAuthorizationAsync("github", TimeSpan.FromSeconds(30), cts.Token);

        Assert.False(result.Success);
        Assert.Equal("授权超时，请重试", result.ErrorMessage);
        Assert.Equal("github", result.Platform);
        Assert.Equal("state-1", result.State);
        Assert.True(source.Stopped);
    }

    [Fact]
    public void Constructor_rejects_missing_dependencies()
    {
        var runner = new CapturingBrowserCommandRunner();
        var launcher = new PortableOAuthBrowserLauncher(runner);

        Assert.Throws<ArgumentNullException>(() => new PortableOAuthAuthorizationFlow(
            null!,
            launcher,
            _ => new QueueCallbackRequestSource(),
            () => "state-1",
            23456));
        Assert.Throws<ArgumentNullException>(() => new PortableOAuthAuthorizationFlow(
            () => PortableOAuthAuthorizationCore.CreateDefaultConfigs(),
            null!,
            _ => new QueueCallbackRequestSource(),
            () => "state-1",
            23456));
        Assert.Throws<ArgumentNullException>(() => new PortableOAuthAuthorizationFlow(
            () => PortableOAuthAuthorizationCore.CreateDefaultConfigs(),
            launcher,
            null!,
            () => "state-1",
            23456));
    }

    private static PortableOAuthAuthorizationFlow CreateFlow(
        CapturingBrowserCommandRunner runner,
        Func<int, IPortableOAuthCallbackRequestSource> sourceFactory)
    {
        var configs = PortableOAuthAuthorizationCore.CreateDefaultConfigs();
        configs["github"].ClientId = "client-1";
        return new PortableOAuthAuthorizationFlow(
            () => configs,
            new PortableOAuthBrowserLauncher(runner),
            sourceFactory,
            () => "state-1",
            callbackPort: 23456);
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

    private sealed class QueueCallbackRequestSource : IPortableOAuthCallbackRequestSource
    {
        private readonly Queue<PortableOAuthCallbackRequest> _requests;

        public QueueCallbackRequestSource(params PortableOAuthCallbackRequest[] requests)
        {
            _requests = new Queue<PortableOAuthCallbackRequest>(requests);
        }

        public bool Stopped { get; private set; }

        public Task<PortableOAuthCallbackRequest> WaitForRequestAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_requests.Dequeue());
        }

        public Task WriteResponseAsync(PortableOAuthCallbackResponse response, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public void Stop()
        {
            Stopped = true;
        }
    }

    private sealed class BlockingCallbackRequestSource : IPortableOAuthCallbackRequestSource
    {
        public bool Stopped { get; private set; }

        public Task<PortableOAuthCallbackRequest> WaitForRequestAsync(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<PortableOAuthCallbackRequest>();
            cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
            return tcs.Task;
        }

        public Task WriteResponseAsync(PortableOAuthCallbackResponse response, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public void Stop()
        {
            Stopped = true;
        }
    }
}
