# M4.3 章节编辑器 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 Avalonia 桌面 shell 中实装"草稿"页（章节编辑器）：AvaloniaEdit Markdown 可写编辑器 + 多 tab 章节切换 + 章节树侧栏 + 字数 / dirty / 自动保存 footer + 防抖 2s 草稿持久化到 `~/Library/Application Support/Tianming/Drafts/<project>/<chapter>.md`。

**Architecture:**
- 3 个 control（MarkdownEditor / MarkdownPreview / ChapterTabBar）→ 容纳在 1 个 View（EditorWorkspaceView）→ 由 1 个根 VM（EditorWorkspaceViewModel）协调多个子 VM（ChapterEditorViewModel，每个 tab 一个）。
- AvaloniaEdit `TextEditor` 用做可写编辑器，`SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("MarkDown")`。控件用 `TextProperty` StyledProperty 加 `TwoWay` binding；TextChanged 事件 + Dispatcher 防抖触发 AutoSave。
- 持久化：`IChapterDraftStore` portable 接口 + `FileChapterDraftStore` 文件实现（写 `<AppSupport>/Tianming/Drafts/<project>/<chapter>.md`）。AutoSaveScheduler 使用 `ITimerScheduler` 抽象，便于测试。
- Markdown 预览本任务**不引入 Markdig**：MarkdownPreview 用 `SelectableTextBlock` 显示原文（monospace + wrap）+ TODO 注释，等 M4.3 Gate 真正用得上预览时再升级。
- LeftNav "写作" 组**新增 "草稿" 项** 指向 PageKey `Editor`，启用 IsEnabled=true。

**Tech Stack:**
- Avalonia 11.0.10 + AvaloniaEdit 11.0.6（已在 csproj）
- CommunityToolkit.Mvvm 8.2.2
- Microsoft.Extensions.DependencyInjection 8.0.1
- xUnit 2.9.2 + Avalonia.Headless.XUnit 11.0.10

---

## File Structure

**New files (src/Tianming.Desktop.Avalonia/):**
- `Controls/MarkdownEditor.axaml` + `.axaml.cs` — AvaloniaEdit TextEditor 可写包装，StyledProperty `Text` / `IsReadOnly` / `WordWrap`，two-way binding 友好（Text 默认 BindingMode.TwoWay）
- `Controls/MarkdownPreview.axaml` + `.axaml.cs` — 占位预览（SelectableTextBlock + TODO Markdig）
- `Controls/ChapterTabBar.axaml` + `.axaml.cs` — `ItemsControl` over `ChapterTabItem` records；每 tab 显示标题 + dirty 点 + 关闭按钮 `×`；ActiveTab + Click/Close 命令；emoji 图标
- `Controls/ChapterTabItem.cs` — `record ChapterTabItem(string ChapterId, string Title, bool IsDirty, bool IsActive)`
- `Infrastructure/ITimerScheduler.cs` + `DispatcherTimerScheduler.cs` — 防抖定时器抽象（DispatcherTimer 实现 + Fake 用于测试）
- `Infrastructure/AutoSaveScheduler.cs` — 防抖 2s schedule（依赖 `ITimerScheduler`）
- `Infrastructure/IChapterDraftStore.cs` + `FileChapterDraftStore.cs` — 草稿读写（portable interface + file impl）
- `Infrastructure/WordCounter.cs` — CJK + 英文单词字数计算（静态工具）
- `ViewModels/Editor/ChapterEditorViewModel.cs` — 单 tab 的 VM：`ChapterId` / `Title` / `Content` / `IsDirty` / `WordCount` / SetContent 命令；构造时收 `IChapterDraftStore` + `AutoSaveScheduler`
- `ViewModels/Editor/EditorWorkspaceViewModel.cs` — 容纳多 ChapterEditorViewModel，ActiveTab，AddTab / CloseTab / ActivateTab 命令；mock 初始化 1 个示例章节
- `Views/Editor/EditorWorkspaceView.axaml` + `.axaml.cs` — image 05 5 区布局：顶 ChapterTabBar + 左 sidebar（章节树占位）+ 主 MarkdownEditor + 右 metadata 占位 + 底 footer（字数 / dirty / 保存时间）

**New files (tests/Tianming.Desktop.Avalonia.Tests/):**
- `Controls/MarkdownEditorTests.cs`
- `Controls/MarkdownPreviewTests.cs`
- `Controls/ChapterTabBarTests.cs`
- `Infrastructure/WordCounterTests.cs`
- `Infrastructure/AutoSaveSchedulerTests.cs`
- `Infrastructure/FileChapterDraftStoreTests.cs`
- `ViewModels/Editor/ChapterEditorViewModelTests.cs`
- `ViewModels/Editor/EditorWorkspaceViewModelTests.cs`

**Modified files:**
- `src/Tianming.Desktop.Avalonia/App.axaml` — 加 1 DataTemplate (EditorWorkspaceViewModel → EditorWorkspaceView) + 加 MarkdownEditor / MarkdownPreview / ChapterTabBar ResourceInclude
- `src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs` — 注册 `ITimerScheduler`/`DispatcherTimerScheduler`、`IChapterDraftStore`/`FileChapterDraftStore`、`AutoSaveScheduler`、`EditorWorkspaceViewModel`，注册 PageKey
- `src/Tianming.Desktop.Avalonia/Navigation/PageKeys.cs` — 加 `public static readonly PageKey Editor = new("editor");`
- `src/Tianming.Desktop.Avalonia/ViewModels/Shell/LeftNavViewModel.cs` — 写作组追加 "草稿" → PageKeys.Editor，IsEnabled=true

---

### Task 1: PageKey Editor 注册

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/Navigation/PageKeys.cs`

- [ ] **Step 1: Add Editor PageKey field**

Open `src/Tianming.Desktop.Avalonia/Navigation/PageKeys.cs`. Replace the entire `PageKeys` static class with:

```csharp
public static class PageKeys
{
    public static readonly PageKey Welcome   = new("welcome");
    public static readonly PageKey Dashboard = new("dashboard");
    public static readonly PageKey Settings  = new("settings");

    // M4.3 章节编辑器
    public static readonly PageKey Editor    = new("editor");

    // M4.1 设计模块（6 页）
    public static readonly PageKey DesignWorld     = new("design.world");
    public static readonly PageKey DesignCharacter = new("design.character");
    public static readonly PageKey DesignFaction   = new("design.faction");
    public static readonly PageKey DesignLocation  = new("design.location");
    public static readonly PageKey DesignPlot      = new("design.plot");
    public static readonly PageKey DesignMaterials = new("design.materials");
}
```

- [ ] **Step 2: Build to verify**

Run: `cd /Users/jimmy/Downloads/tianming-novel-ai-writer/.worktrees/m4-3 && dotnet build src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj`
Expected: build succeeded 0 warning 0 error

- [ ] **Step 3: Commit**

```bash
git add src/Tianming.Desktop.Avalonia/Navigation/PageKeys.cs
git commit -m "feat(nav): M4.3 加 PageKeys.Editor"
```

---

### Task 2: WordCounter 工具（CJK + 英文）

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Infrastructure/WordCounter.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/WordCounterTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/WordCounterTests.cs`:

```csharp
using Tianming.Desktop.Avalonia.Infrastructure;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Infrastructure;

public class WordCounterTests
{
    [Fact]
    public void Empty_string_returns_zero()
    {
        Assert.Equal(0, WordCounter.Count(""));
        Assert.Equal(0, WordCounter.Count(null));
    }

    [Fact]
    public void Chinese_chars_count_each_as_one()
    {
        Assert.Equal(4, WordCounter.Count("天命之书"));
    }

    [Fact]
    public void English_words_count_as_one_each()
    {
        Assert.Equal(3, WordCounter.Count("hello world test"));
    }

    [Fact]
    public void Mixed_chinese_english_count_combined()
    {
        Assert.Equal(5, WordCounter.Count("hello 世界 test")); // 1 + 2 + 1 + 1 = 5
    }

    [Fact]
    public void Whitespace_and_punctuation_not_counted()
    {
        Assert.Equal(2, WordCounter.Count("天，命。"));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter "FullyQualifiedName~WordCounter" --no-restore`
