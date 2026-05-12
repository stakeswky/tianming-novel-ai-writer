# 天命 macOS 迁移 — M6 服务端能力接入设计

日期：2026-05-12
分支：`m6/server-capabilities-2026-05-12`（计划，本 spec 在 `m2-m6/specs-2026-05-12`）
依据：`Docs/macOS迁移/功能对齐审计与迁移设计草案.md`、`Docs/macOS迁移/功能对齐矩阵.md`、`Docs/superpowers/specs/2026-05-12-tianming-m5-macos-platform-design.md`

## 1. 范围与边界

### 1.1 纳入 M6

- **A. 登录 / 注册 / 找回密码**：基于 M1 端口的 `PortableAuthApiClient` / `PortableLoginController` / `PortableRegistrationController` / `PortablePasswordResetController` 接入 Avalonia UI；登录态与 token 存储走 M5 Keychain。
- **B. OAuth 第三方登录**：`PortableOAuthController`（Google / Apple / GitHub 三选或全部，依据已有协议）接入；回调走 `tianming://oauth/callback` URL Scheme（M5 已落）或 localhost loopback 两种策略。
- **C. 订阅 / 卡密 / 续费**：`PortableSubscriptionStore` / `PortableSubscriptionApiProtocol` / `PortableCardKeyActivationMessages` / `PortableAccountRenewalController` 接入订阅页面与续费弹窗。
- **D. 心跳 / 公告 / 强制登出**：`PortableServerHeartbeatPeriodicService` / `PortableServerHeartbeatRunner` / `PortableReturnToLoginNavigationController` 在 Avalonia `AppLifecycle` 中接入；公告呈现走 M5 通知 + 应用内对话框；强制登出时清理 Keychain token、跳回登录页。
- **E. 功能授权**：`PortableFeatureAuthorizationService`（M1 已端口）接入章节生成、AI 调用、导出等受保护入口；授权失败时弹 `FeatureRestrictedDialog` 并引导升级订阅。
- **F. SSL Pinning 真实 pin 注入**：`PortableSslPinningServerConfiguration`（M1 已端口）的 pin 值通过 `pinning.json` 随包分发；运营流程文档化；v1 至少含当前 + 备份两套 pin。
- **G. 自动更新**：`PortableAppUpdatePolicy` / `PortableAppUpdateDownloader` / `PortableMacOSAppInstallerPlanner`（M1 已端口）接入偏好页"检查更新"与周期自动检查；macOS 下载 `.dmg` 后 `open` 启动安装器。
- **H. 账号管理 UI**：用户资料、头像、登录设备、订阅详情、卡密历史、账号注销、数据导出（若服务端支持）。
- **I. Fake Server 套件**：扩展 M2 的 `Tianming.AI.TestSupport` 为 `Tianming.Server.TestSupport`，加 `FakeAuthServer` / `FakeSubscriptionServer` / `FakeHeartbeatServer` / `FakeOAuthServer`，使登录/订阅/心跳闭环可全 CI 跑通。

### 1.2 不在 M6 范围

- 真实生产服务端部署与密钥管理（运营团队职责，M6 仅接受产出）
- 服务端行为修改 / 新 API 设计（M6 仅消费已存在的 portable 协议）
- 打包、公证、App Store 上架（M7）
- ProtectionService（反调试 / DLL pin）— macOS 不复用
- 服务端统计 / A/B 实验 / 远程功能开关（留 v1.1）

### 1.3 决策记录

| 编号 | 决策 |
|---|---|
| Q1 | OAuth 回调：优先 `tianming://oauth/callback`（M5 URL Scheme 已落）；localhost loopback 作为 fallback（无 URL Scheme 权限时） |
| Q2 | Token 存储：refresh token 进 Keychain，access token 内存 + 短期缓存，过期前 5 分钟静默刷新 |
| Q3 | 服务端 base URL 运行时可改：`PortableServerEndpointStore`（M6 新增），用户可在偏好"高级"选 生产/预发/自定义 |
| Q4 | Fake server 套件：单一 `Tianming.Server.TestSupport` 库，内部按服务拆 `Fake*Server`，复用 M2 Kestrel 基础设施 |
| Q5 | 心跳周期：15 分钟（延续服务端约定）；低电量 / 后台时不停止，仅降低优先级 |
| Q6 | 强制登出：立即清理 Keychain token + 内存缓存 + 切回登录页 + 展示原因；不静默 |
| Q7 | SSL Pinning pin 值文件：`pinning.json` 放在 app bundle Resources 下，非用户可改；v1 至少两套 pin（当前证书 + 下一轮证书） |
| Q8 | 自动更新下载目录：`~/Library/Caches/Tianming/Updates/`；安装后清理 |
| Q9 | 功能授权缓存：原版 5 分钟 TTL 保留；网络异常时返回最后一次 "授权通过" 结果并打点 |
| Q10 | `ServerMode=false` 本地降级：完整保留，无服务端配置时应用仍可作为本地写作工具使用 |

