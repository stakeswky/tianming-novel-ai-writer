# M6.8 一键成书 + 内容提炼 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 全书级 10 步流水线编排：设计 → 大纲 → 分卷 → 章节规划 → 蓝图 → 生成 → Humanize → Gate → 保存 → 索引。每步可断点续跑、单步重跑、单步跳过。整个流程作为一个 `BookGenerationOrchestrator`，复用 M4.4 的 `ChapterGenerationPipeline` 做章节级处理，外层负责多章节循环 + 状态追踪。

**Architecture:** 新增 `IBookPipelineStep` 接口，10 个实现各代表一个步骤。`BookGenerationOrchestrator` 持有 `IReadOnlyList<IBookPipelineStep>` 按顺序执行，每步前后查 `BookGenerationJournal`（沿用 M6.3 的 jsonl 设计）做 skip/retry 判断。新增 `BookPipelineViewModel` 显示当前进度、控制 Start/Pause/SkipStep/RetryStep。UI 入口：Dashboard 加"一键成书"按钮，跳转新的 `BookPipelinePage`。

**Tech Stack:** .NET 8 + 复用 `ChapterGenerationPipeline` / `IGenerationJournal`（M6.3）/ Avalonia + xUnit。零新依赖。

**Branch:** `m6-8-one-click-book`（基于 main）。

**前置条件：** Round 2/3 + M6.2-M6.7 已合并入 main 并基线绿（特别是 M6.3 WAL 必须先做）。

---

## Task 0：基线 + worktree

```bash
cd /Users/jimmy/Downloads/tianming-novel-ai-writer
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -10
git worktree add /Users/jimmy/Downloads/tianming-m6-8 -b m6-8-one-click-book main
cd /Users/jimmy/Downloads/tianming-m6-8
```

---

## Task 1：BookPipelineStep 枚举 + IBookPipelineStep 接口

**Files:**
- Create: `src/Tianming.ProjectData/BookPipeline/IBookPipelineStep.cs`
- Create: `src/Tianming.ProjectData/BookPipeline/BookPipelineStepName.cs`
- Test: `tests/Tianming.ProjectData.Tests/BookPipeline/BookPipelineStepNameTests.cs`

- [ ] **Step 1.1：测试**

```csharp
using TM.Services.Modules.ProjectData.BookPipeline;
using Xunit;

namespace Tianming.ProjectData.Tests.BookPipeline;

public class BookPipelineStepNameTests
{
    [Fact]
    public void Ten_step_names_defined()
    {
        var names = new[]
        {
            BookPipelineStepName.Design,
            BookPipelineStepName.Outline,
            BookPipelineStepName.Volume,
            BookPipelineStepName.ChapterPlanning,
            BookPipelineStepName.Blueprint,
            BookPipelineStepName.Generate,
            BookPipelineStepName.Humanize,
            BookPipelineStepName.Gate,
            BookPipelineStepName.Save,
            BookPipelineStepName.Index,
        };
        Assert.Equal(10, names.Length);
        Assert.Equal("Design", names[0]);
    }
}
```

- [ ] **Step 1.2：实现**

`BookPipelineStepName.cs`:

```csharp
namespace TM.Services.Modules.ProjectData.BookPipeline
{
    public static class BookPipelineStepName
    {
        public const string Design = "Design";
        public const string Outline = "Outline";
        public const string Volume = "Volume";
        public const string ChapterPlanning = "ChapterPlanning";
        public const string Blueprint = "Blueprint";
        public const string Generate = "Generate";
        public const string Humanize = "Humanize";
        public const string Gate = "Gate";
        public const string Save = "Save";
        public const string Index = "Index";
    }
}
```

`IBookPipelineStep.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.BookPipeline
{
    public interface IBookPipelineStep
    {
        string Name { get; }
        Task<BookStepResult> ExecuteAsync(BookPipelineContext context, CancellationToken ct = default);
    }

    public sealed class BookPipelineContext
    {
        public string ProjectRoot { get; set; } = string.Empty;
        public System.Collections.Generic.List<string> ChapterIds { get; set; } = new();
        public System.Collections.Generic.Dictionary<string, string> Scratchpad { get; set; } = new();
    }

    public sealed class BookStepResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public System.Collections.Generic.IReadOnlyList<string> ProcessedChapterIds { get; set; }
            = System.Array.Empty<string>();
        public string? PayloadJson { get; set; }
    }
}
```

