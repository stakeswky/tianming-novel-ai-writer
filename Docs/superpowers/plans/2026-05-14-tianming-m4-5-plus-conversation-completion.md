# M4.5+ AI 对话面板补完 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把 m4-5 分支的本地 demo 切片升级成真对话面板：DI 注册 7 个 AI 类、补 4 个控件、升级 BulkEmitter 为 16ms timer、接真 ConversationOrchestrator + IFileSessionStore + 历史抽屉 + @query 引用扩展。

**Architecture:** ConversationPanelViewModel 注入 ConversationOrchestrator/IFileSessionStore，SendCommand 走真 StreamAsync，BulkEmitter 用 DispatcherTimer 16ms 批量 flush ChatStreamDelta 到 ObservableCollection。新增 ConversationPanelView 替换 RightConversationView 内层；ConversationHistoryDrawer 左滑抽屉显示 SessionSummary 列表；ReferenceDropdown 监听 InputDraft 中的 `@`。

**Tech Stack:** Avalonia 11 + AvaloniaEdit + CommunityToolkit.Mvvm + Microsoft.Extensions.DI + System.Text.Json。`DispatcherTimer` 不可单测，所以 BulkEmitter 抽 `IDispatcherScheduler`（参考 M4.3 的 `ITimerScheduler`）。

**Branch:** `m4-5-plus-conversation-completion`（基于 `m4-5-ai-conversation-panel` 合并入 main 后开）。

**前置条件:**
1. Round 2 已合并：m4-4 / m4-5 / m4-6 全部 merge 入 main
2. main 上能 build + test 全绿

---

## Task 0：基线确认

**Files:** 无（只做检查）

- [ ] **Step 0.1：确认 Round 2 已合**

```bash
cd /Users/jimmy/Downloads/tianming-novel-ai-writer
git log main --oneline | head -10
```

Expected: 看到 Merge Lane M4.4/M4.5/M4.6 三条 merge commit。

- [ ] **Step 0.2：跑基线测试**

```bash
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -10
```

Expected: 全过，约 1400+ tests pass。

- [ ] **Step 0.3：开 worktree**

```bash
git worktree add /Users/jimmy/Downloads/tianming-m4-5-plus -b m4-5-plus-conversation-completion main
cd /Users/jimmy/Downloads/tianming-m4-5-plus
```

---

## Task 1：DI 注册 7 个 AI 核心类

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs`
- Test: `tests/Tianming.Desktop.Avalonia.Tests/DI/ConversationRegistrationTests.cs`

- [ ] **Step 1.1：写 ConversationRegistrationTests（先红）**

创建 `tests/Tianming.Desktop.Avalonia.Tests/DI/ConversationRegistrationTests.cs`：

```csharp
using Microsoft.Extensions.DependencyInjection;
using Tianming.Desktop.Avalonia;
using TM.Services.Framework.AI.SemanticKernel.Conversation;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.DI;

public class ConversationRegistrationTests
{
    [Fact]
    public void Build_ResolvesConversationOrchestrator()
    {
        using var sp = (ServiceProvider)AppHost.Build();
        var orch = sp.GetRequiredService<ConversationOrchestrator>();
        Assert.NotNull(orch);
    }

    [Fact]
    public void Build_ResolvesAllThreeConversationTools()
    {
        using var sp = (ServiceProvider)AppHost.Build();
        var tools = sp.GetServices<IConversationTool>();
        Assert.Equal(3, tools.Count());
    }

    [Fact]
    public void Build_ResolvesFileSessionStore()
    {
        using var sp = (ServiceProvider)AppHost.Build();
        var store = sp.GetRequiredService<IFileSessionStore>();
        Assert.NotNull(store);
    }
}
```

- [ ] **Step 1.2：跑 test 确认红**

```bash
dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter ConversationRegistrationTests --nologo -v minimal
```

Expected: FAIL — `Unable to resolve service for type 'TM.Services.Framework.AI.SemanticKernel.Conversation.ConversationOrchestrator'`

- [ ] **Step 1.3：在 AvaloniaShellServiceCollectionExtensions.cs 加 DI**

在 `// M4.6 AI 管理` 区块后插入：

```csharp
// M4.5+ AI 对话面板 — Orchestrator + Tools + SessionStore
s.AddSingleton<OpenAICompatibleChatClient>(sp =>
    new OpenAICompatibleChatClient(sp.GetRequiredService<System.Net.Http.HttpClient>()));
s.AddSingleton<TagBasedThinkingStrategy>();
s.AddSingleton<AskModeMapper>();
s.AddSingleton<IPlanParser, PlanStepParser>();
s.AddSingleton<PlanModeMapper>(sp =>
    new PlanModeMapper(sp.GetRequiredService<IPlanParser>()));
s.AddSingleton<AgentModeMapper>();
s.AddSingleton<IFileSessionStore>(sp =>
{
    var paths = sp.GetRequiredService<AppPaths>();
    var sessionsDir = Path.Combine(paths.AppSupportDirectory, "Sessions");
    return new FileSessionStore(sessionsDir);
});
s.AddSingleton<IConversationTool>(sp =>
    new LookupDataTool(sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
s.AddSingleton<IConversationTool>(sp =>
    new ReadChapterTool(sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
s.AddSingleton<IConversationTool>(sp =>
    new SearchReferencesTool(sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
s.AddSingleton<ConversationOrchestrator>(sp =>
    new ConversationOrchestrator(
        sp.GetRequiredService<OpenAICompatibleChatClient>(),
        sp.GetRequiredService<TagBasedThinkingStrategy>(),
        sp.GetRequiredService<IFileSessionStore>(),
        sp.GetServices<IConversationTool>(),
        sp.GetRequiredService<AskModeMapper>(),
        sp.GetRequiredService<PlanModeMapper>(),
        sp.GetRequiredService<AgentModeMapper>(),
        sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
```

并在 using 区加：
```csharp
using TM.Services.Framework.AI.SemanticKernel.Conversation;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Mapping;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Parsing;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Thinking;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Tools;
using TM.Services.Framework.AI.SemanticKernel;
using TM.Services.Framework.AI.Core;
```

- [ ] **Step 1.4：跑 test 确认绿**

```bash
dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter ConversationRegistrationTests --nologo -v minimal
```

Expected: PASS 3/3.

- [ ] **Step 1.5：commit**

```bash
git add src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs \
        tests/Tianming.Desktop.Avalonia.Tests/DI/ConversationRegistrationTests.cs
git commit -m "feat(conversation): M4.5+.1 DI 注册 Orchestrator + Tools + SessionStore"
```

---

