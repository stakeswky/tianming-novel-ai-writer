# 天命 macOS 迁移 — M2 AI 与向量能力 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 给天命的跨平台 AI 类库接入真实 ONNX 文本嵌入器（`OnnxTextEmbedder`），让用户放一个本地 `.onnx` 模型后能得到真向量；无模型时自动降级到既有 `HashingTextEmbedder`；配套一个 OpenAI-compatible API 的手工联调脚本，确认章节生成真能调用真模型出正文。

**Architecture:** 在 `src/Tianming.AI/SemanticKernel/Embedding/` 下新增 `EmbeddingSettings`、`OnnxTextEmbedder`、`OnnxEmbedderFactory` 三个类；Tokenizer 走 `FastBertTokenizer` NuGet 包（BSD 许可、稳定）；推理走 `Microsoft.ML.OnnxRuntime 1.18.x`（macOS arm64 + x64 原生支持）；Factory 根据 `EmbeddingSettings.ModelFilePath` 是否有效决定返回 Onnx 实现还是 Hashing fallback。手工联调脚本 `Scripts/m2-openai-smoke.csx` 接 `OpenAICompatibleChatClient`（M1 已存在），读 `~/.tianming/smoke.json` 里的 endpoint + key 配置，跑流式与非流式各一次。

**Tech Stack:** .NET 8.0 / xUnit / `Microsoft.ML.OnnxRuntime 1.18.1` / `FastBertTokenizer 1.0.28` / 既有 `OpenAICompatibleChatClient`（HttpClient + SSE）。

**Spec:** `Docs/superpowers/specs/2026-05-12-tianming-m2-ai-vector-design.md`（位于分支 `m2-m6/specs-2026-05-12`，当前 worktree 不可见；scope 关键点已在本 plan 头与各 Task 下 inline）。

## Scope Alignment（对 spec 的务实调整）

Spec 列了 4 项（SK 升级 / OnnxEmbedder / LocalFileFetcher / OpenAI-compatible 联调）。对齐当前仓实际状态后：

- **SK 升级 — 跳过**。当前 `src/Tianming.AI/Tianming.AI.csproj` 没有 `Microsoft.SemanticKernel` PackageReference。目录 `SemanticKernel/` 只是命名约定，内容是 portable 自包含代码（`ITextEmbedder` / `ThinkingBlockParser` 等），不依赖 SK 包。没有包可升。
- **LocalFileFetcher — 推到 M4 再做**。`IBookSourceFetcher` / `PortableBookAnalysisService` 均未在仓里存在；智能拆书整套 portable 服务尚未抽离，单建 LocalFileFetcher 接口无下游消费者。自用场景 M4 真要用"本地文件导入拆书"UI 时再顺手端口。
- **OnnxTextEmbedder — 做**。下文 Task 1-5。
- **OpenAI-compatible 手工联调 — 做**。下文 Task 6。

这个调整让 M2 聚焦到唯一真缺的能力（真向量搜索）并保留最小联调确认。其余推到后续里程碑，符合"不用搞那么复杂，我自己用的"的范围声明。

## File Structure

新建：
- `src/Tianming.AI/SemanticKernel/Embedding/EmbeddingSettings.cs` — 配置记录（模型路径、vocab 路径、最大序列长度、池化策略）
- `src/Tianming.AI/SemanticKernel/Embedding/OnnxTextEmbedder.cs` — `ITextEmbedder` 实现，懒加载 `InferenceSession`，mean pooling 归一化输出
- `src/Tianming.AI/SemanticKernel/Embedding/OnnxEmbedderFactory.cs` — 根据配置返回 Onnx 或 Hashing 实例
- `tests/Tianming.AI.Tests/EmbeddingSettingsTests.cs` — 配置校验
- `tests/Tianming.AI.Tests/OnnxEmbedderFactoryTests.cs` — 工厂行为（有效/无效配置）
- `tests/Tianming.AI.Tests/TestSupport/FakeTextEmbedder.cs` — 工厂单测复用的 stub
- `Scripts/m2-openai-smoke.csx` — 手工联调脚本

修改：
- `src/Tianming.AI/Tianming.AI.csproj` — 加 2 个 NuGet 包 + 3 条 `<Compile Include>`（保持现有显式 Compile 约定）
- `tests/Tianming.AI.Tests/Tianming.AI.Tests.csproj` — 不用改（默认 glob 包含所有 .cs）

不改：
- `src/Tianming.AI/SemanticKernel/Embedding/ITextEmbedder.cs`（已有接口足够）
- `src/Tianming.AI/SemanticKernel/Embedding/HashingTextEmbedder.cs`（fallback 保留原样）

