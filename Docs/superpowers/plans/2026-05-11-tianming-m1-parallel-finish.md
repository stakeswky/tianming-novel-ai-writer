# 天命 macOS 迁移 — M1 剩余并行收尾 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把 M1 剩余的 Tracking 状态服务族、AI 多协议 ChatClient、SK 编排层和模块业务服务一次性并行迁移到跨平台库,使 macOS net8.0 sln 持续 0 警告 0 错误、全测试通过、零 Windows 绑定。

**Architecture:** csproj 改 SDK glob → 单分支共享 workspace → Wave 1 派 12 个并行 agent(A/B/C/D 块,无依赖)→ 主代理合流 + 4 commit → Wave 2.0 派 2 并行 agent(C5 SK 运行时 + D5 章节修复)→ 合流 → Wave 2.1 派 1 agent(C6 NovelAgent)→ 合流 → 文档更新 + push。

**Tech Stack:** .NET 8.0、Microsoft.SemanticKernel 主包、xUnit、自建 HTTP adapter(Anthropic/Gemini/AzureOpenAI)、git。

**Spec:** `Docs/superpowers/specs/2026-05-11-tianming-m1-parallel-finish-design.md`(commit ff881c1)

---

## Wave 0:主代理基线 + csproj 改造

### Task 0:Wave 0 一次性改造

**Files:**
- Modify: `src/Tianming.ProjectData/Tianming.ProjectData.csproj`
- Modify: `src/Tianming.AI/Tianming.AI.csproj`
- Modify: `src/Tianming.Framework/Tianming.Framework.csproj`
- Modify: `tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj`
- Modify: `tests/Tianming.AI.Tests/Tianming.AI.Tests.csproj`
- Modify: `tests/Tianming.Framework.Tests/Tianming.Framework.Tests.csproj`

- [ ] **Step 0.1: 切分支并确认未追踪树状态**

```bash
cd /Users/jimmy/Downloads/tianming-novel-ai-writer
git status --short | head -20
git checkout -b m1/parallel-finish-2026-05-11
```

Expected: 分支创建成功。未追踪 `src/`、`tests/`、`Tianming.MacMigration.sln` 等不入本分支首 commit(留到改造步之后统一处理)。

- [ ] **Step 0.2: 跑基线测试(改造前)**

```bash
dotnet test Tianming.MacMigration.sln -v minimal 2>&1 | tail -20
```

Expected: `Total tests: 1132 / Passed: 1132 / Failed: 0`。若与此不符,停止,先排查既有问题。

- [ ] **Step 0.3: 改 3 个 src csproj**

每个 csproj 做相同改动:
1. 删除 `<EnableDefaultCompileItems>false</EnableDefaultCompileItems>` 这一行
2. 删除所有"非相对路径起头的" `<Compile Include="..." />` 行(即形如 `<Compile Include="Generation/Foo.cs" />` 的自家文件)
3. 保留所有 `<Compile Include="../../..." Link="..." />` 跨目录显式 Include
4. 在 `<ItemGroup>` 内新增一段 `<Compile Remove>` 防御性排除:

```xml
<ItemGroup>
  <Compile Remove="../../Core/**" />
  <Compile Remove="../../Framework/**" />
  <Compile Remove="../../Modules/**" />
  <Compile Remove="../../Services/**" />
  <Compile Remove="../../Storage/**" />
  <Compile Remove="../../tests/**" />
  <Compile Remove="../../src/**/*.cs" />
</ItemGroup>
```

注意:第一段的跨目录 `<Compile Include>` 不会被 `Remove` 抵消,因为 `Include` 后写的 `Remove` 只对 Include 之后的 glob 生效;而我们的跨目录 Include 是显式单文件,优先级更高。**但为保险,把 `<Compile Remove>` 放在 `<Compile Include="../..." />` **之前**的 ItemGroup**。

正确顺序:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <!-- 防御性排除:确保 SDK glob 不会误吸原项目目录 -->
  <ItemGroup>
    <Compile Remove="../../Core/**" />
    <Compile Remove="../../Framework/**" />
    <Compile Remove="../../Modules/**" />
    <Compile Remove="../../Services/**" />
    <Compile Remove="../../Storage/**" />
  </ItemGroup>

  <!-- 跨目录显式复用原项目 .cs(保留原有内容) -->
  <ItemGroup>
    <Compile Include="../../Framework/Common/Helpers/Id/ShortIdGenerator.cs" Link="..." />
    <!-- ... 其余原 Compile Include 行全部保留 ... -->
  </ItemGroup>
</Project>
```

- [ ] **Step 0.4: 改 3 个 tests csproj**

同样处理。tests/ csproj 还保留 `Microsoft.NET.Test.Sdk` / `xunit` / `xunit.runner.visualstudio` 等 PackageReference。

- [ ] **Step 0.5: 把 SK 主包加入 Tianming.AI.csproj**

在 `src/Tianming.AI/Tianming.AI.csproj` 内添加:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.SemanticKernel" Version="1.30.0" />
</ItemGroup>
```

(版本固定 1.30.0,Wave 2 派发前主代理须验证 SK API 兼容性)

- [ ] **Step 0.6: 预审跨目录引用并补齐**

扫一遍 Wave 1/2 计划中各 agent 计划复用的原项目 .cs。基于 spec §3 与已端口现状,补齐到 `Tianming.AI.csproj` 的 Compile Include(若尚未在):

可能需要补齐(主代理自查):
- `Services/Framework/AI/SemanticKernel/Plugins/ChapterDiffContext.cs` — C2 复用模型
- `Services/Framework/AI/SemanticKernel/UIMessageItem.cs` — C7 复用
- `Services/Framework/AI/SemanticKernel/ChatModeSettings.cs` — C1 复用
- `Services/Framework/AI/SemanticKernel/Chunk/IStreamChunk.cs` — C7/C5 复用
- `Services/Framework/AI/SemanticKernel/Prompts/*.cs` — C2/C3 可能复用

若各文件含 WPF/Win32 引用,**只取数据类型**,不复用含 `using System.Windows` 的文件;改写为新文件在 src/Tianming.AI/ 下。

主代理用 `grep -rn 'using System.Windows\|using TM.App' <候选文件>` 决定能否直接 Compile Include。

- [ ] **Step 0.7: 跑改造后基线测试**

```bash
dotnet build Tianming.MacMigration.sln -v minimal 2>&1 | tail -10
dotnet test Tianming.MacMigration.sln -v minimal 2>&1 | tail -20
```

Expected: 仍为 1132 测试全过、0 警告 0 错误。**这是 Wave 0 的硬门禁。**

- [ ] **Step 0.8: 预创建 Wave 1/2 子目录**

```bash
cd /Users/jimmy/Downloads/tianming-novel-ai-writer
mkdir -p src/Tianming.ProjectData/Tracking/States
mkdir -p src/Tianming.AI/Providers
mkdir -p src/Tianming.AI/SemanticKernel/Orchestration
mkdir -p src/Tianming.AI/SemanticKernel/Plugins
mkdir -p src/Tianming.AI/SemanticKernel/Agents/Providers
mkdir -p src/Tianming.AI/SemanticKernel/Agents/Wrappers
mkdir -p src/Tianming.AI/SemanticKernel/Rewrite
mkdir -p src/Tianming.ProjectData/Modules/Design
mkdir -p src/Tianming.ProjectData/Modules/Generate
mkdir -p src/Tianming.ProjectData/Modules/Analysis
mkdir -p src/Tianming.ProjectData/Modules/Summary
mkdir -p src/Tianming.ProjectData/Modules/Repair
mkdir -p tests/Tianming.ProjectData.Tests/Tracking/States
mkdir -p tests/Tianming.AI.Tests/Providers
mkdir -p tests/Tianming.AI.Tests/SemanticKernel/Orchestration
mkdir -p tests/Tianming.AI.Tests/SemanticKernel/Plugins
mkdir -p tests/Tianming.AI.Tests/SemanticKernel/Agents
mkdir -p tests/Tianming.AI.Tests/SemanticKernel/Rewrite
mkdir -p tests/Tianming.ProjectData.Tests/Modules
```