Expected: build fail "WordCounter does not exist"

- [ ] **Step 3: Implement WordCounter**

Create `src/Tianming.Desktop.Avalonia/Infrastructure/WordCounter.cs`:

```csharp
using System.Text.RegularExpressions;

namespace Tianming.Desktop.Avalonia.Infrastructure;

/// <summary>
/// CJK 字符 1 个算 1 字；英文按 [A-Za-z]+(?:['-][A-Za-z]+)? 单词正则算 1 词。
/// 标点 / 空格不计。算法与 ChapterContentStore.AccumulateWordCounts 对齐。
/// </summary>
public static class WordCounter
{
    private static readonly Regex EnglishWord = new(@"[A-Za-z]+(?:['-][A-Za-z]+)?", RegexOptions.Compiled);

    public static int Count(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var cjk = 0;
        foreach (var ch in text)
        {
            if (ch >= '\u4e00' && ch <= '\u9fff') cjk++;
        }
        var en = EnglishWord.Matches(text).Count;
        return cjk + en;
    }
}
```

- [ ] **Step 4: Run test to verify pass**

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter "FullyQualifiedName~WordCounter" --no-restore`
Expected: 5 passed

- [ ] **Step 5: Commit**

```bash
git add src/Tianming.Desktop.Avalonia/Infrastructure/WordCounter.cs tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/WordCounterTests.cs
git commit -m "feat(editor): WordCounter (CJK + 英文单词正则)"
```

---

### Task 3: ITimerScheduler + DispatcherTimerScheduler + FakeTimerScheduler

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Infrastructure/ITimerScheduler.cs`
- Create: `src/Tianming.Desktop.Avalonia/Infrastructure/DispatcherTimerScheduler.cs`

- [ ] **Step 1: Create interface + production impl**

Create `src/Tianming.Desktop.Avalonia/Infrastructure/ITimerScheduler.cs`:

```csharp
using System;

namespace Tianming.Desktop.Avalonia.Infrastructure;

/// <summary>
/// 防抖 / 节流定时器抽象。
/// production 用 DispatcherTimerScheduler（Avalonia UI 线程）；测试用 Fake 控制时间推进。
/// </summary>
public interface ITimerScheduler
{
    /// <summary>
    /// 启动一个 one-shot 定时器。若已存在同 key 的定时器则重置（debounce 语义）。
    /// </summary>
    IDisposable Debounce(string key, TimeSpan delay, Action callback);
}
```

Create `src/Tianming.Desktop.Avalonia/Infrastructure/DispatcherTimerScheduler.cs`:

```csharp
using System;
using System.Collections.Generic;
using Avalonia.Threading;

namespace Tianming.Desktop.Avalonia.Infrastructure;

public sealed class DispatcherTimerScheduler : ITimerScheduler
{
    private readonly Dictionary<string, DispatcherTimer> _timers = new();

    public IDisposable Debounce(string key, TimeSpan delay, Action callback)
    {
        if (_timers.TryGetValue(key, out var existing))
        {
            existing.Stop();
            _timers.Remove(key);
        }
        var timer = new DispatcherTimer { Interval = delay };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _timers.Remove(key);
            try { callback(); } catch { /* swallow to keep dispatcher healthy */ }
        };
        _timers[key] = timer;
        timer.Start();
        return new TimerHandle(this, key);
    }

    private sealed class TimerHandle : IDisposable
    {
        private readonly DispatcherTimerScheduler _owner;
        private readonly string _key;
        public TimerHandle(DispatcherTimerScheduler owner, string key) { _owner = owner; _key = key; }
        public void Dispose()
        {
            if (_owner._timers.TryGetValue(_key, out var t))
            {
                t.Stop();
                _owner._timers.Remove(_key);
            }
        }
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj`
Expected: 0 error

- [ ] **Step 3: Commit**

```bash
git add src/Tianming.Desktop.Avalonia/Infrastructure/ITimerScheduler.cs src/Tianming.Desktop.Avalonia/Infrastructure/DispatcherTimerScheduler.cs
git commit -m "feat(editor): ITimerScheduler + DispatcherTimerScheduler (防抖抽象)"
```

---

### Task 4: AutoSaveScheduler + Fake-based test

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Infrastructure/AutoSaveScheduler.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/AutoSaveSchedulerTests.cs`

- [ ] **Step 1: Write failing test (with FakeTimerScheduler defined inline)**

Create `tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/AutoSaveSchedulerTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using Tianming.Desktop.Avalonia.Infrastructure;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Infrastructure;

public class AutoSaveSchedulerTests
{
    [Fact]
    public void Schedule_does_not_fire_immediately()
    {
        var fake = new FakeTimerScheduler();
        var calls = 0;
        var s = new AutoSaveScheduler(fake, TimeSpan.FromSeconds(2));

        s.Schedule("chap-1", () => calls++);

        Assert.Equal(0, calls);
    }

    [Fact]
    public void Schedule_then_advance_fires_callback()
    {
        var fake = new FakeTimerScheduler();
        var calls = 0;
        var s = new AutoSaveScheduler(fake, TimeSpan.FromSeconds(2));

        s.Schedule("chap-1", () => calls++);
        fake.AdvanceAll();

        Assert.Equal(1, calls);
    }

    [Fact]
    public void Schedule_twice_same_key_only_fires_latest()
    {
        var fake = new FakeTimerScheduler();
        var first = 0; var second = 0;
        var s = new AutoSaveScheduler(fake, TimeSpan.FromSeconds(2));

        s.Schedule("chap-1", () => first++);
        s.Schedule("chap-1", () => second++);
        fake.AdvanceAll();

        Assert.Equal(0, first);
        Assert.Equal(1, second);
    }

    [Fact]
    public void Different_keys_independent()
    {
        var fake = new FakeTimerScheduler();
        var a = 0; var b = 0;
        var s = new AutoSaveScheduler(fake, TimeSpan.FromSeconds(2));

        s.Schedule("chap-1", () => a++);
        s.Schedule("chap-2", () => b++);
        fake.AdvanceAll();

        Assert.Equal(1, a);
        Assert.Equal(1, b);
    }
}

internal sealed class FakeTimerScheduler : ITimerScheduler
{
    private readonly Dictionary<string, Action> _pending = new();
    public IDisposable Debounce(string key, TimeSpan delay, Action callback)
    {
        _pending[key] = callback;
        return new Handle(this, key);
    }
    public void AdvanceAll()
    {
        var snapshot = new List<Action>(_pending.Values);
        _pending.Clear();
        foreach (var a in snapshot) a();
    }
    private sealed class Handle : IDisposable
    {
        private readonly FakeTimerScheduler _o; private readonly string _k;
        public Handle(FakeTimerScheduler o, string k) { _o = o; _k = k; }
        public void Dispose() => _o._pending.Remove(_k);
    }
}
```

- [ ] **Step 2: Run test to verify fail**

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter "FullyQualifiedName~AutoSaveScheduler" --no-restore`
Expected: build fail "AutoSaveScheduler does not exist"

- [ ] **Step 3: Implement AutoSaveScheduler**

Create `src/Tianming.Desktop.Avalonia/Infrastructure/AutoSaveScheduler.cs`:

```csharp
using System;

namespace Tianming.Desktop.Avalonia.Infrastructure;

/// <summary>
/// 章节编辑器自动保存调度：每个 chapterId 一个独立防抖 key，
/// Schedule(chapterId, save) 推迟到 delay 后触发，期间再次调用则重置。
/// </summary>
public sealed class AutoSaveScheduler
{
    private readonly ITimerScheduler _scheduler;
    private readonly TimeSpan _delay;

    public AutoSaveScheduler(ITimerScheduler scheduler, TimeSpan delay)
    {
        _scheduler = scheduler;
        _delay = delay;
    }

    public void Schedule(string chapterId, Action saveCallback)
    {
        if (string.IsNullOrEmpty(chapterId)) return;
        _scheduler.Debounce($"chapter:{chapterId}", _delay, saveCallback);
    }
}
```