注意：由于 `Tianming.AI.csproj` 目前使用 `<EnableDefaultCompileItems>false</EnableDefaultCompileItems>` + 显式 `<Compile Include>`，每新增一个 .cs 文件必须在 csproj 对应 `<ItemGroup>` 加一行，否则不参与编译。

---

## Task 0：基线确认

**Files:**
- 无修改，只做命令验证

- [ ] **Step 0.1：确认在正确的 worktree 与分支**

```bash
cd /Users/jimmy/Downloads/tianming-novel-ai-writer/.worktrees/m2-parallel
git branch --show-current
```

Expected: `m2/onnx-localfetch-2026-05-12`

- [ ] **Step 0.2：确认 SDK**

```bash
dotnet --version
```

Expected: `8.0.420`

- [ ] **Step 0.3：跑基线测试**

```bash
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -6
```

Expected 最后三行含：
```
已通过! - ... 通过:   218 ... Tianming.ProjectData.Tests.dll ...
已通过! - ... 通过:   133 ... Tianming.AI.Tests.dll ...
已通过! - ... 通过:   781 ... Tianming.Framework.Tests.dll ...
```

任一项未过：停止，先排查，不进 Task 1。

---

## Task 1：加 ONNX 与 Tokenizer NuGet 依赖

**Files:**
- Modify: `src/Tianming.AI/Tianming.AI.csproj`

- [ ] **Step 1.1：编辑 csproj 加包引用**

在 `src/Tianming.AI/Tianming.AI.csproj` 的 `<ItemGroup>` 区（紧跟 `<ProjectReference>` 那一段之后、`<Compile Include ...>` 那一段之前）插入新 `<ItemGroup>`：

```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.ML.OnnxRuntime" Version="1.18.1" />
    <PackageReference Include="FastBertTokenizer" Version="1.0.28" />
  </ItemGroup>
```

- [ ] **Step 1.2：还原并确认包可拉到**

```bash
dotnet restore src/Tianming.AI/Tianming.AI.csproj
```

Expected: 无 error；下载 `Microsoft.ML.OnnxRuntime` 1.18.1 和 `FastBertTokenizer` 1.0.28（版本号或补丁号可能略有不同，只要同 major.minor 稳定版即可）。

若 `FastBertTokenizer` 1.0.28 在当日 NuGet 上不可用，改用最近一个 1.0.x 稳定版（NuGet 搜 "FastBertTokenizer" 取 Latest stable）。记录实际用的版本号。

- [ ] **Step 1.3：build + test 基线不退化**

```bash
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -8
```

Expected: 1132 全过，0 warning 0 error。

- [ ] **Step 1.4：commit**

```bash
git add src/Tianming.AI/Tianming.AI.csproj
git commit -m "chore(ai): 加入 Microsoft.ML.OnnxRuntime + FastBertTokenizer"
```

---

## Task 2：EmbeddingSettings（配置记录 + 校验）

**Files:**
- Create: `src/Tianming.AI/SemanticKernel/Embedding/EmbeddingSettings.cs`
- Create: `tests/Tianming.AI.Tests/EmbeddingSettingsTests.cs`
- Modify: `src/Tianming.AI/Tianming.AI.csproj`

- [ ] **Step 2.1：写失败的测试**

创建 `tests/Tianming.AI.Tests/EmbeddingSettingsTests.cs`：

```csharp
using System;
using System.IO;
using TM.Services.Framework.AI.SemanticKernel;
using Xunit;

namespace Tianming.AI.Tests;

public class EmbeddingSettingsTests
{
    [Fact]
    public void Default_ShouldUseHashingFallback()
    {
        var s = EmbeddingSettings.Default;
        Assert.Null(s.ModelFilePath);
        Assert.Null(s.VocabFilePath);
        Assert.Equal(256, s.HashingDimension);
        Assert.Equal(512, s.MaxSequenceLength);
    }

    [Fact]
    public void Validate_WithModelButNoVocab_ReturnsError()
    {
        var s = EmbeddingSettings.Default with { ModelFilePath = "/tmp/x.onnx" };
        var result = s.Validate();
        Assert.False(result.IsValid);
        Assert.Contains("VocabFilePath", result.ErrorMessage);
    }

    [Fact]
    public void Validate_WithVocabButNoModel_ReturnsError()
    {
        var s = EmbeddingSettings.Default with { VocabFilePath = "/tmp/vocab.txt" };
        var result = s.Validate();
        Assert.False(result.IsValid);
        Assert.Contains("ModelFilePath", result.ErrorMessage);
    }

    [Fact]
    public void Validate_WithNonExistingFiles_ReturnsError()
    {
        var s = EmbeddingSettings.Default with
        {
            ModelFilePath = "/tmp/does-not-exist-12345.onnx",
            VocabFilePath = "/tmp/does-not-exist-12345.txt"
        };
        var result = s.Validate();
        Assert.False(result.IsValid);
        Assert.Contains("不存在", result.ErrorMessage);
    }

    [Fact]
    public void Validate_WithExistingFiles_ReturnsValid()
    {
        var model = Path.GetTempFileName();
        var vocab = Path.GetTempFileName();
        try
        {
            var s = EmbeddingSettings.Default with
            {
                ModelFilePath = model,
                VocabFilePath = vocab
            };
            var result = s.Validate();
            Assert.True(result.IsValid);
            Assert.Equal(string.Empty, result.ErrorMessage);
        }
        finally
        {
            File.Delete(model);
            File.Delete(vocab);
        }
    }

    [Fact]
    public void Validate_WithNeitherFile_ReturnsValid_BecauseFallbackIsUsed()
    {
        var result = EmbeddingSettings.Default.Validate();
        Assert.True(result.IsValid);
    }
}
```

