namespace TM.Framework.Security;

public interface IPortableOAuthAuthorizationFlow
{
    Task<PortableOAuthAuthorizationResult> StartAuthorizationAsync(
        string platform,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

public sealed class PortableOAuthAuthorizationFlow : IPortableOAuthAuthorizationFlow
{
    private readonly Func<IReadOnlyDictionary<string, PortableOAuthProviderConfig>> _providerConfigs;
    private readonly PortableOAuthBrowserLauncher _browserLauncher;
    private readonly Func<int, IPortableOAuthCallbackRequestSource> _callbackSourceFactory;
    private readonly Func<string> _stateFactory;
    private readonly int _callbackPort;

    public PortableOAuthAuthorizationFlow(PortableOAuthProviderConfigStore providerConfigStore, int callbackPort = 23456)
        : this(
            RequireProviderConfigStore(providerConfigStore).GetProviders,
            new PortableOAuthBrowserLauncher(),
            port => new HttpListenerPortableOAuthCallbackRequestSource(port),
            () => Guid.NewGuid().ToString("N"),
            callbackPort)
    {
    }

    public PortableOAuthAuthorizationFlow(
        Func<IReadOnlyDictionary<string, PortableOAuthProviderConfig>> providerConfigs,
        PortableOAuthBrowserLauncher browserLauncher,
        Func<int, IPortableOAuthCallbackRequestSource> callbackSourceFactory,
        Func<string> stateFactory,
        int callbackPort)
    {
        _providerConfigs = providerConfigs ?? throw new ArgumentNullException(nameof(providerConfigs));
        _browserLauncher = browserLauncher ?? throw new ArgumentNullException(nameof(browserLauncher));
        _callbackSourceFactory = callbackSourceFactory ?? throw new ArgumentNullException(nameof(callbackSourceFactory));
        _stateFactory = stateFactory ?? throw new ArgumentNullException(nameof(stateFactory));
        if (callbackPort <= 0 || callbackPort > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(callbackPort), "Callback port must be between 1 and 65535.");
        }

        _callbackPort = callbackPort;
    }

    public async Task<PortableOAuthAuthorizationResult> StartAuthorizationAsync(
        string platform,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var state = _stateFactory();
        var prepared = PortableOAuthAuthorizationCore.PrepareAuthorization(
            platform,
            _providerConfigs(),
            state,
            _callbackPort);
        if (!prepared.Success)
        {
            return prepared;
        }

        using var timeoutCts = CreateTimeoutCancellation(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var listener = new PortableOAuthCallbackListener(_callbackSourceFactory(_callbackPort));
        var callbackTask = listener.WaitForCallbackAsync(state, linkedCts.Token);

        if (linkedCts.IsCancellationRequested)
        {
            await StopCallbackTaskAsync(callbackTask);
            return BuildTimeoutResult(platform, state);
        }

        var browserResult = _browserLauncher.OpenAuthorizationUrl(prepared.AuthorizationUrl ?? string.Empty);
        if (!browserResult.Success)
        {
            linkedCts.Cancel();
            await StopCallbackTaskAsync(callbackTask);
            return new PortableOAuthAuthorizationResult
            {
                Success = false,
                Platform = platform,
                State = state,
                ErrorMessage = browserResult.ErrorMessage
            };
        }

        try
        {
            var result = await callbackTask;
            result.Platform = platform;
            result.State = state;
            return result;
        }
        catch (OperationCanceledException)
        {
            return BuildTimeoutResult(platform, state);
        }
        catch (Exception ex)
        {
            return new PortableOAuthAuthorizationResult
            {
                Success = false,
                Platform = platform,
                State = state,
                ErrorMessage = ex.Message
            };
        }
    }

    private static CancellationTokenSource CreateTimeoutCancellation(TimeSpan timeout)
    {
        var source = new CancellationTokenSource();
        if (timeout != Timeout.InfiniteTimeSpan)
        {
            source.CancelAfter(timeout <= TimeSpan.Zero ? TimeSpan.Zero : timeout);
        }

        return source;
    }

    private static async Task StopCallbackTaskAsync(Task<PortableOAuthAuthorizationResult> callbackTask)
    {
        try
        {
            await callbackTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static PortableOAuthAuthorizationResult BuildTimeoutResult(string platform, string state)
    {
        return new PortableOAuthAuthorizationResult
        {
            Success = false,
            Platform = platform,
            State = state,
            ErrorMessage = "授权超时，请重试"
        };
    }

    private static PortableOAuthProviderConfigStore RequireProviderConfigStore(PortableOAuthProviderConfigStore providerConfigStore)
    {
        return providerConfigStore ?? throw new ArgumentNullException(nameof(providerConfigStore));
    }
}
