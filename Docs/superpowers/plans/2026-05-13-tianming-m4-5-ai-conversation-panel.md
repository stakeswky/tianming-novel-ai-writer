# M4.5 Step-Level Plan: AI 对话面板

> 右栏实装：ConversationOrchestrator + Ask/Plan/Agent 三模式 + 流式输出 + 会话历史
> 工作量：3 天 | 不依赖 M4.4

---

## Step M4.5.1：ChatStreamDelta 类型 + ConversationSession

**Files:**
- Create: `src/Tianming.AI/SemanticKernel/Conversation/ChatStreamDelta.cs`
- Create: `src/Tianming.AI/SemanticKernel/Conversation/ConversationSession.cs`

**ChatStreamDelta.cs:**
```csharp
namespace TM.Services.Framework.AI.SemanticKernel.Conversation;

public abstract record ChatStreamDelta;
public sealed record ThinkingDelta(string Text) : ChatStreamDelta;
public sealed record AnswerDelta(string Text) : ChatStreamDelta;
public sealed record ToolCallDelta(string ToolCallId, string ToolName, string ArgumentsJson) : ChatStreamDelta;
public sealed record ToolResultDelta(string ToolCallId, string ResultText) : ChatStreamDelta;
public sealed record PlanStepDelta(PlanStep Step) : ChatStreamDelta;
```

**ConversationSession.cs:**
```csharp
namespace TM.Services.Framework.AI.SemanticKernel.Conversation;

public sealed class ConversationSession
{
    public string Id { get; } = ShortIdGenerator.New();
    public ChatMode Mode { get; set; }
    public string? Title { get; set; }
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public List<ConversationMessage> History { get; } = new();

    // Current streaming state
    public string? CurrentThinking { get; set; }
    public string? CurrentAnswer { get; set; }
    public List<PlanStep> CurrentPlanSteps { get; } = new();
    public List<ToolCallRecord> CurrentToolCalls { get; } = new();

    public void ClearStreamingState() { CurrentThinking = null; CurrentAnswer = null; CurrentPlanSteps.Clear(); CurrentToolCalls.Clear(); }
}
```

**Commit:** `feat(ai): M4.5.1 ChatStreamDelta + ConversationSession types`

---

## Step M4.5.2：IConversationTool 接口 + 3 内置工具

**Files:**
- Create: `src/Tianming.AI/SemanticKernel/Conversation/IConversationTool.cs`
- Create: `src/Tianming.AI/SemanticKernel/Conversation/Tools/LookupDataTool.cs`
- Create: `src/Tianming.AI/SemanticKernel/Conversation/Tools/ReadChapterTool.cs`
- Create: `src/Tianming.AI/SemanticKernel/Conversation/Tools/SearchReferencesTool.cs`

**IConversationTool.cs:**
```csharp
public interface IConversationTool
{
    string Name { get; }
    string Description { get; }
    string ParameterSchemaJson { get; }  // JSON Schema string
    Task<string> InvokeAsync(IReadOnlyDictionary<string, object?> args, CancellationToken ct);
}
```

**3 tools:**
- `LookupDataTool` — 查询项目设计数据（角色/世界观/势力等），读取对应的 JSON 文件目录
- `ReadChapterTool` — 读取指定章节内容，从 `<project>/Generate/Chapters/` 读 .md 文件
- `SearchReferencesTool` — 搜索可引用项，遍历设计模块数据

每个工具的实现：接收参数 dict，读取文件系统上的 JSON/.md 文件，返回结果文本。

**Commit:** `feat(ai): M4.5.2 IConversationTool + 3 built-in tools`

---

## Step M4.5.3：ConversationOrchestrator 核心

**Files:**
- Create: `src/Tianming.AI/SemanticKernel/Conversation/ConversationOrchestrator.cs`
- Create: `tests/Tianming.AI.Tests/Conversation/ConversationOrchestratorTests.cs`

**ConversationOrchestrator 核心流程:**

```csharp
public sealed class ConversationOrchestrator
{
    private readonly OpenAICompatibleChatClient _chat;
    private readonly TagBasedThinkingStrategy _thinking;
    private readonly IFileSessionStore _sessions;
    private readonly IReadOnlyList<IConversationTool> _tools;
    private readonly AskModeMapper _askMapper;
    private readonly PlanModeMapper _planMapper;
    private readonly AgentModeMapper _agentMapper;
    private readonly ConversationModeProfileCatalog _profiles;

    public async Task<ConversationSession> StartSessionAsync(ChatMode mode, string? sessionId = null, CancellationToken ct = default);
    public IAsyncEnumerable<ChatStreamDelta> SendAsync(ConversationSession session, string userInput, CancellationToken ct = default);
    public async Task PersistAsync(ConversationSession session, CancellationToken ct = default);
}
```

