# 天命 macOS 迁移 — M5 macOS 平台能力补齐设计

日期：2026-05-12
分支：`m5/macos-platform-2026-05-12`（计划，本 spec 在 `m2-m6/specs-2026-05-12`）
依据：`Docs/macOS迁移/功能对齐审计与迁移设计草案.md`、`Docs/macOS迁移/功能对齐矩阵.md`、`Docs/superpowers/specs/2026-05-12-tianming-m4-module-pages-design.md`

## 1. 范围与边界

### 1.1 纳入 M5

- **A. Keychain 真值接入**：`MacOSKeychainApiKeySecretStore`（M1 已端口）改走真实 `Security.framework` P/Invoke；替代 M1 的 stub 实现；提供真实增删改查路径并做人工验收。
- **B. 系统通知**：基于 `UserNotifications.framework`（UNUserNotificationCenter）的 `MacOSNotificationService`；首启请求授权；降级路径走 `osascript display notification`。
- **C. 菜单栏图标 / 托盘**：基于 `NSStatusBar` + `NSStatusItem` + `NSMenu` 的 `MacOSTrayService`；菜单项支持"显示主窗口"、"新建项目"、"最近项目 ×5"、"退出"；应用关闭主窗口时后台驻留（可在偏好关闭该行为）。
- **D. 文件类型关联 / URL Protocol**：在 `Info.plist` 中声明 `tianming://` scheme 与 `.tm` 项目关联；`MacOSUrlProtocolService` 监听 `OpenUrls` 事件并路由到导航服务；`MacOSFileTypeAssociationService` 注册/查询。
- **E. 系统主题跟随**：`MacOSSystemAppearanceMonitor`（M1 已端口 shell 轮询）补充基于 `NSDistributedNotificationCenter` 的事件订阅，实时跟随 Light/Dark 切换；变更事件接入 `PortableThemeStateController.Apply()`。
- **F. 音频 / 通知声**：沿用 M1 端口的 `AVFoundation` binding，`MacOSNotificationSoundService` 提供"完成提示音"、"失败提示音"、"新消息提示音"；音频设备枚举做可选。
- **G. 系统代理**：`MacOSSystemProxyService`（M1 已端口 shell 实现）升级为 `SCDynamicStore` 调用并补充 PAC 脚本下载；AI HttpClient 自动从代理服务读取策略。
- **H. 开机自启**：`MacOSAutoStartService`（基于 `SMAppService` 或 LaunchAgent plist）。
- **I. 全局快捷键 / 键盘链**：基于 `NSEvent.addGlobalMonitorForEvents` 的 `MacOSGlobalShortcutService`；天命内置快捷键（⌘+S / ⌘+W / ⌘+⇧+P 命令面板 / ⌘+, 偏好）统一在 `KeyboardShortcutsCatalog` 注册。
- **J. 系统信息 / 监控 / 诊断真实探针**：`MacOSSystemProfilerProbe`、`MacOSSystemMonitorProbe`（M1 已端口 shell 探针）接入系统信息 / 运行环境 / 诊断 / 系统监控页面；`powermetrics` 权限缺失时走降级。
- **K. 文件对话框 / 剪贴板 / 窗口能力**：Avalonia 内置 `TopLevel.StorageProvider` 提供；天命仅封装统一 `IPlatformDialogService`；`IClipboardService` 走 Avalonia Clipboard API。
- **L. 菜单栏主菜单**：应用级 macOS 菜单栏（⌘+Q、⌘+W、⌘+M、⌘+N、帮助、偏好），区分主窗口子菜单与应用菜单。

### 1.2 不在 M5 范围

- 服务端能力接入（登录、订阅、心跳）— M6
- 打包签名公证（M7）
- 窗口拖拽/闪烁/透明等 Windows 专属效果（不做，降级为 Avalonia 默认）
- ProtectionService（反调试 / TMProtect native）— macOS 不复用，`IProtectionService` 提供 Null 实现，SSL Pinning 已独立到 M2
- 语音播报 `VoiceBroadcastViewModel` 的真实合成引擎 — M5 仅提供 `INarrationService` 接口并默认 `AVSpeechSynthesizer`，真合成验收留 v1.1
- 智能拆书 WebView 真接入 — M2 已完成

