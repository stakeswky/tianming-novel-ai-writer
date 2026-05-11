using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableFeatureAuthorizationServiceTests
{
    [Fact]
    public async Task Local_mode_allows_features_without_server_checker()
    {
        var service = new PortableFeatureAuthorizationService(
            PortableFeatureAuthorizationOptions.LocalMode());

        var result = await service.CheckAsync("writing.ai");

        Assert.True(result.Authorized);
        Assert.Equal(PortableFeatureAuthorizationDecision.LocalAllowed, result.Decision);
    }

    [Fact]
    public async Task Server_mode_without_checker_denies_feature()
    {
        var service = new PortableFeatureAuthorizationService(
            PortableFeatureAuthorizationOptions.ServerMode(checker: null));

        var result = await service.CheckAsync("writing.ai");

        Assert.False(result.Authorized);
        Assert.Equal(PortableFeatureAuthorizationDecision.MissingChecker, result.Decision);
    }

    [Fact]
    public async Task Server_mode_uses_checker_result()
    {
        var service = new PortableFeatureAuthorizationService(
            PortableFeatureAuthorizationOptions.ServerMode(featureId =>
                Task.FromResult(new PortableFeatureAuthorizationResult(
                    featureId,
                    Authorized: featureId == "writing.ai",
                    PortableFeatureAuthorizationDecision.ServerAllowed))));

        Assert.True((await service.CheckAsync("writing.ai")).Authorized);
        Assert.False((await service.CheckAsync("admin.panel")).Authorized);
    }

    [Fact]
    public async Task Server_mode_can_use_original_feature_authorization_api()
    {
        var api = new RecordingFeatureAuthorizationApi(
            new PortableApiResponse<PortableFeatureAuthorizationStatus>
            {
                Success = true,
                Message = "allowed",
                Data = new PortableFeatureAuthorizationStatus
                {
                    Authorized = true,
                    ExpiresAt = 1778400300
                }
            });
        var service = new PortableFeatureAuthorizationService(
            PortableFeatureAuthorizationOptions.ServerMode(api));

        var result = await service.CheckAsync("writing.ai");

        Assert.True(result.Authorized);
        Assert.Equal("writing.ai", api.FeatureIds.Single());
        Assert.Equal(PortableFeatureAuthorizationDecision.ServerAllowed, result.Decision);
        Assert.Equal("allowed", result.Message);
    }

    [Fact]
    public async Task Server_mode_api_denies_failed_or_unauthorized_response()
    {
        var deniedApi = new RecordingFeatureAuthorizationApi(
            new PortableApiResponse<PortableFeatureAuthorizationStatus>
            {
                Success = true,
                Message = "subscription expired",
                Data = new PortableFeatureAuthorizationStatus { Authorized = false }
            });
        var failedApi = new RecordingFeatureAuthorizationApi(
            new PortableApiResponse<PortableFeatureAuthorizationStatus>
            {
                Success = false,
                Message = "server unavailable"
            });

        var denied = await new PortableFeatureAuthorizationService(
            PortableFeatureAuthorizationOptions.ServerMode(deniedApi)).CheckAsync("writing.ai");
        var failed = await new PortableFeatureAuthorizationService(
            PortableFeatureAuthorizationOptions.ServerMode(failedApi)).CheckAsync("writing.ai");

        Assert.False(denied.Authorized);
        Assert.Equal(PortableFeatureAuthorizationDecision.ServerDenied, denied.Decision);
        Assert.Equal("subscription expired", denied.Message);
        Assert.False(failed.Authorized);
        Assert.Equal(PortableFeatureAuthorizationDecision.ServerDenied, failed.Decision);
        Assert.Equal("server unavailable", failed.Message);
    }

    [Fact]
    public async Task Server_mode_api_maps_auth_invalid_to_login_required()
    {
        var api = new RecordingFeatureAuthorizationApi(
            new PortableApiResponse<PortableFeatureAuthorizationStatus>
            {
                Success = false,
                ErrorCode = PortableApiErrorCodes.AuthInvalid,
                Message = "未检测到登录状态"
            });
        var service = new PortableFeatureAuthorizationService(
            PortableFeatureAuthorizationOptions.ServerMode(api));

        var result = await service.CheckAsync("writing.ai");

        Assert.False(result.Authorized);
        Assert.Equal(PortableFeatureAuthorizationDecision.LoginRequired, result.Decision);
        Assert.Equal("未检测到登录状态", result.Message);
    }

    [Fact]
    public void Presentation_policy_maps_feature_denials_to_ui_neutral_actions()
    {
        var unsupported = PortableFeatureAuthorizationPresentationPolicy.CreateAction(
            PortableFeatureAuthorizationResult.Deny("writing.ai"));
        var loginRequired = PortableFeatureAuthorizationPresentationPolicy.CreateAction(
            new PortableFeatureAuthorizationResult(
                "writing.ai",
                Authorized: false,
                PortableFeatureAuthorizationDecision.LoginRequired,
                "未检测到登录状态"));
        var expired = PortableFeatureAuthorizationPresentationPolicy.CreateAction(
            new PortableFeatureAuthorizationResult(
                "writing.ai",
                Authorized: false,
                PortableFeatureAuthorizationDecision.SubscriptionExpired,
                "订阅已过期，请使用卡密续费后再使用此功能。"));

        Assert.Equal(PortableFeatureAuthorizationActionKind.Warning, unsupported.Kind);
        Assert.Equal("功能受限", unsupported.Title);
        Assert.Equal("您的订阅计划不支持此功能，请升级订阅", unsupported.Message);
        Assert.Equal(PortableFeatureAuthorizationActionKind.ReturnToLogin, loginRequired.Kind);
        Assert.Equal("登录已过期，请重新登录", loginRequired.Message);
        Assert.Equal(PortableFeatureAuthorizationActionKind.Warning, expired.Kind);
        Assert.Equal("订阅已过期", expired.Title);
        Assert.Equal("订阅已过期，请使用卡密续费后再使用此功能。", expired.Message);
    }

    [Fact]
    public async Task Server_mode_with_subscription_gate_denies_expired_subscription_before_checker()
    {
        var calls = 0;
        var now = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);
        var service = new PortableFeatureAuthorizationService(
            PortableFeatureAuthorizationOptions.ServerMode(
                featureId =>
                {
                    calls++;
                    return Task.FromResult(PortableFeatureAuthorizationResult.Allow(featureId));
                },
                subscriptionProvider: () => new PortableFeatureAuthorizationSubscriptionState(
                    IsActive: true,
                    EndTime: now.AddMinutes(-1),
                    Source: "card_key")),
            clock: () => now);

        var result = await service.CheckAsync("writing.ai");

        Assert.False(result.Authorized);
        Assert.Equal(PortableFeatureAuthorizationDecision.SubscriptionExpired, result.Decision);
        Assert.Equal("订阅已过期，请使用卡密续费后再使用此功能。", result.Message);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task Server_mode_with_subscription_gate_allows_active_card_key_subscription_to_checker()
    {
        var calls = 0;
        var now = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);
        var service = new PortableFeatureAuthorizationService(
            PortableFeatureAuthorizationOptions.ServerMode(
                featureId =>
                {
                    calls++;
                    return Task.FromResult(PortableFeatureAuthorizationResult.Allow(featureId));
                },
                subscriptionProvider: () => new PortableFeatureAuthorizationSubscriptionState(
                    IsActive: true,
                    EndTime: now.AddDays(3),
                    Source: "card_key")),
            clock: () => now);

        var result = await service.CheckAsync("writing.ai");

        Assert.True(result.Authorized);
        Assert.Equal(PortableFeatureAuthorizationDecision.ServerAllowed, result.Decision);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Checker_exceptions_are_denied()
    {
        var service = new PortableFeatureAuthorizationService(
            PortableFeatureAuthorizationOptions.ServerMode(_ => throw new InvalidOperationException("boom")));

        var result = await service.CheckAsync("writing.ai");

        Assert.False(result.Authorized);
        Assert.Equal(PortableFeatureAuthorizationDecision.CheckerError, result.Decision);
        Assert.Equal("boom", result.Message);
    }

    [Fact]
    public async Task Server_results_are_cached_until_ttl_expires()
    {
        var now = new DateTime(2026, 5, 10, 9, 0, 0);
        var calls = 0;
        var service = new PortableFeatureAuthorizationService(
            PortableFeatureAuthorizationOptions.ServerMode(featureId =>
            {
                calls++;
                return Task.FromResult(new PortableFeatureAuthorizationResult(
                    featureId,
                    Authorized: calls == 1,
                    PortableFeatureAuthorizationDecision.ServerAllowed));
            }, cacheTtl: TimeSpan.FromMinutes(5)),
            clock: () => now);

        var first = await service.CheckAsync("writing.ai");
        now = now.AddMinutes(4);
        var cached = await service.CheckAsync("writing.ai");
        now = now.AddMinutes(2);
        var refreshed = await service.CheckAsync("writing.ai");

        Assert.True(first.Authorized);
        Assert.True(cached.Authorized);
        Assert.False(refreshed.Authorized);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Blank_feature_id_is_denied_without_calling_checker()
    {
        var calls = 0;
        var service = new PortableFeatureAuthorizationService(
            PortableFeatureAuthorizationOptions.ServerMode(_ =>
            {
                calls++;
                return Task.FromResult(PortableFeatureAuthorizationResult.Allow("x"));
            }));

        var result = await service.CheckAsync(" ");

        Assert.False(result.Authorized);
        Assert.Equal(PortableFeatureAuthorizationDecision.InvalidFeatureId, result.Decision);
        Assert.Equal(0, calls);
    }

    private sealed class RecordingFeatureAuthorizationApi : IPortableFeatureAuthorizationApi
    {
        private readonly PortableApiResponse<PortableFeatureAuthorizationStatus> _response;

        public RecordingFeatureAuthorizationApi(PortableApiResponse<PortableFeatureAuthorizationStatus> response)
        {
            _response = response;
        }

        public List<string> FeatureIds { get; } = new();

        public Task<PortableApiResponse<PortableFeatureAuthorizationStatus>> CheckFeatureAuthorizationAsync(
            string featureId,
            CancellationToken cancellationToken = default)
        {
            FeatureIds.Add(featureId);
            return Task.FromResult(_response);
        }
    }
}