## Task 2：IDispatcherScheduler 抽象（为 BulkEmitter 服务）

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Infrastructure/IDispatcherScheduler.cs`
- Create: `src/Tianming.Desktop.Avalonia/Infrastructure/AvaloniaDispatcherScheduler.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/FakeDispatcherScheduler.cs`

- [ ] **Step 2.1：写 IDispatcherScheduler 接口**

创建 `src/Tianming.Desktop.Avalonia/Infrastructure/IDispatcherScheduler.cs`：

```csharp
using System;

namespace Tianming.Desktop.Avalonia.Infrastructure;

/// <summary>
/// 抽象 16ms 批量 flush 调度器；生产用 DispatcherTimer，测试用 Fake。
/// </summary>
public interface IDispatcherScheduler
{
    IDisposable ScheduleRecurring(TimeSpan interval, Action callback);
    void Post(Action callback);
}
```

- [ ] **Step 2.2：写 AvaloniaDispatcherScheduler 实现**

创建 `src/Tianming.Desktop.Avalonia/Infrastructure/AvaloniaDispatcherScheduler.cs`：

```csharp
using System;
using Avalonia.Threading;

namespace Tianming.Desktop.Avalonia.Infrastructure;

public sealed class AvaloniaDispatcherScheduler : IDispatcherScheduler
{
    public IDisposable ScheduleRecurring(TimeSpan interval, Action callback)
    {
        var timer = new DispatcherTimer { Interval = interval };
        timer.Tick += (_, _) => callback();
        timer.Start();
        return new TimerHandle(timer);
    }

    public void Post(Action callback) => Dispatcher.UIThread.Post(callback);

    private sealed class TimerHandle : IDisposable
    {
        private readonly DispatcherTimer _timer;
        public TimerHandle(DispatcherTimer timer) => _timer = timer;
        public void Dispose() => _timer.Stop();
    }
}
```

- [ ] **Step 2.3：写 FakeDispatcherScheduler（测试用）**

创建 `tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/FakeDispatcherScheduler.cs`：

```csharp
using System;
using System.Collections.Generic;
using Tianming.Desktop.Avalonia.Infrastructure;

namespace Tianming.Desktop.Avalonia.Tests.Infrastructure;

public sealed class FakeDispatcherScheduler : IDispatcherScheduler
{
    private readonly List<Action> _recurring = new();
    private readonly List<Action> _posts = new();

    public IDisposable ScheduleRecurring(TimeSpan interval, Action callback)
    {
        _recurring.Add(callback);
        return new Disposable(() => _recurring.Remove(callback));
    }

    public void Post(Action callback) => _posts.Add(callback);

    public void Tick()
    {
        foreach (var cb in _recurring.ToArray()) cb();
    }

    public void FlushPosts()
    {
        var snapshot = _posts.ToArray();
        _posts.Clear();
        foreach (var p in snapshot) p();
    }

    private sealed class Disposable : IDisposable
    {
        private readonly Action _dispose;
        public Disposable(Action dispose) => _dispose = dispose;
        public void Dispose() => _dispose();
    }
}
```

- [ ] **Step 2.4：DI 注册 IDispatcherScheduler**

在 `AvaloniaShellServiceCollectionExtensions.cs` 加：

```csharp
s.AddSingleton<IDispatcherScheduler, AvaloniaDispatcherScheduler>();
```

加 using：
```csharp
using Tianming.Desktop.Avalonia.Infrastructure;
```

- [ ] **Step 2.5：build + commit**

```bash
dotnet build src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj --nologo -v minimal
```

Expected: Build succeeded. 0 Warning(s) 0 Error(s).

```bash
git add src/Tianming.Desktop.Avalonia/Infrastructure/IDispatcherScheduler.cs \
        src/Tianming.Desktop.Avalonia/Infrastructure/AvaloniaDispatcherScheduler.cs \
        src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs \
        tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/FakeDispatcherScheduler.cs
git commit -m "feat(conversation): M4.5+.2 IDispatcherScheduler 抽象 + Avalonia 实现 + Fake"
```

---

## Task 3：BulkEmitter 升级为 16ms 批量 flush

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/ViewModels/Conversation/ConversationPanelViewModel.cs`（BulkEmitter 类）
- Test: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Conversation/BulkEmitterTests.cs`

- [ ] **Step 3.1：写 BulkEmitterTests（先红）**

创建 `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Conversation/BulkEmitterTests.cs`：

```csharp
using System;
using System.Collections.ObjectModel;
using Tianming.Desktop.Avalonia.Controls;
using Tianming.Desktop.Avalonia.Tests.Infrastructure;
using Tianming.Desktop.Avalonia.ViewModels.Conversation;
using TM.Services.Framework.AI.SemanticKernel.Conversation;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Models;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Conversation;

public class BulkEmitterTests
{
    [Fact]
    public void Enqueue_does_not_apply_until_tick()
    {
        var bubbles = new ObservableCollection<ConversationBubbleVm>();
        var sched = new FakeDispatcherScheduler();
        var emitter = new BulkEmitter(sched);
        emitter.Start(bubbles);

        emitter.Enqueue(new AnswerDelta("hello"));

        Assert.Empty(bubbles);
    }

    [Fact]
    public void Tick_flushes_pending_deltas_into_assistant_bubble()
    {
        var bubbles = new ObservableCollection<ConversationBubbleVm>();
        var sched = new FakeDispatcherScheduler();
        var emitter = new BulkEmitter(sched);
        emitter.Start(bubbles);

        emitter.Enqueue(new AnswerDelta("hello "));
        emitter.Enqueue(new AnswerDelta("world"));
        sched.Tick();

        Assert.Single(bubbles);
        Assert.Equal(ConversationRole.Assistant, bubbles[0].Role);
        Assert.Equal("hello world", bubbles[0].Content);
    }

    [Fact]
    public void ThinkingDelta_accumulates_into_thinking_block()
    {
        var bubbles = new ObservableCollection<ConversationBubbleVm>();
        var sched = new FakeDispatcherScheduler();
        var emitter = new BulkEmitter(sched);
        emitter.Start(bubbles);

        emitter.Enqueue(new ThinkingDelta("step 1\n"));
        emitter.Enqueue(new ThinkingDelta("step 2"));
        sched.Tick();

        Assert.Equal("step 1\nstep 2", bubbles[0].ThinkingBlock);
    }

    [Fact]
    public void Stop_disposes_recurring_schedule()
    {
        var bubbles = new ObservableCollection<ConversationBubbleVm>();
        var sched = new FakeDispatcherScheduler();
        var emitter = new BulkEmitter(sched);
        emitter.Start(bubbles);
        emitter.Enqueue(new AnswerDelta("x"));
        emitter.Stop();

        sched.Tick(); // 不应抛、不应应用
        Assert.Empty(bubbles);
    }
}
```

- [ ] **Step 3.2：跑 test 确认红**

```bash
dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter BulkEmitterTests --nologo -v minimal
```

Expected: 编译失败 — `BulkEmitter` 没有 `Start` / `Enqueue` / `Stop` 方法。

- [ ] **Step 3.3：升级 BulkEmitter 实现**

在 `src/Tianming.Desktop.Avalonia/ViewModels/Conversation/ConversationPanelViewModel.cs` 替换 `BulkEmitter` 类：

```csharp
public sealed class BulkEmitter
{
    private readonly IDispatcherScheduler _scheduler;
    private readonly Queue<ChatStreamDelta> _pending = new();
    private readonly object _lock = new();
    private ObservableCollection<ConversationBubbleVm>? _bubbles;
    private IDisposable? _handle;