## 2. 架构改造

### 2.1 运行模式切换

```
Tianming.MacMigration.sln
└── 启动决策树
    │
    ├── 无 ServerBaseUrl 配置  →  LocalMode
    │   └── 跳过登录/心跳/订阅/授权；功能授权默认通过
    │
    └── 有 ServerBaseUrl 配置  →  ServerMode
        ├── 未登录：直接进入 LoginView
        ├── 已登录 + token 有效：进入主窗口，启动心跳
        ├── 已登录 + token 失效：刷新 token；失败则跳登录页
        └── 心跳强制登出：清理 token + 回登录页
```

`IAppModeDetector`（M6 新增）：启动时读取 `PortableServerEndpointStore` → 决定 `AppMode.Local` 或 `AppMode.Server`；后续所有服务通过 DI 按模式注入不同实现（如 `IFeatureAuthorizationService` 在 Local 模式绑定 `NullFeatureAuthorizationService`）。

### 2.2 目录结构

```
src/Tianming.Server/                     （新增：M6 主承载）
├── Tianming.Server.csproj
├── Authentication/
│   ├── AuthSessionManager.cs            （登录/登出/token 生命周期编排）
│   ├── TokenStore.cs                    （抽象：IApiKeySecretStore 复用）
│   ├── OAuthFlowController.cs
│   └── LoopbackOAuthListener.cs         （Kestrel on 127.0.0.1 随机端口）
├── Subscription/
│   ├── SubscriptionQueryService.cs
│   ├── CardKeyActivationService.cs
│   └── AccountRenewalService.cs
├── Heartbeat/
│   ├── HeartbeatHostedService.cs        （Microsoft.Extensions.Hosting BackgroundService）
│   ├── AnnouncementPresenter.cs         （M5 INotificationService + 应用内弹窗）
│   └── ForcedLogoutCoordinator.cs
├── FeatureGuard/
│   └── FeatureGuardMiddleware.cs        （VM 层拦截器）
├── Profile/
│   ├── UserProfileSyncService.cs
│   └── AvatarUploadService.cs
├── Update/
│   ├── AppUpdateOrchestrator.cs
│   └── MacOSUpdateInstaller.cs          （调用 M5 PlatformAdapter.Open）
└── ServiceCollectionExtensions.cs       （AddServerServices）

src/Tianming.Desktop.Avalonia/Views/Server/
├── LoginView.axaml / ViewModel
├── RegisterView.axaml / ViewModel
├── PasswordResetView.axaml / ViewModel
├── OAuthStartDialog.axaml
├── SubscriptionPage.axaml / ViewModel
├── CardKeyActivationDialog.axaml
├── AccountRenewalDialog.axaml
├── UserProfilePage.axaml / ViewModel
├── FeatureRestrictedDialog.axaml
├── ForcedLogoutDialog.axaml
├── AnnouncementToast.axaml
└── UpdateAvailableDialog.axaml

tests/Tianming.Server.Tests/             （新增）
tests/Tianming.Server.TestSupport/       （新增：fake servers）
├── FakeAuthServer.cs
├── FakeSubscriptionServer.cs
├── FakeHeartbeatServer.cs
├── FakeOAuthServer.cs
└── FakeUpdateManifestServer.cs
```

`Tianming.MacMigration.sln` 加入 `Tianming.Server` / `Tianming.Server.Tests` / `Tianming.Server.TestSupport`。

### 2.3 Fake Server 套件