- [ ] **Step 0.9: Commit Wave 0**

```bash
git add src/Tianming.ProjectData/Tianming.ProjectData.csproj
git add src/Tianming.AI/Tianming.AI.csproj
git add src/Tianming.Framework/Tianming.Framework.csproj
git add tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj
git add tests/Tianming.AI.Tests/Tianming.AI.Tests.csproj
git add tests/Tianming.Framework.Tests/Tianming.Framework.Tests.csproj
git commit -m "$(cat <<'EOF'
chore(macos): csproj 切隐式 glob + SK 主包接入 (Wave 0)

3 个 src/ csproj 与 3 个 tests/ csproj 移除 EnableDefaultCompileItems=false,
删自家目录显式 Compile Include,加 <Compile Remove> 防御性排除原项目目录,
保留跨目录显式 Include 复用原项目 .cs。

Tianming.AI 接入 Microsoft.SemanticKernel 1.30.0 主包。

基线:1132 测试全过,0 警告 0 错误。

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Wave 1:12 个并行 agent

主代理在**单条消息内**发出 12 个 Agent tool call(subagent_type=general-purpose),等待全部返回。

下面 12 个任务可读作 12 个 agent 的派发说明书。每个任务的 prompt 是一个完整可粘贴的字符串。

### 通用约束与汇报模板(下文 "硬约束 + 汇报:同 A1" 即指本段)

```
硬约束:
- 只新增文件,不动既有文件,不动 csproj/sln,不 commit
- 不引 SK alpha connector(Microsoft.SemanticKernel.Connectors.Anthropic/Google/HuggingFace 等),只允许已加入 csproj 的 Microsoft.SemanticKernel 主包
- 不引 WPF/Win32/TM.App;不出现 using System.Windows、using TM.App、using Microsoft.Web.WebView2、using System.Speech、using NAudio、using System.Management、using Microsoft.Win32 Registry、ProtectedData
- 必须新增对称测试,跑对应子库 dotnet test 命令全过
- 若发现必须改既有文件(如某 portable 接口需要加方法),停下汇报主代理而不是擅自改

汇报要求:
- 新增文件清单(完整相对路径)
- 测试命令与最后 10 行通过日志 tail
- <100 字设计取舍说明
- 若有"建议主代理改的既有文件"清单(可选)
```

### Task A1: ProjectData/Tracking 9 个状态服务

**Files:**
- Create: `src/Tianming.ProjectData/Tracking/States/CharacterStateService.cs`
- Create: `src/Tianming.ProjectData/Tracking/States/FactionStateService.cs`
- Create: `src/Tianming.ProjectData/Tracking/States/LocationStateService.cs`
- Create: `src/Tianming.ProjectData/Tracking/States/ItemStateService.cs`
- Create: `src/Tianming.ProjectData/Tracking/States/ForeshadowingStatusService.cs`
- Create: `src/Tianming.ProjectData/Tracking/States/ConflictProgressService.cs`
- Create: `src/Tianming.ProjectData/Tracking/States/RelationStrengthService.cs`
- Create: `src/Tianming.ProjectData/Tracking/States/TimelineService.cs`
- Create: `src/Tianming.ProjectData/Tracking/States/LedgerTrimService.cs`
- Test: `tests/Tianming.ProjectData.Tests/Tracking/States/{Service}Tests.cs` × 9
- Read (原版): `Services/Modules/ProjectData/Implementations/Tracking/CharacterStateService.cs` 及同目录另外 8 个

- [ ] **Step A1.1: 派发 agent(主代理)**

```
你是 Tianming macOS 迁移 M1 收尾的 A1 agent。
仓库:/Users/jimmy/Downloads/tianming-novel-ai-writer
分支:m1/parallel-finish-2026-05-11(已切好)

任务:把 ProjectData/Tracking 下 9 个状态服务端口到 src/Tianming.ProjectData/Tracking/States/,每个服务自带 xUnit 测试。

原版文件(在 Services/Modules/ProjectData/Implementations/Tracking/):
- CharacterStateService.cs
- FactionStateService.cs
- LocationStateService.cs
- ItemStateService.cs
- ForeshadowingStatusService.cs
- ConflictProgressService.cs
- RelationStrengthService.cs
- TimelineService.cs
- LedgerTrimService.cs

新增目标位置:
- src/Tianming.ProjectData/Tracking/States/<Service>.cs × 9
- tests/Tianming.ProjectData.Tests/Tracking/States/<Service>Tests.cs × 9

接口契约:
- 复用已端口的 src/Tianming.ProjectData/Tracking/FactSnapshot.cs、GuideModels.cs、ConsistencyIssue.cs、ConsistencyResult.cs
- 不直接依赖 TM.App.Log 或 StoragePathHelper;若原版有,改为构造函数注入(Action<string> 日志、显式路径)
- 文件 IO 路径用 string 参数,不假设固定根目录
- JSON 序列化用 System.Text.Json,与已端口风格一致

测试覆盖每个服务至少 5-8 个用例,典型场景:
- 加载/保存往返
- 章节状态写入并叠加
- 坏 JSON 恢复
- 缺文件初始化默认
- 状态回退/裁剪边界

硬约束:
- 只新增文件,不动既有文件,不动 csproj/sln,不 commit
- 不引 SK alpha connector,不引 WPF/Win32/TM.App/System.Windows/System.Speech/NAudio/Registry/WebView2
- 必须跑 `dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj -v minimal` 全过

汇报:
- 新增文件清单(完整相对路径)
- 测试通过日志 tail(最后 10 行)
- <100 字设计取舍说明
```

- [ ] **Step A1.2: 等待 agent 返回**

主代理验收点(收稿后):
- 9 个 .cs + 9 个测试文件已在指定路径
- 测试命令返回的 Passed 数 ≥ Wave 0 基线 + 40(每个 service 平均 5 测试以上)
- 无 csproj/sln 改动

### Task B1: AnthropicChatClient

**Files:**
- Create: `src/Tianming.AI/Providers/AnthropicChatClient.cs`
- Create: `src/Tianming.AI/Providers/AnthropicMessages.cs`(请求/响应模型)
- Test: `tests/Tianming.AI.Tests/Providers/AnthropicChatClientTests.cs`
- Read (原版): `Services/Framework/AI/SemanticKernel/AnthropicChatCompletionService.cs`

- [ ] **Step B1.1: 派发 agent(主代理)**

```
你是 Tianming macOS 迁移 M1 收尾的 B1 agent。
仓库:/Users/jimmy/Downloads/tianming-novel-ai-writer
分支:m1/parallel-finish-2026-05-11(已切好)

任务:端口 Anthropic Messages API 到独立的跨平台 ChatClient,实现 SK Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService。

原版参考:Services/Framework/AI/SemanticKernel/AnthropicChatCompletionService.cs

新增目标:
- src/Tianming.AI/Providers/AnthropicChatClient.cs(主类,实现 IChatCompletionService)
- src/Tianming.AI/Providers/AnthropicMessages.cs(请求/响应 DTO,内部 record)
- tests/Tianming.AI.Tests/Providers/AnthropicChatClientTests.cs

接口契约:
- 实现 Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService
- 非流式:POST /v1/messages,Header x-api-key + anthropic-version + content-type
- 流式:SSE 解析 data: 行,处理 message_start / content_block_delta / message_delta / message_stop
- 支持 tool_use / tool_result 消息映射
- HTTP error payload 映射为 KernelException
- 模型名前缀清理(claude-sonnet-4-5、claude-opus-4 等)
- 注入 HttpMessageHandler 便于测试

测试覆盖 ≥ 10 用例:
- 非流式简单文本
- 非流式带 system message
- 流式 chunk 顺序与累积
- tool_use 解析
- HTTP 4xx/5xx 错误映射
- 缺 api_key 错误
- 模型名前缀清理

