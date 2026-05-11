using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using TM.Framework.Appearance;
using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableServerHeartbeatServiceTests
{
    [Fact]
    public void CreateRequest_builds_original_heartbeat_payload_without_challenge()
    {
        var request = PortableServerHeartbeatService.CreateRequest(
            deviceId: "device-1",
            timestamp: 1778400000,
            lastChallenge: null,
            sessionKey: "session-key");

        Assert.Equal("device-1", request.DeviceId);
        Assert.Equal(1778400000, request.Timestamp);
        Assert.Null(request.ChallengeResponse);
    }

    [Fact]
    public void CreateRequest_signs_previous_challenge_with_session_key()
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("session-key"));
        var expected = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes("challenge-1")));

        var request = PortableServerHeartbeatService.CreateRequest(
            deviceId: "device-1",
            timestamp: 1778400000,
            lastChallenge: "challenge-1",
            sessionKey: "session-key");

        Assert.Equal(expected, request.ChallengeResponse);
    }

    [Fact]
    public void ParseResponse_maps_success_announcement_force_update_and_subscription_data()
    {
        var json = """
        {
          "success": true,
          "data": {
            "serverTime": 1778400000,
            "announcement": "系统维护公告",
            "forceUpdate": true,
            "minVersion": "1.4.7",
            "challenge": "next-challenge",
            "subscriptionValid": true,
            "subscriptionExpireTime": 1778486400,
            "allowedFeatures": ["writing.ai", "export.package"]
          }
        }
        """;

        var result = PortableServerHeartbeatService.ParseResponse(json);

        Assert.True(result.Success);
        Assert.Equal("系统维护公告", result.Announcement);
        Assert.True(result.ForceUpdate);
        Assert.Equal("1.4.7", result.MinVersion);
        Assert.Equal("next-challenge", result.Challenge);
        Assert.True(result.SubscriptionValid);
        Assert.Equal(1778486400, result.SubscriptionExpireTime);
        Assert.Equal(["writing.ai", "export.package"], result.AllowedFeatures);
        Assert.Contains(result.Events, e => e.Type == PortableServerHeartbeatEventType.Announcement && e.Message == "系统维护公告");
        Assert.Contains(result.Events, e => e.Type == PortableServerHeartbeatEventType.ForceUpdate && e.Message == "1.4.7");
    }

    [Fact]
    public void ParseResponse_maps_invalid_token_to_force_logout_event()
    {
        var json = """
        {
          "success": false,
          "message": "Token无效",
          "errorCode": "INVALID_TOKEN"
        }
        """;

        var result = PortableServerHeartbeatService.ParseResponse(json);

        Assert.False(result.Success);
        Assert.Equal("INVALID_TOKEN", result.ErrorCode);
        Assert.Contains(result.Events, e => e.Type == PortableServerHeartbeatEventType.ForceLogout);
    }

    [Fact]
    public void PresentationPolicy_maps_announcement_force_update_and_force_logout_actions()
    {
        var state = new PortableServerHeartbeatPresentationState();
        var result = new PortableHeartbeatResult
        {
            Success = false,
            Events =
            [
                new PortableServerHeartbeatEvent(PortableServerHeartbeatEventType.Announcement, "系统维护公告"),
                new PortableServerHeartbeatEvent(PortableServerHeartbeatEventType.ForceUpdate, "1.4.7"),
                new PortableServerHeartbeatEvent(PortableServerHeartbeatEventType.ForceLogout, "您的账号已在其他设备登录")
            ]
        };

        var actions = PortableServerHeartbeatPresentationPolicy.CreateActions(result, state);
        var repeated = PortableServerHeartbeatPresentationPolicy.CreateActions(result, state);

        Assert.Contains(actions, action =>
            action.Kind == PortableServerHeartbeatPresentationActionKind.Info
            && action.Title == "系统公告"
            && action.Message == "系统维护公告");
        Assert.Contains(actions, action =>
            action.Kind == PortableServerHeartbeatPresentationActionKind.ForceUpdate
            && action.Title == "强制更新"
            && action.Message == "当前版本过低，请升级到 1.4.7 或更高版本。");
        Assert.Contains(actions, action =>
            action.Kind == PortableServerHeartbeatPresentationActionKind.ReturnToLogin
            && action.Message == "您的账号已在其他设备登录");
        Assert.DoesNotContain(repeated, action =>
            action.Kind == PortableServerHeartbeatPresentationActionKind.Info
            && action.Message == "系统维护公告");
    }

    [Theory]
    [InlineData("INVALID_TOKEN", "登录已过期，请重新登录")]
    [InlineData("AUTH_DEVICE_KICKED", "您的账号已在其他设备登录")]
    [InlineData("USER_INVALID", "账号状态异常")]
    public void ParseResponse_uses_original_force_logout_fallback_messages(string errorCode, string expectedMessage)
    {
        var json = $$"""
        {
          "success": false,
          "errorCode": "{{errorCode}}"
        }
        """;

        var result = PortableServerHeartbeatService.ParseResponse(json);

        var forceLogout = Assert.Single(result.Events, e => e.Type == PortableServerHeartbeatEventType.ForceLogout);
        Assert.Equal(expectedMessage, forceLogout.Message);
    }

    [Fact]
    public void ParseResponse_recovers_from_bad_json_as_network_error()
    {
        var result = PortableServerHeartbeatService.ParseResponse("{ bad json");

        Assert.False(result.Success);
        Assert.Equal("NETWORK_ERROR", result.ErrorCode);
        Assert.Contains("JSON", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SerializeRequest_uses_original_json_field_names()
    {
        var request = new PortableHeartbeatRequest
        {
            DeviceId = "device-1",
            Timestamp = 1778400000,
            ChallengeResponse = "signed"
        };

        var json = PortableServerHeartbeatService.SerializeRequest(request);
        using var document = JsonDocument.Parse(json);

        Assert.Equal("device-1", document.RootElement.GetProperty("deviceId").GetString());
        Assert.Equal(1778400000, document.RootElement.GetProperty("timestamp").GetInt64());
        Assert.Equal("signed", document.RootElement.GetProperty("challengeResponse").GetString());
    }

    [Fact]
    public async Task ApiClient_posts_original_heartbeat_request_with_device_and_bearer_headers()
    {
        using var temp = new TempDirectory();
        var tokenStore = CreateAuthenticatedStore(temp);
        var handler = new SequenceHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""
            {
              "success": true,
              "data": {
                "serverTime": 1778400001,
                "challenge": "next-challenge",
                "allowedFeatures": ["writing.ai"]
              }
            }
            """)
        });
        using var httpClient = new HttpClient(handler);
        var api = new PortableServerHeartbeatApiClient(
            httpClient,
            "https://api.example.com",
            apiVersion: "v1",
            tokenStore,
            deviceId: "device-1",
            unixTimeFactory: () => 1778400000);

        var result = await api.SendHeartbeatAsync();

        Assert.True(result.Success);
        Assert.Equal("next-challenge", api.LastChallenge);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.example.com/v1/auth/heartbeat", request.RequestUri!.ToString());
        Assert.Equal("device-1", request.Headers.GetValues("X-Device-Id").Single());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("access-1", request.Headers.Authorization.Parameter);
        using var body = JsonDocument.Parse(handler.Bodies.Single());
        Assert.Equal("device-1", body.RootElement.GetProperty("deviceId").GetString());
        Assert.Equal(1778400000, body.RootElement.GetProperty("timestamp").GetInt64());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("challengeResponse").ValueKind);
    }

    [Fact]
    public async Task ApiClient_signs_previous_challenge_with_session_key()
    {
        using var temp = new TempDirectory();
        var tokenStore = CreateAuthenticatedStore(temp);
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""{"success":true,"data":{"challenge":"challenge-1"}}""")
            },
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent("""{"success":true,"data":{"challenge":"challenge-2"}}""")
            });
        using var httpClient = new HttpClient(handler);
        var api = new PortableServerHeartbeatApiClient(
            httpClient,
            "https://api.example.com/",
            apiVersion: "v1",
            tokenStore,
            deviceId: "device-1",
            unixTimeFactory: () => 1778400000);

        await api.SendHeartbeatAsync();
        await api.SendHeartbeatAsync();

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("session-key"));
        var expected = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes("challenge-1")));
        using var secondBody = JsonDocument.Parse(handler.Bodies[1]);
        Assert.Equal(expected, secondBody.RootElement.GetProperty("challengeResponse").GetString());
        Assert.Equal("challenge-2", api.LastChallenge);
    }

    [Fact]
    public async Task ApiClient_denies_without_login_and_clears_tokens_on_force_logout_response()
    {
        using var temp = new TempDirectory();
        var loggedOutStore = CreateStore(temp);
        var noTokenHandler = new SequenceHandler();
        using var noTokenHttpClient = new HttpClient(noTokenHandler);
        var loggedOutApi = new PortableServerHeartbeatApiClient(
            noTokenHttpClient,
            "https://api.example.com",
            "v1",
            loggedOutStore,
            "device-1");

        var loggedOutResult = await loggedOutApi.SendHeartbeatAsync();

        Assert.False(loggedOutResult.Success);
        Assert.Equal("NOT_LOGGED_IN", loggedOutResult.ErrorCode);
        Assert.Empty(noTokenHandler.Requests);

        var tokenStore = CreateAuthenticatedStore(temp);
        var kickedHandler = new SequenceHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""
            {
              "success": false,
              "message": "您的账号已在其他设备登录",
              "errorCode": "AUTH_DEVICE_KICKED"
            }
            """)
        });
        using var kickedHttpClient = new HttpClient(kickedHandler);
        var kickedApi = new PortableServerHeartbeatApiClient(
            kickedHttpClient,
            "https://api.example.com",
            "v1",
            tokenStore,
            "device-1");

        var kicked = await kickedApi.SendHeartbeatAsync();

        Assert.False(kicked.Success);
        Assert.Equal("AUTH_DEVICE_KICKED", kicked.ErrorCode);
        Assert.Null(tokenStore.AccessToken);
        Assert.Contains(kicked.Events, e => e.Type == PortableServerHeartbeatEventType.ForceLogout);
    }

    [Fact]
    public void HeartbeatPolicy_success_resets_fail_count_and_subscription_expiry_requests_return_to_login()
    {
        var state = new PortableHeartbeatLoopState { ConsecutiveFailures = 4 };
        var result = new PortableHeartbeatResult
        {
            Success = true,
            SubscriptionValid = false
        };

        var action = PortableHeartbeatLoopPolicy.ApplyHeartbeatResult(
            state,
            result,
            nowUtc: new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(0, state.ConsecutiveFailures);
        Assert.Equal(PortableHeartbeatLoopActionKind.ReturnToLogin, action.Kind);
        Assert.Equal("订阅已到期，请续费后重新登录", action.Message);
    }

    [Fact]
    public void HeartbeatPolicy_warns_once_when_subscription_expires_within_24_hours()
    {
        var state = new PortableHeartbeatLoopState();
        var now = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);
        var result = new PortableHeartbeatResult
        {
            Success = true,
            SubscriptionValid = true,
            SubscriptionExpireTime = new DateTimeOffset(now.AddHours(3)).ToUnixTimeSeconds()
        };

        var first = PortableHeartbeatLoopPolicy.ApplyHeartbeatResult(state, result, now);
        var second = PortableHeartbeatLoopPolicy.ApplyHeartbeatResult(state, result, now.AddMinutes(1));

        Assert.Equal(PortableHeartbeatLoopActionKind.Warning, first.Kind);
        Assert.Equal("订阅即将到期", first.Title);
        Assert.Equal("您的订阅将在 3 小时后到期，请及时续费。", first.Message);
        Assert.Equal(PortableHeartbeatLoopActionKind.None, second.Kind);
        Assert.True(state.ExpireWarningShown);
    }

    [Fact]
    public void HeartbeatPolicy_counts_failures_and_returns_to_login_at_original_threshold()
    {
        var state = new PortableHeartbeatLoopState { ConsecutiveFailures = 9 };
        var result = new PortableHeartbeatResult { Success = false };

        var action = PortableHeartbeatLoopPolicy.ApplyHeartbeatResult(
            state,
            result,
            nowUtc: new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(10, state.ConsecutiveFailures);
        Assert.Equal(PortableHeartbeatLoopActionKind.ReturnToLogin, action.Kind);
        Assert.Equal("网络连接丢失，请检查网络后重新登录", action.Message);
    }

    [Fact]
    public void HeartbeatPolicy_low_failure_count_reports_unstable_network_warning()
    {
        var state = new PortableHeartbeatLoopState { ConsecutiveFailures = 2 };
        var result = new PortableHeartbeatResult { Success = false };

        var action = PortableHeartbeatLoopPolicy.ApplyHeartbeatResult(
            state,
            result,
            nowUtc: new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(3, state.ConsecutiveFailures);
        Assert.Equal(PortableHeartbeatLoopActionKind.Warning, action.Kind);
        Assert.Equal("网络不稳定", action.Title);
        Assert.Equal("心跳连接失败（3/10），请检查网络", action.Message);
    }

    [Fact]
    public void HeartbeatPolicy_precheck_requests_return_to_login_when_not_logged_in()
    {
        var loggedOut = PortableHeartbeatLoopPolicy.PrecheckLoginState(isLoggedIn: false);
        var loggedIn = PortableHeartbeatLoopPolicy.PrecheckLoginState(isLoggedIn: true);

        Assert.Equal(PortableHeartbeatLoopActionKind.ReturnToLogin, loggedOut.Kind);
        Assert.Equal("登录已过期，请重新登录", loggedOut.Message);
        Assert.Equal(PortableHeartbeatLoopActionKind.None, loggedIn.Kind);
    }

    [Fact]
    public void ReturnToLoginCoordinator_only_dispatches_first_request_until_reset()
    {
        var coordinator = new PortableReturnToLoginCoordinator();
        var messages = new List<string>();

        Assert.True(coordinator.TryRequest("登录已过期，请重新登录", messages.Add));
        Assert.False(coordinator.TryRequest("网络连接丢失，请检查网络后重新登录", messages.Add));
        coordinator.Reset();
        Assert.True(coordinator.TryRequest("订阅已到期，请续费后重新登录", messages.Add));

        Assert.Equal(
        [
            "登录已过期，请重新登录",
            "订阅已到期，请续费后重新登录"
        ], messages);
        Assert.True(coordinator.IsReturningToLogin);
    }

    [Fact]
    public async Task ReturnToLoginNavigationController_runs_original_relogin_sequence_after_success()
    {
        var operations = new List<string>();
        var controller = new PortableReturnToLoginNavigationController(
            stopSessionServicesAsync: _ =>
            {
                operations.Add("stop-session");
                return Task.CompletedTask;
            },
            clearAuthStateAsync: _ =>
            {
                operations.Add("clear-auth");
                return Task.CompletedTask;
            },
            flushBeforeLoginAsync:
            [
                _ =>
                {
                    operations.Add("flush-chapter");
                    return Task.CompletedTask;
                },
                _ =>
                {
                    operations.Add("flush-guide");
                    return Task.CompletedTask;
                }
            ],
            showLoginAsync: (message, _) =>
            {
                operations.Add($"show-login:{message}");
                return Task.FromResult(PortableReturnToLoginResult.LoginSucceeded("jimmy"));
            },
            restartSessionServicesAsync: (username, _) =>
            {
                operations.Add($"restart:{username}");
                return Task.CompletedTask;
            },
            shutdownAsync: _ =>
            {
                operations.Add("shutdown");
                return Task.CompletedTask;
            });

        var result = await controller.ReturnToLoginAsync("登录已过期，请重新登录");

        Assert.Equal(PortableReturnToLoginOutcome.LoginSucceeded, result.Outcome);
        Assert.Equal("jimmy", result.LoggedInUsername);
        Assert.Equal(
        [
            "stop-session",
            "clear-auth",
            "flush-chapter",
            "flush-guide",
            "show-login:登录已过期，请重新登录",
            "restart:jimmy"
        ], operations);
    }

    [Fact]
    public async Task ReturnToLoginNavigationController_shutdowns_when_relogin_is_cancelled()
    {
        var operations = new List<string>();
        var controller = new PortableReturnToLoginNavigationController(
            stopSessionServicesAsync: _ =>
            {
                operations.Add("stop-session");
                return Task.CompletedTask;
            },
            clearAuthStateAsync: _ =>
            {
                operations.Add("clear-auth");
                return Task.CompletedTask;
            },
            flushBeforeLoginAsync: [],
            showLoginAsync: (_, _) =>
            {
                operations.Add("show-login");
                return Task.FromResult(PortableReturnToLoginResult.LoginCancelled());
            },
            restartSessionServicesAsync: (_, _) =>
            {
                operations.Add("restart");
                return Task.CompletedTask;
            },
            shutdownAsync: _ =>
            {
                operations.Add("shutdown");
                return Task.CompletedTask;
            });

        var result = await controller.ReturnToLoginAsync("网络连接丢失，请检查网络后重新登录");

        Assert.Equal(PortableReturnToLoginOutcome.LoginCancelled, result.Outcome);
        Assert.True(result.ShutdownRequested);
        Assert.Equal(
        [
            "stop-session",
            "clear-auth",
            "show-login",
            "shutdown"
        ], operations);
    }

    [Fact]
    public async Task HeartbeatRunner_tick_prechecks_login_sends_heartbeat_and_dispatches_actions()
    {
        var sent = 0;
        var actions = new List<PortableServerHeartbeatPresentationAction>();
        var returnMessages = new List<string>();
        var runner = new PortableServerHeartbeatRunner(
            isLoggedIn: () => true,
            sendHeartbeatAsync: _ =>
            {
                sent++;
                return Task.FromResult(new PortableHeartbeatResult
                {
                    Success = true,
                    SubscriptionValid = true,
                    Events =
                    [
                        new PortableServerHeartbeatEvent(
                            PortableServerHeartbeatEventType.Announcement,
                            "系统维护公告")
                    ]
                });
            },
            dispatchAction: actions.Add,
            dispatchReturnToLogin: returnMessages.Add,
            nowUtc: () => new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc));

        var first = await runner.TickAsync();
        var second = await runner.TickAsync();

        Assert.Equal(PortableHeartbeatLoopActionKind.None, first.LoopAction.Kind);
        Assert.Equal(PortableHeartbeatLoopActionKind.None, second.LoopAction.Kind);
        Assert.Equal(2, sent);
        Assert.Empty(returnMessages);
        Assert.Single(actions);
        Assert.Equal(PortableServerHeartbeatPresentationActionKind.Info, actions[0].Kind);
        Assert.Equal("系统维护公告", actions[0].Message);
    }

    [Fact]
    public async Task HeartbeatRunner_tick_returns_to_login_without_network_when_not_logged_in()
    {
        var sent = 0;
        var returnMessages = new List<string>();
        var runner = new PortableServerHeartbeatRunner(
            isLoggedIn: () => false,
            sendHeartbeatAsync: _ =>
            {
                sent++;
                return Task.FromResult(new PortableHeartbeatResult { Success = true });
            },
            dispatchAction: _ => { },
            dispatchReturnToLogin: returnMessages.Add,
            nowUtc: () => new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc));

        var result = await runner.TickAsync();

        Assert.Equal(PortableHeartbeatLoopActionKind.ReturnToLogin, result.LoopAction.Kind);
        Assert.Equal("登录已过期，请重新登录", result.LoopAction.Message);
        Assert.Equal(0, sent);
        Assert.Equal(["登录已过期，请重新登录"], returnMessages);
    }

    [Fact]
    public async Task HeartbeatRunner_tick_converts_network_exception_to_failed_heartbeat_action()
    {
        var actions = new List<PortableServerHeartbeatPresentationAction>();
        var runner = new PortableServerHeartbeatRunner(
            isLoggedIn: () => true,
            sendHeartbeatAsync: _ => throw new HttpRequestException("connection reset"),
            dispatchAction: actions.Add,
            dispatchReturnToLogin: _ => { },
            nowUtc: () => new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc));

        var result = await runner.TickAsync();

        Assert.NotNull(result.HeartbeatResult);
        Assert.False(result.HeartbeatResult.Success);
        Assert.Equal("NETWORK_ERROR", result.HeartbeatResult.ErrorCode);
        Assert.Equal("connection reset", result.HeartbeatResult.Message);
        Assert.Equal(PortableHeartbeatLoopActionKind.Warning, result.LoopAction.Kind);
        Assert.Equal("网络不稳定", result.LoopAction.Title);
        Assert.Equal("心跳连接失败（1/10），请检查网络", result.LoopAction.Message);
        Assert.Empty(actions);
    }

    [Fact]
    public async Task HeartbeatRunner_tick_retries_transient_exception_before_reporting_success()
    {
        var attempts = 0;
        var actions = new List<PortableServerHeartbeatPresentationAction>();
        var returnMessages = new List<string>();
        var runner = new PortableServerHeartbeatRunner(
            isLoggedIn: () => true,
            sendHeartbeatAsync: _ =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new HttpRequestException("temporary outage");
                }

                return Task.FromResult(new PortableHeartbeatResult
                {
                    Success = true,
                    SubscriptionValid = true
                });
            },
            dispatchAction: actions.Add,
            dispatchReturnToLogin: returnMessages.Add,
            nowUtc: () => new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc),
            maxSendAttempts: 2);

        var result = await runner.TickAsync();

        Assert.Equal(2, attempts);
        Assert.NotNull(result.HeartbeatResult);
        Assert.True(result.HeartbeatResult.Success);
        Assert.Equal(PortableHeartbeatLoopActionKind.None, result.LoopAction.Kind);
        Assert.Empty(actions);
        Assert.Empty(returnMessages);
    }

    [Fact]
    public async Task HeartbeatPeriodicService_start_runs_initial_tick_and_timer_ticks()
    {
        var ticks = new ManualTickSource();
        var heartbeatTicks = 0;
        var runner = new PortableServerHeartbeatRunner(
            isLoggedIn: () => true,
            sendHeartbeatAsync: _ =>
            {
                Interlocked.Increment(ref heartbeatTicks);
                return Task.FromResult(new PortableHeartbeatResult
                {
                    Success = true,
                    SubscriptionValid = true
                });
            });
        await using var service = new PortableServerHeartbeatPeriodicService(
            runner,
            tickSource: ticks);

        await service.StartAsync();
        await ticks.TickAsync();
        await ticks.TickAsync();
        await WaitForCountAsync(() => Volatile.Read(ref heartbeatTicks), 3);

        Assert.True(service.IsRunning);
        Assert.Equal(3, heartbeatTicks);
    }

    [Fact]
    public async Task HeartbeatPeriodicService_start_is_idempotent()
    {
        var ticks = new ManualTickSource();
        var heartbeatTicks = 0;
        var runner = new PortableServerHeartbeatRunner(
            isLoggedIn: () => true,
            sendHeartbeatAsync: _ =>
            {
                Interlocked.Increment(ref heartbeatTicks);
                return Task.FromResult(new PortableHeartbeatResult
                {
                    Success = true,
                    SubscriptionValid = true
                });
            });
        await using var service = new PortableServerHeartbeatPeriodicService(
            runner,
            tickSource: ticks);

        await service.StartAsync();
        await service.StartAsync();
        await ticks.TickAsync();
        await WaitForCountAsync(() => Volatile.Read(ref heartbeatTicks), 2);

        Assert.Equal(2, heartbeatTicks);
    }

    [Fact]
    public async Task HeartbeatPeriodicService_stop_cancels_loop_and_ignores_later_ticks()
    {
        var ticks = new ManualTickSource();
        var heartbeatTicks = 0;
        var runner = new PortableServerHeartbeatRunner(
            isLoggedIn: () => true,
            sendHeartbeatAsync: _ =>
            {
                Interlocked.Increment(ref heartbeatTicks);
                return Task.FromResult(new PortableHeartbeatResult
                {
                    Success = true,
                    SubscriptionValid = true
                });
            });
        await using var service = new PortableServerHeartbeatPeriodicService(
            runner,
            tickSource: ticks);

        await service.StartAsync();
        await service.StopAsync();
        await ticks.TickAsync();
        await Task.Delay(25);

        Assert.False(service.IsRunning);
        Assert.Equal(1, heartbeatTicks);
    }

    private static PortableAuthTokenStore CreateStore(TempDirectory temp)
    {
        return new PortableAuthTokenStore(
            Path.Combine(temp.Path, "auth_token.dat"),
            new Base64JsonTokenProtector(),
            () => DateTimeOffset.FromUnixTimeSeconds(1778400000).UtcDateTime,
            () => "client-1",
            () => "nonce-1");
    }

    private static PortableAuthTokenStore CreateAuthenticatedStore(TempDirectory temp)
    {
        var tokenStore = CreateStore(temp);
        tokenStore.SaveTokens(new PortableLoginResult
        {
            AccessToken = "access-1",
            RefreshToken = "refresh-1",
            SessionKey = "session-key",
            ExpiresAt = new DateTime(2026, 5, 10, 13, 0, 0, DateTimeKind.Utc),
            User = new PortableUserInfo { UserId = "u1", Username = "jimmy" }
        });
        return tokenStore;
    }

    private static StringContent JsonContent(string json)
    {
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private sealed class Base64JsonTokenProtector : IPortableAuthTokenProtector
    {
        public string Protect(byte[] data) => Convert.ToBase64String(data);
        public byte[] Unprotect(string payload) => Convert.FromBase64String(payload);
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;
        public List<HttpRequestMessage> Requests { get; } = new();
        public List<string> Bodies { get; } = new();

        public SequenceHandler(params HttpResponseMessage[] responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
            return _responses.Dequeue();
        }
    }

    private static async Task WaitForCountAsync(Func<int> currentCount, int expectedCount)
    {
        var deadline = DateTime.UtcNow.AddSeconds(1);
        while (DateTime.UtcNow < deadline)
        {
            if (currentCount() >= expectedCount)
            {
                return;
            }

            await Task.Delay(5);
        }

        Assert.Fail($"Expected at least {expectedCount} ticks, observed {currentCount()}.");
    }

    private sealed class ManualTickSource : IPortableTimerTickSource
    {
        private readonly Channel<bool> _ticks = Channel.CreateUnbounded<bool>();
        private readonly ConcurrentQueue<TaskCompletionSource> _waiters = new();
        private int _observedTicks;

        public async Task TickAsync()
        {
            await _ticks.Writer.WriteAsync(true);
        }

        public async IAsyncEnumerable<DateTime> WaitForTicksAsync(
            TimeSpan interval,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            while (await _ticks.Reader.WaitToReadAsync(cancellationToken))
            {
                while (_ticks.Reader.TryRead(out _))
                {
                    var observed = Interlocked.Increment(ref _observedTicks);
                    while (_waiters.TryDequeue(out var waiter))
                    {
                        waiter.TrySetResult();
                    }

                    yield return DateTime.UtcNow.AddTicks(observed);
                }
            }
        }
    }
}
