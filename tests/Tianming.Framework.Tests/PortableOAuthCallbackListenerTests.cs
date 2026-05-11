using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableOAuthCallbackListenerTests
{
    [Fact]
    public async Task WaitForCallbackAsync_returns_success_and_writes_success_html()
    {
        var source = new QueueCallbackRequestSource(
            new PortableOAuthCallbackRequest("/oauth/callback", "/oauth/callback?code=abc%20123&state=state-1"));
        var listener = new PortableOAuthCallbackListener(source);

        var result = await listener.WaitForCallbackAsync("state-1", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("abc 123", result.Code);
        Assert.Equal("state-1", result.State);
        var response = Assert.Single(source.Responses);
        Assert.Equal(200, response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", response.ContentType);
        Assert.Contains("授权成功", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WaitForCallbackAsync_replies_404_for_non_callback_path_and_keeps_waiting()
    {
        var source = new QueueCallbackRequestSource(
            new PortableOAuthCallbackRequest("/favicon.ico", "/favicon.ico"),
            new PortableOAuthCallbackRequest("/oauth/callback", "/oauth/callback?code=abc&state=state-1"));
        var listener = new PortableOAuthCallbackListener(source);

        var result = await listener.WaitForCallbackAsync("state-1", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(2, source.Responses.Count);
        Assert.Equal(404, source.Responses[0].StatusCode);
        Assert.Equal(200, source.Responses[1].StatusCode);
    }

    [Theory]
    [InlineData("/oauth/callback?error=access_denied&error_description=user%20denied", "user denied", "user denied")]
    [InlineData("/oauth/callback?code=abc&state=wrong", "State不匹配，可能存在CSRF攻击", "安全验证失败，请重试")]
    [InlineData("/oauth/callback?state=state-1", "未获取到授权码", "未获取到授权码")]
    public async Task WaitForCallbackAsync_returns_error_result_and_writes_error_html(
        string pathAndQuery,
        string expectedError,
        string expectedHtmlMessage)
    {
        var source = new QueueCallbackRequestSource(
            new PortableOAuthCallbackRequest("/oauth/callback", pathAndQuery));
        var listener = new PortableOAuthCallbackListener(source);

        var result = await listener.WaitForCallbackAsync("state-1", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(expectedError, result.ErrorMessage);
        var response = Assert.Single(source.Responses);
        Assert.Equal(200, response.StatusCode);
        Assert.Contains(expectedHtmlMessage, response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WaitForCallbackAsync_stops_source_after_result()
    {
        var source = new QueueCallbackRequestSource(
            new PortableOAuthCallbackRequest("/oauth/callback", "/oauth/callback?code=abc&state=state-1"));
        var listener = new PortableOAuthCallbackListener(source);

        await listener.WaitForCallbackAsync("state-1", CancellationToken.None);

        Assert.True(source.Stopped);
    }

    [Fact]
    public void Constructor_rejects_missing_source()
    {
        Assert.Throws<ArgumentNullException>(() => new PortableOAuthCallbackListener(null!));
    }

    private sealed class QueueCallbackRequestSource : IPortableOAuthCallbackRequestSource
    {
        private readonly Queue<PortableOAuthCallbackRequest> _requests;

        public QueueCallbackRequestSource(params PortableOAuthCallbackRequest[] requests)
        {
            _requests = new Queue<PortableOAuthCallbackRequest>(requests);
        }

        public List<PortableOAuthCallbackResponse> Responses { get; } = new();
        public bool Stopped { get; private set; }

        public Task<PortableOAuthCallbackRequest> WaitForRequestAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_requests.Dequeue());
        }

        public Task WriteResponseAsync(PortableOAuthCallbackResponse response, CancellationToken cancellationToken)
        {
            Responses.Add(response);
            return Task.CompletedTask;
        }

        public void Stop()
        {
            Stopped = true;
        }
    }
}
