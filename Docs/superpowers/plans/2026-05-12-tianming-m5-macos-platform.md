# 天命 macOS 迁移 — M5 macOS 平台能力 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

## 适用场景声明

**M5 目标：让作者本人在自己的 macOS 机器上能长期顺手使用天命。**

**M5 不覆盖的场景（给他人安装 / 对外分发）：**

若未来要把天命做成可分发的 macOS 应用（如发 TestFlight / Mac App Store / 自托管 DMG 给朋友装），以下能力**不在 M5，需要单独立项（暂定 M7 "macOS 分发"）**：

| 分发场景要求 | M5 是否做 | 未来在何处补 |
|---|---|---|
| URL Scheme（`tianming://...`）| 不做 | M7 |
| 文件关联（双击 `.tianming` 项目文件启动）| 不做 | M7 |
| 用户通知（章节生成完成弹通知）| 不做 | M7 |
| 开机自启 / 后台驻留 | 不做（自用不需要） | M7 |
| Entitlements 声明（网络 / 文件访问 / Keychain 权限）| 不做 | M7 |
| Hardened Runtime（代码完整性 / JIT 禁用等）| 不做 | M7 |
| App Sandbox | 不做 | M7 |
| 代码签名 + Notarization（Gatekeeper 不拦截）| 不做 | M7 |
| DMG / pkg 打包 / 自动更新 | 不做 | M7 |
| 全局快捷键 / 状态栏托盘 / 语音输入等附加能力 | 不做（spec 已剔除） | 视需要再加 |

**为什么现在不做：** 自用场景下，从 Xcode 命令行 `dotnet run --project src/Tianming.Desktop.Avalonia` 启动即可；绕 Gatekeeper 的方式（右键打开、自签）对单机单用户毫无摩擦。分发场景才需要付出这些复杂度的代价，而"给别人用"不是当前目标。先把 M4 闭环做扎实，真要分发时再做 M7。

**Goal:** 补齐 M4 已假设但实际缺失的两项 macOS 能力——系统代理读取（AI 请求走本机代理）与应用主菜单（⌘Q / ⌘N / ⌘, / ⌘S 等系统快捷键），并把 `WindowStateStore` / `AppPaths` 等 M3 建立的基础设施做最终打磨。Keychain 与系统外观监听在 M1 已完整（shell 实现），自用场景保留现状。

**Architecture:** 从 `SystemConfiguration.framework` 通过 `scutil --proxy` shell 命令读系统代理字典（不做 P/Invoke，保持"能 shell 就 shell"的决策），解析为 `ProxyPolicy`；`HttpClient` 组装 `SocketsHttpHandler { Proxy = AvaloniaSystemHttpProxy }`，`AI` 命名空间下所有出站 HTTP 都走这个代理。应用主菜单走 Avalonia 11.x 内置的 `NativeMenu` xaml，不写 P/Invoke——Avalonia 自动转换成 macOS 系统菜单栏（AppKit 负责渲染）。

**Tech Stack:** .NET 8.0 / Avalonia 11.x（`NativeMenu` / `NativeMenuItem` 已内置）/ xUnit / `/usr/sbin/scutil` shell 命令。

**Spec:** `Docs/superpowers/specs/2026-05-12-tianming-m5-macos-platform-design.md`（位于分支 `m2-m6/specs-2026-05-12`）。

## Scope Alignment（仓库真实状态）

> 视觉规格：参考 Mac_UI/images/10-macos-preferences-platform.png + Mac_UI/pseudocode/10-macos-preferences-platform.md（M5 主要做系统代理 + NativeMenu 命令绑定，UI 表面延续 M3 chrome/status bar）。

核对过的关键事实：

1. **`MacOSKeychainApiKeySecretStore` 已是完整 shell 实现**（`src/Tianming.AI/Core/ApiKeySecretStore.cs`），通过 `/usr/bin/security` 做 `find-generic-password / add-generic-password -U / delete-generic-password`，service name `"tianming-novel-ai-writer"`，非 stub。**自用场景跳过 P/Invoke 升级**——shell 已足够快、正确、授权流程直观。
2. **`MacOSSystemAppearanceMonitor` 已是完整 polling 实现**（`src/Tianming.Framework/Appearance/MacOSSystemAppearanceMonitor.cs`），`IPortableTimerTickSource` 驱动，3 秒轮询 `/usr/bin/defaults read -g AppleInterfaceStyle`。**自用场景跳过事件订阅升级**——3 秒延迟可接受；`NSDistributedNotificationCenter` P/Invoke 复杂度不划算。
3. **`ISystemProxyService` 不存在**，M5 从零创建。走 shell `scutil --proxy`（输出是 plist 格式字典，用 regex 解析关键字段），封装为 `IPortableSystemProxyService` 接口。
4. **`NSStatusBar` 托盘 / URL Scheme / 文件关联 / 开机自启 / 全局快捷键 / 通知 / 通知声 / 语音** — spec 已声明一律不做。
5. **应用主菜单** — Avalonia 11.x 内置 `NativeMenu` 机制（`App.axaml` 里声明 `<NativeMenu.Menu>`），macOS 下自动挂到系统菜单栏顶部。**无需 P/Invoke**。

## 实做范围

