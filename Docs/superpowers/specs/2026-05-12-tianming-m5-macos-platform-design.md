# 天命 macOS 迁移 — M5 macOS 平台能力（自用版）

日期：2026-05-12
分支：`m2-m6/specs-2026-05-12`
依据：`Docs/superpowers/specs/2026-05-12-tianming-m4-module-pages-design.md`
定位：**个人自用**，不做公开分发

## 1. 范围与边界

### 1.1 纳入 M5

自用只保留三个真刚需 + 一个便宜就顺手做的：

- **A. Keychain 真值接入**：`MacOSKeychainApiKeySecretStore`（M1 已端口 stub）改走真实 `Security.framework` P/Invoke；M4 填入的 API Key 从明文 JSON 迁到 Keychain。
- **B. 系统代理**：`MacOSSystemProxyService`，从 `SCDynamicStoreCopyProxies` 读 HTTP/HTTPS 代理；`HttpClient` 组装 `SocketsHttpHandler { Proxy = ... }`，AI 请求走代理。
- **C. 系统主题跟随**：`MacOSSystemAppearanceMonitor`（M1 已端口 shell 轮询）升级为 `NSDistributedNotificationCenter` 订阅 `AppleInterfaceThemeChangedNotification`，Light/Dark 实时跟随。
- **D. 应用主菜单**（便宜就做）：`NSApp.mainMenu` 配基础菜单项（关于 / 偏好 ⌘, / 退出 ⌘Q / 新建 ⌘N / 打开 ⌘O / 保存 ⌘S / 全屏）。Avalonia 在 macOS 下会自动把顶部应用菜单放到系统菜单栏。

### 1.2 不做

自用一律不做：

- 菜单栏图标 / 托盘（NSStatusBar）
- URL Scheme `tianming://` / 文件关联 `.tm`（需改 Info.plist + 签名；自用没意义）
- 系统通知（UNUserNotificationCenter）/ 通知声
- 开机自启（SMAppService / LaunchAgent）
- 全局快捷键（NSEvent.addGlobalMonitorForEvents，需辅助功能权限）
- 语音播报（AVSpeechSynthesizer）
- 系统信息/监控/诊断真实探针（powermetrics、system_profiler）
- PAC 脚本解析
- Info.plist / entitlements / hardened runtime（M7 范畴，已砍）
- ProtectionService / 反调试
- v2.8.7 写作内核升级（追踪债务、人味润色、WAL、AI middleware、Agent 插件拆分等）—— 这些属于 M6，不混进平台层

### 1.3 决策

| 编号 | 决策 |
|---|---|
| Q1 | P/Invoke 只写三个：`Security`（Keychain）、`SystemConfiguration`（代理）、`Foundation`（NSDistributedNotificationCenter）；能 shell 就 shell |
| Q2 | ObjC runtime 胶水自建最小 `ObjCRuntime.cs`（objc_msgSend / sel_registerName / objc_getClass），不引 Xamarin.Mac / MonoMac |
| Q3 | Keychain：`kSecClassGenericPassword`，Service `com.tianming.apikey.<providerId>`，Account `default` |
| Q4 | 代理：PAC 脚本不解析，只读 `HTTPEnable`/`HTTPProxy`/`HTTPPort` 和 `HTTPSEnable`/`HTTPSProxy`/`HTTPSPort` |
| Q5 | 主题监听：debounce 200ms；订阅 `AppleInterfaceThemeChangedNotification` + 初次启动时取一次当前值 |
| Q6 | macOS 最低版本：**macOS 13 Ventura+**（与 M0 基线一致） |
| Q7 | M5 是平台收尾，不承接业务内核；完成后进入 M6 v2.8.7 写作内核升级 |

## 2. 架构

### 2.1 目录结构