- [ ] **Step 4: Run test to verify pass**

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter "FullyQualifiedName~AutoSaveScheduler" --no-restore`
Expected: 4 passed

- [ ] **Step 5: Commit**

```bash
git add src/Tianming.Desktop.Avalonia/Infrastructure/AutoSaveScheduler.cs tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/AutoSaveSchedulerTests.cs
git commit -m "feat(editor): AutoSaveScheduler 防抖 + FakeTimerScheduler 单测"
```

---

### Task 5: IChapterDraftStore + FileChapterDraftStore

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Infrastructure/IChapterDraftStore.cs`
- Create: `src/Tianming.Desktop.Avalonia/Infrastructure/FileChapterDraftStore.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/FileChapterDraftStoreTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/FileChapterDraftStoreTests.cs`:

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Tianming.Desktop.Avalonia.Infrastructure;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Infrastructure;

public class FileChapterDraftStoreTests
{
    [Fact]
    public async Task SaveDraftAsync_writes_file_under_project()
    {
        var (store, root) = NewStore();
        await store.SaveDraftAsync("proj-a", "chap-1", "# hello\n世界");

        var file = Path.Combine(root, "proj-a", "chap-1.md");
        Assert.True(File.Exists(file));
        Assert.Equal("# hello\n世界", await File.ReadAllTextAsync(file));
    }

    [Fact]
    public async Task LoadDraftAsync_returns_null_when_missing()
    {
        var (store, _) = NewStore();
        Assert.Null(await store.LoadDraftAsync("nope", "missing"));
    }

    [Fact]
    public async Task LoadDraftAsync_returns_content_after_save()
    {
        var (store, _) = NewStore();
        await store.SaveDraftAsync("proj-b", "ch-2", "draft body");
        var result = await store.LoadDraftAsync("proj-b", "ch-2");
        Assert.Equal("draft body", result);
    }

    [Fact]
    public async Task SaveDraftAsync_overwrites_existing()
    {
        var (store, _) = NewStore();
        await store.SaveDraftAsync("proj-c", "ch-3", "v1");
        await store.SaveDraftAsync("proj-c", "ch-3", "v2");
        Assert.Equal("v2", await store.LoadDraftAsync("proj-c", "ch-3"));
    }

    [Fact]
    public async Task Empty_chapterId_throws()
    {
        var (store, _) = NewStore();
        await Assert.ThrowsAsync<ArgumentException>(() => store.SaveDraftAsync("p", "", "x"));
    }

    private static (FileChapterDraftStore store, string root) NewStore()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-drafts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return (new FileChapterDraftStore(root), root);
    }
}
```

- [ ] **Step 2: Run test to verify fail**

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter "FullyQualifiedName~FileChapterDraftStore" --no-restore`
Expected: build fail

- [ ] **Step 3: Implement IChapterDraftStore**

Create `src/Tianming.Desktop.Avalonia/Infrastructure/IChapterDraftStore.cs`:

```csharp
using System.Threading.Tasks;

namespace Tianming.Desktop.Avalonia.Infrastructure;

/// <summary>
/// 章节草稿存储。M4.3 用文件实现；M6 之后可换 SQLite / WAL 实现而不影响 VM。
/// </summary>
public interface IChapterDraftStore
{
    Task SaveDraftAsync(string projectId, string chapterId, string content);
    Task<string?> LoadDraftAsync(string projectId, string chapterId);
}
```

Create `src/Tianming.Desktop.Avalonia/Infrastructure/FileChapterDraftStore.cs`:

```csharp
using System;
using System.IO;
using System.Threading.Tasks;

namespace Tianming.Desktop.Avalonia.Infrastructure;

/// <summary>
/// 文件草稿存储：root/{projectId}/{chapterId}.md。
/// 直接写文件（atomic via temp + rename 留给 M6.3 WAL，本任务先简单覆盖写）。
/// </summary>
public sealed class FileChapterDraftStore : IChapterDraftStore
{
    private readonly string _root;

    public FileChapterDraftStore(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("root", nameof(root));
        _root = root;
    }

    public async Task SaveDraftAsync(string projectId, string chapterId, string content)
    {
        if (string.IsNullOrWhiteSpace(projectId)) throw new ArgumentException("projectId", nameof(projectId));
        if (string.IsNullOrWhiteSpace(chapterId)) throw new ArgumentException("chapterId", nameof(chapterId));

        var dir = Path.Combine(_root, projectId);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{chapterId}.md");
        await File.WriteAllTextAsync(path, content ?? string.Empty).ConfigureAwait(false);
    }

    public async Task<string?> LoadDraftAsync(string projectId, string chapterId)
    {
        if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(chapterId)) return null;
        var path = Path.Combine(_root, projectId, $"{chapterId}.md");
        if (!File.Exists(path)) return null;
        return await File.ReadAllTextAsync(path).ConfigureAwait(false);
    }
}
```

- [ ] **Step 4: Run test to verify pass**

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter "FullyQualifiedName~FileChapterDraftStore" --no-restore`
Expected: 5 passed

- [ ] **Step 5: Commit**

```bash
git add src/Tianming.Desktop.Avalonia/Infrastructure/IChapterDraftStore.cs src/Tianming.Desktop.Avalonia/Infrastructure/FileChapterDraftStore.cs tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/FileChapterDraftStoreTests.cs
git commit -m "feat(editor): IChapterDraftStore + FileChapterDraftStore + 5 测试"
```

---

### Task 6: MarkdownEditor 控件（AvaloniaEdit TextEditor 可写包装）

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Controls/MarkdownEditor.axaml`
- Create: `src/Tianming.Desktop.Avalonia/Controls/MarkdownEditor.axaml.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Controls/MarkdownEditorTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Tianming.Desktop.Avalonia.Tests/Controls/MarkdownEditorTests.cs`:

```csharp
using Avalonia.Headless.XUnit;
using Tianming.Desktop.Avalonia.Controls;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class MarkdownEditorTests
{
    [AvaloniaFact]
    public void Defaults_text_empty_writable_wrap_true()
    {
        var e = new MarkdownEditor();
        Assert.Equal(string.Empty, e.Text);
        Assert.False(e.IsReadOnly);
        Assert.True(e.WordWrap);
    }

    [AvaloniaFact]
    public void Set_text_persists_to_property()
    {
        var e = new MarkdownEditor { Text = "# hello" };
        Assert.Equal("# hello", e.Text);
    }

    [AvaloniaFact]
    public void Set_text_pushes_into_inner_editor()
    {
        var e = new MarkdownEditor { Text = "abc" };
        Assert.Equal("abc", e.InnerEditorText);
    }

    [AvaloniaFact]
    public void Typing_in_inner_editor_updates_Text_property()
    {
        var e = new MarkdownEditor();
        e.SetInnerEditorTextForTest("typed-by-user");
        Assert.Equal("typed-by-user", e.Text);
    }

    [AvaloniaFact]
    public void Toggle_readonly_persists()
    {
        var e = new MarkdownEditor { IsReadOnly = true };
        Assert.True(e.IsReadOnly);
    }
}
```

- [ ] **Step 2: Run test to verify fail**

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter "FullyQualifiedName~MarkdownEditorTests" --no-restore`
Expected: build fail

- [ ] **Step 3: Implement MarkdownEditor**

Create `src/Tianming.Desktop.Avalonia/Controls/MarkdownEditor.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="Tianming.Desktop.Avalonia.Controls.MarkdownEditor">
  <!-- 内容由 .cs 设置（_editor）；axaml 仅作 1:1 文件存在用于 XAML compile -->
</UserControl>
```

Create `src/Tianming.Desktop.Avalonia/Controls/MarkdownEditor.axaml.cs`:

```csharp
using Avalonia;
using Avalonia.Controls;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;

namespace Tianming.Desktop.Avalonia.Controls;

