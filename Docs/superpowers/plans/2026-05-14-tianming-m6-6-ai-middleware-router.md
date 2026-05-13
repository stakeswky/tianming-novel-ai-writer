# M6.6 AI Middleware + 多模型路由 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 把目前"所有 AI 调用都走单一 OpenAICompatibleChatClient"的瓶颈拆开，按任务类型分流：Chat（对话用轻量模型）/ Writing（章节生成用长上下文模型）/ Polish（润色用高准确度模型）/ Validation（一致性校验用便宜准确模型）/ Embedding（已分流到 OnnxTextEmbedder）。新增 `IAIModelRouter` 接口 + `RoutingPolicy` 决策层 + middleware 拦截 4 个调用入口。

**Architecture:** UserConfiguration 扩 `Purpose` 字段（"Chat"/"Writing"/"Polish"/"Validation"/"Default"）。新增 `IAIModelRouter`：输入 `AITaskPurpose` 枚举，输出对应 `UserConfiguration`。新增 `RoutedChatClient` 装饰器包装 `OpenAICompatibleChatClient`，按 Purpose 用 Router 决定 BaseUrl/ApiKey/Model 再调底层 client。`ConversationOrchestrator` / `ConfiguredAITextGenerationService` 注入 RoutedChatClient 替代直接依赖。

**Tech Stack:** .NET 8 + 复用现有 `OpenAICompatibleChatClient` + `FileAIConfigurationStore` + xUnit。零新依赖。

**Branch:** `m6-6-ai-middleware`（基于 main）。

**前置条件：** Round 2/3 + M6.2-M6.5 已合并入 main 并基线绿。

---

## Task 0：基线 + worktree

```bash
cd /Users/jimmy/Downloads/tianming-novel-ai-writer
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -10
git worktree add /Users/jimmy/Downloads/tianming-m6-6 -b m6-6-ai-middleware main
cd /Users/jimmy/Downloads/tianming-m6-6
```

---

## Task 1：AITaskPurpose 枚举 + UserConfiguration.Purpose 字段

**Files:**
- Create: `src/Tianming.AI/Core/AITaskPurpose.cs`
- Modify: `Services/Framework/AI/Core/UserConfiguration.cs`
- Test: `tests/Tianming.AI.Tests/Core/AITaskPurposeTests.cs`

- [ ] **Step 1.1：测试**

```csharp
using System.Text.Json;
using TM.Services.Framework.AI.Core;
using Xunit;

namespace Tianming.AI.Tests.Core;

public class AITaskPurposeTests
{
    [Fact]
    public void All_five_purposes_defined()
    {
        Assert.True(System.Enum.IsDefined(typeof(AITaskPurpose), AITaskPurpose.Chat));
        Assert.True(System.Enum.IsDefined(typeof(AITaskPurpose), AITaskPurpose.Writing));
        Assert.True(System.Enum.IsDefined(typeof(AITaskPurpose), AITaskPurpose.Polish));
        Assert.True(System.Enum.IsDefined(typeof(AITaskPurpose), AITaskPurpose.Validation));
        Assert.True(System.Enum.IsDefined(typeof(AITaskPurpose), AITaskPurpose.Default));
    }

    [Fact]
    public void UserConfiguration_carries_purpose()
    {
        var cfg = new UserConfiguration { Purpose = "Writing" };
        var json = JsonSerializer.Serialize(cfg);
        Assert.Contains("Writing", json);
        var back = JsonSerializer.Deserialize<UserConfiguration>(json);
        Assert.Equal("Writing", back!.Purpose);
    }
}
```

- [ ] **Step 1.2：实现**

`AITaskPurpose.cs`:

```csharp
namespace TM.Services.Framework.AI.Core
{
    public enum AITaskPurpose
    {
        Default,
        Chat,
        Writing,
        Polish,
        Validation,
        // Embedding 不在这里（走 ITextEmbedder 独立链路）
    }
}
```

修改 `UserConfiguration.cs` 加：

```csharp
[JsonPropertyName("Purpose")]
public string Purpose { get; set; } = "Default";
```

- [ ] **Step 1.3：commit**

```bash
dotnet test tests/Tianming.AI.Tests/Tianming.AI.Tests.csproj --filter AITaskPurposeTests --nologo -v minimal
git add src/Tianming.AI/Core/AITaskPurpose.cs \
        Services/Framework/AI/Core/UserConfiguration.cs \
        tests/Tianming.AI.Tests/Core/AITaskPurposeTests.cs
git commit -m "feat(ai-router): M6.6.1 AITaskPurpose 枚举 + UserConfiguration.Purpose"
```

---

## Task 2：IAIModelRouter 接口 + DefaultAIModelRouter

