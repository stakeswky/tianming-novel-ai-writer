using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TM.Framework.Appearance;

namespace TM.Framework.Security;

public sealed class PortableHeartbeatRequest
{
    [JsonPropertyName("deviceId")] public string DeviceId { get; set; } = string.Empty;
    [JsonPropertyName("timestamp")] public long Timestamp { get; set; }
    [JsonPropertyName("challengeResponse")] public string? ChallengeResponse { get; set; }
}

public sealed class PortableHeartbeatResult
{
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? Message { get; init; }
    public long ServerTime { get; init; }
    public string? Announcement { get; init; }
    public bool ForceUpdate { get; init; }
    public string? MinVersion { get; init; }
    public string? Challenge { get; init; }
    public bool SubscriptionValid { get; init; }
    public long? SubscriptionExpireTime { get; init; }
    public List<string> AllowedFeatures { get; init; } = [];
    public List<PortableServerHeartbeatEvent> Events { get; init; } = [];
}

public enum PortableServerHeartbeatEventType
{
    Announcement,
    ForceUpdate,
    ForceLogout
}

public sealed record PortableServerHeartbeatEvent(
    PortableServerHeartbeatEventType Type,
    string Message);

public sealed class PortableServerHeartbeatPresentationState
{
    public string? LastAnnouncementShown { get; set; }
}

public enum PortableServerHeartbeatPresentationActionKind
{
    Info,
    ForceUpdate,
    ReturnToLogin
}

public sealed record PortableServerHeartbeatPresentationAction(
    PortableServerHeartbeatPresentationActionKind Kind,
    string Title,
    string Message);

public static class PortableServerHeartbeatPresentationPolicy
{
    public static IReadOnlyList<PortableServerHeartbeatPresentationAction> CreateActions(
        PortableHeartbeatResult result,
        PortableServerHeartbeatPresentationState state)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(state);

        var actions = new List<PortableServerHeartbeatPresentationAction>();
        foreach (var heartbeatEvent in result.Events)
        {
            switch (heartbeatEvent.Type)
            {
                case PortableServerHeartbeatEventType.Announcement:
                    AddAnnouncement(actions, state, heartbeatEvent.Message);
                    break;
                case PortableServerHeartbeatEventType.ForceUpdate:
                    actions.Add(new PortableServerHeartbeatPresentationAction(
                        PortableServerHeartbeatPresentationActionKind.ForceUpdate,
                        "强制更新",
                        CreateForceUpdateMessage(heartbeatEvent.Message)));
                    break;
                case PortableServerHeartbeatEventType.ForceLogout:
                    actions.Add(new PortableServerHeartbeatPresentationAction(
                        PortableServerHeartbeatPresentationActionKind.ReturnToLogin,
                        "登录状态失效",
                        heartbeatEvent.Message));
                    break;
            }
        }

        return actions;
    }

    private static void AddAnnouncement(
        List<PortableServerHeartbeatPresentationAction> actions,
        PortableServerHeartbeatPresentationState state,
        string message)
    {
        if (string.IsNullOrWhiteSpace(message) || message == state.LastAnnouncementShown)
        {
            return;
        }

        state.LastAnnouncementShown = message;
        actions.Add(new PortableServerHeartbeatPresentationAction(
            PortableServerHeartbeatPresentationActionKind.Info,
            "系统公告",
            message));
    }

    private static string CreateForceUpdateMessage(string minVersion)
    {
        return string.IsNullOrWhiteSpace(minVersion)
            ? "当前版本过低，请升级到最新版本。"
            : $"当前版本过低，请升级到 {minVersion} 或更高版本。";
    }
}

public sealed class PortableHeartbeatLoopState
{
    public int ConsecutiveFailures { get; set; }
    public bool ExpireWarningShown { get; set; }
}

public enum PortableHeartbeatLoopActionKind
{
    None,
    Warning,
    ReturnToLogin
}

public sealed record PortableHeartbeatLoopAction(
    PortableHeartbeatLoopActionKind Kind,
    string? Title = null,
    string? Message = null)
{
    public static PortableHeartbeatLoopAction None { get; } = new(PortableHeartbeatLoopActionKind.None);
}