/// <summary>
/// AvaloniaEdit TextEditor 可写包装；Markdown 语法高亮；Text TwoWay binding 友好。
/// 注：AvaloniaEdit 11.0.6 的 TextEditor.TextProperty 是 StyledProperty&lt;string&gt;，
/// TextChanged 事件在用户输入后触发。这里手工双向桥接以确保 Avalonia binding 看见 update。
/// </summary>
public partial class MarkdownEditor : UserControl
{
    private readonly TextEditor _editor;
    private bool _suppressSync;

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MarkdownEditor, string>(
            nameof(Text), string.Empty,
            defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<MarkdownEditor, bool>(nameof(IsReadOnly), false);

    public static readonly StyledProperty<bool> WordWrapProperty =
        AvaloniaProperty.Register<MarkdownEditor, bool>(nameof(WordWrap), true);

    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    public bool WordWrap { get => GetValue(WordWrapProperty); set => SetValue(WordWrapProperty, value); }

    /// <summary>测试用：读取内部 editor 当前文本。</summary>
    internal string InnerEditorText => _editor.Text ?? string.Empty;

    /// <summary>测试用：模拟用户在 editor 里输入，验证 binding 出口。</summary>
    internal void SetInnerEditorTextForTest(string value)
    {
        _editor.Text = value;
    }

    public MarkdownEditor()
    {
        _editor = new TextEditor
        {
            IsReadOnly = false,
            ShowLineNumbers = false,
            WordWrap = true,
            FontFamily = "SF Mono, Menlo, Consolas, monospace",
            SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("MarkDown"),
        };
        Content = _editor;

        // editor → Text
        _editor.TextChanged += (_, _) =>
        {
            if (_suppressSync) return;
            _suppressSync = true;
            try { SetCurrentValue(TextProperty, _editor.Text ?? string.Empty); }
            finally { _suppressSync = false; }
        };

        // Text → editor
        TextProperty.Changed.AddClassHandler<MarkdownEditor>((c, _) =>
        {
            if (c._suppressSync) return;
            c._suppressSync = true;
            try
            {
                var v = c.Text ?? string.Empty;
                if (c._editor.Text != v) c._editor.Text = v;
            }
            finally { c._suppressSync = false; }
        });

        IsReadOnlyProperty.Changed.AddClassHandler<MarkdownEditor>((c, _) => c._editor.IsReadOnly = c.IsReadOnly);
        WordWrapProperty.Changed.AddClassHandler<MarkdownEditor>((c, _) => c._editor.WordWrap = c.WordWrap);
    }
}
```

- [ ] **Step 4: Run test to verify pass**

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter "FullyQualifiedName~MarkdownEditorTests" --no-restore`
Expected: 5 passed

- [ ] **Step 5: Commit**

```bash
git add src/Tianming.Desktop.Avalonia/Controls/MarkdownEditor.axaml src/Tianming.Desktop.Avalonia/Controls/MarkdownEditor.axaml.cs tests/Tianming.Desktop.Avalonia.Tests/Controls/MarkdownEditorTests.cs
git commit -m "feat(editor): MarkdownEditor (AvaloniaEdit 包装 + Text TwoWay 桥接)"
```

---

### Task 7: MarkdownPreview 控件（占位 SelectableTextBlock）

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Controls/MarkdownPreview.axaml`
- Create: `src/Tianming.Desktop.Avalonia/Controls/MarkdownPreview.axaml.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Controls/MarkdownPreviewTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Tianming.Desktop.Avalonia.Tests/Controls/MarkdownPreviewTests.cs`:

```csharp
using Avalonia.Headless.XUnit;
using Tianming.Desktop.Avalonia.Controls;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class MarkdownPreviewTests
{
    [AvaloniaFact]
    public void Defaults_markdown_empty()
    {
        var p = new MarkdownPreview();
        Assert.Equal(string.Empty, p.Markdown);
    }

    [AvaloniaFact]
    public void Set_markdown_persists()
    {
        var p = new MarkdownPreview { Markdown = "# title\nbody" };
        Assert.Equal("# title\nbody", p.Markdown);
    }

    [AvaloniaFact]
    public void Update_markdown_raises_property_changed()
    {
        var p = new MarkdownPreview();
        var changed = 0;
        p.PropertyChanged += (_, e) => { if (e.Property == MarkdownPreview.MarkdownProperty) changed++; };
        p.Markdown = "x";
        Assert.Equal(1, changed);
    }
}
```

- [ ] **Step 2: Run test to verify fail**

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter "FullyQualifiedName~MarkdownPreviewTests" --no-restore`
Expected: build fail

- [ ] **Step 3: Implement MarkdownPreview**

Create `src/Tianming.Desktop.Avalonia/Controls/MarkdownPreview.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="Tianming.Desktop.Avalonia.Controls.MarkdownPreview">
  <!-- 内容由 .cs 设置（_block）；axaml 仅作 1:1 文件存在用于 XAML compile -->
</UserControl>
```

Create `src/Tianming.Desktop.Avalonia/Controls/MarkdownPreview.axaml.cs`:

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;

namespace Tianming.Desktop.Avalonia.Controls;

/// <summary>
/// Markdown 预览占位实现：用 SelectableTextBlock 显示原 markdown 文本（monospace + wrap）。
/// TODO M4.3 后续：引入 Markdig 0.45.0 → MarkdownDocument visitor → 真正的 H1/H2/bold/list 渲染。
/// 当前先满足 M4.3 control + binding 契约，Gate 阶段（M4.3 Gate）再升级。
/// </summary>
public partial class MarkdownPreview : UserControl
{
    private readonly SelectableTextBlock _block;

    public static readonly StyledProperty<string> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownPreview, string>(nameof(Markdown), string.Empty);

    public string Markdown { get => GetValue(MarkdownProperty); set => SetValue(MarkdownProperty, value); }

    public MarkdownPreview()
    {
        _block = new SelectableTextBlock
        {
            FontFamily = "SF Mono, Menlo, Consolas, monospace",
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(12),
        };
        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _block,
        };
        MarkdownProperty.Changed.AddClassHandler<MarkdownPreview>((c, _) => c._block.Text = c.Markdown ?? string.Empty);
    }
}
```

- [ ] **Step 4: Run test to verify pass**

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter "FullyQualifiedName~MarkdownPreviewTests" --no-restore`
Expected: 3 passed

- [ ] **Step 5: Commit**

```bash
git add src/Tianming.Desktop.Avalonia/Controls/MarkdownPreview.axaml src/Tianming.Desktop.Avalonia/Controls/MarkdownPreview.axaml.cs tests/Tianming.Desktop.Avalonia.Tests/Controls/MarkdownPreviewTests.cs
git commit -m "feat(editor): MarkdownPreview 占位实现 (TODO Markdig)"
```

---

### Task 8: ChapterTabItem record + ChapterTabBar 控件

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Controls/ChapterTabItem.cs`
- Create: `src/Tianming.Desktop.Avalonia/Controls/ChapterTabBar.axaml`
- Create: `src/Tianming.Desktop.Avalonia/Controls/ChapterTabBar.axaml.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Controls/ChapterTabBarTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Tianming.Desktop.Avalonia.Tests/Controls/ChapterTabBarTests.cs`:

```csharp
using System.Collections.ObjectModel;
using Avalonia.Headless.XUnit;
using Tianming.Desktop.Avalonia.Controls;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class ChapterTabBarTests
{
    [AvaloniaFact]
    public void Defaults_tabs_empty_active_null()
    {
        var bar = new ChapterTabBar();
        Assert.NotNull(bar.Tabs);
        Assert.Empty(bar.Tabs);
        Assert.Null(bar.ActiveTab);
    }

    [AvaloniaFact]
    public void Set_tabs_persists()
    {
        var bar = new ChapterTabBar();
        bar.Tabs = new ObservableCollection<ChapterTabItem>
        {
            new("ch-1", "第 1 章", IsDirty: false, IsActive: true),
            new("ch-2", "第 2 章", IsDirty: true,  IsActive: false),
        };
        Assert.Equal(2, bar.Tabs!.Count);
        Assert.Equal("第 1 章", bar.Tabs[0].Title);
        Assert.True(bar.Tabs[1].IsDirty);
    }

    [AvaloniaFact]
    public void Set_active_tab_persists()
    {
        var bar = new ChapterTabBar();
        var t = new ChapterTabItem("ch-7", "末章", false, true);
        bar.ActiveTab = t;
        Assert.Same(t, bar.ActiveTab);
    }
}
```

- [ ] **Step 2: Run test to verify fail**

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter "FullyQualifiedName~ChapterTabBarTests" --no-restore`
Expected: build fail