### 1.3 决策记录

| 编号 | 决策 |
|---|---|
| Q1 | 原生互操作策略：UI 感知能力走 P/Invoke（NSStatusBar、UNUserNotificationCenter、NSEvent.globalMonitor、NSDistributedNotificationCenter）；基础设施能力走 shell（defaults、security、open、osascript、system_profiler、pmset、top、powermetrics） |
| Q2 | P/Invoke 绑定放 `src/Tianming.Desktop.Avalonia/Platform/Native/MacosNative/`，按类别分单文件；不引 Xamarin.Mac |
| Q3 | 菜单栏图标 v1 必须包含，默认启用；可在偏好关闭 |
| Q4 | Keychain 使用 `kSecClassGenericPassword`，Service 前缀 `com.tianming.apikey.`，Account 为 Provider ID |
| Q5 | 通知授权请求时机：首次需要发通知时按需请求（不在启动时阻塞 UI） |
| Q6 | 全局快捷键默认禁用，用户可在偏好页启用；默认快捷键仅触发主窗口激活 |
| Q7 | 代理：HttpClient 的 `HttpMessageHandler` 注入 `SocketsHttpHandler` + `Proxy = MacOSSystemProxy.Resolve()`；PAC 文件超时 5 秒，失败降级 `DefaultProxy` |
| Q8 | Info.plist 由 M7 打包管理；M5 在 `src/Tianming.Desktop.Avalonia/Platform/Mac/Info.plist.template` 中提供基线条目 |
| Q9 | `NSDistributedNotificationCenter` 订阅走 P/Invoke（`AppleInterfaceThemeChangedNotification`），收到后 debounce 200ms 再 apply |

## 2. 架构改造

### 2.1 平台抽象层

接口在 `src/Tianming.Framework/Platform/` 下已部分存在（M1），M5 补齐接口契约并落 macOS 实现到 `Tianming.Desktop.Avalonia`：

```
src/Tianming.Framework/Platform/        （接口与 portable 默认实现）
├── ISecureStorage.cs                    （已存在：ApiKey Keychain 抽象）
├── INotificationService.cs              （M5 新增）
├── ITrayService.cs                      （M5 新增）
├── IFileAssociationService.cs           （M5 新增）
├── IUrlProtocolService.cs               （M5 新增）
├── IAutoStartService.cs                 （M5 新增）
├── ISystemProxyService.cs               （M1 已端口 PortableSystemProxy，M5 扩展）
├── IGlobalShortcutService.cs            （M5 新增）
├── INarrationService.cs                 （M5 新增，占位）
├── IPlatformDialogService.cs            （M5 新增，File/Folder pickers）
├── IAppMenuService.cs                   （M5 新增，应用级菜单栏）
└── ISystemAppearanceMonitor.cs          （M1 已端口，M5 补 NSDistributedNotification）
```

macOS 实现：

```
src/Tianming.Desktop.Avalonia/Platform/
├── Mac/
│   ├── MacOSKeychainApiKeySecretStore.cs   （改造 M1 stub → 真 P/Invoke）
│   ├── MacOSNotificationService.cs
│   ├── MacOSTrayService.cs
│   ├── MacOSFileAssociationService.cs
│   ├── MacOSUrlProtocolService.cs
│   ├── MacOSAutoStartService.cs
│   ├── MacOSSystemProxyService.cs           （SCDynamicStore + PAC）
│   ├── MacOSGlobalShortcutService.cs
│   ├── MacOSNotificationSoundService.cs
│   ├── MacOSNarrationService.cs             （AVSpeechSynthesizer）
│   ├── MacOSPlatformDialogService.cs
│   ├── MacOSAppMenuService.cs
│   ├── MacOSSystemAppearanceMonitor.cs      （订阅 NSDistributedNotification）
│   └── Info.plist.template
└── Native/
    └── MacosNative/
        ├── Security.cs                      （SecItem API）
        ├── UserNotifications.cs             （UNUserNotificationCenter）
        ├── AppKit.cs                        （NSStatusBar / NSMenu）
        ├── Foundation.cs                    （NSString / NSDictionary 胶水）
        ├── SystemConfiguration.cs           （SCDynamicStore 代理）
        ├── CoreFoundation.cs                （CFString / CFDictionary）
        ├── ObjCRuntime.cs                   （objc_msgSend / class_get*）
        └── Dispatch.cs                      （DispatchQueue 主线程）
```