- [ ] **Step 1.3：commit**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter BookPipelineStepNameTests --nologo -v minimal
git add src/Tianming.ProjectData/BookPipeline/IBookPipelineStep.cs \
        src/Tianming.ProjectData/BookPipeline/BookPipelineStepName.cs \
        tests/Tianming.ProjectData.Tests/BookPipeline/BookPipelineStepNameTests.cs
git commit -m "feat(book): M6.8.1 IBookPipelineStep + 10 step name 常量"
```

---

## Task 2：BookGenerationJournal（jsonl 复用 M6.3 模式）

**Files:**
- Create: `src/Tianming.ProjectData/BookPipeline/IBookGenerationJournal.cs`
- Create: `src/Tianming.ProjectData/BookPipeline/FileBookGenerationJournal.cs`
- Test: `tests/Tianming.ProjectData.Tests/BookPipeline/FileBookGenerationJournalTests.cs`

- [ ] **Step 2.1：测试**

```csharp
using System.IO;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.BookPipeline;
using Xunit;

namespace Tianming.ProjectData.Tests.BookPipeline;

public class FileBookGenerationJournalTests
{
    [Fact]
    public async Task Records_step_completion()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-bookj-{System.Guid.NewGuid():N}");
        var journal = new FileBookGenerationJournal(dir);
        await journal.RecordCompletedAsync(BookPipelineStepName.Design);
        Assert.True(await journal.IsCompletedAsync(BookPipelineStepName.Design));
        Assert.False(await journal.IsCompletedAsync(BookPipelineStepName.Outline));
    }

    [Fact]
    public async Task Skip_marks_step_as_skipped_not_completed()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-bookj-{System.Guid.NewGuid():N}");
        var journal = new FileBookGenerationJournal(dir);
        await journal.MarkSkippedAsync(BookPipelineStepName.Humanize);
        Assert.True(await journal.IsSkippedAsync(BookPipelineStepName.Humanize));
        Assert.False(await journal.IsCompletedAsync(BookPipelineStepName.Humanize));
    }

    [Fact]
    public async Task Reset_clears_step()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-bookj-{System.Guid.NewGuid():N}");
        var journal = new FileBookGenerationJournal(dir);
        await journal.RecordCompletedAsync(BookPipelineStepName.Design);
        await journal.ResetAsync(BookPipelineStepName.Design);
        Assert.False(await journal.IsCompletedAsync(BookPipelineStepName.Design));
    }
}
```

- [ ] **Step 2.2：实现**

```csharp
// IBookGenerationJournal.cs
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.BookPipeline
{
    public interface IBookGenerationJournal
    {
        Task<bool> IsCompletedAsync(string stepName, CancellationToken ct = default);
        Task<bool> IsSkippedAsync(string stepName, CancellationToken ct = default);
        Task RecordCompletedAsync(string stepName, CancellationToken ct = default);
        Task MarkSkippedAsync(string stepName, CancellationToken ct = default);
        Task ResetAsync(string stepName, CancellationToken ct = default);
    }
}
```

```csharp
// FileBookGenerationJournal.cs
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.BookPipeline
{
    public sealed class FileBookGenerationJournal : IBookGenerationJournal
    {
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        private readonly string _filePath;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public FileBookGenerationJournal(string projectRoot)
        {
            var dir = Path.Combine(projectRoot, ".book");
            Directory.CreateDirectory(dir);
            _filePath = Path.Combine(dir, "pipeline.json");
        }

        private async Task<Dictionary<string, string>> LoadAsync(CancellationToken ct)
        {
            if (!File.Exists(_filePath)) return new Dictionary<string, string>();
            var json = await File.ReadAllTextAsync(_filePath, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOpts) ?? new();
        }

        private async Task SaveAsync(Dictionary<string, string> map, CancellationToken ct)
        {
            var json = JsonSerializer.Serialize(map, JsonOpts);
            var tmp = _filePath + ".tmp";
            await File.WriteAllTextAsync(tmp, json, ct).ConfigureAwait(false);
            File.Move(tmp, _filePath, overwrite: true);
        }

        public async Task<bool> IsCompletedAsync(string stepName, CancellationToken ct = default)
        {
            var map = await LoadAsync(ct).ConfigureAwait(false);
            return map.TryGetValue(stepName, out var status) && status == "Completed";
        }

        public async Task<bool> IsSkippedAsync(string stepName, CancellationToken ct = default)
        {
            var map = await LoadAsync(ct).ConfigureAwait(false);
            return map.TryGetValue(stepName, out var status) && status == "Skipped";
        }

        public async Task RecordCompletedAsync(string stepName, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var map = await LoadAsync(ct).ConfigureAwait(false);
                map[stepName] = "Completed";
                await SaveAsync(map, ct).ConfigureAwait(false);
            }
            finally { _lock.Release(); }
        }

        public async Task MarkSkippedAsync(string stepName, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var map = await LoadAsync(ct).ConfigureAwait(false);
                map[stepName] = "Skipped";
                await SaveAsync(map, ct).ConfigureAwait(false);
            }
            finally { _lock.Release(); }
        }

        public async Task ResetAsync(string stepName, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var map = await LoadAsync(ct).ConfigureAwait(false);
                map.Remove(stepName);
                await SaveAsync(map, ct).ConfigureAwait(false);
            }
            finally { _lock.Release(); }
        }
    }
}
```

- [ ] **Step 2.3：commit**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter FileBookGenerationJournalTests --nologo -v minimal
git add src/Tianming.ProjectData/BookPipeline/IBookGenerationJournal.cs \
        src/Tianming.ProjectData/BookPipeline/FileBookGenerationJournal.cs \
        tests/Tianming.ProjectData.Tests/BookPipeline/FileBookGenerationJournalTests.cs
git commit -m "feat(book): M6.8.2 FileBookGenerationJournal 持久化步骤状态"
```