每个 `Fake*Server` 基于 `WebApplication.CreateBuilder()` + 随机端口 + xUnit `IAsyncLifetime`，契约：
- 启动后暴露 `BaseUrl`
- 提供 `Setup*` 方法预置返回（如 `SetupSuccessfulLogin(username, password, tokenPayload)`）
- 暴露 `ReceivedRequests` 属性做断言
- `Dispose` 优雅关闭

典型用法：
```csharp
await using var auth = new FakeAuthServer();
await using var hb = new FakeHeartbeatServer();
auth.SetupSuccessfulLogin("alice", "pwd", new TokenPayload(...));
hb.SetupHeartbeatSequence([HeartbeatOk(), HeartbeatOk(), HeartbeatForceLogout("device")]);

var app = await AppHost.StartAsync(builder => builder
    .UseServerBaseUrl(auth.BaseUrl)
    .UseHeartbeatBaseUrl(hb.BaseUrl));
```

### 2.4 OAuth 流程

**主路径：URL Scheme**
1. 用户点击"Google 登录" → 打开系统浏览器访问 `https://<server>/oauth/start?provider=google&return=tianming://oauth/callback`
2. OAuth 完成 → 浏览器 302 到 `tianming://oauth/callback?code=...`
3. macOS 系统把 URL 递交给天命（经 M5 `IUrlProtocolService`）
4. `OAuthFlowController.HandleCallback(code)` → POST `<server>/oauth/exchange` → 得到 token → 存 Keychain → 进主窗口

**Fallback：Loopback**
1. 本地随机端口启动 `LoopbackOAuthListener`（Kestrel）
2. 打开浏览器到 `https://<server>/oauth/start?provider=google&return=http://127.0.0.1:<port>/callback`
3. OAuth 完成 → 浏览器 302 到 `http://127.0.0.1:<port>/callback?code=...`
4. 同上

两种策略在启动时根据"是否可注册 URL Scheme"自动选择。

### 2.5 心跳周期

```csharp
public sealed class HeartbeatHostedService : BackgroundService
{
    private readonly PortableServerHeartbeatRunner _runner;
    private readonly PortableServerHeartbeatPeriodicService _periodic;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _periodic.StartAsync(TimeSpan.FromMinutes(15), stoppingToken);
    }
}
```

挂在 `Host` 的 BackgroundService；`IHostedService.StopAsync` 时优雅取消。

公告呈现：
- `SystemAnnouncement` → M5 `INotificationService.ShowAsync` + 应用内 toast
- `ForcedLogout` → `ForcedLogoutCoordinator.Handle()`：清 Keychain + 清内存 token + 跳 `LoginView` + 弹对话框（含服务端原因文本）
- `ForcedUpdate` → `UpdateAvailableDialog`，强制模式不允许"稍后"

### 2.6 功能授权拦截

VM 执行受保护命令前调用 `IFeatureGuard.EnsureAsync("chapter.generate")`：

```csharp
public interface IFeatureGuard
{
    Task<FeatureGuardResult> EnsureAsync(string featureId, CancellationToken ct = default);
}

public record FeatureGuardResult(bool Allowed, string? DenyReason, FeatureDenyAction? Action);
public enum FeatureDenyAction { ShowRestrictedDialog, ReturnToLogin, ShowSubscriptionUpgrade }
```

常见保护入口：
- 章节生成 / AI 调用（`ai.generate`）
- 智能拆书（`book.analyze`）
- 发布打包（`package.publish`）
- 数据导出（`export.project`）

默认 Local 模式 `IFeatureGuard` 注入 `AlwaysAllowFeatureGuard`，ServerMode 注入真实实现。

### 2.7 SSL Pinning 部署

`src/Tianming.Desktop.Avalonia/Assets/pinning.json`：

```json
{
  "servers": [
    {
      "host": "api.tianming.example.com",
      "pins": [
        { "sha256": "<primary-pin-sha256>", "notBefore": "2026-01-01", "notAfter": "2026-12-31" },
        { "sha256": "<backup-pin-sha256>", "notBefore": "2026-06-01", "notAfter": "2027-05-31" }
      ]
    }
  ]
}
```

`PortableSslPinningValidator` 读取并构造 `HttpClientHandler` 回调；所有 `Tianming.Server` 的 `HttpClient` 都注入该 handler。