**SendAsync 流程:**
1. 按 mode 从 `_profiles` 取 profile，构建 system message
2. 把 `userInput` + session history 转为 OpenAI-compatible message 列表
3. **Ask/Plan:** `_chat.StreamAsync()` → 对每个 chunk 用 `_thinking.Extract()` 拆 thinking/answer → yield `ThinkingDelta` / `AnswerDelta`
   - Plan 模式额外：answer 累积完后用 `PlanStepParser.Parse()` 得到步骤 → yield `PlanStepDelta`
4. **Agent:** 循环：
   - `_chat.StreamAsync()` 带 tools 声明
   - 检测 `tool_calls` in response → 对每个 tool_call 找对应 `IConversationTool` → `InvokeAsync()`
   - 把 tool 结果加回 history → 继续流 → yield `ToolCallDelta` / `ToolResultDelta` / `AnswerDelta`
5. 累积结果到 `session.History`

**测试（≥8 条）:**
1. Ask happy path → ThinkingDelta + AnswerDelta
2. Plan happy path → PlanStepDelta(s)
3. Agent tool call → ToolCallDelta + ToolResultDelta + AnswerDelta
4. Plan 模式 payload 无 tools 字段
5. 取消 token → OperationCanceledException
6. Session 恢复 → history loaded
7. Persist → round-trip 一致性
8. 空输入 → 不发消息

**Commit:** 2-3 个（orchestrator + tools + tests）

---

## Step M4.5.4：ConversationPanelViewModel

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/Conversation/ConversationPanelViewModel.cs`
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/Conversation/ChatMessageViewModel.cs`
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/Conversation/BulkEmitter.cs`

**ChatMessageViewModel:**
- `abstract class ChatMessageViewModel : ObservableObject`
- `UserChatMessageVM : ChatMessageViewModel` — `string Content`, `DateTime Timestamp`
- `AssistantChatMessageVM : ChatMessageViewModel` — `string Content`, `string? ThinkingBlock`, `bool IsThinkingExpanded`, `ObservableCollection<ToolCallCardVM> ToolCalls`, `bool IsStreaming`
- `ToolCallCardVM : ObservableObject` — `string ToolName`, `string Arguments`, `string Result`, `ToolCallState State`, `double? DurationMs`

**BulkEmitter:**
- 每 16ms 批量 flush `ChatStreamDelta` 到 `ObservableCollection<ChatMessageViewModel>`
- 内部 timer + queue，tick 时 dequeue 并更新/创建 message VM

**ConversationPanelViewModel:**
- 注入: `ConversationOrchestrator`, `IFileSessionStore`, `INavigationService`
- `[ObservableProperty] string _selectedMode = "ask"`
- `[ObservableProperty] string _inputText`
- `ObservableCollection<ChatMessageViewModel> Messages`
- `[ObservableProperty] ConversationSession? _currentSession`
- `[ObservableProperty] bool _isLoading`
- `[RelayCommand] async Task SendAsync()` — 构建 user message → `Orchestrator.SendAsync()` → BulkEmitter 消费 deltas → `Orchestrator.PersistAsync()`
- `[RelayCommand] NewSession()` — 清空 Messages，新 Session
- `[RelayCommand] async Task LoadSessionAsync(string sessionId)`
- `[RelayCommand] ToggleMode(string mode)` — 切 Ask/Plan/Agent

**Commit:** `feat(conversation): M4.5.4 ConversationPanelViewModel + ChatMessageVM + BulkEmitter`

---

## Step M4.5.5：ChatStreamView + 消息卡片控件

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Controls/ChatStreamView.axaml`
- Create: `src/Tianming.Desktop.Avalonia/Controls/PlanStepListView.axaml` + `.cs`
- Modify: `src/Tianming.Desktop.Avalonia/Controls/ToolCallCard.axaml`（已有，验证绑定）
- Modify: `src/Tianming.Desktop.Avalonia/Controls/ConversationBubble.axaml`（已有）

**ChatStreamView.axaml:**
- `ListBox` 绑定 `Messages`，`ItemsSource` 自动滚底（用 `ScrollViewer` + `PropertyChanged` 监听）
- DataTemplate 区分 `UserChatMessageVM` → `ConversationBubble(Role=User)` 和 `AssistantChatMessageVM` → `ConversationBubble(Role=Assistant)` + thinking 折叠 + ToolCallCard 列表

**PlanStepListView.axaml:**
- `ItemsControl` 绑定 `PlanSteps`
- 每步：`Border` + 状态图标（pending=○, running=◉, done=✓, failed=✗）+ 标题 + 可展开详情
- 用 `Expander` 或手动 toggle 展开详情