---

## Task 3：BookGenerationOrchestrator

**Files:**
- Create: `src/Tianming.ProjectData/BookPipeline/BookGenerationOrchestrator.cs`
- Test: `tests/Tianming.ProjectData.Tests/BookPipeline/BookGenerationOrchestratorTests.cs`

- [ ] **Step 3.1：测试**

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.BookPipeline;
using Xunit;

namespace Tianming.ProjectData.Tests.BookPipeline;

public class BookGenerationOrchestratorTests
{
    private sealed class FakeStep : IBookPipelineStep
    {
        public string Name { get; }
        public bool Called { get; private set; }
        public FakeStep(string name) { Name = name; }
        public Task<BookStepResult> ExecuteAsync(BookPipelineContext ctx, CancellationToken ct)
        {
            Called = true;
            return Task.FromResult(new BookStepResult { Success = true });
        }
    }

    private sealed class InMemoryJournal : IBookGenerationJournal
    {
        public HashSet<string> Completed { get; } = new();
        public HashSet<string> Skipped { get; } = new();
        public Task<bool> IsCompletedAsync(string n, CancellationToken ct = default) => Task.FromResult(Completed.Contains(n));
        public Task<bool> IsSkippedAsync(string n, CancellationToken ct = default) => Task.FromResult(Skipped.Contains(n));
        public Task RecordCompletedAsync(string n, CancellationToken ct = default) { Completed.Add(n); return Task.CompletedTask; }
        public Task MarkSkippedAsync(string n, CancellationToken ct = default) { Skipped.Add(n); return Task.CompletedTask; }
        public Task ResetAsync(string n, CancellationToken ct = default) { Completed.Remove(n); Skipped.Remove(n); return Task.CompletedTask; }
    }

    [Fact]
    public async Task Runs_all_uncompleted_steps()
    {
        var steps = new[] { new FakeStep("S1"), new FakeStep("S2"), new FakeStep("S3") };
        var journal = new InMemoryJournal();
        var orch = new BookGenerationOrchestrator(steps, journal);
        var result = await orch.RunAsync(new BookPipelineContext());
        Assert.True(result.Success);
        Assert.All(steps, s => Assert.True(s.Called));
    }

    [Fact]
    public async Task Skips_completed_steps()
    {
        var steps = new[] { new FakeStep("S1"), new FakeStep("S2") };
        var journal = new InMemoryJournal();
        journal.Completed.Add("S1");
        var orch = new BookGenerationOrchestrator(steps, journal);
        await orch.RunAsync(new BookPipelineContext());
        Assert.False(steps[0].Called);
        Assert.True(steps[1].Called);
    }

    [Fact]
    public async Task Stops_on_step_failure()
    {
        var steps = new IBookPipelineStep[]
        {
            new FakeStep("S1"),
            new FailingStep("S2"),
            new FakeStep("S3"),
        };
        var journal = new InMemoryJournal();
        var orch = new BookGenerationOrchestrator(steps, journal);
        var result = await orch.RunAsync(new BookPipelineContext());
        Assert.False(result.Success);
        Assert.Equal("S2", result.FailedStepName);
    }