- [ ] **Step 2.2：跑测试确认失败**

```bash
dotnet test tests/Tianming.AI.Tests/Tianming.AI.Tests.csproj --nologo -v q 2>&1 | tail -6
```

Expected: 编译错误（`EmbeddingSettings` 类型不存在）。

- [ ] **Step 2.3：写最小实现**

创建 `src/Tianming.AI/SemanticKernel/Embedding/EmbeddingSettings.cs`：

```csharp
using System.IO;

namespace TM.Services.Framework.AI.SemanticKernel;

public sealed record EmbeddingSettings
{
    public static EmbeddingSettings Default { get; } = new();

    public string? ModelFilePath { get; init; }
    public string? VocabFilePath { get; init; }
    public int HashingDimension { get; init; } = 256;
    public int MaxSequenceLength { get; init; } = 512;
    public int OutputDimension { get; init; } = 512;

    public EmbeddingSettingsValidationResult Validate()
    {
        var hasModel = !string.IsNullOrWhiteSpace(ModelFilePath);
        var hasVocab = !string.IsNullOrWhiteSpace(VocabFilePath);

        if (!hasModel && !hasVocab)
            return EmbeddingSettingsValidationResult.Valid();

        if (hasModel && !hasVocab)
            return EmbeddingSettingsValidationResult.Invalid("配置了 ModelFilePath 但未配置 VocabFilePath。");

        if (!hasModel && hasVocab)
            return EmbeddingSettingsValidationResult.Invalid("配置了 VocabFilePath 但未配置 ModelFilePath。");

        if (!File.Exists(ModelFilePath))
            return EmbeddingSettingsValidationResult.Invalid($"ModelFilePath 指向的文件不存在：{ModelFilePath}");

        if (!File.Exists(VocabFilePath))
            return EmbeddingSettingsValidationResult.Invalid($"VocabFilePath 指向的文件不存在：{VocabFilePath}");

        return EmbeddingSettingsValidationResult.Valid();
    }
}

public sealed record EmbeddingSettingsValidationResult(bool IsValid, string ErrorMessage)
{
    public static EmbeddingSettingsValidationResult Valid() => new(true, string.Empty);
    public static EmbeddingSettingsValidationResult Invalid(string msg) => new(false, msg);
}
```

- [ ] **Step 2.4：在 csproj 加 Compile Include**

编辑 `src/Tianming.AI/Tianming.AI.csproj`，在 `<Compile Include="SemanticKernel/Embedding/HashingTextEmbedder.cs" />` 之后插入一行：

```xml
    <Compile Include="SemanticKernel/Embedding/EmbeddingSettings.cs" />
```

- [ ] **Step 2.5：跑测试确认通过**

```bash
dotnet test tests/Tianming.AI.Tests/Tianming.AI.Tests.csproj --nologo --filter "EmbeddingSettings" -v q 2>&1 | tail -6
```

Expected: 6 个 EmbeddingSettings 测试全过。

- [ ] **Step 2.6：跑全量测试确认未退化**

```bash
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -6
```

Expected: ProjectData 218、AI 139（133 原有 + 6 新增）、Framework 781 全过。

- [ ] **Step 2.7：commit**

```bash
git add src/Tianming.AI/SemanticKernel/Embedding/EmbeddingSettings.cs \
        src/Tianming.AI/Tianming.AI.csproj \
        tests/Tianming.AI.Tests/EmbeddingSettingsTests.cs
git commit -m "feat(ai): EmbeddingSettings 配置与校验"
```