硬约束:同 A1。

汇报:同 A1。
```

### Task B2: GeminiChatClient

**Files:**
- Create: `src/Tianming.AI/Providers/GeminiChatClient.cs`
- Create: `src/Tianming.AI/Providers/GeminiMessages.cs`
- Test: `tests/Tianming.AI.Tests/Providers/GeminiChatClientTests.cs`

- [ ] **Step B2.1: 派发 agent**

```
你是 Tianming macOS 迁移 M1 收尾的 B2 agent。
仓库:/Users/jimmy/Downloads/tianming-novel-ai-writer
分支:m1/parallel-finish-2026-05-11

任务:实现 Google Gemini GenerateContent API 的跨平台 ChatClient,实现 SK IChatCompletionService。

参考:https://ai.google.dev/api/generate-content REST 文档(不复制 alpha SK connector)

新增目标:
- src/Tianming.AI/Providers/GeminiChatClient.cs
- src/Tianming.AI/Providers/GeminiMessages.cs(请求/响应 DTO)
- tests/Tianming.AI.Tests/Providers/GeminiChatClientTests.cs

接口契约:
- 实现 Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService
- 非流式:POST /v1beta/models/{model}:generateContent?key={apiKey}
- 流式:POST /v1beta/models/{model}:streamGenerateContent?alt=sse
- Gemini role 映射:user/model(注意没有 system,把 system 拼到第一条 user 前)
- functionCall / functionResponse 工具调用映射到 SK ChatMessageContent.Items
- safety_settings 走配置(默认 BLOCK_NONE)
- generationConfig.temperature/topP/maxOutputTokens 接 PromptExecutionSettings

测试覆盖 ≥ 10 用例:同 B1 类似结构。

硬约束 + 汇报:同 B1。
```

### Task B3: AzureOpenAIChatClient

**Files:**
- Create: `src/Tianming.AI/Providers/AzureOpenAIChatClient.cs`
- Test: `tests/Tianming.AI.Tests/Providers/AzureOpenAIChatClientTests.cs`

- [ ] **Step B3.1: 派发 agent**

```
你是 Tianming macOS 迁移 M1 收尾的 B3 agent。
仓库:/Users/jimmy/Downloads/tianming-novel-ai-writer
分支:m1/parallel-finish-2026-05-11

任务:实现 Azure OpenAI Service ChatCompletions 的跨平台 ChatClient,实现 SK IChatCompletionService。

参考:Azure OpenAI REST API 文档(deployment-id 路由)

新增目标:
- src/Tianming.AI/Providers/AzureOpenAIChatClient.cs
- tests/Tianming.AI.Tests/Providers/AzureOpenAIChatClientTests.cs

接口契约:
- 实现 Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService
- URL:{endpoint}/openai/deployments/{deployment-id}/chat/completions?api-version={apiVersion}
- 鉴权:api-key Header(简化版,AAD/Token credential 留 placeholder 接口)
- 响应解析与 OpenAI 兼容
- 模型名映射到 deploymentId(用户配置 model = deploymentId)
- 错误响应 envelope 映射

可以复用 src/Tianming.AI/Core/OpenAICompatibleChatClient.cs 的 SSE 解析逻辑(internal helper 抽出来或复制少量代码;不重复 300+ 行,提取共享 helper 到 Providers/SseChatStreamReader.cs)。

测试覆盖 ≥ 8 用例。

硬约束 + 汇报:同 B1。
```

### Task C1: ChatModeSettings / PlanModeFilter

**Files:**
- Create: `src/Tianming.AI/SemanticKernel/Orchestration/ChatModeSettings.cs`
- Create: `src/Tianming.AI/SemanticKernel/Orchestration/PlanModeFilter.cs`
- Test: `tests/Tianming.AI.Tests/SemanticKernel/Orchestration/ChatModeSettingsTests.cs`
- Test: `tests/Tianming.AI.Tests/SemanticKernel/Orchestration/PlanModeFilterTests.cs`
- Read (原版): `Services/Framework/AI/SemanticKernel/ChatModeSettings.cs`、`PlanModeFilter.cs`

- [ ] **Step C1.1: 派发 agent**

```
你是 Tianming macOS 迁移 M1 收尾的 C1 agent。
仓库:/Users/jimmy/Downloads/tianming-novel-ai-writer
分支:m1/parallel-finish-2026-05-11

任务:端口 ChatModeSettings 和 PlanModeFilter 两个轻量配置类到跨平台库。

原版:
- Services/Framework/AI/SemanticKernel/ChatModeSettings.cs
- Services/Framework/AI/SemanticKernel/PlanModeFilter.cs

新增目标:
- src/Tianming.AI/SemanticKernel/Orchestration/ChatModeSettings.cs
- src/Tianming.AI/SemanticKernel/Orchestration/PlanModeFilter.cs
- tests/Tianming.AI.Tests/SemanticKernel/Orchestration/ChatModeSettingsTests.cs
- tests/Tianming.AI.Tests/SemanticKernel/Orchestration/PlanModeFilterTests.cs

接口契约:
- ChatModeSettings:配置 Ask/Plan/Agent 三种模式的 PromptExecutionSettings + 模型温度/topP/maxTokens
- PlanModeFilter:实现 Microsoft.SemanticKernel.IFunctionInvocationFilter,在 Plan 模式下拦截非允许的工具调用
- 替换 TM.App.Log 为 Action<string> 注入

测试覆盖:
- ChatModeSettings:Ask/Plan/Agent 三套默认参数、json 序列化反序列化、覆盖更新、未知模式 fallback
- PlanModeFilter:允许列表内工具放行、非允许工具拦截、函数调用元数据传递

硬约束 + 汇报:同 A1。
```

### Task C2: 纯逻辑工具(LayeredPromptBuilder / ChapterDiffContext / StructuredMemoryExtractor)

**Files:**
- Create: `src/Tianming.AI/SemanticKernel/Rewrite/LayeredPromptBuilder.cs`
- Create: `src/Tianming.AI/SemanticKernel/Rewrite/ChapterDiffContext.cs`
- Create: `src/Tianming.AI/SemanticKernel/Orchestration/StructuredMemoryExtractor.cs`
- Test: `tests/Tianming.AI.Tests/SemanticKernel/Rewrite/LayeredPromptBuilderTests.cs`
- Test: `tests/Tianming.AI.Tests/SemanticKernel/Rewrite/ChapterDiffContextTests.cs`
- Test: `tests/Tianming.AI.Tests/SemanticKernel/Orchestration/StructuredMemoryExtractorTests.cs`
- Read (原版): `Services/Framework/AI/SemanticKernel/Plugins/LayeredPromptBuilder.cs`、`ChapterDiffContext.cs`、`StructuredMemoryExtractor.cs`

- [ ] **Step C2.1: 派发 agent**

```
你是 Tianming macOS 迁移 M1 收尾的 C2 agent。
仓库:/Users/jimmy/Downloads/tianming-novel-ai-writer
分支:m1/parallel-finish-2026-05-11

任务:端口三个不依赖 SK 运行时的纯逻辑工具类。

原版:
- Services/Framework/AI/SemanticKernel/Plugins/LayeredPromptBuilder.cs
- Services/Framework/AI/SemanticKernel/Plugins/ChapterDiffContext.cs
- Services/Framework/AI/SemanticKernel/StructuredMemoryExtractor.cs

新增目标:
- src/Tianming.AI/SemanticKernel/Rewrite/LayeredPromptBuilder.cs
- src/Tianming.AI/SemanticKernel/Rewrite/ChapterDiffContext.cs
- src/Tianming.AI/SemanticKernel/Orchestration/StructuredMemoryExtractor.cs
+ 对应 3 个测试文件