public static class PortableHeartbeatLoopPolicy
{
    public const int MaxHeartbeatFailCount = 10;

    public static PortableHeartbeatLoopAction PrecheckLoginState(bool isLoggedIn)
    {
        return isLoggedIn
            ? PortableHeartbeatLoopAction.None
            : new PortableHeartbeatLoopAction(
                PortableHeartbeatLoopActionKind.ReturnToLogin,
                Message: "登录已过期，请重新登录");
    }

    public static PortableHeartbeatLoopAction ApplyHeartbeatResult(
        PortableHeartbeatLoopState state,
        PortableHeartbeatResult result,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(result);

        if (result.Success)
        {
            state.ConsecutiveFailures = 0;
            if (!result.SubscriptionValid)
            {
                return new PortableHeartbeatLoopAction(
                    PortableHeartbeatLoopActionKind.ReturnToLogin,
                    Message: "订阅已到期，请续费后重新登录");
            }

            if (!state.ExpireWarningShown && result.SubscriptionExpireTime.HasValue)
            {
                var expireUtc = DateTimeOffset.FromUnixTimeSeconds(result.SubscriptionExpireTime.Value).UtcDateTime;
                var remaining = expireUtc - nowUtc;
                if (remaining.TotalHours <= 24)
                {
                    state.ExpireWarningShown = true;
                    var remainingText = remaining.TotalHours < 1
                        ? "不到1小时"
                        : $"{(int)remaining.TotalHours} 小时";
                    return new PortableHeartbeatLoopAction(
                        PortableHeartbeatLoopActionKind.Warning,
                        "订阅即将到期",
                        $"您的订阅将在 {remainingText}后到期，请及时续费。");
                }
            }

            return PortableHeartbeatLoopAction.None;
        }

        state.ConsecutiveFailures++;
        if (state.ConsecutiveFailures >= MaxHeartbeatFailCount)
        {
            return new PortableHeartbeatLoopAction(
                PortableHeartbeatLoopActionKind.ReturnToLogin,
                Message: "网络连接丢失，请检查网络后重新登录");
        }

        return new PortableHeartbeatLoopAction(
            PortableHeartbeatLoopActionKind.Warning,
            "网络不稳定",
            $"心跳连接失败（{state.ConsecutiveFailures}/{MaxHeartbeatFailCount}），请检查网络");
    }
}

public sealed class PortableReturnToLoginCoordinator
{
    public bool IsReturningToLogin { get; private set; }

    public bool TryRequest(string message, Action<string>? dispatch = null)
    {
        if (IsReturningToLogin)
        {
            return false;
        }

        IsReturningToLogin = true;
        dispatch?.Invoke(message);
        return true;
    }

    public void Reset()
    {
        IsReturningToLogin = false;
    }
}

public enum PortableReturnToLoginOutcome
{
    LoginSucceeded,
    LoginCancelled,
    Failed,
    AlreadyInProgress
}

public sealed record PortableReturnToLoginResult(
    PortableReturnToLoginOutcome Outcome,
    string? LoggedInUsername = null,
    bool ShutdownRequested = false,
    string? ErrorMessage = null)
{
    public static PortableReturnToLoginResult LoginSucceeded(string? loggedInUsername)
    {
        return new PortableReturnToLoginResult(
            PortableReturnToLoginOutcome.LoginSucceeded,
            loggedInUsername);
    }

    public static PortableReturnToLoginResult LoginCancelled()
    {
        return new PortableReturnToLoginResult(PortableReturnToLoginOutcome.LoginCancelled);
    }
}

public sealed class PortableReturnToLoginNavigationController
{
    private readonly Func<CancellationToken, Task> _stopSessionServicesAsync;
    private readonly Func<CancellationToken, Task> _clearAuthStateAsync;
    private readonly IReadOnlyList<Func<CancellationToken, Task>> _flushBeforeLoginAsync;
    private readonly Func<string, CancellationToken, Task<PortableReturnToLoginResult>> _showLoginAsync;
    private readonly Func<string?, CancellationToken, Task> _restartSessionServicesAsync;
    private readonly Func<CancellationToken, Task> _shutdownAsync;
    private int _isReturningToLogin;