---

## Task 3：FakeTextEmbedder（工厂测试替身）

**Files:**
- Create: `tests/Tianming.AI.Tests/TestSupport/FakeTextEmbedder.cs`

只供 `OnnxEmbedderFactoryTests` 用，简单的确定性 stub。

- [ ] **Step 3.1：写 stub**

创建 `tests/Tianming.AI.Tests/TestSupport/FakeTextEmbedder.cs`：

```csharp
using System;
using TM.Services.Framework.AI.SemanticKernel;

namespace Tianming.AI.Tests.TestSupport;

internal sealed class FakeTextEmbedder : ITextEmbedder
{
    public string Name { get; }
    public bool Disposed { get; private set; }

    public FakeTextEmbedder(string name) { Name = name; }

    public float[] Embed(string text)
        => new[] { (float)text.Length, (float)Name.GetHashCode() };

    public double Similarity(float[] a, float[] b) => 0.0;

    public void Dispose() { Disposed = true; }
}
```

- [ ] **Step 3.2：build 确认无错（tests csproj 用 glob，新文件自动编译）**

```bash
dotnet build tests/Tianming.AI.Tests/Tianming.AI.Tests.csproj --nologo -v q 2>&1 | tail -4
```

Expected: 0 error 0 warning。

- [ ] **Step 3.3：commit**

```bash
git add tests/Tianming.AI.Tests/TestSupport/FakeTextEmbedder.cs
git commit -m "test(ai): FakeTextEmbedder stub for factory tests"
```

---

## Task 4：OnnxTextEmbedder（真 ONNX 实现）

**Files:**
- Create: `src/Tianming.AI/SemanticKernel/Embedding/OnnxTextEmbedder.cs`
- Modify: `src/Tianming.AI/Tianming.AI.csproj`

`OnnxTextEmbedder` 的职责：
- 构造器接受 `EmbeddingSettings`；在非法配置时抛 `InvalidOperationException`（由工厂层拦截并降级到 Hashing）
- 懒加载 `InferenceSession` 与 `BertTokenizer`
- `Embed(text)`：tokenize → 构造 `input_ids` / `attention_mask` / `token_type_ids` → 跑 session → mean pooling → L2 归一化 → 返回 `float[]`
- `Similarity(a, b)`：点积（归一化后等价于 cosine）
- `Dispose`：释放 session

Embedder 不做单测（依赖真模型文件），行为由 Task 5 的工厂单测覆盖"实例化/降级"路径。真推理在 Task 6 的手工联调一并验收。

- [ ] **Step 4.1：写实现**

创建 `src/Tianming.AI/SemanticKernel/Embedding/OnnxTextEmbedder.cs`：

```csharp
using System;
using System.IO;
using System.Linq;
using FastBertTokenizer;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace TM.Services.Framework.AI.SemanticKernel;

public sealed class OnnxTextEmbedder : ITextEmbedder
{
    private readonly EmbeddingSettings _settings;
    private readonly Lazy<(InferenceSession Session, BertTokenizer Tokenizer)> _lazy;

    public OnnxTextEmbedder(EmbeddingSettings settings)
    {
        var v = settings.Validate();
        if (!v.IsValid || string.IsNullOrWhiteSpace(settings.ModelFilePath))
            throw new InvalidOperationException($"EmbeddingSettings 无效或缺失：{v.ErrorMessage}");

        _settings = settings;
        _lazy = new Lazy<(InferenceSession, BertTokenizer)>(() =>
        {
            var session = new InferenceSession(settings.ModelFilePath);
            var tokenizer = new BertTokenizer();
            using (var reader = File.OpenText(settings.VocabFilePath!))
                tokenizer.LoadVocabulary(reader, convertInputToLowercase: true);
            return (session, tokenizer);
        });
    }

    public float[] Embed(string text)
    {
        var (session, tokenizer) = _lazy.Value;
        var maxLen = _settings.MaxSequenceLength;

        var encoded = tokenizer.Encode(text ?? string.Empty, maxLen);
        var inputIds = encoded.InputIds.Select(i => (long)i).ToArray();
        var attentionMask = encoded.AttentionMask.Select(i => (long)i).ToArray();
        var tokenTypeIds = new long[inputIds.Length];

        var shape = new[] { 1L, inputIds.Length };
        using var inputs = new[]
        {
            NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(inputIds, shape)),
            NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(attentionMask, shape)),
            NamedOnnxValue.CreateFromTensor("token_type_ids", new DenseTensor<long>(tokenTypeIds, shape)),
        };

        using var outputs = session.Run(inputs);
        var hidden = outputs.First().AsTensor<float>();

        var seqLen = (int)hidden.Dimensions[1];
        var dim = (int)hidden.Dimensions[2];
        var pooled = new float[dim];
        var divisor = 0f;

        for (var t = 0; t < seqLen; t++)
        {
            if (attentionMask[t] == 0) continue;
            divisor += 1f;
            for (var d = 0; d < dim; d++)
                pooled[d] += hidden[0, t, d];
        }

        if (divisor > 0)
            for (var d = 0; d < dim; d++) pooled[d] /= divisor;

        var norm = (float)Math.Sqrt(pooled.Sum(x => x * x));
        if (norm > 1e-12f)
            for (var d = 0; d < dim; d++) pooled[d] /= norm;

        return pooled;
    }

    public double Similarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0.0;
        var s = 0.0;
        for (var i = 0; i < a.Length; i++) s += a[i] * b[i];
        return s;
    }

    public void Dispose()
    {
        if (_lazy.IsValueCreated) _lazy.Value.Session.Dispose();
    }
}
```