- [ ] **Step 3: Implement ChapterTabItem record**

Create `src/Tianming.Desktop.Avalonia/Controls/ChapterTabItem.cs`:

```csharp
namespace Tianming.Desktop.Avalonia.Controls;

/// <summary>顶部章节 tab 一项。VM 暴露给 ChapterTabBar 的 immutable 投影。</summary>
public sealed record ChapterTabItem(string ChapterId, string Title, bool IsDirty, bool IsActive);
```

- [ ] **Step 4: Implement ChapterTabBar**

Create `src/Tianming.Desktop.Avalonia/Controls/ChapterTabBar.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="using:Tianming.Desktop.Avalonia.Controls"
             x:Class="Tianming.Desktop.Avalonia.Controls.ChapterTabBar">
  <ScrollViewer HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Disabled">
    <ItemsControl ItemsSource="{Binding Tabs, RelativeSource={RelativeSource AncestorType=controls:ChapterTabBar}}">
      <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate>
          <StackPanel Orientation="Horizontal" Spacing="2"/>
        </ItemsPanelTemplate>
      </ItemsControl.ItemsPanel>
      <ItemsControl.ItemTemplate>
        <DataTemplate DataType="controls:ChapterTabItem">
          <Border Padding="10,6" CornerRadius="4" Background="#F1F2F4">
            <StackPanel Orientation="Horizontal" Spacing="6">
              <TextBlock Text="{Binding Title}" FontSize="12" VerticalAlignment="Center"/>
              <TextBlock Text="•" Foreground="#E03B3B" VerticalAlignment="Center" IsVisible="{Binding IsDirty}"/>
              <TextBlock Text="×" FontSize="12" Foreground="#9C9FA5" VerticalAlignment="Center"/>
            </StackPanel>
          </Border>
        </DataTemplate>
      </ItemsControl.ItemTemplate>
    </ItemsControl>
  </ScrollViewer>
</UserControl>
```

Create `src/Tianming.Desktop.Avalonia/Controls/ChapterTabBar.axaml.cs`:

```csharp
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;

namespace Tianming.Desktop.Avalonia.Controls;

/// <summary>
/// 顶部章节 tab 条。Tabs ObservableCollection&lt;ChapterTabItem&gt;；ActiveTab 单选指针。
/// 点击 / 关闭交互由外层 VM 通过 attached command 处理（M4.3 先只渲染，不绑命令——草稿页只挂 1 个 tab）。
/// </summary>
public partial class ChapterTabBar : UserControl
{
    public static readonly StyledProperty<ObservableCollection<ChapterTabItem>?> TabsProperty =
        AvaloniaProperty.Register<ChapterTabBar, ObservableCollection<ChapterTabItem>?>(nameof(Tabs));

    public static readonly StyledProperty<ChapterTabItem?> ActiveTabProperty =
        AvaloniaProperty.Register<ChapterTabBar, ChapterTabItem?>(nameof(ActiveTab));

    public ObservableCollection<ChapterTabItem>? Tabs { get => GetValue(TabsProperty); set => SetValue(TabsProperty, value); }
    public ChapterTabItem? ActiveTab { get => GetValue(ActiveTabProperty); set => SetValue(ActiveTabProperty, value); }

    public ChapterTabBar()
    {
        InitializeComponent();
        SetCurrentValue(TabsProperty, new ObservableCollection<ChapterTabItem>());
    }
}
```

- [ ] **Step 5: Run test to verify pass**

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter "FullyQualifiedName~ChapterTabBarTests" --no-restore`
Expected: 3 passed

- [ ] **Step 6: Commit**

```bash
git add src/Tianming.Desktop.Avalonia/Controls/ChapterTabItem.cs src/Tianming.Desktop.Avalonia/Controls/ChapterTabBar.axaml src/Tianming.Desktop.Avalonia/Controls/ChapterTabBar.axaml.cs tests/Tianming.Desktop.Avalonia.Tests/Controls/ChapterTabBarTests.cs
git commit -m "feat(editor): ChapterTabBar + ChapterTabItem record + 3 测试"
```

---

### Task 9: ChapterEditorViewModel（单 tab VM）

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/Editor/ChapterEditorViewModel.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Editor/ChapterEditorViewModelTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Editor/ChapterEditorViewModelTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Tests.Infrastructure;
using Tianming.Desktop.Avalonia.ViewModels.Editor;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Editor;

public class ChapterEditorViewModelTests
{
    [Fact]
    public void New_vm_has_empty_content_and_not_dirty()
    {
        var vm = NewVm(out _, out _);
        Assert.Equal(string.Empty, vm.Content);
        Assert.False(vm.IsDirty);
        Assert.Equal(0, vm.WordCount);
    }

    [Fact]
    public void Set_content_marks_dirty_and_updates_word_count()
    {
        var vm = NewVm(out _, out _);
        vm.Content = "天命之书";
        Assert.True(vm.IsDirty);
        Assert.Equal(4, vm.WordCount);
    }

    [Fact]
    public void Set_content_schedules_autosave()
    {
        var vm = NewVm(out var store, out var fake);
        vm.Content = "abc";
        fake.AdvanceAll();
        // store should have been called
        Assert.True(store.SaveCalls.Count >= 1);
        Assert.Equal("abc", store.SaveCalls[^1].content);
    }

    [Fact]
    public void Autosave_clears_dirty_after_save()
    {
        var vm = NewVm(out var store, out var fake);
        vm.Content = "abc";
        fake.AdvanceAll();
        // give the async save a chance
        store.WaitAllSaves();
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void Loaded_content_does_not_mark_dirty()
    {
        var vm = NewVm(out _, out _);
        vm.LoadContent("# initial");
        Assert.False(vm.IsDirty);
        Assert.Equal("# initial", vm.Content);
    }

    private static ChapterEditorViewModel NewVm(out FakeDraftStore store, out FakeTimerScheduler fake)
    {
        store = new FakeDraftStore();
        fake = new FakeTimerScheduler();
        var sch = new AutoSaveScheduler(fake, TimeSpan.FromMilliseconds(50));
        return new ChapterEditorViewModel(
            projectId: "proj-test",
            chapterId: "ch-1",
            title: "第 1 章",
            draftStore: store,
            autoSave: sch);
    }
}

internal sealed class FakeDraftStore : IChapterDraftStore
{
    public List<(string projectId, string chapterId, string content)> SaveCalls { get; } = new();
    private readonly List<Task> _inflight = new();

    public Task SaveDraftAsync(string projectId, string chapterId, string content)
    {
        SaveCalls.Add((projectId, chapterId, content));
        var t = Task.CompletedTask;
        _inflight.Add(t);
        return t;
    }
    public Task<string?> LoadDraftAsync(string projectId, string chapterId) => Task.FromResult<string?>(null);

    public void WaitAllSaves()
    {
        Task.WaitAll(_inflight.ToArray());
    }
}
```

- [ ] **Step 2: Run to verify fail**

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter "FullyQualifiedName~ChapterEditorViewModelTests" --no-restore`
Expected: build fail

- [ ] **Step 3: Implement ChapterEditorViewModel**

Create `src/Tianming.Desktop.Avalonia/ViewModels/Editor/ChapterEditorViewModel.cs`:

```csharp
using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Tianming.Desktop.Avalonia.Infrastructure;

namespace Tianming.Desktop.Avalonia.ViewModels.Editor;