    private sealed class FailingStep : IBookPipelineStep
    {
        public string Name { get; }
        public FailingStep(string n) { Name = n; }
        public Task<BookStepResult> ExecuteAsync(BookPipelineContext c, CancellationToken ct)
            => Task.FromResult(new BookStepResult { Success = false, ErrorMessage = "boom" });
    }
}
```

- [ ] **Step 3.2：实现**

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.BookPipeline
{
    public sealed class BookGenerationOrchestrator
    {
        private readonly IReadOnlyList<IBookPipelineStep> _steps;
        private readonly IBookGenerationJournal _journal;

        public BookGenerationOrchestrator(IEnumerable<IBookPipelineStep> steps, IBookGenerationJournal journal)
        {
            _steps = new List<IBookPipelineStep>(steps);
            _journal = journal;
        }

        public async Task<BookOrchestratorResult> RunAsync(BookPipelineContext context, CancellationToken ct = default)
        {
            foreach (var step in _steps)
            {
                ct.ThrowIfCancellationRequested();
                if (await _journal.IsCompletedAsync(step.Name, ct).ConfigureAwait(false)) continue;
                if (await _journal.IsSkippedAsync(step.Name, ct).ConfigureAwait(false)) continue;

                var result = await step.ExecuteAsync(context, ct).ConfigureAwait(false);
                if (!result.Success)
                {
                    return new BookOrchestratorResult
                    {
                        Success = false,
                        FailedStepName = step.Name,
                        ErrorMessage = result.ErrorMessage ?? "(no message)",
                    };
                }
                await _journal.RecordCompletedAsync(step.Name, ct).ConfigureAwait(false);
            }
            return new BookOrchestratorResult { Success = true };
        }
    }

    public sealed class BookOrchestratorResult
    {
        public bool Success { get; set; }
        public string? FailedStepName { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
```

- [ ] **Step 3.3：commit**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter BookGenerationOrchestratorTests --nologo -v minimal
git add src/Tianming.ProjectData/BookPipeline/BookGenerationOrchestrator.cs \
        tests/Tianming.ProjectData.Tests/BookPipeline/BookGenerationOrchestratorTests.cs
