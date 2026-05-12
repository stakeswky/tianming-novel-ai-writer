# 天命 macOS 迁移 — M2 AI 与向量能力设计

日期：2026-05-12
分支：`m2/ai-vector-2026-05-12`（计划，本 spec 在 `m2-m6/specs-2026-05-12`）
依据：`Docs/macOS迁移/功能对齐审计与迁移设计草案.md`、`Docs/macOS迁移/功能对齐矩阵.md`、`Docs/superpowers/specs/2026-05-11-tianming-m1-parallel-finish-design.md`

## 1. 范围与边界

### 1.1 纳入 M2

- **A. WebView2 替换**：用 Avalonia.WebView（macOS 走 WKWebView）接入 BookAnalysis 爬虫，维持原版智能拆书的动态渲染能力；CefGlue 作为备选。
- **B. ONNX 真向量**：`OnnxTextEmbedder` 接入 Microsoft.ML.OnnxRuntime；模型按需下载（SHA-256 校验、缓存到 `~/Library/Application Support/Tianming/Models/`）；无模型时继续 `HashingTextEmbedder` fallback。
- **C. SK 2.x API 适配验收**：对 M1 端口到 `src/Tianming.AI/SemanticKernel/` 的 `SKChatService` / `NovelAgent` / Plugin 装配路径做 SK 稳定版 API 真值联调；若 API 破坏性变更则做适配层。
- **D. 多协议真实联调**：B1/B2/B3（Anthropic、Gemini、Azure OpenAI）ChatClient 对真实 endpoint（或 mock server）跑通非流式 + 流式 + tool_use + 错误映射 + Key 轮换。
- **E. TMProtect 真实 pin 值**：把生产环境服务端证书的 SPKI SHA-256 pin 值注入 `PortableSslPinningServerConfiguration`（不含证书替换的跨平台验证逻辑——已在 M1 端口）。
- **F. Fake Provider 测试闭环**：新增 `Tianming.AI.TestSupport` 辅助包，提供 `FakeChatClient`、`FakeEmbedder`、`FakeWebFetcher`，让章节生成闭环可离线跑通。

### 1.2 不在 M2 范围

- Avalonia UI 主窗口 / XAML / DI 主接线（M3）
- 页面迁移（设计/生成/校验/编辑器等，M4）
- macOS 平台能力补齐（M5）
- 服务端能力接入（M6）
- WebView2 替换的"真实爬虫站点规则库"产品决策（M2 只保证技术通路；站点规则库由运营维护）
- ONNX 模型托管 URL 的实际部署（M2 定义接口与默认地址占位；真实地址留 M7 发布前填入）

### 1.3 决策记录

| 编号 | 决策 |
|---|---|
| Q1 | WebView2 替换走 Avalonia.WebView（非 CefGlue），理由：原生 WKWebView 包体小、签名简单、Mac 上性能一致 |
| Q2 | ONNX 模型按需下载（非内置打包），单模型文件约 80-150MB，不适合打入 app bundle |
| Q3 | 模型默认候选：`bge-small-zh-v1.5`（512 维，约 90MB），可通过设置覆盖 |
| Q4 | Fake server 使用内置 `Kestrel` + WireMock.Net 最小套件，不引入 docker 依赖 |
| Q5 | SK 版本锁定到 M2 起跑时的 Microsoft.SemanticKernel 最新稳定版（非 alpha），`global.json` 级 roll-forward 关闭 |
| Q6 | 多协议联调默认对 fake server，真实 endpoint 联调作为人工验收项（需用户提供凭据） |
| Q7 | OnnxRuntime 包版本锁 `Microsoft.ML.OnnxRuntime 1.18.x`（支持 macOS arm64 与 x64 native runtime） |
| Q8 | 爬虫站点规则以 JSON 注入（`BookSourceRule`），不在 C# 代码中硬编码，便于社区贡献 |

## 2. 架构改造

### 2.1 Avalonia.WebView 接入形态