/// <summary>
/// 单个章节 tab 的 ViewModel：内容 / 字数 / dirty / 自动保存调度。
/// 由 EditorWorkspaceViewModel 创建并管理生命周期。
/// </summary>
public sealed partial class ChapterEditorViewModel : ObservableObject
{
    private readonly string _projectId;
    private readonly IChapterDraftStore _draftStore;
    private readonly AutoSaveScheduler _autoSave;
    private bool _loading;

    public string ChapterId { get; }
    public string Title { get; }

    [ObservableProperty] private string _content = string.Empty;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private int _wordCount;
    [ObservableProperty] private DateTime? _savedAt;

    public ChapterEditorViewModel(
        string projectId,
        string chapterId,
        string title,
        IChapterDraftStore draftStore,
        AutoSaveScheduler autoSave)
    {
        _projectId = projectId ?? throw new ArgumentNullException(nameof(projectId));
        ChapterId = chapterId ?? throw new ArgumentNullException(nameof(chapterId));
        Title = title ?? string.Empty;
        _draftStore = draftStore ?? throw new ArgumentNullException(nameof(draftStore));
        _autoSave = autoSave ?? throw new ArgumentNullException(nameof(autoSave));
    }

    /// <summary>用于从存储载入初始内容（不触发 dirty / autosave）。</summary>
    public void LoadContent(string content)
    {
        _loading = true;
        try
        {
            Content = content ?? string.Empty;
            IsDirty = false;
        }
        finally { _loading = false; }
    }

    partial void OnContentChanged(string value)
    {
        WordCount = WordCounter.Count(value);
        if (_loading) return;
        IsDirty = true;
        _autoSave.Schedule(ChapterId, () => _ = SaveAsync(value));
    }