接口契约:
- 替换 TM.App.Log → Action<string>
- 替换 StoragePathHelper → 显式 string 路径
- 替换 SK ChatHistory(若 StructuredMemoryExtractor 用)→ 已端口的 ConversationMessage 列表(src/Tianming.AI/SemanticKernel/Conversation/Models/ConversationMessage.cs)
- LayeredPromptBuilder:多层 prompt 拼装(behavior / business / developer / dialog),提供输入校验和裁剪
- ChapterDiffContext:章节前后对比上下文构建(原文片段、变更块、增量统计)
- StructuredMemoryExtractor:从 PortableChatMessage 历史抽取结构化记忆 entries

测试覆盖:每类 ≥ 6 用例,涵盖空输入、边界长度、嵌套结构、异常输入。

硬约束 + 汇报:同 A1。
```

### Task C3: SK Plugins(WriterPlugin / DataLookupPlugin / SystemPlugin)

**Files:**
- Create: `src/Tianming.AI/SemanticKernel/Plugins/WriterPlugin.cs`
- Create: `src/Tianming.AI/SemanticKernel/Plugins/DataLookupPlugin.cs`
- Create: `src/Tianming.AI/SemanticKernel/Plugins/SystemPlugin.cs`
- Test: `tests/Tianming.AI.Tests/SemanticKernel/Plugins/WriterPluginTests.cs`
- Test: `tests/Tianming.AI.Tests/SemanticKernel/Plugins/DataLookupPluginTests.cs`
- Test: `tests/Tianming.AI.Tests/SemanticKernel/Plugins/SystemPluginTests.cs`
- Read (原版): `Services/Framework/AI/SemanticKernel/Plugins/WriterPlugin.cs`、`DataLookupPlugin.cs`、`SystemPlugin.cs`

- [ ] **Step C3.1: 派发 agent**

```
你是 Tianming macOS 迁移 M1 收尾的 C3 agent。
仓库:/Users/jimmy/Downloads/tianming-novel-ai-writer
分支:m1/parallel-finish-2026-05-11

任务:把 3 个 SK Plugin 端口到跨平台库,KernelFunction 装饰保留,内部委托到已端口的 ProjectData portable 服务。

原版:
- Services/Framework/AI/SemanticKernel/Plugins/WriterPlugin.cs
- Services/Framework/AI/SemanticKernel/Plugins/DataLookupPlugin.cs
- Services/Framework/AI/SemanticKernel/Plugins/SystemPlugin.cs