git commit -m "feat(book): M6.8.3 BookGenerationOrchestrator + skip-completed 逻辑"
```

---

## Task 4：10 个 Step 实现（最小占位）

> 每个 Step 都是 placeholder：调用现有 service（如 ChapterGenerationPipeline）或返回成功。这是 M6.8 的最小可用版本，后续 M6.8+ 再增强。

**Files:** 10 个 `src/Tianming.ProjectData/BookPipeline/Steps/*.cs`

每个 step 模板：

```csharp
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.BookPipeline.Steps
{
    public sealed class DesignStep : IBookPipelineStep
    {
        public string Name => BookPipelineStepName.Design;

        public Task<BookStepResult> ExecuteAsync(BookPipelineContext context, CancellationToken ct = default)
        {
            // M6.8 占位：只检查设计模块文件是否存在
            var designDir = System.IO.Path.Combine(context.ProjectRoot, "Design");
            var ok = System.IO.Directory.Exists(designDir);
            return Task.FromResult(new BookStepResult
            {
                Success = ok,
                ErrorMessage = ok ? null : $"Design directory not found at {designDir}",
            });
        }
    }
}
```

10 个 step 各自写 1-2 条测试（验证 Name 正确 + 一个 happy path）。

各 commit message：
- `feat(book): M6.8.4a DesignStep 占位`
- `feat(book): M6.8.4b OutlineStep 占位`
- `feat(book): M6.8.4c VolumeStep 占位`
- `feat(book): M6.8.4d ChapterPlanningStep 占位`
- `feat(book): M6.8.4e BlueprintStep 占位`
- `feat(book): M6.8.4f GenerateStep 占位（接 ChapterGenerationPipeline.SaveGeneratedChapterStrictAsync）`
- `feat(book): M6.8.4g HumanizeStep 占位（接 M6.2 HumanizePipeline）`
- `feat(book): M6.8.4h GateStep 占位（接 GenerationGate）`
- `feat(book): M6.8.4i SaveStep 占位`
- `feat(book): M6.8.4j IndexStep 占位`

> **简化注**：每个 step 实际实现可以非常薄（返回 Success=true）。重点是骨架打通让 orchestrator 能运行 10 步。M6.8+ 再深化每步细节。

---

## Task 5：BookPipelineViewModel + UI

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/Book/BookPipelineViewModel.cs`
- Create: `src/Tianming.Desktop.Avalonia/Views/Book/BookPipelinePage.axaml` + `.axaml.cs`
- Modify: `src/Tianming.Desktop.Avalonia/Navigation/PageKeys.cs`（加 `BookPipeline`）
- Modify: `src/Tianming.Desktop.Avalonia/ViewModels/Shell/LeftNavViewModel.cs`（加导航项）
- Modify: `src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs`（DI + PageRegistry）
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml`（DataTemplate）

- [ ] **Step 5.1：ViewModel**

```csharp
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TM.Services.Modules.ProjectData.BookPipeline;

namespace Tianming.Desktop.Avalonia.ViewModels.Book;

public partial class BookPipelineViewModel : ObservableObject
{
    private readonly BookGenerationOrchestrator _orchestrator;
    private readonly IBookGenerationJournal _journal;
    private CancellationTokenSource? _cts;

    public ObservableCollection<BookStepVm> Steps { get; } = new();

    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string? _statusMessage;

    public BookPipelineViewModel(BookGenerationOrchestrator orchestrator, IBookGenerationJournal journal)
    {
        _orchestrator = orchestrator;
        _journal = journal;
        foreach (var name in new[]
        {
            BookPipelineStepName.Design, BookPipelineStepName.Outline, BookPipelineStepName.Volume,
            BookPipelineStepName.ChapterPlanning, BookPipelineStepName.Blueprint,
            BookPipelineStepName.Generate, BookPipelineStepName.Humanize,
            BookPipelineStepName.Gate, BookPipelineStepName.Save, BookPipelineStepName.Index,
        })
        {
            Steps.Add(new BookStepVm { Name = name });
        }
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        IsRunning = true;
        StatusMessage = "运行中…";
        _cts = new CancellationTokenSource();
        try
        {
            var result = await _orchestrator.RunAsync(new BookPipelineContext(), _cts.Token);
            StatusMessage = result.Success
                ? "完成"
                : $"在 {result.FailedStepName} 失败：{result.ErrorMessage}";
        }
        finally
        {
            IsRunning = false;
        }
    }

    [RelayCommand]
    private void Pause()
    {
        _cts?.Cancel();
        StatusMessage = "已暂停";
    }

    [RelayCommand]
    private async Task SkipStepAsync(string? stepName)
    {
        if (string.IsNullOrEmpty(stepName)) return;
        await _journal.MarkSkippedAsync(stepName);
    }

    [RelayCommand]
    private async Task ResetStepAsync(string? stepName)
    {
        if (string.IsNullOrEmpty(stepName)) return;
        await _journal.ResetAsync(stepName);
    }
}

public partial class BookStepVm : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _status = "Pending"; // Pending / Running / Completed / Skipped / Failed
}
```

- [ ] **Step 5.2：View（简单列表 + 控制按钮）**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Tianming.Desktop.Avalonia.ViewModels.Book"
             x:Class="Tianming.Desktop.Avalonia.Views.Book.BookPipelinePage"
             x:DataType="vm:BookPipelineViewModel">
  <DockPanel Margin="20">
    <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Spacing="8" Margin="0,0,0,12">
      <Button Classes="primary" Content="开始" Command="{Binding StartCommand}" IsEnabled="{Binding !IsRunning}"/>
      <Button Classes="ghost" Content="暂停" Command="{Binding PauseCommand}" IsEnabled="{Binding IsRunning}"/>
      <TextBlock Text="{Binding StatusMessage}" VerticalAlignment="Center" Margin="12,0,0,0"/>
    </StackPanel>
    <ItemsControl ItemsSource="{Binding Steps}">
      <ItemsControl.ItemTemplate>
        <DataTemplate DataType="vm:BookStepVm">
          <Border Padding="10" Margin="0,4" Background="{DynamicResource SurfacePanelBrush}" CornerRadius="{DynamicResource RadiusSm}">
            <Grid ColumnDefinitions="Auto, *, Auto, Auto">
              <TextBlock Grid.Column="0" Text="{Binding Status}" Width="80"/>
              <TextBlock Grid.Column="1" Text="{Binding Name}" FontWeight="SemiBold"/>
              <Button Grid.Column="2" Classes="ghost" Content="跳过"
                      Command="{Binding $parent[ItemsControl].DataContext.SkipStepCommand}"
                      CommandParameter="{Binding Name}"/>
              <Button Grid.Column="3" Classes="ghost" Content="重跑"
                      Command="{Binding $parent[ItemsControl].DataContext.ResetStepCommand}"
                      CommandParameter="{Binding Name}"/>
            </Grid>
          </Border>
        </DataTemplate>
      </ItemsControl.ItemTemplate>
    </ItemsControl>
  </DockPanel>
</UserControl>
```

`.axaml.cs`:

```csharp
using Avalonia.Controls;
namespace Tianming.Desktop.Avalonia.Views.Book;
public partial class BookPipelinePage : UserControl
{
    public BookPipelinePage() => InitializeComponent();
}
```

- [ ] **Step 5.3：PageKeys + LeftNav + DI + DataTemplate**

`PageKeys.cs`:
```csharp
public static readonly PageKey BookPipeline = new("book.pipeline");
```

`LeftNavViewModel.cs` 工具组加：
```csharp
new(PageKeys.BookPipeline, "一键成书", "📚"),
```

`AvaloniaShellServiceCollectionExtensions.cs`:
```csharp
// M6.8 一键成书
s.AddSingleton<IBookGenerationJournal>(sp =>
    new FileBookGenerationJournal(sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
s.AddSingleton<IBookPipelineStep, DesignStep>();
s.AddSingleton<IBookPipelineStep, OutlineStep>();
s.AddSingleton<IBookPipelineStep, VolumeStep>();
s.AddSingleton<IBookPipelineStep, ChapterPlanningStep>();
s.AddSingleton<IBookPipelineStep, BlueprintStep>();
s.AddSingleton<IBookPipelineStep, GenerateStep>();
s.AddSingleton<IBookPipelineStep, HumanizeStep>();
s.AddSingleton<IBookPipelineStep, GateStep>();
s.AddSingleton<IBookPipelineStep, SaveStep>();
s.AddSingleton<IBookPipelineStep, IndexStep>();
s.AddSingleton<BookGenerationOrchestrator>(sp =>
    new BookGenerationOrchestrator(sp.GetServices<IBookPipelineStep>(), sp.GetRequiredService<IBookGenerationJournal>()));
s.AddTransient<BookPipelineViewModel>();
reg.Register<BookPipelineViewModel, BookPipelinePage>(PageKeys.BookPipeline);
```

`App.axaml` DataTemplate:
```xml
<DataTemplate DataType="vmb:BookPipelineViewModel">
  <vbk:BookPipelinePage/>
</DataTemplate>
```

加 xmlns：
```xml
xmlns:vmb="using:Tianming.Desktop.Avalonia.ViewModels.Book"
xmlns:vbk="using:Tianming.Desktop.Avalonia.Views.Book"
```

- [ ] **Step 5.4：build + 全量 test**

```bash
dotnet build Tianming.MacMigration.sln --nologo -v minimal
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -10
```

- [ ] **Step 5.5：commit**

```bash
git add src/Tianming.Desktop.Avalonia/ViewModels/Book/BookPipelineViewModel.cs \
        src/Tianming.Desktop.Avalonia/Views/Book/BookPipelinePage.axaml \
        src/Tianming.Desktop.Avalonia/Views/Book/BookPipelinePage.axaml.cs \
        src/Tianming.Desktop.Avalonia/Navigation/PageKeys.cs \
        src/Tianming.Desktop.Avalonia/ViewModels/Shell/LeftNavViewModel.cs \
        src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs \
        src/Tianming.Desktop.Avalonia/App.axaml
git commit -m "feat(book): M6.8.5 BookPipelineViewModel + Page + LeftNav 一键成书入口"
```

---

## M6.8 Gate 验收

| 项 | 标准 |
|---|---|
| IBookPipelineStep + 10 step name | 接口 + 常量 + 占位实现 |
| BookGenerationJournal | Completed / Skipped 状态 + Reset |
| BookGenerationOrchestrator | skip-completed + stop-on-fail |
| 10 个 Step 占位 | 各 ≥1 测试 |
| BookPipelineViewModel | Start/Pause/Skip/Reset 命令 + Steps 集合 |
| BookPipelinePage | 列表 + 控制按钮可见 |
| DI + PageKeys + LeftNav | "一键成书" 导航能跳进页面 |
| 全量 test | 新增 ≥18 条，dotnet test 全过 |