```
Tianming.AI (portable)
└── BookAnalysis
    ├── IBookSourceFetcher            （M1 已留接口）
    ├── BookSourceRule                （JSON schema：URL 模板 / 选择器 / 分章规则）
    ├── PortableBookAnalysisService   （M1 已端口）
    └── WebCrawlerCoordinator         （M2 新增：纯数据 URL 轮询与 rule 匹配）

Tianming.Desktop.Avalonia (M3 建立)
└── Platform
    └── Crawler
        ├── AvaloniaWebViewFetcher    （实现 IBookSourceFetcher；WKWebView 背后）
        └── HttpOnlyFetcher           （无 JS 渲染的 fallback）
```

`IBookSourceFetcher` 契约：

```csharp
public interface IBookSourceFetcher
{
    Task<FetchResult> FetchAsync(FetchRequest request, CancellationToken ct);
}

public record FetchRequest(string Url, BookSourceRule Rule, TimeSpan Timeout);
public record FetchResult(string Html, string FinalUrl, int StatusCode, bool FromCache);
```

Avalonia.WebView 实现要点：
- 隐藏 WebView 控件（`IsVisible=false`，`Width=0/Height=0`）挂在后台 `TopLevel`，纯做无头渲染
- 通过 `NavigationCompleted` 事件抓取 `document.documentElement.outerHTML`
- 提供 User-Agent 伪装、Cookie 注入、超时取消
- 并发抓取受限到 3 个同时窗口（避免 WKWebView 内存压力）
- 单次抓取失败降级到 `HttpOnlyFetcher`（HttpClient + HtmlAgilityPack）

### 2.2 ONNX 按需下载

```
src/Tianming.AI
├── Embeddings
│   ├── ITextEmbedder                 （M1 已端口）
│   ├── HashingTextEmbedder           （M1 已端口，纯托管 fallback）
│   ├── OnnxTextEmbedder              （M2 新增）
│   ├── IModelDownloader              （M2 新增）
│   ├── ModelRegistry                 （M2 新增：内置候选清单 + SHA-256）
│   ├── PortableModelDownloader       （M2 新增：HttpClient + 校验 + 原子落盘）
│   └── OnnxEmbedderFactory           （M2 新增：按配置选择 embedder）
└── Configuration
    └── EmbeddingSettings             （扩展：ModelName / ModelFilePath）
```

模型注册表（初版硬编码，后续抽到 JSON）：

```csharp
internal static class BuiltInModels
{
    public static readonly ModelDescriptor BgeSmallZh = new(
        Name: "bge-small-zh-v1.5",
        DownloadUrl: "<configured-at-M7>",
        FileName: "bge-small-zh-v1.5.onnx",
        Sha256: "<to-be-filled-M7>",
        Dimensions: 512,
        MaxTokens: 512,
        SizeBytes: 94_600_000);
}
```

`EmbeddingSettings.ModelFilePath` 用户覆盖优先级：用户设置路径 > 已下载缓存 > fallback 到 `HashingTextEmbedder`。

`PortableModelDownloader` 行为：
- 目标路径：`~/Library/Application Support/Tianming/Models/<filename>.part` → 完成后 rename 到 `.onnx`
- 分片下载、断点续传（Range header）
- 进度事件（MB/s、百分比、ETA）
- SHA-256 边下边算；失败清理临时文件
- 取消令牌支持

### 2.3 SK 2.x 适配层

`SKChatService`、`NovelAgent` 已在 M1 端口到 `src/Tianming.AI/SemanticKernel/`。M2 的工作：

1. 升级 `Microsoft.SemanticKernel` 到当前最新稳定版本
2. 跑全量 `Tianming.AI.Tests` → 预期失败点：
   - `IChatCompletionService` 签名变化（流式迭代 API）
   - `KernelFunction` 特性参数
   - `ChatHistory` 序列化
3. 按失败点分批做最小适配（不引入跨版本双轨）
4. 对 Anthropic/Gemini/Azure ChatClient 加 `[Fact(Skip="Manual")]` 的真实联调用例，留给人工验收

若 SK 主包 API 无法适配（破坏性过大），降级方案：
- 剥离 SK，自建 `IChatCompletionService` 等价接口
- Plugin 机制自写 `[KernelFunction]` 等价的 attribute + reflection 调度
- 保留 `WriterPlugin` / `DataLookupPlugin` / `SystemPlugin` 代码结构