- **A. 系统代理服务**（`IPortableSystemProxyService` 接口 + `MacOSSystemProxyService` shell 实现 + `ProxyPolicy` 记录 + `AvaloniaSystemHttpProxy` 类 + `HttpClient` 装配集成）
- **B. 应用主菜单**（`App.axaml` 里 `<NativeMenu.Menu>` + MainWindowViewModel 命令 + 单测）

## File Structure

**新建文件：**
- `src/Tianming.Framework/Platform/IPortableSystemProxyService.cs` — 接口
- `src/Tianming.Framework/Platform/ProxyPolicy.cs` — record
- `src/Tianming.Framework/Platform/ScutilProxyOutputParser.cs` — 解析器（portable 可单测）
- `src/Tianming.Framework/Platform/IScutilCommandRunner.cs` — shell 抽象
- `src/Tianming.Framework/Platform/ProcessScutilCommandRunner.cs` — 默认实现
- `src/Tianming.Framework/Platform/MacOSSystemProxyService.cs` — 实现
- `src/Tianming.Desktop.Avalonia/Infrastructure/AvaloniaSystemHttpProxy.cs` — `IWebProxy` 包装
- `tests/Tianming.Framework.Tests/Platform/ScutilProxyOutputParserTests.cs`
- `tests/Tianming.Framework.Tests/Platform/MacOSSystemProxyServiceTests.cs`

**修改文件：**
- `src/Tianming.Framework/Tianming.Framework.csproj`（若仍显式 Compile Include）— 加 4 条
- `src/Tianming.Framework/ServiceCollectionExtensions.cs`（M3 建立）— 注册 proxy service
- `src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs`（M3 建立）— 注册 `AvaloniaSystemHttpProxy` 并装到默认 `HttpClient`
- `src/Tianming.AI/Core/OpenAICompatibleChatClient.cs` 的 `HttpClient` 来源改从 DI（若尚未接 DI）
- `src/Tianming.Desktop.Avalonia/App.axaml` — 加 `<NativeMenu.Menu>`
- `src/Tianming.Desktop.Avalonia/ViewModels/MainWindowViewModel.cs` — 加 About / OpenSettings / NewProject / OpenProject / Save / Quit 命令

---

## Task 0：基线确认

> Round 3 replay note (2026-05-14): 本轮按最新派单改用 `/Users/jimmy/Downloads/tianming-m5`
> worktree，而不是旧的 `.worktrees/m2-parallel` 路径。基线已复跑：
> `git log main --oneline | head -5` 看到 `155aeaf / e69528d / 1e2494f / ff69853`；
> `dotnet test Tianming.MacMigration.sln --nologo -v q` 全过（1423 tests）。
> 当前机器 `scutil --proxy` 为代理开启态，实测 HTTP/HTTPS 走 `127.0.0.1:1082`，
> 输出已记录到 `/tmp/scutil-sample-on.txt`；未主动改动宿主机代理配置，因此未采集 live off
> snapshot，沿用 Task 1 的 off fixture 覆盖“代理关闭”解析场景。

- [ ] **Step 0.1：确认 M3 已完成并合入**

```bash
cd /Users/jimmy/Downloads/tianming-novel-ai-writer/.worktrees/m2-parallel
git branch --show-current
```

Expected: 在 M5 专用 worktree 的实施分支（如 `m5/platform-capabilities-2026-0x-xx`），该分支已基于 M4 合入后的 main。若仍在 `m2/onnx-localfetch-2026-05-12`，停下先开 M5 worktree。

- [ ] **Step 0.2：跑基线测试**

```bash
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -8
```

Expected: 全过（具体数字取决于 M3/M4 进度）。

- [ ] **Step 0.3：手动探测 scutil 输出格式**

```bash
scutil --proxy
```

Expected 看到类似（以你当前机器有无代理而异）：
```
<dictionary> {
  ExceptionsList : <array> {
    0 : *.local
    1 : 169.254/16
  }
  FTPPassive : 1
  HTTPEnable : 0
  HTTPSEnable : 0
  SOCKSEnable : 0
  ...
}
```

若开着 Clash / ClashX / Surge 等代理，关键字段会变 1 并带 `HTTPProxy` / `HTTPPort` / `HTTPSProxy` / `HTTPSPort`。

记录一份当前输出到 `/tmp/scutil-sample-off.txt`（代理关）和 `/tmp/scutil-sample-on.txt`（代理开），作为 Task 1 测试 fixture。

---

## Task 1：ProxyPolicy 记录 + ScutilProxyOutputParser（纯数据，TDD）

> Round 3 replay note (2026-05-14): 该任务已在 `main` 实装，提交为 `2aaaa0a`
> (`feat(platform): ProxyPolicy + ScutilProxyOutputParser`)。因此本轮不重写实现，只复跑
> 验证并保留结果：Step 1.3/1.6 的 parser 测试当前直接通过（7 tests），不再出现计划中
> 预期的“编译错误”；`Tianming.Framework.csproj` 未显式关闭 default compile items，
> 所以 Step 1.5 继续跳过。

**Files:**
- Create: `src/Tianming.Framework/Platform/ProxyPolicy.cs`
- Create: `src/Tianming.Framework/Platform/ScutilProxyOutputParser.cs`
- Create: `tests/Tianming.Framework.Tests/Platform/ScutilProxyOutputParserTests.cs`
- Modify: `src/Tianming.Framework/Tianming.Framework.csproj`

- [ ] **Step 1.1：写 ProxyPolicy record**

创建 `src/Tianming.Framework/Platform/ProxyPolicy.cs`：