### 2.2 P/Invoke 胶水

`ObjCRuntime.cs` 提供最小 runtime 封装（不引 MonoMac/Xamarin.Mac）：

```csharp
internal static class ObjCRuntime
{
    private const string LibObjC = "/usr/lib/libobjc.dylib";
    [DllImport(LibObjC)] public static extern IntPtr objc_getClass(string name);
    [DllImport(LibObjC)] public static extern IntPtr sel_registerName(string name);
    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    public static extern IntPtr SendPointer(IntPtr target, IntPtr selector);
    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    public static extern IntPtr SendPointer(IntPtr target, IntPtr selector, IntPtr arg1);
    [DllImport(LibObjC, EntryPoint = "objc_msgSend")]
    public static extern bool SendBool(IntPtr target, IntPtr selector);
    // ... 按需扩展 long / double 重载
}
```

调用约定：
- 统一走 `Send*` 弱类型封装；受益于 .NET 8 的 `LibraryImport` source generator 之后可迁移到 `[LibraryImport]`
- 所有 NSObject 生命周期走 `retain/release`；用 `SafeObjCHandle : SafeHandle` 封装
- 字符串 bridge：`NSString.FromString(string)` / `NSString.ToString(IntPtr nsString)` 走 `UTF8String` + `strlen`
- 主线程调度：`Dispatch.MainSync/Async` 走 `dispatch_get_main_queue()` + `dispatch_async_f`

### 2.3 Keychain 实现要点

```csharp
public sealed class MacOSKeychainApiKeySecretStore : IApiKeySecretStore
{
    private const string ServicePrefix = "com.tianming.apikey.";
    public Task<string?> GetAsync(string providerId, CancellationToken ct) { /* SecItemCopyMatching */ }
    public Task SetAsync(string providerId, string apiKey, CancellationToken ct) { /* SecItemAdd or SecItemUpdate */ }
    public Task DeleteAsync(string providerId, CancellationToken ct) { /* SecItemDelete */ }
    public Task<bool> HasAsync(string providerId, CancellationToken ct);
}
```

- Service `com.tianming.apikey.<providerId>`、Account 固定 `default`（一个 provider 仅一条记录）
- `errSecItemNotFound` 映射 null；其他错误抛 `KeychainException`
- 写入同 Service/Account 先 `SecItemUpdate`，`errSecItemNotFound` 回退 `SecItemAdd`
- 首次写入会触发 Keychain 授权弹窗（系统级），后续自动放行
- M5 提供手动验收脚本 `Scripts/keychain-smoke.sh`：生成 / 读取 / 删除 / 清理

### 2.4 菜单栏与应用菜单

菜单栏图标：
- `NSStatusBar.systemStatusBar` → 请 `NSStatusItem`（`NSVariableStatusItemLength`）
- `button.image` 设 `NSImage`（18x18 template 模板图）
- 按 `NSMenu` 组装菜单项；每个 `NSMenuItem` 绑定 action selector → 转发到 C# `Action<>` 回调
- 动作列表（最小集）：
  - 显示主窗口
  - 新建项目
  - 最近项目（子菜单，来自 `IRecentProjectsService`）
  - —
  - 偏好设置
  - 退出

应用菜单（主菜单栏）：
- `NSApp.mainMenu` → `NSMenu`
- 第一项 "天命"：关于 / 偏好 ⌘, / 服务 / 隐藏 ⌘H / 隐藏其他 ⌥⌘H / 显示全部 / 退出 ⌘Q
- "文件"：新建 ⌘N / 打开 ⌘O / 最近 / 关闭标签 ⌘W / 保存 ⌘S / 另存为 ⇧⌘S
- "编辑"：撤销 ⌘Z / 重做 ⇧⌘Z / 剪切 ⌘X / 复制 ⌘C / 粘贴 ⌘V / 全选 ⌘A / 查找 ⌘F
- "视图"：切换侧栏 / 全屏 ⌃⌘F
- "窗口"：最小化 ⌘M / 缩放 / 合并所有窗口
- "帮助"：官网 / 文档 / 反馈

