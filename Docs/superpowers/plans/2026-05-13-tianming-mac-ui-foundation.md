# 天命 macOS — Mac UI 视觉基建（Sub-plan 1）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把天命 macOS 端的视觉基建打到 pixel-level 参考图对齐的基础上：design tokens / 自定义 chrome / 底部状态栏 / NativeMenu stub / 14 个 primitives / 3 个 NuGet 整合 / Mac_UI 入仓。

**Architecture:** 一次性出全套基建（Approach 1）。token 分 5 个 axaml（Colors/Typography/Spacing/Radii/Shadows）；primitives 用 `TemplatedControl` + ControlTheme，Lucide icon string token；chrome 用 `ExtendClientAreaToDecorationsHint`；status bar 走 probe 异步填充。亮色 only（删 `PalettesDark.axaml`）。

**Tech Stack:** Avalonia 11.0.10 / CommunityToolkit.Mvvm 8.2.2 / xunit 2.9.2 / LiveChartsCore.SkiaSharpView.Avalonia / AvaloniaEdit 11.0.6 / Projektanker.Icons.Avalonia.Lucide 9.4.1 / Avalonia.Headless（新加）

**Spec:** `Docs/superpowers/specs/2026-05-13-tianming-mac-ui-foundation-design.md`

**实施分支策略：** 在 `main` 分支基于 M3 落地代码开新 feature 分支 `mac-ui/foundation-2026-05-13` 推进；Mac_UI 入仓需要在 `m2-m6/specs-2026-05-12` 也做一次（§§Phase 1）。每个 Task 完成后 `git commit`，最终 PR 回 `main`。

---

## Phase 1 / Mac_UI 入仓两边

### Task 1: Mac_UI 入仓 specs 分支

**Files:**
- Modify: `Mac_UI/README.md`
- Create: 实际上是把已有目录 commit 进 git，无新增物理文件
- Working branch: `m2-m6/specs-2026-05-12`

- [ ] **Step 1: 切换到 specs 分支并确认 Mac_UI 未跟踪**

```bash
git checkout m2-m6/specs-2026-05-12
git status --short | grep Mac_UI
```

Expected output:
```
?? Mac_UI/
```

- [ ] **Step 2: 在 Mac_UI/README.md 末尾补一段 M6 spec 链接**

打开 `Mac_UI/README.md`，在最后一行后面追加：

```markdown

> M6 已写 spec（v2.8.7 写作内核升级，**不是发布级，是后续版本更新**）：`Docs/superpowers/specs/2026-05-12-tianming-m6-v287-core-upgrade-design.md`。06 / 07 / 08 / 09 图里 `disabledUntil(M6)` 占位都对应 M6 实装能力，不是"以后删掉的功能"。
```

- [ ] **Step 3: stage + commit**

```bash
git add Mac_UI/
git commit -m "docs(ui): Mac_UI 视觉参考素材入仓（10 张参考图 + pseudocode）

包含：
- images/01-10.png 10 张高保真参考图
- pseudocode/01-10.md 对应伪代码（PageKey / View / VM / state / services / commands / render）
- README.md 映射 M3-M6 计划，补 M6 spec 链接

视觉真值源：specs 分支首发，main 用 git checkout 单向同步（详见 mac-ui-foundation-design spec §8）"
```

Expected output:
```
[m2-m6/specs-2026-05-12 <hash>] docs(ui): ...
```

---

### Task 2: Mac_UI 同步到 main，并开 feature 分支

**Files:**
- 通过 `git checkout` 把 Mac_UI/ 拷过去 main
- Branches: 起 `mac-ui/foundation-2026-05-13` 基于 main

- [ ] **Step 1: 切到 main 并取得 Mac_UI 副本**

```bash
git checkout main
git checkout m2-m6/specs-2026-05-12 -- Mac_UI/
git status --short
```

Expected：`Mac_UI/` 下文件全部 `A` (added)

- [ ] **Step 2: commit 到 main**

```bash
git add Mac_UI/
git commit -m "docs(ui): 从 specs 分支单向同步 Mac_UI 视觉参考素材

视觉真值源约定：Mac_UI 只在 m2-m6/specs-2026-05-12 编辑；main 走 git checkout specs -- Mac_UI/ 单向同步。"
```

- [ ] **Step 3: 开 feature 分支 `mac-ui/foundation-2026-05-13`**

```bash
git checkout -b mac-ui/foundation-2026-05-13
git status
```

Expected：`On branch mac-ui/foundation-2026-05-13`，working tree clean

---

## Phase 2 / NuGet 依赖追加

### Task 3: 探明 LiveCharts2 Avalonia 11 兼容版本

**Files:** 调研，无文件修改

- [ ] **Step 1: 查 LiveCharts2 最新可用版本**

```bash
dotnet package search LiveChartsCore.SkiaSharpView.Avalonia --prerelease --take 5
```

记下输出里支持 Avalonia 11 的最新稳定 / RC 版本号，写到下一 Step 的 csproj 里。预期目前是 `2.0.0-rc5` 或更新；若 search 失败可直接到 nuget.org 看 `LiveChartsCore.SkiaSharpView.Avalonia` 的 Avalonia 11.x 兼容版。

- [ ] **Step 2: csproj 加 3 个新包**

打开 `src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj`，把第一个 `<ItemGroup>` 改成：

```xml
<ItemGroup>
  <PackageReference Include="Avalonia" Version="11.0.10" />
  <PackageReference Include="Avalonia.Desktop" Version="11.0.10" />
  <PackageReference Include="Avalonia.Themes.Fluent" Version="11.0.10" />
  <PackageReference Include="Avalonia.Fonts.Inter" Version="11.0.10" />
  <PackageReference Condition="'$(Configuration)' == 'Debug'" Include="Avalonia.Diagnostics" Version="11.0.10" />
  <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
  <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.1" />
  <PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />
  <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="8.0.0" />
  <!-- 新加：LiveCharts2 / AvaloniaEdit / Lucide icons -->
  <PackageReference Include="LiveChartsCore.SkiaSharpView.Avalonia" Version="<step-1 调研得到的版本>" />
  <PackageReference Include="AvaloniaEdit" Version="11.0.6" />
  <PackageReference Include="Projektanker.Icons.Avalonia" Version="9.4.1" />
  <PackageReference Include="Projektanker.Icons.Avalonia.Lucide" Version="9.4.1" />
</ItemGroup>
```

- [ ] **Step 3: Test 项目加 Avalonia.Headless**

打开 `tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj`，把 PackageReferences 改成：

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
  <PackageReference Include="xunit" Version="2.9.2" />
  <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  <!-- 新加：Avalonia headless for UI tests -->
  <PackageReference Include="Avalonia.Headless" Version="11.0.10" />
  <PackageReference Include="Avalonia.Headless.XUnit" Version="11.0.10" />
</ItemGroup>
```

- [ ] **Step 4: restore + build verify**

```bash
dotnet restore Tianming.MacMigration.sln
dotnet build Tianming.MacMigration.sln -c Debug --nologo -v q
```

Expected：`Build succeeded. 0 Warning(s) 0 Error(s)`。

如果有 NU1605 downgrade 警告，再调 Microsoft.Extensions.DependencyInjection 等版本（按错误提示）。

- [ ] **Step 5: 跑现有 1156 测试基线确认未退化**

```bash
dotnet test Tianming.MacMigration.sln -c Debug --nologo --no-build -v q 2>&1 | tail -10
```

Expected：1156 个测试通过（Framework 781 + ProjectData 218 + AI 144 + Avalonia 13）。

- [ ] **Step 6: commit**

```bash
git add src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj \
        tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj
git commit -m "build(deps): 加 LiveCharts2 / AvaloniaEdit / Lucide / Avalonia.Headless

- LiveChartsCore.SkiaSharpView.Avalonia <version>: 后续图表渲染
- AvaloniaEdit 11.0.6: CodeViewer primitive + M4.3 章节编辑器
- Projektanker.Icons.Avalonia(.Lucide) 9.4.1: 全套 primitive 用 Lucide string token
- Avalonia.Headless(.XUnit) 11.0.10: shell / primitives UI 单元测试

1156 测试基线保持通过。"
```

---

## Phase 3 / Design tokens（5 个 axaml + sampling 脚本 + 旧 palette 迁移）

### Task 4: 写取色脚本生成 sampled-tokens.json

**Files:**
- Create: `Mac_UI/sample-tokens.py`
- Create: `Mac_UI/sampled-tokens.json` （脚本输出）

- [ ] **Step 1: 写采样脚本**

Create `Mac_UI/sample-tokens.py`:

```python
#!/usr/bin/env python3
"""从 Mac_UI/images/*.png 采样关键 anchor 像素，生成 sampled-tokens.json。

anchor 位置由肉眼挑选（标定特征区域中心点），output 为 token 名 → "#RRGGBB"。
后续如需调整，改 ANCHORS 字典即可。
"""
from __future__ import annotations
import json
import sys
from pathlib import Path
from PIL import Image

# (image_name, (x, y), token_name)
ANCHORS = [
    # 01 Welcome
    ("01-welcome-project-selector.png", (40, 40),   "AccentBase"),       # 天命 logo 青色
    ("01-welcome-project-selector.png", (300, 300), "SurfaceCanvas"),    # 页面背景
    ("01-welcome-project-selector.png", (480, 180), "SurfaceBase"),      # 卡片底
    # 02 Main workspace
    ("02-main-workspace-three-column.png", (520, 220), "TextPrimary"),   # dashboard 标题黑色
    ("02-main-workspace-three-column.png", (520, 240), "TextSecondary"), # 副文字
    # ...实施时按肉眼挑选位置补全所有 token，先有结构再迭代
]

def sample(image_path: Path, xy: tuple[int, int]) -> str:
    img = Image.open(image_path).convert("RGB")
    r, g, b = img.getpixel(xy)
    return f"#{r:02X}{g:02X}{b:02X}"