```csharp
using System;
using System.Collections.Generic;

namespace TM.Framework.Platform;

public sealed record ProxyPolicy(
    Uri? HttpProxy,
    Uri? HttpsProxy,
    IReadOnlyList<string> Exceptions)
{
    public static ProxyPolicy Direct { get; } = new(null, null, Array.Empty<string>());

    public bool HasProxy => HttpProxy is not null || HttpsProxy is not null;

    public Uri? ResolveFor(Uri target) => target.Scheme.ToLowerInvariant() switch
    {
        "https" => HttpsProxy ?? HttpProxy,
        "http"  => HttpProxy,
        _       => null
    };

    public bool ShouldBypass(Uri target)
    {
        foreach (var rule in Exceptions)
            if (MatchesBypass(target.Host, rule)) return true;
        return false;
    }

    private static bool MatchesBypass(string host, string rule)
    {
        // 常见形式："*.local"、"169.254/16"、"localhost"
        if (rule.Equals(host, StringComparison.OrdinalIgnoreCase)) return true;
        if (rule.StartsWith("*.") && host.EndsWith(rule[1..], StringComparison.OrdinalIgnoreCase)) return true;
        if (rule.Contains('/'))
        {
            // CIDR 不精确处理，只做前缀匹配（自用够）
            var prefix = rule.Split('/')[0];
            return host.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }
}
```

- [ ] **Step 1.2：写失败的 parser 测试**

创建 `tests/Tianming.Framework.Tests/Platform/ScutilProxyOutputParserTests.cs`：

```csharp
using System;
using TM.Framework.Platform;
using Xunit;

namespace Tianming.Framework.Tests.Platform;

public class ScutilProxyOutputParserTests
{
    [Fact]
    public void Parse_EmptyOutput_ReturnsDirect()
    {
        var r = ScutilProxyOutputParser.Parse("");
        Assert.Same(ProxyPolicy.Direct, r);
    }

    [Fact]
    public void Parse_ProxyOff_ReturnsDirect()
    {
        var output = """
            <dictionary> {
              ExceptionsList : <array> {
                0 : *.local
              }
              HTTPEnable : 0
              HTTPSEnable : 0
              SOCKSEnable : 0
            }
            """;
        var r = ScutilProxyOutputParser.Parse(output);
        Assert.False(r.HasProxy);
        Assert.Contains("*.local", r.Exceptions);
    }

    [Fact]
    public void Parse_HttpProxyOn_ExtractsUri()
    {
        var output = """
            <dictionary> {
              ExceptionsList : <array> { 0 : localhost }
              HTTPEnable : 1
              HTTPPort : 7890
              HTTPProxy : 127.0.0.1
              HTTPSEnable : 0
            }
            """;
        var r = ScutilProxyOutputParser.Parse(output);
        Assert.Equal(new Uri("http://127.0.0.1:7890"), r.HttpProxy);
        Assert.Null(r.HttpsProxy);
    }

    [Fact]
    public void Parse_BothHttpAndHttps_ExtractsBoth()
    {
        var output = """
            <dictionary> {
              HTTPEnable : 1
              HTTPPort : 7890
              HTTPProxy : 127.0.0.1
              HTTPSEnable : 1
              HTTPSPort : 7890
              HTTPSProxy : 127.0.0.1
            }
            """;
        var r = ScutilProxyOutputParser.Parse(output);
        Assert.Equal(new Uri("http://127.0.0.1:7890"), r.HttpProxy);
        Assert.Equal(new Uri("http://127.0.0.1:7890"), r.HttpsProxy);
    }

    [Fact]
    public void Parse_EnabledButMissingProxyField_TreatedAsDirect()
    {
        var output = """
            <dictionary> {
              HTTPEnable : 1
              HTTPSEnable : 0
            }
            """;
        var r = ScutilProxyOutputParser.Parse(output);
        Assert.False(r.HasProxy);
    }
}
```

- [ ] **Step 1.3：跑测试确认失败**

```bash
dotnet test tests/Tianming.Framework.Tests/ --nologo --filter "ScutilProxyOutputParser" -v q 2>&1 | tail -4
```

Expected: 编译错误（`ScutilProxyOutputParser` 不存在）。

- [ ] **Step 1.4：写最小实现**

创建 `src/Tianming.Framework/Platform/ScutilProxyOutputParser.cs`：

```csharp
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TM.Framework.Platform;

public static class ScutilProxyOutputParser
{
    private static readonly Regex KeyValue = new(@"^\s*([A-Za-z]+)\s*:\s*(.+?)\s*$", RegexOptions.Compiled);
    private static readonly Regex ArrayItem = new(@"^\s*\d+\s*:\s*(.+?)\s*$", RegexOptions.Compiled);

    public static ProxyPolicy Parse(string scutilOutput)
    {
        if (string.IsNullOrWhiteSpace(scutilOutput))
            return ProxyPolicy.Direct;

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var exceptions = new List<string>();
        var inExceptions = false;

        foreach (var raw in scutilOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.TrimEnd('\r');
            if (line.Contains("ExceptionsList : <array>")) { inExceptions = true; continue; }
            if (inExceptions)
            {
                if (line.Contains('}')) { inExceptions = false; continue; }
                var am = ArrayItem.Match(line);
                if (am.Success) exceptions.Add(am.Groups[1].Value);
                continue;
            }
            var m = KeyValue.Match(line);
            if (!m.Success) continue;
            values[m.Groups[1].Value] = m.Groups[2].Value;
        }

        Uri? http = BuildUri(values, "HTTPEnable", "HTTPProxy", "HTTPPort");
        Uri? https = BuildUri(values, "HTTPSEnable", "HTTPSProxy", "HTTPSPort");
        return new ProxyPolicy(http, https, exceptions);
    }

    private static Uri? BuildUri(IReadOnlyDictionary<string, string> v, string enableKey, string hostKey, string portKey)
    {
        if (!v.TryGetValue(enableKey, out var enable) || enable != "1") return null;
        if (!v.TryGetValue(hostKey, out var host) || string.IsNullOrWhiteSpace(host)) return null;
        if (!v.TryGetValue(portKey, out var port) || !int.TryParse(port, out var p)) return null;
        return new Uri($"http://{host}:{p}");
    }
}
```