### 2.5 URL Protocol 与文件关联

`Info.plist.template` 基线条目：

```xml
<key>CFBundleURLTypes</key>
<array>
  <dict>
    <key>CFBundleURLName</key><string>com.tianming.url</string>
    <key>CFBundleURLSchemes</key><array><string>tianming</string></array>
  </dict>
</array>
<key>CFBundleDocumentTypes</key>
<array>
  <dict>
    <key>CFBundleTypeName</key><string>天命项目</string>
    <key>CFBundleTypeRole</key><string>Editor</string>
    <key>LSItemContentTypes</key><array><string>com.tianming.project</string></array>
  </dict>
</array>
<key>UTExportedTypeDeclarations</key>
<array>
  <dict>
    <key>UTTypeIdentifier</key><string>com.tianming.project</string>
    <key>UTTypeDescription</key><string>天命项目</string>
    <key>UTTypeConformsTo</key><array><string>public.folder</string></array>
    <key>UTTypeTagSpecification</key>
    <dict><key>public.filename-extension</key><array><string>tm</string></array></dict>
  </dict>
</array>
```

Avalonia `IClassicDesktopStyleApplicationLifetime.Startup` 回调中读取命令行参数 / `NSAppleEventManager` 监听 `kAEOpenDocuments` / `kAEGetURL`，路由到 `IUrlProtocolService` / `IFileAssociationService`。

### 2.6 系统代理

```csharp
public sealed class MacOSSystemProxyService : ISystemProxyService
{
    public ProxyPolicy Resolve(Uri target)
    {
        // 1. SCDynamicStoreCopyProxies → CFDictionary
        // 2. 按 target scheme 选 HTTP/HTTPS/SOCKS 配置
        // 3. 处理 AutoProxyURL（PAC） → 下载 .pac，执行 FindProxyForURL（不做 JS，退化为"使用/直连"启发式）
        // 4. ExceptionsList 匹配 bypass
        // 5. 返回 ProxyPolicy { Uri? Proxy, bool Bypass }
    }
}
```

`HttpClient` 组装走 `SocketsHttpHandler { Proxy = new AvaloniaSystemProxy(service) }`；此 Proxy 在每次请求时调用 `service.Resolve(uri)`。

### 2.7 目录结构（完整）

M5 新增：
- `src/Tianming.Framework/Platform/` 补齐接口
- `src/Tianming.Desktop.Avalonia/Platform/Mac/`
- `src/Tianming.Desktop.Avalonia/Platform/Native/MacosNative/`
- `src/Tianming.Desktop.Avalonia/Platform/Mac/Info.plist.template`
- `tests/Tianming.Desktop.Avalonia.Tests/Platform/Mac/`（可跑单测的抽象部分）
- `Scripts/keychain-smoke.sh`、`Scripts/tray-smoke.md`（人工验收脚本）

## 3. 工作拆分

### 3.1 Wave 0（主代理串行，~60 分钟）

1. `src/Tianming.Framework/Platform/` 下新增所有接口文件（空签名）
2. `src/Tianming.Desktop.Avalonia/Platform/Native/MacosNative/` 建立 ObjC runtime 胶水（`ObjCRuntime.cs` + `Foundation.cs` + `CoreFoundation.cs` + `Dispatch.cs`）
3. 建立 `SafeObjCHandle : SafeHandle` 与 `NSString` / `NSDictionary` / `NSArray` 最小 bridge
4. 加基础 xUnit 测试验证 bridge 能 round-trip 字符串（`NSString.FromString("hello").ToString()` 返回 "hello"）
5. Commit：`feat(platform): M5 P/Invoke bridge 基线`

### 3.2 Wave 1（5 个 agent 并行）