### 2.4 Fake Provider 套件

```
tests/Tianming.AI.TestSupport/
├── Tianming.AI.TestSupport.csproj
├── Fakes
│   ├── FakeChatClient.cs             （IChatCompletionService 实现）
│   ├── FakeEmbedder.cs               （ITextEmbedder 实现，返回确定性向量）
│   ├── FakeWebFetcher.cs             （IBookSourceFetcher 实现，返回预置 HTML）
│   └── FakeSessionStore.cs
├── Scenarios
│   ├── ChapterGenerationScenario.cs  （端到端脚本：输入大纲 → 期望 CHANGES）
│   ├── ValidationScenario.cs
│   └── BookAnalysisScenario.cs
└── FakeServer
    ├── FakeOpenAICompatibleServer.cs （Kestrel /v1/chat/completions）
    ├── FakeAnthropicServer.cs        （/v1/messages）
    ├── FakeGeminiServer.cs           （/v1beta/models/:generateContent）
    └── FakeAzureOpenAIServer.cs      （/openai/deployments/:deployment-id/chat/completions）
```

`tests/Tianming.AI.Tests` 依赖 `Tianming.AI.TestSupport`（ProjectReference）。

FakeServer 使用 `WebApplication.CreateBuilder()` + Kestrel，随机端口，xUnit `IAsyncLifetime` 管理生命周期；不引 WireMock.Net（减少 NuGet 依赖）。

### 2.5 目录结构

新增：
- `src/Tianming.AI/Embeddings/`
- `src/Tianming.AI/BookAnalysis/`（若不存在）
- `src/Tianming.Desktop.Avalonia/Platform/Crawler/`（需与 M3 协调建立 Avalonia 项目；若 M3 未到则先空壳）
- `tests/Tianming.AI.TestSupport/`
- `tests/Tianming.AI.Tests/Embeddings/`
- `tests/Tianming.AI.Tests/BookAnalysis/`
- `tests/Tianming.AI.Tests/SemanticKernel/Integration/`

## 3. 工作拆分

### 3.1 Wave 0（主代理串行，~30 分钟）

| 任务 | 内容 | 产出 |
|---|---|---|
| Branch | `git checkout -b m2/ai-vector-2026-05-12` | 分支就绪 |
| 基线 | `dotnet test Tianming.MacMigration.sln` | 测试全过（延续 M1 基线） |
| SK 升级 | `Tianming.AI.csproj` 升级 SK 版本 | 记录破坏性变化清单 |
| NuGet | 加 `Microsoft.ML.OnnxRuntime 1.18.x`、`Avalonia.WebView` 占位（M3 前不引入） | csproj 更新 |
| 建 Avalonia 预留 | 若 M3 未完成，则 `src/Tianming.Desktop.Avalonia/` 先不建；Platform/Crawler 先挂到 `src/Tianming.AI/BookAnalysis/`，M3 再搬家 | 占位目录 |
| Commit | `chore(m2): wave-0 sk upgrade + onnx runtime package` | 1 commit |

### 3.2 Wave 1（7 个 agent，可并行）

| ID | 范围 | 主要新增 | 依赖 |
|---|---|---|---|
| **E1** | ONNX embedder + 模型下载器 | `Embeddings/OnnxTextEmbedder.cs`、`IModelDownloader.cs`、`PortableModelDownloader.cs`、`ModelRegistry.cs`、`OnnxEmbedderFactory.cs` + 6 测试 | Wave 0 OnnxRuntime |
| **E2** | Embedding 配置扩展 | `Configuration/EmbeddingSettings.cs`、`FileAIConfigurationStore` 扩展字段 + 测试 | — |
| **F1** | Fake ChatClient / Embedder / SessionStore | `tests/Tianming.AI.TestSupport/Fakes/*` + 测试夹具 | — |
| **F2** | Fake Server（OpenAI / Anthropic / Gemini / Azure） | `tests/Tianming.AI.TestSupport/FakeServer/*` + 4 套服务器 | Wave 0 |
| **F3** | 多协议 ChatClient 真值集成测试 | `tests/Tianming.AI.Tests/SemanticKernel/Integration/*` + 4 套 @Skip=Manual 联调用例 | F2 |
| **A1** | BookSourceRule + WebCrawlerCoordinator 纯数据层 | `src/Tianming.AI/BookAnalysis/BookSourceRule.cs`、`WebCrawlerCoordinator.cs` + 测试 | — |
| **A2** | HttpOnlyFetcher | `src/Tianming.AI/BookAnalysis/HttpOnlyFetcher.cs`（HttpClient + HtmlAgilityPack）+ 测试 | — |