- [ ] **Step 1.5：在 csproj 加 Compile Include（若 Framework 用显式模式）**

检查 `src/Tianming.Framework/Tianming.Framework.csproj` 是否含 `<EnableDefaultCompileItems>false</EnableDefaultCompileItems>`：

```bash
grep "EnableDefaultCompileItems" src/Tianming.Framework/Tianming.Framework.csproj
```

若是，加：

```xml
    <Compile Include="Platform/ProxyPolicy.cs" />
    <Compile Include="Platform/ScutilProxyOutputParser.cs" />
```

否则（用 glob 模式）跳过本步。

- [ ] **Step 1.6：跑测试确认通过**

```bash
dotnet test tests/Tianming.Framework.Tests/ --nologo --filter "ScutilProxyOutputParser" -v q 2>&1 | tail -4
```

Expected: 5 个测试全过。

- [ ] **Step 1.7：跑全量测试未退化**

```bash
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -6
```

Expected: 基线 + 5 个新测试全过。

- [ ] **Step 1.8：commit**

```bash
git add src/Tianming.Framework/Platform/ProxyPolicy.cs \
        src/Tianming.Framework/Platform/ScutilProxyOutputParser.cs \
        src/Tianming.Framework/Tianming.Framework.csproj \
        tests/Tianming.Framework.Tests/Platform/ScutilProxyOutputParserTests.cs
git commit -m "feat(platform): ProxyPolicy + ScutilProxyOutputParser"
```

---

## Task 2：MacOSSystemProxyService（shell 调用 scutil --proxy）

> Round 3 replay note (2026-05-14): 该任务已在 `main` 实装，提交为 `57319a6`
> (`feat(platform): MacOSSystemProxyService（shell scutil --proxy）`)。因此本轮不重写
> 接口/实现，只复跑验证：Step 2.3/2.6 的测试当前直接通过（3 tests）。Step 2.8 的真实
> `scutil --proxy` smoke 复跑结果为 `HTTPEnable=1 / HTTPProxy=127.0.0.1 / HTTPPort=1082 /
> HTTPSEnable=1 / HTTPSProxy=127.0.0.1 / HTTPSPort=1082`，Exceptions 11 项。

**Files:**
- Create: `src/Tianming.Framework/Platform/IPortableSystemProxyService.cs`
- Create: `src/Tianming.Framework/Platform/IScutilCommandRunner.cs`
- Create: `src/Tianming.Framework/Platform/ProcessScutilCommandRunner.cs`
- Create: `src/Tianming.Framework/Platform/MacOSSystemProxyService.cs`
- Create: `tests/Tianming.Framework.Tests/Platform/MacOSSystemProxyServiceTests.cs`
- Modify: `src/Tianming.Framework/Tianming.Framework.csproj`

- [ ] **Step 2.1：写接口**

创建 `src/Tianming.Framework/Platform/IPortableSystemProxyService.cs`：

```csharp
namespace TM.Framework.Platform;

public interface IPortableSystemProxyService
{
    ProxyPolicy GetCurrent();
}
```

创建 `src/Tianming.Framework/Platform/IScutilCommandRunner.cs`：

```csharp
namespace TM.Framework.Platform;

public interface IScutilCommandRunner
{
    string Run();
}
```

- [ ] **Step 2.2：写 MacOSSystemProxyService 失败测试**

创建 `tests/Tianming.Framework.Tests/Platform/MacOSSystemProxyServiceTests.cs`：

```csharp
using TM.Framework.Platform;
using Xunit;

namespace Tianming.Framework.Tests.Platform;

public class MacOSSystemProxyServiceTests
{
    [Fact]
    public void GetCurrent_ProxyOff_ReturnsDirect()
    {
        var runner = new FakeScutilRunner("""
            <dictionary> {
              HTTPEnable : 0
              HTTPSEnable : 0
            }
            """);
        var svc = new MacOSSystemProxyService(runner);
        var p = svc.GetCurrent();
        Assert.False(p.HasProxy);
    }

    [Fact]
    public void GetCurrent_ProxyOn_ReturnsPolicy()
    {
        var runner = new FakeScutilRunner("""
            <dictionary> {
              HTTPEnable : 1
              HTTPPort : 7890
              HTTPProxy : 127.0.0.1
            }
            """);
        var svc = new MacOSSystemProxyService(runner);
        var p = svc.GetCurrent();
        Assert.True(p.HasProxy);
        Assert.Equal("http://127.0.0.1:7890/", p.HttpProxy!.ToString());
    }

    [Fact]
    public void GetCurrent_RunnerThrows_ReturnsDirect()
    {
        var runner = new ThrowingScutilRunner();
        var svc = new MacOSSystemProxyService(runner);
        var p = svc.GetCurrent();
        Assert.Same(ProxyPolicy.Direct, p);
    }

    private sealed class FakeScutilRunner : IScutilCommandRunner
    {
        private readonly string _out;
        public FakeScutilRunner(string o) => _out = o;
        public string Run() => _out;
    }

    private sealed class ThrowingScutilRunner : IScutilCommandRunner
    {
        public string Run() => throw new System.InvalidOperationException("scutil failed");
    }
}
```