def main() -> int:
    images_dir = Path(__file__).parent / "images"
    out: dict[str, str] = {}
    for image_name, xy, token in ANCHORS:
        path = images_dir / image_name
        if not path.exists():
            print(f"WARN: {path} not found, skipping", file=sys.stderr)
            continue
        try:
            color = sample(path, xy)
            out[token] = color
            print(f"  {token:20s} = {color}  ({image_name} @ {xy})")
        except Exception as exc:
            print(f"WARN: sampling {token} failed: {exc}", file=sys.stderr)

    out_path = Path(__file__).parent / "sampled-tokens.json"
    out_path.write_text(json.dumps(out, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"\nwrote {out_path}")
    return 0

if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Step 2: 跑脚本，生成 JSON**

```bash
cd /Users/jimmy/Downloads/tianming-novel-ai-writer
python3 Mac_UI/sample-tokens.py
```

Expected：终端打印每个 token 的采样值 + `wrote Mac_UI/sampled-tokens.json`。

输出示例：
```
  AccentBase           = #06B6D4  (01-welcome-project-selector.png @ (40, 40))
  SurfaceCanvas        = #F8FAFC  (01-welcome-project-selector.png @ (300, 300))
  ...
wrote Mac_UI/sampled-tokens.json
```

> 若实际采样值跟 spec §2.1 估计值差距 > ΔE5，**以采样值为准**，把 spec §2.1 的注释 "实施时校准" 落实，把后续 token 文件（Task 5）里的 HEX 全部替换为采样值。

- [ ] **Step 3: commit**

```bash
git add Mac_UI/sample-tokens.py Mac_UI/sampled-tokens.json
git commit -m "tools(ui): Mac_UI 取色脚本 + sampled-tokens.json

PIL 采样 Mac_UI/images/ 的 anchor 像素到 JSON；后续 token axaml 用这份 JSON 作为视觉真值。
ANCHORS 字典可迭代扩充。"
```

---

### Task 5: Theme/DesignTokens/Colors.axaml + 加载测试

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Theme/DesignTokens/Colors.axaml`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Theme/DesignTokensTests.cs`

- [ ] **Step 1: 写 Colors.axaml**

Create `src/Tianming.Desktop.Avalonia/Theme/DesignTokens/Colors.axaml`:

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <!-- Brand / Accent -->
  <Color x:Key="AccentBase">#06B6D4</Color>
  <Color x:Key="AccentHover">#0891B2</Color>
  <Color x:Key="AccentPressed">#0E7490</Color>
  <Color x:Key="AccentSubtle">#CFFAFE</Color>
  <Color x:Key="AccentForeground">#FFFFFF</Color>

  <!-- Neutral surfaces -->
  <Color x:Key="SurfaceBase">#FFFFFF</Color>
  <Color x:Key="SurfaceCanvas">#F8FAFC</Color>
  <Color x:Key="SurfaceSubtle">#F1F5F9</Color>
  <Color x:Key="SurfaceMuted">#E2E8F0</Color>
  <Color x:Key="BorderSubtle">#E5E7EB</Color>
  <Color x:Key="BorderStrong">#CBD5E1</Color>

  <!-- Text -->
  <Color x:Key="TextPrimary">#0F172A</Color>
  <Color x:Key="TextSecondary">#475569</Color>
  <Color x:Key="TextTertiary">#94A3B8</Color>
  <Color x:Key="TextOnAccent">#FFFFFF</Color>

  <!-- Semantic / status -->
  <Color x:Key="StatusSuccess">#10B981</Color>
  <Color x:Key="StatusSuccessSubtle">#D1FAE5</Color>
  <Color x:Key="StatusWarning">#F59E0B</Color>
  <Color x:Key="StatusWarningSubtle">#FEF3C7</Color>
  <Color x:Key="StatusDanger">#EF4444</Color>
  <Color x:Key="StatusDangerSubtle">#FEE2E2</Color>
  <Color x:Key="StatusInfo">#3B82F6</Color>
  <Color x:Key="StatusInfoSubtle">#DBEAFE</Color>
  <Color x:Key="StatusNeutral">#6B7280</Color>
  <Color x:Key="StatusNeutralSubtle">#F3F4F6</Color>

  <!-- *Brush 同名 + Brush 后缀 -->
  <SolidColorBrush x:Key="AccentBaseBrush"            Color="{StaticResource AccentBase}"/>
  <SolidColorBrush x:Key="AccentHoverBrush"           Color="{StaticResource AccentHover}"/>
  <SolidColorBrush x:Key="AccentPressedBrush"         Color="{StaticResource AccentPressed}"/>
  <SolidColorBrush x:Key="AccentSubtleBrush"          Color="{StaticResource AccentSubtle}"/>
  <SolidColorBrush x:Key="AccentForegroundBrush"      Color="{StaticResource AccentForeground}"/>
  <SolidColorBrush x:Key="SurfaceBaseBrush"           Color="{StaticResource SurfaceBase}"/>
  <SolidColorBrush x:Key="SurfaceCanvasBrush"         Color="{StaticResource SurfaceCanvas}"/>
  <SolidColorBrush x:Key="SurfaceSubtleBrush"         Color="{StaticResource SurfaceSubtle}"/>
  <SolidColorBrush x:Key="SurfaceMutedBrush"          Color="{StaticResource SurfaceMuted}"/>
  <SolidColorBrush x:Key="BorderSubtleBrush"          Color="{StaticResource BorderSubtle}"/>
  <SolidColorBrush x:Key="BorderStrongBrush"          Color="{StaticResource BorderStrong}"/>
  <SolidColorBrush x:Key="TextPrimaryBrush"           Color="{StaticResource TextPrimary}"/>
  <SolidColorBrush x:Key="TextSecondaryBrush"         Color="{StaticResource TextSecondary}"/>
  <SolidColorBrush x:Key="TextTertiaryBrush"          Color="{StaticResource TextTertiary}"/>
  <SolidColorBrush x:Key="TextOnAccentBrush"          Color="{StaticResource TextOnAccent}"/>
  <SolidColorBrush x:Key="StatusSuccessBrush"         Color="{StaticResource StatusSuccess}"/>
  <SolidColorBrush x:Key="StatusSuccessSubtleBrush"   Color="{StaticResource StatusSuccessSubtle}"/>
  <SolidColorBrush x:Key="StatusWarningBrush"         Color="{StaticResource StatusWarning}"/>
  <SolidColorBrush x:Key="StatusWarningSubtleBrush"   Color="{StaticResource StatusWarningSubtle}"/>
  <SolidColorBrush x:Key="StatusDangerBrush"          Color="{StaticResource StatusDanger}"/>
  <SolidColorBrush x:Key="StatusDangerSubtleBrush"    Color="{StaticResource StatusDangerSubtle}"/>
  <SolidColorBrush x:Key="StatusInfoBrush"            Color="{StaticResource StatusInfo}"/>
  <SolidColorBrush x:Key="StatusInfoSubtleBrush"      Color="{StaticResource StatusInfoSubtle}"/>
  <SolidColorBrush x:Key="StatusNeutralBrush"         Color="{StaticResource StatusNeutral}"/>
  <SolidColorBrush x:Key="StatusNeutralSubtleBrush"   Color="{StaticResource StatusNeutralSubtle}"/>
</ResourceDictionary>
```

- [ ] **Step 2: 把 axaml 设为 AvaloniaResource**

确保 `Tianming.Desktop.Avalonia.csproj` 里 axaml 自动包含。Avalonia SDK 默认对 `**/*.axaml` 做 AvaloniaResource glob，不需要单独声明。验证：

```bash
dotnet build src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj -c Debug --nologo -v q
```

Expected：build 通过。Colors.axaml 此时还未被任何地方 include，所以不会被加载，只是验证编译期 XAML 合法。

- [ ] **Step 3: 写加载测试（先红）**

Create `tests/Tianming.Desktop.Avalonia.Tests/Theme/DesignTokensTests.cs`:

```csharp
using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Theme;

public class DesignTokensTests
{
    private static readonly string ColorsXamlUri = "avares://Tianming.Desktop.Avalonia/Theme/DesignTokens/Colors.axaml";

    private static IResourceDictionary LoadColors()
    {
        // 通过 ResourceInclude 加载 axaml 文件，避免依赖 App.axaml 还没接入它
        var include = new ResourceInclude(new Uri("resm:?", UriKind.Relative))
        {
            Source = new Uri(ColorsXamlUri)
        };
        return include.Loaded;
    }

    [AvaloniaFact]
    public void Colors_AccentKeysResolve()
    {
        var dict = LoadColors();
        var keys = new[] { "AccentBase", "AccentHover", "AccentPressed", "AccentSubtle", "AccentForeground" };
        foreach (var k in keys)
        {
            Assert.True(dict.TryGetResource(k, null, out var v), $"missing key {k}");
            Assert.IsType<Color>(v);
        }
    }

    [AvaloniaFact]
    public void Colors_NeutralAndTextKeysResolve()
    {
        var dict = LoadColors();
        var keys = new[]
        {
            "SurfaceBase", "SurfaceCanvas", "SurfaceSubtle", "SurfaceMuted",
            "BorderSubtle", "BorderStrong",
            "TextPrimary", "TextSecondary", "TextTertiary", "TextOnAccent"
        };
        foreach (var k in keys)
        {
            Assert.True(dict.TryGetResource(k, null, out var v), $"missing key {k}");
            Assert.IsType<Color>(v);
        }
    }

    [AvaloniaFact]
    public void Colors_StatusKeysResolve()
    {
        var dict = LoadColors();
        var keys = new[]
        {
            "StatusSuccess", "StatusSuccessSubtle",
            "StatusWarning", "StatusWarningSubtle",
            "StatusDanger",  "StatusDangerSubtle",
            "StatusInfo",    "StatusInfoSubtle",
            "StatusNeutral", "StatusNeutralSubtle"
        };
        foreach (var k in keys)
        {
            Assert.True(dict.TryGetResource(k, null, out var v), $"missing key {k}");
            Assert.IsType<Color>(v);
        }
    }

    [AvaloniaFact]
    public void Colors_BrushAliasesResolve()
    {
        var dict = LoadColors();
        // 每个 Color 都有同名 *Brush 别名
        Assert.True(dict.TryGetResource("AccentBaseBrush", null, out var v));
        Assert.IsType<SolidColorBrush>(v);
        Assert.True(dict.TryGetResource("TextPrimaryBrush", null, out v));
        Assert.IsType<SolidColorBrush>(v);
        Assert.True(dict.TryGetResource("StatusSuccessBrush", null, out v));
        Assert.IsType<SolidColorBrush>(v);
    }
}
```

> 如果 Avalonia.Headless 还没准备好 Application context，需要在测试程序集 `AssemblyInfo.cs` 加 `[assembly: AvaloniaTestApplication(typeof(TestApp))]` + 一个最小 `TestApp : Application`。这一步在 Task 6（Typography 测试）首次需要时再补；这里 ResourceInclude 加载不依赖 Application.Current 也能跑。

- [ ] **Step 4: 跑测试看结果**

```bash
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ -c Debug --nologo --no-build --filter "FullyQualifiedName~DesignTokensTests" -v q
```

Expected：4 个测试全过（绿）。

如果失败，原因通常是 ResourceInclude 加载方式不对 → 改用 `AvaloniaXamlLoader.Load(new Uri(...))` 或类似。调到通过为止。

- [ ] **Step 5: commit**

```bash
git add src/Tianming.Desktop.Avalonia/Theme/DesignTokens/Colors.axaml \
        tests/Tianming.Desktop.Avalonia.Tests/Theme/DesignTokensTests.cs
git commit -m "feat(theme): DesignTokens/Colors.axaml 亮色色板（采样自 Mac_UI 参考图）

- 5 个 Accent / 6 个 Neutral surface / 4 个 Text / 10 个 Status 共 25 个 Color
- 每个 Color 都有 *Brush 同名 SolidColorBrush 别名
- 4 个加载测试（Accent / Neutral+Text / Status / Brush 别名）"
```

---

### Task 6: Theme/DesignTokens/Typography.axaml

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Theme/DesignTokens/Typography.axaml`
- Modify: `tests/Tianming.Desktop.Avalonia.Tests/Theme/DesignTokensTests.cs`（加测试）

- [ ] **Step 1: 写 Typography.axaml**

Create `src/Tianming.Desktop.Avalonia/Theme/DesignTokens/Typography.axaml`:

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:sys="clr-namespace:System;assembly=System.Runtime">
  <!-- Font families: macOS 优先 PingFang SC (CJK) + SF Pro Text (Latin) -->
  <FontFamily x:Key="FontUI">PingFang SC, SF Pro Text, sans-serif</FontFamily>
  <FontFamily x:Key="FontMono">SF Mono, Menlo, Consolas, monospace</FontFamily>

  <!-- Font sizes (pt) -->
  <x:Double x:Key="FontSizeDisplay">28</x:Double>
  <x:Double x:Key="FontSizeH1">22</x:Double>
  <x:Double x:Key="FontSizeH2">18</x:Double>
  <x:Double x:Key="FontSizeH3">15</x:Double>
  <x:Double x:Key="FontSizeBody">13</x:Double>
  <x:Double x:Key="FontSizeSecondary">12</x:Double>
  <x:Double x:Key="FontSizeCaption">11</x:Double>

  <!-- Font weights -->
  <FontWeight x:Key="FontWeightRegular">Normal</FontWeight>
  <FontWeight x:Key="FontWeightMedium">Medium</FontWeight>
  <FontWeight x:Key="FontWeightSemibold">SemiBold</FontWeight>
  <FontWeight x:Key="FontWeightBold">Bold</FontWeight>

  <!-- Line height multipliers -->
  <x:Double x:Key="LineHeightTight">1.2</x:Double>
  <x:Double x:Key="LineHeightNormal">1.5</x:Double>
  <x:Double x:Key="LineHeightRelaxed">1.7</x:Double>
</ResourceDictionary>
```

- [ ] **Step 2: 加测试**

把 `DesignTokensTests.cs` 末尾追加（在 class 内）：

```csharp
    private static IResourceDictionary LoadTypography()
    {
        var include = new ResourceInclude(new Uri("resm:?", UriKind.Relative))
        {
            Source = new Uri("avares://Tianming.Desktop.Avalonia/Theme/DesignTokens/Typography.axaml")
        };
        return include.Loaded;
    }

    [AvaloniaFact]
    public void Typography_FontFamiliesResolve()
    {
        var dict = LoadTypography();
        Assert.True(dict.TryGetResource("FontUI", null, out var ui));
        Assert.IsType<FontFamily>(ui);
        Assert.True(dict.TryGetResource("FontMono", null, out var mono));
        Assert.IsType<FontFamily>(mono);
    }

    [AvaloniaFact]
    public void Typography_FontSizesResolveAndAreNumeric()
    {
        var dict = LoadTypography();
        var sizes = new[] { "FontSizeDisplay", "FontSizeH1", "FontSizeH2", "FontSizeH3",
                            "FontSizeBody", "FontSizeSecondary", "FontSizeCaption" };
        foreach (var k in sizes)
        {
            Assert.True(dict.TryGetResource(k, null, out var v), $"missing {k}");
            Assert.IsType<double>(v);
            Assert.True((double)v! > 0);
        }
    }

    [AvaloniaFact]
    public void Typography_WeightsAndLineHeightsResolve()
    {
        var dict = LoadTypography();
        foreach (var k in new[] { "FontWeightRegular", "FontWeightMedium", "FontWeightSemibold", "FontWeightBold" })
        {
            Assert.True(dict.TryGetResource(k, null, out var v));
            Assert.IsType<FontWeight>(v);
        }
        foreach (var k in new[] { "LineHeightTight", "LineHeightNormal", "LineHeightRelaxed" })
        {
            Assert.True(dict.TryGetResource(k, null, out var v));
            Assert.IsType<double>(v);
        }
    }
```

- [ ] **Step 3: 跑测试 + commit**

```bash
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ -c Debug --nologo --no-build --filter "FullyQualifiedName~DesignTokensTests" -v q
```

Expected：7 个测试全过（4 Colors + 3 Typography）。

```bash
git add src/Tianming.Desktop.Avalonia/Theme/DesignTokens/Typography.axaml \
        tests/Tianming.Desktop.Avalonia.Tests/Theme/DesignTokensTests.cs
git commit -m "feat(theme): DesignTokens/Typography.axaml 字体族 / 字号 / 字重 / 行高

PingFang SC + SF Pro Text UI + SF Mono 等宽；7 档字号；4 档字重；3 档行高。
3 个加载测试。"
```

---

### Task 7: Theme/DesignTokens/Spacing.axaml

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Theme/DesignTokens/Spacing.axaml`
- Modify: `tests/Tianming.Desktop.Avalonia.Tests/Theme/DesignTokensTests.cs`

- [ ] **Step 1: 写 Spacing.axaml**

Create `src/Tianming.Desktop.Avalonia/Theme/DesignTokens/Spacing.axaml`:

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <!-- 4px 基础栅格 -->
  <x:Double x:Key="Space1">4</x:Double>
  <x:Double x:Key="Space2">8</x:Double>
  <x:Double x:Key="Space3">12</x:Double>
  <x:Double x:Key="Space4">16</x:Double>
  <x:Double x:Key="Space5">20</x:Double>
  <x:Double x:Key="Space6">24</x:Double>
  <x:Double x:Key="Space8">32</x:Double>
  <x:Double x:Key="Space10">40</x:Double>

  <!-- 常用 padding -->
  <Thickness x:Key="PaddingCard">16</Thickness>
  <Thickness x:Key="PaddingPage">24</Thickness>
  <Thickness x:Key="PaddingInputControl">8,4</Thickness>
</ResourceDictionary>
```

- [ ] **Step 2: 加测试**

把这段 append 到 `DesignTokensTests.cs`：

```csharp
    private static IResourceDictionary LoadSpacing()
    {
        var include = new ResourceInclude(new Uri("resm:?", UriKind.Relative))
        {
            Source = new Uri("avares://Tianming.Desktop.Avalonia/Theme/DesignTokens/Spacing.axaml")
        };
        return include.Loaded;
    }

    [AvaloniaFact]
    public void Spacing_AllSpaceKeysResolveAndIncreasing()
    {
        var dict = LoadSpacing();
        var keys = new[] { "Space1", "Space2", "Space3", "Space4", "Space5", "Space6", "Space8", "Space10" };
        double prev = 0;
        foreach (var k in keys)
        {
            Assert.True(dict.TryGetResource(k, null, out var v));
            var val = Assert.IsType<double>(v);
            Assert.True(val > prev, $"{k} should be > previous {prev}, got {val}");
            prev = val;
        }
    }

    [AvaloniaFact]
    public void Spacing_PaddingThicknessKeysResolve()
    {
        var dict = LoadSpacing();
        foreach (var k in new[] { "PaddingCard", "PaddingPage", "PaddingInputControl" })
        {
            Assert.True(dict.TryGetResource(k, null, out var v));
            Assert.IsType<Thickness>(v);
        }
    }
```

- [ ] **Step 3: 跑测试 + commit**

```bash
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ -c Debug --nologo --no-build --filter "FullyQualifiedName~DesignTokensTests" -v q
```

Expected：9 个测试全过。

```bash
git add src/Tianming.Desktop.Avalonia/Theme/DesignTokens/Spacing.axaml \
        tests/Tianming.Desktop.Avalonia.Tests/Theme/DesignTokensTests.cs
git commit -m "feat(theme): DesignTokens/Spacing.axaml 4px 栅格 + 常用 padding"
```

---

### Task 8: Theme/DesignTokens/Radii.axaml

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Theme/DesignTokens/Radii.axaml`
- Modify: `tests/Tianming.Desktop.Avalonia.Tests/Theme/DesignTokensTests.cs`

- [ ] **Step 1: 写 Radii.axaml**

Create `src/Tianming.Desktop.Avalonia/Theme/DesignTokens/Radii.axaml`:

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <CornerRadius x:Key="RadiusSm">4</CornerRadius>
  <CornerRadius x:Key="RadiusMd">6</CornerRadius>
  <CornerRadius x:Key="RadiusLg">8</CornerRadius>
  <CornerRadius x:Key="RadiusXl">12</CornerRadius>
  <CornerRadius x:Key="RadiusFull">9999</CornerRadius>
</ResourceDictionary>
```

- [ ] **Step 2: 加测试**

```csharp
    private static IResourceDictionary LoadRadii()
    {
        var include = new ResourceInclude(new Uri("resm:?", UriKind.Relative))
        {
            Source = new Uri("avares://Tianming.Desktop.Avalonia/Theme/DesignTokens/Radii.axaml")
        };
        return include.Loaded;
    }

    [AvaloniaFact]
    public void Radii_AllKeysResolveAsCornerRadius()
    {
        var dict = LoadRadii();
        foreach (var k in new[] { "RadiusSm", "RadiusMd", "RadiusLg", "RadiusXl", "RadiusFull" })
        {
            Assert.True(dict.TryGetResource(k, null, out var v));
            Assert.IsType<CornerRadius>(v);
        }
    }
```

- [ ] **Step 3: 跑测试 + commit**

```bash
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ -c Debug --nologo --no-build --filter "FullyQualifiedName~DesignTokensTests" -v q
```

```bash
git add src/Tianming.Desktop.Avalonia/Theme/DesignTokens/Radii.axaml \
        tests/Tianming.Desktop.Avalonia.Tests/Theme/DesignTokensTests.cs
git commit -m "feat(theme): DesignTokens/Radii.axaml 5 档圆角（4/6/8/12/9999）"
```

---

### Task 9: Theme/DesignTokens/Shadows.axaml

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Theme/DesignTokens/Shadows.axaml`
- Modify: `tests/Tianming.Desktop.Avalonia.Tests/Theme/DesignTokensTests.cs`

- [ ] **Step 1: 写 Shadows.axaml**

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <BoxShadows x:Key="ShadowSm">0 1 2 0 #14000000</BoxShadows>
  <BoxShadows x:Key="ShadowMd">0 4 12 0 #1F000000</BoxShadows>
  <BoxShadows x:Key="ShadowLg">0 12 32 0 #29000000</BoxShadows>
</ResourceDictionary>
```

> 阴影色用 `#aaRRGGBB` 8 位 alpha（0x14 ≈ 8%, 0x1F ≈ 12%, 0x29 ≈ 16%），黑色叠层。

- [ ] **Step 2: 加测试**

```csharp
    private static IResourceDictionary LoadShadows()
    {
        var include = new ResourceInclude(new Uri("resm:?", UriKind.Relative))
        {
            Source = new Uri("avares://Tianming.Desktop.Avalonia/Theme/DesignTokens/Shadows.axaml")
        };
        return include.Loaded;
    }

    [AvaloniaFact]
    public void Shadows_AllKeysResolveAsBoxShadows()
    {
        var dict = LoadShadows();
        foreach (var k in new[] { "ShadowSm", "ShadowMd", "ShadowLg" })
        {
            Assert.True(dict.TryGetResource(k, null, out var v));
            Assert.IsType<BoxShadows>(v);
        }
    }
```

- [ ] **Step 3: 跑测试 + commit**

```bash
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ -c Debug --nologo --no-build --filter "FullyQualifiedName~DesignTokensTests" -v q
git add src/Tianming.Desktop.Avalonia/Theme/DesignTokens/Shadows.axaml \
        tests/Tianming.Desktop.Avalonia.Tests/Theme/DesignTokensTests.cs
git commit -m "feat(theme): DesignTokens/Shadows.axaml 3 档阴影（sm/md/lg）"
```

---

### Task 10: 重写 PalettesLight + 删 PalettesDark + 迁移现有 views + 更新 App.axaml ResourceDictionary

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/Theme/PalettesLight.axaml`
- Delete: `src/Tianming.Desktop.Avalonia/Theme/PalettesDark.axaml`
- Modify: `src/Tianming.Desktop.Avalonia/Theme/CommonStyles.axaml`
- Modify: `src/Tianming.Desktop.Avalonia/Views/Shell/LeftNavView.axaml`
- Modify: `src/Tianming.Desktop.Avalonia/Views/Shell/RightConversationView.axaml`
- Modify: `src/Tianming.Desktop.Avalonia/Views/WelcomeView.axaml`
- Modify: `src/Tianming.Desktop.Avalonia/Views/PlaceholderView.axaml`
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml`
- Modify: `src/Tianming.Desktop.Avalonia/Theme/ThemeBridge.cs`（删暗色相关代码或锁亮色）

- [ ] **Step 1: 重写 PalettesLight.axaml 为 reference shim**

把 `src/Tianming.Desktop.Avalonia/Theme/PalettesLight.axaml` 改为：

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <!-- 此文件是历史兼容入口：把 DesignTokens/Colors.axaml 的 *Brush 暴露给
       旧代码使用的旧 key 名（UnifiedBackground / ContentBackground / 等）。
       新写的 view 请直接用 DesignTokens/Colors.axaml 的 *Brush key。 -->
  <ResourceDictionary.MergedDictionaries>
    <ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Theme/DesignTokens/Colors.axaml"/>
  </ResourceDictionary.MergedDictionaries>

  <!-- 旧 key → 新 brush 别名（保留过渡期兼容；Sub-plan 2 完成后删） -->
  <SolidColorBrush x:Key="UnifiedBackground"   Color="{StaticResource SurfaceBase}"/>
  <SolidColorBrush x:Key="ContentBackground"   Color="{StaticResource SurfaceCanvas}"/>
  <SolidColorBrush x:Key="SurfaceBackground"   Color="{StaticResource SurfaceSubtle}"/>
  <SolidColorBrush x:Key="BorderBrush"         Color="{StaticResource BorderSubtle}"/>
  <SolidColorBrush x:Key="TextPrimary"         Color="{StaticResource TextPrimary}"/>
  <SolidColorBrush x:Key="TextSecondary"       Color="{StaticResource TextSecondary}"/>
  <SolidColorBrush x:Key="TextTertiary"        Color="{StaticResource TextTertiary}"/>
  <SolidColorBrush x:Key="HoverBackground"     Color="{StaticResource SurfaceSubtle}"/>
  <SolidColorBrush x:Key="ActiveBackground"    Color="{StaticResource SurfaceMuted}"/>
  <SolidColorBrush x:Key="SelectedBackground"  Color="{StaticResource AccentSubtle}"/>
  <SolidColorBrush x:Key="PrimaryColor"        Color="{StaticResource AccentBase}"/>
  <SolidColorBrush x:Key="PrimaryHover"        Color="{StaticResource AccentHover}"/>
  <SolidColorBrush x:Key="PrimaryActive"       Color="{StaticResource AccentPressed}"/>
  <SolidColorBrush x:Key="SuccessColor"        Color="{StaticResource StatusSuccess}"/>
  <SolidColorBrush x:Key="WarningColor"        Color="{StaticResource StatusWarning}"/>
  <SolidColorBrush x:Key="DangerColor"         Color="{StaticResource StatusDanger}"/>
  <SolidColorBrush x:Key="DangerHover"         Color="{StaticResource StatusDanger}"/>
  <SolidColorBrush x:Key="InfoColor"           Color="{StaticResource StatusInfo}"/>
</ResourceDictionary>
```

- [ ] **Step 2: 删 PalettesDark.axaml**

```bash
git rm src/Tianming.Desktop.Avalonia/Theme/PalettesDark.axaml
```

- [ ] **Step 3: 更新 CommonStyles.axaml 用新名 + 加 token / control styles 入口**

打开 `src/Tianming.Desktop.Avalonia/Theme/CommonStyles.axaml`，全文替换为：

```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Styles.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <!-- DesignTokens 已经在 App.axaml 里合并；这里不重复 -->
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Styles.Resources>

  <!-- 全局基础样式（旧 key 名暂留兼容，后续 ControlStyles/ 子文件按 primitive 接管） -->
  <Style Selector="Window">
    <Setter Property="Background" Value="{DynamicResource SurfaceCanvasBrush}"/>
    <Setter Property="Foreground" Value="{DynamicResource TextPrimaryBrush}"/>
    <Setter Property="FontFamily" Value="{DynamicResource FontUI}"/>
    <Setter Property="FontSize"   Value="{DynamicResource FontSizeBody}"/>
  </Style>
  <Style Selector="TextBlock">
    <Setter Property="Foreground" Value="{DynamicResource TextPrimaryBrush}"/>
    <Setter Property="FontFamily" Value="{DynamicResource FontUI}"/>
  </Style>
</Styles>
```

- [ ] **Step 4: 现有 views 把旧 brush 名换成新 *Brush 名**

`src/Tianming.Desktop.Avalonia/Views/Shell/LeftNavView.axaml`：

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Tianming.Desktop.Avalonia.ViewModels.Shell"
             x:Class="Tianming.Desktop.Avalonia.Views.Shell.LeftNavView"
             x:DataType="vm:LeftNavViewModel">
  <Border Background="{DynamicResource SurfaceCanvasBrush}" Padding="8">
    <StackPanel>
      <TextBlock Text="导航" FontWeight="Bold" Margin="0,0,0,12" Foreground="{DynamicResource TextSecondaryBrush}"/>
      <ItemsControl ItemsSource="{Binding Entries}">
        <ItemsControl.ItemTemplate>
          <DataTemplate>
            <Button Command="{Binding $parent[ItemsControl].((vm:LeftNavViewModel)DataContext).NavigateCommand}"
                    CommandParameter="{Binding}"
                    HorizontalAlignment="Stretch" HorizontalContentAlignment="Left"
                    Margin="0,2" Background="Transparent" Foreground="{DynamicResource TextPrimaryBrush}">
              <TextBlock Text="{Binding Label}"/>
            </Button>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
    </StackPanel>
  </Border>
</UserControl>
```

`src/Tianming.Desktop.Avalonia/Views/Shell/RightConversationView.axaml`：

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Tianming.Desktop.Avalonia.ViewModels.Shell"
             x:Class="Tianming.Desktop.Avalonia.Views.Shell.RightConversationView"
             x:DataType="vm:RightConversationViewModel">
  <Border Background="{DynamicResource SurfaceCanvasBrush}" Padding="16">
    <TextBlock Text="{Binding PlaceholderText}" Foreground="{DynamicResource TextTertiaryBrush}"
               HorizontalAlignment="Center" VerticalAlignment="Center"/>
  </Border>
</UserControl>
```

`src/Tianming.Desktop.Avalonia/Views/WelcomeView.axaml`：把 `Foreground="{DynamicResource TextSecondary}"` 改成 `Foreground="{DynamicResource TextSecondaryBrush}"`。

`src/Tianming.Desktop.Avalonia/Views/PlaceholderView.axaml`：把 `Foreground="{DynamicResource TextTertiary}"` 改成 `Foreground="{DynamicResource TextTertiaryBrush}"`。

- [ ] **Step 5: 更新 App.axaml — 合并 token 字典 + ResourceDictionary 顺序**

打开 `src/Tianming.Desktop.Avalonia/App.axaml`，整体改为：

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="Tianming.Desktop.Avalonia.App"
             RequestedThemeVariant="Light">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <!-- 1. Design tokens 先合 -->
        <ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Theme/DesignTokens/Colors.axaml"/>
        <ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Theme/DesignTokens/Typography.axaml"/>
        <ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Theme/DesignTokens/Spacing.axaml"/>
        <ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Theme/DesignTokens/Radii.axaml"/>
        <ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Theme/DesignTokens/Shadows.axaml"/>
        <!-- 2. 旧 palette 兼容（PalettesLight 现在 internally MergedDictionaries 了 Colors.axaml） -->
        <ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Theme/PalettesLight.axaml"/>
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
  <Application.Styles>
    <FluentTheme />
    <StyleInclude Source="avares://Tianming.Desktop.Avalonia/Theme/CommonStyles.axaml"/>
  </Application.Styles>
</Application>
```

注意 `RequestedThemeVariant="Light"` 锁亮色。

- [ ] **Step 6: ThemeBridge.cs 移除暗色支持**

打开 `src/Tianming.Desktop.Avalonia/Theme/ThemeBridge.cs`，把 `ApplyLightDarkVariant` 改成永远只设 `ThemeVariant.Light`：

```csharp
private void ApplyLightDarkVariant(PortableThemeType type)
{
    var app = Application.Current;
    if (app is null) return;
    // 亮色 only（Mac UI 基建决策）
    app.RequestedThemeVariant = ThemeVariant.Light;
}
```

并在 `ApplyCore` 末尾 `app.RequestedThemeVariant = variant;` 那行强制改为 `app.RequestedThemeVariant = ThemeVariant.Light;`（忽略 request.Plan.ColorMode）：

```csharp
private void ApplyCore(PortableThemeApplicationRequest request)
{
    var app = Application.Current;
    if (app is null) return;

    foreach (var kv in request.Brushes)
    {
        if (!TryParseHex(kv.Value, out var color)) continue;
        app.Resources[kv.Key] = new SolidColorBrush(color);
    }

    // 亮色 only（Mac UI 基建决策）
    app.RequestedThemeVariant = ThemeVariant.Light;
    _log.LogInformation("Applied theme {Theme} (forced Light)", request.Plan.ThemeType);
}
```

- [ ] **Step 7: build + 跑全测试**

```bash
dotnet build Tianming.MacMigration.sln -c Debug --nologo -v q
```

Expected：0 error 0 warning。

```bash
dotnet test Tianming.MacMigration.sln -c Debug --nologo --no-build -v q 2>&1 | tail -10
```

Expected：1156 基线 + 13（Design tokens 测试）通过。

- [ ] **Step 8: 冒烟启动**

```bash
dotnet run --project src/Tianming.Desktop.Avalonia -c Debug 2>&1 | head -20
```

Expected：窗口启动成功；现有 Welcome view 显示出来（视觉可能跟以前略有不同，因为 PrimaryColor 从 #3B82F6 蓝色变成 #06B6D4 青色，但不应崩溃）。手动关掉窗口。

- [ ] **Step 9: commit**

```bash
git add src/Tianming.Desktop.Avalonia/Theme/ \
        src/Tianming.Desktop.Avalonia/Views/Shell/ \
        src/Tianming.Desktop.Avalonia/Views/WelcomeView.axaml \
        src/Tianming.Desktop.Avalonia/Views/PlaceholderView.axaml \
        src/Tianming.Desktop.Avalonia/App.axaml
git commit -m "refactor(theme): 切到 DesignTokens token 系；删 PalettesDark；锁亮色

- PalettesLight 改为 reference shim：合并 DesignTokens/Colors.axaml + 暴露旧 key 别名（兼容现有 view）
- App.axaml ResourceDictionary 合并 5 个 token + PalettesLight
- 现有 views (LeftNav / RightConv / Welcome / Placeholder / CommonStyles) 迁移到 *Brush 命名
- ThemeBridge 锁定 ThemeVariant.Light，忽略 Dark request
- 删 PalettesDark.axaml

1156 + 13 测试基线全过。"
```

---

## Phase 4 / ControlStyles overrides + LiveCharts / AvaloniaEdit bootstrap

### Task 11: ControlStyles overrides — Button / TextBox / ComboBox / ListBox / DataGrid / ScrollBar

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Theme/ControlStyles/Button.axaml`
- Create: `src/Tianming.Desktop.Avalonia/Theme/ControlStyles/TextBox.axaml`
- Create: `src/Tianming.Desktop.Avalonia/Theme/ControlStyles/ComboBox.axaml`
- Create: `src/Tianming.Desktop.Avalonia/Theme/ControlStyles/ListBox.axaml`
- Create: `src/Tianming.Desktop.Avalonia/Theme/ControlStyles/DataGrid.axaml`
- Create: `src/Tianming.Desktop.Avalonia/Theme/ControlStyles/ScrollBar.axaml`
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml`

- [ ] **Step 1: Button.axaml — 4 个 Style.Class（Primary / Secondary / Ghost / Icon）**

Create `src/Tianming.Desktop.Avalonia/Theme/ControlStyles/Button.axaml`:

```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <!-- 默认 Button = Primary 强调色 -->
  <Style Selector="Button">
    <Setter Property="Background" Value="{DynamicResource AccentBaseBrush}"/>
    <Setter Property="Foreground" Value="{DynamicResource AccentForegroundBrush}"/>
    <Setter Property="CornerRadius" Value="{DynamicResource RadiusSm}"/>
    <Setter Property="Padding" Value="{DynamicResource PaddingInputControl}"/>
    <Setter Property="FontFamily" Value="{DynamicResource FontUI}"/>
    <Setter Property="FontSize" Value="{DynamicResource FontSizeBody}"/>
    <Setter Property="FontWeight" Value="{DynamicResource FontWeightMedium}"/>
    <Setter Property="BorderThickness" Value="0"/>
  </Style>
  <Style Selector="Button:pointerover">
    <Setter Property="Background" Value="{DynamicResource AccentHoverBrush}"/>
  </Style>
  <Style Selector="Button:pressed">
    <Setter Property="Background" Value="{DynamicResource AccentPressedBrush}"/>
  </Style>
  <Style Selector="Button:disabled">
    <Setter Property="Opacity" Value="0.5"/>
  </Style>

  <!-- Secondary：白底 + 强色描边 -->
  <Style Selector="Button.secondary">
    <Setter Property="Background" Value="{DynamicResource SurfaceBaseBrush}"/>
    <Setter Property="Foreground" Value="{DynamicResource AccentBaseBrush}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource AccentBaseBrush}"/>
    <Setter Property="BorderThickness" Value="1"/>
  </Style>
  <Style Selector="Button.secondary:pointerover">
    <Setter Property="Background" Value="{DynamicResource AccentSubtleBrush}"/>
  </Style>

  <!-- Ghost：透明 + neutral 字色 -->
  <Style Selector="Button.ghost">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="Foreground" Value="{DynamicResource TextPrimaryBrush}"/>
    <Setter Property="BorderThickness" Value="0"/>
  </Style>
  <Style Selector="Button.ghost:pointerover">
    <Setter Property="Background" Value="{DynamicResource SurfaceSubtleBrush}"/>
  </Style>

  <!-- Icon：方形小按钮 -->
  <Style Selector="Button.icon">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="Foreground" Value="{DynamicResource TextSecondaryBrush}"/>
    <Setter Property="Padding" Value="6"/>
    <Setter Property="CornerRadius" Value="{DynamicResource RadiusMd}"/>
    <Setter Property="BorderThickness" Value="0"/>
  </Style>
  <Style Selector="Button.icon:pointerover">
    <Setter Property="Background" Value="{DynamicResource SurfaceSubtleBrush}"/>
    <Setter Property="Foreground" Value="{DynamicResource TextPrimaryBrush}"/>
  </Style>
</Styles>
```

- [ ] **Step 2: TextBox.axaml**

Create `src/Tianming.Desktop.Avalonia/Theme/ControlStyles/TextBox.axaml`:

```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style Selector="TextBox">
    <Setter Property="Background" Value="{DynamicResource SurfaceBaseBrush}"/>
    <Setter Property="Foreground" Value="{DynamicResource TextPrimaryBrush}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource BorderSubtleBrush}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="CornerRadius" Value="{DynamicResource RadiusMd}"/>
    <Setter Property="Padding" Value="{DynamicResource PaddingInputControl}"/>
    <Setter Property="FontFamily" Value="{DynamicResource FontUI}"/>
    <Setter Property="FontSize" Value="{DynamicResource FontSizeBody}"/>
  </Style>
  <Style Selector="TextBox:pointerover /template/ Border#PART_BorderElement">
    <Setter Property="BorderBrush" Value="{DynamicResource BorderStrongBrush}"/>
  </Style>
  <Style Selector="TextBox:focus /template/ Border#PART_BorderElement">
    <Setter Property="BorderBrush" Value="{DynamicResource AccentBaseBrush}"/>
  </Style>
</Styles>
```

- [ ] **Step 3: ComboBox.axaml / ListBox.axaml**

Create `src/Tianming.Desktop.Avalonia/Theme/ControlStyles/ComboBox.axaml`:

```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style Selector="ComboBox">
    <Setter Property="Background" Value="{DynamicResource SurfaceBaseBrush}"/>
    <Setter Property="BorderBrush" Value="{DynamicResource BorderSubtleBrush}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="CornerRadius" Value="{DynamicResource RadiusMd}"/>
    <Setter Property="Padding" Value="{DynamicResource PaddingInputControl}"/>
    <Setter Property="MinHeight" Value="28"/>
  </Style>
</Styles>
```

Create `src/Tianming.Desktop.Avalonia/Theme/ControlStyles/ListBox.axaml`:

```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style Selector="ListBox">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="BorderThickness" Value="0"/>
  </Style>
  <Style Selector="ListBoxItem">
    <Setter Property="Padding" Value="8,6"/>
    <Setter Property="CornerRadius" Value="{DynamicResource RadiusMd}"/>
  </Style>
  <Style Selector="ListBoxItem:pointerover">
    <Setter Property="Background" Value="{DynamicResource SurfaceSubtleBrush}"/>
  </Style>
  <Style Selector="ListBoxItem:selected">
    <Setter Property="Background" Value="{DynamicResource AccentSubtleBrush}"/>
    <Setter Property="Foreground" Value="{DynamicResource AccentPressedBrush}"/>
  </Style>
</Styles>
```

- [ ] **Step 4: DataGrid.axaml**

Create `src/Tianming.Desktop.Avalonia/Theme/ControlStyles/DataGrid.axaml`:

```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style Selector="DataGrid">
    <Setter Property="Background" Value="{DynamicResource SurfaceBaseBrush}"/>
    <Setter Property="GridLinesVisibility" Value="Horizontal"/>
    <Setter Property="HorizontalGridLinesBrush" Value="{DynamicResource BorderSubtleBrush}"/>
    <Setter Property="HeadersVisibility" Value="Column"/>
    <Setter Property="RowBackground" Value="Transparent"/>
    <Setter Property="AlternatingRowBackground" Value="{DynamicResource SurfaceSubtleBrush}"/>
  </Style>
  <Style Selector="DataGridColumnHeader">
    <Setter Property="Background" Value="{DynamicResource SurfaceSubtleBrush}"/>
    <Setter Property="Foreground" Value="{DynamicResource TextSecondaryBrush}"/>
    <Setter Property="FontWeight" Value="{DynamicResource FontWeightSemibold}"/>
    <Setter Property="FontSize" Value="{DynamicResource FontSizeSecondary}"/>
    <Setter Property="Padding" Value="12,8"/>
  </Style>
  <Style Selector="DataGridRow">
    <Setter Property="MinHeight" Value="36"/>
  </Style>
</Styles>
```

- [ ] **Step 5: ScrollBar.axaml — macOS 风薄滚动条**

Create `src/Tianming.Desktop.Avalonia/Theme/ControlStyles/ScrollBar.axaml`:

```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style Selector="ScrollBar:vertical">
    <Setter Property="Width" Value="10"/>
    <Setter Property="MinWidth" Value="10"/>
  </Style>
  <Style Selector="ScrollBar:horizontal">
    <Setter Property="Height" Value="10"/>
    <Setter Property="MinHeight" Value="10"/>
  </Style>
  <Style Selector="ScrollBar /template/ Thumb">
    <Setter Property="Background" Value="{DynamicResource SurfaceMutedBrush}"/>
  </Style>
  <Style Selector="ScrollBar:pointerover /template/ Thumb">
    <Setter Property="Background" Value="{DynamicResource BorderStrongBrush}"/>
  </Style>
</Styles>
```

- [ ] **Step 6: App.axaml 把 6 个 ControlStyles 加进 Application.Styles**

打开 `src/Tianming.Desktop.Avalonia/App.axaml`，把 `<Application.Styles>` 改成：

```xml
<Application.Styles>
  <FluentTheme />
  <StyleInclude Source="avares://Tianming.Desktop.Avalonia/Theme/CommonStyles.axaml"/>
  <StyleInclude Source="avares://Tianming.Desktop.Avalonia/Theme/ControlStyles/Button.axaml"/>
  <StyleInclude Source="avares://Tianming.Desktop.Avalonia/Theme/ControlStyles/TextBox.axaml"/>
  <StyleInclude Source="avares://Tianming.Desktop.Avalonia/Theme/ControlStyles/ComboBox.axaml"/>
  <StyleInclude Source="avares://Tianming.Desktop.Avalonia/Theme/ControlStyles/ListBox.axaml"/>
  <StyleInclude Source="avares://Tianming.Desktop.Avalonia/Theme/ControlStyles/DataGrid.axaml"/>
  <StyleInclude Source="avares://Tianming.Desktop.Avalonia/Theme/ControlStyles/ScrollBar.axaml"/>
</Application.Styles>
```

- [ ] **Step 7: build + 跑全测试 + 冒烟**

```bash
dotnet build Tianming.MacMigration.sln -c Debug --nologo -v q
dotnet test Tianming.MacMigration.sln -c Debug --nologo --no-build -v q 2>&1 | tail -10
dotnet run --project src/Tianming.Desktop.Avalonia -c Debug 2>&1 | head -20
```

Expected：build 通过；测试全过；启动窗口显示新按钮颜色（青色），无异常。

- [ ] **Step 8: commit**

```bash
git add src/Tianming.Desktop.Avalonia/Theme/ControlStyles/ \
        src/Tianming.Desktop.Avalonia/App.axaml
git commit -m "feat(theme): ControlStyles 覆盖 Button/TextBox/ComboBox/ListBox/DataGrid/ScrollBar

- Button 4 个 class（默认 Primary / .secondary / .ghost / .icon）
- TextBox hover / focus 描边
- ComboBox / ListBox 圆角 + accent 选中态
- DataGrid 行高 36、表头副色背景、横条 grid line
- ScrollBar macOS 风 10px 薄条"
```

---

### Task 12: LiveCharts2 bootstrap

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml.cs`

- [ ] **Step 1: 改 App.axaml.cs 在 OnFrameworkInitializationCompleted 之前调 LiveChartsSettings**

打开 `src/Tianming.Desktop.Avalonia/App.axaml.cs`，把整个文件改成：

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;
using System;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.ViewModels;
using Tianming.Desktop.Avalonia.Views;

namespace Tianming.Desktop.Avalonia;

public partial class App : Application
{
    internal static IServiceProvider? Services { get; private set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // 1. LiveCharts2 全局主题：颜色 palette + 中文字体
        ConfigureLiveCharts();

        // 2. AvaloniaEdit 字体 / 高亮初始化
        AvaloniaEditBootstrap.Initialize();

        Services = AppHost.Build();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var lifecycle = Services.GetRequiredService<AppLifecycle>();
            _ = lifecycle.OnStartupAsync();

            var vm = Services.GetRequiredService<MainWindowViewModel>();
            var window = new MainWindow { DataContext = vm };

            var savedState = lifecycle.LoadInitialWindowState();
            window.Width  = savedState.Width;
            window.Height = savedState.Height;
            if (!double.IsNaN(savedState.X) && !double.IsNaN(savedState.Y))
                window.Position = new PixelPoint((int)savedState.X, (int)savedState.Y);
            if (savedState.IsMaximized)
                window.WindowState = global::Avalonia.Controls.WindowState.Maximized;

            window.Closing += (_, _) =>
            {
                var layout = Services!.GetRequiredService<ThreeColumnLayoutViewModel>();
                var state = new WindowState(
                    X: window.Position.X,
                    Y: window.Position.Y,
                    Width: window.Width,
                    Height: window.Height,
                    LeftColumnWidth: layout.LeftColumnWidth,
                    RightColumnWidth: layout.RightColumnWidth,
                    IsMaximized: window.WindowState == global::Avalonia.Controls.WindowState.Maximized);
                lifecycle.SaveWindowState(state);
            };

            desktop.MainWindow = window;
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureLiveCharts()
    {
        LiveCharts.Configure(settings => settings
            .AddDefaultMappers()
            .AddSkiaSharp()
            .HasGlobalSKTypeface(SKFontManager.Default.MatchFamily("PingFang SC")
                                  ?? SKTypeface.Default)
            .AddTheme<SkiaSharpDrawingContext>(theme =>
            {
                theme.Colors = new[]
                {
                    new LiveChartsCore.Drawing.LvcColor(6, 182, 212),      // AccentBase #06B6D4
                    new LiveChartsCore.Drawing.LvcColor(16, 185, 129),     // StatusSuccess #10B981
                    new LiveChartsCore.Drawing.LvcColor(245, 158, 11),     // StatusWarning #F59E0B
                    new LiveChartsCore.Drawing.LvcColor(59, 130, 246),     // StatusInfo #3B82F6
                    new LiveChartsCore.Drawing.LvcColor(148, 163, 184),    // StatusNeutral #94A3B8
                };
            }));
    }
}
```

> 注：LiveCharts2 API 在不同版本间略有差异。如果上面 `AddTheme<SkiaSharpDrawingContext>` 签名不对，按 Task 3 调研得到的版本对应 LiveCharts 文档调整。原则保留：palette = 5 个颜色（accent / success / warning / info / neutral）+ 中文字体。

- [ ] **Step 2: 占位 AvaloniaEditBootstrap.cs（Task 13 实装）**

先创建占位让代码能编：

Create `src/Tianming.Desktop.Avalonia/Infrastructure/AvaloniaEditBootstrap.cs`:

```csharp
namespace Tianming.Desktop.Avalonia.Infrastructure;

internal static class AvaloniaEditBootstrap
{
    public static void Initialize()
    {
        // Task 13 实装：注册 JSON / Markdown 高亮 + 字体
    }
}
```

- [ ] **Step 3: build + 启动确认无异常**

```bash
dotnet build Tianming.MacMigration.sln -c Debug --nologo -v q
dotnet run --project src/Tianming.Desktop.Avalonia -c Debug 2>&1 | head -30
```

Expected：build 通过；启动正常，无 LiveCharts 相关异常输出（窗口里没图，所以 palette 看不出来，但 init 必须不抛）。

- [ ] **Step 4: commit**

```bash
git add src/Tianming.Desktop.Avalonia/App.axaml.cs \
        src/Tianming.Desktop.Avalonia/Infrastructure/AvaloniaEditBootstrap.cs
git commit -m "feat(theme): LiveCharts2 全局主题 + AvaloniaEditBootstrap 占位

- LiveCharts.Configure 设 palette（accent / success / warning / info / neutral）+ PingFang SC
- AvaloniaEditBootstrap 占位等 Task 13 实装"
```

---

### Task 13: AvaloniaEditBootstrap 实装（字体 + JSON/Markdown 高亮）

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/Infrastructure/AvaloniaEditBootstrap.cs`

- [ ] **Step 1: 实装 Initialize**

Replace contents of `src/Tianming.Desktop.Avalonia/Infrastructure/AvaloniaEditBootstrap.cs`:

```csharp
using System.IO;
using System.Reflection;
using System.Xml;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace Tianming.Desktop.Avalonia.Infrastructure;

/// <summary>
/// 在 App.OnFrameworkInitializationCompleted 早期调用一次。
/// 确保 AvaloniaEdit 高亮 / 字体在 CodeViewer / 章节编辑器 (M4.3) 渲染前就绪。
/// </summary>
internal static class AvaloniaEditBootstrap
{
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        // AvaloniaEdit 自带 JSON / Markdown / XML / C# / JS 等高亮，HighlightingManager.Instance 默认已注册。
        // 这里仅触发一次 GetDefinition() 来确认 manager 起来；缺失则 fail-fast。
        var json = HighlightingManager.Instance.GetDefinition("Json")
                   ?? throw new System.InvalidOperationException("AvaloniaEdit 缺少 Json highlighting");
        var md   = HighlightingManager.Instance.GetDefinition("MarkDown")
                   ?? throw new System.InvalidOperationException("AvaloniaEdit 缺少 MarkDown highlighting");
        _ = json;
        _ = md;
    }
}
```

> AvaloniaEdit 自带 highlighting 是 `MarkDown`（大小写敏感）。如果实际版本里名称不一致，按错误信息改。

- [ ] **Step 2: build + 启动确认**

```bash
dotnet build Tianming.MacMigration.sln -c Debug --nologo -v q
dotnet run --project src/Tianming.Desktop.Avalonia -c Debug 2>&1 | head -30
```

Expected：build 通过；启动无异常（高亮 manager 起来）。

- [ ] **Step 3: commit**

```bash
git add src/Tianming.Desktop.Avalonia/Infrastructure/AvaloniaEditBootstrap.cs
git commit -m "feat(theme): AvaloniaEditBootstrap 触发 Json / MarkDown 高亮注册

CodeViewer / M4.3 章节编辑器渲染前确保 HighlightingManager 可用。"
```

---

## Phase 5 / Shell 基础设施接口与实现

### Task 14: IBreadcrumbSource + NavigationBreadcrumbSource

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Infrastructure/IBreadcrumbSource.cs`
- Create: `src/Tianming.Desktop.Avalonia/Infrastructure/NavigationBreadcrumbSource.cs`
- Create: `src/Tianming.Desktop.Avalonia/Shell/BreadcrumbSegment.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/NavigationBreadcrumbSourceTests.cs`

- [ ] **Step 1: 写 BreadcrumbSegment 记录**

Create `src/Tianming.Desktop.Avalonia/Shell/BreadcrumbSegment.cs`:

```csharp
using Tianming.Desktop.Avalonia.Navigation;

namespace Tianming.Desktop.Avalonia.Shell;

public sealed record BreadcrumbSegment(string Label, PageKey? Target);
```

- [ ] **Step 2: 写接口**

Create `src/Tianming.Desktop.Avalonia/Infrastructure/IBreadcrumbSource.cs`:

```csharp
using System;
using System.Collections.Generic;
using Tianming.Desktop.Avalonia.Shell;

namespace Tianming.Desktop.Avalonia.Infrastructure;

public interface IBreadcrumbSource
{
    IReadOnlyList<BreadcrumbSegment> Current { get; }
    event EventHandler<IReadOnlyList<BreadcrumbSegment>>? SegmentsChanged;
}
```

- [ ] **Step 3: 写失败测试**

Create `tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/NavigationBreadcrumbSourceTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.Shell;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Infrastructure;

public class NavigationBreadcrumbSourceTests
{
    private sealed class FakeVm { }
    private sealed class FakeView { }

    private static (NavigationService nav, NavigationBreadcrumbSource src) Build()
    {
        var reg = new PageRegistry();
        reg.Register<FakeVm, FakeView>(PageKeys.Welcome);
        reg.Register<FakeVm, FakeView>(PageKeys.Dashboard);
        reg.Register<FakeVm, FakeView>(PageKeys.Settings);

        var services = new ServiceCollection();
        services.AddTransient<FakeVm>();
        var sp = services.BuildServiceProvider();

        var nav = new NavigationService(sp, reg);
        var src = new NavigationBreadcrumbSource(nav);
        return (nav, src);
    }

    [Fact]
    public void Initial_HasRootSegment()
    {
        var (_, src) = Build();
        Assert.Single(src.Current);
        Assert.Equal("天命", src.Current[0].Label);
    }

    [Fact]
    public async System.Threading.Tasks.Task Navigate_AppendsPageSegment()
    {
        var (nav, src) = Build();
        IReadOnlyList<BreadcrumbSegment>? fired = null;
        src.SegmentsChanged += (_, s) => fired = s;

        await nav.NavigateAsync(PageKeys.Welcome);

        Assert.NotNull(fired);
        Assert.Equal(2, fired!.Count);
        Assert.Equal("天命",  fired[0].Label);
        Assert.Equal("欢迎", fired[1].Label);
    }

    [Fact]
    public async System.Threading.Tasks.Task Navigate_UnknownLabel_FallsBackToPageKeyId()
    {
        var (nav, src) = Build();
        var registry = new PageRegistry();
        registry.Register<FakeVm, FakeView>(new PageKey("uncharted"));
        var services = new ServiceCollection();
        services.AddTransient<FakeVm>();
        var sp = services.BuildServiceProvider();
        var nav2 = new NavigationService(sp, registry);
        var src2 = new NavigationBreadcrumbSource(nav2);

        await nav2.NavigateAsync(new PageKey("uncharted"));

        // 没有内置标签 → 退化到 PageKey.Id
        Assert.Equal("uncharted", src2.Current[1].Label);
    }
}
```

```bash
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ -c Debug --filter "FullyQualifiedName~NavigationBreadcrumbSourceTests" -v q
```

Expected：3 个测试全失败（`NavigationBreadcrumbSource not defined`）。

- [ ] **Step 4: 写实现**

Create `src/Tianming.Desktop.Avalonia/Infrastructure/NavigationBreadcrumbSource.cs`:

```csharp
using System;
using System.Collections.Generic;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.Shell;

namespace Tianming.Desktop.Avalonia.Infrastructure;

public sealed class NavigationBreadcrumbSource : IBreadcrumbSource
{
    private static readonly Dictionary<PageKey, string> KnownLabels = new()
    {
        [PageKeys.Welcome]   = "欢迎",
        [PageKeys.Dashboard] = "仪表盘",
        [PageKeys.Settings]  = "设置",
    };

    private readonly INavigationService _nav;
    private List<BreadcrumbSegment> _current;

    public NavigationBreadcrumbSource(INavigationService nav)
    {
        _nav = nav;
        _current = new List<BreadcrumbSegment> { new("天命", null) };
        _nav.CurrentKeyChanged += OnNavigated;
    }

    public IReadOnlyList<BreadcrumbSegment> Current => _current;

    public event EventHandler<IReadOnlyList<BreadcrumbSegment>>? SegmentsChanged;

    private void OnNavigated(object? sender, PageKey key)
    {
        var label = KnownLabels.TryGetValue(key, out var known) ? known : key.Id;
        _current = new List<BreadcrumbSegment>
        {
            new("天命", null),
            new(label, key)
        };
        SegmentsChanged?.Invoke(this, _current);
    }
}
```

- [ ] **Step 5: 跑测试 + commit**

```bash
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ -c Debug --filter "FullyQualifiedName~NavigationBreadcrumbSourceTests" -v q
```

Expected：3 个测试全过。

```bash
git add src/Tianming.Desktop.Avalonia/Shell/BreadcrumbSegment.cs \
        src/Tianming.Desktop.Avalonia/Infrastructure/IBreadcrumbSource.cs \
        src/Tianming.Desktop.Avalonia/Infrastructure/NavigationBreadcrumbSource.cs \
        tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/NavigationBreadcrumbSourceTests.cs
git commit -m "feat(shell): IBreadcrumbSource + NavigationBreadcrumbSource

订阅 INavigationService.CurrentKeyChanged，维护 [天命, <当前页>] 的 BreadcrumbSegment 列表。
未知 PageKey 退化为 Id 作为 label。3 个测试。"
```

---

### Task 15: IRuntimeInfoProvider + RuntimeInfoProvider

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Infrastructure/IRuntimeInfoProvider.cs`
- Create: `src/Tianming.Desktop.Avalonia/Infrastructure/RuntimeInfoProvider.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/RuntimeInfoProviderTests.cs`

- [ ] **Step 1: 接口 + 实现**

Create `src/Tianming.Desktop.Avalonia/Infrastructure/IRuntimeInfoProvider.cs`:

```csharp
namespace Tianming.Desktop.Avalonia.Infrastructure;

public interface IRuntimeInfoProvider
{
    string FrameworkDescription { get; }
    bool IsLocalMode { get; }
}
```

Create `src/Tianming.Desktop.Avalonia/Infrastructure/RuntimeInfoProvider.cs`:

```csharp
using System.Runtime.InteropServices;

namespace Tianming.Desktop.Avalonia.Infrastructure;

public sealed class RuntimeInfoProvider : IRuntimeInfoProvider
{
    public string FrameworkDescription { get; } = RuntimeInformation.FrameworkDescription;
    public bool IsLocalMode => true; // 自用 mode 锁定
}
```

- [ ] **Step 2: 测试**

Create `tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/RuntimeInfoProviderTests.cs`:

```csharp
using Tianming.Desktop.Avalonia.Infrastructure;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Infrastructure;

public class RuntimeInfoProviderTests
{
    [Fact]
    public void FrameworkDescription_StartsWith_DotNet()
    {
        var p = new RuntimeInfoProvider();
        Assert.StartsWith(".NET", p.FrameworkDescription);
    }

    [Fact]
    public void IsLocalMode_AlwaysTrue()
    {
        var p = new RuntimeInfoProvider();
        Assert.True(p.IsLocalMode);
    }
}
```

```bash
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ -c Debug --filter "FullyQualifiedName~RuntimeInfoProviderTests" -v q
```

Expected：2 测试通过。

- [ ] **Step 3: commit**

```bash
git add src/Tianming.Desktop.Avalonia/Infrastructure/IRuntimeInfoProvider.cs \
        src/Tianming.Desktop.Avalonia/Infrastructure/RuntimeInfoProvider.cs \
        tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/RuntimeInfoProviderTests.cs
git commit -m "feat(shell): IRuntimeInfoProvider + RuntimeInfoProvider

启动时取 RuntimeInformation.FrameworkDescription；IsLocalMode 永远 true（自用）。"
```

---

### Task 16: StatusIndicator + StatusKind + IKeychainHealthProbe + KeychainHealthProbe

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Shell/StatusIndicator.cs`
- Create: `src/Tianming.Desktop.Avalonia/Infrastructure/IKeychainHealthProbe.cs`
- Create: `src/Tianming.Desktop.Avalonia/Infrastructure/KeychainHealthProbe.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/KeychainHealthProbeTests.cs`

- [ ] **Step 1: 共享类型**

Create `src/Tianming.Desktop.Avalonia/Shell/StatusIndicator.cs`:

```csharp
namespace Tianming.Desktop.Avalonia.Shell;

public enum StatusKind { Success, Warning, Danger, Info, Neutral }

public sealed record StatusIndicator(string Label, StatusKind Kind, string? Tooltip = null);
```

- [ ] **Step 2: 接口**

Create `src/Tianming.Desktop.Avalonia/Infrastructure/IKeychainHealthProbe.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using Tianming.Desktop.Avalonia.Shell;

namespace Tianming.Desktop.Avalonia.Infrastructure;

public interface IKeychainHealthProbe
{
    Task<StatusIndicator> ProbeAsync(CancellationToken ct = default);
}
```

- [ ] **Step 3: 写测试（先红）**

Create `tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/KeychainHealthProbeTests.cs`:

```csharp
using System.Threading.Tasks;
using TM.Services.Framework.AI.Core;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Shell;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Infrastructure;

public class KeychainHealthProbeTests
{
    private sealed class FakeRunner_Ok : ISecurityCommandRunner
    {
        public SecurityCommandResult Run(string fileName, System.Collections.Generic.IReadOnlyList<string> arguments)
            => new(ExitCode: 44, StandardOutput: string.Empty, StandardError: "item not in keychain.");
        // exit 44 = not found 但 security tool 自身可用 → keychain 工作正常
    }

    private sealed class FakeRunner_NoTool : ISecurityCommandRunner
    {
        public SecurityCommandResult Run(string fileName, System.Collections.Generic.IReadOnlyList<string> arguments)
            => throw new System.IO.FileNotFoundException("/usr/bin/security not found");
    }

    [Fact]
    public async Task ProbeAsync_ToolAvailable_ReturnsSuccess()
    {
        var store = new MacOSKeychainApiKeySecretStore(new FakeRunner_Ok(), "tianming-test");
        var probe = new KeychainHealthProbe(store);
        var status = await probe.ProbeAsync();
        Assert.Equal(StatusKind.Success, status.Kind);
        Assert.Contains("Keychain", status.Label);
    }

    [Fact]
    public async Task ProbeAsync_ToolMissing_ReturnsDanger()
    {
        var store = new MacOSKeychainApiKeySecretStore(new FakeRunner_NoTool(), "tianming-test");
        var probe = new KeychainHealthProbe(store);
        var status = await probe.ProbeAsync();
        Assert.Equal(StatusKind.Danger, status.Kind);
    }
}
```

```bash
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ -c Debug --filter "FullyQualifiedName~KeychainHealthProbeTests" -v q
```

Expected：fail（`KeychainHealthProbe not defined`）。

- [ ] **Step 4: 写实现**

Create `src/Tianming.Desktop.Avalonia/Infrastructure/KeychainHealthProbe.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Framework.AI.Core;
using Tianming.Desktop.Avalonia.Shell;

namespace Tianming.Desktop.Avalonia.Infrastructure;

public sealed class KeychainHealthProbe : IKeychainHealthProbe
{
    private const string ProbeKey = "__tianming_health_probe__";
    private readonly IApiKeySecretStore _store;

    public KeychainHealthProbe(IApiKeySecretStore store) { _store = store; }

    public Task<StatusIndicator> ProbeAsync(CancellationToken ct = default)
    {
        try
        {
            // 调一次 GetSecret 来确认 security tool 可用
            // 返回 null（未找到）也算可用，只要不抛异常
            _ = _store.GetSecret(ProbeKey);
            return Task.FromResult(new StatusIndicator("Keychain", StatusKind.Success, "macOS Keychain 可用"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new StatusIndicator("Keychain", StatusKind.Danger, $"Keychain 不可用：{ex.Message}"));
        }
    }
}
```

- [ ] **Step 5: 跑测试 + commit**

```bash
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ -c Debug --filter "FullyQualifiedName~KeychainHealthProbeTests" -v q
```

Expected：2 测试过。

```bash
git add src/Tianming.Desktop.Avalonia/Shell/StatusIndicator.cs \
        src/Tianming.Desktop.Avalonia/Infrastructure/IKeychainHealthProbe.cs \
        src/Tianming.Desktop.Avalonia/Infrastructure/KeychainHealthProbe.cs \
        tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/KeychainHealthProbeTests.cs
git commit -m "feat(shell): StatusIndicator + IKeychainHealthProbe 探测 macOS Keychain

调一次 IApiKeySecretStore.GetSecret(\"__tianming_health_probe__\")，无异常 = Success；抛异常 = Danger。
2 个测试覆盖 (toolAvailable / toolMissing)。"
```

---

### Task 17: IOnnxHealthProbe + OnnxHealthProbe

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Infrastructure/IOnnxHealthProbe.cs`
- Create: `src/Tianming.Desktop.Avalonia/Infrastructure/OnnxHealthProbe.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/OnnxHealthProbeTests.cs`

- [ ] **Step 1: 接口 + 实现**

Create `src/Tianming.Desktop.Avalonia/Infrastructure/IOnnxHealthProbe.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using Tianming.Desktop.Avalonia.Shell;

namespace Tianming.Desktop.Avalonia.Infrastructure;

public interface IOnnxHealthProbe
{
    Task<StatusIndicator> ProbeAsync(CancellationToken ct = default);
}
```

Create `src/Tianming.Desktop.Avalonia/Infrastructure/OnnxHealthProbe.cs`:

```csharp
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Framework.AI.SemanticKernel.Embedding;
using Tianming.Desktop.Avalonia.Shell;

namespace Tianming.Desktop.Avalonia.Infrastructure;

public sealed class OnnxHealthProbe : IOnnxHealthProbe
{
    private readonly EmbeddingSettings _settings;

    public OnnxHealthProbe(EmbeddingSettings settings) { _settings = settings; }

    public Task<StatusIndicator> ProbeAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ModelFilePath) ||
            string.IsNullOrWhiteSpace(_settings.VocabFilePath))
        {
            return Task.FromResult(new StatusIndicator(
                "ONNX", StatusKind.Info, "ONNX 模型未配置，向量化用 Hashing 降级"));
        }

        if (!File.Exists(_settings.ModelFilePath))
            return Task.FromResult(new StatusIndicator(
                "ONNX", StatusKind.Warning, $"模型文件不存在：{_settings.ModelFilePath}"));

        if (!File.Exists(_settings.VocabFilePath))
            return Task.FromResult(new StatusIndicator(
                "ONNX", StatusKind.Warning, $"词表文件不存在：{_settings.VocabFilePath}"));

        return Task.FromResult(new StatusIndicator(
            "ONNX", StatusKind.Success, $"模型 {Path.GetFileName(_settings.ModelFilePath)}"));
    }
}
```

- [ ] **Step 2: 测试**

Create `tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/OnnxHealthProbeTests.cs`:

```csharp
using System.IO;
using System.Threading.Tasks;
using TM.Services.Framework.AI.SemanticKernel.Embedding;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Shell;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Infrastructure;

public class OnnxHealthProbeTests
{
    [Fact]
    public async Task ProbeAsync_NoModelConfigured_ReturnsInfoOptional()
    {
        var settings = EmbeddingSettings.Default; // ModelFilePath / VocabFilePath 都 null
        var probe = new OnnxHealthProbe(settings);
        var status = await probe.ProbeAsync();
        Assert.Equal(StatusKind.Info, status.Kind);
    }

    [Fact]
    public async Task ProbeAsync_ModelMissing_ReturnsWarning()
    {
        var settings = EmbeddingSettings.Default with
        {
            ModelFilePath = "/nonexistent/model.onnx",
            VocabFilePath = "/nonexistent/vocab.txt"
        };
        var probe = new OnnxHealthProbe(settings);
        var status = await probe.ProbeAsync();
        Assert.Equal(StatusKind.Warning, status.Kind);
    }

    [Fact]
    public async Task ProbeAsync_BothExist_ReturnsSuccess()
    {
        var tmp = Path.GetTempPath();
        var model = Path.Combine(tmp, $"probe-{System.Guid.NewGuid():N}.onnx");
        var vocab = Path.Combine(tmp, $"probe-{System.Guid.NewGuid():N}.txt");
        File.WriteAllText(model, "");
        File.WriteAllText(vocab, "");
        try
        {
            var settings = EmbeddingSettings.Default with { ModelFilePath = model, VocabFilePath = vocab };
            var probe = new OnnxHealthProbe(settings);
            var status = await probe.ProbeAsync();
            Assert.Equal(StatusKind.Success, status.Kind);
        }
        finally
        {
            File.Delete(model);
            File.Delete(vocab);
        }
    }
}
```

```bash
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ -c Debug --filter "FullyQualifiedName~OnnxHealthProbeTests" -v q
```

Expected：3 测试过。

- [ ] **Step 3: commit**

```bash
git add src/Tianming.Desktop.Avalonia/Infrastructure/IOnnxHealthProbe.cs \
        src/Tianming.Desktop.Avalonia/Infrastructure/OnnxHealthProbe.cs \
        tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/OnnxHealthProbeTests.cs
git commit -m "feat(shell): IOnnxHealthProbe + OnnxHealthProbe

- 未配置模型 → Info（Hashing 降级，正常）
- 配置了但文件不存在 → Warning
- 配置正确 → Success
3 测试覆盖。"
```

---

## Phase 6 / Shell ViewModels

### Task 18: AppChromeViewModel + tests

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Shell/AppChromeViewModel.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Shell/AppChromeViewModelTests.cs`

- [ ] **Step 1: 写 VM**

Create `src/Tianming.Desktop.Avalonia/Shell/AppChromeViewModel.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Navigation;

namespace Tianming.Desktop.Avalonia.Shell;

public partial class AppChromeViewModel : ObservableObject
{
    private readonly IBreadcrumbSource _source;
    private readonly INavigationService _nav;

    public ObservableCollection<BreadcrumbSegment> Segments { get; } = new();

    public AppChromeViewModel(IBreadcrumbSource source, INavigationService nav)
    {
        _source = source;
        _nav = nav;
        ApplySegments(_source.Current);
        _source.SegmentsChanged += (_, list) => ApplySegments(list);
    }

    private void ApplySegments(IReadOnlyList<BreadcrumbSegment> list)
    {
        Segments.Clear();
        foreach (var s in list) Segments.Add(s);
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task NavigateAsync(BreadcrumbSegment? segment)
    {
        if (segment?.Target is not { } key) return;
        await _nav.NavigateAsync(key);
    }
}
```

- [ ] **Step 2: 测试**

Create `tests/Tianming.Desktop.Avalonia.Tests/Shell/AppChromeViewModelTests.cs`:

```csharp
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.Shell;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Shell;

public class AppChromeViewModelTests
{
    private sealed class FakeVm { }
    private sealed class FakeView { }

    private static (NavigationService nav, AppChromeViewModel vm) Build()
    {
        var reg = new PageRegistry();
        reg.Register<FakeVm, FakeView>(PageKeys.Welcome);
        reg.Register<FakeVm, FakeView>(PageKeys.Dashboard);
        var services = new ServiceCollection();
        services.AddTransient<FakeVm>();
        var sp = services.BuildServiceProvider();
        var nav = new NavigationService(sp, reg);
        var src = new NavigationBreadcrumbSource(nav);
        return (nav, new AppChromeViewModel(src, nav));
    }

    [Fact]
    public void Initial_SegmentsHasRoot()
    {
        var (_, vm) = Build();
        Assert.Single(vm.Segments);
        Assert.Equal("天命", vm.Segments[0].Label);
    }

    [Fact]
    public async Task Navigate_UpdatesSegments()
    {
        var (nav, vm) = Build();
        await nav.NavigateAsync(PageKeys.Dashboard);
        Assert.Equal(2, vm.Segments.Count);
        Assert.Equal("仪表盘", vm.Segments[1].Label);
    }

    [Fact]
    public async Task NavigateCommand_TargetNull_NoOp()
    {
        var (_, vm) = Build();
        await vm.NavigateCommand.ExecuteAsync(new BreadcrumbSegment("天命", null));
        Assert.Single(vm.Segments);
    }
}
```

```bash
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ -c Debug --filter "FullyQualifiedName~AppChromeViewModelTests" -v q
```

Expected：3 测试过。

- [ ] **Step 3: commit**

```bash
git add src/Tianming.Desktop.Avalonia/Shell/AppChromeViewModel.cs \
        tests/Tianming.Desktop.Avalonia.Tests/Shell/AppChromeViewModelTests.cs
git commit -m "feat(shell): AppChromeViewModel — breadcrumb Segments + NavigateCommand

订阅 IBreadcrumbSource.SegmentsChanged，把 List 投射到 ObservableCollection；
点击 segment 调 NavigateAsync(Target)；Target=null（root）no-op。"
```

---

### Task 19: AppStatusBarViewModel + tests

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Shell/AppStatusBarViewModel.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Shell/AppStatusBarViewModelTests.cs`

- [ ] **Step 1: 写 VM**

Create `src/Tianming.Desktop.Avalonia/Shell/AppStatusBarViewModel.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Tianming.Desktop.Avalonia.Infrastructure;

namespace Tianming.Desktop.Avalonia.Shell;

public partial class AppStatusBarViewModel : ObservableObject
{
    private readonly IKeychainHealthProbe _keychainProbe;
    private readonly IOnnxHealthProbe _onnxProbe;

    [ObservableProperty] private string _dotNetRuntime;
    [ObservableProperty] private StatusIndicator _localMode;
    [ObservableProperty] private StatusIndicator _keychainStatus;
    [ObservableProperty] private StatusIndicator _onnxStatus;
    [ObservableProperty] private string? _currentProjectPath;

    public AppStatusBarViewModel(
        IRuntimeInfoProvider runtime,
        IKeychainHealthProbe keychainProbe,
        IOnnxHealthProbe onnxProbe)
    {
        _keychainProbe = keychainProbe;
        _onnxProbe = onnxProbe;

        _dotNetRuntime = runtime.FrameworkDescription;
        _localMode = new StatusIndicator("本地写作模式", StatusKind.Success);
        _keychainStatus = new StatusIndicator("Keychain", StatusKind.Neutral, "检测中…");
        _onnxStatus = new StatusIndicator("ONNX", StatusKind.Neutral, "检测中…");
    }

    /// <summary>由 App 启动时 fire-and-forget 调用。</summary>
    public async Task RefreshProbesAsync(CancellationToken ct = default)
    {
        try { KeychainStatus = await _keychainProbe.ProbeAsync(ct); }
        catch (Exception ex) { KeychainStatus = new StatusIndicator("Keychain", StatusKind.Danger, ex.Message); }

        try { OnnxStatus = await _onnxProbe.ProbeAsync(ct); }
        catch (Exception ex) { OnnxStatus = new StatusIndicator("ONNX", StatusKind.Danger, ex.Message); }
    }
}
```

- [ ] **Step 2: 测试**

Create `tests/Tianming.Desktop.Avalonia.Tests/Shell/AppStatusBarViewModelTests.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Shell;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Shell;

public class AppStatusBarViewModelTests
{
    private sealed class FakeRuntime : IRuntimeInfoProvider
    {
        public string FrameworkDescription => ".NET 8.0.test";
        public bool IsLocalMode => true;
    }

    private sealed class FakeKeyProbe : IKeychainHealthProbe
    {
        private readonly StatusIndicator _r;
        public FakeKeyProbe(StatusIndicator r) { _r = r; }
        public Task<StatusIndicator> ProbeAsync(CancellationToken ct = default) => Task.FromResult(_r);
    }

    private sealed class FakeOnnxProbe : IOnnxHealthProbe
    {
        private readonly StatusIndicator _r;
        public FakeOnnxProbe(StatusIndicator r) { _r = r; }
        public Task<StatusIndicator> ProbeAsync(CancellationToken ct = default) => Task.FromResult(_r);
    }

    [Fact]
    public void Constructor_InitializesImmediateFields()
    {
        var vm = new AppStatusBarViewModel(
            new FakeRuntime(),
            new FakeKeyProbe(new StatusIndicator("X", StatusKind.Success)),
            new FakeOnnxProbe(new StatusIndicator("Y", StatusKind.Info)));
        Assert.Equal(".NET 8.0.test", vm.DotNetRuntime);
        Assert.Equal(StatusKind.Success, vm.LocalMode.Kind);
        Assert.Equal(StatusKind.Neutral, vm.KeychainStatus.Kind);
        Assert.Equal(StatusKind.Neutral, vm.OnnxStatus.Kind);
    }

    [Fact]
    public async Task RefreshProbes_PopulatesStatuses()
    {
        var vm = new AppStatusBarViewModel(
            new FakeRuntime(),
            new FakeKeyProbe(new StatusIndicator("Keychain", StatusKind.Success)),
            new FakeOnnxProbe(new StatusIndicator("ONNX", StatusKind.Info)));
        await vm.RefreshProbesAsync();
        Assert.Equal(StatusKind.Success, vm.KeychainStatus.Kind);
        Assert.Equal(StatusKind.Info, vm.OnnxStatus.Kind);
    }

    [Fact]
    public async Task RefreshProbes_ProbeThrows_SetDanger()
    {
        var vm = new AppStatusBarViewModel(
            new FakeRuntime(),
            new ThrowingProbe(),
            new FakeOnnxProbe(new StatusIndicator("ONNX", StatusKind.Info)));
        await vm.RefreshProbesAsync();
        Assert.Equal(StatusKind.Danger, vm.KeychainStatus.Kind);
    }

    private sealed class ThrowingProbe : IKeychainHealthProbe
    {
        public Task<StatusIndicator> ProbeAsync(CancellationToken ct = default)
            => throw new System.InvalidOperationException("boom");
    }
}
```

```bash
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ -c Debug --filter "FullyQualifiedName~AppStatusBarViewModelTests" -v q
```

Expected：3 测试过。

- [ ] **Step 3: commit**

```bash
git add src/Tianming.Desktop.Avalonia/Shell/AppStatusBarViewModel.cs \
        tests/Tianming.Desktop.Avalonia.Tests/Shell/AppStatusBarViewModelTests.cs
git commit -m "feat(shell): AppStatusBarViewModel — runtime / 本地模式 / Keychain / ONNX 状态

构造时同步取 RuntimeInfoProvider 和 LocalMode；Keychain / ONNX 字段先 Neutral，
RefreshProbesAsync 异步填充；probe 抛异常时记录为 Danger。3 测试。"
```

---

### Task 20: MainWindowViewModel 扩展（注入 Chrome + StatusBar）

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/ViewModels/MainWindowViewModel.cs`

- [ ] **Step 1: 改 VM 加 Chrome + StatusBar 属性**

Replace `src/Tianming.Desktop.Avalonia/ViewModels/MainWindowViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using Tianming.Desktop.Avalonia.Shell;

namespace Tianming.Desktop.Avalonia.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty] private string _title = "天命";
    [ObservableProperty] private ThreeColumnLayoutViewModel _layout;
    [ObservableProperty] private AppChromeViewModel _chrome;
    [ObservableProperty] private AppStatusBarViewModel _statusBar;

    public MainWindowViewModel(
        ThreeColumnLayoutViewModel layout,
        AppChromeViewModel chrome,
        AppStatusBarViewModel statusBar)
    {
        _layout = layout;
        _chrome = chrome;
        _statusBar = statusBar;
    }
}
```

- [ ] **Step 2: build 让现有 AppHostTests 暂时失败，准备 Task 23 修 DI**

```bash
dotnet build Tianming.MacMigration.sln -c Debug --nologo -v q
```

Expected：build 通过（编译没问题），但 `dotnet test` 跑 `AppHostTests.Build_ResolvesMainWindowViewModel` 会因为 DI 缺 `AppChromeViewModel` / `AppStatusBarViewModel` 而失败。这是预期的，会在 Task 23 修。

- [ ] **Step 3: commit**

```bash
git add src/Tianming.Desktop.Avalonia/ViewModels/MainWindowViewModel.cs
git commit -m "feat(shell): MainWindowViewModel 新增 Chrome + StatusBar 属性

ctor 注入 AppChromeViewModel + AppStatusBarViewModel；
DI 注册在 Task 23 修，暂时 AppHostTests 会红。"
```

---

## Phase 7 / Shell Views（AppChrome + AppStatusBar + MainWindow + NativeMenu）

### Task 21: AppChrome.axaml + .cs

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Shell/AppChrome.axaml`
- Create: `src/Tianming.Desktop.Avalonia/Shell/AppChrome.axaml.cs`

- [ ] **Step 1: 写 axaml**

Create `src/Tianming.Desktop.Avalonia/Shell/AppChrome.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:shell="using:Tianming.Desktop.Avalonia.Shell"
             x:Class="Tianming.Desktop.Avalonia.Shell.AppChrome"
             x:DataType="shell:AppChromeViewModel"
             Height="36">
  <Border Background="{DynamicResource SurfaceBaseBrush}"
          BorderBrush="{DynamicResource BorderSubtleBrush}"
          BorderThickness="0,0,0,1">
    <Grid>
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="78"/>  <!-- macOS traffic light 留位 -->
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="Auto"/>
        <ColumnDefinition Width="12"/>
      </Grid.ColumnDefinitions>

      <!-- Breadcrumb 居中区 -->
      <ItemsControl Grid.Column="1" ItemsSource="{Binding Segments}"
                    VerticalAlignment="Center">
        <ItemsControl.ItemsPanel>
          <ItemsPanelTemplate>
            <StackPanel Orientation="Horizontal" Spacing="6"/>
          </ItemsPanelTemplate>
        </ItemsControl.ItemsPanel>
        <ItemsControl.ItemTemplate>
          <DataTemplate DataType="shell:BreadcrumbSegment">
            <StackPanel Orientation="Horizontal" Spacing="6">
              <Button Classes="ghost"
                      Command="{Binding $parent[ItemsControl].((shell:AppChromeViewModel)DataContext).NavigateCommand}"
                      CommandParameter="{Binding}"
                      Padding="4,2"
                      IsHitTestVisible="{Binding Target, Converter={x:Static ObjectConverters.IsNotNull}}">
                <TextBlock Text="{Binding Label}"
                           FontSize="{DynamicResource FontSizeCaption}"
                           Foreground="{DynamicResource TextSecondaryBrush}"/>
              </Button>
              <TextBlock Text=" · "
                         FontSize="{DynamicResource FontSizeCaption}"
                         Foreground="{DynamicResource TextTertiaryBrush}"
                         VerticalAlignment="Center"/>
            </StackPanel>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>

      <!-- 右侧 icon 按钮组（搜索 / 通知 / 用户）—— 暂用占位文本，Sub-plan 2 接 Lucide icon -->
      <StackPanel Grid.Column="2" Orientation="Horizontal" Spacing="4" VerticalAlignment="Center">
        <Button Classes="icon" ToolTip.Tip="搜索">🔍</Button>
        <Button Classes="icon" ToolTip.Tip="通知">🔔</Button>
        <Button Classes="icon" ToolTip.Tip="用户">👤</Button>
      </StackPanel>
    </Grid>
  </Border>
</UserControl>
```

- [ ] **Step 2: 写 .cs**

Create `src/Tianming.Desktop.Avalonia/Shell/AppChrome.axaml.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Tianming.Desktop.Avalonia.Shell;

public partial class AppChrome : UserControl
{
    public AppChrome() => InitializeComponent();
}
```

- [ ] **Step 3: build 验证 XAML 合法**

```bash
dotnet build Tianming.MacMigration.sln -c Debug --nologo -v q
```

Expected：build 通过（视图未注入，编译 OK；运行测试 AppHostTests 仍未通过，但不影响其它）。

- [ ] **Step 4: commit**

```bash
git add src/Tianming.Desktop.Avalonia/Shell/AppChrome.axaml \
        src/Tianming.Desktop.Avalonia/Shell/AppChrome.axaml.cs
git commit -m "feat(shell): AppChrome.axaml — 36px 标题栏（78px traffic light 留位 + breadcrumb + icon 按钮组）

breadcrumb 用 ItemsControl + DataTemplate 渲染；点击非 root segment 走 NavigateCommand；
右侧 emoji 占位（搜索 / 通知 / 用户），Sub-plan 2 替换为 Lucide icon。"
```

---

### Task 22: AppStatusBar.axaml + .cs

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Shell/AppStatusBar.axaml`
- Create: `src/Tianming.Desktop.Avalonia/Shell/AppStatusBar.axaml.cs`

- [ ] **Step 1: 写 axaml**

Create `src/Tianming.Desktop.Avalonia/Shell/AppStatusBar.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:shell="using:Tianming.Desktop.Avalonia.Shell"
             x:Class="Tianming.Desktop.Avalonia.Shell.AppStatusBar"
             x:DataType="shell:AppStatusBarViewModel"
             Height="28">
  <Border Background="{DynamicResource SurfaceSubtleBrush}"
          BorderBrush="{DynamicResource BorderSubtleBrush}"
          BorderThickness="0,1,0,0">
    <Grid Margin="12,0">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto"/>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="Auto"/>
      </Grid.ColumnDefinitions>

      <!-- 左侧 indicators -->
      <StackPanel Grid.Column="0" Orientation="Horizontal" Spacing="12" VerticalAlignment="Center">
        <TextBlock Text="{Binding DotNetRuntime}"
                   FontSize="{DynamicResource FontSizeCaption}"
                   Foreground="{DynamicResource TextSecondaryBrush}"
                   VerticalAlignment="Center"/>

        <!-- 占位用 TextBlock 表示 status；Task 25 起 StatusBarItem primitive 接管 -->
        <TextBlock Text="{Binding LocalMode.Label}"
                   FontSize="{DynamicResource FontSizeCaption}"
                   Foreground="{DynamicResource StatusSuccessBrush}"
                   VerticalAlignment="Center"/>
        <TextBlock Text="{Binding KeychainStatus.Label}"
                   FontSize="{DynamicResource FontSizeCaption}"
                   Foreground="{DynamicResource TextSecondaryBrush}"
                   VerticalAlignment="Center"
                   ToolTip.Tip="{Binding KeychainStatus.Tooltip}"/>
        <TextBlock Text="{Binding OnnxStatus.Label}"
                   FontSize="{DynamicResource FontSizeCaption}"
                   Foreground="{DynamicResource TextSecondaryBrush}"
                   VerticalAlignment="Center"
                   ToolTip.Tip="{Binding OnnxStatus.Tooltip}"/>
      </StackPanel>

      <!-- 右侧项目路径 -->
      <TextBlock Grid.Column="2"
                 Text="{Binding CurrentProjectPath}"
                 FontSize="{DynamicResource FontSizeCaption}"
                 Foreground="{DynamicResource TextTertiaryBrush}"
                 VerticalAlignment="Center"
                 IsVisible="{Binding CurrentProjectPath, Converter={x:Static ObjectConverters.IsNotNull}}"/>
    </Grid>
  </Border>
</UserControl>
```

> 这里 status 渲染先用 TextBlock 占位，Task 25 起把 `StatusBarItem` primitive 接进来。

- [ ] **Step 2: .cs**

Create `src/Tianming.Desktop.Avalonia/Shell/AppStatusBar.axaml.cs`:

```csharp
using Avalonia.Controls;

namespace Tianming.Desktop.Avalonia.Shell;

public partial class AppStatusBar : UserControl
{
    public AppStatusBar() => InitializeComponent();
}
```

- [ ] **Step 3: commit**

```bash
dotnet build Tianming.MacMigration.sln -c Debug --nologo -v q
git add src/Tianming.Desktop.Avalonia/Shell/AppStatusBar.axaml \
        src/Tianming.Desktop.Avalonia/Shell/AppStatusBar.axaml.cs
git commit -m "feat(shell): AppStatusBar.axaml — 28px 底栏（runtime + 本地模式 + Keychain + ONNX + 项目路径）

status 字段先用 TextBlock 占位；Task 25 起 StatusBarItem primitive 接管。"
```

---

### Task 23: MainWindow.axaml 重写（ExtendClientArea + Chrome + StatusBar）

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/Views/MainWindow.axaml`

- [ ] **Step 1: 重写**

Replace `src/Tianming.Desktop.Avalonia/Views/MainWindow.axaml`:

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:Tianming.Desktop.Avalonia.ViewModels"
        xmlns:v="using:Tianming.Desktop.Avalonia.Views"
        xmlns:shell="using:Tianming.Desktop.Avalonia.Shell"
        x:Class="Tianming.Desktop.Avalonia.Views.MainWindow"
        x:DataType="vm:MainWindowViewModel"
        Title="{Binding Title}"
        Width="1280" Height="820"
        MinWidth="960" MinHeight="600"
        Background="{DynamicResource SurfaceCanvasBrush}"
        ExtendClientAreaToDecorationsHint="True"
        ExtendClientAreaChromeHints="PreferSystemChrome"
        ExtendClientAreaTitleBarHeightHint="36">
  <Grid RowDefinitions="36, *, 28">
    <shell:AppChrome    Grid.Row="0" DataContext="{Binding Chrome}"/>
    <v:ThreeColumnLayoutView Grid.Row="1" DataContext="{Binding Layout}"/>
    <shell:AppStatusBar Grid.Row="2" DataContext="{Binding StatusBar}"/>
  </Grid>
</Window>
```

- [ ] **Step 2: build 验证（运行时还会因 DI 缺失炸，Task 24 修）**

```bash
dotnet build Tianming.MacMigration.sln -c Debug --nologo -v q
```

Expected：build 通过。

- [ ] **Step 3: commit**

```bash
git add src/Tianming.Desktop.Avalonia/Views/MainWindow.axaml
git commit -m "feat(shell): MainWindow.axaml 重写为 ExtendClientArea + 36/auto/28 三段 Grid

- ExtendClientAreaToDecorationsHint=True + PreferSystemChrome + TitleBarHeightHint=36
- Row 0: AppChrome（自定义 breadcrumb 顶栏）
- Row 1: 原 ThreeColumnLayoutView（保持现有 M3 三栏）
- Row 2: AppStatusBar（28px 底部状态栏）"
```

---

### Task 24: App.axaml NativeMenu stub

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml`

- [ ] **Step 1: 加 NativeMenu**

把 `src/Tianming.Desktop.Avalonia/App.axaml` 整体改为（注意 NativeMenu 必须挂在 Application 下作为附加属性）：

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="Tianming.Desktop.Avalonia.App"
             RequestedThemeVariant="Light">

  <NativeMenu.Menu>
    <NativeMenu>
      <NativeMenuItem Header="天命">
        <NativeMenu>
          <NativeMenuItem Header="关于天命"/>
          <NativeMenuItemSeparator/>
          <NativeMenuItem Header="偏好…" Gesture="Cmd+OemComma"/>
          <NativeMenuItemSeparator/>
          <NativeMenuItem Header="隐藏天命" Gesture="Cmd+H"/>
          <NativeMenuItem Header="退出"     Gesture="Cmd+Q"/>
        </NativeMenu>
      </NativeMenuItem>
      <NativeMenuItem Header="文件">
        <NativeMenu>
          <NativeMenuItem Header="新建项目…" Gesture="Cmd+N"/>
          <NativeMenuItem Header="打开项目…" Gesture="Cmd+O"/>
          <NativeMenuItemSeparator/>
          <NativeMenuItem Header="保存"     Gesture="Cmd+S"/>
        </NativeMenu>
      </NativeMenuItem>
      <NativeMenuItem Header="编辑">
        <NativeMenu>
          <NativeMenuItem Header="撤销" Gesture="Cmd+Z"/>
          <NativeMenuItem Header="重做" Gesture="Cmd+Shift+Z"/>
          <NativeMenuItemSeparator/>
          <NativeMenuItem Header="查找" Gesture="Cmd+F"/>
        </NativeMenu>
      </NativeMenuItem>
      <NativeMenuItem Header="视图"/>
      <NativeMenuItem Header="窗口"/>
      <NativeMenuItem Header="帮助"/>
    </NativeMenu>
  </NativeMenu.Menu>

  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Theme/DesignTokens/Colors.axaml"/>
        <ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Theme/DesignTokens/Typography.axaml"/>
        <ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Theme/DesignTokens/Spacing.axaml"/>
        <ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Theme/DesignTokens/Radii.axaml"/>
        <ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Theme/DesignTokens/Shadows.axaml"/>
        <ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Theme/PalettesLight.axaml"/>
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>

  <Application.Styles>
    <FluentTheme/>
    <StyleInclude Source="avares://Tianming.Desktop.Avalonia/Theme/CommonStyles.axaml"/>
    <StyleInclude Source="avares://Tianming.Desktop.Avalonia/Theme/ControlStyles/Button.axaml"/>
    <StyleInclude Source="avares://Tianming.Desktop.Avalonia/Theme/ControlStyles/TextBox.axaml"/>
    <StyleInclude Source="avares://Tianming.Desktop.Avalonia/Theme/ControlStyles/ComboBox.axaml"/>
    <StyleInclude Source="avares://Tianming.Desktop.Avalonia/Theme/ControlStyles/ListBox.axaml"/>
    <StyleInclude Source="avares://Tianming.Desktop.Avalonia/Theme/ControlStyles/DataGrid.axaml"/>
    <StyleInclude Source="avares://Tianming.Desktop.Avalonia/Theme/ControlStyles/ScrollBar.axaml"/>
  </Application.Styles>
</Application>
```

- [ ] **Step 2: build + 启动后检查 macOS 菜单栏**

```bash
dotnet build Tianming.MacMigration.sln -c Debug --nologo -v q
```

DI 还差最后一步（Task 25），所以应用此时还跑不起来。但 build 必须过：

Expected：build 通过。

- [ ] **Step 3: commit**

```bash
git add src/Tianming.Desktop.Avalonia/App.axaml
git commit -m "feat(shell): App.axaml NativeMenu stub — 6 个一级菜单

天命 / 文件 / 编辑 / 视图 / 窗口 / 帮助；命令绑定留 M5 plan；
快捷键文字（Cmd+N/O/S/Z/H/Q/逗号）在 macOS 系统菜单栏显示。"
```

---

## Phase 8 / DI 装配 + AppHostTests 修

### Task 25: AvaloniaShellServiceCollectionExtensions 注册基建服务 + 修测试

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs`
- Modify: `tests/Tianming.Desktop.Avalonia.Tests/DI/AppHostTests.cs`

- [ ] **Step 1: 扩展 DI**

Replace contents of `src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using TM.Services.Framework.AI.SemanticKernel.Embedding;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.Shell;
using Tianming.Desktop.Avalonia.Views;
using Tianming.Desktop.Avalonia.Views.Shell;
using Tianming.Desktop.Avalonia.ViewModels;
using Tianming.Desktop.Avalonia.ViewModels.Shell;
using Tianming.Framework.Appearance.Portable;

namespace Tianming.Desktop.Avalonia;

public static class AvaloniaShellServiceCollectionExtensions
{
    public static IServiceCollection AddAvaloniaShell(this IServiceCollection s)
    {
        // Infra（既有）
        s.AddSingleton(AppPaths.Default);
        s.AddSingleton(sp => new WindowStateStore(
            System.IO.Path.Combine(sp.GetRequiredService<AppPaths>().AppSupportDirectory, "window_state.json")));
        s.AddSingleton<AppLifecycle>();
        s.AddSingleton<DispatcherScheduler>();

        // Infra（新增）
        s.AddSingleton<IRuntimeInfoProvider, RuntimeInfoProvider>();
        s.AddSingleton<IBreadcrumbSource, NavigationBreadcrumbSource>();
        s.AddSingleton<IKeychainHealthProbe, KeychainHealthProbe>();
        s.AddSingleton<IOnnxHealthProbe>(_ => new OnnxHealthProbe(EmbeddingSettings.Default));

        // Theme（既有）
        s.AddSingleton<PortableThemeState>(_ => new PortableThemeState());
        s.AddSingleton<PortableThemeStateController>(sp =>
        {
            var state = sp.GetRequiredService<PortableThemeState>();
            var bridge = sp.GetRequiredService<ThemeBridge>();
            return new PortableThemeStateController(state, bridge.ApplyAsync);
        });
        s.AddSingleton<ThemeBridge>();

        // Navigation（既有）
        s.AddSingleton<PageRegistry>(_ => RegisterPages(new PageRegistry()));
        s.AddSingleton<INavigationService, NavigationService>();

        // Shell VMs（新增）
        s.AddSingleton<AppChromeViewModel>();
        s.AddSingleton<AppStatusBarViewModel>();

        // 既有 ViewModels
        s.AddSingleton<MainWindowViewModel>();
        s.AddSingleton<ThreeColumnLayoutViewModel>();
        s.AddSingleton<LeftNavViewModel>();
        s.AddSingleton<RightConversationViewModel>();
        s.AddTransient<WelcomeViewModel>();
        s.AddTransient<PlaceholderViewModel>();

        return s;
    }

    private static PageRegistry RegisterPages(PageRegistry reg)
    {
        reg.Register<WelcomeViewModel,     WelcomeView>(PageKeys.Welcome);
        reg.Register<PlaceholderViewModel, PlaceholderView>(PageKeys.Dashboard);
        reg.Register<PlaceholderViewModel, PlaceholderView>(PageKeys.Settings);
        return reg;
    }
}
```

> 注：`OnnxHealthProbe` 用 `EmbeddingSettings.Default`（model/vocab 都 null → probe 返回 Info）。后续 M2 / M6 接真实配置时可改成从 settings store 拉。

- [ ] **Step 2: 扩展 AppHostTests**

Replace `tests/Tianming.Desktop.Avalonia.Tests/DI/AppHostTests.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Tianming.Desktop.Avalonia;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.Shell;
using Tianming.Desktop.Avalonia.ViewModels;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.DI;

public class AppHostTests
{
    [Fact]
    public void Build_ResolvesNavigationService()
    {
        using var sp = (ServiceProvider)AppHost.Build();
        var nav = sp.GetRequiredService<INavigationService>();
        Assert.NotNull(nav);
    }

    [Fact]
    public void Build_ResolvesMainWindowViewModel()
    {
        using var sp = (ServiceProvider)AppHost.Build();
        var vm = sp.GetRequiredService<MainWindowViewModel>();
        Assert.NotNull(vm);
        Assert.NotNull(vm.Chrome);
        Assert.NotNull(vm.StatusBar);
    }

    [Fact]
    public void Build_RegistersAllM3Pages()
    {
        using var sp = (ServiceProvider)AppHost.Build();
        var reg = sp.GetRequiredService<PageRegistry>();
        Assert.Contains(PageKeys.Welcome,   reg.Keys);
        Assert.Contains(PageKeys.Dashboard, reg.Keys);
        Assert.Contains(PageKeys.Settings,  reg.Keys);
    }

    [Fact]
    public void Build_ResolvesAllInfraProbes()
    {
        using var sp = (ServiceProvider)AppHost.Build();
        Assert.NotNull(sp.GetRequiredService<IRuntimeInfoProvider>());
        Assert.NotNull(sp.GetRequiredService<IBreadcrumbSource>());
        Assert.NotNull(sp.GetRequiredService<IKeychainHealthProbe>());
        Assert.NotNull(sp.GetRequiredService<IOnnxHealthProbe>());
    }

    [Fact]
    public void Build_ResolvesShellVms()
    {
        using var sp = (ServiceProvider)AppHost.Build();
        Assert.NotNull(sp.GetRequiredService<AppChromeViewModel>());
        Assert.NotNull(sp.GetRequiredService<AppStatusBarViewModel>());
    }
}
```

- [ ] **Step 3: 跑测试**

```bash
dotnet test Tianming.MacMigration.sln -c Debug --nologo -v q 2>&1 | tail -10
```

Expected：所有测试通过（既有 1156 + 设计 token ~13 + breadcrumb 3 + runtime 2 + keychain probe 2 + onnx probe 3 + chrome vm 3 + statusbar vm 3 + 新增 AppHostTests 2 = 1187 上下）。

- [ ] **Step 4: 启动应用 + AppStatusBarViewModel.RefreshProbesAsync 在 startup 触发**

打开 `src/Tianming.Desktop.Avalonia/App.axaml.cs`，在创建 window 后追加一行 `_ = statusBar.RefreshProbesAsync();`：

```csharp
if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
{
    var lifecycle = Services.GetRequiredService<AppLifecycle>();
    _ = lifecycle.OnStartupAsync();

    var vm = Services.GetRequiredService<MainWindowViewModel>();
    var window = new MainWindow { DataContext = vm };

    // 异步 fire probes，不阻塞 UI
    _ = vm.StatusBar.RefreshProbesAsync();

    // …（其余 saved state / closing 代码不变）
}
```

- [ ] **Step 5: 冒烟启动**

```bash
dotnet run --project src/Tianming.Desktop.Avalonia -c Debug 2>&1 | head -40
```

Expected：
- 窗口启动
- macOS 顶部菜单栏出现 天命 / 文件 / 编辑 / 视图 / 窗口 / 帮助
- 自定义 chrome 顶栏可见（breadcrumb "天命 · 欢迎"，右边 emoji 占位按钮）
- 底部状态栏可见（".NET 8.0.x  本地写作模式  Keychain  ONNX  …"）
- 中间 ThreeColumnLayoutView 显示（含已有 LeftNavView / WelcomeView / RightConversationView）

手动关掉窗口。

- [ ] **Step 6: commit**

```bash
git add src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs \
        tests/Tianming.Desktop.Avalonia.Tests/DI/AppHostTests.cs \
        src/Tianming.Desktop.Avalonia/App.axaml.cs
git commit -m "feat(shell): DI 装配基建服务 + AppHostTests 扩展 + 启动时 fire probes

注册 4 个新 infra 服务（RuntimeInfoProvider / NavigationBreadcrumbSource / KeychainHealthProbe / OnnxHealthProbe）
+ 2 个新 Shell VM (AppChromeViewModel / AppStatusBarViewModel)。
AppHostTests 验证全部可 resolve；App.axaml.cs 启动时 fire-and-forget RefreshProbesAsync。"
```

---

## Phase 9 / 14 个 Primitives

每个 primitive 用 `TemplatedControl` + ControlTheme。所有 IconGlyph 字符串走 Lucide。每个 primitive：
- 命名空间 `Tianming.Desktop.Avalonia.Controls`
- 文件结构 `Controls/<Name>.cs`（class + StyledProperty 定义）+ `Controls/<Name>.axaml`（ControlTheme + Template）
- 测试位于 `tests/.../Controls/<Name>Tests.cs`，用 `Avalonia.Headless.XUnit.AvaloniaFact`，至少 3 测试：默认值 / property invalidation / edge case
- 注册 ControlTheme 入 App.axaml `<Application.Styles>`（每个 primitive 一行 StyleInclude）

### Task 26: BadgePill primitive

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Controls/BadgePill.cs`
- Create: `src/Tianming.Desktop.Avalonia/Controls/BadgePill.axaml`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Controls/BadgePillTests.cs`
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml`

- [ ] **Step 1: 写 class**

Create `src/Tianming.Desktop.Avalonia/Controls/BadgePill.cs`:

```csharp
using Avalonia;
using Avalonia.Controls.Primitives;
using Tianming.Desktop.Avalonia.Shell;

namespace Tianming.Desktop.Avalonia.Controls;

public class BadgePill : TemplatedControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<BadgePill, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<StatusKind> KindProperty =
        AvaloniaProperty.Register<BadgePill, StatusKind>(nameof(Kind), StatusKind.Neutral);

    public static readonly StyledProperty<bool> ShowDotProperty =
        AvaloniaProperty.Register<BadgePill, bool>(nameof(ShowDot), false);

    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public StatusKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public bool ShowDot { get => GetValue(ShowDotProperty); set => SetValue(ShowDotProperty, value); }
}
```

- [ ] **Step 2: 写 axaml（ControlTheme + Template）**

Create `src/Tianming.Desktop.Avalonia/Controls/BadgePill.axaml`:

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:controls="using:Tianming.Desktop.Avalonia.Controls">

  <ControlTheme x:Key="{x:Type controls:BadgePill}" TargetType="controls:BadgePill">
    <Setter Property="FontSize" Value="{DynamicResource FontSizeCaption}"/>
    <Setter Property="FontWeight" Value="{DynamicResource FontWeightMedium}"/>
    <Setter Property="Padding" Value="8,2"/>
    <Setter Property="CornerRadius" Value="{DynamicResource RadiusFull}"/>
    <Setter Property="HorizontalAlignment" Value="Left"/>
    <Setter Property="Template">
      <ControlTemplate>
        <Border x:Name="PART_Root"
                Background="{DynamicResource StatusNeutralSubtleBrush}"
                CornerRadius="{TemplateBinding CornerRadius}"
                Padding="{TemplateBinding Padding}">
          <StackPanel Orientation="Horizontal" Spacing="4" VerticalAlignment="Center">
            <Ellipse x:Name="PART_Dot"
                     Width="6" Height="6"
                     Fill="{DynamicResource StatusNeutralBrush}"
                     IsVisible="{TemplateBinding ShowDot}"
                     VerticalAlignment="Center"/>
            <TextBlock x:Name="PART_Text"
                       Text="{TemplateBinding Text}"
                       Foreground="{DynamicResource StatusNeutralBrush}"
                       FontSize="{TemplateBinding FontSize}"
                       FontWeight="{TemplateBinding FontWeight}"
                       VerticalAlignment="Center"/>
          </StackPanel>
        </Border>
      </ControlTemplate>
    </Setter>

    <!-- Kind 触发 background + foreground 切换 -->
    <Style Selector="^[Kind=Success] /template/ Border#PART_Root">
      <Setter Property="Background" Value="{DynamicResource StatusSuccessSubtleBrush}"/>
    </Style>
    <Style Selector="^[Kind=Success] /template/ Ellipse#PART_Dot">
      <Setter Property="Fill" Value="{DynamicResource StatusSuccessBrush}"/>
    </Style>
    <Style Selector="^[Kind=Success] /template/ TextBlock#PART_Text">
      <Setter Property="Foreground" Value="{DynamicResource StatusSuccessBrush}"/>
    </Style>

    <Style Selector="^[Kind=Warning] /template/ Border#PART_Root">
      <Setter Property="Background" Value="{DynamicResource StatusWarningSubtleBrush}"/>
    </Style>
    <Style Selector="^[Kind=Warning] /template/ Ellipse#PART_Dot">
      <Setter Property="Fill" Value="{DynamicResource StatusWarningBrush}"/>
    </Style>
    <Style Selector="^[Kind=Warning] /template/ TextBlock#PART_Text">
      <Setter Property="Foreground" Value="{DynamicResource StatusWarningBrush}"/>
    </Style>

    <Style Selector="^[Kind=Danger] /template/ Border#PART_Root">
      <Setter Property="Background" Value="{DynamicResource StatusDangerSubtleBrush}"/>
    </Style>
    <Style Selector="^[Kind=Danger] /template/ Ellipse#PART_Dot">
      <Setter Property="Fill" Value="{DynamicResource StatusDangerBrush}"/>
    </Style>
    <Style Selector="^[Kind=Danger] /template/ TextBlock#PART_Text">
      <Setter Property="Foreground" Value="{DynamicResource StatusDangerBrush}"/>
    </Style>

    <Style Selector="^[Kind=Info] /template/ Border#PART_Root">
      <Setter Property="Background" Value="{DynamicResource StatusInfoSubtleBrush}"/>
    </Style>
    <Style Selector="^[Kind=Info] /template/ Ellipse#PART_Dot">
      <Setter Property="Fill" Value="{DynamicResource StatusInfoBrush}"/>
    </Style>
    <Style Selector="^[Kind=Info] /template/ TextBlock#PART_Text">
      <Setter Property="Foreground" Value="{DynamicResource StatusInfoBrush}"/>
    </Style>
  </ControlTheme>
</ResourceDictionary>
```

- [ ] **Step 3: App.axaml 注册 ControlTheme**

把 `src/Tianming.Desktop.Avalonia/App.axaml` 的 `Application.Resources` 部分 MergedDictionaries 列表末尾加：

```xml
<ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Controls/BadgePill.axaml"/>
```

- [ ] **Step 4: 测试**

Create `tests/Tianming.Desktop.Avalonia.Tests/Controls/BadgePillTests.cs`:

```csharp
using Avalonia.Headless.XUnit;
using Tianming.Desktop.Avalonia.Controls;
using Tianming.Desktop.Avalonia.Shell;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class BadgePillTests
{
    [AvaloniaFact]
    public void Defaults_TextEmpty_KindNeutral_ShowDotFalse()
    {
        var b = new BadgePill();
        Assert.Equal(string.Empty, b.Text);
        Assert.Equal(StatusKind.Neutral, b.Kind);
        Assert.False(b.ShowDot);
    }

    [AvaloniaFact]
    public void SetText_PersistsValue()
    {
        var b = new BadgePill { Text = "已连接" };
        Assert.Equal("已连接", b.Text);
    }

    [AvaloniaFact]
    public void SetKind_PersistsAndPropertyChanged()
    {
        var b = new BadgePill();
        var raised = false;
        b.PropertyChanged += (_, e) =>
        {
            if (e.Property == BadgePill.KindProperty) raised = true;
        };
        b.Kind = StatusKind.Success;
        Assert.True(raised);
        Assert.Equal(StatusKind.Success, b.Kind);
    }
}
```

> Avalonia.Headless.XUnit 需要 TestApp。在 `tests/Tianming.Desktop.Avalonia.Tests/AssemblyInfo.cs` 加（如果文件不存在则创建）：
>
> ```csharp
> using Avalonia;
> using Avalonia.Headless;
>
> [assembly: AvaloniaTestApplication(typeof(Tianming.Desktop.Avalonia.Tests.TestApp))]
>
> namespace Tianming.Desktop.Avalonia.Tests;
> public class TestApp : Application
> {
>     public override void Initialize() { /* 不加载 axaml，避免 NativeMenu 依赖系统层 */ }
> }
> public static class TestAppEntryPoint
> {
>     public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
> }
> ```

- [ ] **Step 5: 跑测试 + commit**

```bash
dotnet build Tianming.MacMigration.sln -c Debug --nologo -v q
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ -c Debug --nologo --filter "FullyQualifiedName~BadgePillTests" -v q
```

Expected：3 测试过。

```bash
git add src/Tianming.Desktop.Avalonia/Controls/BadgePill.cs \
        src/Tianming.Desktop.Avalonia/Controls/BadgePill.axaml \
        src/Tianming.Desktop.Avalonia/App.axaml \
        tests/Tianming.Desktop.Avalonia.Tests/Controls/BadgePillTests.cs \
        tests/Tianming.Desktop.Avalonia.Tests/AssemblyInfo.cs