### 3.3 Wave 2（3 个 agent，依赖 Wave 1 + M3）

| ID | 依赖 | 范围 | 主要新增 |
|---|---|---|---|
| **A3** | A1/A2，M3 Shell 就绪 | AvaloniaWebViewFetcher（WKWebView 背后） | `Tianming.Desktop.Avalonia/Platform/Crawler/AvaloniaWebViewFetcher.cs` + 测试（无头 E2E） |
| **C1** | 全部 F 项 | SK 2.x 破坏性适配 | 改 `SKChatService.cs`、`NovelAgent.cs` 使适配新 API；相关测试维持全过 |
| **D1** | C1 完成 | SKChatService 对 fake server 端到端测试 | `tests/Tianming.AI.Tests/SemanticKernel/Integration/ChapterGenerationE2ETests.cs` |

### 3.4 Wave 3（1 个 agent，M7 前）

| ID | 范围 | 主要新增 |
|---|---|---|
| **E3** | 真实模型 URL + SHA-256 + 真 pin 值 | 更新 `ModelRegistry.cs`、`PortableSslPinningServerConfiguration` 注入生产 pin |

## 4. 执行编排

1. **Wave 0 串行**：SK 升级 + OnnxRuntime + 基线测试 → 1 commit。
2. **Wave 1 同消息内派发 7 个 agent 并行**：主代理等全部返回 → 逐一 review → 统一合流。若 F3 发现 ChatClient 实际行为与 M1 单测不一致，回到对应 Provider 做 fix。合流分 3 commit：`feat(ai): onnx embedder on-demand`、`feat(ai): crawler portable layer`、`test(ai): fake providers + servers`。
3. **Wave 2 派发**：A3 需等 M3 Shell 就绪；若 M3 未完成则 A3 暂缓，`M2` 仍可关闭（A3 留 M3 一起合流）。C1 + D1 并行。2-3 commit。
4. **Wave 3**：M7 发布前人工触发，非 M2 关闭门禁。

## 5. 依赖图

```
Wave 0 → Wave 1 (7 agents 并行) → Wave 1 合流 → Wave 2 (3 agents)
                                                    │
                                                    ├─ A3 (依赖 M3)
                                                    ├─ C1 (依赖 F 全部)
                                                    └─ D1 (依赖 C1)
                                                            ↓
                                                       Wave 2 合流
                                                            ↓
                                                   M2 关闭 (A3 可延至 M3)
```

## 6. 测试策略

| 类别 | 覆盖点 | 工具 |
|---|---|---|
| 单元（E1/E2） | OnnxTextEmbedder 维度、归一化、空文本、超长文本截断；模型下载器 SHA-256 校验失败、断点续传、取消 | xUnit |
| 单元（A1/A2） | BookSourceRule 匹配、HTML 选择器分章、HttpOnlyFetcher 重试与超时 | xUnit + HtmlAgilityPack |
| 单元（F1） | FakeChatClient 脚本化响应、FakeEmbedder 确定性向量 | xUnit |
| 集成（F3） | 对 fake server 跑非流式 + 流式 + tool_use + 错误映射 + 轮换 | xUnit + Kestrel |
| 集成（D1） | SKChatService 对 fake OpenAI server 的章节生成端到端 | xUnit + Kestrel |
| E2E（A3） | Avalonia.WebView 对静态 HTTP 本地服务的真 DOM 抓取 | xUnit + Avalonia Headless |
| 人工验收（F3 @Skip=Manual） | 真 Anthropic/Gemini/Azure/OpenAI 各家 key 的 hello-world 非流式/流式 | 手动 |
| 人工验收（Wave 3） | 真实 ONNX 模型下载、加载、章节 topK 召回 | 手动 |