    public BulkEmitter(IDispatcherScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    public void Start(ObservableCollection<ConversationBubbleVm> bubbles)
    {
        _bubbles = bubbles;
        _handle = _scheduler.ScheduleRecurring(TimeSpan.FromMilliseconds(16), Flush);
    }

    public void Stop()
    {
        _handle?.Dispose();
        _handle = null;
        lock (_lock) _pending.Clear();
    }

    public void Enqueue(ChatStreamDelta delta)
    {
        lock (_lock) _pending.Enqueue(delta);
    }

    private void Flush()
    {
        if (_bubbles == null) return;
        ChatStreamDelta[] batch;
        lock (_lock)
        {
            if (_pending.Count == 0) return;
            batch = _pending.ToArray();
            _pending.Clear();
        }

        var assistant = EnsureAssistantBubble(_bubbles);
        foreach (var delta in batch) Apply(assistant, delta);
    }

    private static ConversationBubbleVm EnsureAssistantBubble(ObservableCollection<ConversationBubbleVm> bubbles)
    {
        if (bubbles.Count > 0 && bubbles[^1].Role == ConversationRole.Assistant)
            return bubbles[^1];
        var bubble = new ConversationBubbleVm
        {
            Role = ConversationRole.Assistant,
            Content = string.Empty,
            Timestamp = DateTime.Now,
        };
        bubbles.Add(bubble);
        return bubble;
    }

    private static void Apply(ConversationBubbleVm bubble, ChatStreamDelta delta)
    {
        switch (delta)
        {
            case ThinkingDelta t:
                bubble.ThinkingBlock = (bubble.ThinkingBlock ?? string.Empty) + t.Text;
                break;
            case AnswerDelta a:
                bubble.Content += a.Text;
                break;
            case ToolCallDelta c:
                bubble.Content += $"\n[tool:{c.ToolName}] {c.ArgumentsJson}";
                break;
            case ToolResultDelta r:
                bubble.Content += $"\n[result:{r.ToolCallId}] {r.ResultText}";
                break;
            case PlanStepDelta s:
                bubble.Content += $"\n{s.Step.Index}. {s.Step.Title}";
                break;
        }
    }
}
```

在 `ConversationPanelViewModel.cs` 顶部 using 加：

```csharp
using System.Collections.Generic;
using Tianming.Desktop.Avalonia.Infrastructure;
```

- [ ] **Step 3.4：跑 test 确认绿**

```bash
dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter BulkEmitterTests --nologo -v minimal
```

Expected: PASS 4/4.

- [ ] **Step 3.5：commit**

```bash
git add src/Tianming.Desktop.Avalonia/ViewModels/Conversation/ConversationPanelViewModel.cs \
        tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Conversation/BulkEmitterTests.cs
git commit -m "feat(conversation): M4.5+.3 BulkEmitter 升级 16ms 批量 flush"
```

---

## Task 4：ConversationPanelViewModel 接真 Orchestrator

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/ViewModels/Conversation/ConversationPanelViewModel.cs`
- Test: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Conversation/ConversationPanelViewModelTests.cs`（追加）

- [ ] **Step 4.1：写 SendAsync 真路径测试（先红）**

在 `ConversationPanelViewModelTests.cs` 追加：

```csharp
[Fact]
public async Task SendAsync_with_orchestrator_streams_deltas_into_bubbles()
{
    var sched = new FakeDispatcherScheduler();
    var orch = new StubOrchestrator
    {
        StreamFunc = (_, _) => AsyncDeltas(
            new ThinkingDelta("thinking..."),
            new AnswerDelta("hi"),
            new AnswerDelta(" there"))
    };
    var sessionStore = new StubSessionStore();
    var vm = new ConversationPanelViewModel(orch, sessionStore, sched, seedSamples: false);
    vm.InputDraft = "hello";

    await vm.SendCommand.ExecuteAsync(null);
    sched.Tick();

    Assert.Equal(2, vm.SampleBubbles.Count); // user + assistant
    Assert.Equal(ConversationRole.User, vm.SampleBubbles[0].Role);
    Assert.Equal("hello", vm.SampleBubbles[0].Content);
    Assert.Equal(ConversationRole.Assistant, vm.SampleBubbles[1].Role);
    Assert.Equal("hi there", vm.SampleBubbles[1].Content);
    Assert.Equal("thinking...", vm.SampleBubbles[1].ThinkingBlock);
}

private static async IAsyncEnumerable<ChatStreamDelta> AsyncDeltas(params ChatStreamDelta[] items)
{
    foreach (var d in items) { await Task.Yield(); yield return d; }
}

private sealed class StubOrchestrator : IConversationOrchestrator
{
    public Func<ConversationSession, string, IAsyncEnumerable<ChatStreamDelta>> StreamFunc { get; set; } = default!;
    public Task<ConversationSession> StartSessionAsync(ChatMode mode, string? id, CancellationToken ct) =>
        Task.FromResult(new ConversationSession { Mode = mode });
    public IAsyncEnumerable<ChatStreamDelta> SendAsync(ConversationSession s, string input, CancellationToken ct)
        => StreamFunc(s, input);
    public Task PersistAsync(ConversationSession session, CancellationToken ct) => Task.CompletedTask;
}

private sealed class StubSessionStore : IFileSessionStore
{
    public Task SaveSessionAsync(ConversationSession s, CancellationToken ct) => Task.CompletedTask;
    public Task<ConversationSession?> LoadSessionAsync(string id, CancellationToken ct) =>
        Task.FromResult<ConversationSession?>(null);
    public Task<IReadOnlyList<SessionSummary>> ListSessionsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SessionSummary>>(Array.Empty<SessionSummary>());
    public Task DeleteSessionAsync(string id, CancellationToken ct) => Task.CompletedTask;
}
```

加 using：
```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TM.Framework.UI.Workspace.RightPanel.Modes;
using TM.Services.Framework.AI.SemanticKernel.Conversation;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Models;
using Tianming.Desktop.Avalonia.Tests.Infrastructure;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Controls;
```

- [ ] **Step 4.2：跑 test 确认红**

```bash
dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter "SendAsync_with_orchestrator_streams" --nologo -v minimal
```

Expected: 编译失败 — `IConversationOrchestrator` 不存在 / 构造函数签名不匹配。

- [ ] **Step 4.3：在 Tianming.AI 抽 IConversationOrchestrator 接口**

创建 `src/Tianming.AI/SemanticKernel/Conversation/IConversationOrchestrator.cs`：

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TM.Framework.UI.Workspace.RightPanel.Modes;

namespace TM.Services.Framework.AI.SemanticKernel.Conversation;

public interface IConversationOrchestrator
{
    Task<ConversationSession> StartSessionAsync(ChatMode mode, string? sessionId = null, CancellationToken ct = default);
    IAsyncEnumerable<ChatStreamDelta> SendAsync(ConversationSession session, string userInput, CancellationToken ct = default);
    Task PersistAsync(ConversationSession session, CancellationToken ct = default);
}
```

修改 `ConversationOrchestrator` 类签名加 `: IConversationOrchestrator`：

```csharp
public sealed class ConversationOrchestrator : IConversationOrchestrator
```

- [ ] **Step 4.4：升级 ConversationPanelViewModel 构造函数 + SendCommand**

替换 `src/Tianming.Desktop.Avalonia/ViewModels/Conversation/ConversationPanelViewModel.cs` 的 ctor 和 SendAsync：

```csharp
public partial class ConversationPanelViewModel : ObservableObject, IDisposable
{
    private readonly IConversationOrchestrator? _orchestrator;
    private readonly IFileSessionStore? _sessionStore;
    private readonly BulkEmitter? _emitter;
    private ConversationSession? _currentSession;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private string _selectedMode = "ask";
    [ObservableProperty] private string _inputDraft = string.Empty;
    [ObservableProperty] private bool _isStreaming;

    public ObservableCollection<SegmentItem> ModeSegments { get; } = new()
    {
        new SegmentItem("ask", "Ask"),
        new SegmentItem("plan", "Plan"),
        new SegmentItem("agent", "Agent"),
    };

    public ObservableCollection<ConversationBubbleVm> SampleBubbles { get; } = new();

    // 旧 demo 路径保留（无 orchestrator 时 fall back 到本地预览）
    public ConversationPanelViewModel(bool seedSamples = true)
    {
        if (seedSamples) SeedSamples();
    }

    // 真 AI 路径
    public ConversationPanelViewModel(
        IConversationOrchestrator orchestrator,
        IFileSessionStore sessionStore,
        IDispatcherScheduler scheduler,
        bool seedSamples = false)
    {
        _orchestrator = orchestrator;
        _sessionStore = sessionStore;
        _emitter = new BulkEmitter(scheduler);
        _emitter.Start(SampleBubbles);
        if (seedSamples) SeedSamples();
    }

    [RelayCommand]
    private void SelectMode(string? key)
    {
        if (!string.IsNullOrWhiteSpace(key)) SelectedMode = key;
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        var input = InputDraft.Trim();
        if (input.Length == 0) return;

        SampleBubbles.Add(new ConversationBubbleVm
        {
            Role = ConversationRole.User,
            Content = input,
            Timestamp = DateTime.Now,
        });
        InputDraft = string.Empty;
        IsStreaming = true;

        if (_orchestrator == null || _emitter == null || _sessionStore == null)
        {
            // 本地 demo 路径
            await SendLocalDemoAsync(input);
            return;
        }

        var mode = ParseChatMode(SelectedMode);
        _currentSession ??= await _orchestrator.StartSessionAsync(mode);
        _currentSession.Mode = mode;
        _cts = new CancellationTokenSource();

        try
        {
            await foreach (var delta in _orchestrator.SendAsync(_currentSession, input, _cts.Token))
                _emitter.Enqueue(delta);
            await _orchestrator.PersistAsync(_currentSession, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            // user-initiated abort; nothing to do
        }
        finally
        {
            IsStreaming = false;
        }
    }

    [RelayCommand]
    private void NewSession()
    {
        _cts?.Cancel();
        _emitter?.Stop();
        _currentSession = null;
        SampleBubbles.Clear();
        InputDraft = string.Empty;
        IsStreaming = false;
        if (_emitter != null) _emitter.Start(SampleBubbles);
    }

    private static ChatMode ParseChatMode(string s) => s.ToLowerInvariant() switch
    {
        "plan" => ChatMode.Plan,
        "agent" => ChatMode.Agent,
        _ => ChatMode.Ask,
    };

    private async Task SendLocalDemoAsync(string input)
    {
        SampleBubbles.Add(new ConversationBubbleVm
        {
            Role = ConversationRole.Assistant,
            ThinkingBlock = $"{FormatMode(SelectedMode)} 模式本地预览\n输入长度：{input.Length}",
            Content = BuildLocalDemoResponse(input),
            Timestamp = DateTime.Now,
        });
        await Task.Yield();
        IsStreaming = false;
    }

    // 保留 BuildLocalDemoResponse / FormatMode / SeedSamples 原方法不变

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _emitter?.Stop();
    }
}
```

加 using：
```csharp
using System.Threading;
using TM.Framework.UI.Workspace.RightPanel.Modes;
using Tianming.Desktop.Avalonia.Infrastructure;
```

- [ ] **Step 4.5：跑 test 确认绿**

```bash
dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter "ConversationPanelViewModelTests" --nologo -v minimal
```

Expected: PASS 全部（旧 7 条 + 新 1 条）。

- [ ] **Step 4.6：commit**

```bash
git add src/Tianming.AI/SemanticKernel/Conversation/IConversationOrchestrator.cs \
        src/Tianming.AI/SemanticKernel/Conversation/ConversationOrchestrator.cs \
        src/Tianming.Desktop.Avalonia/ViewModels/Conversation/ConversationPanelViewModel.cs \
        tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Conversation/ConversationPanelViewModelTests.cs
git commit -m "feat(conversation): M4.5+.4 ConversationPanelVM 接真 Orchestrator + IConversationOrchestrator 抽象"
```

---

## Task 5：RightConversationViewModel 切到真 Orchestrator

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/ViewModels/Shell/RightConversationViewModel.cs`
- Modify: `src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs`

- [ ] **Step 5.1：把 RightConversationViewModel 改为消费 DI**

替换 `RightConversationViewModel.cs`：

```csharp
using TM.Services.Framework.AI.SemanticKernel.Conversation;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.ViewModels.Conversation;

namespace Tianming.Desktop.Avalonia.ViewModels.Shell;

public sealed class RightConversationViewModel : ConversationPanelViewModel
{
    public RightConversationViewModel(
        IConversationOrchestrator orchestrator,
        IFileSessionStore sessionStore,
        IDispatcherScheduler scheduler)
        : base(orchestrator, sessionStore, scheduler, seedSamples: true)
    {
    }
}
```

- [ ] **Step 5.2：修复 DI 注册（已是 Singleton 注册保留）**

确认 `AvaloniaShellServiceCollectionExtensions.cs` 中 `s.AddSingleton<RightConversationViewModel>()` 行未改（ctor 参数现在能从 DI 解析）。

- [ ] **Step 5.3：build + test**

```bash
dotnet build src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj --nologo -v minimal
dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --nologo -v minimal 2>&1 | tail -8
```

Expected: 全过。

- [ ] **Step 5.4：commit**

```bash
git add src/Tianming.Desktop.Avalonia/ViewModels/Shell/RightConversationViewModel.cs
git commit -m "feat(conversation): M4.5+.5 RightConversationVM 接真 Orchestrator"
```

---

## Task 6：ChatStreamView 控件（流式输出渲染）

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Controls/ChatStreamView.axaml`
- Create: `src/Tianming.Desktop.Avalonia/Controls/ChatStreamView.axaml.cs`

- [ ] **Step 6.1：写 ChatStreamView.axaml**

创建 `src/Tianming.Desktop.Avalonia/Controls/ChatStreamView.axaml`：

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="using:Tianming.Desktop.Avalonia.Controls"
             x:Class="Tianming.Desktop.Avalonia.Controls.ChatStreamView">
  <ScrollViewer Name="StreamScroll" HorizontalScrollBarVisibility="Disabled">
    <ItemsControl Name="Items" ItemsSource="{Binding Bubbles, RelativeSource={RelativeSource AncestorType=controls:ChatStreamView}}">
      <ItemsControl.ItemTemplate>
        <DataTemplate>
          <controls:ConversationBubble Role="{Binding Role}"
                                       ContentText="{Binding Content}"
                                       ThinkingBlock="{Binding ThinkingBlock}"
                                       Timestamp="{Binding Timestamp}"
                                       Margin="0,4"/>
        </DataTemplate>
      </ItemsControl.ItemTemplate>
    </ItemsControl>
  </ScrollViewer>
</UserControl>
```

- [ ] **Step 6.2：写 ChatStreamView.axaml.cs（含 Bubbles 依赖属性 + 自动滚底）**

创建 `src/Tianming.Desktop.Avalonia/Controls/ChatStreamView.axaml.cs`：

```csharp
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Tianming.Desktop.Avalonia.Controls;

public partial class ChatStreamView : UserControl
{
    public static readonly StyledProperty<ObservableCollection<ConversationBubbleVm>?> BubblesProperty =
        AvaloniaProperty.Register<ChatStreamView, ObservableCollection<ConversationBubbleVm>?>(nameof(Bubbles));