```
src/Tianming.Framework/Platform/
├── ISecureStorage.cs                    （M1 已有）
├── ISystemProxyService.cs               （M1 有 Portable，M5 补接口）
└── ISystemAppearanceMonitor.cs          （M1 已有）

src/Tianming.Desktop.Avalonia/Platform/
├── Mac/
│   ├── MacOSKeychainApiKeySecretStore.cs   （改 M1 stub → 真 P/Invoke）
│   ├── MacOSSystemProxyService.cs
│   └── MacOSSystemAppearanceMonitor.cs     （订阅版）
└── Native/
    └── MacosNative/
        ├── ObjCRuntime.cs               （objc_msgSend + sel + class 最小封装）
        ├── Security.cs                  （SecItemAdd/Update/CopyMatching/Delete）
        ├── SystemConfiguration.cs       （SCDynamicStoreCopyProxies）
        └── Foundation.cs                （NSDistributedNotificationCenter.addObserver + NSString bridge）
```

### 2.2 Keychain 实现

```csharp
public sealed class MacOSKeychainApiKeySecretStore : IApiKeySecretStore
{
    private const string ServicePrefix = "com.tianming.apikey.";

    public Task<string?> GetAsync(string providerId, CancellationToken ct)
        => Task.FromResult(Security.SecItemCopyString(ServicePrefix + providerId, "default"));

    public Task SetAsync(string providerId, string apiKey, CancellationToken ct)
    {
        var svc = ServicePrefix + providerId;
        if (!Security.SecItemUpdate(svc, "default", apiKey))
            Security.SecItemAdd(svc, "default", apiKey);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string providerId, CancellationToken ct)
    { Security.SecItemDelete(ServicePrefix + providerId, "default"); return Task.CompletedTask; }

    public Task<bool> HasAsync(string providerId, CancellationToken ct)
        => Task.FromResult(Security.SecItemCopyString(ServicePrefix + providerId, "default") != null);
}
```

`Security.cs` 用 `SecItemAdd` / `SecItemUpdate` / `SecItemCopyMatching` / `SecItemDelete` P/Invoke；错误码处理：`errSecItemNotFound` → null，其他非 0 抛 `KeychainException`。

首次写入会触发 Keychain 授权弹窗（系统级），点"始终允许"后不再提示。

### 2.3 系统代理

```csharp
public sealed class MacOSSystemProxyService : ISystemProxyService
{
    public ProxyPolicy Resolve(Uri target)
    {
        var dict = SystemConfiguration.SCDynamicStoreCopyProxies();
        if (dict == null) return ProxyPolicy.Direct;

        var scheme = target.Scheme;
        bool enabled = dict.GetInt(scheme == "https" ? "HTTPSEnable" : "HTTPEnable") == 1;
        if (!enabled) return ProxyPolicy.Direct;

        var host = dict.GetString(scheme == "https" ? "HTTPSProxy" : "HTTPProxy");
        var port = dict.GetInt(scheme == "https" ? "HTTPSPort" : "HTTPPort");
        return string.IsNullOrEmpty(host) ? ProxyPolicy.Direct
            : new ProxyPolicy(new Uri($"http://{host}:{port}"), Bypass: false);
    }
}
```

`AppHost` 里：`services.AddHttpClient("AI").ConfigurePrimaryHttpMessageHandler(sp => new SocketsHttpHandler { Proxy = new MacOSHttpProxy(sp.GetRequiredService<ISystemProxyService>()) });`

### 2.4 主题监听

```csharp
public sealed class MacOSSystemAppearanceMonitor : ISystemAppearanceMonitor, IDisposable
{
    private IntPtr _observer;
    public event EventHandler<SystemAppearance>? AppearanceChanged;

    public void Start()
    {
        _observer = Foundation.AddDistributedObserver(
            "AppleInterfaceThemeChangedNotification",
            _ => DebouncedRaise());
        RaiseCurrent();   // 启动时取一次
    }

    public void Dispose() => Foundation.RemoveObserver(_observer);

    private void DebouncedRaise() { /* 200ms debounce → AppearanceChanged?.Invoke(sender, Read()) */ }
    private SystemAppearance Read() => Foundation.ReadAppleInterfaceStyle() == "Dark"
        ? SystemAppearance.Dark : SystemAppearance.Light;
}
```