    public PortableReturnToLoginNavigationController(
        Func<CancellationToken, Task> stopSessionServicesAsync,
        Func<CancellationToken, Task> clearAuthStateAsync,
        IEnumerable<Func<CancellationToken, Task>>? flushBeforeLoginAsync,
        Func<string, CancellationToken, Task<PortableReturnToLoginResult>> showLoginAsync,
        Func<string?, CancellationToken, Task> restartSessionServicesAsync,
        Func<CancellationToken, Task> shutdownAsync)
    {
        _stopSessionServicesAsync = stopSessionServicesAsync
            ?? throw new ArgumentNullException(nameof(stopSessionServicesAsync));
        _clearAuthStateAsync = clearAuthStateAsync
            ?? throw new ArgumentNullException(nameof(clearAuthStateAsync));
        _flushBeforeLoginAsync = flushBeforeLoginAsync?.ToArray() ?? [];
        _showLoginAsync = showLoginAsync ?? throw new ArgumentNullException(nameof(showLoginAsync));
        _restartSessionServicesAsync = restartSessionServicesAsync
            ?? throw new ArgumentNullException(nameof(restartSessionServicesAsync));
        _shutdownAsync = shutdownAsync ?? throw new ArgumentNullException(nameof(shutdownAsync));
    }

    public bool IsReturningToLogin => Volatile.Read(ref _isReturningToLogin) == 1;

    public async Task<PortableReturnToLoginResult> ReturnToLoginAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _isReturningToLogin, 1) == 1)
        {
            return new PortableReturnToLoginResult(PortableReturnToLoginOutcome.AlreadyInProgress);
        }

        try
        {
            await _stopSessionServicesAsync(cancellationToken).ConfigureAwait(false);
            await _clearAuthStateAsync(cancellationToken).ConfigureAwait(false);
            foreach (var flushAsync in _flushBeforeLoginAsync)
            {
                await flushAsync(cancellationToken).ConfigureAwait(false);
            }

            var loginResult = await _showLoginAsync(message, cancellationToken).ConfigureAwait(false);
            if (loginResult.Outcome != PortableReturnToLoginOutcome.LoginSucceeded)
            {
                await _shutdownAsync(cancellationToken).ConfigureAwait(false);
                return loginResult with { ShutdownRequested = true };
            }

            await _restartSessionServicesAsync(loginResult.LoggedInUsername, cancellationToken)
                .ConfigureAwait(false);
            return loginResult;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            await _shutdownAsync(CancellationToken.None).ConfigureAwait(false);
            return new PortableReturnToLoginResult(
                PortableReturnToLoginOutcome.Failed,
                ShutdownRequested: true,
                ErrorMessage: ex.Message);
        }
        finally
        {
            Volatile.Write(ref _isReturningToLogin, 0);
        }
    }
}

public sealed record PortableServerHeartbeatTickResult(
    PortableHeartbeatResult? HeartbeatResult,
    PortableHeartbeatLoopAction LoopAction,
    IReadOnlyList<PortableServerHeartbeatPresentationAction> PresentationActions);

public sealed class PortableServerHeartbeatRunner
{
    private readonly Func<bool> _isLoggedIn;
    private readonly Func<CancellationToken, Task<PortableHeartbeatResult>> _sendHeartbeatAsync;
    private readonly Action<PortableServerHeartbeatPresentationAction> _dispatchAction;
    private readonly Action<string> _dispatchReturnToLogin;
    private readonly Func<DateTime> _nowUtc;
    private readonly int _maxSendAttempts;
    private readonly PortableHeartbeatLoopState _loopState = new();
    private readonly PortableServerHeartbeatPresentationState _presentationState = new();
    private readonly PortableReturnToLoginCoordinator _returnToLoginCoordinator = new();

