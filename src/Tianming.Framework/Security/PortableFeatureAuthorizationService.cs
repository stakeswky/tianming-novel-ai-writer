using System.Text.Json.Serialization;

namespace TM.Framework.Security;

public enum PortableFeatureAuthorizationDecision
{
    LocalAllowed,
    ServerAllowed,
    ServerDenied,
    MissingChecker,
    CheckerError,
    InvalidFeatureId,
    LoginRequired,
    SubscriptionRequired,
    SubscriptionExpired
}

public sealed record PortableFeatureAuthorizationResult(
    string FeatureId,
    bool Authorized,
    PortableFeatureAuthorizationDecision Decision,
    string? Message = null)
{
    public static PortableFeatureAuthorizationResult Allow(string featureId)
    {
        return new PortableFeatureAuthorizationResult(
            featureId,
            Authorized: true,
            PortableFeatureAuthorizationDecision.ServerAllowed);
    }

    public static PortableFeatureAuthorizationResult Deny(string featureId, string? message = null)
    {
        return new PortableFeatureAuthorizationResult(
            featureId,
            Authorized: false,
            PortableFeatureAuthorizationDecision.ServerDenied,
            message);
    }
}

public sealed class PortableFeatureAuthorizationStatus
{
    [JsonPropertyName("authorized")] public bool Authorized { get; set; }
    [JsonPropertyName("expiresAt")] public long? ExpiresAt { get; set; }
}

public enum PortableFeatureAuthorizationActionKind
{
    None,
    Warning,
    ReturnToLogin
}

public sealed record PortableFeatureAuthorizationAction(
    PortableFeatureAuthorizationActionKind Kind,
    string Title = "",
    string Message = "");

public static class PortableFeatureAuthorizationPresentationPolicy
{
    public static PortableFeatureAuthorizationAction CreateAction(
        PortableFeatureAuthorizationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Authorized)
        {
            return new PortableFeatureAuthorizationAction(PortableFeatureAuthorizationActionKind.None);
        }

        return result.Decision switch
        {
            PortableFeatureAuthorizationDecision.LoginRequired => new PortableFeatureAuthorizationAction(
                PortableFeatureAuthorizationActionKind.ReturnToLogin,
                "登录状态失效",
                "登录已过期，请重新登录"),
            PortableFeatureAuthorizationDecision.SubscriptionRequired => Warning(
                "账号未激活",
                result.Message ?? "账号未激活，请先使用卡密激活后再使用此功能。"),
            PortableFeatureAuthorizationDecision.SubscriptionExpired => Warning(
                "订阅已过期",
                result.Message ?? "订阅已过期，请使用卡密续费后再使用此功能。"),
            _ => Warning(
                "功能受限",
                result.Message ?? "您的订阅计划不支持此功能，请升级订阅")
        };
    }

    private static PortableFeatureAuthorizationAction Warning(string title, string message)
    {
        return new PortableFeatureAuthorizationAction(
            PortableFeatureAuthorizationActionKind.Warning,
            title,
            message);
    }
}

public sealed record PortableFeatureAuthorizationSubscriptionState(
    bool IsActive,
    DateTime? EndTime,
    string? Source = null)
{
    public static PortableFeatureAuthorizationSubscriptionState? FromSnapshot(
        PortableSubscriptionSnapshot? snapshot)
    {
        return snapshot == null
            ? null
            : new PortableFeatureAuthorizationSubscriptionState(
                snapshot.IsActive,
                snapshot.EndTime,
                snapshot.Source);
    }
}

public interface IPortableFeatureAuthorizationApi
{
    Task<PortableApiResponse<PortableFeatureAuthorizationStatus>> CheckFeatureAuthorizationAsync(
        string featureId,
        CancellationToken cancellationToken = default);
}

public sealed class PortableFeatureAuthorizationOptions
{
    public bool ServerModeEnabled { get; init; }
    public Func<string, Task<PortableFeatureAuthorizationResult>>? Checker { get; init; }
    public Func<PortableFeatureAuthorizationSubscriptionState?>? SubscriptionProvider { get; init; }
    public TimeSpan CacheTtl { get; init; } = TimeSpan.FromMinutes(5);

    public static PortableFeatureAuthorizationOptions LocalMode()
    {
        return new PortableFeatureAuthorizationOptions
        {
            ServerModeEnabled = false
        };
    }

    public static PortableFeatureAuthorizationOptions ServerMode(
        Func<string, Task<PortableFeatureAuthorizationResult>>? checker,
        TimeSpan? cacheTtl = null,
        Func<PortableFeatureAuthorizationSubscriptionState?>? subscriptionProvider = null)
    {
        return new PortableFeatureAuthorizationOptions
        {
            ServerModeEnabled = true,
            Checker = checker,
            CacheTtl = cacheTtl ?? TimeSpan.FromMinutes(5),
            SubscriptionProvider = subscriptionProvider
        };
    }