git commit -m "feat(controls): BadgePill primitive — 5 Kind 圆角 pill 徽章

Text / Kind (Success/Warning/Danger/Info/Neutral) / ShowDot 3 个 StyledProperty；
ControlTheme 按 Kind 切 background + dot fill + text color；
3 测试覆盖默认值 / Text 持久 / Kind PropertyChanged。
另：AssemblyInfo 加 TestApp 给 Avalonia.Headless.XUnit 用。"
```

---

### Task 27: StatusBarItem primitive

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Controls/StatusBarItem.cs`
- Create: `src/Tianming.Desktop.Avalonia/Controls/StatusBarItem.axaml`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Controls/StatusBarItemTests.cs`
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml`
- Modify: `src/Tianming.Desktop.Avalonia/Shell/AppStatusBar.axaml`（接管 TextBlock 占位）

- [ ] **Step 1: 写 class**

Create `src/Tianming.Desktop.Avalonia/Controls/StatusBarItem.cs`:

```csharp
using Avalonia;
using Avalonia.Controls.Primitives;
using Tianming.Desktop.Avalonia.Shell;

namespace Tianming.Desktop.Avalonia.Controls;

public class StatusBarItem : TemplatedControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<StatusBarItem, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<StatusKind> KindProperty =
        AvaloniaProperty.Register<StatusBarItem, StatusKind>(nameof(Kind), StatusKind.Neutral);

    public static readonly StyledProperty<string?> TooltipTextProperty =
        AvaloniaProperty.Register<StatusBarItem, string?>(nameof(TooltipText));

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public StatusKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public string? TooltipText { get => GetValue(TooltipTextProperty); set => SetValue(TooltipTextProperty, value); }
}
```