- [ ] **Step 2.3：跑测试确认失败**

```bash
dotnet test tests/Tianming.Framework.Tests/ --nologo --filter "MacOSSystemProxyService" -v q 2>&1 | tail -4
```

Expected: 编译错误。

- [ ] **Step 2.4：写实现**

创建 `src/Tianming.Framework/Platform/MacOSSystemProxyService.cs`：

```csharp
using System;

namespace TM.Framework.Platform;

public sealed class MacOSSystemProxyService : IPortableSystemProxyService
{
    private readonly IScutilCommandRunner _runner;

    public MacOSSystemProxyService() : this(new ProcessScutilCommandRunner()) { }

    public MacOSSystemProxyService(IScutilCommandRunner runner)
    {
        _runner = runner;
    }

    public ProxyPolicy GetCurrent()
    {
        try
        {
            var output = _runner.Run();
            return ScutilProxyOutputParser.Parse(output);
        }
        catch
        {
            return ProxyPolicy.Direct;
        }
    }
}
```

创建 `src/Tianming.Framework/Platform/ProcessScutilCommandRunner.cs`：

```csharp
using System;
using System.Diagnostics;

namespace TM.Framework.Platform;

public sealed class ProcessScutilCommandRunner : IScutilCommandRunner
{
    public string Run()
    {
        var psi = new ProcessStartInfo("/usr/sbin/scutil", "--proxy")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start /usr/sbin/scutil");
        var stdout = p.StandardOutput.ReadToEnd();
        p.WaitForExit(5000);
        return stdout;
    }
}
```

- [ ] **Step 2.5：在 csproj 加 Compile Include（若需）**

```xml
    <Compile Include="Platform/IPortableSystemProxyService.cs" />
    <Compile Include="Platform/IScutilCommandRunner.cs" />
    <Compile Include="Platform/ProcessScutilCommandRunner.cs" />
    <Compile Include="Platform/MacOSSystemProxyService.cs" />
```

- [ ] **Step 2.6：跑测试确认通过**

```bash
dotnet test tests/Tianming.Framework.Tests/ --nologo --filter "MacOSSystemProxyService" -v q 2>&1 | tail -4
```

Expected: 3 个测试全过。

- [ ] **Step 2.7：全量测试**

```bash
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -6
```

Expected: 基线 + 8 个新测试全过。

- [ ] **Step 2.8：手工验证真 scutil 调用**

写一个 10 行小程序验证：

```bash
cat > /tmp/proxy-smoke.cs <<'EOF'
using TM.Framework.Platform;
var svc = new MacOSSystemProxyService();
var p = svc.GetCurrent();
System.Console.WriteLine($"HasProxy={p.HasProxy}");
System.Console.WriteLine($"HTTP={p.HttpProxy}");
System.Console.WriteLine($"HTTPS={p.HttpsProxy}");
System.Console.WriteLine($"Exceptions={string.Join(",", p.Exceptions)}");
EOF
dotnet script /tmp/proxy-smoke.cs
```

若未装 `dotnet-script`：`dotnet tool install -g dotnet-script`。

Expected: 输出反映你当前机器的实际代理配置（开 / 关 Clash 测两次看差异）。

- [ ] **Step 2.9：commit**

```bash
git add src/Tianming.Framework/Platform/IPortableSystemProxyService.cs \
        src/Tianming.Framework/Platform/IScutilCommandRunner.cs \
        src/Tianming.Framework/Platform/ProcessScutilCommandRunner.cs \
        src/Tianming.Framework/Platform/MacOSSystemProxyService.cs \
        src/Tianming.Framework/Tianming.Framework.csproj \
        tests/Tianming.Framework.Tests/Platform/MacOSSystemProxyServiceTests.cs
git commit -m "feat(platform): MacOSSystemProxyService（shell scutil --proxy）"
```

---

## Task 3：AvaloniaSystemHttpProxy + HttpClient 装配

> Round 3 replay note (2026-05-14): 该任务已在 `main` 实装，提交为 `f46dcb8`
> (`feat(platform): HttpClient 走系统代理（AvaloniaSystemHttpProxy）`)。`AvaloniaSystemHttpProxy`
> 已存在，`AddFrameworkServices()` 已注册 `IPortableSystemProxyService`，`AddAvaloniaShell()`
> 已注册 named client `"tianming"` + singleton `HttpClient`，`Tianming.Desktop.Avalonia.csproj`
> 也已引用 `Microsoft.Extensions.Http`。因此本轮不重复改代码，只复跑代理单测与后续全量
> build/test；计划中的手工代理链路验收继续保留到 Task 5 清单。

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Infrastructure/AvaloniaSystemHttpProxy.cs`
- Modify: `src/Tianming.Framework/ServiceCollectionExtensions.cs`（注册 proxy service）
- Modify: `src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs`（装 `HttpClient`）

- [ ] **Step 3.1：写 AvaloniaSystemHttpProxy**

`IWebProxy` 适配器，每次请求查 `IPortableSystemProxyService`：

创建 `src/Tianming.Desktop.Avalonia/Infrastructure/AvaloniaSystemHttpProxy.cs`：

```csharp
using System;
using System.Net;
using TM.Framework.Platform;