    public ObservableCollection<ConversationBubbleVm>? Bubbles
    {
        get => GetValue(BubblesProperty);
        set => SetValue(BubblesProperty, value);
    }

    public ChatStreamView()
    {
        InitializeComponent();
        this.GetObservable(BubblesProperty).Subscribe(b =>
        {
            if (b == null) return;
            b.CollectionChanged += OnBubblesChanged;
        });
    }

    private void OnBubblesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;
        Dispatcher.UIThread.Post(() =>
        {
            var sv = this.FindControl<ScrollViewer>("StreamScroll");
            sv?.ScrollToEnd();
        }, DispatcherPriority.Background);
    }
}
```

- [ ] **Step 6.3：build**

```bash
dotnet build src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj --nologo -v minimal
```

Expected: 0 Warning 0 Error.

- [ ] **Step 6.4：commit**

```bash
git add src/Tianming.Desktop.Avalonia/Controls/ChatStreamView.axaml \
        src/Tianming.Desktop.Avalonia/Controls/ChatStreamView.axaml.cs
git commit -m "feat(conversation): M4.5+.6 ChatStreamView 控件（流式 ItemsControl + 自动滚底）"
```

---

## Task 7：PlanStepListView 控件

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Controls/PlanStepListView.axaml`
- Create: `src/Tianming.Desktop.Avalonia/Controls/PlanStepListView.axaml.cs`
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/Conversation/PlanStepVm.cs`

- [ ] **Step 7.1：写 PlanStepVm**

创建 `src/Tianming.Desktop.Avalonia/ViewModels/Conversation/PlanStepVm.cs`：

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tianming.Desktop.Avalonia.ViewModels.Conversation;

public partial class PlanStepVm : ObservableObject
{
    [ObservableProperty] private int _index;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _detail = string.Empty;
    [ObservableProperty] private PlanStepStatus _status = PlanStepStatus.Pending;
    [ObservableProperty] private bool _isExpanded;
}

public enum PlanStepStatus { Pending, Running, Done, Failed }
```