- [ ] **Step 2: 写 axaml**

Create `src/Tianming.Desktop.Avalonia/Controls/StatusBarItem.axaml`:

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:controls="using:Tianming.Desktop.Avalonia.Controls">
  <ControlTheme x:Key="{x:Type controls:StatusBarItem}" TargetType="controls:StatusBarItem">
    <Setter Property="FontSize" Value="{DynamicResource FontSizeCaption}"/>
    <Setter Property="Template">
      <ControlTemplate>
        <Border ToolTip.Tip="{TemplateBinding TooltipText}">
          <StackPanel Orientation="Horizontal" Spacing="4" VerticalAlignment="Center">
            <Ellipse x:Name="PART_Dot" Width="6" Height="6"
                     Fill="{DynamicResource StatusNeutralBrush}"
                     VerticalAlignment="Center"/>
            <TextBlock x:Name="PART_Label"
                       Text="{TemplateBinding Label}"
                       Foreground="{DynamicResource TextSecondaryBrush}"
                       FontSize="{TemplateBinding FontSize}"
                       VerticalAlignment="Center"/>
          </StackPanel>
        </Border>
      </ControlTemplate>
    </Setter>

    <Style Selector="^[Kind=Success] /template/ Ellipse#PART_Dot">
      <Setter Property="Fill" Value="{DynamicResource StatusSuccessBrush}"/>
    </Style>
    <Style Selector="^[Kind=Warning] /template/ Ellipse#PART_Dot">
      <Setter Property="Fill" Value="{DynamicResource StatusWarningBrush}"/>
    </Style>
    <Style Selector="^[Kind=Danger] /template/ Ellipse#PART_Dot">
      <Setter Property="Fill" Value="{DynamicResource StatusDangerBrush}"/>
    </Style>
    <Style Selector="^[Kind=Info] /template/ Ellipse#PART_Dot">
      <Setter Property="Fill" Value="{DynamicResource StatusInfoBrush}"/>
    </Style>
  </ControlTheme>