namespace Tianming.Desktop.Avalonia.Infrastructure;

public sealed class AvaloniaSystemHttpProxy : IWebProxy
{
    private readonly IPortableSystemProxyService _proxy;

    public AvaloniaSystemHttpProxy(IPortableSystemProxyService proxy)
    {
        _proxy = proxy;
    }

    public ICredentials? Credentials { get; set; }

    public Uri? GetProxy(Uri destination)
    {
        var policy = _proxy.GetCurrent();
        if (policy.ShouldBypass(destination)) return null;
        return policy.ResolveFor(destination);
    }

    public bool IsBypassed(Uri host)
        => _proxy.GetCurrent().ShouldBypass(host);
}
```

- [ ] **Step 3.2：注册到 Framework ServiceCollection**

编辑 `src/Tianming.Framework/ServiceCollectionExtensions.cs`（M3 已建立，内容在 M3 plan Task 5），在 `AddFrameworkServices` 方法里新增一行：

```csharp
services.AddSingleton<IPortableSystemProxyService, MacOSSystemProxyService>();
```

- [ ] **Step 3.3：装 HttpClient**

编辑 `src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs`（M3 已建立），在 `AddAvaloniaShell` 方法里新增：

```csharp
services.AddSingleton<AvaloniaSystemHttpProxy>();
services.AddHttpClient("tianming")
        .ConfigurePrimaryHttpMessageHandler(sp => new System.Net.Http.SocketsHttpHandler
        {
            Proxy = sp.GetRequiredService<AvaloniaSystemHttpProxy>(),
            UseProxy = true,
        });
services.AddSingleton<System.Net.Http.HttpClient>(sp =>
    sp.GetRequiredService<System.Net.Http.IHttpClientFactory>().CreateClient("tianming"));
```

csproj 加 `<PackageReference Include="Microsoft.Extensions.Http" Version="8.0.*" />` 若尚未引入。

- [ ] **Step 3.4：build + test**

```bash
dotnet build Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -4
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -6
```

Expected: 编译过、测试全过。

- [ ] **Step 3.5：手工验证代理链路**

步骤：
1. 在 macOS 系统偏好 → 网络 → 选当前接口 → 高级 → 代理，配置 HTTP/HTTPS 代理为 `127.0.0.1:7890`（或你实际 Clash 端口）
2. 启动天命（`dotnet run --project src/Tianming.Desktop.Avalonia`）
3. 用 Clash 客户端或 `mitmproxy` 观察是否有来自天命进程的出站 HTTP 经过
4. 关掉系统代理开关（保留端口配置），再触发一次 AI 请求，确认走直连

此步手工验收，过或不过都记录到 `Docs/macOS迁移/M5-人工验收.md`（文件在 Task 5 创建）。

- [ ] **Step 3.6：commit**

```bash
git add src/Tianming.Desktop.Avalonia/Infrastructure/AvaloniaSystemHttpProxy.cs \
        src/Tianming.Framework/ServiceCollectionExtensions.cs \
        src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs \
        src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj
git commit -m "feat(platform): HttpClient 走系统代理（AvaloniaSystemHttpProxy）"
```

---

## Task 4：应用主菜单（NativeMenu）

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml`（加 `<NativeMenu.Menu>`）
- Modify: `src/Tianming.Desktop.Avalonia/ViewModels/MainWindowViewModel.cs`（加命令）
- Create: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/MainWindowViewModelMenuTests.cs`

- [ ] **Step 4.1：扩展 MainWindowViewModel 加命令**

编辑 `src/Tianming.Desktop.Avalonia/ViewModels/MainWindowViewModel.cs`，加以下 `[RelayCommand]` 方法（CommunityToolkit.Mvvm 源生成器自动生成 `AboutCommand` 等属性）：

```csharp
using CommunityToolkit.Mvvm.Input;

public partial class MainWindowViewModel : ObservableObject
{
    // ... 既有成员