注：`BertTokenizer.LoadVocabulary` / `Encode` 的具体签名取决于 `FastBertTokenizer` 实际版本。若 API 有小差异（如 `Encode` 返回 `IReadOnlyList<long>` 而非 `IReadOnlyList<int>`），按编译错误指示小幅调整；核心骨架不变。

若 `BertTokenizer` 没有 `LoadVocabulary(TextReader, bool)` 重载，使用包提供的等效 API（通常有 `Load(IEnumerable<string> vocab)` 一类入口，读 `vocab.txt` 行数组喂进去）。

- [ ] **Step 4.2：在 csproj 加 Compile Include**

编辑 `src/Tianming.AI/Tianming.AI.csproj`，在 `<Compile Include="SemanticKernel/Embedding/EmbeddingSettings.cs" />` 之后插入：

```xml
    <Compile Include="SemanticKernel/Embedding/OnnxTextEmbedder.cs" />
```

- [ ] **Step 4.3：build 确认无错**

```bash
dotnet build src/Tianming.AI/Tianming.AI.csproj --nologo -v q 2>&1 | tail -6
```

Expected: 0 error 0 warning。

若编译失败因 `BertTokenizer` API 不匹配：打开 `~/.nuget/packages/fastberttokenizer/<version>/lib/net6.0/` 下的 .xml/.dll，用 `dotnet run` 或 IDE 看实际签名，调整对应几行代码；不改整体设计。

- [ ] **Step 4.4：跑全量测试未退化**

```bash
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -6
```

Expected: 1138 全过（新增 6 条来自 Task 2）。

- [ ] **Step 4.5：commit**

```bash
git add src/Tianming.AI/SemanticKernel/Embedding/OnnxTextEmbedder.cs \
        src/Tianming.AI/Tianming.AI.csproj
git commit -m "feat(ai): OnnxTextEmbedder（mean pooling + L2 归一化）"
```

---

## Task 5：OnnxEmbedderFactory（配置 → 选择 embedder）

**Files:**
- Create: `src/Tianming.AI/SemanticKernel/Embedding/OnnxEmbedderFactory.cs`
- Create: `tests/Tianming.AI.Tests/OnnxEmbedderFactoryTests.cs`
- Modify: `src/Tianming.AI/Tianming.AI.csproj`

工厂行为：
- 有效 `EmbeddingSettings`（模型 + vocab 都存在） → 返回 `OnnxTextEmbedder`
- 无模型文件 → 返回 `HashingTextEmbedder(settings.HashingDimension)`
- 配置非法但部分填（比如只写了 ModelFilePath） → 日志告警 + 回退 Hashing
- 捕获 `OnnxTextEmbedder` 构造器/懒加载异常 → 回退 Hashing（比如模型格式损坏）

- [ ] **Step 5.1：写失败的测试**

创建 `tests/Tianming.AI.Tests/OnnxEmbedderFactoryTests.cs`：