    public PortableServerHeartbeatRunner(
        Func<bool> isLoggedIn,
        Func<CancellationToken, Task<PortableHeartbeatResult>> sendHeartbeatAsync,
        Action<PortableServerHeartbeatPresentationAction>? dispatchAction = null,
        Action<string>? dispatchReturnToLogin = null,
        Func<DateTime>? nowUtc = null,
        int maxSendAttempts = 1)
    {
        _isLoggedIn = isLoggedIn ?? throw new ArgumentNullException(nameof(isLoggedIn));
        _sendHeartbeatAsync = sendHeartbeatAsync ?? throw new ArgumentNullException(nameof(sendHeartbeatAsync));
        _dispatchAction = dispatchAction ?? (_ => { });
        _dispatchReturnToLogin = dispatchReturnToLogin ?? (_ => { });
        _nowUtc = nowUtc ?? (() => DateTime.UtcNow);
        _maxSendAttempts = Math.Max(1, maxSendAttempts);
    }

    public async Task<PortableServerHeartbeatTickResult> TickAsync(
        CancellationToken cancellationToken = default)
    {
        var precheck = PortableHeartbeatLoopPolicy.PrecheckLoginState(_isLoggedIn());
        if (precheck.Kind == PortableHeartbeatLoopActionKind.ReturnToLogin)
        {
            DispatchLoopAction(precheck);
            return new PortableServerHeartbeatTickResult(
                HeartbeatResult: null,
                precheck,
                []);
        }

        var heartbeatResult = await SendHeartbeatOrFailureAsync(cancellationToken).ConfigureAwait(false);
        var loopAction = PortableHeartbeatLoopPolicy.ApplyHeartbeatResult(
            _loopState,
            heartbeatResult,
            _nowUtc());
        DispatchLoopAction(loopAction);

        var presentationActions = PortableServerHeartbeatPresentationPolicy.CreateActions(
            heartbeatResult,
            _presentationState);
        foreach (var action in presentationActions)
        {
            DispatchPresentationAction(action);
        }

        return new PortableServerHeartbeatTickResult(
            heartbeatResult,
            loopAction,
            presentationActions);
    }

    private async Task<PortableHeartbeatResult> SendHeartbeatOrFailureAsync(
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= _maxSendAttempts; attempt++)
        {
            try
            {
                return await _sendHeartbeatAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (attempt >= _maxSendAttempts)
            {
                return new PortableHeartbeatResult
                {
                    Success = false,
                    ErrorCode = "NETWORK_ERROR",
                    Message = ex.Message
                };
            }
            catch (Exception)
            {
            }
        }

        return new PortableHeartbeatResult
        {
            Success = false,
            ErrorCode = "NETWORK_ERROR",
            Message = "Heartbeat failed."
        };
    }

    private void DispatchLoopAction(PortableHeartbeatLoopAction action)
    {
        if (action.Kind != PortableHeartbeatLoopActionKind.ReturnToLogin
            || string.IsNullOrWhiteSpace(action.Message))
        {
            return;
        }

        _returnToLoginCoordinator.TryRequest(action.Message, _dispatchReturnToLogin);
    }

    private void DispatchPresentationAction(PortableServerHeartbeatPresentationAction action)
    {
        if (action.Kind == PortableServerHeartbeatPresentationActionKind.ReturnToLogin)
        {
            _returnToLoginCoordinator.TryRequest(action.Message, _dispatchReturnToLogin);
            return;
        }

        _dispatchAction(action);
    }
}

public sealed class PortableServerHeartbeatPeriodicService : IAsyncDisposable
{
    private readonly PortableServerHeartbeatRunner _runner;
    private readonly TimeSpan _interval;
    private readonly IPortableTimerTickSource _tickSource;
    private readonly bool _runImmediately;
    private readonly object _lock = new();
    private CancellationTokenSource? _stopSource;
    private Task? _loopTask;

    public PortableServerHeartbeatPeriodicService(
        PortableServerHeartbeatRunner runner,
        TimeSpan? interval = null,
        IPortableTimerTickSource? tickSource = null,
        bool runImmediately = true)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _interval = interval ?? TimeSpan.FromMinutes(1);
        if (_interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Heartbeat interval must be positive.");
        }