**Commit:** `feat(conversation): M4.5.5 ChatStreamView + PlanStepListView controls`

---

## Step M4.5.6：RightConversationView 替换为 ConversationPanelView

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Views/Conversation/ConversationPanelView.axaml` + `.axaml.cs`
- Modify: `src/Tianming.Desktop.Avalonia/Views/Shell/RightConversationView.axaml`（替换内部内容为 `<conv:ConversationPanelView/>`）
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml`（确认 DataTemplate 指向正确）
- Modify: `AvaloniaShellServiceCollectionExtensions.cs`（注册 ConversationOrchestrator + 工具 + VM）

**DI 注册:**
```csharp
// M4.5 AI 对话
s.AddSingleton<OpenAICompatibleChatClient>(sp =>
{
    var config = sp.GetRequiredService<FileAIConfigurationStore>();
    var active = config.GetActiveConfiguration();
    // 从 active config 构建 chat client
    return new OpenAICompatibleChatClient();
});
s.AddSingleton<TagBasedThinkingStrategy>();
s.AddSingleton<FileSessionStore>(sp =>
    new FileSessionStore(Path.Combine(sp.GetRequiredService<AppPaths>().AppSupportDirectory, "Sessions")));
s.AddSingleton<AskModeMapper>();
s.AddSingleton<PlanModeMapper>(sp => new PlanModeMapper(new PlanStepParser()));
s.AddSingleton<AgentModeMapper>();
s.AddSingleton<IConversationTool, LookupDataTool>();
s.AddSingleton<IConversationTool, ReadChapterTool>();
s.AddSingleton<IConversationTool, SearchReferencesTool>();
s.AddSingleton<ConversationOrchestrator>();
s.AddTransient<ConversationPanelViewModel>();
```

**RightConversationView 更新:**
- 保留外壳（因为 ThreeColumnLayout 引用的就是 RightConversationView）
- 内部替换为 `ConversationPanelView`，DataContext 传递

**ConversationPanelView.axaml:**
- 顶部：SegmentedTabs (Ask/Plan/Agent) + 新会话/历史按钮
- 中间：ChatStreamView
- 底部：输入框 + 发送按钮
- 侧抽屉：会话历史列表

**Commit:** `feat(conversation): M4.5.6 替换 RightConversationView + DI 注册`

---

## Step M4.5.7：会话历史抽屉

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Controls/ConversationHistoryDrawer.axaml` + VM

**ConversationHistoryDrawer:**
- `ListBox` 列出所有 session（从 `FileSessionStore.GetAllSessions()`）
- 按时间倒序，显示标题/时间
- 点击 → 加载到当前面板
- 右键/按钮 → 删除会话

**ConversationPanelViewModel 新增:**
- `ObservableCollection<SessionListItem> SessionHistory`
- `[RelayCommand] async Task LoadHistoryAsync()` — 从 store 加载列表
- `[RelayCommand] async Task SelectSessionAsync(string sessionId)`
- `[RelayCommand] async Task DeleteSessionAsync(string sessionId)`

**Commit:** `feat(conversation): M4.5.7 会话历史抽屉`

---

## Step M4.5.8：引用扩展 — @query 触发 ReferenceDropdown

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Controls/ReferenceDropdown.axaml` + VM
- Modify: `ConversationPanelViewModel` — 加 `ObservableCollection<ReferenceItem> ReferenceCandidates`

**ReferenceDropdown:**
- Popup 或 AutoCompleteBox，绑定 `ReferenceCandidates`
- 输入 `@` 时搜索项目数据（角色/世界观/章节等）
- 选中后插入 `@name ` 到输入框

**简化实现（M4）:** 不做强 @ 解析，输入框下方放一个 ReferenceDropdown 手动选择引用，插入后显示为 chip。

**Commit:** `feat(conversation): M4.5.8 ReferenceDropdown`

---

## M4.5 Gate 验收步骤

### Ask 模式
1. 输入 "@九州大陆 的核心冲突是什么？"
2. 流式回复 thinking 块可折叠
3. answer 引用了真实项目数据
4. 关应用 → 重开 → 会话历史可加载

### Plan 模式
1. 切 Plan → "帮我规划第 5 章"
2. PlanStepListView 显示 ≥3 步骤
3. 每步有 pending/running/done 状态
4. 步骤保存到 session

### Agent 模式
1. 切 Agent → "帮我查张三在第 3 章有没有矛盾"
2. ToolCallCard 展示工具调用 + 参数 + 结果
3. 最终 answer 基于工具返回