```csharp
using System;
using System.IO;
using TM.Services.Framework.AI.SemanticKernel;
using Xunit;

namespace Tianming.AI.Tests;

public class OnnxEmbedderFactoryTests
{
    [Fact]
    public void Create_WithDefaultSettings_ReturnsHashingEmbedder()
    {
        var embedder = OnnxEmbedderFactory.Create(EmbeddingSettings.Default);
        Assert.IsType<HashingTextEmbedder>(embedder);
    }

    [Fact]
    public void Create_WithPartialConfig_FallsBackToHashing()
    {
        var s = EmbeddingSettings.Default with { ModelFilePath = "/tmp/only-model.onnx" };
        var embedder = OnnxEmbedderFactory.Create(s);
        Assert.IsType<HashingTextEmbedder>(embedder);
    }

    [Fact]
    public void Create_WithNonExistingModelFile_FallsBackToHashing()
    {
        var s = EmbeddingSettings.Default with
        {
            ModelFilePath = "/tmp/does-not-exist-98765.onnx",
            VocabFilePath = "/tmp/does-not-exist-98765.txt"
        };
        var embedder = OnnxEmbedderFactory.Create(s);
        Assert.IsType<HashingTextEmbedder>(embedder);
    }

    [Fact]
    public void Create_WithCorruptedModelFile_FallsBackToHashing()
    {
        var model = Path.GetTempFileName() + ".onnx";
        var vocab = Path.GetTempFileName();
        File.WriteAllText(model, "this is not a valid onnx file");
        File.WriteAllLines(vocab, new[] { "[PAD]", "[UNK]", "[CLS]", "[SEP]", "hello" });
        try
        {
            var s = EmbeddingSettings.Default with
            {
                ModelFilePath = model,
                VocabFilePath = vocab
            };
            var embedder = OnnxEmbedderFactory.Create(s);
            // 模型损坏时构造器应抛错被捕获，工厂回退到 Hashing
            Assert.IsType<HashingTextEmbedder>(embedder);
        }
        finally
        {
            File.Delete(model);
            File.Delete(vocab);
        }
    }

    [Fact]
    public void Create_RespectsCustomHashingDimension()
    {
        var s = EmbeddingSettings.Default with { HashingDimension = 128 };
        var embedder = OnnxEmbedderFactory.Create(s);
        var vec = embedder.Embed("hello world");
        Assert.Equal(128, vec.Length);
    }
}
```

- [ ] **Step 5.2：跑测试确认失败**

```bash
dotnet test tests/Tianming.AI.Tests/Tianming.AI.Tests.csproj --nologo --filter "OnnxEmbedderFactory" -v q 2>&1 | tail -4
```

Expected: 编译错误（`OnnxEmbedderFactory` 不存在）。

- [ ] **Step 5.3：写最小实现**

创建 `src/Tianming.AI/SemanticKernel/Embedding/OnnxEmbedderFactory.cs`：

```csharp
using System;

namespace TM.Services.Framework.AI.SemanticKernel;

public static class OnnxEmbedderFactory
{
    public static ITextEmbedder Create(EmbeddingSettings settings)
    {
        var v = settings.Validate();
        if (!v.IsValid || string.IsNullOrWhiteSpace(settings.ModelFilePath))
            return new HashingTextEmbedder(settings.HashingDimension);

        try
        {
            var onnx = new OnnxTextEmbedder(settings);
            // 触发懒加载，确保模型真能打开；若抛错立即捕获降级
            _ = onnx.Embed("");
            return onnx;
        }
        catch
        {
            return new HashingTextEmbedder(settings.HashingDimension);
        }
    }
}
```

- [ ] **Step 5.4：在 csproj 加 Compile Include**

编辑 `src/Tianming.AI/Tianming.AI.csproj`，在 `<Compile Include="SemanticKernel/Embedding/OnnxTextEmbedder.cs" />` 之后插入：

```xml
    <Compile Include="SemanticKernel/Embedding/OnnxEmbedderFactory.cs" />
```

- [ ] **Step 5.5：跑测试确认通过**

```bash
dotnet test tests/Tianming.AI.Tests/Tianming.AI.Tests.csproj --nologo --filter "OnnxEmbedderFactory" -v q 2>&1 | tail -4
```

Expected: 5 个 OnnxEmbedderFactory 测试全过。

- [ ] **Step 5.6：跑全量测试**

```bash
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -6
```

Expected: 1143 全过（218 + 144 + 781；AI 层 133 + 6 + 5 = 144）。

- [ ] **Step 5.7：commit**

```bash
git add src/Tianming.AI/SemanticKernel/Embedding/OnnxEmbedderFactory.cs \
        src/Tianming.AI/Tianming.AI.csproj \
        tests/Tianming.AI.Tests/OnnxEmbedderFactoryTests.cs
git commit -m "feat(ai): OnnxEmbedderFactory（缺模型降级到 Hashing）"
```

---

## Task 6：OpenAI-compatible 手工联调脚本

**Files:**
- Create: `Scripts/m2-openai-smoke.csx`
- Create: `Docs/macOS迁移/M2-联调笔记.md`