</ResourceDictionary>
```

- [ ] **Step 3: 注册 + 用到 AppStatusBar**

App.axaml `Application.Resources` MergedDictionaries 加：

```xml
<ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Controls/StatusBarItem.axaml"/>
```

修改 `src/Tianming.Desktop.Avalonia/Shell/AppStatusBar.axaml`，把左侧 3 个 status TextBlock 替换为 StatusBarItem：

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:shell="using:Tianming.Desktop.Avalonia.Shell"
             xmlns:controls="using:Tianming.Desktop.Avalonia.Controls"
             x:Class="Tianming.Desktop.Avalonia.Shell.AppStatusBar"
             x:DataType="shell:AppStatusBarViewModel"
             Height="28">
  <Border Background="{DynamicResource SurfaceSubtleBrush}"
          BorderBrush="{DynamicResource BorderSubtleBrush}"
          BorderThickness="0,1,0,0">
    <Grid Margin="12,0">
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width="Auto"/>
        <ColumnDefinition Width="*"/>
        <ColumnDefinition Width="Auto"/>
      </Grid.ColumnDefinitions>

      <StackPanel Grid.Column="0" Orientation="Horizontal" Spacing="12" VerticalAlignment="Center">
        <TextBlock Text="{Binding DotNetRuntime}"
                   FontSize="{DynamicResource FontSizeCaption}"
                   Foreground="{DynamicResource TextSecondaryBrush}"
                   VerticalAlignment="Center"/>
        <controls:StatusBarItem Label="{Binding LocalMode.Label}"
                                Kind="{Binding LocalMode.Kind}"/>
        <controls:StatusBarItem Label="{Binding KeychainStatus.Label}"
                                Kind="{Binding KeychainStatus.Kind}"
                                TooltipText="{Binding KeychainStatus.Tooltip}"/>
        <controls:StatusBarItem Label="{Binding OnnxStatus.Label}"
                                Kind="{Binding OnnxStatus.Kind}"
                                TooltipText="{Binding OnnxStatus.Tooltip}"/>
      </StackPanel>

      <TextBlock Grid.Column="2"
                 Text="{Binding CurrentProjectPath}"
                 FontSize="{DynamicResource FontSizeCaption}"
                 Foreground="{DynamicResource TextTertiaryBrush}"
                 VerticalAlignment="Center"
                 IsVisible="{Binding CurrentProjectPath, Converter={x:Static ObjectConverters.IsNotNull}}"/>
    </Grid>
  </Border>
</UserControl>
```

- [ ] **Step 4: 测试**

Create `tests/Tianming.Desktop.Avalonia.Tests/Controls/StatusBarItemTests.cs`:

```csharp
using Avalonia.Headless.XUnit;
using Tianming.Desktop.Avalonia.Controls;
using Tianming.Desktop.Avalonia.Shell;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class StatusBarItemTests
{
    [AvaloniaFact]
    public void Defaults_LabelEmpty_KindNeutral_TooltipNull()
    {
        var i = new StatusBarItem();
        Assert.Equal(string.Empty, i.Label);
        Assert.Equal(StatusKind.Neutral, i.Kind);
        Assert.Null(i.TooltipText);
    }

    [AvaloniaFact]
    public void SetLabelAndKind_Persists()
    {
        var i = new StatusBarItem { Label = "Keychain", Kind = StatusKind.Success };
        Assert.Equal("Keychain", i.Label);
        Assert.Equal(StatusKind.Success, i.Kind);
    }

    [AvaloniaFact]
    public void SetTooltipText_Persists()
    {
        var i = new StatusBarItem { TooltipText = "macOS Keychain 可用" };
        Assert.Equal("macOS Keychain 可用", i.TooltipText);
    }
}
```

- [ ] **Step 5: 跑测试 + 冒烟 + commit**

```bash
dotnet build Tianming.MacMigration.sln -c Debug --nologo -v q
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ -c Debug --nologo --filter "FullyQualifiedName~StatusBarItemTests" -v q
dotnet run --project src/Tianming.Desktop.Avalonia -c Debug 2>&1 | head -10
```

Expected：3 测试过；启动后底部状态栏看到 4 个圆点（绿 / 灰 → 异步变 / 灰 → 异步变；DotNet 字符串不变）。

```bash
git add src/Tianming.Desktop.Avalonia/Controls/StatusBarItem.cs \
        src/Tianming.Desktop.Avalonia/Controls/StatusBarItem.axaml \
        src/Tianming.Desktop.Avalonia/App.axaml \
        src/Tianming.Desktop.Avalonia/Shell/AppStatusBar.axaml \
        tests/Tianming.Desktop.Avalonia.Tests/Controls/StatusBarItemTests.cs
git commit -m "feat(controls): StatusBarItem primitive + AppStatusBar 接入

Label / Kind / TooltipText 3 个 StyledProperty；ControlTheme 按 Kind 切 dot 颜色；
AppStatusBar 用 StatusBarItem 替换 3 个 TextBlock 占位；3 测试。"
```

---

### Task 28: SectionCard primitive

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Controls/SectionCard.cs`
- Create: `src/Tianming.Desktop.Avalonia/Controls/SectionCard.axaml`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Controls/SectionCardTests.cs`
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml`

- [ ] **Step 1: class**

Create `src/Tianming.Desktop.Avalonia/Controls/SectionCard.cs`:

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Tianming.Desktop.Avalonia.Controls;

public class SectionCard : HeaderedContentControl
{
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<SectionCard, string?>(nameof(Subtitle));

    public static readonly StyledProperty<object?> HeaderActionsProperty =
        AvaloniaProperty.Register<SectionCard, object?>(nameof(HeaderActions));

    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public object? HeaderActions { get => GetValue(HeaderActionsProperty); set => SetValue(HeaderActionsProperty, value); }
}
```

> 继承 `HeaderedContentControl` 让 `Header` / `Content` 走 Avalonia 内置（Header = Title）。

- [ ] **Step 2: axaml**

Create `src/Tianming.Desktop.Avalonia/Controls/SectionCard.axaml`:

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:controls="using:Tianming.Desktop.Avalonia.Controls">
  <ControlTheme x:Key="{x:Type controls:SectionCard}" TargetType="controls:SectionCard">
    <Setter Property="Padding" Value="{DynamicResource PaddingCard}"/>
    <Setter Property="Template">
      <ControlTemplate>
        <Border Background="{DynamicResource SurfaceBaseBrush}"
                CornerRadius="{DynamicResource RadiusLg}"
                BorderBrush="{DynamicResource BorderSubtleBrush}"
                BorderThickness="1"
                BoxShadow="{DynamicResource ShadowSm}"
                Padding="{TemplateBinding Padding}">
          <DockPanel LastChildFill="True">
            <Grid DockPanel.Dock="Top" Margin="0,0,0,12"
                  IsVisible="{TemplateBinding Header, Converter={x:Static ObjectConverters.IsNotNull}}">
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
              </Grid.ColumnDefinitions>
              <StackPanel Grid.Column="0" Spacing="2">
                <TextBlock Text="{TemplateBinding Header}"
                           FontSize="{DynamicResource FontSizeH3}"
                           FontWeight="{DynamicResource FontWeightSemibold}"
                           Foreground="{DynamicResource TextPrimaryBrush}"/>
                <TextBlock Text="{TemplateBinding Subtitle}"
                           FontSize="{DynamicResource FontSizeSecondary}"
                           Foreground="{DynamicResource TextSecondaryBrush}"
                           IsVisible="{TemplateBinding Subtitle, Converter={x:Static ObjectConverters.IsNotNull}}"/>
              </StackPanel>
              <ContentPresenter Grid.Column="1"
                                Content="{TemplateBinding HeaderActions}"
                                VerticalAlignment="Center"/>
            </Grid>
            <ContentPresenter Content="{TemplateBinding Content}"/>
          </DockPanel>
        </Border>
      </ControlTemplate>
    </Setter>
  </ControlTheme>
</ResourceDictionary>
```

- [ ] **Step 3: App.axaml 注册**

加 `<ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Controls/SectionCard.axaml"/>`

- [ ] **Step 4: 测试**

Create `tests/Tianming.Desktop.Avalonia.Tests/Controls/SectionCardTests.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Tianming.Desktop.Avalonia.Controls;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class SectionCardTests
{
    [AvaloniaFact]
    public void Defaults_HeaderNull_SubtitleNull_HeaderActionsNull()
    {
        var c = new SectionCard();
        Assert.Null(c.Header);
        Assert.Null(c.Subtitle);
        Assert.Null(c.HeaderActions);
    }

    [AvaloniaFact]
    public void SetHeaderAndSubtitle_Persists()
    {
        var c = new SectionCard { Header = "最近项目", Subtitle = "上次打开" };
        Assert.Equal("最近项目", c.Header);
        Assert.Equal("上次打开", c.Subtitle);
    }

    [AvaloniaFact]
    public void HeaderActions_AcceptsControl()
    {
        var btn = new Button { Content = "+" };
        var c = new SectionCard { HeaderActions = btn };
        Assert.Same(btn, c.HeaderActions);
    }
}
```

- [ ] **Step 5: 跑测试 + commit**

```bash
dotnet build Tianming.MacMigration.sln -c Debug --nologo -v q
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ -c Debug --nologo --filter "FullyQualifiedName~SectionCardTests" -v q
git add src/Tianming.Desktop.Avalonia/Controls/SectionCard.cs \
        src/Tianming.Desktop.Avalonia/Controls/SectionCard.axaml \
        src/Tianming.Desktop.Avalonia/App.axaml \
        tests/Tianming.Desktop.Avalonia.Tests/Controls/SectionCardTests.cs
git commit -m "feat(controls): SectionCard primitive — 白底圆角 + 标题/副标题/HeaderActions/Content

继承 HeaderedContentControl；ControlTheme 渲染：Border(Surface+Shadow) → DockPanel(Header 行 + Subtitle + ContentPresenter)。
3 测试覆盖默认值 / Header+Subtitle / HeaderActions accept Control。"
```

---

### Task 29: StatsCard primitive

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Controls/StatsCard.cs`
- Create: `src/Tianming.Desktop.Avalonia/Controls/StatsCard.axaml`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Controls/StatsCardTests.cs`
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml`

- [ ] **Step 1: class**

Create `src/Tianming.Desktop.Avalonia/Controls/StatsCard.cs`:

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Tianming.Desktop.Avalonia.Shell;

namespace Tianming.Desktop.Avalonia.Controls;

public class StatsCard : TemplatedControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<StatsCard, string>(nameof(Label), string.Empty);
    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<StatsCard, string>(nameof(Value), string.Empty);
    public static readonly StyledProperty<string?> CaptionProperty =
        AvaloniaProperty.Register<StatsCard, string?>(nameof(Caption));
    public static readonly StyledProperty<StatusKind?> TrendKindProperty =
        AvaloniaProperty.Register<StatsCard, StatusKind?>(nameof(TrendKind));
    public static readonly StyledProperty<object?> AccessoryContentProperty =
        AvaloniaProperty.Register<StatsCard, object?>(nameof(AccessoryContent));

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string? Caption { get => GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }
    public StatusKind? TrendKind { get => GetValue(TrendKindProperty); set => SetValue(TrendKindProperty, value); }
    public object? AccessoryContent { get => GetValue(AccessoryContentProperty); set => SetValue(AccessoryContentProperty, value); }
}
```

- [ ] **Step 2: axaml**

Create `src/Tianming.Desktop.Avalonia/Controls/StatsCard.axaml`:

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:controls="using:Tianming.Desktop.Avalonia.Controls">
  <ControlTheme x:Key="{x:Type controls:StatsCard}" TargetType="controls:StatsCard">
    <Setter Property="Padding" Value="{DynamicResource PaddingCard}"/>
    <Setter Property="Template">
      <ControlTemplate>
        <Border Background="{DynamicResource SurfaceBaseBrush}"
                CornerRadius="{DynamicResource RadiusLg}"
                BorderBrush="{DynamicResource BorderSubtleBrush}"
                BorderThickness="1"
                BoxShadow="{DynamicResource ShadowSm}"
                Padding="{TemplateBinding Padding}">
          <Grid ColumnDefinitions="*,Auto" RowDefinitions="Auto,Auto,Auto">
            <TextBlock Grid.Column="0" Grid.Row="0"
                       Text="{TemplateBinding Label}"
                       FontSize="{DynamicResource FontSizeSecondary}"
                       Foreground="{DynamicResource TextSecondaryBrush}"/>
            <TextBlock Grid.Column="0" Grid.Row="1"
                       Text="{TemplateBinding Value}"
                       FontSize="{DynamicResource FontSizeDisplay}"
                       FontWeight="{DynamicResource FontWeightBold}"
                       Foreground="{DynamicResource TextPrimaryBrush}"
                       Margin="0,4,0,0"/>
            <TextBlock x:Name="PART_Caption"
                       Grid.Column="0" Grid.Row="2"
                       Text="{TemplateBinding Caption}"
                       FontSize="{DynamicResource FontSizeCaption}"
                       Foreground="{DynamicResource TextSecondaryBrush}"
                       Margin="0,4,0,0"
                       IsVisible="{TemplateBinding Caption, Converter={x:Static ObjectConverters.IsNotNull}}"/>
            <ContentPresenter Grid.Column="1" Grid.Row="0" Grid.RowSpan="3"
                              Content="{TemplateBinding AccessoryContent}"
                              VerticalAlignment="Center"
                              HorizontalAlignment="Right"/>
          </Grid>
        </Border>
      </ControlTemplate>
    </Setter>

    <!-- TrendKind 给 Caption 上色 -->
    <Style Selector="^[TrendKind=Success] /template/ TextBlock#PART_Caption">
      <Setter Property="Foreground" Value="{DynamicResource StatusSuccessBrush}"/>
    </Style>
    <Style Selector="^[TrendKind=Warning] /template/ TextBlock#PART_Caption">
      <Setter Property="Foreground" Value="{DynamicResource StatusWarningBrush}"/>
    </Style>
    <Style Selector="^[TrendKind=Danger] /template/ TextBlock#PART_Caption">
      <Setter Property="Foreground" Value="{DynamicResource StatusDangerBrush}"/>
    </Style>
    <Style Selector="^[TrendKind=Info] /template/ TextBlock#PART_Caption">
      <Setter Property="Foreground" Value="{DynamicResource StatusInfoBrush}"/>
    </Style>
  </ControlTheme>