**Files:**
- Create: `src/Tianming.AI/Core/Routing/IAIModelRouter.cs`
- Create: `src/Tianming.AI/Core/Routing/DefaultAIModelRouter.cs`
- Test: `tests/Tianming.AI.Tests/Core/Routing/DefaultAIModelRouterTests.cs`

- [ ] **Step 2.1：测试**

```csharp
using System.Collections.Generic;
using TM.Services.Framework.AI.Core;
using TM.Services.Framework.AI.Core.Routing;
using Xunit;

namespace Tianming.AI.Tests.Core.Routing;

public class DefaultAIModelRouterTests
{
    private static List<UserConfiguration> SampleConfigs() => new()
    {
        new() { Id = "c1", Purpose = "Chat", IsEnabled = true, ModelId = "haiku" },
        new() { Id = "c2", Purpose = "Writing", IsEnabled = true, ModelId = "opus" },
        new() { Id = "c3", Purpose = "Default", IsEnabled = true, IsActive = true, ModelId = "sonnet" },
    };

    [Fact]
    public void Routes_chat_to_chat_config()
    {
        var router = new DefaultAIModelRouter(SampleConfigs);
        var cfg = router.Resolve(AITaskPurpose.Chat);
        Assert.Equal("haiku", cfg.ModelId);
    }

    [Fact]
    public void Falls_back_to_active_default_when_purpose_not_found()
    {
        var router = new DefaultAIModelRouter(SampleConfigs);
        var cfg = router.Resolve(AITaskPurpose.Polish);
        Assert.Equal("sonnet", cfg.ModelId);
    }

    [Fact]
    public void Throws_when_no_default_and_purpose_missing()
    {
        var router = new DefaultAIModelRouter(() => new List<UserConfiguration>());
        Assert.Throws<System.InvalidOperationException>(() => router.Resolve(AITaskPurpose.Chat));
    }
}
```

- [ ] **Step 2.2：实现**

`IAIModelRouter.cs`:

```csharp
namespace TM.Services.Framework.AI.Core.Routing
{
    public interface IAIModelRouter
    {
        UserConfiguration Resolve(AITaskPurpose purpose);
    }
}
```

`DefaultAIModelRouter.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TM.Services.Framework.AI.Core.Routing
{
    public sealed class DefaultAIModelRouter : IAIModelRouter
    {
        private readonly Func<IReadOnlyList<UserConfiguration>> _configsProvider;

        public DefaultAIModelRouter(Func<IReadOnlyList<UserConfiguration>> configsProvider)
        {
            _configsProvider = configsProvider;
        }

        public UserConfiguration Resolve(AITaskPurpose purpose)
        {
            var configs = _configsProvider();
            var enabled = configs.Where(c => c.IsEnabled).ToList();

            // 1) 精确 Purpose 匹配
            var match = enabled.FirstOrDefault(c =>
                string.Equals(c.Purpose, purpose.ToString(), StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;

            // 2) IsActive 的 Default 配置
            var active = enabled.FirstOrDefault(c => c.IsActive);
            if (active != null) return active;

            throw new InvalidOperationException(
                $"No UserConfiguration matches purpose {purpose} and no active default found.");
        }
    }
}
```

- [ ] **Step 2.3：commit**

```bash
dotnet test tests/Tianming.AI.Tests/Tianming.AI.Tests.csproj --filter DefaultAIModelRouterTests --nologo -v minimal
git add src/Tianming.AI/Core/Routing/IAIModelRouter.cs \
        src/Tianming.AI/Core/Routing/DefaultAIModelRouter.cs \
        tests/Tianming.AI.Tests/Core/Routing/DefaultAIModelRouterTests.cs
git commit -m "feat(ai-router): M6.6.2 IAIModelRouter + DefaultAIModelRouter（按 Purpose 解析）"
```

---

## Task 3：RoutedChatClient 装饰器

**Files:**
- Create: `src/Tianming.AI/Core/Routing/RoutedChatClient.cs`
- Test: `tests/Tianming.AI.Tests/Core/Routing/RoutedChatClientTests.cs`

- [ ] **Step 3.1：测试**

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using TM.Services.Framework.AI.Core;
using TM.Services.Framework.AI.Core.Routing;
using Xunit;

namespace Tianming.AI.Tests.Core.Routing;

public class RoutedChatClientTests
{
    private sealed class StubRouter : IAIModelRouter
    {
        public Dictionary<AITaskPurpose, UserConfiguration> Map { get; } = new();
        public UserConfiguration Resolve(AITaskPurpose p) => Map[p];
    }