新增目标:
- src/Tianming.AI/SemanticKernel/Plugins/{WriterPlugin,DataLookupPlugin,SystemPlugin}.cs
- tests/Tianming.AI.Tests/SemanticKernel/Plugins/*Tests.cs × 3

接口契约:
- 用 [KernelFunction] 装饰每个工具方法
- 构造函数注入 portable 依赖:IProjectStorage(可定义)、ChapterContentStore、FileChapterKeywordIndex、FileVectorSearchService、IPromptTextGenerator 等
- 不直接依赖 WPF / TM.App / SKChatService(SKChatService 由 C5 接线时再装配,这里只暴露工具函数)
- WriterPlugin:WriteChapterDraft / RewriteChapter / GenerateContentBlock 等工具方法(参考原版方法签名,但实现委托到 ChapterGenerationPipeline / PortableChapterRepairService / IChatRewriteClient interface)
- DataLookupPlugin:LookupCharacter / LookupFaction / LookupChapterSummary 等(委托到 FileModuleDataStore / ChapterTrackingDispatcher 等)
- SystemPlugin:GetCurrentDateTime / GetProjectInfo / 等系统工具

注意:若 WriterPlugin 需要 AI 调用,通过构造函数注入 IChatRewriteClient interface(由 C4 定义、C5 完成具体接线);本 agent 不实现真实 IChatRewriteClient,只用 fake 测试。

测试覆盖:每个 Plugin ≥ 8 用例,涵盖工具调用成功、参数缺失、底层服务失败、KernelFunction 元数据正确。

硬约束 + 汇报:同 A1。
```

### Task C4: AutoRewriteEngine

**Files:**
- Create: `src/Tianming.AI/SemanticKernel/Rewrite/IChatRewriteClient.cs`(接口)
- Create: `src/Tianming.AI/SemanticKernel/Rewrite/AutoRewriteEngine.cs`
- Test: `tests/Tianming.AI.Tests/SemanticKernel/Rewrite/AutoRewriteEngineTests.cs`
- Read (原版): `Services/Framework/AI/SemanticKernel/Plugins/AutoRewriteEngine.cs`

- [ ] **Step C4.1: 派发 agent**

```
你是 Tianming macOS 迁移 M1 收尾的 C4 agent。
仓库:/Users/jimmy/Downloads/tianming-novel-ai-writer
分支:m1/parallel-finish-2026-05-11

任务:端口 AutoRewriteEngine —— CHANGES diff 应用 + AI 重写控制循环。

原版:Services/Framework/AI/SemanticKernel/Plugins/AutoRewriteEngine.cs

新增目标:
- src/Tianming.AI/SemanticKernel/Rewrite/IChatRewriteClient.cs(抽象接口,本 agent 定义)
- src/Tianming.AI/SemanticKernel/Rewrite/AutoRewriteEngine.cs
- tests/Tianming.AI.Tests/SemanticKernel/Rewrite/AutoRewriteEngineTests.cs

接口契约:
- IChatRewriteClient:抽象方法 Task<RewriteResult> RewriteAsync(RewriteRequest req, CancellationToken ct),含 prompt / system prompt / chat history / cancellation
- AutoRewriteEngine:依赖 IChatRewriteClient(C5 完成真实接线)、ChangesProtocolParser(已端口)、PortableChapterRepairService(已端口)
- 控制循环:发起重写 → 解析 CHANGES → 应用到章节内容 → 校验 → 重试或终止
- 重试策略:最多 N 次,失败原因记录
- 不直接 IO,所有持久化通过注入的 ChapterContentStore

测试覆盖 ≥ 10 用例:
- 单次成功重写
- CHANGES 解析失败重试
- 协议无效终止
- AI 返回为空降级
- 重试达到上限失败
- 取消 token 中断
- 多块 CHANGES 顺序应用
- 应用后内容长度突变拒绝
- IChatRewriteClient 用 fake 实现(本 agent 写 fake)

硬约束 + 汇报:同 A1。
```

### Task C7: GenerationProgressHub / UIMessageItem / Chunk 残留

**Files:**
- Create: `src/Tianming.AI/SemanticKernel/Orchestration/GenerationProgressHub.cs`
- Create: `src/Tianming.AI/SemanticKernel/Orchestration/UIMessageItem.cs`
- Create: `src/Tianming.AI/SemanticKernel/Orchestration/StreamChunkModels.cs`
- Test: `tests/Tianming.AI.Tests/SemanticKernel/Orchestration/GenerationProgressHubTests.cs`
- Test: `tests/Tianming.AI.Tests/SemanticKernel/Orchestration/UIMessageItemTests.cs`
- Read (原版): `Services/Framework/AI/SemanticKernel/GenerationProgressHub.cs`、`UIMessageItem.cs`、`Chunk/IStreamChunk.cs`

- [ ] **Step C7.1: 派发 agent**

```
你是 Tianming macOS 迁移 M1 收尾的 C7 agent。
仓库:/Users/jimmy/Downloads/tianming-novel-ai-writer
分支:m1/parallel-finish-2026-05-11

任务:端口生成进度集线器、UI 消息项模型、Chunk 残留模型(去 WPF 依赖)。

原版:
- Services/Framework/AI/SemanticKernel/GenerationProgressHub.cs
- Services/Framework/AI/SemanticKernel/UIMessageItem.cs
- Services/Framework/AI/SemanticKernel/Chunk/IStreamChunk.cs

新增目标:
- src/Tianming.AI/SemanticKernel/Orchestration/GenerationProgressHub.cs
- src/Tianming.AI/SemanticKernel/Orchestration/UIMessageItem.cs
- src/Tianming.AI/SemanticKernel/Orchestration/StreamChunkModels.cs(把 IStreamChunk 抽象 + 默认实现合并)
+ 测试

接口契约:
- GenerationProgressHub:线程安全的进度状态广播,事件 ProgressUpdated / Completed / Failed
- 不依赖 System.Windows / WPF Dispatcher,改用 IProgress<T> + SynchronizationContext.Current
- UIMessageItem:纯 POCO 消息项(role / content / timestamp / isStreaming / metadata)
- StreamChunkModels:IStreamChunk 接口 + TextDelta / ToolCall / ThinkingDelta 实现

测试覆盖:
- GenerationProgressHub:多订阅、并发更新、取消、事件顺序
- UIMessageItem:序列化、流式拼接、元数据合并
- StreamChunkModels:类型判别、JSON 反序列化

硬约束 + 汇报:同 A1。
```

### Task D1: 设计/规则 portable 包装(6 个)

**Files:**
- Create: `src/Tianming.ProjectData/Modules/Design/CharacterRulesService.cs`
- Create: `src/Tianming.ProjectData/Modules/Design/FactionRulesService.cs`
- Create: `src/Tianming.ProjectData/Modules/Design/LocationRulesService.cs`
- Create: `src/Tianming.ProjectData/Modules/Design/PlotRulesService.cs`
- Create: `src/Tianming.ProjectData/Modules/Design/WorldRulesService.cs`
- Create: `src/Tianming.ProjectData/Modules/Design/CreativeMaterialsService.cs`
- Test: `tests/Tianming.ProjectData.Tests/Modules/Design/*Tests.cs` × 6
- Read (原版): `Modules/Design/Elements/{Character,Faction,Location,Plot}Rules/Services/*.cs`、`Modules/Design/GlobalSettings/WorldRules/Services/WorldRulesService.cs`、`Modules/Design/Templates/CreativeMaterials/Services/CreativeMaterialsService.cs`

- [ ] **Step D1.1: 派发 agent**

```
你是 Tianming macOS 迁移 M1 收尾的 D1 agent。
仓库:/Users/jimmy/Downloads/tianming-novel-ai-writer
分支:m1/parallel-finish-2026-05-11

任务:端口 5 大规则 Service + 创意素材库 Service 为薄壳,内部全部委托到已端口的 FileModuleDataStore。

原版:
- Modules/Design/Elements/CharacterRules/Services/CharacterRulesService.cs
- Modules/Design/Elements/FactionRules/Services/FactionRulesService.cs
- Modules/Design/Elements/LocationRules/Services/LocationRulesService.cs
- Modules/Design/Elements/PlotRules/Services/PlotRulesService.cs
- Modules/Design/GlobalSettings/WorldRules/Services/WorldRulesService.cs
- Modules/Design/Templates/CreativeMaterials/Services/CreativeMaterialsService.cs

新增目标:
- src/Tianming.ProjectData/Modules/Design/<Service>.cs × 6
- tests/Tianming.ProjectData.Tests/Modules/Design/<Service>Tests.cs × 6

接口契约:
- 每个 Service 构造函数接 IModuleDataStore(或直接接 FileModuleDataStore 实例) + 模块标识(string moduleId)
- 暴露 GetCategories / GetItems / Save / Delete / Move 等业务方法,内部委托 store
- 若原版有"规则集预设"(如 CharacterRules 的"主角/反派/配角"角色类型预设),用 string[] 常量 + 初始化 helper 端口
- 不引入 ViewModel 依赖、不引入 ModuleServiceBase(已被 FileModuleDataStore 取代)

测试覆盖:每个 Service ≥ 5 用例,涵盖增删改查、预设初始化、级联删除、模块边界。

硬约束 + 汇报:同 A1。
```

### Task D2: 生成/规划 portable 包装(5 个)

**Files:**
- Create: `src/Tianming.ProjectData/Modules/Generate/OutlineService.cs`
- Create: `src/Tianming.ProjectData/Modules/Generate/VolumeDesignService.cs`
- Create: `src/Tianming.ProjectData/Modules/Generate/ChapterService.cs`
- Create: `src/Tianming.ProjectData/Modules/Generate/BlueprintService.cs`
- Create: `src/Tianming.ProjectData/Modules/Generate/ContentConfigService.cs`
- Test: `tests/Tianming.ProjectData.Tests/Modules/Generate/*Tests.cs` × 5

- [ ] **Step D2.1: 派发 agent**

```
你是 Tianming macOS 迁移 M1 收尾的 D2 agent。
仓库:/Users/jimmy/Downloads/tianming-novel-ai-writer
分支:m1/parallel-finish-2026-05-11

任务:端口 5 个生成/规划 Service。

原版:
- Modules/Generate/GlobalSettings/Outline/Services/OutlineService.cs
- Modules/Generate/Elements/VolumeDesign/Services/VolumeDesignService.cs
- Modules/Generate/Elements/Chapter/Services/ChapterService.cs
- Modules/Generate/Elements/Blueprint/Services/BlueprintService.cs
- Modules/Generate/Content/Services/ContentConfigService.cs

新增目标:
- src/Tianming.ProjectData/Modules/Generate/<Service>.cs × 5
- tests/Tianming.ProjectData.Tests/Modules/Generate/<Service>Tests.cs × 5

接口契约:
- 与 D1 风格统一:委托 FileModuleDataStore,模块标识不同
- ChapterService:除标准 CRUD,还有"按卷归属"查询、跨卷续写 ID 生成(可委托到已端口的 ChapterContentStore)
- VolumeDesignService:卷设计 CRUD + 卷顺序管理
- ContentConfigService:内容配置(语言风格、视角、字数偏好)读写,settings.json 风格

测试覆盖:每个 Service ≥ 5 用例。

硬约束 + 汇报:同 A1。
```

### Task D3: 智能拆书核心(去 WebView2)

**Files:**
- Create: `src/Tianming.ProjectData/Modules/Analysis/BookAnalysisService.cs`
- Create: `src/Tianming.ProjectData/Modules/Analysis/EssenceChapterSelectionService.cs`
- Create: `src/Tianming.ProjectData/Modules/Analysis/IBookSourceFetcher.cs`
- Create: `src/Tianming.ProjectData/Modules/Analysis/NullBookSourceFetcher.cs`
- Test: `tests/Tianming.ProjectData.Tests/Modules/Analysis/BookAnalysisServiceTests.cs`
- Test: `tests/Tianming.ProjectData.Tests/Modules/Analysis/EssenceChapterSelectionServiceTests.cs`

- [ ] **Step D3.1: 派发 agent**

```
你是 Tianming macOS 迁移 M1 收尾的 D3 agent。
仓库:/Users/jimmy/Downloads/tianming-novel-ai-writer
分支:m1/parallel-finish-2026-05-11

任务:端口智能拆书核心,**剥离 WebView2 / NovelCrawlerService 爬虫层**。仅保留"已有文本/HTML"的解析与精华章节选择。

原版参考:
- Modules/Design/SmartParsing/BookAnalysis/Services/BookAnalysisService.cs
- Modules/Design/SmartParsing/BookAnalysis/Services/EssenceChapterSelectionService.cs
- Modules/Design/SmartParsing/BookAnalysis/Services/Parsers/(若有)

不端口:NovelCrawlerService、WebCrawlerService、ConfigurableBookWebSearchProvider(WebView2 依赖)

新增目标:
- src/Tianming.ProjectData/Modules/Analysis/BookAnalysisService.cs
- src/Tianming.ProjectData/Modules/Analysis/EssenceChapterSelectionService.cs
- src/Tianming.ProjectData/Modules/Analysis/IBookSourceFetcher.cs(placeholder 接口,留 M2 替换)
- src/Tianming.ProjectData/Modules/Analysis/NullBookSourceFetcher.cs(默认实现:抛 NotSupportedException,提示用户提供文本)
- tests/Tianming.ProjectData.Tests/Modules/Analysis/<Service>Tests.cs × 2

接口契约:
- BookAnalysisService:输入小说原文 string → 章节分割、人物提取、场景标记、风格指纹
- EssenceChapterSelectionService:从章节列表选出 N 个精华章节(基于长度、关键词密度、情节高潮)
- IBookSourceFetcher:Task<string> FetchAsync(Uri source, CancellationToken ct) —— 抽象书源拉取
- NullBookSourceFetcher:抛 NotSupportedException("M2 阶段会接入实际书源拉取实现")

测试覆盖:
- BookAnalysisService:章节分割准确性、人物提取(基于固定文本样本)、空输入、巨大文本截断
- EssenceChapterSelectionService:N=0、N>章节数、按权重选择正确顺序

硬约束 + 汇报:同 A1。
```

### Task D4: 摘要/残留服务

**Files:**
- Create: `src/Tianming.ProjectData/Modules/Summary/ProgressiveSummaryService.cs`
- Create: `src/Tianming.ProjectData/Modules/Summary/GlobalSummaryService.cs`
- Create: `src/Tianming.ProjectData/Modules/Summary/GeneratedContentExtras.cs`(若有残留)
- Test: `tests/Tianming.ProjectData.Tests/Modules/Summary/ProgressiveSummaryServiceTests.cs`
- Test: `tests/Tianming.ProjectData.Tests/Modules/Summary/GlobalSummaryServiceTests.cs`

- [ ] **Step D4.1: 派发 agent**

```
你是 Tianming macOS 迁移 M1 收尾的 D4 agent。
仓库:/Users/jimmy/Downloads/tianming-novel-ai-writer
分支:m1/parallel-finish-2026-05-11

任务:端口摘要服务 + GeneratedContentService 残留(已端口 ChapterContentStore 未覆盖的部分)。

原版参考:
- Services/Modules/ProjectData/Implementations/Summary/ProgressiveSummaryService.cs (若存在)
- Services/Modules/ProjectData/Implementations/Summary/GlobalSummaryService.cs (若存在)
- Services/Modules/ProjectData/Implementations/Generation/GeneratedContentService.cs (扫一遍,把 ChapterContentStore 未覆盖的逻辑端口过来)

若原版路径不一致,自行 grep 找到。

新增目标:
- src/Tianming.ProjectData/Modules/Summary/ProgressiveSummaryService.cs
- src/Tianming.ProjectData/Modules/Summary/GlobalSummaryService.cs
+ 测试

接口契约:
- ProgressiveSummaryService:增量章节摘要、跨章节摘要合并、按卷打包
- GlobalSummaryService:全书摘要、定时刷新、压缩存储
- 都用 IPromptTextGenerator interface(已端口)做 AI 调用;不直接依赖具体 client

测试覆盖:每类 ≥ 5 用例,fake IPromptTextGenerator。

硬约束 + 汇报:同 A1。
```

---

## Task M1: Wave 1 合流(主代理串行)

- [ ] **Step M1.1: 等 12 个 agent 全部返回**

记录每个 agent 报告的:文件清单、测试日志、设计取舍。

- [ ] **Step M1.2: 检查无意外既有文件改动**

```bash
git status --short
git diff --stat
```

Expected: 只看到 `??` 新文件,没有 `M` 既有文件修改。若有,locally 检查、必要时 `git checkout -- <file>` 撤销该 hunk。

- [ ] **Step M1.3: 防御性扫 Windows 绑定**

```bash
grep -rn "using System.Windows\|using TM.App\|using Microsoft.Web.WebView2\|using System.Speech\|using NAudio\|using System.Management" src/ tests/
```

Expected: 0 命中。

- [ ] **Step M1.4: 构建全 sln**

```bash
dotnet build Tianming.MacMigration.sln -v minimal 2>&1 | tail -15
```

Expected: 0 Warning / 0 Error。若有 build error,定位到具体 agent 的文件,fix 或派遣单点修复 agent。

- [ ] **Step M1.5: 跑全 sln 测试**

```bash
dotnet test Tianming.MacMigration.sln -v minimal 2>&1 | tail -25
```

Expected: 所有测试通过,总数 ≥ Wave 0 基线 + 12 个 agent 报告的新增测试合计。

- [ ] **Step M1.6: 4 个分批 commit(按 A/B/C/D 块)**

```bash
git add src/Tianming.ProjectData/Tracking/States tests/Tianming.ProjectData.Tests/Tracking/States
git commit -m "$(cat <<'EOF'
feat(tracking): port 9 state services

CharacterState/FactionState/LocationState/ItemState/ForeshadowingStatus/
ConflictProgress/RelationStrength/Timeline/LedgerTrim 全部端口到
src/Tianming.ProjectData/Tracking/States/,xUnit 覆盖。

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

```bash
git add src/Tianming.AI/Providers tests/Tianming.AI.Tests/Providers
git commit -m "$(cat <<'EOF'
feat(ai): anthropic/gemini/azure-openai chat clients

3 个自建 HTTP adapter 实现 SK IChatCompletionService:
- AnthropicChatClient(Messages API + tool_use)
- GeminiChatClient(GenerateContent + functionCall)
- AzureOpenAIChatClient(deployment-id 路由)

不引 SK alpha connector 包。

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

```bash
git add src/Tianming.AI/SemanticKernel/Orchestration src/Tianming.AI/SemanticKernel/Plugins src/Tianming.AI/SemanticKernel/Rewrite tests/Tianming.AI.Tests/SemanticKernel
git commit -m "$(cat <<'EOF'
feat(ai-sk): orchestration foundation

端口 ChatModeSettings/PlanModeFilter/LayeredPromptBuilder/ChapterDiffContext/
StructuredMemoryExtractor/WriterPlugin/DataLookupPlugin/SystemPlugin/
AutoRewriteEngine/IChatRewriteClient/GenerationProgressHub/UIMessageItem/
StreamChunkModels 到跨平台 SemanticKernel 子库,SKChatService 与 NovelAgent 在 Wave 2 接入。

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

```bash
git add src/Tianming.ProjectData/Modules tests/Tianming.ProjectData.Tests/Modules
git commit -m "$(cat <<'EOF'
feat(modules): portable wrappers for rules/generate/analysis/summary

设计/规则 6 + 生成/规划 5 + 智能拆书核心(去 WebView2)+ 摘要 2 共
~18 个 Service 端口,薄壳委托 FileModuleDataStore 或 IPromptTextGenerator。
IBookSourceFetcher 接口预留 M2 替换爬虫层。

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step M1.7: Wave 1 收尾确认**

```bash
git log --oneline -10
dotnet test Tianming.MacMigration.sln -v minimal 2>&1 | tail -5
```

Expected: 看到 Wave 0 + 4 个新 commit,测试全过。

---

## Wave 2.0:2 个并行 agent

主代理在**单条消息内**派发 2 个 Agent tool call(C5、D5)。

### Task C5: SKChatService 主对话循环

**Files:**
- Create: `src/Tianming.AI/SemanticKernel/Orchestration/SKChatService.cs`
- Create: `src/Tianming.AI/SemanticKernel/Orchestration/SKChatServiceOptions.cs`
- Create: `src/Tianming.AI/SemanticKernel/Rewrite/SKChatRewriteClient.cs`(实现 IChatRewriteClient,接 SKChatService)
- Test: `tests/Tianming.AI.Tests/SemanticKernel/Orchestration/SKChatServiceTests.cs`
- Test: `tests/Tianming.AI.Tests/SemanticKernel/Rewrite/SKChatRewriteClientTests.cs`
- Read (原版): `Services/Framework/AI/SemanticKernel/SKChatService.cs`

- [ ] **Step C5.1: 派发 agent(Wave 2.0)**

```
你是 Tianming macOS 迁移 M1 收尾的 C5 agent(Wave 2.0)。
仓库:/Users/jimmy/Downloads/tianming-novel-ai-writer
分支:m1/parallel-finish-2026-05-11(Wave 1 已合流)

任务:端口 SKChatService —— SK Kernel 装配 + IChatCompletionService 注入 + Plugin 注册 + Memory 通过 FileVectorSearchService 注入 + ChatHistoryCompressionService 接线。

原版:Services/Framework/AI/SemanticKernel/SKChatService.cs

新增目标:
- src/Tianming.AI/SemanticKernel/Orchestration/SKChatService.cs
- src/Tianming.AI/SemanticKernel/Orchestration/SKChatServiceOptions.cs
- src/Tianming.AI/SemanticKernel/Rewrite/SKChatRewriteClient.cs(实现 Wave 1 C4 定义的 IChatRewriteClient)
+ 测试

接口契约:
- 构造函数注入:
  - IChatCompletionService(由 B1/B2/B3 或 OpenAICompatibleChatClient 之一传入)
  - 已端口的 WriterPlugin / DataLookupPlugin / SystemPlugin(Wave 1 C3)
  - FileVectorSearchService(已端口)— 用于 RAG 上下文注入
  - ChatHistoryCompressionService(已端口)— 用于压缩长会话
  - FileSessionStore(已端口)— 用于会话持久化
  - PlanModeFilter(Wave 1 C1)— Plan 模式工具过滤
- 主对话循环:
  1. 接收用户 ConversationMessage
  2. 从 ReferenceExpansionService 展开 @引用
  3. 通过 FileVectorSearchService 注入相关章节片段
  4. 装配 SK Kernel + 注册 Plugin + ChatHistory
  5. 调用 IChatCompletionService.GetStreamingChatMessageContentsAsync 或非流式
  6. ThinkingBlockParser 分流 thinking/answer(已端口)
  7. Mode profile mapper(Ask/Plan/Agent)产出最终 ConversationMessage(已端口)
  8. ChatHistoryCompressionService 在阈值触发压缩
  9. FileSessionStore 持久化最新消息

不实现:UI 流式回调(M3 由 Avalonia 注入 IProgress<ChunkEvent>);本 agent 暴露 IProgress<T> 参数留接口
SKChatRewriteClient:实现 IChatRewriteClient,把 RewriteRequest 转成 SKChatService 调用

预先验证 SK 1.30.0 API 兼容(主代理 Wave 2 派发前已验证):
- IChatCompletionService 注册到 IServiceCollection.AddKeyedSingleton
- KernelBuilder.Build() / Kernel.InvokePromptAsync
- IFunctionInvocationFilter 注册到 Kernel.FunctionInvocationFilters

测试覆盖 ≥ 12 用例:
- Ask 模式简单问答
- Plan 模式工具拦截
- Agent 模式 plugin 调用链
- RAG 上下文注入
- 流式 chunk 顺序
- thinking 分流
- 历史压缩触发
- 会话持久化
- 取消 token 中断
- 多 provider 切换(注入不同 IChatCompletionService)
- SKChatRewriteClient 包装 IChatRewriteClient 调用
- 异常路径(provider 失败)

硬约束 + 汇报:同 A1。
```

### Task D5: ChapterRepairService 真实接线

**Files:**
- Create: `src/Tianming.ProjectData/Modules/Repair/ChapterRepairService.cs`
- Test: `tests/Tianming.ProjectData.Tests/Modules/Repair/ChapterRepairServiceTests.cs`
- Read (原版): `Modules/Validate/ValidationSummary/ValidationResult/ChapterRepairService.cs`

- [ ] **Step D5.1: 派发 agent(Wave 2.0)**

```
你是 Tianming macOS 迁移 M1 收尾的 D5 agent(Wave 2.0)。
仓库:/Users/jimmy/Downloads/tianming-novel-ai-writer
分支:m1/parallel-finish-2026-05-11(Wave 1 已合流)

任务:端口 ChapterRepairService —— 把已端口的 PortableChapterRepairService + WriterPlugin(Wave 1 C3) + AutoRewriteEngine(Wave 1 C4) 串起来。

原版:Modules/Validate/ValidationSummary/ValidationResult/ChapterRepairService.cs

新增目标:
- src/Tianming.ProjectData/Modules/Repair/ChapterRepairService.cs
- tests/Tianming.ProjectData.Tests/Modules/Repair/ChapterRepairServiceTests.cs

接口契约:
- 构造函数注入:
  - PortableChapterRepairService(已端口,负责修复编排)
  - WriterPlugin(Wave 1 C3,负责 AI 工具调用)
  - AutoRewriteEngine(Wave 1 C4,负责 CHANGES diff 应用)
  - IChatRewriteClient(由 C5 接线;本 agent 测试用 fake)
- 入口:Task<ChapterRepairResult> RepairAsync(ChapterRepairRequest req, CancellationToken ct)
  1. PortableChapterRepairService 加载上下文 + FactSnapshot
  2. 调 WriterPlugin.RewriteChapter 工具方法
  3. AutoRewriteEngine 解析 CHANGES + 应用
  4. 校验后保存

测试覆盖 ≥ 8 用例:
- 单章修复成功路径
- 缺 FactSnapshot 阻断
- AI 调用失败重试
- CHANGES 协议非法终止
- 取消中断
- 多次修复历史

硬约束 + 汇报:同 A1。
```

---

## Task M2.0: Wave 2.0 合流(主代理串行)

- [ ] **Step M2.0.1**: 等 C5、D5 返回,review 文件与测试
- [ ] **Step M2.0.2**: `git status --short` + `git diff --stat` 检查无意外改动
- [ ] **Step M2.0.3**: `dotnet build Tianming.MacMigration.sln -v minimal` 0 错
- [ ] **Step M2.0.4**: `dotnet test Tianming.MacMigration.sln -v minimal` 全过
- [ ] **Step M2.0.5**: 2 个 commit

```bash
git add src/Tianming.AI/SemanticKernel/Orchestration/SKChatService.cs src/Tianming.AI/SemanticKernel/Orchestration/SKChatServiceOptions.cs src/Tianming.AI/SemanticKernel/Rewrite/SKChatRewriteClient.cs tests/Tianming.AI.Tests/SemanticKernel/Orchestration/SKChatServiceTests.cs tests/Tianming.AI.Tests/SemanticKernel/Rewrite/SKChatRewriteClientTests.cs
git commit -m "$(cat <<'EOF'
feat(ai-sk): orchestration runtime

SKChatService 装配 Kernel,注入 IChatCompletionService / WriterPlugin /
DataLookupPlugin / SystemPlugin / FileVectorSearchService /
ChatHistoryCompressionService / FileSessionStore / PlanModeFilter,
RAG 上下文注入 + thinking/answer 分流 + 三种 mode mapper 接入。
SKChatRewriteClient 实现 IChatRewriteClient(C4 定义)。

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"

git add src/Tianming.ProjectData/Modules/Repair tests/Tianming.ProjectData.Tests/Modules/Repair
git commit -m "$(cat <<'EOF'
feat(modules): chapter repair wiring

ChapterRepairService 接通 PortableChapterRepairService +
WriterPlugin + AutoRewriteEngine + IChatRewriteClient。

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Wave 2.1:1 个 agent(C6,依赖 C5 已合流)

### Task C6: NovelAgent + Providers/Wrappers

**Files:**
- Create: `src/Tianming.AI/SemanticKernel/Agents/NovelAgent.cs`
- Create: `src/Tianming.AI/SemanticKernel/Agents/Providers/NovelMemoryProvider.cs`
- Create: `src/Tianming.AI/SemanticKernel/Agents/Providers/RAGContextProvider.cs`
- Create: `src/Tianming.AI/SemanticKernel/Agents/Wrappers/ThinkingStreamWrapper.cs`
- Test: `tests/Tianming.AI.Tests/SemanticKernel/Agents/NovelAgentTests.cs`
- Test: `tests/Tianming.AI.Tests/SemanticKernel/Agents/NovelMemoryProviderTests.cs`
- Test: `tests/Tianming.AI.Tests/SemanticKernel/Agents/RAGContextProviderTests.cs`
- Test: `tests/Tianming.AI.Tests/SemanticKernel/Agents/ThinkingStreamWrapperTests.cs`
- Read (原版): `Services/Framework/AI/SemanticKernel/Agents/NovelAgent.cs`、`Providers/NovelMemoryProvider.cs`、`Providers/RAGContextProvider.cs`、`Wrappers/ThinkingStreamWrapper.cs`

- [ ] **Step C6.1: 派发 agent(Wave 2.1)**

```
你是 Tianming macOS 迁移 M1 收尾的 C6 agent(Wave 2.1)。
仓库:/Users/jimmy/Downloads/tianming-novel-ai-writer
分支:m1/parallel-finish-2026-05-11(Wave 2.0 已合流)

任务:端口 NovelAgent + Memory/RAG Provider + Thinking Stream Wrapper,基于已合流的 SKChatService(Wave 2.0 C5)。

原版:
- Services/Framework/AI/SemanticKernel/Agents/NovelAgent.cs
- Services/Framework/AI/SemanticKernel/Agents/Providers/NovelMemoryProvider.cs
- Services/Framework/AI/SemanticKernel/Agents/Providers/RAGContextProvider.cs
- Services/Framework/AI/SemanticKernel/Agents/Wrappers/ThinkingStreamWrapper.cs

新增目标:对称放在 src/Tianming.AI/SemanticKernel/Agents/ + 测试。

接口契约:
- NovelAgent:基于 SKChatService(Wave 2.0 C5)封装 Agent 模式专用逻辑 —— 任务规划、工具链路由、子任务执行、回溯
- NovelMemoryProvider:从 FileVectorSearchService 提取章节记忆并按相关性排序
- RAGContextProvider:把记忆 + 当前章节上下文拼成 SK Kernel 的 Memory 注入(不用 SK Memory,自己接进 SystemMessage 或 user message 上下文)
- ThinkingStreamWrapper:把流式 chunk 用 ThinkingBlockParser(已端口)分流,thinking 走 IProgress<ThinkingChunk>,answer 走主流

测试覆盖 ≥ 12 用例,fake SKChatService。

硬约束 + 汇报:同 A1。
```

---

## Task M2.1: Wave 2.1 合流(主代理串行)

- [ ] **Step M2.1.1**: 等 C6 返回,review
- [ ] **Step M2.1.2**: `git status --short` + `git diff --stat`
- [ ] **Step M2.1.3**: `dotnet build` + `dotnet test` sln 全过
- [ ] **Step M2.1.4**: 1 个 commit

```bash
git add src/Tianming.AI/SemanticKernel/Agents tests/Tianming.AI.Tests/SemanticKernel/Agents
git commit -m "$(cat <<'EOF'
feat(ai-sk): novel agent + providers + thinking wrapper

NovelAgent 基于 SKChatService 封装 Agent 模式;
NovelMemoryProvider/RAGContextProvider 用 FileVectorSearchService 提供 RAG;
ThinkingStreamWrapper 用 ThinkingBlockParser 流式分流 thinking/answer。

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task F: 最终收尾(主代理)

- [ ] **Step F.1: 跑 Windows 绑定扫描**

```bash
bash Scripts/check-windows-bindings.sh 2>&1 | tail -5
```

Expected: src/ 与 tests/ 范围内 0 命中。若有,定位并修复(可能是 agent 漏过的违例)。

- [ ] **Step F.2: 更新 M0 文档**

打开 `Docs/macOS迁移/M0-环境与阻塞基线.md`,在文件末尾追加:

```markdown
## M1 第六个跨平台验证点

- 新增 ProjectData/Tracking 状态服务:CharacterStateService、FactionStateService、LocationStateService、ItemStateService、ForeshadowingStatusService、ConflictProgressService、RelationStrengthService、TimelineService、LedgerTrimService
- 新增 AI 多协议 ChatClient:AnthropicChatClient、GeminiChatClient、AzureOpenAIChatClient
- 新增 SK 编排基础:ChatModeSettings、PlanModeFilter、LayeredPromptBuilder、ChapterDiffContext、StructuredMemoryExtractor、WriterPlugin、DataLookupPlugin、SystemPlugin、AutoRewriteEngine、IChatRewriteClient、GenerationProgressHub、UIMessageItem、StreamChunkModels
- 新增模块业务服务:CharacterRulesService、FactionRulesService、LocationRulesService、PlotRulesService、WorldRulesService、CreativeMaterialsService、OutlineService、VolumeDesignService、ChapterService、BlueprintService、ContentConfigService、BookAnalysisService、EssenceChapterSelectionService、IBookSourceFetcher、ProgressiveSummaryService、GlobalSummaryService
- 验证结果:dotnet test Tianming.MacMigration.sln -v minimal 通过,新增测试 80+;dotnet build sln 通过,0 Warning / 0 Error。
- 意义:M1 服务层抽离阶段宣告完成,SK 主包接入,多协议 ChatClient 与 Plugin 端口齐备。

## M1 第七个跨平台验证点

- 新增 SK 编排运行时:SKChatService、SKChatServiceOptions、SKChatRewriteClient
- 新增 Agent 模式:NovelAgent、NovelMemoryProvider、RAGContextProvider、ThinkingStreamWrapper
- 新增章节修复真实接线:Modules/Repair/ChapterRepairService
- 验证结果:dotnet test sln 通过,dotnet build 0 Warning / 0 Error。
- 意义:AI 对话主循环、Agent 模式、章节修复 AI 接线在跨平台库内完整闭合;M1 收尾完成,后续进入 M2(WebView2 替换、TMProtect 真实 pin、SK API 适配验收、ONNX 真向量)与 M3(Avalonia UI 启动)。
```

- [ ] **Step F.3: 更新功能对齐矩阵**

打开 `Docs/macOS迁移/功能对齐矩阵.md`,把以下行的状态备注从"仍需迁移"更新为"已端口":
- 角色/势力/地点/剧情/世界观规则(D1)
- 创意素材库(D1)
- 大纲/分卷/章节/蓝图/ContentConfig(D2)
- 智能拆书核心(D3,标记爬虫 M2 替换)
- 章节修复(D5)
- SK 对话服务、Agent 模式、章节生成(C5/C6)
- 兼容接口调用(B1/B2/B3)
- 9 个状态追踪(A1)

具体改法:每一行最后的"...仍需迁移"短语替换为"...已端口,UI 与真实 provider 联调留 M3"。

- [ ] **Step F.4: 最终 commit**

```bash
git add Docs/macOS迁移/M0-环境与阻塞基线.md Docs/macOS迁移/功能对齐矩阵.md
git commit -m "$(cat <<'EOF'
docs(macos): M1 收尾验证点 + 对齐矩阵更新

记录第六、第七验证点;把功能对齐矩阵中本轮端口完成的行
从"仍需迁移"更新为"已端口,UI/Provider 联调留 M3"。

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

- [ ] **Step F.5: 推送分支**

```bash
git push -u origin m1/parallel-finish-2026-05-11
```

- [ ] **Step F.6: 完成报告**

主代理向用户总结:
- 总 commit 数(预期 9 个:Wave 0 + Wave 1 ×4 + Wave 2.0 ×2 + Wave 2.1 ×1 + 收尾 ×1)
- 总新增文件数(预期 ~60 个 .cs + 60 个 Tests.cs)
- 总测试增量(预期 ≥ 80)
- 已知遗留(M2 项:WebView2 替换、TMProtect 真实 pin、ONNX 真向量、SK 2.x 兼容性、真实多协议联调;M3:Avalonia UI)
- 验证证据:最后一次 `dotnet test sln` 输出 tail

---

## 验收标准(再次列出便于核对)

1. `dotnet build Tianming.MacMigration.sln` → 0 Warning / 0 Error
2. `dotnet test Tianming.MacMigration.sln -v minimal` → 全过
3. `Scripts/check-windows-bindings.sh` 在 src/ 与 tests/ 范围 → 0 命中
4. `Docs/macOS迁移/M0-环境与阻塞基线.md` 包含第六/第七验证点
5. `Docs/macOS迁移/功能对齐矩阵.md` 对应行状态更新
6. 分支 `m1/parallel-finish-2026-05-11` 已 push

完成后下一步进入 M2 与 M3。