</ResourceDictionary>
```

- [ ] **Step 3: App.axaml 注册 + 测试**

App.axaml 加 `<ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Controls/StatsCard.axaml"/>`。

Create `tests/Tianming.Desktop.Avalonia.Tests/Controls/StatsCardTests.cs`:

```csharp
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Tianming.Desktop.Avalonia.Controls;
using Tianming.Desktop.Avalonia.Shell;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class StatsCardTests
{
    [AvaloniaFact]
    public void Defaults_AllStringsEmptyOrNull()
    {
        var s = new StatsCard();
        Assert.Equal(string.Empty, s.Label);
        Assert.Equal(string.Empty, s.Value);
        Assert.Null(s.Caption);
        Assert.Null(s.TrendKind);
        Assert.Null(s.AccessoryContent);
    }

    [AvaloniaFact]
    public void SetLabelAndValueAndCaption_Persists()
    {
        var s = new StatsCard { Label = "总字数", Value = "328,742", Caption = "本周 +12.3%", TrendKind = StatusKind.Success };
        Assert.Equal("总字数", s.Label);
        Assert.Equal("328,742", s.Value);
        Assert.Equal("本周 +12.3%", s.Caption);
        Assert.Equal(StatusKind.Success, s.TrendKind);
    }

    [AvaloniaFact]
    public void AccessoryContent_AcceptsControl()
    {
        var sparkline = new Border();
        var s = new StatsCard { AccessoryContent = sparkline };
        Assert.Same(sparkline, s.AccessoryContent);
    }
}
```

- [ ] **Step 4: 跑测试 + commit**

```bash
dotnet build Tianming.MacMigration.sln -c Debug --nologo -v q
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ -c Debug --nologo --filter "FullyQualifiedName~StatsCardTests" -v q
git add src/Tianming.Desktop.Avalonia/Controls/StatsCard.cs \
        src/Tianming.Desktop.Avalonia/Controls/StatsCard.axaml \
        src/Tianming.Desktop.Avalonia/App.axaml \
        tests/Tianming.Desktop.Avalonia.Tests/Controls/StatsCardTests.cs
git commit -m "feat(controls): StatsCard primitive — Label/Value/Caption/TrendKind/AccessoryContent

5 个 StyledProperty；Value 走 FontSizeDisplay+Bold；TrendKind 决定 Caption 颜色；
AccessoryContent slot 接 sparkline / icon / chart preview。3 测试。"
```

---

### Task 30: SearchBox primitive

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Controls/SearchBox.cs`
- Create: `src/Tianming.Desktop.Avalonia/Controls/SearchBox.axaml`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Controls/SearchBoxTests.cs`
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml`

- [ ] **Step 1: class**

Create `src/Tianming.Desktop.Avalonia/Controls/SearchBox.cs`:

```csharp
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace Tianming.Desktop.Avalonia.Controls;

public class SearchBox : TemplatedControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<SearchBox, string>(nameof(Text), string.Empty, defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<SearchBox, string?>(nameof(Placeholder));
    public static readonly StyledProperty<ICommand?> SubmitCommandProperty =
        AvaloniaProperty.Register<SearchBox, ICommand?>(nameof(SubmitCommand));

    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public ICommand? SubmitCommand { get => GetValue(SubmitCommandProperty); set => SetValue(SubmitCommandProperty, value); }
}
```

> 需要 `using Avalonia.Data;` 给 `BindingMode`。补到 using 列表。

- [ ] **Step 2: axaml**

Create `src/Tianming.Desktop.Avalonia/Controls/SearchBox.axaml`:

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:controls="using:Tianming.Desktop.Avalonia.Controls">
  <ControlTheme x:Key="{x:Type controls:SearchBox}" TargetType="controls:SearchBox">
    <Setter Property="Padding" Value="8,4"/>
    <Setter Property="Template">
      <ControlTemplate>
        <Border Background="{DynamicResource SurfaceSubtleBrush}"
                CornerRadius="{DynamicResource RadiusMd}"
                BorderBrush="{DynamicResource BorderSubtleBrush}"
                BorderThickness="1"
                Padding="{TemplateBinding Padding}">
          <DockPanel LastChildFill="True">
            <TextBlock DockPanel.Dock="Left"
                       Text="🔍"
                       FontSize="{DynamicResource FontSizeBody}"
                       VerticalAlignment="Center"
                       Margin="0,0,6,0"
                       Foreground="{DynamicResource TextTertiaryBrush}"/>
            <TextBox x:Name="PART_TextBox"
                     Text="{TemplateBinding Text, Mode=TwoWay}"
                     Watermark="{TemplateBinding Placeholder}"
                     Background="Transparent"
                     BorderThickness="0"
                     Padding="0"
                     Foreground="{DynamicResource TextPrimaryBrush}"
                     FontSize="{DynamicResource FontSizeBody}"/>
          </DockPanel>
        </Border>
      </ControlTemplate>
    </Setter>
  </ControlTheme>
</ResourceDictionary>
```

> 🔍 emoji 占位；后续 Lucide icon 接入时换掉。

- [ ] **Step 3: App.axaml 注册 + 测试**

App.axaml 加 `<ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Controls/SearchBox.axaml"/>`。

Create `tests/Tianming.Desktop.Avalonia.Tests/Controls/SearchBoxTests.cs`:

```csharp
using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Controls;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class SearchBoxTests
{
    [AvaloniaFact]
    public void Defaults_TextEmpty_PlaceholderNull_CommandNull()
    {
        var s = new SearchBox();
        Assert.Equal(string.Empty, s.Text);
        Assert.Null(s.Placeholder);
        Assert.Null(s.SubmitCommand);
    }

    [AvaloniaFact]
    public void SetText_Persists()
    {
        var s = new SearchBox { Text = "查找章节" };
        Assert.Equal("查找章节", s.Text);
    }

    [AvaloniaFact]
    public void SetSubmitCommand_Persists()
    {
        var cmd = new RelayCommand(() => { });
        var s = new SearchBox { SubmitCommand = cmd };
        Assert.Same(cmd, s.SubmitCommand);
    }
}
```

- [ ] **Step 4: 跑测试 + commit**

```bash
dotnet build Tianming.MacMigration.sln -c Debug --nologo -v q
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ -c Debug --nologo --filter "FullyQualifiedName~SearchBoxTests" -v q
git add src/Tianming.Desktop.Avalonia/Controls/SearchBox.cs \
        src/Tianming.Desktop.Avalonia/Controls/SearchBox.axaml \
        src/Tianming.Desktop.Avalonia/App.axaml \
        tests/Tianming.Desktop.Avalonia.Tests/Controls/SearchBoxTests.cs
git commit -m "feat(controls): SearchBox primitive — 圆角灰底 + 🔍 前缀 icon + 内嵌 TextBox

Text(TwoWay) / Placeholder / SubmitCommand 3 个 StyledProperty。
🔍 暂用 emoji，后续 Lucide icon 接入时换掉。3 测试。"
```

---

### Task 31: SegmentedTabs primitive

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Controls/SegmentItem.cs`
- Create: `src/Tianming.Desktop.Avalonia/Controls/SegmentedTabs.cs`
- Create: `src/Tianming.Desktop.Avalonia/Controls/SegmentedTabs.axaml`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Controls/SegmentedTabsTests.cs`
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml`

- [ ] **Step 1: 共享 record + class**

Create `src/Tianming.Desktop.Avalonia/Controls/SegmentItem.cs`:

```csharp
namespace Tianming.Desktop.Avalonia.Controls;

public sealed record SegmentItem(string Key, string Label, string? IconGlyph = null);
```

Create `src/Tianming.Desktop.Avalonia/Controls/SegmentedTabs.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace Tianming.Desktop.Avalonia.Controls;

public class SegmentedTabs : TemplatedControl
{
    public static readonly StyledProperty<ObservableCollection<SegmentItem>> ItemsProperty =
        AvaloniaProperty.Register<SegmentedTabs, ObservableCollection<SegmentItem>>(
            nameof(Items), new ObservableCollection<SegmentItem>());

    public static readonly StyledProperty<string?> SelectedKeyProperty =
        AvaloniaProperty.Register<SegmentedTabs, string?>(nameof(SelectedKey));

    public static readonly StyledProperty<ICommand?> SelectCommandProperty =
        AvaloniaProperty.Register<SegmentedTabs, ICommand?>(nameof(SelectCommand));

    public ObservableCollection<SegmentItem> Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }
    public string? SelectedKey { get => GetValue(SelectedKeyProperty); set => SetValue(SelectedKeyProperty, value); }
    public ICommand? SelectCommand { get => GetValue(SelectCommandProperty); set => SetValue(SelectCommandProperty, value); }
}
```

- [ ] **Step 2: axaml**

Create `src/Tianming.Desktop.Avalonia/Controls/SegmentedTabs.axaml`:

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:controls="using:Tianming.Desktop.Avalonia.Controls">
  <ControlTheme x:Key="{x:Type controls:SegmentedTabs}" TargetType="controls:SegmentedTabs">
    <Setter Property="Template">
      <ControlTemplate>
        <Border Background="{DynamicResource SurfaceSubtleBrush}"
                CornerRadius="{DynamicResource RadiusMd}"
                Padding="2">
          <ItemsControl ItemsSource="{TemplateBinding Items}">
            <ItemsControl.ItemsPanel>
              <ItemsPanelTemplate>
                <StackPanel Orientation="Horizontal" Spacing="2"/>
              </ItemsPanelTemplate>
            </ItemsControl.ItemsPanel>
            <ItemsControl.ItemTemplate>
              <DataTemplate DataType="controls:SegmentItem">
                <Button Classes="ghost"
                        Command="{Binding $parent[controls:SegmentedTabs].SelectCommand}"
                        CommandParameter="{Binding Key}"
                        Padding="12,4"
                        CornerRadius="{DynamicResource RadiusSm}">
                  <TextBlock Text="{Binding Label}"
                             FontSize="{DynamicResource FontSizeSecondary}"
                             FontWeight="{DynamicResource FontWeightMedium}"/>
                </Button>
              </DataTemplate>
            </ItemsControl.ItemTemplate>
          </ItemsControl>
        </Border>
      </ControlTemplate>
    </Setter>
  </ControlTheme>
</ResourceDictionary>
```

> 选中态在视觉上靠 `SelectedKey` 跟 binding 切；MVP 暂不在 ControlTheme 里做"按当前 SelectedKey 给选中按钮加白底"。Sub-plan 2 真正消费时如果发现选中态视觉不够再加 `Classes.Selected` 双向 binding。

- [ ] **Step 3: App.axaml + 测试**

App.axaml 加 `<ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Controls/SegmentedTabs.axaml"/>`。

Create `tests/Tianming.Desktop.Avalonia.Tests/Controls/SegmentedTabsTests.cs`:

```csharp
using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Controls;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class SegmentedTabsTests
{
    [AvaloniaFact]
    public void Defaults_ItemsEmpty_SelectedKeyNull()
    {
        var s = new SegmentedTabs();
        Assert.Empty(s.Items);
        Assert.Null(s.SelectedKey);
        Assert.Null(s.SelectCommand);
    }

    [AvaloniaFact]
    public void AddItems_PersistsAndSelectedKeyWorks()
    {
        var s = new SegmentedTabs();
        s.Items.Add(new SegmentItem("ask", "Ask"));
        s.Items.Add(new SegmentItem("plan", "Plan"));
        s.Items.Add(new SegmentItem("agent", "Agent"));
        s.SelectedKey = "plan";
        Assert.Equal(3, s.Items.Count);
        Assert.Equal("plan", s.SelectedKey);
    }

    [AvaloniaFact]
    public void SetSelectCommand_Persists()
    {
        var cmd = new RelayCommand<string>(_ => { });
        var s = new SegmentedTabs { SelectCommand = cmd };
        Assert.Same(cmd, s.SelectCommand);
    }
}
```

- [ ] **Step 4: 跑测试 + commit**

```bash
dotnet build Tianming.MacMigration.sln -c Debug --nologo -v q
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ -c Debug --nologo --filter "FullyQualifiedName~SegmentedTabsTests" -v q
git add src/Tianming.Desktop.Avalonia/Controls/SegmentItem.cs \
        src/Tianming.Desktop.Avalonia/Controls/SegmentedTabs.cs \
        src/Tianming.Desktop.Avalonia/Controls/SegmentedTabs.axaml \
        src/Tianming.Desktop.Avalonia/App.axaml \
        tests/Tianming.Desktop.Avalonia.Tests/Controls/SegmentedTabsTests.cs
git commit -m "feat(controls): SegmentedTabs primitive — 水平段控制（Ask/Plan/Agent 等）

Items: ObservableCollection<SegmentItem>; SelectedKey; SelectCommand。
ControlTheme: 圆角灰底 + ItemsControl 横向；选中视觉在 Sub-plan 2 真用时再加 Classes.Selected。3 测试。"
```

---

### Task 32: BreadcrumbBar primitive（替换 AppChrome 里的内联 ItemsControl）

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Controls/BreadcrumbBar.cs`
- Create: `src/Tianming.Desktop.Avalonia/Controls/BreadcrumbBar.axaml`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Controls/BreadcrumbBarTests.cs`
- Modify: `src/Tianming.Desktop.Avalonia/Shell/AppChrome.axaml`
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml`

- [ ] **Step 1: class**

Create `src/Tianming.Desktop.Avalonia/Controls/BreadcrumbBar.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;
using Tianming.Desktop.Avalonia.Shell;

namespace Tianming.Desktop.Avalonia.Controls;

public class BreadcrumbBar : TemplatedControl
{
    public static readonly StyledProperty<ObservableCollection<BreadcrumbSegment>> SegmentsProperty =
        AvaloniaProperty.Register<BreadcrumbBar, ObservableCollection<BreadcrumbSegment>>(
            nameof(Segments), new ObservableCollection<BreadcrumbSegment>());

    public static readonly StyledProperty<ICommand?> NavigateCommandProperty =
        AvaloniaProperty.Register<BreadcrumbBar, ICommand?>(nameof(NavigateCommand));

    public ObservableCollection<BreadcrumbSegment> Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }
    public ICommand? NavigateCommand { get => GetValue(NavigateCommandProperty); set => SetValue(NavigateCommandProperty, value); }
}
```

- [ ] **Step 2: axaml**

Create `src/Tianming.Desktop.Avalonia/Controls/BreadcrumbBar.axaml`:

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:controls="using:Tianming.Desktop.Avalonia.Controls"
                    xmlns:shell="using:Tianming.Desktop.Avalonia.Shell">
  <ControlTheme x:Key="{x:Type controls:BreadcrumbBar}" TargetType="controls:BreadcrumbBar">
    <Setter Property="Template">
      <ControlTemplate>
        <ItemsControl ItemsSource="{TemplateBinding Segments}" VerticalAlignment="Center">
          <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate>
              <StackPanel Orientation="Horizontal" Spacing="6"/>
            </ItemsPanelTemplate>
          </ItemsControl.ItemsPanel>
          <ItemsControl.ItemTemplate>
            <DataTemplate DataType="shell:BreadcrumbSegment">
              <StackPanel Orientation="Horizontal" Spacing="6">
                <Button Classes="ghost"
                        Command="{Binding $parent[controls:BreadcrumbBar].NavigateCommand}"
                        CommandParameter="{Binding}"
                        Padding="4,2"
                        IsHitTestVisible="{Binding Target, Converter={x:Static ObjectConverters.IsNotNull}}">
                  <TextBlock Text="{Binding Label}"
                             FontSize="{DynamicResource FontSizeCaption}"
                             Foreground="{DynamicResource TextSecondaryBrush}"/>
                </Button>
                <TextBlock Text=" · "
                           FontSize="{DynamicResource FontSizeCaption}"
                           Foreground="{DynamicResource TextTertiaryBrush}"
                           VerticalAlignment="Center"/>
              </StackPanel>
            </DataTemplate>
          </ItemsControl.ItemTemplate>
        </ItemsControl>
      </ControlTemplate>
    </Setter>
  </ControlTheme>
</ResourceDictionary>
```

- [ ] **Step 3: App.axaml 注册 + AppChrome 替换内联 ItemsControl**

App.axaml 加 `<ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Controls/BreadcrumbBar.axaml"/>`。

修改 `src/Tianming.Desktop.Avalonia/Shell/AppChrome.axaml`，把 Grid.Column=1 的 `<ItemsControl>...</ItemsControl>` 整段替换为：

```xml
<controls:BreadcrumbBar Grid.Column="1"
                        Segments="{Binding Segments}"
                        NavigateCommand="{Binding NavigateCommand}"
                        VerticalAlignment="Center"/>
```

并在文件顶部 UserControl 标签里加命名空间引用：

```xml
xmlns:controls="using:Tianming.Desktop.Avalonia.Controls"
```

- [ ] **Step 4: 测试**

Create `tests/Tianming.Desktop.Avalonia.Tests/Controls/BreadcrumbBarTests.cs`:

```csharp
using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Controls;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.Shell;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class BreadcrumbBarTests
{
    [AvaloniaFact]
    public void Defaults_SegmentsEmpty_CommandNull()
    {
        var b = new BreadcrumbBar();
        Assert.Empty(b.Segments);
        Assert.Null(b.NavigateCommand);
    }

    [AvaloniaFact]
    public void AddSegments_Persists()
    {
        var b = new BreadcrumbBar();
        b.Segments.Add(new BreadcrumbSegment("天命", null));
        b.Segments.Add(new BreadcrumbSegment("欢迎", PageKeys.Welcome));
        Assert.Equal(2, b.Segments.Count);
        Assert.Null(b.Segments[0].Target);
        Assert.Equal(PageKeys.Welcome, b.Segments[1].Target);
    }

    [AvaloniaFact]
    public void SetNavigateCommand_Persists()
    {
        var cmd = new RelayCommand<BreadcrumbSegment>(_ => { });
        var b = new BreadcrumbBar { NavigateCommand = cmd };
        Assert.Same(cmd, b.NavigateCommand);
    }
}
```

- [ ] **Step 5: 跑测试 + 冒烟 + commit**

```bash
dotnet build Tianming.MacMigration.sln -c Debug --nologo -v q
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ -c Debug --nologo --filter "FullyQualifiedName~BreadcrumbBarTests" -v q
dotnet run --project src/Tianming.Desktop.Avalonia -c Debug 2>&1 | head -10
```

Expected：3 测试过；启动后 chrome 顶栏的 breadcrumb 视觉跟 Task 21 时基本一致（因为 BreadcrumbBar template 几乎复制了 AppChrome 里原 ItemsControl）。

```bash
git add src/Tianming.Desktop.Avalonia/Controls/BreadcrumbBar.cs \
        src/Tianming.Desktop.Avalonia/Controls/BreadcrumbBar.axaml \
        src/Tianming.Desktop.Avalonia/App.axaml \
        src/Tianming.Desktop.Avalonia/Shell/AppChrome.axaml \
        tests/Tianming.Desktop.Avalonia.Tests/Controls/BreadcrumbBarTests.cs
git commit -m "feat(controls): BreadcrumbBar primitive 抽离自 AppChrome 内联 ItemsControl

Segments + NavigateCommand 两个 StyledProperty；
AppChrome.axaml 用 <controls:BreadcrumbBar/> 替换原 ItemsControl 块。3 测试。"
```

---

### Task 33: NavRail primitive

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Controls/NavRailItem.cs`
- Create: `src/Tianming.Desktop.Avalonia/Controls/NavRailGroup.cs`
- Create: `src/Tianming.Desktop.Avalonia/Controls/NavRail.cs`
- Create: `src/Tianming.Desktop.Avalonia/Controls/NavRail.axaml`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Controls/NavRailTests.cs`
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml`

- [ ] **Step 1: 共享 record**

Create `src/Tianming.Desktop.Avalonia/Controls/NavRailItem.cs`:

```csharp
using Tianming.Desktop.Avalonia.Navigation;

namespace Tianming.Desktop.Avalonia.Controls;

public sealed record NavRailItem(PageKey Key, string Label, string IconGlyph, bool IsEnabled = true);
```

Create `src/Tianming.Desktop.Avalonia/Controls/NavRailGroup.cs`:

```csharp
using System.Collections.Generic;

namespace Tianming.Desktop.Avalonia.Controls;

public sealed record NavRailGroup(string Title, IReadOnlyList<NavRailItem> Items);
```

- [ ] **Step 2: class**

Create `src/Tianming.Desktop.Avalonia/Controls/NavRail.cs`:

```csharp
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;
using Tianming.Desktop.Avalonia.Navigation;

namespace Tianming.Desktop.Avalonia.Controls;

public class NavRail : TemplatedControl
{
    public static readonly StyledProperty<ObservableCollection<NavRailGroup>> GroupsProperty =
        AvaloniaProperty.Register<NavRail, ObservableCollection<NavRailGroup>>(
            nameof(Groups), new ObservableCollection<NavRailGroup>());

    public static readonly StyledProperty<PageKey?> ActiveKeyProperty =
        AvaloniaProperty.Register<NavRail, PageKey?>(nameof(ActiveKey));

    public static readonly StyledProperty<ICommand?> NavigateCommandProperty =
        AvaloniaProperty.Register<NavRail, ICommand?>(nameof(NavigateCommand));

    public ObservableCollection<NavRailGroup> Groups
    {
        get => GetValue(GroupsProperty);
        set => SetValue(GroupsProperty, value);
    }
    public PageKey? ActiveKey { get => GetValue(ActiveKeyProperty); set => SetValue(ActiveKeyProperty, value); }
    public ICommand? NavigateCommand { get => GetValue(NavigateCommandProperty); set => SetValue(NavigateCommandProperty, value); }
}
```

- [ ] **Step 3: axaml**

Create `src/Tianming.Desktop.Avalonia/Controls/NavRail.axaml`:

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:controls="using:Tianming.Desktop.Avalonia.Controls">
  <ControlTheme x:Key="{x:Type controls:NavRail}" TargetType="controls:NavRail">
    <Setter Property="Padding" Value="8"/>
    <Setter Property="Template">
      <ControlTemplate>
        <ScrollViewer HorizontalScrollBarVisibility="Disabled">
          <ItemsControl ItemsSource="{TemplateBinding Groups}"
                        Padding="{TemplateBinding Padding}">
            <ItemsControl.ItemTemplate>
              <DataTemplate DataType="controls:NavRailGroup">
                <StackPanel Spacing="2" Margin="0,0,0,12">
                  <TextBlock Text="{Binding Title}"
                             FontSize="{DynamicResource FontSizeCaption}"
                             FontWeight="{DynamicResource FontWeightSemibold}"
                             Foreground="{DynamicResource TextTertiaryBrush}"
                             Margin="8,0,0,4"/>
                  <ItemsControl ItemsSource="{Binding Items}">
                    <ItemsControl.ItemTemplate>
                      <DataTemplate DataType="controls:NavRailItem">
                        <Button Classes="ghost"
                                Command="{Binding $parent[controls:NavRail].NavigateCommand}"
                                CommandParameter="{Binding Key}"
                                IsEnabled="{Binding IsEnabled}"
                                HorizontalAlignment="Stretch"
                                HorizontalContentAlignment="Left"
                                Padding="8,6">
                          <StackPanel Orientation="Horizontal" Spacing="8">
                            <TextBlock Text="{Binding IconGlyph}"
                                       Width="16"
                                       FontSize="{DynamicResource FontSizeBody}"
                                       VerticalAlignment="Center"
                                       Foreground="{DynamicResource TextSecondaryBrush}"/>
                            <TextBlock Text="{Binding Label}"
                                       FontSize="{DynamicResource FontSizeBody}"
                                       Foreground="{DynamicResource TextPrimaryBrush}"
                                       VerticalAlignment="Center"/>
                          </StackPanel>
                        </Button>
                      </DataTemplate>
                    </ItemsControl.ItemTemplate>
                  </ItemsControl>
                </StackPanel>
              </DataTemplate>
            </ItemsControl.ItemTemplate>
          </ItemsControl>
        </ScrollViewer>
      </ControlTemplate>
    </Setter>
  </ControlTheme>
</ResourceDictionary>
```

> ActiveKey 高亮：Sub-plan 2 真消费时按 ActiveKey 在 ItemTemplate 套 `Classes.Active`，基建只先暴露 API。

- [ ] **Step 4: App.axaml + 测试**

App.axaml 加 `<ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Controls/NavRail.axaml"/>`。

Create `tests/Tianming.Desktop.Avalonia.Tests/Controls/NavRailTests.cs`:

```csharp
using System.Collections.Generic;
using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Controls;
using Tianming.Desktop.Avalonia.Navigation;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class NavRailTests
{
    [AvaloniaFact]
    public void Defaults_GroupsEmpty_ActiveKeyNull_CommandNull()
    {
        var r = new NavRail();
        Assert.Empty(r.Groups);
        Assert.Null(r.ActiveKey);
        Assert.Null(r.NavigateCommand);
    }

    [AvaloniaFact]
    public void AddGroups_Persists()
    {
        var r = new NavRail();
        r.Groups.Add(new NavRailGroup("写作", new List<NavRailItem>
        {
            new(PageKeys.Welcome, "欢迎", "home"),
            new(PageKeys.Dashboard, "仪表盘", "layout-dashboard"),
        }));
        Assert.Single(r.Groups);
        Assert.Equal(2, r.Groups[0].Items.Count);
        Assert.Equal("欢迎", r.Groups[0].Items[0].Label);
    }

    [AvaloniaFact]
    public void SetActiveKey_AndCommand_Persist()
    {
        var cmd = new RelayCommand<PageKey>(_ => { });
        var r = new NavRail { ActiveKey = PageKeys.Welcome, NavigateCommand = cmd };
        Assert.Equal(PageKeys.Welcome, r.ActiveKey);
        Assert.Same(cmd, r.NavigateCommand);
    }
}
```

- [ ] **Step 5: 跑测试 + commit**

```bash
dotnet build Tianming.MacMigration.sln -c Debug --nologo -v q
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ -c Debug --nologo --filter "FullyQualifiedName~NavRailTests" -v q
git add src/Tianming.Desktop.Avalonia/Controls/NavRailItem.cs \
        src/Tianming.Desktop.Avalonia/Controls/NavRailGroup.cs \
        src/Tianming.Desktop.Avalonia/Controls/NavRail.cs \
        src/Tianming.Desktop.Avalonia/Controls/NavRail.axaml \
        src/Tianming.Desktop.Avalonia/App.axaml \
        tests/Tianming.Desktop.Avalonia.Tests/Controls/NavRailTests.cs
git commit -m "feat(controls): NavRail primitive — 左侧分组导航容器

Groups: ObservableCollection<NavRailGroup>; ActiveKey: PageKey?; NavigateCommand。
ControlTheme: 两层 ItemsControl（组 → 项）；项用 ghost button 渲染 icon + label。3 测试。"
```

---

### Task 34: SidebarTreeItem primitive

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Controls/SidebarTreeItem.cs`
- Create: `src/Tianming.Desktop.Avalonia/Controls/SidebarTreeItem.axaml`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Controls/SidebarTreeItemTests.cs`
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml`

- [ ] **Step 1: class**

Create `src/Tianming.Desktop.Avalonia/Controls/SidebarTreeItem.cs`:

```csharp
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Tianming.Desktop.Avalonia.Controls;