    [Fact]
    public async Task CompleteAsync_uses_Writing_config_when_purpose_is_Writing()
    {
        var router = new StubRouter();
        router.Map[AITaskPurpose.Writing] = new UserConfiguration
        {
            CustomEndpoint = "https://writing.example.com",
            ModelId = "writing-opus",
            ApiKey = "key-w",
            Temperature = 0.8,
            MaxTokens = 8192,
        };
        var inner = new OpenAICompatibleChatClient(new HttpClient());
        var client = new RoutedChatClient(inner, router);

        var captured = client.BuildRequestFor(AITaskPurpose.Writing,
            new List<OpenAICompatibleChatMessage>
            {
                new("user", "test"),
            });

        Assert.Equal("https://writing.example.com", captured.BaseUrl);
        Assert.Equal("writing-opus", captured.Model);
        Assert.Equal("key-w", captured.ApiKey);
        Assert.Equal(0.8, captured.Temperature);
    }
}
```

- [ ] **Step 3.2：实现**

```csharp
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Framework.AI.Core.Routing
{
    public sealed class RoutedChatClient
    {
        private readonly OpenAICompatibleChatClient _inner;
        private readonly IAIModelRouter _router;

        public RoutedChatClient(OpenAICompatibleChatClient inner, IAIModelRouter router)
        {
            _inner = inner;
            _router = router;
        }

        public OpenAICompatibleChatRequest BuildRequestFor(
            AITaskPurpose purpose,
            List<OpenAICompatibleChatMessage> messages,
            List<OpenAICompatibleToolDefinition>? tools = null,
            int? overrideMaxTokens = null,
            double? overrideTemperature = null)
        {
            var cfg = _router.Resolve(purpose);
            return new OpenAICompatibleChatRequest
            {
                BaseUrl = cfg.CustomEndpoint ?? string.Empty,
                ApiKey = cfg.ApiKey,
                Model = cfg.ModelId,
                Messages = messages,
                Temperature = overrideTemperature ?? cfg.Temperature,
                MaxTokens = overrideMaxTokens ?? cfg.MaxTokens,
                Tools = tools,
            };
        }

        public Task<OpenAICompatibleChatResult> CompleteAsync(
            AITaskPurpose purpose,
            List<OpenAICompatibleChatMessage> messages,
            CancellationToken ct = default)
        {
            var request = BuildRequestFor(purpose, messages);
            return _inner.CompleteAsync(request, ct);
        }

        public IAsyncEnumerable<OpenAICompatibleStreamChunk> StreamAsync(
            AITaskPurpose purpose,
            List<OpenAICompatibleChatMessage> messages,
            List<OpenAICompatibleToolDefinition>? tools = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var request = BuildRequestFor(purpose, messages, tools);
            return _inner.StreamAsync(request, ct);
        }
    }
}
```

- [ ] **Step 3.3：commit**

```bash
dotnet test tests/Tianming.AI.Tests/Tianming.AI.Tests.csproj --filter RoutedChatClientTests --nologo -v minimal
git add src/Tianming.AI/Core/Routing/RoutedChatClient.cs \
        tests/Tianming.AI.Tests/Core/Routing/RoutedChatClientTests.cs
git commit -m "feat(ai-router): M6.6.3 RoutedChatClient 装饰器（按 Purpose 构造 request）"
```

---

## Task 4：ConversationOrchestrator 切到 RoutedChatClient

**Files:**
- Modify: `src/Tianming.AI/SemanticKernel/Conversation/ConversationOrchestrator.cs`

- [ ] **Step 4.1：在 ctor 加 IAIModelRouter 可选注入**

```csharp
private readonly IAIModelRouter? _router;

public ConversationOrchestrator(
    OpenAICompatibleChatClient chat,
    TagBasedThinkingStrategy thinking,
    IFileSessionStore sessions,
    IEnumerable<IConversationTool> tools,
    AskModeMapper askMapper,
    PlanModeMapper planMapper,
    AgentModeMapper agentMapper,
    string? projectRoot = null,
    IAIModelRouter? router = null)
{
    /* 原赋值 */
    _router = router;
}
```

- [ ] **Step 4.2：内部 StreamXxxAsync 用 Router 决定 Purpose**

把现有构造 OpenAICompatibleChatRequest 的地方改为：

```csharp
// StreamAskAsync 中
UserConfiguration cfg = _router?.Resolve(AITaskPurpose.Chat)
    ?? FallbackConfig();