    [RelayCommand]
    private void About()
    {
        // 先简单：日志输出或 Toast；M4 AI 管理页可挂"关于"对话框
        System.Console.WriteLine("天命 macOS 迁移版 v0.0.1-dev");
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task OpenSettingsAsync()
    {
        await _navigation.NavigateAsync(PageKeys.Settings);
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task NewProjectAsync()
    {
        await _navigation.NavigateAsync(PageKeys.Welcome);
        // 或直接触发 WelcomeViewModel.NewProjectCommand
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task OpenProjectAsync()
    {
        await _navigation.NavigateAsync(PageKeys.Welcome);
    }

    [RelayCommand]
    private void Save()
    {
        // M4 编辑器接入前留空 stub；编辑器实装时接入
    }

    [RelayCommand]
    private void Quit()
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime
            is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
```

- [ ] **Step 4.2：App.axaml 加 NativeMenu**

编辑 `src/Tianming.Desktop.Avalonia/App.axaml`（M3 已建立），在 `<Application>` 根元素内加：

```xml
<NativeMenu.Menu>
  <NativeMenu>
    <NativeMenuItem Header="天命">
      <NativeMenuItem.Menu>
        <NativeMenu>
          <NativeMenuItem Header="关于天命"
                          Command="{Binding MainWindow.DataContext.AboutCommand, Source={x:Static Application.Current}}"/>
          <NativeMenuItemSeparator/>
          <NativeMenuItem Header="偏好..."
                          Gesture="Cmd+OemComma"
                          Command="{Binding MainWindow.DataContext.OpenSettingsCommand, Source={x:Static Application.Current}}"/>
          <NativeMenuItemSeparator/>
          <NativeMenuItem Header="退出"
                          Gesture="Cmd+Q"
                          Command="{Binding MainWindow.DataContext.QuitCommand, Source={x:Static Application.Current}}"/>
        </NativeMenu>
      </NativeMenuItem.Menu>
    </NativeMenuItem>
    <NativeMenuItem Header="文件">
      <NativeMenuItem.Menu>
        <NativeMenu>
          <NativeMenuItem Header="新建项目"
                          Gesture="Cmd+N"
                          Command="{Binding MainWindow.DataContext.NewProjectCommand, Source={x:Static Application.Current}}"/>
          <NativeMenuItem Header="打开项目..."
                          Gesture="Cmd+O"
                          Command="{Binding MainWindow.DataContext.OpenProjectCommand, Source={x:Static Application.Current}}"/>
          <NativeMenuItemSeparator/>
          <NativeMenuItem Header="保存"
                          Gesture="Cmd+S"
                          Command="{Binding MainWindow.DataContext.SaveCommand, Source={x:Static Application.Current}}"/>
        </NativeMenu>
      </NativeMenuItem.Menu>
    </NativeMenuItem>
  </NativeMenu>
</NativeMenu.Menu>
```

注意：上面绑定语法依赖 `Application.Current.MainWindow.DataContext` 是 `MainWindowViewModel`。若实际绑定上下文不同（M3 plan 里 `MainWindow.DataContext = MainWindowViewModel`），按实际调。在开发中最快的验证方式是：build 后 `dotnet run`，命令触发不了就换绑定路径（常见替代：`{StaticResource MainWindowViewModel}` 若 DI 把 VM 注册为资源）。

- [ ] **Step 4.3：写 VM 命令单测**

创建 `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/MainWindowViewModelMenuTests.cs`：

```csharp
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.ViewModels;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels;

public class MainWindowViewModelMenuTests
{
    [Fact]
    public async System.Threading.Tasks.Task OpenSettings_NavigatesToSettingsPage()
    {
        var nav = new FakeNavigationService();
        var vm = new MainWindowViewModel(nav /* 及其他 M3 构造器参数 */);
        await vm.OpenSettingsCommand.ExecuteAsync(null);
        Assert.Equal(PageKeys.Settings, nav.LastNavigatedKey);
    }

    [Fact]
    public async System.Threading.Tasks.Task NewProject_NavigatesToWelcome()
    {
        var nav = new FakeNavigationService();
        var vm = new MainWindowViewModel(nav);
        await vm.NewProjectCommand.ExecuteAsync(null);
        Assert.Equal(PageKeys.Welcome, nav.LastNavigatedKey);
    }

    private sealed class FakeNavigationService : INavigationService
    {
        public PageKey? LastNavigatedKey { get; private set; }
        public PageKey? CurrentKey => LastNavigatedKey;
        public bool CanGoBack => false;
        public System.Threading.Tasks.Task NavigateAsync(PageKey key, object? p = null)
        {
            LastNavigatedKey = key;
            return System.Threading.Tasks.Task.CompletedTask;
        }
        public System.Threading.Tasks.Task GoBackAsync()
            => System.Threading.Tasks.Task.CompletedTask;
    }
}
```

注意：`FakeNavigationService` 若 M3 plan 的测试里已存在同名 fake，优先复用（提到 TestSupport 命名空间）。

- [ ] **Step 4.4：build + test**

```bash
dotnet build Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -4
dotnet test Tianming.MacMigration.sln --nologo --filter "MainWindowViewModelMenu" -v q 2>&1 | tail -4
```

Expected: 编译过、2 个菜单命令测试过。

- [ ] **Step 4.5：手工验证菜单栏**

```bash
dotnet run --project src/Tianming.Desktop.Avalonia
```

验收项：
- macOS 顶部系统菜单栏看到"天命 / 文件"两个顶级项
- ⌘Q 退出应用
- ⌘, 跳设置页
- ⌘N 跳 Welcome
- ⌘O 跳 Welcome（M4 接入文件夹选择后改）

- [ ] **Step 4.6：全量测试**

```bash
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -6
```

Expected: 全过。

- [ ] **Step 4.7：commit**

```bash
git add src/Tianming.Desktop.Avalonia/App.axaml \
        src/Tianming.Desktop.Avalonia/ViewModels/MainWindowViewModel.cs \
        tests/Tianming.Desktop.Avalonia.Tests/ViewModels/MainWindowViewModelMenuTests.cs
git commit -m "feat(shell): macOS 应用主菜单（NativeMenu + ⌘Q/⌘N/⌘O/⌘S/⌘,）"
```

---

## Task 5：M5 验收文档

**Files:**
- Create: `Docs/macOS迁移/M5-人工验收.md`

- [ ] **Step 5.1：写验收记录模板**

创建 `Docs/macOS迁移/M5-人工验收.md`：

```markdown
# 天命 macOS M5 人工验收记录

日期：<填>

## Keychain（M1 已完整，M5 沿用）

- [ ] 通过 M4 AI Keys 页填入一个 API Key
- [ ] 关闭应用重启，Key 自动从 Keychain 读回
- [ ] 打开 macOS "钥匙串访问" → 登录 → 查找 `tianming-novel-ai-writer` → 能看到条目

## 系统代理

- [ ] 关闭 macOS 系统代理开关 → 启动天命 → AI 请求直连（mitmproxy 不拦截）
- [ ] 打开 macOS 系统代理（127.0.0.1:7890 或你 Clash 端口）→ 重启天命 → AI 请求经代理（Clash 可见流量）
- [ ] 配置 `*.local` 在 Exceptions 里 → 访问 localhost 不走代理

## 系统外观跟随（M1 已完整 polling 实现，M5 沿用）

- [ ] 系统偏好切换 Light → Dark，3 秒内天命 UI 跟随
- [ ] Dark → Light，3 秒内跟随
- [ ] 冷启动时初始主题对齐当前系统

## 应用主菜单

- [ ] 菜单栏顶部看到"天命 / 文件"
- [ ] ⌘Q 退出应用
- [ ] ⌘, 跳设置页（M3 建立的 Settings stub）
- [ ] ⌘N / ⌘O 跳 Welcome（M4 后改为触发新建 / 打开对话框）
- [ ] ⌘S M4 编辑器实装后保存当前章节（M5 只是 stub）
```

- [ ] **Step 5.2：commit**

```bash
git add Docs/macOS迁移/M5-人工验收.md
git commit -m "docs(m5): 人工验收清单"
```

---

## Task 6：最终收尾

- [ ] **Step 6.1：全量测试**

```bash
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -6
```

Expected: 全过（M4 基线 + M5 新增 10 测试）。

- [ ] **Step 6.2：Windows 绑定扫描**

```bash
Scripts/check-windows-bindings.sh src/ tests/ 2>&1 | tail -5
```

Expected: 0 命中。

- [ ] **Step 6.3：commit 历史清点**

```bash
git log --oneline -10
```

Expected 看到 5 个新 commit（Task 1 / 2 / 3 / 4 / 5 各 1 个）。

- [ ] **Step 6.4：更新功能对齐矩阵**

编辑 `Docs/macOS迁移/功能对齐矩阵.md`，把"系统代理"、"应用主菜单"、"Keychain"、"系统外观跟随"四行状态改为"已端口 / 已完成"。commit：

```bash
git add Docs/macOS迁移/功能对齐矩阵.md
git commit -m "docs(matrix): M5 状态更新"
```

- [ ] **Step 6.5：push（按需）**

```bash
git push -u origin <m5-branch-name>
```

自用本地即可不 push。

---

## 测试策略汇总

- 单测：`ScutilProxyOutputParserTests`（5 例）+ `MacOSSystemProxyServiceTests`（3 例）+ `MainWindowViewModelMenuTests`（2 例）= 10 个新测试
- 手工：代理链路手试、系统菜单快捷键手试、Keychain 与外观跟随按既有实现手试（M5 不改动）
- 不写：P/Invoke 相关测试（无 P/Invoke 代码）

## 风险与缓解

| ID | 风险 | 缓解 |
|---|---|---|
| R1 | `scutil --proxy` 输出格式在 macOS 新版本变化 | 解析器容错：正则匹配不到就返回 Direct；失败记日志不抛 |
| R2 | `IWebProxy` 每请求调一次 `scutil` 慢（fork 进程 ~10ms） | 可选优化：`MacOSSystemProxyService` 内缓存 30 秒；M5 先不做，实际观察到卡顿再加 |
| R3 | Avalonia `NativeMenu` 绑定语法与我写的不一致 | 若绑定不触发，用 Avalonia Diagnostics 看绑定错误；最坏改为在 `App.axaml.cs` 代码里动态挂 `NativeMenu` 并直接绑 command 实例 |
| R4 | `Cmd+OemComma` 不是 Avalonia 约定键名 | 实测不对就换 `Cmd+,` 或用 Avalonia `KeyGesture` API 写 |
| R5 | 手动代理配置在 Control Panel 里没开但在 Network 里开了 | `scutil --proxy` 会正确反映，测试 fixture 已覆盖 |

## 验收标准

1. `dotnet build Tianming.MacMigration.sln` → 0 Warning / 0 Error
2. `dotnet test Tianming.MacMigration.sln` → 全过（M4 基线 + 10 新增）
3. `Scripts/check-windows-bindings.sh src/ tests/` → 0 命中
4. `Docs/macOS迁移/M5-人工验收.md` 四大能力全部打勾
5. 5 个清晰 commit 在分支上：
   - `feat(platform): ProxyPolicy + ScutilProxyOutputParser`
   - `feat(platform): MacOSSystemProxyService（shell scutil --proxy）`
   - `feat(platform): HttpClient 走系统代理（AvaloniaSystemHttpProxy）`
   - `feat(shell): macOS 应用主菜单（NativeMenu + ⌘Q/⌘N/⌘O/⌘S/⌘,）`
   - `docs(m5): 人工验收清单` + `docs(matrix): M5 状态更新`

**完成后……迁移就完成了，开写。**