public class SidebarTreeItem : TemplatedControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<SidebarTreeItem, string>(nameof(Label), string.Empty);
    public static readonly StyledProperty<string?> IconGlyphProperty =
        AvaloniaProperty.Register<SidebarTreeItem, string?>(nameof(IconGlyph));
    public static readonly StyledProperty<Control?> TrailingProperty =
        AvaloniaProperty.Register<SidebarTreeItem, Control?>(nameof(Trailing));
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<SidebarTreeItem, bool>(nameof(IsExpanded), false);
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<SidebarTreeItem, bool>(nameof(IsSelected), false);
    public static readonly StyledProperty<int> DepthProperty =
        AvaloniaProperty.Register<SidebarTreeItem, int>(nameof(Depth), 0);
    public static readonly StyledProperty<ObservableCollection<SidebarTreeItem>?> ChildrenProperty =
        AvaloniaProperty.Register<SidebarTreeItem, ObservableCollection<SidebarTreeItem>?>(nameof(Children));

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? IconGlyph { get => GetValue(IconGlyphProperty); set => SetValue(IconGlyphProperty, value); }
    public Control? Trailing { get => GetValue(TrailingProperty); set => SetValue(TrailingProperty, value); }
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
    public int Depth { get => GetValue(DepthProperty); set => SetValue(DepthProperty, value); }
    public ObservableCollection<SidebarTreeItem>? Children { get => GetValue(ChildrenProperty); set => SetValue(ChildrenProperty, value); }
}
```

- [ ] **Step 2: axaml**

Create `src/Tianming.Desktop.Avalonia/Controls/SidebarTreeItem.axaml`:

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:controls="using:Tianming.Desktop.Avalonia.Controls">
  <ControlTheme x:Key="{x:Type controls:SidebarTreeItem}" TargetType="controls:SidebarTreeItem">
    <Setter Property="Template">
      <ControlTemplate>
        <StackPanel>
          <Border x:Name="PART_Row"
                  Padding="6,4"
                  CornerRadius="{DynamicResource RadiusSm}">
            <DockPanel LastChildFill="True">
              <!-- 缩进 spacer：每层 16px -->
              <Border DockPanel.Dock="Left" Width="{Binding Depth, RelativeSource={RelativeSource TemplatedParent}, Converter={x:Static controls:DepthToWidthConverter.Instance}}"/>
              <TextBlock DockPanel.Dock="Left" Width="14"
                         Text="{TemplateBinding IsExpanded, Converter={x:Static controls:ExpandedToChevronConverter.Instance}}"
                         FontSize="{DynamicResource FontSizeCaption}"
                         Foreground="{DynamicResource TextTertiaryBrush}"
                         VerticalAlignment="Center"
                         IsVisible="{TemplateBinding Children, Converter={x:Static ObjectConverters.IsNotNull}}"/>
              <TextBlock DockPanel.Dock="Left"
                         Text="{TemplateBinding IconGlyph}"
                         Width="16"
                         Margin="0,0,6,0"
                         FontSize="{DynamicResource FontSizeBody}"
                         Foreground="{DynamicResource TextSecondaryBrush}"
                         VerticalAlignment="Center"
                         IsVisible="{TemplateBinding IconGlyph, Converter={x:Static ObjectConverters.IsNotNull}}"/>
              <ContentPresenter DockPanel.Dock="Right" Content="{TemplateBinding Trailing}" VerticalAlignment="Center"/>
              <TextBlock Text="{TemplateBinding Label}"
                         FontSize="{DynamicResource FontSizeBody}"
                         Foreground="{DynamicResource TextPrimaryBrush}"
                         VerticalAlignment="Center"/>
            </DockPanel>
          </Border>

          <!-- 子节点 -->
          <ItemsControl ItemsSource="{TemplateBinding Children}"
                        IsVisible="{TemplateBinding IsExpanded}"/>
        </StackPanel>
      </ControlTemplate>
    </Setter>

    <Style Selector="^[IsSelected=True] /template/ Border#PART_Row">
      <Setter Property="Background" Value="{DynamicResource AccentSubtleBrush}"/>
    </Style>
  </ControlTheme>
</ResourceDictionary>
```

- [ ] **Step 3: 加 2 个简单 Converter**

Create `src/Tianming.Desktop.Avalonia/Controls/SidebarTreeItemConverters.cs`:

```csharp
using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Tianming.Desktop.Avalonia.Controls;

internal sealed class DepthToWidthConverter : IValueConverter
{
    public static readonly DepthToWidthConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int d ? d * 16.0 : 0.0;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

internal sealed class ExpandedToChevronConverter : IValueConverter
{
    public static readonly ExpandedToChevronConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? "▾" : "▸";
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

- [ ] **Step 4: App.axaml 注册 + 测试**

App.axaml 加 `<ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Controls/SidebarTreeItem.axaml"/>`。

Create `tests/Tianming.Desktop.Avalonia.Tests/Controls/SidebarTreeItemTests.cs`:

```csharp
using System.Collections.ObjectModel;
using Avalonia.Headless.XUnit;
using Tianming.Desktop.Avalonia.Controls;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class SidebarTreeItemTests
{
    [AvaloniaFact]
    public void Defaults_LabelEmpty_IconNull_NotSelected_DepthZero()
    {
        var i = new SidebarTreeItem();
        Assert.Equal(string.Empty, i.Label);
        Assert.Null(i.IconGlyph);
        Assert.False(i.IsSelected);
        Assert.False(i.IsExpanded);
        Assert.Equal(0, i.Depth);
        Assert.Null(i.Children);
    }

    [AvaloniaFact]
    public void SetLabelAndDepth_Persists()
    {
        var i = new SidebarTreeItem { Label = "第一卷 · 序章", Depth = 1, IsExpanded = true };
        Assert.Equal("第一卷 · 序章", i.Label);
        Assert.Equal(1, i.Depth);
        Assert.True(i.IsExpanded);
    }

    [AvaloniaFact]
    public void Children_CanBeSet()
    {
        var children = new ObservableCollection<SidebarTreeItem>
        {
            new() { Label = "第 1 章", Depth = 2 },
            new() { Label = "第 2 章", Depth = 2 },
        };
        var i = new SidebarTreeItem { Label = "第一卷", Depth = 1, Children = children };
        Assert.Equal(2, i.Children!.Count);
    }
}
```

- [ ] **Step 5: 跑测试 + commit**

```bash
dotnet build Tianming.MacMigration.sln -c Debug --nologo -v q
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ -c Debug --nologo --filter "FullyQualifiedName~SidebarTreeItemTests" -v q
git add src/Tianming.Desktop.Avalonia/Controls/SidebarTreeItem.cs \
        src/Tianming.Desktop.Avalonia/Controls/SidebarTreeItem.axaml \
        src/Tianming.Desktop.Avalonia/Controls/SidebarTreeItemConverters.cs \
        src/Tianming.Desktop.Avalonia/App.axaml \
        tests/Tianming.Desktop.Avalonia.Tests/Controls/SidebarTreeItemTests.cs
git commit -m "feat(controls): SidebarTreeItem primitive — 可折叠树节点

7 个 StyledProperty (Label / IconGlyph / Trailing / IsExpanded / IsSelected / Depth / Children)；
ControlTheme: depth*16px 缩进 + ▸/▾ 折叠 chevron + icon + label + Trailing slot + 嵌套 ItemsControl 渲染 Children。
2 个内部 Converter (DepthToWidth / ExpandedToChevron)。3 测试。"
```

---

### Task 35: ProjectCard primitive

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Controls/ProjectCard.cs`
- Create: `src/Tianming.Desktop.Avalonia/Controls/ProjectCard.axaml`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Controls/ProjectCardTests.cs`
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml`

- [ ] **Step 1: class**

Create `src/Tianming.Desktop.Avalonia/Controls/ProjectCard.cs`:

```csharp
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Tianming.Desktop.Avalonia.Controls;

public class ProjectCard : TemplatedControl
{
    public static readonly StyledProperty<string> ProjectNameProperty =
        AvaloniaProperty.Register<ProjectCard, string>(nameof(ProjectName), string.Empty);
    public static readonly StyledProperty<IImage?> CoverProperty =
        AvaloniaProperty.Register<ProjectCard, IImage?>(nameof(Cover));
    public static readonly StyledProperty<string?> LastOpenedTextProperty =
        AvaloniaProperty.Register<ProjectCard, string?>(nameof(LastOpenedText));
    public static readonly StyledProperty<string?> ChapterProgressProperty =
        AvaloniaProperty.Register<ProjectCard, string?>(nameof(ChapterProgress));
    public static readonly StyledProperty<double> ProgressPercentProperty =
        AvaloniaProperty.Register<ProjectCard, double>(nameof(ProgressPercent), 0.0);
    public static readonly StyledProperty<ICommand?> OpenCommandProperty =
        AvaloniaProperty.Register<ProjectCard, ICommand?>(nameof(OpenCommand));

    public string ProjectName { get => GetValue(ProjectNameProperty); set => SetValue(ProjectNameProperty, value); }
    public IImage? Cover { get => GetValue(CoverProperty); set => SetValue(CoverProperty, value); }
    public string? LastOpenedText { get => GetValue(LastOpenedTextProperty); set => SetValue(LastOpenedTextProperty, value); }
    public string? ChapterProgress { get => GetValue(ChapterProgressProperty); set => SetValue(ChapterProgressProperty, value); }
    public double ProgressPercent { get => GetValue(ProgressPercentProperty); set => SetValue(ProgressPercentProperty, value); }
    public ICommand? OpenCommand { get => GetValue(OpenCommandProperty); set => SetValue(OpenCommandProperty, value); }
}
```

- [ ] **Step 2: axaml**

Create `src/Tianming.Desktop.Avalonia/Controls/ProjectCard.axaml`:

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:controls="using:Tianming.Desktop.Avalonia.Controls">
  <ControlTheme x:Key="{x:Type controls:ProjectCard}" TargetType="controls:ProjectCard">
    <Setter Property="Width" Value="200"/>
    <Setter Property="Template">
      <ControlTemplate>
        <Border Background="{DynamicResource SurfaceBaseBrush}"
                CornerRadius="{DynamicResource RadiusLg}"
                BorderBrush="{DynamicResource BorderSubtleBrush}"
                BorderThickness="1"
                BoxShadow="{DynamicResource ShadowSm}">
          <StackPanel>
            <!-- Cover -->
            <Border Height="120"
                    CornerRadius="8,8,0,0"
                    Background="{DynamicResource SurfaceSubtleBrush}"
                    ClipToBounds="True">
              <Image Source="{TemplateBinding Cover}" Stretch="UniformToFill"/>
            </Border>
            <!-- Body -->
            <StackPanel Margin="12" Spacing="6">
              <TextBlock Text="{TemplateBinding ProjectName}"
                         FontSize="{DynamicResource FontSizeH3}"
                         FontWeight="{DynamicResource FontWeightSemibold}"
                         Foreground="{DynamicResource TextPrimaryBrush}"
                         TextTrimming="CharacterEllipsis"/>
              <TextBlock Text="{TemplateBinding LastOpenedText}"
                         FontSize="{DynamicResource FontSizeCaption}"
                         Foreground="{DynamicResource TextTertiaryBrush}"
                         IsVisible="{TemplateBinding LastOpenedText, Converter={x:Static ObjectConverters.IsNotNull}}"/>
              <Grid ColumnDefinitions="*,Auto">
                <ProgressBar Grid.Column="0"
                             Value="{TemplateBinding ProgressPercent}"
                             Maximum="1.0"
                             Height="4"
                             VerticalAlignment="Center"
                             Background="{DynamicResource SurfaceMutedBrush}"
                             Foreground="{DynamicResource AccentBaseBrush}"/>
                <TextBlock Grid.Column="1"
                           Text="{TemplateBinding ChapterProgress}"
                           FontSize="{DynamicResource FontSizeCaption}"
                           Foreground="{DynamicResource TextSecondaryBrush}"
                           Margin="8,0,0,0"
                           VerticalAlignment="Center"
                           IsVisible="{TemplateBinding ChapterProgress, Converter={x:Static ObjectConverters.IsNotNull}}"/>
              </Grid>
            </StackPanel>
          </StackPanel>
        </Border>
      </ControlTemplate>
    </Setter>
  </ControlTheme>
</ResourceDictionary>
```

- [ ] **Step 3: App.axaml + 测试**

App.axaml 加 `<ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Controls/ProjectCard.axaml"/>`。

Create `tests/Tianming.Desktop.Avalonia.Tests/Controls/ProjectCardTests.cs`:

```csharp
using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Controls;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class ProjectCardTests
{
    [AvaloniaFact]
    public void Defaults_NameEmpty_CoverNull_ProgressZero()
    {
        var c = new ProjectCard();
        Assert.Equal(string.Empty, c.ProjectName);
        Assert.Null(c.Cover);
        Assert.Null(c.LastOpenedText);
        Assert.Null(c.ChapterProgress);
        Assert.Equal(0.0, c.ProgressPercent);
        Assert.Null(c.OpenCommand);
    }

    [AvaloniaFact]
    public void SetAllProperties_Persists()
    {
        var c = new ProjectCard
        {
            ProjectName = "第九纪元",
            LastOpenedText = "3 小时前",
            ChapterProgress = "12/60",
            ProgressPercent = 0.2
        };
        Assert.Equal("第九纪元", c.ProjectName);
        Assert.Equal("3 小时前", c.LastOpenedText);
        Assert.Equal("12/60", c.ChapterProgress);
        Assert.Equal(0.2, c.ProgressPercent);
    }

    [AvaloniaFact]
    public void SetOpenCommand_Persists()
    {
        var cmd = new RelayCommand(() => { });
        var c = new ProjectCard { OpenCommand = cmd };
        Assert.Same(cmd, c.OpenCommand);
    }
}
```

- [ ] **Step 4: 跑 + commit**

```bash
dotnet build Tianming.MacMigration.sln -c Debug --nologo -v q
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ -c Debug --nologo --filter "FullyQualifiedName~ProjectCardTests" -v q
git add src/Tianming.Desktop.Avalonia/Controls/ProjectCard.cs \
        src/Tianming.Desktop.Avalonia/Controls/ProjectCard.axaml \
        src/Tianming.Desktop.Avalonia/App.axaml \
        tests/Tianming.Desktop.Avalonia.Tests/Controls/ProjectCardTests.cs
git commit -m "feat(controls): ProjectCard primitive — 封面 + 名称 + 元信息 + 进度条

6 个 StyledProperty (ProjectName / Cover / LastOpenedText / ChapterProgress / ProgressPercent / OpenCommand)；
ControlTheme: 120px Cover + 12px padding 内容区 + 4px ProgressBar 走 accent。3 测试。"
```

---

### Task 36: DataGridRowCell primitive

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Controls/DataGridRowCell.cs`
- Create: `src/Tianming.Desktop.Avalonia/Controls/DataGridRowCell.axaml`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Controls/DataGridRowCellTests.cs`
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml`

- [ ] **Step 1: enum + class**

Create `src/Tianming.Desktop.Avalonia/Controls/DataGridRowCell.cs`:

```csharp
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;
using Tianming.Desktop.Avalonia.Shell;

namespace Tianming.Desktop.Avalonia.Controls;

public enum DataGridCellKind { Text, Badge, Number, Link }

public class DataGridRowCell : TemplatedControl
{
    public static readonly StyledProperty<DataGridCellKind> KindProperty =
        AvaloniaProperty.Register<DataGridRowCell, DataGridCellKind>(nameof(Kind), DataGridCellKind.Text);

    public static readonly StyledProperty<string> ContentProperty =
        AvaloniaProperty.Register<DataGridRowCell, string>(nameof(Content), string.Empty);

    public static readonly StyledProperty<StatusKind?> BadgeKindProperty =
        AvaloniaProperty.Register<DataGridRowCell, StatusKind?>(nameof(BadgeKind));

    public static readonly StyledProperty<ICommand?> ClickCommandProperty =
        AvaloniaProperty.Register<DataGridRowCell, ICommand?>(nameof(ClickCommand));

    public DataGridCellKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public new string Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }
    public StatusKind? BadgeKind { get => GetValue(BadgeKindProperty); set => SetValue(BadgeKindProperty, value); }
    public ICommand? ClickCommand { get => GetValue(ClickCommandProperty); set => SetValue(ClickCommandProperty, value); }
}
```

- [ ] **Step 2: axaml — 4 个 Kind 切换**

Create `src/Tianming.Desktop.Avalonia/Controls/DataGridRowCell.axaml`:

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:controls="using:Tianming.Desktop.Avalonia.Controls">
  <ControlTheme x:Key="{x:Type controls:DataGridRowCell}" TargetType="controls:DataGridRowCell">
    <Setter Property="VerticalAlignment" Value="Center"/>
    <Setter Property="Template">
      <ControlTemplate>
        <Panel>
          <!-- Text -->
          <TextBlock x:Name="PART_Text"
                     Text="{TemplateBinding Content}"
                     FontSize="{DynamicResource FontSizeBody}"
                     Foreground="{DynamicResource TextPrimaryBrush}"
                     VerticalAlignment="Center"/>
          <!-- Badge -->
          <controls:BadgePill x:Name="PART_Badge"
                              Text="{TemplateBinding Content}"
                              Kind="{TemplateBinding BadgeKind, Converter={x:Static controls:BadgeKindFallbackConverter.Instance}}"
                              IsVisible="False"/>
          <!-- Link -->
          <Button x:Name="PART_Link"
                  Classes="ghost"
                  Command="{TemplateBinding ClickCommand}"
                  Padding="0"
                  IsVisible="False">
            <TextBlock Text="{TemplateBinding Content}"
                       Foreground="{DynamicResource AccentBaseBrush}"
                       FontSize="{DynamicResource FontSizeBody}"/>
          </Button>
        </Panel>
      </ControlTemplate>
    </Setter>

    <Style Selector="^[Kind=Badge] /template/ TextBlock#PART_Text">
      <Setter Property="IsVisible" Value="False"/>
    </Style>
    <Style Selector="^[Kind=Badge] /template/ controls|BadgePill#PART_Badge">
      <Setter Property="IsVisible" Value="True"/>
    </Style>

    <Style Selector="^[Kind=Number] /template/ TextBlock#PART_Text">
      <Setter Property="HorizontalAlignment" Value="Right"/>
      <Setter Property="FontFamily" Value="{DynamicResource FontMono}"/>
    </Style>

    <Style Selector="^[Kind=Link] /template/ TextBlock#PART_Text">
      <Setter Property="IsVisible" Value="False"/>
    </Style>
    <Style Selector="^[Kind=Link] /template/ Button#PART_Link">
      <Setter Property="IsVisible" Value="True"/>
    </Style>
  </ControlTheme>
</ResourceDictionary>
```

- [ ] **Step 3: BadgeKindFallbackConverter**

Append to `src/Tianming.Desktop.Avalonia/Controls/SidebarTreeItemConverters.cs`（同文件凑齐 controls 共享 converter）:

```csharp
using Tianming.Desktop.Avalonia.Shell;

internal sealed class BadgeKindFallbackConverter : IValueConverter
{
    public static readonly BadgeKindFallbackConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is StatusKind k ? k : StatusKind.Neutral;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

记得在文件顶部 `using` 加 `using Tianming.Desktop.Avalonia.Shell;`，并改文件名（可选）为 `ControlsConverters.cs` 更准确（这一步如果改名要更新 csproj 隐式 glob，不影响）。

- [ ] **Step 4: App.axaml + 测试**

App.axaml 加 `<ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Controls/DataGridRowCell.axaml"/>`。

Create `tests/Tianming.Desktop.Avalonia.Tests/Controls/DataGridRowCellTests.cs`:

```csharp
using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Controls;
using Tianming.Desktop.Avalonia.Shell;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class DataGridRowCellTests
{
    [AvaloniaFact]
    public void Defaults_KindText_ContentEmpty()
    {
        var c = new DataGridRowCell();
        Assert.Equal(DataGridCellKind.Text, c.Kind);
        Assert.Equal(string.Empty, c.Content);
        Assert.Null(c.BadgeKind);
        Assert.Null(c.ClickCommand);
    }

    [AvaloniaFact]
    public void SetKindAndContent_Persists()
    {
        var c = new DataGridRowCell { Kind = DataGridCellKind.Badge, Content = "已发布", BadgeKind = StatusKind.Success };
        Assert.Equal(DataGridCellKind.Badge, c.Kind);
        Assert.Equal("已发布", c.Content);
        Assert.Equal(StatusKind.Success, c.BadgeKind);
    }

    [AvaloniaFact]
    public void Link_SetClickCommand_Persists()
    {
        var cmd = new RelayCommand(() => { });
        var c = new DataGridRowCell { Kind = DataGridCellKind.Link, Content = "查看", ClickCommand = cmd };
        Assert.Same(cmd, c.ClickCommand);
    }
}
```

- [ ] **Step 5: 跑 + commit**

```bash
dotnet build Tianming.MacMigration.sln -c Debug --nologo -v q
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ -c Debug --nologo --filter "FullyQualifiedName~DataGridRowCellTests" -v q
git add src/Tianming.Desktop.Avalonia/Controls/DataGridRowCell.cs \
        src/Tianming.Desktop.Avalonia/Controls/DataGridRowCell.axaml \
        src/Tianming.Desktop.Avalonia/Controls/SidebarTreeItemConverters.cs \
        src/Tianming.Desktop.Avalonia/App.axaml \
        tests/Tianming.Desktop.Avalonia.Tests/Controls/DataGridRowCellTests.cs
git commit -m "feat(controls): DataGridRowCell primitive — Text/Badge/Number/Link 4 种 cell

Kind / Content / BadgeKind / ClickCommand 4 个 StyledProperty；
4 个 PART 互斥显示；Number 走 FontMono + 右对齐；Link 走 ghost button + accent 字色。
3 测试 + 共享 BadgeKindFallbackConverter。"
```

---

### Task 37: ConversationBubble primitive

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Controls/ConversationRole.cs`
- Create: `src/Tianming.Desktop.Avalonia/Controls/ReferenceTag.cs`
- Create: `src/Tianming.Desktop.Avalonia/Controls/ConversationBubble.cs`
- Create: `src/Tianming.Desktop.Avalonia/Controls/ConversationBubble.axaml`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Controls/ConversationBubbleTests.cs`
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml`

- [ ] **Step 1: 共享类型**

Create `src/Tianming.Desktop.Avalonia/Controls/ConversationRole.cs`:

```csharp
namespace Tianming.Desktop.Avalonia.Controls;

public enum ConversationRole { User, Assistant }
```

Create `src/Tianming.Desktop.Avalonia/Controls/ReferenceTag.cs`:

```csharp
namespace Tianming.Desktop.Avalonia.Controls;

public sealed record ReferenceTag(string Label, string? Tooltip = null);
```

- [ ] **Step 2: class**

Create `src/Tianming.Desktop.Avalonia/Controls/ConversationBubble.cs`:

```csharp
using System;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace Tianming.Desktop.Avalonia.Controls;

public class ConversationBubble : TemplatedControl
{
    public static readonly StyledProperty<ConversationRole> RoleProperty =
        AvaloniaProperty.Register<ConversationBubble, ConversationRole>(nameof(Role), ConversationRole.User);
    public static readonly StyledProperty<string> ContentTextProperty =
        AvaloniaProperty.Register<ConversationBubble, string>(nameof(ContentText), string.Empty);
    public static readonly StyledProperty<string?> ThinkingBlockProperty =
        AvaloniaProperty.Register<ConversationBubble, string?>(nameof(ThinkingBlock));
    public static readonly StyledProperty<ObservableCollection<ReferenceTag>?> ReferencesProperty =
        AvaloniaProperty.Register<ConversationBubble, ObservableCollection<ReferenceTag>?>(nameof(References));
    public static readonly StyledProperty<bool> IsStreamingProperty =
        AvaloniaProperty.Register<ConversationBubble, bool>(nameof(IsStreaming), false);
    public static readonly StyledProperty<DateTime> TimestampProperty =
        AvaloniaProperty.Register<ConversationBubble, DateTime>(nameof(Timestamp));

    public ConversationRole Role { get => GetValue(RoleProperty); set => SetValue(RoleProperty, value); }
    public string ContentText { get => GetValue(ContentTextProperty); set => SetValue(ContentTextProperty, value); }
    public string? ThinkingBlock { get => GetValue(ThinkingBlockProperty); set => SetValue(ThinkingBlockProperty, value); }
    public ObservableCollection<ReferenceTag>? References { get => GetValue(ReferencesProperty); set => SetValue(ReferencesProperty, value); }
    public bool IsStreaming { get => GetValue(IsStreamingProperty); set => SetValue(IsStreamingProperty, value); }
    public DateTime Timestamp { get => GetValue(TimestampProperty); set => SetValue(TimestampProperty, value); }
}
```

- [ ] **Step 3: axaml**

Create `src/Tianming.Desktop.Avalonia/Controls/ConversationBubble.axaml`:

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:controls="using:Tianming.Desktop.Avalonia.Controls">
  <ControlTheme x:Key="{x:Type controls:ConversationBubble}" TargetType="controls:ConversationBubble">
    <Setter Property="Template">
      <ControlTemplate>
        <Border x:Name="PART_Root"
                Background="{DynamicResource SurfaceSubtleBrush}"
                CornerRadius="{DynamicResource RadiusLg}"
                Padding="12"
                MaxWidth="640">
          <StackPanel Spacing="6">
            <!-- Thinking block 可折叠占位（基建不真实现折叠状态机，view 自己接） -->
            <Border IsVisible="{TemplateBinding ThinkingBlock, Converter={x:Static ObjectConverters.IsNotNull}}"
                    Background="{DynamicResource SurfaceMutedBrush}"
                    CornerRadius="{DynamicResource RadiusSm}"
                    Padding="8">
              <TextBlock Text="{TemplateBinding ThinkingBlock}"
                         FontFamily="{DynamicResource FontMono}"
                         FontSize="{DynamicResource FontSizeCaption}"
                         Foreground="{DynamicResource TextSecondaryBrush}"
                         TextWrapping="Wrap"/>
            </Border>

            <!-- Body -->
            <SelectableTextBlock Text="{TemplateBinding ContentText}"
                                 FontSize="{DynamicResource FontSizeBody}"
                                 Foreground="{DynamicResource TextPrimaryBrush}"
                                 LineHeight="{DynamicResource LineHeightRelaxed}"
                                 TextWrapping="Wrap"/>

            <!-- References (chips) -->
            <ItemsControl ItemsSource="{TemplateBinding References}"
                          IsVisible="{TemplateBinding References, Converter={x:Static ObjectConverters.IsNotNull}}">
              <ItemsControl.ItemsPanel>
                <ItemsPanelTemplate>
                  <WrapPanel Orientation="Horizontal"/>
                </ItemsPanelTemplate>
              </ItemsControl.ItemsPanel>
              <ItemsControl.ItemTemplate>
                <DataTemplate DataType="controls:ReferenceTag">
                  <controls:BadgePill Text="{Binding Label}" Kind="Info" Margin="0,4,4,0"/>
                </DataTemplate>
              </ItemsControl.ItemTemplate>
            </ItemsControl>

            <!-- Footer：streaming 指示 + 时间戳 -->
            <Grid ColumnDefinitions="*,Auto" Margin="0,4,0,0">
              <TextBlock Grid.Column="0"
                         Text="正在生成…"
                         FontSize="{DynamicResource FontSizeCaption}"
                         Foreground="{DynamicResource TextTertiaryBrush}"
                         IsVisible="{TemplateBinding IsStreaming}"/>
              <TextBlock Grid.Column="1"
                         Text="{TemplateBinding Timestamp, Converter={x:Static controls:TimestampShortConverter.Instance}}"
                         FontSize="{DynamicResource FontSizeCaption}"
                         Foreground="{DynamicResource TextTertiaryBrush}"/>
            </Grid>
          </StackPanel>
        </Border>
      </ControlTemplate>
    </Setter>

    <!-- Role=Assistant：保持 subtle 灰底 + 左对齐（默认） -->
    <!-- Role=User：accent 浅底 + 右对齐 -->
    <Style Selector="^[Role=User] /template/ Border#PART_Root">
      <Setter Property="Background" Value="{DynamicResource AccentSubtleBrush}"/>
      <Setter Property="HorizontalAlignment" Value="Right"/>
    </Style>
  </ControlTheme>
</ResourceDictionary>
```

- [ ] **Step 4: TimestampShortConverter（追加到 SidebarTreeItemConverters.cs）**