var request = new OpenAICompatibleChatRequest
{
    BaseUrl = cfg.CustomEndpoint ?? string.Empty,
    ApiKey = cfg.ApiKey,
    Model = cfg.ModelId,
    Messages = messages,
    Temperature = cfg.Temperature,
    MaxTokens = cfg.MaxTokens,
};
```

`StreamPlanAsync` 用 `AITaskPurpose.Writing`；`StreamAgentAsync` 用 `AITaskPurpose.Chat`。

加 using：
```csharp
using TM.Services.Framework.AI.Core.Routing;
```

- [ ] **Step 4.3：写 FallbackConfig 私有方法（router 未注入时的 graceful 降级）**

```csharp
private UserConfiguration FallbackConfig()
{
    // 保留原有"读 active config"的逻辑（如果 Orchestrator 原本就这么干）
    // 否则返回空配置依靠外部错误处理
    return new UserConfiguration { ModelId = string.Empty };
}
```

- [ ] **Step 4.4：build + test**

```bash
dotnet build Tianming.MacMigration.sln --nologo -v minimal
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -10
```

Expected: 全过（旧 orchestrator 测试不应破坏，因为 router 是可选的）。

- [ ] **Step 4.5：commit**

```bash
git add src/Tianming.AI/SemanticKernel/Conversation/ConversationOrchestrator.cs
git commit -m "feat(ai-router): M6.6.4 ConversationOrchestrator 接入 IAIModelRouter (可选)"
```

---

## Task 5：DI 注册 + ModelManagementPage Purpose 列

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs`
- Modify: `src/Tianming.Desktop.Avalonia/ViewModels/AI/ModelManagementViewModel.cs`（加 Purpose 字段）
- Modify: `src/Tianming.Desktop.Avalonia/Views/AI/ModelManagementPage.axaml`（加 Purpose 列）

- [ ] **Step 5.1：DI 注册 Router**

```csharp
// M6.6 AI Middleware + 多模型路由
s.AddSingleton<IAIModelRouter>(sp => new DefaultAIModelRouter(
    () => sp.GetRequiredService<FileAIConfigurationStore>().GetAllConfigurations()));
s.AddSingleton<RoutedChatClient>(sp => new RoutedChatClient(
    sp.GetRequiredService<OpenAICompatibleChatClient>(),
    sp.GetRequiredService<IAIModelRouter>()));
```

加 using：
```csharp
using TM.Services.Framework.AI.Core.Routing;
```

- [ ] **Step 5.2：ModelManagementViewModel 加 Purpose 字段**

在 `ModelConfigItem` 类内加：

```csharp
[ObservableProperty] private string _purpose = "Default";
```

`LoadModels` 中追加：

```csharp
Purpose = config.Purpose,
```

`SaveModelAsync` 中追加：

```csharp
Purpose = item.Purpose,
```

`AddModelAsync` 类似处理（新建配置）。

- [ ] **Step 5.3：ModelManagementPage.axaml 加 Purpose 选择列**

在 DataGrid 现有列后追加：

```xml
<DataGridTemplateColumn Header="用途" Width="120">
  <DataGridTemplateColumn.CellTemplate>
    <DataTemplate>
      <ComboBox SelectedItem="{Binding Purpose, Mode=TwoWay}">
        <ComboBoxItem>Default</ComboBoxItem>
        <ComboBoxItem>Chat</ComboBoxItem>
        <ComboBoxItem>Writing</ComboBoxItem>
        <ComboBoxItem>Polish</ComboBoxItem>
        <ComboBoxItem>Validation</ComboBoxItem>
      </ComboBox>
    </DataTemplate>
  </DataGridTemplateColumn.CellTemplate>
</DataGridTemplateColumn>
```

- [ ] **Step 5.4：build + 全量 test**

```bash
dotnet build Tianming.MacMigration.sln --nologo -v minimal
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -10
```

Expected: 全过。

- [ ] **Step 5.5：commit**

```bash
git add src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs \
        src/Tianming.Desktop.Avalonia/ViewModels/AI/ModelManagementViewModel.cs \
        src/Tianming.Desktop.Avalonia/Views/AI/ModelManagementPage.axaml
git commit -m "feat(ai-router): M6.6.5 DI 注册 + ModelManagementPage 加 Purpose 列"
```

---

## M6.6 Gate 验收

| 项 | 标准 |
|---|---|
| AITaskPurpose 枚举 | 5 值定义 + UserConfiguration.Purpose 字段 |
| IAIModelRouter + Default 实现 | 精确 Purpose 匹配 + Active fallback + Throw on none |
| RoutedChatClient 装饰器 | BuildRequestFor / CompleteAsync / StreamAsync 按 Purpose |
| ConversationOrchestrator 接入 | 可选 router 注入，Ask/Plan/Agent 各自 Purpose |
| UI Purpose 列 | ModelManagementPage 可编辑 Purpose |
| 全量 test | 新增 ≥8 条 router + middleware 测试，dotnet test 全过 |