        _tickSource = tickSource ?? new PortablePeriodicTimerTickSource();
        _runImmediately = runImmediately;
    }

    public bool IsRunning { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (IsRunning)
            {
                return;
            }

            _stopSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            IsRunning = true;
            _loopTask = RunLoopAsync(_stopSource.Token);
        }

        if (_runImmediately)
        {
            await _runner.TickAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task StopAsync()
    {
        Task? loopTask;
        CancellationTokenSource? stopSource;

        lock (_lock)
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            loopTask = _loopTask;
            stopSource = _stopSource;
            _loopTask = null;
            _stopSource = null;
        }

        stopSource?.Cancel();
        if (loopTask is not null)
        {
            try
            {
                await loopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        stopSource?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        await foreach (var _ in _tickSource.WaitForTicksAsync(_interval, cancellationToken)
                           .WithCancellation(cancellationToken)
                           .ConfigureAwait(false))
        {
            await _runner.TickAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}

public static class PortableServerHeartbeatService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static PortableHeartbeatRequest CreateRequest(
        string deviceId,
        long timestamp,
        string? lastChallenge,
        string? sessionKey)
    {
        return new PortableHeartbeatRequest
        {
            DeviceId = deviceId,
            Timestamp = timestamp,
            ChallengeResponse = CreateChallengeResponse(lastChallenge, sessionKey)
        };
    }

    public static string SerializeRequest(PortableHeartbeatRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return JsonSerializer.Serialize(request, JsonOptions);
    }

    public static PortableHeartbeatResult ParseResponse(string json)
    {
        try
        {
            var response = JsonSerializer.Deserialize<HeartbeatResponseEnvelope>(json, JsonOptions);
            if (response?.Success == true)
            {
                return MapSuccess(response.Data);
            }

            return MapFailure(response);
        }
        catch (JsonException ex)
        {
            return new PortableHeartbeatResult
            {
                Success = false,
                ErrorCode = "NETWORK_ERROR",
                Message = $"Invalid JSON: {ex.Message}"
            };
        }
    }

    private static string? CreateChallengeResponse(string? lastChallenge, string? sessionKey)
    {
        if (string.IsNullOrWhiteSpace(lastChallenge) || string.IsNullOrWhiteSpace(sessionKey))
        {
            return null;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(sessionKey));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(lastChallenge)));
    }

    private static PortableHeartbeatResult MapSuccess(HeartbeatResponseData? data)
    {
        var events = new List<PortableServerHeartbeatEvent>();
        if (!string.IsNullOrWhiteSpace(data?.Announcement))
        {
            events.Add(new PortableServerHeartbeatEvent(
                PortableServerHeartbeatEventType.Announcement,
                data.Announcement));
        }

        if (data?.ForceUpdate == true)
        {
            events.Add(new PortableServerHeartbeatEvent(
                PortableServerHeartbeatEventType.ForceUpdate,
                data.MinVersion ?? string.Empty));
        }

        return new PortableHeartbeatResult
        {
            Success = true,
            ServerTime = data?.ServerTime ?? 0,
            Announcement = data?.Announcement,
            ForceUpdate = data?.ForceUpdate ?? false,
            MinVersion = data?.MinVersion,
            Challenge = data?.Challenge,
            SubscriptionValid = data?.SubscriptionValid ?? false,
            SubscriptionExpireTime = data?.SubscriptionExpireTime,
            AllowedFeatures = data?.AllowedFeatures ?? [],
            Events = events
        };
    }

    private static PortableHeartbeatResult MapFailure(HeartbeatResponseEnvelope? response)
    {
        var events = new List<PortableServerHeartbeatEvent>();
        if (string.Equals(response?.ErrorCode, "INVALID_TOKEN", StringComparison.Ordinal) ||
            string.Equals(response?.ErrorCode, "AUTH_DEVICE_KICKED", StringComparison.Ordinal) ||
            string.Equals(response?.ErrorCode, "USER_INVALID", StringComparison.Ordinal))
        {
            events.Add(new PortableServerHeartbeatEvent(
                PortableServerHeartbeatEventType.ForceLogout,
                ResolveForceLogoutMessage(response?.ErrorCode, response?.Message)));
        }

        return new PortableHeartbeatResult
        {
            Success = false,
            ErrorCode = response?.ErrorCode,
            Message = response?.Message,
            Events = events
        };
    }

    private static string ResolveForceLogoutMessage(string? errorCode, string? message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            return message;
        }

        return errorCode switch
        {
            "AUTH_DEVICE_KICKED" => "您的账号已在其他设备登录",
            "USER_INVALID" => "账号状态异常",
            _ => "登录已过期，请重新登录"
        };
    }

    private sealed class HeartbeatResponseEnvelope
    {
        [JsonPropertyName("success")] public bool Success { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
        [JsonPropertyName("errorCode")] public string? ErrorCode { get; set; }
        [JsonPropertyName("data")] public HeartbeatResponseData? Data { get; set; }
    }

    private sealed class HeartbeatResponseData
    {
        [JsonPropertyName("serverTime")] public long ServerTime { get; set; }
        [JsonPropertyName("announcement")] public string? Announcement { get; set; }
        [JsonPropertyName("forceUpdate")] public bool ForceUpdate { get; set; }
        [JsonPropertyName("minVersion")] public string? MinVersion { get; set; }
        [JsonPropertyName("challenge")] public string? Challenge { get; set; }
        [JsonPropertyName("subscriptionValid")] public bool SubscriptionValid { get; set; }
        [JsonPropertyName("subscriptionExpireTime")] public long? SubscriptionExpireTime { get; set; }
        [JsonPropertyName("allowedFeatures")] public List<string> AllowedFeatures { get; set; } = [];
    }
}