    public static PortableFeatureAuthorizationOptions ServerMode(
        IPortableFeatureAuthorizationApi api,
        TimeSpan? cacheTtl = null,
        Func<PortableFeatureAuthorizationSubscriptionState?>? subscriptionProvider = null)
    {
        ArgumentNullException.ThrowIfNull(api);

        return ServerMode(async featureId =>
        {
            var response = await api.CheckFeatureAuthorizationAsync(featureId).ConfigureAwait(false);
            var authorized = response.Success && response.Data?.Authorized == true;
            return new PortableFeatureAuthorizationResult(
                featureId,
                authorized,
                MapApiDecision(authorized, response.ErrorCode),
                response.Message ?? response.ErrorCode);
        }, cacheTtl, subscriptionProvider);
    }

    private static PortableFeatureAuthorizationDecision MapApiDecision(
        bool authorized,
        string? errorCode)
    {
        if (authorized)
        {
            return PortableFeatureAuthorizationDecision.ServerAllowed;
        }

        return errorCode == PortableApiErrorCodes.AuthInvalid
            ? PortableFeatureAuthorizationDecision.LoginRequired
            : PortableFeatureAuthorizationDecision.ServerDenied;
    }
}

public sealed class PortableFeatureAuthorizationService
{
    private readonly PortableFeatureAuthorizationOptions _options;
    private readonly Func<DateTime> _clock;
    private readonly Dictionary<string, CachedResult> _cache = new(StringComparer.Ordinal);

    public PortableFeatureAuthorizationService(
        PortableFeatureAuthorizationOptions options,
        Func<DateTime>? clock = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? (() => DateTime.UtcNow);
    }

    public async Task<PortableFeatureAuthorizationResult> CheckAsync(string featureId)
    {
        if (string.IsNullOrWhiteSpace(featureId))
        {
            return new PortableFeatureAuthorizationResult(
                featureId,
                Authorized: false,
                PortableFeatureAuthorizationDecision.InvalidFeatureId);
        }

        if (!_options.ServerModeEnabled)
        {
            return new PortableFeatureAuthorizationResult(
                featureId,
                Authorized: true,
                PortableFeatureAuthorizationDecision.LocalAllowed);
        }

        if (_options.Checker == null)
        {
            return new PortableFeatureAuthorizationResult(
                featureId,
                Authorized: false,
                PortableFeatureAuthorizationDecision.MissingChecker);
        }

        var now = _clock();
        var subscriptionDenied = CheckSubscriptionGate(featureId, now);
        if (subscriptionDenied != null)
        {
            return subscriptionDenied;
        }

        if (_cache.TryGetValue(featureId, out var cached) && now < cached.ExpiresAt)
        {
            return cached.Result;
        }

        try
        {
            var result = await _options.Checker(featureId).ConfigureAwait(false);
            var normalized = NormalizeResult(featureId, result);
            _cache[featureId] = new CachedResult(
                normalized,
                now.Add(_options.CacheTtl < TimeSpan.Zero ? TimeSpan.Zero : _options.CacheTtl));
            return normalized;
        }
        catch (Exception ex)
        {
            return new PortableFeatureAuthorizationResult(
                featureId,
                Authorized: false,
                PortableFeatureAuthorizationDecision.CheckerError,
                ex.Message);
        }
    }

    public void ClearCache()
    {
        _cache.Clear();
    }

    private PortableFeatureAuthorizationResult? CheckSubscriptionGate(string featureId, DateTime now)
    {
        if (_options.SubscriptionProvider == null)
        {
            return null;
        }

        var subscription = _options.SubscriptionProvider();
        if (subscription == null || !subscription.IsActive)
        {
            return new PortableFeatureAuthorizationResult(
                featureId,
                Authorized: false,
                PortableFeatureAuthorizationDecision.SubscriptionRequired,
                "账号未激活，请先使用卡密激活后再使用此功能。");
        }

        if (subscription.EndTime.HasValue && subscription.EndTime.Value <= now)
        {
            return new PortableFeatureAuthorizationResult(
                featureId,
                Authorized: false,
                PortableFeatureAuthorizationDecision.SubscriptionExpired,
                "订阅已过期，请使用卡密续费后再使用此功能。");
        }

        return null;
    }

    private static PortableFeatureAuthorizationResult NormalizeResult(
        string requestedFeatureId,
        PortableFeatureAuthorizationResult result)
    {
        if (!result.Authorized)
        {
            return result.Decision is PortableFeatureAuthorizationDecision.ServerAllowed
                or PortableFeatureAuthorizationDecision.LocalAllowed
                ? result with { Decision = PortableFeatureAuthorizationDecision.ServerDenied }
                : result;
        }

        return result.FeatureId == requestedFeatureId
            ? result
            : result with { FeatureId = requestedFeatureId };
    }

    private sealed record CachedResult(
        PortableFeatureAuthorizationResult Result,
        DateTime ExpiresAt);
}
