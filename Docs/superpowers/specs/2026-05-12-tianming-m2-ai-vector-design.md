# 天命 macOS 迁移 — M2 AI 与向量能力设计（自用版）

日期：2026-05-12
分支：`m2-m6/specs-2026-05-12`
依据：`Docs/macOS迁移/功能对齐审计与迁移设计草案.md`、`Docs/superpowers/specs/2026-05-11-tianming-m1-parallel-finish-design.md`
定位：**个人自用**，不做公开分发

## 1. 范围与边界

### 1.1 纳入 M2

- **A. OnnxTextEmbedder**：`src/Tianming.AI/Embeddings/OnnxTextEmbedder.cs`，基于 `Microsoft.ML.OnnxRuntime`。模型文件由用户自己放到 `~/Library/Application Support/Tianming/Models/<file>.onnx`；`EmbeddingSettings.ModelFilePath` 配置指向；没有文件则继续用 M1 的 `HashingTextEmbedder`。
- **B. SK 2.x 升级验证**：把 `Microsoft.SemanticKernel` 升到当前最新稳定版，跑 `Tianming.AI.Tests` 全量，逐个修破坏性变化。
- **C. OpenAI-compatible 真实联调**：用你自己的 API Key（通义千问 / DeepSeek / 月之暗面 / ChatGPT / 硅基流动 等任一），跑通非流式 + 流式 + tool_use + 多 Key 轮换。
- **D. 智能拆书降级**：`IBookSourceFetcher` 加一个 `LocalFileFetcher`（接收本地 HTML/TXT 文件路径）。网络爬虫不做，需要时你自己用浏览器存文件再导入。

### 1.2 不做

- Fake Server 套件（`FakeOpenAI/Anthropic/Gemini/Azure` Kestrel 服务端）— 自用场景单测 + 你手动试 1-2 次就够
- Anthropic / Gemini / Azure OpenAI 多协议真实联调 — 你用哪个接哪个，用不着的先不做
- ONNX 模型按需下载器（SHA-256 校验、断点续传、进度事件）— 你自己手放文件就好
- WebView2 / Avalonia.WebView / CefGlue 动态渲染爬虫 — 智能拆书不是你核心需求，保留静态文件导入能力即可
- TMProtect pin 值注入 — 自用不走生产服务端，M2 直接把 `PortableSslPinningValidator` 在客户端不启用

### 1.3 决策

| 编号 | 决策 |
|---|---|
| Q1 | 模型文件：用户手放，默认搜路径 `~/Library/Application Support/Tianming/Models/`，设置里可覆盖 |
| Q2 | OnnxRuntime 版本：`Microsoft.ML.OnnxRuntime 1.18.x`（支持 macOS arm64 + x64） |
| Q3 | SK 版本：锁到 M2 起跑时的最新稳定版 |
| Q4 | 默认 Provider：你填一个 OpenAI-compatible endpoint 就能跑；Anthropic/Gemini 保留 M1 已端口代码但不做 M2 真实联调 |
| Q5 | 智能拆书：只做本地文件导入，`PortableBookAnalysisService` + `LocalFileFetcher` |

## 2. 架构

### 2.1 新增目录

```
src/Tianming.AI/
├── Embeddings/
│   ├── OnnxTextEmbedder.cs              （新增）
│   ├── OnnxEmbedderFactory.cs           （新增：按配置选 Onnx 或 Hashing）
│   └── Configuration/
│       └── EmbeddingSettings.cs         （扩展 ModelFilePath 字段）
└── BookAnalysis/
    └── LocalFileFetcher.cs              （新增：IBookSourceFetcher 实现）
```

`OnnxTextEmbedder` 关键逻辑：
- 构造器读 `EmbeddingSettings.ModelFilePath`；空或文件不存在 → 抛 `ModelNotConfiguredException`
- `OnnxEmbedderFactory.Create()` 捕获该异常 → 返回 `HashingTextEmbedder` + 日志降级告知
- Tokenizer：用 `Microsoft.ML.Tokenizers`（如 `bge` 系列用 BertTokenizer）
- 首次调用懒加载 `InferenceSession`，后续复用

`LocalFileFetcher`：

```csharp
public sealed class LocalFileFetcher : IBookSourceFetcher
{
    public async Task<FetchResult> FetchAsync(FetchRequest req, CancellationToken ct)
    {
        // req.Url 格式：file:///Users/.../novel.html
        var path = new Uri(req.Url).LocalPath;
        var html = await File.ReadAllTextAsync(path, ct);
        return new FetchResult(html, req.Url, 200, false);
    }
}
```

### 2.2 SK 升级适配

流程：
1. `Tianming.AI.csproj` 升级 `Microsoft.SemanticKernel` 到最新稳定
2. `dotnet test tests/Tianming.AI.Tests` → 按错误逐个修
3. 重点关注：`IChatCompletionService` 流式 API、`KernelFunction` attribute、`ChatHistory` 序列化
4. 修不动的（破坏性太大）：降级到 M1 锁定的版本；记录在文档里

## 3. 工作拆分

一共 4 个步骤串行（自用不需要多 agent 并行）：

1. **Step 1 — SK 升级 + 跑测**（~1 小时）：升 SK → 跑全量 → 修 break。Commit `chore(ai): upgrade SemanticKernel to <version>`。
2. **Step 2 — OnnxTextEmbedder**（~2 小时）：加 `Microsoft.ML.OnnxRuntime` + `Microsoft.ML.Tokenizers`；写 `OnnxTextEmbedder` + `OnnxEmbedderFactory` + 3 个单测（有模型/无模型/模型损坏降级）。Commit `feat(ai): OnnxTextEmbedder 接入`。
3. **Step 3 — LocalFileFetcher**（~30 分钟）：写 fetcher + 1 个单测。Commit `feat(ai): BookAnalysis LocalFileFetcher`。
4. **Step 4 — OpenAI-compatible 真实联调**（~1 小时，手工）：改 `appsettings.Development.json`（或等效）填你的 endpoint/key，跑一个小程序（或 `Tianming.AI.Tests` 的 `[Fact(Skip="Manual")]` 标记的集成用例），验证非流式 + 流式 + tool_use。记录在 `Docs/macOS迁移/M2-联调笔记.md`。

## 4. 测试

- 单测：OnnxTextEmbedder 3 例、LocalFileFetcher 1 例、升 SK 后 M1 基线全过
- 手工：OpenAI-compatible 真实 endpoint 联调 1 次，确认走得通
- 不写：fake server、多协议集成、CI 集成

## 5. 风险

| ID | 风险 | 缓解 |
|---|---|---|
| R1 | SK 升级破坏性过大 | 降回 M1 锁定版本；M2 跳过 SK 升级 |
| R2 | OnnxRuntime 在你本机架构（arm64 or x64）不可用 | 用 `HashingTextEmbedder` fallback；不强求 ONNX |
| R3 | 无 ONNX 模型时功能残缺 | 不是 M2 问题——你自己下个 bge-small-zh 放进去即可 |

## 6. 验收

1. `dotnet build Tianming.MacMigration.sln` → 0 Error
2. `dotnet test Tianming.MacMigration.sln` → 全过
3. 放一个 ONNX 模型到 `~/Library/Application Support/Tianming/Models/`，`OnnxTextEmbedder` 能返回向量（维度符合模型规格）
4. 无模型时自动降级 `HashingTextEmbedder`
5. 你自己的 OpenAI-compatible API 联调成功（流式 + 非流式至少一次对话）

完成后进入 M3（Avalonia Shell）。