| ID | 范围 | 主要新增 |
|---|---|---|
| **E1** | Keychain 真实实现 | `MacOSKeychainApiKeySecretStore.cs` + `Native/MacosNative/Security.cs` + xUnit（skip on non-macOS） |
| **E2** | 通知服务 | `MacOSNotificationService.cs` + `Native/MacosNative/UserNotifications.cs` |
| **E3** | 菜单栏图标 | `MacOSTrayService.cs` + `Native/MacosNative/AppKit.cs`（NSStatusBar / NSMenu 子集） |
| **E4** | 系统外观订阅 | `MacOSSystemAppearanceMonitor.cs` 升级 + `NSDistributedNotificationCenter` P/Invoke |
| **E5** | 系统代理 | `MacOSSystemProxyService.cs` + `Native/MacosNative/SystemConfiguration.cs` + PAC 下载器 |

### 3.3 Wave 2（4 个 agent 并行）

| ID | 范围 | 主要新增 |
|---|---|---|
| **F1** | 应用菜单 / 全局快捷键 | `MacOSAppMenuService.cs` + `MacOSGlobalShortcutService.cs` + `KeyboardShortcutsCatalog` |
| **F2** | URL Protocol / 文件关联 | `MacOSUrlProtocolService.cs` + `MacOSFileAssociationService.cs` + `Info.plist.template` + `NSAppleEventManager` bridge |
| **F3** | 开机自启 | `MacOSAutoStartService.cs`（SMAppService 优先，LaunchAgent plist fallback） |
| **F4** | 通知声 / 语音 | `MacOSNotificationSoundService.cs` + `MacOSNarrationService.cs`（AVSpeechSynthesizer） |

### 3.4 Wave 3（主代理，平台偏好页面接入）

1. `Settings/PreferencesPage.axaml`：键盘快捷键、菜单栏开关、开机自启、通知授权、系统代理状态只读展示
2. 各 portable service 注册到 DI（`AddAvaloniaShellServices`）
3. 在 `AppLifecycle.Startup` 中初始化托盘、主菜单、URL Protocol 监听
4. 更新 `AppPaths` 使用 `NSSearchPathForDirectoriesInDomains` 获得标准路径（`Application Support`、`Caches`、`Preferences`、`Logs`）

### 3.5 Wave 4（最终收尾）

1. 人工验收：Keychain 增删、通知显示、托盘菜单点击、菜单栏快捷键、系统主题跟随、代理读取
2. 更新 `Docs/macOS迁移/功能对齐矩阵.md`：M5 涉及所有行状态更新为"已端口"
3. 更新 `Docs/macOS迁移/M5-人工验收.md`：记录验收项与截图
4. Commit 并 push

## 4. 依赖图

```
M5.Wave 0（ObjC runtime 胶水）
   ├── E1 Keychain
   ├── E2 通知
   ├── E3 菜单栏图标
   ├── E4 外观监听
   └── E5 系统代理
          ↓
M5.Wave 2（F1-F4）独立，仅依赖 Wave 0
          ↓
M5.Wave 3（DI 接线 + 偏好页接入）依赖 Wave 1/2 全部合流
          ↓
M5.Wave 4（验收 + 文档）
```

## 5. 测试策略

| 类别 | 做法 | 说明 |
|---|---|---|
| P/Invoke bridge 单测 | `[Fact]` + `Skip` 属性跳过非 macOS | 只在 macOS CI 上跑 |
| Keychain 单测 | Service 前缀 `com.tianming.test.apikey.`，测试隔离 | teardown 调 `SecItemDelete` 清理 |
| 通知单测 | 注入 `IMacOSNotificationCenter` 接口做可 mock | 真实发送仅人工验收 |
| 托盘单测 | 纯 VM 层（`TrayMenuBuilder`）单测 | NSStatusBar 交互仅人工验收 |
| 系统代理单测 | `ProxyPolicyResolver` 纯数据单测 | SCDynamicStore 调用仅人工验收 |
| 人工验收清单 | `Docs/macOS迁移/M5-人工验收.md` | 每能力 1-2 个场景 + 截图 |

自动化测试数约增 100 条；人工验收 30+ 场景。

## 6. 兼容性矩阵