运营流程（文档化）：
- 服务端证书轮换前 30 天，运营计算新证书 SPKI SHA-256 → 提交 `pinning.json` 更新 → 发布新版本
- 用户必须在旧版本失效前升级（通过 `ForcedUpdate` 推送）

## 3. 工作拆分

### 3.1 Wave 0（主代理串行，~90 分钟）

1. 新建 `Tianming.Server` / `Tianming.Server.Tests` / `Tianming.Server.TestSupport` 三个项目
2. 加入 `Tianming.MacMigration.sln`
3. 建立 `IAppModeDetector`、`PortableServerEndpointStore`、`AuthSessionManager` 骨架
4. 建立 `Fake*Server` 基础类（共享 Kestrel 启动代码）
5. 跑基线测试（验证 sln 编译 + 原 tests 全过）
6. Commit：`feat(server): M6 server 项目基线`

### 3.2 Wave 1（6 个 agent 并行）

| ID | 范围 | 主要新增 |
|---|---|---|
| **G1** | 登录 / 注册 / 找回密码 VM + 视图 | `LoginView*` / `RegisterView*` / `PasswordResetView*` + Fake 对接单测 |
| **G2** | OAuth 流程 | `OAuthFlowController` + `LoopbackOAuthListener` + `OAuthStartDialog` + `FakeOAuthServer` 单测 |
| **G3** | 订阅 / 卡密 / 续费 页面 | `SubscriptionPage*` / `CardKeyActivationDialog*` / `AccountRenewalDialog*` + 单测 |
| **G4** | 心跳 / 公告 / 强制登出 | `HeartbeatHostedService` + `AnnouncementPresenter` + `ForcedLogoutCoordinator` + `ForcedLogoutDialog` + `AnnouncementToast` |
| **G5** | 用户资料 / 头像 | `UserProfilePage*` + `AvatarUploadService` + 单测 |
| **G6** | 自动更新 | `AppUpdateOrchestrator` + `UpdateAvailableDialog` + `FakeUpdateManifestServer` + `MacOSUpdateInstaller`（接 M5 PlatformAdapter） |

### 3.3 Wave 2（3 个 agent 并行，依赖 Wave 1）

| ID | 范围 | 主要新增 |
|---|---|---|
| **H1** | 功能授权中间层 | `IFeatureGuard` / `FeatureGuardMiddleware` / `FeatureRestrictedDialog`；在 M4 各 VM 受保护入口插桩 |
| **H2** | SSL Pinning 部署 | `pinning.json` + 运营流程文档 + `HttpClient` 注入点清单 |
| **H3** | 账号模式切换与主窗口接线 | `AppModeDetector` + `AppLifecycle` 修改（先 LoginView 或直入主窗口） + DI 按模式分支 |

### 3.4 Wave 3（主代理收尾）

1. 端到端人工验收（搭配 Fake Server 套件）：
   - 注册 → 登录 → 查询订阅 → 激活卡密 → 发送心跳（正常/强制登出/强制更新）→ 触发功能授权限制 → OAuth 登录（两种回调路径各一次）
2. 运营对接：收取真实 pin 值 + OAuth ClientId + 生产 ServerBaseUrl，填入 `pinning.json` / `appsettings.production.json`
3. 真实服务端联调 smoke（由运营/开发联合执行），结果记入 `Docs/macOS迁移/M6-人工验收.md`
4. 更新 `Docs/macOS迁移/功能对齐矩阵.md`：M6 涉及 15+ 行状态更新为"已接入"
5. Commit `docs(server): M6 验收记录`；push

## 4. 依赖图

```
M5 完成（Keychain、URL Scheme、通知、托盘就位）
         ↓
M6.Wave 0（项目建立 + Fake Server 骨架）
         ↓
M6.Wave 1（6 并行：登录/OAuth/订阅/心跳/资料/更新）
         ↓
M6.Wave 2（3 并行：授权/Pinning/模式切换）
         ↓
M6.Wave 3（真实联调 + 文档）
```

## 5. 测试策略