`OpenAICompatibleChatClient` 已在 `src/Tianming.AI/Core/OpenAICompatibleChatClient.cs` 存在。这一步只是给用户一个可执行的验收脚本，证明真实 endpoint 可通。

- [ ] **Step 6.1：写 `dotnet script` 方式的联调脚本**

创建 `Scripts/m2-openai-smoke.csx`：

```csharp
#r "../src/Tianming.AI/bin/Debug/net8.0/Tianming.AI.dll"
#r "../src/Tianming.AI/bin/Debug/net8.0/Tianming.ProjectData.dll"

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Framework.AI.Core;

// 读取配置：$HOME/.tianming/smoke.json
//   { "BaseUrl": "https://api.deepseek.com", "ApiKey": "sk-...", "Model": "deepseek-chat" }
var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
var cfgPath = Path.Combine(home, ".tianming", "smoke.json");
if (!File.Exists(cfgPath))
{
    Console.Error.WriteLine($"请先创建 {cfgPath}，内容示例：");
    Console.Error.WriteLine("{ \"BaseUrl\": \"https://api.deepseek.com\", \"ApiKey\": \"sk-xxx\", \"Model\": \"deepseek-chat\" }");
    Environment.Exit(1);
}

using var cfgStream = File.OpenRead(cfgPath);
var cfg = JsonSerializer.Deserialize<JsonElement>(cfgStream);
var baseUrl = cfg.GetProperty("BaseUrl").GetString()!;
var apiKey  = cfg.GetProperty("ApiKey").GetString()!;
var model   = cfg.GetProperty("Model").GetString()!;

using var http = new HttpClient();
var client = new OpenAICompatibleChatClient(http);

var request = new OpenAICompatibleChatRequest
{
    BaseUrl = baseUrl,
    ApiKey = apiKey,
    Model = model,
    Temperature = 0.3,
    MaxTokens = 256,
    Messages =
    {
        new OpenAICompatibleChatMessage("system", "你是天命 macOS 迁移项目的冒烟测试助手，请用一句话回复。"),
        new OpenAICompatibleChatMessage("user", "今天是 2026 年 5 月 12 日，请只回复：收到。")
    }
};

Console.WriteLine("=== 非流式 ===");
var result = await client.ChatAsync(request, CancellationToken.None);
Console.WriteLine($"Success={result.Success} Status={result.StatusCode}");
Console.WriteLine($"Content: {result.Content}");
Console.WriteLine($"Tokens: prompt={result.PromptTokens} completion={result.CompletionTokens} total={result.TotalTokens}");
if (!string.IsNullOrEmpty(result.ErrorMessage))
    Console.WriteLine($"Error: {result.ErrorMessage}");

Console.WriteLine("\n=== 流式 ===");
var chunks = 0;
await foreach (var chunk in client.StreamAsync(request, CancellationToken.None))
{
    Console.Write(chunk.Content);
    chunks++;
    if (chunk.FinishReason is not null)
        Console.WriteLine($"\n[finish_reason={chunk.FinishReason}]");
}
Console.WriteLine($"\n收到 {chunks} 个流式分片。");
```

注意：脚本里调用的是 `OpenAICompatibleChatClient.ChatAsync` / `StreamAsync`。如果这俩方法名与真实类不一致，按真实类签名调整（实际类见 `src/Tianming.AI/Core/OpenAICompatibleChatClient.cs`）。

- [ ] **Step 6.2：确认 OpenAICompatibleChatClient 的公开 API 对齐**

```bash
grep -n "public.*Task\|public.*IAsyncEnumerable" src/Tianming.AI/Core/OpenAICompatibleChatClient.cs
```

若公开的是 `SendAsync` 而非 `ChatAsync`，或流式方法叫 `StreamChatAsync`，按实际修改脚本里的方法名。

- [ ] **Step 6.3：build 确保 DLL 存在**

```bash
dotnet build src/Tianming.AI/Tianming.AI.csproj --nologo -v q 2>&1 | tail -4
```

Expected: 0 error；`src/Tianming.AI/bin/Debug/net8.0/Tianming.AI.dll` 存在。

- [ ] **Step 6.4：准备本机配置并跑脚本**

```bash
mkdir -p ~/.tianming
cat > ~/.tianming/smoke.json <<'EOF'
{
  "BaseUrl": "https://api.deepseek.com",
  "ApiKey": "REPLACE-WITH-YOUR-KEY",
  "Model": "deepseek-chat"
}
EOF
# 编辑 ~/.tianming/smoke.json 填入真实 key
```

若没装 `dotnet-script`：
```bash
dotnet tool install -g dotnet-script
```