事件接 `PortableThemeStateController.OnSystemAppearanceChanged` → `ThemeBridge` 切 Avalonia 资源。

### 2.5 应用主菜单

`Avalonia 11` 原生支持 macOS 系统菜单栏：在 `App.axaml` 声明 `<NativeMenu.Menu>`，内容即应用菜单。M5 写一份 `AppMenu.axaml` 挂上去：

```xml
<NativeMenu>
  <NativeMenuItem Header="关于天命" Command="{Binding AboutCommand}"/>
  <NativeMenuItemSeparator/>
  <NativeMenuItem Header="偏好..." Gesture="Cmd+Comma" Command="{Binding OpenSettingsCommand}"/>
  <!-- 文件 / 编辑 / 视图 / 窗口 -->
</NativeMenu>
```

不写 P/Invoke，Avalonia 自己处理。

## 3. 工作拆分

串行 4 步：

1. **Step 1 — ObjC runtime 胶水**（~2 小时）：`ObjCRuntime.cs` + `Foundation.cs` 最小 bridge（NSString ↔ string、NSDictionary 读 key）+ 1 个 round-trip 单测。Commit `feat(platform): ObjC runtime 最小胶水`。
2. **Step 2 — Keychain**（~3 小时）：`Security.cs` P/Invoke + `MacOSKeychainApiKeySecretStore` + 增删改查手试（密码在"钥匙串访问"能看到一条 `com.tianming.apikey.*`）。改 M4.6 的 `ApiKeysPage` 走真 Keychain 不是明文 JSON。Commit。
3. **Step 3 — 系统代理**（~2 小时）：`SystemConfiguration.cs` + `MacOSSystemProxyService` + `HttpClient` 接线。测试：开 Clash → AI 请求走代理。Commit。
4. **Step 4 — 主题跟随 + 应用菜单**（~2 小时）：`MacOSSystemAppearanceMonitor` + `AppMenu.axaml` 挂到 `App.axaml`。测试：系统偏好切深/浅 → 应用即时跟随；⌘Q ⌘N ⌘, 快捷键可用。Commit。

## 4. 测试

- Keychain：1-2 单测（fake `Security` 接口），真调用手试
- 代理：`ProxyPolicyParser` 纯数据单测（给 dict 返回 policy），`SCDynamicStoreCopyProxies` 手试
- 主题：事件订阅逻辑单测（mock observer），真切换手试

## 5. 风险

| ID | 风险 | 缓解 |
|---|---|---|
| R1 | P/Invoke 调用约定出错 → crash | 每次 NSObject 用完就 release；SEGFAULT 时先查 selector 与参数类型；先跑简单 `NSString` round-trip 确认胶水对 |
| R2 | Keychain 首次授权弹窗 | 点"始终允许"即可；后续无感 |
| R3 | macOS 主题切换事件不触发 | 检查 Foundation NSDistributedNotificationCenter 监听的 notification name；fallback 到 M1 已端口的 `defaults read` 轮询 |

## 6. 验收

1. `dotnet build Tianming.MacMigration.sln` → 0 Error
2. `dotnet test Tianming.MacMigration.sln` → 全过
3. 在 M4.6 的 `ApiKeysPage` 填入 API Key → 关闭应用重启 → Key 自动从 Keychain 读回（在"钥匙串访问"里能看到条目）
4. 开系统代理（如 Clash）→ AI 请求能走代理（可用 Charles 或 tcpdump 抓到）
5. 系统偏好切换深/浅色 → 应用即时跟随
6. macOS 顶部系统菜单栏能看到"天命"菜单 + ⌘Q / ⌘, / ⌘N / ⌘S 快捷键

完成后进入 M6（v2.8.7 写作内核升级）。M5 结束时应用已经能在 macOS 自用写作，M6 是把 v2.8.7 的长篇稳定性、生成质量、Agent 动手能力补齐。