    private async Task SaveAsync(string content)
    {
        await _draftStore.SaveDraftAsync(_projectId, ChapterId, content).ConfigureAwait(false);
        SavedAt = DateTime.Now;
        IsDirty = false;
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter "FullyQualifiedName~ChapterEditorViewModelTests" --no-restore`
Expected: 5 passed

- [ ] **Step 5: Commit**

```bash
git add src/Tianming.Desktop.Avalonia/ViewModels/Editor/ChapterEditorViewModel.cs tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Editor/ChapterEditorViewModelTests.cs
git commit -m "feat(editor): ChapterEditorViewModel + 5 测试"
```

---

### Task 10: EditorWorkspaceViewModel（容纳多 tab）

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/Editor/EditorWorkspaceViewModel.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Editor/EditorWorkspaceViewModelTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Editor/EditorWorkspaceViewModelTests.cs`:

```csharp
using System;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Tests.Infrastructure;
using Tianming.Desktop.Avalonia.ViewModels.Editor;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Editor;

public class EditorWorkspaceViewModelTests
{
    [Fact]
    public void New_workspace_has_one_default_tab()
    {
        var vm = NewWorkspace();
        Assert.Single(vm.Tabs);
        Assert.NotNull(vm.ActiveTab);
    }

    [Fact]
    public void Default_tab_chapterId_and_title_are_set()
    {
        var vm = NewWorkspace();
        Assert.NotNull(vm.ActiveTab);
        Assert.False(string.IsNullOrEmpty(vm.ActiveTab!.ChapterId));
        Assert.False(string.IsNullOrEmpty(vm.ActiveTab!.Title));
    }

    [Fact]
    public void AddTab_appends_and_activates()
    {
        var vm = NewWorkspace();
        vm.AddTab("ch-2", "第 2 章");
        Assert.Equal(2, vm.Tabs.Count);
        Assert.Equal("ch-2", vm.ActiveTab!.ChapterId);
    }

    [Fact]
    public void CloseTab_removes_and_activates_neighbor()
    {
        var vm = NewWorkspace();
        vm.AddTab("ch-2", "第 2 章");
        vm.AddTab("ch-3", "第 3 章");
        Assert.Equal("ch-3", vm.ActiveTab!.ChapterId);

        vm.CloseTab(vm.ActiveTab);

        Assert.Equal(2, vm.Tabs.Count);
        Assert.NotNull(vm.ActiveTab);
    }

    [Fact]
    public void ActivateTab_switches_active()
    {
        var vm = NewWorkspace();
        vm.AddTab("ch-2", "第 2 章");
        var first = vm.Tabs[0];
        vm.ActivateTab(first);
        Assert.Same(first, vm.ActiveTab);
    }

    private static EditorWorkspaceViewModel NewWorkspace()
    {
        var store = new FakeDraftStore();
        var fake = new FakeTimerScheduler();
        var sch = new AutoSaveScheduler(fake, TimeSpan.FromMilliseconds(50));
        return new EditorWorkspaceViewModel("proj-test", store, sch);
    }
}
```

- [ ] **Step 2: Run to verify fail**

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter "FullyQualifiedName~EditorWorkspaceViewModelTests" --no-restore`
Expected: build fail

- [ ] **Step 3: Implement EditorWorkspaceViewModel**

Create `src/Tianming.Desktop.Avalonia/ViewModels/Editor/EditorWorkspaceViewModel.cs`:

```csharp
using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Controls;
using Tianming.Desktop.Avalonia.Infrastructure;

namespace Tianming.Desktop.Avalonia.ViewModels.Editor;

/// <summary>
/// 章节编辑器工作区根 VM：管理多个 ChapterEditorViewModel（每 tab 一个），
/// 暴露 Tabs（投影成 ChapterTabItem 给 ChapterTabBar 渲染）、ActiveTab、ActiveEditor。
/// </summary>
public sealed partial class EditorWorkspaceViewModel : ObservableObject
{
    private readonly string _projectId;
    private readonly IChapterDraftStore _draftStore;
    private readonly AutoSaveScheduler _autoSave;

    /// <summary>所有打开的章节编辑器（每 tab 一个 VM）。</summary>
    public ObservableCollection<ChapterEditorViewModel> Editors { get; } = new();

    /// <summary>给 ChapterTabBar 渲染的投影。</summary>
    public ObservableCollection<ChapterTabItem> Tabs { get; } = new();

    [ObservableProperty] private ChapterTabItem? _activeTab;
    [ObservableProperty] private ChapterEditorViewModel? _activeEditor;

    public EditorWorkspaceViewModel(string projectId, IChapterDraftStore draftStore, AutoSaveScheduler autoSave)
    {
        _projectId = projectId ?? throw new ArgumentNullException(nameof(projectId));
        _draftStore = draftStore ?? throw new ArgumentNullException(nameof(draftStore));
        _autoSave = autoSave ?? throw new ArgumentNullException(nameof(autoSave));

        // 启动给一个示例草稿 tab，让 dotnet run 切到草稿页时可见非空内容。
        AddTab("ch-001", "第 1 章 蒙学开篇");
        ActiveEditor!.LoadContent("# 第 1 章 蒙学开篇\n\n清晨，少年推开木门——");
    }

    [RelayCommand]
    public void AddTab(string chapterId, string title)
    {
        var editor = new ChapterEditorViewModel(_projectId, chapterId, title, _draftStore, _autoSave);
        Editors.Add(editor);
        var item = new ChapterTabItem(chapterId, title, IsDirty: false, IsActive: true);
        Tabs.Add(item);
        ActivateTab(item);
    }

    [RelayCommand]
    public void CloseTab(ChapterTabItem? tab)
    {
        if (tab == null) return;
        var idx = Tabs.IndexOf(tab);
        if (idx < 0) return;
        Tabs.RemoveAt(idx);
        var ed = Editors.FirstOrDefault(e => e.ChapterId == tab.ChapterId);
        if (ed != null) Editors.Remove(ed);

        if (Tabs.Count == 0)
        {
            ActiveTab = null;
            ActiveEditor = null;
            return;
        }
        var neighbor = Tabs[Math.Min(idx, Tabs.Count - 1)];
        ActivateTab(neighbor);
    }

    [RelayCommand]
    public void ActivateTab(ChapterTabItem? tab)
    {
        if (tab == null) return;
        ActiveTab = tab;
        ActiveEditor = Editors.FirstOrDefault(e => e.ChapterId == tab.ChapterId);
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter "FullyQualifiedName~EditorWorkspaceViewModelTests" --no-restore`
Expected: 5 passed

- [ ] **Step 5: Commit**

```bash
git add src/Tianming.Desktop.Avalonia/ViewModels/Editor/EditorWorkspaceViewModel.cs tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Editor/EditorWorkspaceViewModelTests.cs
git commit -m "feat(editor): EditorWorkspaceViewModel + 5 测试"
```

---

### Task 11: EditorWorkspaceView（image 05 布局）

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Views/Editor/EditorWorkspaceView.axaml`
- Create: `src/Tianming.Desktop.Avalonia/Views/Editor/EditorWorkspaceView.axaml.cs`

- [ ] **Step 1: Create view code-behind**

Create `src/Tianming.Desktop.Avalonia/Views/Editor/EditorWorkspaceView.axaml.cs`:

```csharp
using Avalonia.Controls;

namespace Tianming.Desktop.Avalonia.Views.Editor;

public partial class EditorWorkspaceView : UserControl
{
    public EditorWorkspaceView() => InitializeComponent();
}
```

- [ ] **Step 2: Create view axaml (image 05 5 区布局)**

Create `src/Tianming.Desktop.Avalonia/Views/Editor/EditorWorkspaceView.axaml`:

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="using:Tianming.Desktop.Avalonia.Controls"
             xmlns:vme="using:Tianming.Desktop.Avalonia.ViewModels.Editor"
             x:Class="Tianming.Desktop.Avalonia.Views.Editor.EditorWorkspaceView"
             x:DataType="vme:EditorWorkspaceViewModel">

  <Grid RowDefinitions="36,*,28" Background="{DynamicResource SurfaceCanvasBrush}">

    <!-- 顶部：ChapterTabBar -->
    <controls:ChapterTabBar Grid.Row="0"
                            Tabs="{Binding Tabs}"
                            ActiveTab="{Binding ActiveTab, Mode=TwoWay}"
                            Margin="8,4"/>

    <!-- 中部 3 列：左 sidebar 章节树 / 主 editor / 右 metadata -->
    <Grid Grid.Row="1" ColumnDefinitions="220,*,260">

      <!-- 左 sidebar 章节树（M4.3 占位：1 项当前 tab） -->
      <Border Grid.Column="0" Background="#FAFAFB" BorderBrush="#E5E7EB" BorderThickness="0,0,1,0">
        <StackPanel Margin="12,12,12,0" Spacing="6">
          <TextBlock Text="章节" FontSize="11" Foreground="#6B7280" FontWeight="Bold"/>
          <controls:SidebarTreeItem Label="{Binding ActiveTab.Title, FallbackValue=未命名章节}"
                                    IconGlyph="📄"
                                    IsSelected="True"/>
        </StackPanel>
      </Border>

      <!-- 主区 MarkdownEditor -->
      <controls:MarkdownEditor Grid.Column="1"
                               Text="{Binding ActiveEditor.Content, Mode=TwoWay, FallbackValue=''}"
                               IsReadOnly="False"
                               WordWrap="True"
                               Margin="12"/>

      <!-- 右 metadata 占位 -->
      <Border Grid.Column="2" Background="#FAFAFB" BorderBrush="#E5E7EB" BorderThickness="1,0,0,0">
        <StackPanel Margin="12" Spacing="10">
          <TextBlock Text="元数据" FontSize="11" Foreground="#6B7280" FontWeight="Bold"/>
          <StackPanel Spacing="3">
            <TextBlock Text="章节标题" FontSize="11" Foreground="#9CA3AF"/>
            <TextBlock Text="{Binding ActiveTab.Title, FallbackValue=未命名章节}" FontSize="13"/>
          </StackPanel>
          <StackPanel Spacing="3">
            <TextBlock Text="字数" FontSize="11" Foreground="#9CA3AF"/>
            <TextBlock Text="{Binding ActiveEditor.WordCount, FallbackValue=0, StringFormat='{}{0} 字'}" FontSize="13"/>
          </StackPanel>
          <StackPanel Spacing="3">
            <TextBlock Text="状态" FontSize="11" Foreground="#9CA3AF"/>
            <TextBlock Text="{Binding ActiveEditor.IsDirty, FallbackValue=False, StringFormat='已修改: {0}'}" FontSize="13"/>
          </StackPanel>
        </StackPanel>
      </Border>

    </Grid>

    <!-- 底部 footer -->
    <Border Grid.Row="2" Background="#F3F4F6" BorderBrush="#E5E7EB" BorderThickness="0,1,0,0">
      <Grid ColumnDefinitions="*,Auto,Auto" Margin="12,0">
        <TextBlock Grid.Column="0"
                   Text="{Binding ActiveEditor.WordCount, FallbackValue=0, StringFormat='{}{0} 字'}"
                   VerticalAlignment="Center" FontSize="11" Foreground="#6B7280"/>
        <TextBlock Grid.Column="1"
                   Text="{Binding ActiveEditor.IsDirty, FallbackValue=False, StringFormat='已修改:{0}'}"
                   VerticalAlignment="Center" FontSize="11" Foreground="#6B7280" Margin="0,0,12,0"/>
        <TextBlock Grid.Column="2"
                   Text="{Binding ActiveEditor.SavedAt, FallbackValue='', StringFormat='保存于 {0:HH:mm:ss}'}"
                   VerticalAlignment="Center" FontSize="11" Foreground="#6B7280"/>
      </Grid>
    </Border>
  </Grid>
</UserControl>
```

- [ ] **Step 3: Build to verify**

Run: `cd /Users/jimmy/Downloads/tianming-novel-ai-writer/.worktrees/m4-3 && dotnet build src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj`
Expected: 0 error

- [ ] **Step 4: Commit**

```bash
git add src/Tianming.Desktop.Avalonia/Views/Editor/EditorWorkspaceView.axaml src/Tianming.Desktop.Avalonia/Views/Editor/EditorWorkspaceView.axaml.cs
git commit -m "feat(editor): EditorWorkspaceView (image 05 5 区布局)"
```

---

### Task 12: App.axaml DataTemplate + ResourceInclude

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml`

- [ ] **Step 1: Add namespace + DataTemplate + ResourceIncludes**

Open `src/Tianming.Desktop.Avalonia/App.axaml`. Replace the existing root `Application` opening with the same plus 2 new xmlns aliases (`vme` and `ve`), and add 1 new DataTemplate and 3 new ResourceIncludes.

Apply these specific edits:

1. After existing `xmlns:vd="using:Tianming.Desktop.Avalonia.Views.Design"` line, add:
```
             xmlns:vme="using:Tianming.Desktop.Avalonia.ViewModels.Editor"
             xmlns:ve="using:Tianming.Desktop.Avalonia.Views.Editor"
```

2. Inside `<Application.DataTemplates>`, after the last existing `DataTemplate` (`vmd:CreativeMaterialsViewModel`), insert:
```xml
    <DataTemplate DataType="vme:EditorWorkspaceViewModel">
      <ve:EditorWorkspaceView/>
    </DataTemplate>
```

3. Inside `<Application.Resources><ResourceDictionary><ResourceDictionary.MergedDictionaries>`, after the `ToolCallCard.axaml` ResourceInclude, add:
```xml
        <ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Controls/MarkdownEditor.axaml"/>
        <ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Controls/MarkdownPreview.axaml"/>
        <ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Controls/ChapterTabBar.axaml"/>
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj`
Expected: 0 error

- [ ] **Step 3: Commit**

```bash
git add src/Tianming.Desktop.Avalonia/App.axaml
git commit -m "feat(editor): App.axaml 加 EditorWorkspaceView DataTemplate + 3 ResourceIncludes"
```

---

### Task 13: DI 注册 (AvaloniaShellServiceCollectionExtensions)

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs`

- [ ] **Step 1: Add imports**

Open `src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs`. After the existing `using Tianming.Desktop.Avalonia.ViewModels.Design;` line, add:

```csharp
using Tianming.Desktop.Avalonia.ViewModels.Editor;
using Tianming.Desktop.Avalonia.Views.Editor;
```

- [ ] **Step 2: Register infra + VM in AddAvaloniaShell**

Inside `AddAvaloniaShell`, **after** the existing `s.AddSingleton<ICurrentProjectService, CurrentProjectService>();` line, add:

```csharp
        // M4.3 章节编辑器基础设施
        s.AddSingleton<ITimerScheduler, DispatcherTimerScheduler>();
        s.AddSingleton<IChapterDraftStore>(sp =>
        {
            var paths = sp.GetRequiredService<AppPaths>();
            return new FileChapterDraftStore(System.IO.Path.Combine(paths.AppSupportDirectory, "Drafts"));
        });
        s.AddSingleton<AutoSaveScheduler>(sp =>
            new AutoSaveScheduler(sp.GetRequiredService<ITimerScheduler>(), System.TimeSpan.FromSeconds(2)));
```

**After** the existing `s.AddTransient<CreativeMaterialsViewModel>();` line (end of M4.1 block), add:

```csharp

        // M4.3 章节编辑器 VM
        s.AddTransient<EditorWorkspaceViewModel>(sp =>
            new EditorWorkspaceViewModel(
                projectId: "default",
                sp.GetRequiredService<IChapterDraftStore>(),
                sp.GetRequiredService<AutoSaveScheduler>()));
```

- [ ] **Step 3: Register PageKey in RegisterPages**

In `RegisterPages`, **after** the line `reg.Register<PlaceholderViewModel, PlaceholderView>(PageKeys.Settings);` add:

```csharp
        // M4.3 章节编辑器
        reg.Register<EditorWorkspaceViewModel, EditorWorkspaceView>(PageKeys.Editor);
```

- [ ] **Step 4: Build to verify**

Run: `dotnet build src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj`
Expected: 0 error

- [ ] **Step 5: Commit**

```bash
git add src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs
git commit -m "feat(editor): DI 注册 ITimerScheduler/IChapterDraftStore/AutoSaveScheduler/EditorWorkspaceVM + PageKey"
```

---

### Task 14: LeftNav 启用"草稿"

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/ViewModels/Shell/LeftNavViewModel.cs`

- [ ] **Step 1: Add "草稿" item to 写作 group**

Open `src/Tianming.Desktop.Avalonia/ViewModels/Shell/LeftNavViewModel.cs`. Replace the 写作 group block:

```csharp
        Groups.Add(new NavRailGroup("写作", new List<NavRailItem>
        {
            new(PageKeys.Welcome,   "欢迎",     "home"),
            new(PageKeys.Dashboard, "仪表盘",   "layout-dashboard"),
        }));
```

with:

```csharp
        Groups.Add(new NavRailGroup("写作", new List<NavRailItem>
        {
            new(PageKeys.Welcome,   "欢迎",     "home"),
            new(PageKeys.Dashboard, "仪表盘",   "layout-dashboard"),
            new(PageKeys.Editor,    "草稿",     "📝"),
        }));
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj`
Expected: 0 error

- [ ] **Step 3: Commit**

```bash
git add src/Tianming.Desktop.Avalonia/ViewModels/Shell/LeftNavViewModel.cs
git commit -m "feat(left-nav): 启用 M4.3 草稿 → PageKeys.Editor"
```

---

### Task 15: 全量测试 + 启动冒烟

**Files:** (no new files)

- [ ] **Step 1: Run full test suite**

Run: `cd /Users/jimmy/Downloads/tianming-novel-ai-writer/.worktrees/m4-3 && dotnet build && dotnet test --no-build`
Expected: 0 error 0 warning; baseline 1336 + new ~30 = 1366 left+/ all pass.

- [ ] **Step 2: Verify baseline not regressed**

Each test project should still report at least its baseline count:
- Tianming.AI.Tests ≥ 144
- Tianming.ProjectData.Tests ≥ 273
- Tianming.Framework.Tests ≥ 791
- Tianming.Desktop.Avalonia.Tests ≥ 128 + (5+4+5+5+3+3+5+5) = 163

- [ ] **Step 3: Sanity-check startup (manual)**

Run (background): `cd /Users/jimmy/Downloads/tianming-novel-ai-writer/.worktrees/m4-3 && dotnet run --project src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj`
Expected: app launches; left nav has new "草稿" entry under 写作; clicking it loads EditorWorkspaceView with 1 tab + mock content; no exception in console.

If it crashes: capture stack trace, diagnose with systematic-debugging, fix in a separate commit, re-run.

- [ ] **Step 4: Final commit (if necessary)**

If everything passes without changes, no commit needed. If you patched anything in Step 3, commit it:
```bash
git add <files>
git commit -m "fix(editor): <issue> from smoke test"
```

---

## Self-Review

**1. Spec coverage:**
- M4.3.1 MarkdownEditor (AvaloniaEdit) — Task 6 ✓
- M4.3.2 MarkdownPreview — Task 7 (occupied with TODO Markdig) ✓
- M4.3.3 ChapterTabBar + EditorWorkspaceView + 持久化 — Tasks 8, 11, 5 ✓
- 自动保存防抖 2s — Tasks 3, 4 ✓
- IChapterDraftStore portable 接口 + FileChapterDraftStore — Task 5 ✓
- PageKey Editor — Task 1 ✓
- LeftNav 启用草稿 — Task 14 ✓
- DI 注册 + PageRegistry + DataTemplate — Tasks 12, 13 ✓
- ≥ 3/控件 + ≥ 3/VM + DraftStore 测试 — covered (5+3+3 for controls, 5+5 for VMs, 5 for DraftStore) ✓
- AutoSaveScheduler with FakeTimerScheduler — Tasks 3, 4 ✓

**Gaps:** ChapterTabBar click/close interaction is stubbed (renders only). M4.3 brief says "顶 ChapterTabBar + ... 每个 tab 含章节标题 / dirty 指示 / 关闭按钮" — visual element present, but the actual click/close command wiring is deferred. EditorWorkspaceVM exposes `AddTabCommand` / `CloseTabCommand` / `ActivateTabCommand` so future task can wire them. This is acceptable for M4.3 scope since brief says we only need 1 mock chapter on startup.

**2. Placeholder scan:**
- No "TBD" / "TODO: fill in" in steps. Markdig TODO comment is **intentional** documented deferral, called out in MarkdownPreview docstring.

**3. Type consistency:**
- `IChapterDraftStore.SaveDraftAsync(string projectId, string chapterId, string content)` — used identically in Task 5, 9, 10 ✓
- `AutoSaveScheduler(ITimerScheduler, TimeSpan)` — Task 3/4/9/10/13 ✓
- `ChapterTabItem(string ChapterId, string Title, bool IsDirty, bool IsActive)` — Task 8, 10, 11 ✓
- `ChapterEditorViewModel(string projectId, string chapterId, string title, IChapterDraftStore, AutoSaveScheduler)` — Task 9, 10 ✓
- `EditorWorkspaceViewModel(string projectId, IChapterDraftStore, AutoSaveScheduler)` — Task 10, 13 ✓
- `FakeTimerScheduler` defined in `AutoSaveSchedulerTests.cs` (`Tianming.Desktop.Avalonia.Tests.Infrastructure` namespace), reused in `ChapterEditorViewModelTests.cs` and `EditorWorkspaceViewModelTests.cs` via `using Tianming.Desktop.Avalonia.Tests.Infrastructure;` ✓
- `FakeDraftStore` defined in `ChapterEditorViewModelTests.cs`, reused in `EditorWorkspaceViewModelTests.cs` ✓
- `PageKeys.Editor` — Task 1, 13, 14 ✓
- `WordCounter.Count(string?)` — Task 2, 9 ✓

---

## Execution Handoff

Plan complete and saved to `Docs/superpowers/plans/2026-05-13-tianming-m4-3-chapter-editor.md`.

Recommended execution: **Inline Execution** (subagent-driven-development is overkill for a 15-task linear plan with tight scope). Proceed using superpowers:executing-plans.