```csharp
internal sealed class TimestampShortConverter : IValueConverter
{
    public static readonly TimestampShortConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateTime dt && dt != default ? dt.ToString("HH:mm", culture) : string.Empty;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

- [ ] **Step 5: App.axaml + 测试**

App.axaml 加 `<ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Controls/ConversationBubble.axaml"/>`。

Create `tests/Tianming.Desktop.Avalonia.Tests/Controls/ConversationBubbleTests.cs`:

```csharp
using System;
using System.Collections.ObjectModel;
using Avalonia.Headless.XUnit;
using Tianming.Desktop.Avalonia.Controls;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class ConversationBubbleTests
{
    [AvaloniaFact]
    public void Defaults_RoleUser_ContentEmpty_NotStreaming()
    {
        var b = new ConversationBubble();
        Assert.Equal(ConversationRole.User, b.Role);
        Assert.Equal(string.Empty, b.ContentText);
        Assert.Null(b.ThinkingBlock);
        Assert.Null(b.References);
        Assert.False(b.IsStreaming);
    }

    [AvaloniaFact]
    public void SetAssistantContent_Persists()
    {
        var b = new ConversationBubble
        {
            Role = ConversationRole.Assistant,
            ContentText = "好的，根据剧情设计……",
            IsStreaming = true,
            Timestamp = new DateTime(2026, 5, 13, 14, 30, 0)
        };
        Assert.Equal(ConversationRole.Assistant, b.Role);
        Assert.True(b.IsStreaming);
        Assert.Contains("剧情", b.ContentText);
    }

    [AvaloniaFact]
    public void References_CanBeSet()
    {
        var refs = new ObservableCollection<ReferenceTag>
        {
            new("第 12 章 · 盟约之城"),
            new("世界规则 · 灵气体系"),
        };
        var b = new ConversationBubble { References = refs };
        Assert.Equal(2, b.References!.Count);
    }
}
```

- [ ] **Step 6: 跑 + commit**

```bash
dotnet build Tianming.MacMigration.sln -c Debug --nologo -v q
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ -c Debug --nologo --filter "FullyQualifiedName~ConversationBubbleTests" -v q
git add src/Tianming.Desktop.Avalonia/Controls/ConversationRole.cs \
        src/Tianming.Desktop.Avalonia/Controls/ReferenceTag.cs \
        src/Tianming.Desktop.Avalonia/Controls/ConversationBubble.cs \
        src/Tianming.Desktop.Avalonia/Controls/ConversationBubble.axaml \
        src/Tianming.Desktop.Avalonia/Controls/SidebarTreeItemConverters.cs \
        src/Tianming.Desktop.Avalonia/App.axaml \
        tests/Tianming.Desktop.Avalonia.Tests/Controls/ConversationBubbleTests.cs
git commit -m "feat(controls): ConversationBubble primitive — 聊天气泡 user/assistant

6 个 StyledProperty (Role / ContentText / ThinkingBlock / References / IsStreaming / Timestamp)；
ControlTheme: subtle 灰底（assistant 左对）/ accent 浅底（user 右对）+ thinking 折叠区 + reference chips + streaming 指示 + 时间戳。
TimestampShortConverter 输出 HH:mm。3 测试。"
```

---

### Task 38: ToolCallCard primitive

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Controls/ToolCallState.cs`
- Create: `src/Tianming.Desktop.Avalonia/Controls/ToolCallCard.cs`
- Create: `src/Tianming.Desktop.Avalonia/Controls/ToolCallCard.axaml`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Controls/ToolCallCardTests.cs`
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml`

- [ ] **Step 1: enum + class**

Create `src/Tianming.Desktop.Avalonia/Controls/ToolCallState.cs`:

```csharp
namespace Tianming.Desktop.Avalonia.Controls;

public enum ToolCallState { Pending, Applied, Rejected }
```

Create `src/Tianming.Desktop.Avalonia/Controls/ToolCallCard.cs`:

```csharp
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace Tianming.Desktop.Avalonia.Controls;

public class ToolCallCard : TemplatedControl
{
    public static readonly StyledProperty<string> ToolNameProperty =
        AvaloniaProperty.Register<ToolCallCard, string>(nameof(ToolName), string.Empty);
    public static readonly StyledProperty<string?> ArgumentsPreviewProperty =
        AvaloniaProperty.Register<ToolCallCard, string?>(nameof(ArgumentsPreview));
    public static readonly StyledProperty<ToolCallState> StateProperty =
        AvaloniaProperty.Register<ToolCallCard, ToolCallState>(nameof(State), ToolCallState.Pending);
    public static readonly StyledProperty<ICommand?> ApproveCommandProperty =
        AvaloniaProperty.Register<ToolCallCard, ICommand?>(nameof(ApproveCommand));
    public static readonly StyledProperty<ICommand?> RejectCommandProperty =
        AvaloniaProperty.Register<ToolCallCard, ICommand?>(nameof(RejectCommand));

    public string ToolName { get => GetValue(ToolNameProperty); set => SetValue(ToolNameProperty, value); }
    public string? ArgumentsPreview { get => GetValue(ArgumentsPreviewProperty); set => SetValue(ArgumentsPreviewProperty, value); }
    public ToolCallState State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }
    public ICommand? ApproveCommand { get => GetValue(ApproveCommandProperty); set => SetValue(ApproveCommandProperty, value); }
    public ICommand? RejectCommand { get => GetValue(RejectCommandProperty); set => SetValue(RejectCommandProperty, value); }
}
```

- [ ] **Step 2: axaml**

Create `src/Tianming.Desktop.Avalonia/Controls/ToolCallCard.axaml`:

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:controls="using:Tianming.Desktop.Avalonia.Controls"
                    xmlns:shell="using:Tianming.Desktop.Avalonia.Shell">
  <ControlTheme x:Key="{x:Type controls:ToolCallCard}" TargetType="controls:ToolCallCard">
    <Setter Property="Template">
      <ControlTemplate>
        <Border Background="{DynamicResource SurfaceBaseBrush}"
                CornerRadius="{DynamicResource RadiusLg}"
                BorderBrush="{DynamicResource BorderSubtleBrush}"
                BorderThickness="1"
                Padding="12">
          <StackPanel Spacing="8">
            <!-- Header：tool name + state badge -->
            <Grid ColumnDefinitions="*,Auto">
              <TextBlock Grid.Column="0"
                         Text="{TemplateBinding ToolName}"
                         FontFamily="{DynamicResource FontMono}"
                         FontSize="{DynamicResource FontSizeSecondary}"
                         FontWeight="{DynamicResource FontWeightSemibold}"
                         Foreground="{DynamicResource TextPrimaryBrush}"/>
              <controls:BadgePill Grid.Column="1"
                                  Text="{TemplateBinding State, Converter={x:Static controls:ToolCallStateLabelConverter.Instance}}"
                                  Kind="{TemplateBinding State, Converter={x:Static controls:ToolCallStateKindConverter.Instance}}"/>
            </Grid>
            <!-- Arguments preview -->
            <TextBlock Text="{TemplateBinding ArgumentsPreview}"
                       FontFamily="{DynamicResource FontMono}"
                       FontSize="{DynamicResource FontSizeCaption}"
                       Foreground="{DynamicResource TextSecondaryBrush}"
                       TextWrapping="Wrap"
                       IsVisible="{TemplateBinding ArgumentsPreview, Converter={x:Static ObjectConverters.IsNotNull}}"/>
            <!-- Actions（仅 Pending 显示） -->
            <StackPanel x:Name="PART_Actions" Orientation="Horizontal" Spacing="8">
              <Button Classes="secondary"
                      Content="拒绝"
                      Command="{TemplateBinding RejectCommand}"/>
              <Button Content="批准并应用"
                      Command="{TemplateBinding ApproveCommand}"/>
            </StackPanel>
          </StackPanel>
        </Border>
      </ControlTemplate>
    </Setter>

    <Style Selector="^[State=Applied] /template/ StackPanel#PART_Actions">
      <Setter Property="IsVisible" Value="False"/>
    </Style>
    <Style Selector="^[State=Rejected] /template/ StackPanel#PART_Actions">
      <Setter Property="IsVisible" Value="False"/>
    </Style>
  </ControlTheme>
</ResourceDictionary>
```

- [ ] **Step 3: 2 个 Converter（追加到 SidebarTreeItemConverters.cs）**

```csharp
internal sealed class ToolCallStateLabelConverter : IValueConverter
{
    public static readonly ToolCallStateLabelConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            ToolCallState.Pending  => "待确认",
            ToolCallState.Applied  => "已应用",
            ToolCallState.Rejected => "已拒绝",
            _ => string.Empty
        };
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

internal sealed class ToolCallStateKindConverter : IValueConverter
{
    public static readonly ToolCallStateKindConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            ToolCallState.Pending  => StatusKind.Warning,
            ToolCallState.Applied  => StatusKind.Success,
            ToolCallState.Rejected => StatusKind.Danger,
            _ => StatusKind.Neutral
        };
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

- [ ] **Step 4: App.axaml + 测试**

App.axaml 加 `<ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Controls/ToolCallCard.axaml"/>`。

Create `tests/Tianming.Desktop.Avalonia.Tests/Controls/ToolCallCardTests.cs`:

```csharp
using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Controls;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class ToolCallCardTests
{
    [AvaloniaFact]
    public void Defaults_StatePending_ToolNameEmpty()
    {
        var c = new ToolCallCard();
        Assert.Equal(string.Empty, c.ToolName);
        Assert.Null(c.ArgumentsPreview);
        Assert.Equal(ToolCallState.Pending, c.State);
        Assert.Null(c.ApproveCommand);
        Assert.Null(c.RejectCommand);
    }

    [AvaloniaFact]
    public void SetToolNameAndArgs_Persists()
    {
        var c = new ToolCallCard
        {
            ToolName = "DataEditPlugin.UpdateCharacter",
            ArgumentsPreview = "{\"id\":\"C001\",\"name\":\"白起\"}"
        };
        Assert.Equal("DataEditPlugin.UpdateCharacter", c.ToolName);
        Assert.Contains("C001", c.ArgumentsPreview);
    }

    [AvaloniaFact]
    public void StateTransitions_AndCommands_Persist()
    {
        var approve = new RelayCommand(() => { });
        var reject = new RelayCommand(() => { });
        var c = new ToolCallCard { ApproveCommand = approve, RejectCommand = reject };
        c.State = ToolCallState.Applied;
        Assert.Equal(ToolCallState.Applied, c.State);
        Assert.Same(approve, c.ApproveCommand);
        Assert.Same(reject, c.RejectCommand);
    }
}
```

- [ ] **Step 5: 跑 + commit**

```bash
dotnet build Tianming.MacMigration.sln -c Debug --nologo -v q
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ -c Debug --nologo --filter "FullyQualifiedName~ToolCallCardTests" -v q
git add src/Tianming.Desktop.Avalonia/Controls/ToolCallState.cs \
        src/Tianming.Desktop.Avalonia/Controls/ToolCallCard.cs \
        src/Tianming.Desktop.Avalonia/Controls/ToolCallCard.axaml \
        src/Tianming.Desktop.Avalonia/Controls/SidebarTreeItemConverters.cs \
        src/Tianming.Desktop.Avalonia/App.axaml \
        tests/Tianming.Desktop.Avalonia.Tests/Controls/ToolCallCardTests.cs
git commit -m "feat(controls): ToolCallCard primitive — Agent 工具调用三态卡

5 个 StyledProperty (ToolName / ArgumentsPreview / State Pending|Applied|Rejected / Approve / Reject)；
ControlTheme: tool name + state BadgePill + arguments preview + Pending 时显批准/拒绝 actions。
2 个 Converter (ToolCallStateLabel / ToolCallStateKind)。3 测试。"
```

---

### Task 39: CodeViewer primitive（AvaloniaEdit 封装）

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Controls/CodeLanguage.cs`
- Create: `src/Tianming.Desktop.Avalonia/Controls/CodeViewer.cs`
- Create: `src/Tianming.Desktop.Avalonia/Controls/CodeViewer.axaml`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Controls/CodeViewerTests.cs`
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml`

- [ ] **Step 1: enum**

Create `src/Tianming.Desktop.Avalonia/Controls/CodeLanguage.cs`:

```csharp
namespace Tianming.Desktop.Avalonia.Controls;

public enum CodeLanguage { Plain, Json, Markdown }
```

- [ ] **Step 2: class — UserControl 用 AvaloniaEdit.TextEditor**

CodeViewer 不用 TemplatedControl（AvaloniaEdit.TextEditor 不适合 ControlTemplate 套），改用 UserControl 直接持有 TextEditor 实例。

Create `src/Tianming.Desktop.Avalonia/Controls/CodeViewer.cs`:

```csharp
using Avalonia;
using Avalonia.Controls;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;

namespace Tianming.Desktop.Avalonia.Controls;

public class CodeViewer : UserControl
{
    private readonly TextEditor _editor;

    public static readonly StyledProperty<string> CodeProperty =
        AvaloniaProperty.Register<CodeViewer, string>(nameof(Code), string.Empty);
    public static readonly StyledProperty<CodeLanguage> LanguageProperty =
        AvaloniaProperty.Register<CodeViewer, CodeLanguage>(nameof(Language), CodeLanguage.Plain);
    public static readonly StyledProperty<bool> ShowLineNumbersProperty =
        AvaloniaProperty.Register<CodeViewer, bool>(nameof(ShowLineNumbers), true);
    public static readonly StyledProperty<bool> WordWrapProperty =
        AvaloniaProperty.Register<CodeViewer, bool>(nameof(WordWrap), false);

    public string Code { get => GetValue(CodeProperty); set => SetValue(CodeProperty, value); }
    public CodeLanguage Language { get => GetValue(LanguageProperty); set => SetValue(LanguageProperty, value); }
    public bool ShowLineNumbers { get => GetValue(ShowLineNumbersProperty); set => SetValue(ShowLineNumbersProperty, value); }
    public bool WordWrap { get => GetValue(WordWrapProperty); set => SetValue(WordWrapProperty, value); }

    public CodeViewer()
    {
        _editor = new TextEditor
        {
            IsReadOnly = true,
            ShowLineNumbers = true,
            WordWrap = false,
            FontFamily = "SF Mono, Menlo, Consolas, monospace",
        };
        Content = _editor;
        Apply();

        CodeProperty.Changed.AddClassHandler<CodeViewer>((c, _) => c.Apply());
        LanguageProperty.Changed.AddClassHandler<CodeViewer>((c, _) => c.Apply());
        ShowLineNumbersProperty.Changed.AddClassHandler<CodeViewer>((c, _) => c._editor.ShowLineNumbers = c.ShowLineNumbers);
        WordWrapProperty.Changed.AddClassHandler<CodeViewer>((c, _) => c._editor.WordWrap = c.WordWrap);
    }

    private void Apply()
    {
        _editor.Text = Code ?? string.Empty;
        _editor.SyntaxHighlighting = Language switch
        {
            CodeLanguage.Json     => HighlightingManager.Instance.GetDefinition("Json"),
            CodeLanguage.Markdown => HighlightingManager.Instance.GetDefinition("MarkDown"),
            _                     => null
        };
    }
}
```

- [ ] **Step 3: axaml — 几乎不用 template（UserControl 已经直接持有 TextEditor）**

Create `src/Tianming.Desktop.Avalonia/Controls/CodeViewer.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="Tianming.Desktop.Avalonia.Controls.CodeViewer">
  <!-- 内容由 .cs 设置（_editor）；这里仅作为 axaml 一对一文件存在用于 XAML compile -->
</UserControl>
```

> 由于 UserControl 直接 `Content = _editor`，axaml 不需要写 root content。InitializeComponent 不调用也可（这里全靠 code-behind）。注：CodeViewer.axaml.cs 不需要，class 文件已经 partial 化定义；如果 axaml 编译器要求 .axaml.cs，临时补一个空 partial。

> 若 Avalonia 编译器抱怨缺 .axaml.cs，把 `CodeViewer.cs` 改名为 `CodeViewer.axaml.cs` 并把开头 `public class CodeViewer : UserControl` 改为 `public partial class CodeViewer : UserControl`。

- [ ] **Step 4: App.axaml + 测试**

App.axaml 不需要加 ResourceInclude（CodeViewer 不依赖 ControlTheme）。

Create `tests/Tianming.Desktop.Avalonia.Tests/Controls/CodeViewerTests.cs`:

```csharp
using Avalonia.Headless.XUnit;
using Tianming.Desktop.Avalonia.Controls;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class CodeViewerTests
{
    [AvaloniaFact]
    public void Defaults_CodeEmpty_LanguagePlain_ShowLineNumbersTrue()
    {
        var v = new CodeViewer();
        Assert.Equal(string.Empty, v.Code);
        Assert.Equal(CodeLanguage.Plain, v.Language);
        Assert.True(v.ShowLineNumbers);
        Assert.False(v.WordWrap);
    }

    [AvaloniaFact]
    public void SetCodeAndLanguage_Persists()
    {
        var v = new CodeViewer { Code = "{ \"x\": 1 }", Language = CodeLanguage.Json };
        Assert.Contains("\"x\"", v.Code);
        Assert.Equal(CodeLanguage.Json, v.Language);
    }

    [AvaloniaFact]
    public void ToggleWordWrap_Persists()
    {
        var v = new CodeViewer { WordWrap = true };
        Assert.True(v.WordWrap);
    }
}
```

- [ ] **Step 5: 跑 + commit**

```bash
dotnet build Tianming.MacMigration.sln -c Debug --nologo -v q
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ -c Debug --nologo --filter "FullyQualifiedName~CodeViewerTests" -v q
git add src/Tianming.Desktop.Avalonia/Controls/CodeLanguage.cs \
        src/Tianming.Desktop.Avalonia/Controls/CodeViewer.cs \
        src/Tianming.Desktop.Avalonia/Controls/CodeViewer.axaml \
        tests/Tianming.Desktop.Avalonia.Tests/Controls/CodeViewerTests.cs
git commit -m "feat(controls): CodeViewer primitive — AvaloniaEdit 只读高亮

4 个 StyledProperty (Code / Language Plain|Json|Markdown / ShowLineNumbers / WordWrap)；
UserControl 持有 TextEditor 实例（不走 ControlTemplate）；
属性 changed 时 Apply() 重置 SyntaxHighlighting / Text。3 测试。"
```

---

## Phase 10 / Done Definition 验收 + 最终交付

### Task 40: 总冒烟 + Done Definition 全量验收 + 推 specs 同步 + push main

**Files:** 无新增；纯验证 + git operations

- [ ] **Step 1: 全量 build + test**

```bash
dotnet build Tianming.MacMigration.sln -c Debug --nologo -v q
dotnet test Tianming.MacMigration.sln -c Debug --nologo --no-build -v q 2>&1 | tail -20
```

Expected：build 0 error 0 warning；全部测试通过（1156 基线 + 设计 token 13 + breadcrumb 3 + runtime 2 + keychain 2 + onnx 3 + chrome vm 3 + statusbar vm 3 + AppHost 5 + 14 primitives × 3 = 42，合计 ~1232 测试）。

- [ ] **Step 2: 手动冒烟**

```bash
dotnet run --project src/Tianming.Desktop.Avalonia -c Debug 2>&1 | head -40
```

人工核对：

- [ ] 窗口 36px 顶部 chrome（traffic light 在左、breadcrumb "天命 · 欢迎" 居中、emoji 占位按钮在右）
- [ ] 中间 ThreeColumnLayoutView 显示（旧 LeftNavView + WelcomeView + RightConversationView 装在新 chrome 里）
- [ ] 底部 28px 状态栏（".NET 8.x.y" + 圆点指示器 4 个：本地写作模式绿 / Keychain probe 绿 / ONNX probe 蓝 info）
- [ ] macOS 顶部系统菜单栏出现 6 个一级菜单（天命 / 文件 / 编辑 / 视图 / 窗口 / 帮助），下拉看到带 Cmd+N 等快捷键的菜单项
- [ ] Cmd+Q 能退出
- [ ] 整体配色青色 accent（#06B6D4 系），不再蓝色

如有任何一项不通过，回上一相关 Task 修复，**不要走到 Step 3**。

- [ ] **Step 3: 同步 Mac_UI 到 main（如果 Phase 1 没把 Mac_UI 留在 main 的同步分支）**

```bash
git checkout main
git checkout m2-m6/specs-2026-05-12 -- Mac_UI/
git diff --stat
```

如果有差异（specs 分支这期间又改过 Mac_UI），commit；如果没差异，跳过。

```bash
git checkout mac-ui/foundation-2026-05-13
```

- [ ] **Step 4: feature 分支 push + 准备 PR / merge**

```bash
git push origin mac-ui/foundation-2026-05-13
```

- [ ] **Step 5: 合回 main（自用项目不走 PR review，直接 merge --no-ff）**

```bash
git checkout main
git merge --no-ff mac-ui/foundation-2026-05-13 -m "Merge Mac UI 视觉基建（Sub-plan 1）

包括：
- Mac_UI 入仓 + 同步约定
- 3 个 NuGet 依赖（LiveCharts2 / AvaloniaEdit / Lucide）+ Avalonia.Headless
- 5 个 design token axaml（Colors/Typography/Spacing/Radii/Shadows）+ ControlStyles 6 个覆盖
- 自定义 chrome（ExtendClientArea + 36px breadcrumb + 78px traffic light 留位）
- 底部 28px 状态栏（.NET / 本地 / Keychain / ONNX 探测 + 项目路径）
- App.axaml NativeMenu stub（6 个一级菜单，命令绑定留 M5）
- 14 个 primitives（BadgePill / StatusBarItem / SectionCard / StatsCard / SearchBox /
  SegmentedTabs / BreadcrumbBar / NavRail / SidebarTreeItem / ProjectCard /
  DataGridRowCell / ConversationBubble / ToolCallCard / CodeViewer）
- 测试：1156 基线 + ~76 新增 = ~1232 全过

Done Definition 10/10 通过；Sub-plan 2（M3 视觉重做）/ Sub-plan 3（docs+plans 对齐）待续。"
```

- [ ] **Step 6: push main**

```bash
git push origin main
```

- [ ] **Step 7: 删 feature 分支（local + remote）**

```bash
git branch -d mac-ui/foundation-2026-05-13
git push origin --delete mac-ui/foundation-2026-05-13
```

- [ ] **Step 8: Done Definition 10 项最终勾选**

人工对照 spec §9.2 逐项确认：

- [ ] 1. Mac_UI 在 specs + main 都可见；sampled-tokens.json 已生成
- [ ] 2. 3 个 NuGet + Avalonia.Headless 已加；build 0/0
- [ ] 3. 5 个 token 文件 + ControlStyles/* 就位；PalettesDark 已删；亮色锁定
- [ ] 4. MainWindow ExtendClientArea 启动，breadcrumb + traffic light 共存
- [ ] 5. 底部 28px 状态栏 4 个 indicator
- [ ] 6. macOS 顶部菜单栏 6 个一级菜单 + 快捷键
- [ ] 7. 14 个 primitives 全有 .axaml ControlTheme + .cs + ≥3 测试（CodeViewer 是 UserControl 不是 TemplatedControl，例外但满足"3 测试"）
- [ ] 8. 全部 ~1232 测试通过
- [ ] 9. dotnet run 启动成功，新 chrome / status bar + 旧 WelcomeView 共存无异常
- [ ] 10. 所有 commit message 清晰；feature 分支已 merge --no-ff 到 main

全部 ✓ → Sub-plan 1 完成，可以开 Sub-plan 2（M3 视觉重做）的 brainstorm。

---

## Self-Review

### Spec coverage

逐 spec section 对照 plan task 编号：

- spec §0（上下文）→ plan 标题 + Tech Stack + branch 策略
- spec §1（架构/文件布局）→ Task 4-39 各创建文件覆盖了 spec §1 列出的所有路径
- spec §2.1 Colors → Task 5
- spec §2.2 Typography → Task 6
- spec §2.3 Spacing/Radii/Shadows → Task 7-9
- spec §2.4 取色校准 → Task 4 (sample-tokens.py)
- spec §3.1 chrome → Task 21 + 23
- spec §3.2 status bar → Task 22
- spec §3.3 NativeMenu stub → Task 24
- spec §4（14 primitives）→ Task 26-39
- spec §5.1 NuGet → Task 3
- spec §5.2 LiveCharts2 全局样式 → Task 12
- spec §5.3 AvaloniaEdit 整合 → Task 13 + Task 39
- spec §5.4 Lucide 整合 → Task 3 加包 + 各 primitive IconGlyph string 接口
- spec §6.1 DI 装配 → Task 25
- spec §6.2 Breadcrumb 数据来源 → Task 14
- spec §6.3 StatusBar 数据来源 → Task 15-17
- spec §6.4 跨进程 API 兼容性确认 → 已通过 Explore subagent 完成（embed 在 plan 中 KeychainHealthProbe / OnnxHealthProbe 实现）
- spec §6.5 启动顺序 → Task 12 (LiveCharts) + Task 13 (AvaloniaEdit) + Task 25 (RefreshProbes)
- spec §7.1-7.3 测试 → Task 5-9 / 14-19 / 26-39 各自带测试
- spec §7.4 像素级保真验证 → Task 40 Step 2 人工冒烟
- spec §7.5 现有测试不退化 → 每个 Task 都跑全量测试
- spec §8 Mac_UI 入仓 + 同步 → Task 1-2 + Task 40 Step 3
- spec §9.1 Out of Scope → 未在任何 task 中实现（确认）
- spec §9.2 Done Definition → Task 40

✓ 覆盖完整。

### Placeholder scan

- ✓ 无 "TBD" / "TODO" / "implement later"
- ✓ 无 "appropriate error handling" 等模糊指令
- ✓ 无 "write tests for the above" 没给代码的步骤
- ✓ 无 "Similar to Task N" 引用
- ✓ 唯一可变值：Task 3 LiveCharts2 版本号占位 `<step-1 调研得到的版本>` —— 但已经明确给出调研命令 + 决策路径，可执行
- ✓ 唯一推迟的小项：Task 31 SegmentedTabs 选中态视觉 / Task 33 NavRail ActiveKey 高亮，明确写明"Sub-plan 2 真消费时再加 Classes.Active"，是显式跳过而非含糊

### Type consistency

- ✓ `BreadcrumbSegment` 在 `Tianming.Desktop.Avalonia.Shell` 命名空间，Task 14 / 18 / 21 / 32 都用同样路径
- ✓ `StatusKind` enum 5 个值 (Success/Warning/Danger/Info/Neutral)，spec + plan 一致
- ✓ `StatusIndicator(Label, Kind, Tooltip)` 三参数 record，Shell namespace
- ✓ `PageKey` 是 `readonly record struct`（依据 Explore 报告），breadcrumb / NavRail 都用此类型
- ✓ `IBreadcrumbSource.SegmentsChanged` 用 `event EventHandler<IReadOnlyList<BreadcrumbSegment>>?`，AppChromeViewModel 订阅签名一致
- ✓ 14 个 primitives 全部在 `Tianming.Desktop.Avalonia.Controls` namespace
- ✓ App.axaml 每次注册 ControlTheme 的 ResourceInclude 路径都用 `avares://Tianming.Desktop.Avalonia/Controls/<Name>.axaml`
- ✓ ControlTheme 的 `x:Key` 全部用 `{x:Type controls:<Name>}`

---

## Execution Handoff

Plan 完整，所有 40 个 task 都覆盖 spec 中提到的设计点；TDD 步骤 + 完整代码 + 真实 git 命令；通过 Explore subagent 已核对真实仓 API 签名（M3 落地 / Tianming.AI / 测试基线）。

**推荐执行方式：superpowers:subagent-driven-development**

理由：
- 40 个 task 大部分相互独立（token / primitives / shell 各 task 可并行）
- 每个 task 自带 TDD + commit；subagent 单 task 闭环良好
- 主线程做 review checkpoint，避免上下文污染

按 superpowers:subagent-driven-development 规范，每 task 派一个 fresh subagent 完成"读 task → 写代码 → 跑测 → commit"，主线程在 task 间做 light review（看 commit / 跑 build 验证）。
