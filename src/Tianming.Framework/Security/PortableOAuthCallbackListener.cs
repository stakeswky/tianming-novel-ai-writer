using System.Net;
using System.Text;

namespace TM.Framework.Security;

public sealed record PortableOAuthCallbackRequest(string AbsolutePath, string PathAndQuery);

public sealed record PortableOAuthCallbackResponse(int StatusCode, string ContentType, string Body);

public interface IPortableOAuthCallbackRequestSource
{
    Task<PortableOAuthCallbackRequest> WaitForRequestAsync(CancellationToken cancellationToken);
    Task WriteResponseAsync(PortableOAuthCallbackResponse response, CancellationToken cancellationToken);
    void Stop();
}

public sealed class PortableOAuthCallbackListener
{
    private readonly IPortableOAuthCallbackRequestSource _source;

    public PortableOAuthCallbackListener(IPortableOAuthCallbackRequestSource source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
    }

    public async Task<PortableOAuthAuthorizationResult> WaitForCallbackAsync(
        string expectedState,
        CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                var request = await _source.WaitForRequestAsync(cancellationToken);
                if (!string.Equals(request.AbsolutePath, "/oauth/callback", StringComparison.OrdinalIgnoreCase))
                {
                    await _source.WriteResponseAsync(
                        new PortableOAuthCallbackResponse(404, "text/plain; charset=utf-8", string.Empty),
                        cancellationToken);
                    continue;
                }

                var result = PortableOAuthAuthorizationCore.ParseCallback(request.PathAndQuery, expectedState);
                await _source.WriteResponseAsync(
                    new PortableOAuthCallbackResponse(
                        200,
                        "text/html; charset=utf-8",
                        GenerateResponseHtml(result.Success, GetBrowserMessage(result))),
                    cancellationToken);
                return result;
            }
        }
        finally
        {
            _source.Stop();
        }
    }

    private static string GetBrowserMessage(PortableOAuthAuthorizationResult result)
    {
        if (result.Success)
        {
            return "授权成功！您可以关闭此窗口。";
        }

        return string.Equals(result.ErrorMessage, "State不匹配，可能存在CSRF攻击", StringComparison.Ordinal)
            ? "安全验证失败，请重试"
            : result.ErrorMessage ?? "授权失败";
    }

    private static string GenerateResponseHtml(bool success, string message)
    {
        var color = success ? "#4CAF50" : "#f44336";
        var icon = success ? "✓" : "✗";
        var encodedMessage = WebUtility.HtmlEncode(message);

        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>OAuth授权 - 天命</title>
    <style>
        body {{
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            display: flex;
            justify-content: center;
            align-items: center;
            height: 100vh;
            margin: 0;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
        }}
        .container {{
            text-align: center;
            background: white;
            padding: 60px 80px;
            border-radius: 16px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.3);
        }}
        .icon {{
            font-size: 72px;
            color: {color};
            margin-bottom: 20px;
        }}
        .message {{
            font-size: 24px;
            color: #333;
            margin-bottom: 20px;
        }}
        .hint {{
            font-size: 14px;
            color: #666;
        }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""icon"">{icon}</div>
        <div class=""message"">{encodedMessage}</div>
        <div class=""hint"">此窗口可以安全关闭</div>
    </div>
    <script>setTimeout(() => window.close(), 3000);</script>
</body>
</html>";
    }
}

public sealed class HttpListenerPortableOAuthCallbackRequestSource : IPortableOAuthCallbackRequestSource
{
    private readonly HttpListener _listener = new();
    private HttpListenerContext? _currentContext;
    private bool _started;

    public HttpListenerPortableOAuthCallbackRequestSource(int callbackPort)
    {
        if (callbackPort <= 0 || callbackPort > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(callbackPort), "Callback port must be between 1 and 65535.");
        }

        _listener.Prefixes.Add($"http://localhost:{callbackPort}/");
    }

    public async Task<PortableOAuthCallbackRequest> WaitForRequestAsync(CancellationToken cancellationToken)
    {
        if (!_started)
        {
            _listener.Start();
            _started = true;
        }

        var contextTask = _listener.GetContextAsync();
        var completedTask = await Task.WhenAny(contextTask, Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));
        if (completedTask != contextTask)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        _currentContext = await contextTask;
        var url = _currentContext.Request.Url;
        return new PortableOAuthCallbackRequest(url?.AbsolutePath ?? string.Empty, url?.PathAndQuery ?? string.Empty);
    }

    public async Task WriteResponseAsync(PortableOAuthCallbackResponse response, CancellationToken cancellationToken)
    {
        if (_currentContext == null)
        {
            return;
        }

        var httpResponse = _currentContext.Response;
        httpResponse.StatusCode = response.StatusCode;
        httpResponse.ContentType = response.ContentType;

        if (!string.IsNullOrEmpty(response.Body))
        {
            var buffer = Encoding.UTF8.GetBytes(response.Body);
            httpResponse.ContentLength64 = buffer.Length;
            await httpResponse.OutputStream.WriteAsync(buffer, cancellationToken);
        }

        httpResponse.Close();
        _currentContext = null;
    }

    public void Stop()
    {
        if (_listener.IsListening)
        {
            _listener.Stop();
        }

        _listener.Close();
    }
}