- [ ] **Step 7.2：写 PlanStepListView.axaml**

创建 `src/Tianming.Desktop.Avalonia/Controls/PlanStepListView.axaml`：

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Tianming.Desktop.Avalonia.ViewModels.Conversation"
             x:Class="Tianming.Desktop.Avalonia.Controls.PlanStepListView">
  <ItemsControl Name="Items" ItemsSource="{Binding Steps, RelativeSource={RelativeSource AncestorType=UserControl}}">
    <ItemsControl.ItemTemplate>
      <DataTemplate DataType="vm:PlanStepVm">
        <Border Padding="8" Margin="0,2"
                Background="{DynamicResource SurfaceCanvasBrush}"
                CornerRadius="{DynamicResource RadiusSm}">
          <StackPanel Orientation="Horizontal" Spacing="8">
            <TextBlock Text="{Binding Status, Converter={x:Static vm:PlanStepStatusIconConverter.Instance}}"
                       Width="20"
                       FontSize="{DynamicResource FontSizeBody}"
                       VerticalAlignment="Center"/>
            <TextBlock Text="{Binding Index, StringFormat='{}{0}.'}"
                       FontWeight="{DynamicResource FontWeightSemibold}"
                       VerticalAlignment="Center"/>
            <TextBlock Text="{Binding Title}"
                       Foreground="{DynamicResource TextPrimaryBrush}"
                       VerticalAlignment="Center"/>
          </StackPanel>
        </Border>
      </DataTemplate>
    </ItemsControl.ItemTemplate>
  </ItemsControl>
</UserControl>
```

- [ ] **Step 7.3：写 PlanStepListView.axaml.cs + Converter**

创建 `src/Tianming.Desktop.Avalonia/Controls/PlanStepListView.axaml.cs`：

```csharp
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Tianming.Desktop.Avalonia.ViewModels.Conversation;

namespace Tianming.Desktop.Avalonia.Controls;

public partial class PlanStepListView : UserControl
{
    public static readonly StyledProperty<ObservableCollection<PlanStepVm>?> StepsProperty =
        AvaloniaProperty.Register<PlanStepListView, ObservableCollection<PlanStepVm>?>(nameof(Steps));

    public ObservableCollection<PlanStepVm>? Steps
    {
        get => GetValue(StepsProperty);
        set => SetValue(StepsProperty, value);
    }