| 类别 | 做法 | 覆盖 |
|---|---|---|
| 纯协议单测 | 沿用 M1 端口的 Portable* 单测 | Protocol / Store / Controller 已有覆盖 |
| Fake Server 集成测 | `Fake*Server` + 真 HttpClient | 登录、订阅、心跳、OAuth、更新端到端 |
| VM 绑定测 | `FakeAuthSessionManager` + ViewModel assertion | UI 状态与服务状态一致性 |
| 模式切换测 | `AppModeDetector` 黑盒 | Local vs Server 模式下 DI 正确 |
| 强制登出测 | Fake 心跳返回 force-logout → 期望 token 清理 + 跳登录页 | 安全关键路径 |
| SSL Pinning 单测 | `PortableSslPinningValidator` + mock 证书链 | M1 已覆盖，M6 补充 `pinning.json` 加载 |
| 人工验收 | 真实服务端联调 | OAuth 真实三方登录、真实卡密激活、真实订阅 |

自动化测试增量 ≥ 120 用例。

## 6. 风险与回滚

### 6.1 主要风险

| ID | 风险 | 影响 | 缓解 |
|---|---|---|---|
| R1 | 真实服务端与 Fake 行为不一致 | 线上 bug | 运营对 Fake Server 逐 API 校准；真实联调阶段按 contract 测试 |
| R2 | OAuth ClientId 未就位 | G2 无法真联调 | 使用 FakeOAuthServer 闭环；真联调推 M7 |
| R3 | SSL Pin 值错误 | 合法服务被拒 | `pinning.json` 至少两套 pin（当前+备份）；部署前 `Scripts/pin-verify.sh` 对准生产证书 |
| R4 | Keychain token 泄漏 | 安全事件 | token 只在 Keychain + 内存；不写日志；清除事件有 telemetry |
| R5 | 心跳频繁导致服务端压力 | 服务成本 | 15 分钟周期；失败指数退避；接 30 天限流告警 |
| R6 | 自动更新误升级破坏用户 | 用户数据风险 | 更新前备份 `~/Library/Application Support/Tianming/`；更新失败回滚旧版本；降级自动走 ForcedUpdate-optional |
| R7 | LoopbackOAuthListener 端口冲突 | 登录失败 | 随机端口 + 重试；失败提示用户关闭冲突进程 |
| R8 | 功能授权缓存误判 | 付费功能异常 | 缓存 TTL 5 分钟；授权失败时强制刷新；提供"重新检查"按钮 |

### 6.2 回滚策略

- Wave 1/2 每能力独立 commit，失败单个 revert
- `pinning.json` 版本化；错误 pin 值可 hotfix release
- 自动更新失败自动回退旧版本（`~/Library/Caches/Tianming/Updates/backup/`）

### 6.3 退路

- 若 R1 发生：推迟 M6 发布，保持 LocalMode 作为 v1 唯一路径；服务端能力改为 v1.1
- 若 OAuth 真联调推迟：v1 不开放第三方登录，仅账号/密码
- 若自动更新复杂度超估：改为"检查到新版本 → 跳浏览器下载"，不做自动安装

## 7. 验收标准

M6 完成的判据：

1. `dotnet build Tianming.MacMigration.sln` → 0 Warning / 0 Error
2. `dotnet test Tianming.MacMigration.sln -v minimal` → 全过（较 M5 基线增加 ≥ 120 用例）
3. `Scripts/check-windows-bindings.sh src/ tests/` → 0 命中
4. Fake Server 套件跑通全端到端：
   - 注册 → 登录 → 查订阅 → 激活卡密 → 续费
   - 心跳正常 / 强制登出 / 强制更新各 1 次
   - OAuth URL Scheme + Loopback 两路径各 1 次
   - 功能授权通过 / 拒绝 / 返回登录各 1 次
   - 自动更新下载 + 校验 + 启动安装器
5. 真实服务端联调（至少一次）：登录 + 心跳 + 订阅查询 + 功能授权
6. `pinning.json` 含真实生产 pin + 备份 pin；运营确认
7. LocalMode 走通本地创作闭环（无需任何服务端配置）
8. `Docs/macOS迁移/功能对齐矩阵.md` 中 M6 涉及 15+ 行状态更新为"已接入"
9. `Docs/macOS迁移/M6-人工验收.md` 含所有验收截图
10. 分支 `m6/server-capabilities-2026-05-12` 已 push

完成后进入 M7（打包、签名、公证与回归）。