运行：
```bash
dotnet script Scripts/m2-openai-smoke.csx
```

Expected:
- `Success=True Status=200`
- `Content: 收到。`（或类似）
- Tokens 三项均为正整数
- 流式分片数 ≥ 1

若失败：看错误信息定位（网络、key、model 名、endpoint 格式）。排障不算代码缺陷，这个脚本的作用就是暴露配置问题。

`~/.tianming/smoke.json` 已被 gitignore（仓库根 `.gitignore` 已含 `~` 排除逻辑或需补）——下一步检查。

- [ ] **Step 6.5：确认 smoke.json 不会进入 git**

```bash
git status --short
```

Expected: `smoke.json` 不在家目录下，不会被 repo 追踪；只应看到 `Scripts/m2-openai-smoke.csx` 和 `Docs/macOS迁移/M2-联调笔记.md`。

- [ ] **Step 6.6：写联调笔记**

创建 `Docs/macOS迁移/M2-联调笔记.md`：

```markdown
# 天命 macOS M2 联调笔记

日期：2026-05-12

## OpenAI-compatible 冒烟

脚本：`Scripts/m2-openai-smoke.csx`
配置：`~/.tianming/smoke.json`（本机不入库）

### 验证记录

| 日期 | Provider | Model | 非流式 | 流式 | 备注 |
|---|---|---|---|---|---|
| 2026-05-12 | <填> | <填> | <填写"通过/失败"> | <填写"通过/失败"> | <填> |

### 常见问题

- 401 鉴权失败：确认 Bearer header 写法与 `ApiKey` 值是否完整
- 404：endpoint 路径是否以 `/v1/chat/completions` 为后缀？`OpenAICompatibleChatClient` 会自动归一化 BaseUrl
- SSE 卡顿：看 `Transfer-Encoding: chunked` 是否被中间代理剥掉

## ONNX 真向量

放模型文件到：`~/Library/Application Support/Tianming/Models/`

推荐模型（手动下载）：
- `bge-small-zh-v1.5`：~90MB，512 维，中文友好
  - `pytorch_model.bin` → 用 transformers 导出成 onnx；或 HuggingFace 上搜已转好的版本
  - `vocab.txt` 必需

### 验证记录

| 日期 | 模型名 | ModelFilePath | 输出维度 | 备注 |
|---|---|---|---|---|
| 2026-05-12 | <填> | <填> | <填> | <填> |
```

- [ ] **Step 6.7：commit**

```bash
git add Scripts/m2-openai-smoke.csx Docs/macOS迁移/M2-联调笔记.md
git commit -m "chore(m2): OpenAI-compatible 联调脚本 + 联调笔记模板"
```

---

## Task 7：最终收尾

- [ ] **Step 7.1：全量测试最后一次**

```bash
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -6
```

Expected: 1143 全过。

- [ ] **Step 7.2：Windows 绑定扫描**

```bash
Scripts/check-windows-bindings.sh src/ tests/ 2>&1 | tail -5
```

Expected: 0 命中。

- [ ] **Step 7.3：查看 commit 历史**

```bash
git log --oneline -10
```

Expected 看到 6 个新 commit（Task 1-6 各 1 个；Task 3 的 stub 只用测试包含不单独 commit 也可以合并到 Task 5；此处按 Task 3 已分离 commit 计算）。

- [ ] **Step 7.4：推送分支（按需）**

```bash
git push -u origin m2/onnx-localfetch-2026-05-12
```

> 自用场景可以先不 push，等 M2 验收完再 push 到远端。如果只在本机用，干脆不 push。

---

## 验收标准

1. `dotnet build Tianming.MacMigration.sln` → 0 Warning / 0 Error
2. `dotnet test Tianming.MacMigration.sln` → 1143 全过（6 个 EmbeddingSettings + 5 个 OnnxEmbedderFactory 新增）
3. `Scripts/check-windows-bindings.sh src/ tests/` → 0 命中
4. 放一个真实 ONNX 模型到 `~/Library/Application Support/Tianming/Models/` 并配置 `EmbeddingSettings`，能跑出向量（在联调脚本或自写 C# 里手动验证）
5. 不放模型时 `OnnxEmbedderFactory.Create(EmbeddingSettings.Default)` 返回 `HashingTextEmbedder`，既有 1132 测试全过
6. `Scripts/m2-openai-smoke.csx` 用你自己的 OpenAI-compatible endpoint 跑通非流式 + 流式
7. 分支 `m2/onnx-localfetch-2026-05-12` 上有 6 个干净的 commit

完成后：进入 M3 Avalonia Shell（在另一个 worktree）。