预期测试增量：Wave 1 约 +60，Wave 2 约 +20；合计 1132 → 约 1212。

## 7. 风险与回滚

### 7.1 主要风险

| ID | 风险 | 影响 | 缓解 |
|---|---|---|---|
| R1 | SK 2.x 破坏性变化过大无法适配 | C1 失败，M1 的 SK 端口需返工 | Wave 0 先 sandbox 一个 SK 最小 demo 跑 chat + plugin；若失败触发降级：剥离 SK，自建 IChatCompletionService 等价接口（M1 代码结构大部分可保留） |
| R2 | Avalonia.WebView 在 macOS 无头运行不稳 | A3 卡死 | 提供 HttpOnlyFetcher fallback；A3 失败不阻塞 M2 关闭，A3 转 M3 完成 |
| R3 | OnnxRuntime 在 Apple Silicon arm64 缺 native | E1 无法运行 | 锁 `1.18.x`（已支持 macOS arm64）；双架构 CI 验证；无 native 时降级到 HashingTextEmbedder 并报警 |
| R4 | FakeServer Kestrel 端口冲突导致测试并行不稳 | F2/F3/D1 间歇失败 | 用 `0` 端口让系统分配；测试 fixture 用 xUnit `IAsyncLifetime` 保证启停顺序 |
| R5 | 真实多协议联调暴露 M1 端口的协议细节错误 | 需要反向修 Provider 实现 | F3 真 @Skip=Manual 用例标注在 M2 设计文档但执行留人工；发现 bug 回 M1 Provider 改代码走 M2 修复 commit |
| R6 | ModelDownloader 下载 URL 被墙 / 证书问题 | 用户首启体验差 | 支持自定义 URL 覆盖 + 提前缓存指引；M7 前补 CDN 镜像 |
| R7 | TMProtect 真实 pin 注入泄露 pin 值到代码库 | 安全隐患 | pin 值走 `Configuration/ssl_pins.prod.json`，`.gitignore` 屏蔽；测试用占位 pin |

### 7.2 回滚策略

- 全部工作在 `m2/ai-vector-2026-05-12`，最坏 `git branch -D` 即可弃
- SK 升级 commit 单独 revert 即回到 M1 SK 版本
- Wave 1 各 agent 独立 commit，失败 revert 单个即可
- OnnxRuntime 包升降级走 csproj diff

### 7.3 退路

- 若 R1 发生：降级自建 IChatCompletionService，M2 仍关闭但 C1 拆出；SK 目录改名 `AIOrchestration/` 以免误导
- 若 R2 发生：v1 智能拆书仅接 HttpOnly 静态站点 + 本地 HTML/TXT 导入；Avalonia.WebView 留 v1.1
- 若 R3 发生：v1 向量能力仅 HashingTextEmbedder；ONNX 留 v1.1

## 8. 验收标准

M2 完成的判据：

1. `dotnet build Tianming.MacMigration.sln` → 0 Warning / 0 Error
2. `dotnet test Tianming.MacMigration.sln -v minimal` → 全过（较 M1 基线增加 ≥ 80 用例）
3. `Scripts/check-windows-bindings.sh src/ tests/` → 0 命中
4. 对 `FakeOpenAICompatibleServer` 跑通章节生成端到端（无需真实 API Key）
5. `OnnxTextEmbedder` 在用户提供合法 ONNX 模型时可返回 512 维向量，无模型时自动降级到 `HashingTextEmbedder`
6. `Docs/macOS迁移/功能对齐矩阵.md` 中 `智能拆书`、`ONNX 向量搜索`、`SK 对话服务`、`兼容接口调用`、`Agent / Plan / Edit 模式` 五行状态更新为"已端口"
7. 分支 `m2/ai-vector-2026-05-12` 已 push 到远端
8. 人工验收记录：至少一个真实 Provider（任选 OpenAI / Anthropic / Gemini / Azure 之一）hello-world 流式 + 非流式通过

完成后进入 M3（Avalonia 基础 Shell）。