| 能力 | macOS 最低版本 | 备注 |
|---|---|---|
| Keychain（Security.framework） | 10.9+ | 全版本可用 |
| UNUserNotificationCenter | 10.14+ | 低于此版本降级到 NSUserNotification（deprecated） |
| NSStatusBar / NSStatusItem | 10.0+ | 全版本可用 |
| NSDistributedNotificationCenter | 10.0+ | `AppleInterfaceThemeChangedNotification` 在 10.14 起 |
| SMAppService | 13.0+ | 低于此版本走 LaunchAgent plist |
| SCDynamicStoreCopyProxies | 10.4+ | 全版本可用 |
| AVSpeechSynthesizer | 10.14+ | 低于此版本降级 osascript say |
| GlobalShortcut（NSEvent.addGlobal） | 10.6+ | 需"辅助功能"权限 |

M5 最低目标：**macOS 13 Ventura**（与 M0 基线一致）；13 以下不强制支持。

## 7. 风险与回滚

### 7.1 主要风险

| ID | 风险 | 影响 | 缓解 |
|---|---|---|---|
| R1 | P/Invoke 调用约定错误导致 crash | 应用崩溃 | 每个 NSObject 交互走 `SafeObjCHandle`；crash 时立即 try/catch + log；启动时加 `AppDomain.UnhandledException` 守护 |
| R2 | Keychain 授权弹窗引起首启卡顿 | 用户体验 | 异步写入 + UI loading；首启指引说明 Keychain 用途 |
| R3 | `UNUserNotificationCenter` 授权被拒 | 通知无法送达 | 检测授权状态，失败时 fallback `osascript display notification` + 用户提示去系统设置开启 |
| R4 | `NSEvent.addGlobalMonitorForEvents` 需辅助功能权限 | 全局快捷键无效 | 检测 AXIsProcessTrusted；未授权时只监听应用内快捷键并提示用户去开启 |
| R5 | SMAppService 在 < 13 不可用 | 开机自启失败 | 落地 `~/Library/LaunchAgents/com.tianming.autostart.plist`；launchctl 加载 |
| R6 | PAC 脚本 JS 解析复杂 | 代理策略不准 | PAC 只支持"有代理则用配置的 Proxy，否则直连"的简化启发式；告知用户可手动配置 |
| R7 | Info.plist 未签名 / 未公证导致 URL scheme 失效 | 深链不工作 | M7 打包签名覆盖；M5 开发模式允许本地 unsigned 测试 |
| R8 | NSStatusBar 图标在深色菜单栏看不清 | 视觉问题 | 使用 `template` 模板图（自动反色）；提供两套备选图 |

### 7.2 回滚策略

- Wave 1/2 每能力独立 commit，失败单个 revert
- P/Invoke bridge（Wave 0）若 runtime 胶水有问题，整体 revert 即可回到 M4 基线
- `Info.plist.template` 独立 commit

### 7.3 退路

- 若 R1 连环发生：降级为 shell/osascript 覆盖大部分能力，仅 Keychain 必须 P/Invoke；v1 可行
- 若 R4 发生：全局快捷键功能直接禁用，文档里说明（"需辅助功能权限，当前 v1 不开放"）
- 若 R5 发生：开机自启改为引导用户手动添加到"登录项"

## 8. 验收标准

M5 完成的判据：

1. `dotnet build Tianming.MacMigration.sln` → 0 Warning / 0 Error
2. `dotnet test Tianming.MacMigration.sln -v minimal` → 全过（较 M4 基线增加 ≥ 100 用例）
3. `Scripts/check-windows-bindings.sh src/ tests/` → 0 命中
4. 人工验收清单（`Docs/macOS迁移/M5-人工验收.md`）≥ 30 项全部通过：
   - Keychain 增删改查 + 系统授权弹窗行为
   - 通知发送（含授权拒绝降级）
   - 菜单栏图标点击 → 显示窗口、新建项目、最近项目、退出
   - 应用主菜单栏完整项（关于、偏好、退出、编辑/查找/全选、窗口/全屏）
   - URL scheme `tianming://project/<id>` 启动应用并跳转项目
   - 主题跟随系统（深/浅切换时 UI 即时更新）
   - 开机自启开关可生效
   - 系统代理识别并通过 HttpClient 走代理
5. `Docs/macOS迁移/功能对齐矩阵.md` 中 M5 涉及 15+ 行状态更新为"已端口"
6. 分支 `m5/macos-platform-2026-05-12` 已 push

完成后进入 M6（服务端能力接入）。