    public PlanStepListView() => InitializeComponent();
}
```

在 `PlanStepVm.cs` 同文件追加：

```csharp
public sealed class PlanStepStatusIconConverter : Avalonia.Data.Converters.IValueConverter
{
    public static readonly PlanStepStatusIconConverter Instance = new();
    public object? Convert(object? value, System.Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        value switch
        {
            PlanStepStatus.Pending => "○",
            PlanStepStatus.Running => "◉",
            PlanStepStatus.Done => "✓",
            PlanStepStatus.Failed => "✗",
            _ => "○",
        };
    public object? ConvertBack(object? value, System.Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new System.NotSupportedException();
}
```

- [ ] **Step 7.4：build + commit**

```bash
dotnet build src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj --nologo -v minimal
git add src/Tianming.Desktop.Avalonia/ViewModels/Conversation/PlanStepVm.cs \
        src/Tianming.Desktop.Avalonia/Controls/PlanStepListView.axaml \
        src/Tianming.Desktop.Avalonia/Controls/PlanStepListView.axaml.cs
git commit -m "feat(conversation): M4.5+.7 PlanStepListView 控件 + PlanStepVm"
```

---

## Task 8：ConversationHistoryDrawer + 历史命令

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Controls/ConversationHistoryDrawer.axaml`
- Create: `src/Tianming.Desktop.Avalonia/Controls/ConversationHistoryDrawer.axaml.cs`
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/Conversation/SessionListItemVm.cs`
- Modify: `src/Tianming.Desktop.Avalonia/ViewModels/Conversation/ConversationPanelViewModel.cs`

- [ ] **Step 8.1：写 SessionListItemVm**

```csharp
using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tianming.Desktop.Avalonia.ViewModels.Conversation;

public partial class SessionListItemVm : ObservableObject
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private DateTime _updatedAt;
    [ObservableProperty] private int _messageCount;
}
```

- [ ] **Step 8.2：写 ConversationHistoryDrawer.axaml**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Tianming.Desktop.Avalonia.ViewModels.Conversation"
             x:Class="Tianming.Desktop.Avalonia.Controls.ConversationHistoryDrawer">
  <Border Background="{DynamicResource SurfacePanelBrush}"
          Padding="12"
          CornerRadius="{DynamicResource RadiusMd}">
    <DockPanel>
      <TextBlock DockPanel.Dock="Top"
                 Text="会话历史"
                 FontSize="{DynamicResource FontSizeH3}"
                 FontWeight="{DynamicResource FontWeightSemibold}"
                 Margin="0,0,0,8"/>
      <ListBox Name="SessionList"
               ItemsSource="{Binding Sessions, RelativeSource={RelativeSource AncestorType=UserControl}}"
               SelectedItem="{Binding SelectedSession, RelativeSource={RelativeSource AncestorType=UserControl}, Mode=TwoWay}"
               Background="Transparent"
               BorderThickness="0">
        <ListBox.ItemTemplate>
          <DataTemplate DataType="vm:SessionListItemVm">
            <StackPanel Margin="0,4" Spacing="2">
              <TextBlock Text="{Binding Title}" Foreground="{DynamicResource TextPrimaryBrush}"/>
              <TextBlock Text="{Binding UpdatedAt, StringFormat='{}{0:yyyy-MM-dd HH:mm}'}"
                         FontSize="{DynamicResource FontSizeCaption}"
                         Foreground="{DynamicResource TextTertiaryBrush}"/>
            </StackPanel>
          </DataTemplate>
        </ListBox.ItemTemplate>
      </ListBox>
    </DockPanel>
  </Border>
</UserControl>
```

- [ ] **Step 8.3：写 ConversationHistoryDrawer.axaml.cs**

```csharp
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Tianming.Desktop.Avalonia.ViewModels.Conversation;

namespace Tianming.Desktop.Avalonia.Controls;

public partial class ConversationHistoryDrawer : UserControl
{
    public static readonly StyledProperty<ObservableCollection<SessionListItemVm>?> SessionsProperty =
        AvaloniaProperty.Register<ConversationHistoryDrawer, ObservableCollection<SessionListItemVm>?>(nameof(Sessions));
    public static readonly StyledProperty<SessionListItemVm?> SelectedSessionProperty =
        AvaloniaProperty.Register<ConversationHistoryDrawer, SessionListItemVm?>(nameof(SelectedSession), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public ObservableCollection<SessionListItemVm>? Sessions
    {
        get => GetValue(SessionsProperty);
        set => SetValue(SessionsProperty, value);
    }
    public SessionListItemVm? SelectedSession
    {
        get => GetValue(SelectedSessionProperty);
        set => SetValue(SelectedSessionProperty, value);
    }

    public ConversationHistoryDrawer() => InitializeComponent();
}
```

- [ ] **Step 8.4：ConversationPanelViewModel 加历史命令**

在 `ConversationPanelViewModel` 加：

```csharp
public ObservableCollection<SessionListItemVm> SessionHistory { get; } = new();
[ObservableProperty] private SessionListItemVm? _selectedHistoryItem;
[ObservableProperty] private bool _isHistoryOpen;

[RelayCommand]
private async Task LoadHistoryAsync()
{
    if (_sessionStore == null) return;
    var list = await _sessionStore.ListSessionsAsync();
    SessionHistory.Clear();
    foreach (var s in list)
    {
        SessionHistory.Add(new SessionListItemVm
        {
            Id = s.Id,
            Title = string.IsNullOrEmpty(s.Title) ? "（未命名会话）" : s.Title,
            UpdatedAt = s.UpdatedAt,
            MessageCount = s.MessageCount,
        });
    }
    IsHistoryOpen = true;
}

[RelayCommand]
private async Task LoadSessionAsync(string sessionId)
{
    if (_orchestrator == null || _sessionStore == null) return;
    _cts?.Cancel();
    var session = await _sessionStore.LoadSessionAsync(sessionId);
    if (session == null) return;
    _currentSession = session;
    SampleBubbles.Clear();
    foreach (var msg in session.History)
    {
        SampleBubbles.Add(new ConversationBubbleVm
        {
            Role = msg.Role == ConversationRole.User ? ConversationRole.User : ConversationRole.Assistant,
            Content = msg.Content,
            Timestamp = msg.Timestamp,
        });
    }
    IsHistoryOpen = false;
}

[RelayCommand]
private async Task DeleteSessionAsync(string sessionId)
{
    if (_sessionStore == null) return;
    await _sessionStore.DeleteSessionAsync(sessionId);
    var item = SessionHistory.FirstOrDefault(s => s.Id == sessionId);
    if (item != null) SessionHistory.Remove(item);
}
```

加 using：`using System.Linq;`、`using TM.Services.Framework.AI.SemanticKernel.Conversation.Models;`。

- [ ] **Step 8.5：build + commit**

```bash
dotnet build src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj --nologo -v minimal
git add src/Tianming.Desktop.Avalonia/Controls/ConversationHistoryDrawer.axaml \
        src/Tianming.Desktop.Avalonia/Controls/ConversationHistoryDrawer.axaml.cs \
        src/Tianming.Desktop.Avalonia/ViewModels/Conversation/SessionListItemVm.cs \
        src/Tianming.Desktop.Avalonia/ViewModels/Conversation/ConversationPanelViewModel.cs
git commit -m "feat(conversation): M4.5+.8 ConversationHistoryDrawer + Load/Delete 命令"
```

---

## Task 9：ReferenceDropdown + @query 引用扩展

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Controls/ReferenceDropdown.axaml`
- Create: `src/Tianming.Desktop.Avalonia/Controls/ReferenceDropdown.axaml.cs`
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/Conversation/ReferenceItemVm.cs`
- Modify: `src/Tianming.Desktop.Avalonia/ViewModels/Conversation/ConversationPanelViewModel.cs`

- [ ] **Step 9.1：写 ReferenceItemVm**

```csharp
namespace Tianming.Desktop.Avalonia.ViewModels.Conversation;

public sealed class ReferenceItemVm
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty; // "Character" / "World" / "Chapter" ...
}
```

- [ ] **Step 9.2：ConversationPanelViewModel 加 @ 监听**

加：

```csharp
public ObservableCollection<ReferenceItemVm> ReferenceCandidates { get; } = new();
[ObservableProperty] private bool _isReferencePopupOpen;

partial void OnInputDraftChanged(string value)
{
    var idx = value.LastIndexOf('@');
    if (idx < 0 || idx == value.Length - 1)
    {
        IsReferencePopupOpen = false;
        return;
    }
    var query = value[(idx + 1)..];
    if (string.IsNullOrWhiteSpace(query))
    {
        IsReferencePopupOpen = false;
        return;
    }
    PopulateReferenceCandidates(query);
}

private void PopulateReferenceCandidates(string query)
{
    ReferenceCandidates.Clear();
    // M4.5+ 简化：固定示例集；M6 接 SearchReferencesTool
    var samples = new[]
    {
        new ReferenceItemVm { Id = "ch-001", Name = "第 1 章 风起青萍", Category = "Chapter" },
        new ReferenceItemVm { Id = "char-zhuge", Name = "诸葛清", Category = "Character" },
        new ReferenceItemVm { Id = "world-jiuzhou", Name = "九州大陆", Category = "World" },
    };
    foreach (var s in samples)
        if (s.Name.Contains(query, System.StringComparison.OrdinalIgnoreCase))
            ReferenceCandidates.Add(s);
    IsReferencePopupOpen = ReferenceCandidates.Count > 0;
}

[RelayCommand]
private void SelectReference(ReferenceItemVm? item)
{
    if (item == null) return;
    var idx = InputDraft.LastIndexOf('@');
    if (idx < 0) return;
    InputDraft = InputDraft[..idx] + $"@{item.Name} ";
    IsReferencePopupOpen = false;
}
```

- [ ] **Step 9.3：写 ReferenceDropdown.axaml**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Tianming.Desktop.Avalonia.ViewModels.Conversation"
             x:Class="Tianming.Desktop.Avalonia.Controls.ReferenceDropdown">
  <Popup Name="Pop"
         IsOpen="{Binding IsOpen, RelativeSource={RelativeSource AncestorType=UserControl}}"
         Placement="Top">
    <Border Background="{DynamicResource SurfacePanelBrush}"
            Padding="6"
            CornerRadius="{DynamicResource RadiusMd}">
      <ListBox ItemsSource="{Binding Items, RelativeSource={RelativeSource AncestorType=UserControl}}"
               SelectedItem="{Binding SelectedItem, RelativeSource={RelativeSource AncestorType=UserControl}, Mode=TwoWay}"
               Background="Transparent">
        <ListBox.ItemTemplate>
          <DataTemplate DataType="vm:ReferenceItemVm">
            <StackPanel Orientation="Horizontal" Spacing="6">
              <TextBlock Text="{Binding Category}" Foreground="{DynamicResource TextTertiaryBrush}" Width="60"/>
              <TextBlock Text="{Binding Name}" Foreground="{DynamicResource TextPrimaryBrush}"/>
            </StackPanel>
          </DataTemplate>
        </ListBox.ItemTemplate>
      </ListBox>
    </Border>
  </Popup>
</UserControl>
```

- [ ] **Step 9.4：写 ReferenceDropdown.axaml.cs**

```csharp
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Tianming.Desktop.Avalonia.ViewModels.Conversation;

namespace Tianming.Desktop.Avalonia.Controls;

public partial class ReferenceDropdown : UserControl
{
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ReferenceDropdown, bool>(nameof(IsOpen));
    public static readonly StyledProperty<ObservableCollection<ReferenceItemVm>?> ItemsProperty =
        AvaloniaProperty.Register<ReferenceDropdown, ObservableCollection<ReferenceItemVm>?>(nameof(Items));
    public static readonly StyledProperty<ReferenceItemVm?> SelectedItemProperty =
        AvaloniaProperty.Register<ReferenceDropdown, ReferenceItemVm?>(nameof(SelectedItem), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public ObservableCollection<ReferenceItemVm>? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    public ReferenceItemVm? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }

    public ReferenceDropdown() => InitializeComponent();
}
```

- [ ] **Step 9.5：build + commit**

```bash
dotnet build src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj --nologo -v minimal
git add src/Tianming.Desktop.Avalonia/Controls/ReferenceDropdown.axaml \
        src/Tianming.Desktop.Avalonia/Controls/ReferenceDropdown.axaml.cs \
        src/Tianming.Desktop.Avalonia/ViewModels/Conversation/ReferenceItemVm.cs \
        src/Tianming.Desktop.Avalonia/ViewModels/Conversation/ConversationPanelViewModel.cs
git commit -m "feat(conversation): M4.5+.9 ReferenceDropdown + @query 引用扩展"
```

---

## Task 10：ConversationPanelView 主 View 替换 RightConversationView 内容

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Views/Conversation/ConversationPanelView.axaml`
- Create: `src/Tianming.Desktop.Avalonia/Views/Conversation/ConversationPanelView.axaml.cs`
- Modify: `src/Tianming.Desktop.Avalonia/Views/Shell/RightConversationView.axaml`
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml`（加 DataTemplate）

- [ ] **Step 10.1：写 ConversationPanelView.axaml**

创建 `src/Tianming.Desktop.Avalonia/Views/Conversation/ConversationPanelView.axaml`：

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="using:Tianming.Desktop.Avalonia.Controls"
             xmlns:vm="using:Tianming.Desktop.Avalonia.ViewModels.Conversation"
             x:Class="Tianming.Desktop.Avalonia.Views.Conversation.ConversationPanelView"
             x:DataType="vm:ConversationPanelViewModel">
  <DockPanel LastChildFill="True">
    <!-- 顶部：模式切换 + 新会话 + 历史 -->
    <Border DockPanel.Dock="Top" Padding="8" Background="{DynamicResource SurfacePanelBrush}">
      <Grid ColumnDefinitions="*, Auto, Auto">
        <controls:SegmentedTabs Grid.Column="0"
                                ItemsSource="{Binding ModeSegments}"
                                SelectedKey="{Binding SelectedMode, Mode=TwoWay}"/>
        <Button Grid.Column="1" Classes="ghost" Content="历史"
                Command="{Binding LoadHistoryCommand}"/>
        <Button Grid.Column="2" Classes="ghost" Content="新会话" Margin="6,0,0,0"
                Command="{Binding NewSessionCommand}"/>
      </Grid>
    </Border>

    <!-- 底部：输入区 -->
    <Border DockPanel.Dock="Bottom" Padding="8" Background="{DynamicResource SurfacePanelBrush}">
      <Grid RowDefinitions="*, Auto">
        <TextBox Grid.Row="0"
                 Text="{Binding InputDraft, Mode=TwoWay}"
                 Watermark="输入消息，@ 可引用项目数据"
                 AcceptsReturn="True" MinHeight="60"/>
        <controls:ReferenceDropdown Grid.Row="0"
                                    IsOpen="{Binding IsReferencePopupOpen}"
                                    Items="{Binding ReferenceCandidates}"
                                    SelectedItem="{Binding SelectedReferenceItem, Mode=TwoWay}"/>
        <Button Grid.Row="1" Classes="primary" Content="发送" Margin="0,6,0,0"
                HorizontalAlignment="Right"
                Command="{Binding SendCommand}"
                IsEnabled="{Binding !IsStreaming}"/>
      </Grid>
    </Border>

    <!-- 中部：消息流 + 历史抽屉（覆盖式） -->
    <Grid>
      <controls:ChatStreamView Bubbles="{Binding SampleBubbles}"/>
      <controls:ConversationHistoryDrawer
          IsVisible="{Binding IsHistoryOpen}"
          Sessions="{Binding SessionHistory}"
          HorizontalAlignment="Right" Width="240"/>
    </Grid>
  </DockPanel>
</UserControl>
```

- [ ] **Step 10.2：写 ConversationPanelView.axaml.cs**

```csharp
using Avalonia.Controls;

namespace Tianming.Desktop.Avalonia.Views.Conversation;

public partial class ConversationPanelView : UserControl
{
    public ConversationPanelView() => InitializeComponent();
}
```

注意：还需为 `ConversationPanelViewModel` 加属性：`SelectedReferenceItem`（partial OnChanged 调用 SelectReferenceCommand）：

```csharp
[ObservableProperty] private ReferenceItemVm? _selectedReferenceItem;
partial void OnSelectedReferenceItemChanged(ReferenceItemVm? value)
{
    if (value != null) SelectReferenceCommand.Execute(value);
}
```

- [ ] **Step 10.3：App.axaml 加 DataTemplate**

在 `App.axaml` `<Application.DataTemplates>` 加：

```xml
<DataTemplate DataType="vc:ConversationPanelViewModel">
  <vcv:ConversationPanelView/>
</DataTemplate>
```

顶部加 xmlns：
```xml
xmlns:vc="using:Tianming.Desktop.Avalonia.ViewModels.Conversation"
xmlns:vcv="using:Tianming.Desktop.Avalonia.Views.Conversation"
```

- [ ] **Step 10.4：替换 RightConversationView 内容为 ConversationPanelView**

修改 `src/Tianming.Desktop.Avalonia/Views/Shell/RightConversationView.axaml` 的 ItemsControl/StackPanel 整段替换为：

```xml
<vcv:ConversationPanelView />
```

并在顶部加 xmlns：
```xml
xmlns:vcv="using:Tianming.Desktop.Avalonia.Views.Conversation"
```

- [ ] **Step 10.5：build + 启动 demo（手工验证）**

```bash
dotnet build src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj --nologo -v minimal
dotnet run --project src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj
```

Expected: 右栏出现新对话面板（Ask/Plan/Agent 切换 + 输入框 + 发送按钮 + 历史按钮）。

- [ ] **Step 10.6：commit**

```bash
git add src/Tianming.Desktop.Avalonia/Views/Conversation/ConversationPanelView.axaml \
        src/Tianming.Desktop.Avalonia/Views/Conversation/ConversationPanelView.axaml.cs \
        src/Tianming.Desktop.Avalonia/Views/Shell/RightConversationView.axaml \
        src/Tianming.Desktop.Avalonia/App.axaml \
        src/Tianming.Desktop.Avalonia/ViewModels/Conversation/ConversationPanelViewModel.cs
git commit -m "feat(conversation): M4.5+.10 ConversationPanelView + RightConversationView 替换"
```

---

## Task 11：全量 build + test + 收尾

- [ ] **Step 11.1：全量 build**

```bash
dotnet build Tianming.MacMigration.sln --nologo -v minimal
```

Expected: 0 Warning(s) 0 Error(s).

- [ ] **Step 11.2：全量 test**

```bash
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -10
```

Expected: 所有项目全过。

- [ ] **Step 11.3：人工验收 checklist**

启动应用：`dotnet run --project src/Tianming.Desktop.Avalonia`

- [ ] 右栏看到 Ask/Plan/Agent 三按钮，切换无 crash
- [ ] 输入"hello"→发送→看到用户气泡 + 助手气泡（如配置了 AI key，会真流式；否则本地预览）
- [ ] 点"历史"→抽屉滑出，显示已保存会话（FileSessionStore 持久化生效）
- [ ] 输入"@九"→出现 ReferenceDropdown 候选
- [ ] 点候选→输入框插入 `@九州大陆 `
- [ ] 点"新会话"→消息清空

---

## M4.5+ Gate 验收

| 项 | 标准 |
|---|---|
| DI 注册 | 7 个 AI 类（Orchestrator/Tools/SessionStore/Mappers）能 resolve |
| BulkEmitter | 16ms timer 批量 flush，单测 4/4 绿 |
| 真 Orchestrator | ConversationPanelVM 接 IConversationOrchestrator |
| 5 控件 | ChatStreamView / PlanStepListView / ConversationHistoryDrawer / ReferenceDropdown / ConversationPanelView 全部建好 |
| 历史抽屉 | LoadHistory / LoadSession / DeleteSession 命令 |
| @query | OnInputDraftChanged 触发 ReferenceDropdown |
| 集成 | RightConversationView 切到 ConversationPanelView |
| 全量 test | dotnet test 全过 |