public sealed class PortableServerHeartbeatApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _apiVersion;
    private readonly PortableAuthTokenStore _tokenStore;
    private readonly string _deviceId;
    private readonly Func<long> _unixTimeFactory;

    public PortableServerHeartbeatApiClient(
        HttpClient httpClient,
        string baseUrl,
        string apiVersion,
        PortableAuthTokenStore tokenStore,
        string deviceId,
        Func<long>? unixTimeFactory = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _baseUrl = string.IsNullOrWhiteSpace(baseUrl)
            ? throw new ArgumentException("Base URL cannot be empty.", nameof(baseUrl))
            : baseUrl;
        _apiVersion = string.IsNullOrWhiteSpace(apiVersion)
            ? throw new ArgumentException("API version cannot be empty.", nameof(apiVersion))
            : apiVersion.Trim('/');
        _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        _deviceId = string.IsNullOrWhiteSpace(deviceId)
            ? throw new ArgumentException("Device ID cannot be empty.", nameof(deviceId))
            : deviceId;
        _unixTimeFactory = unixTimeFactory ?? (() => DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    public string? LastChallenge { get; private set; }

    public async Task<PortableHeartbeatResult> SendHeartbeatAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_tokenStore.AccessToken))
        {
            return new PortableHeartbeatResult
            {
                Success = false,
                ErrorCode = "NOT_LOGGED_IN",
                Message = "未登录"
            };
        }

        try
        {
            var payload = PortableServerHeartbeatService.CreateRequest(
                _deviceId,
                _unixTimeFactory(),
                LastChallenge,
                _tokenStore.SessionKey);
            var json = PortableServerHeartbeatService.SerializeRequest(payload);
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                CombineUrl(_baseUrl, $"/{_apiVersion}/auth/heartbeat"))
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-Device-Id", _deviceId);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var result = PortableServerHeartbeatService.ParseResponse(responseJson);
            if (result.Success)
            {
                LastChallenge = result.Challenge;
            }
            else if (result.Events.Any(e => e.Type == PortableServerHeartbeatEventType.ForceLogout))
            {
                _tokenStore.ClearTokens();
            }

            return result;
        }
        catch (Exception ex)
        {
            return new PortableHeartbeatResult
            {
                Success = false,
                ErrorCode = "NETWORK_ERROR",
                Message = ex.Message
            };
        }
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        return baseUrl.TrimEnd('/') + (path.StartsWith('/') ? path : "/" + path);
    }
}
