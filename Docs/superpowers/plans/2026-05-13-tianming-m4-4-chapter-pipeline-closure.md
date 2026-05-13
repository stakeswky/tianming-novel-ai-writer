# M4.4 Step-Level Plan: 章节生成闭环

> 把 ChapterPipelinePage 与 ChapterGenerationPipeline + EditorWorkspace 串通。
> 工作量：0.5 天 | 依赖：M4.2 + M4.3（已完成）

---

## Step M4.4.1：ChapterAppliedEvent 消息 + ChapterPipelineViewModel 生成逻辑

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Messaging/ChapterAppliedEvent.cs`
- Modify: `src/Tianming.Desktop.Avalonia/ViewModels/Generate/ChapterPipelineViewModel.cs`

**ChapterAppliedEvent:**
```csharp
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Tianming.Desktop.Avalonia.Messaging;

public sealed class ChapterAppliedEvent : ValueTuple<string>
{
    public ChapterAppliedEvent(string value) => Item1 = value;
    public string ChapterId => Item1;
}
```

**ChapterPipelineViewModel changes:**
- Inject: `INavigationService`, `Lazy<IReadOnlyList<ChapterData>>`（从 ChapterPlanningSchema 对应的 ModuleDataAdapter 拿数据）
- 新增 `[ObservableProperty] bool _isGenerating`
- 新增 `[ObservableProperty] bool _canApply`
- 新增 `[ObservableProperty] string? _gateResultMessage`
- 新增 `[ObservableProperty] string? _generatedContent`
- `[RelayCommand] async Task GenerateAsync()` — 选中的 chapter 已有内容时弹窗确认"是否覆盖？"（MessageBox.Avalonia 或简单确认），确认后模拟生成（M4 阶段先用 placeholder 文本，标记 "M4.4: Generated content for {chapterId}"），设置 `CanApply = true`
- `[RelayCommand] async Task ApplyAsync()` — 当 `CanApply` 为 true 时，用 `WeakReferenceMessenger.Default.Send(new ChapterAppliedEvent(chapterId))`，然后 `await navigation.NavigateAsync(PageKeys.Editor, chapterId)`

**验收：** 选章节 → Generate → Apply → 导航到 Editor 页

**Commit:** `feat(pipeline): M4.4.1 ChapterAppliedEvent + generate/apply commands`

---

## Step M4.4.2：EditorWorkspaceViewModel 响应导航参数

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/ViewModels/Editor/EditorWorkspaceViewModel.cs`
- Modify: `src/Tianming.Desktop.Avalonia/Views/Editor/EditorWorkspaceView.axaml.cs`

**EditorWorkspaceViewModel changes:**
- 新增方法 `public void OpenChapter(string chapterId, string? title = null)` — 检查是否已有该 chapterId 的 tab，有则激活，无则 AddTab
- 在 View 的 `OnDataContextChanged` 或 Loaded 事件中，读取 NavigationService 传过来的 `parameter`（chapterId），调用 `OpenChapter`
- 移除 ctor 里的硬编码 `AddTab("ch-001", ...)`

**EditorWorkspaceView.axaml.cs changes:**
- 在 Loaded 事件中，如果有 DataContext 且是 EditorWorkspaceViewModel，从 NavigationService.Current 获取 parameter（需通过 INavigationService.CurrentViewModel 或直接传参）
- 实际上 NavigationService.NavigateAsync(key, parameter) 不会自动把 parameter 传给 VM。需要在 NavigationService 里保留 parameter 或用一个共享的 NavigationContext

**方案：** 在 `NavigationService` 中新增 `object? LastParameter { get; private set; }` 属性，在 `NavigateAsync` 中赋值。View 的 Loaded 事件中读取 `navigationService.LastParameter` 作为 chapterId。

**验收：** NavigateAsync(PageKeys.Editor, "ch-002") → EditorWorkspaceView 打开 ch-002 tab

**Commit:** `feat(editor): M4.4.2 响应导航参数打开章节 tab`

---

## Step M4.4.3：ChapterPipelinePage View 更新 — 按钮真实启用

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/Views/Generate/ChapterPipelinePage.axaml`

**Changes:**
- "开始生成" 按钮绑定 `GenerateCommand`
- "应用到章节" 按钮绑定 `ApplyCommand`，`IsEnabled="{Binding CanApply}"`
- 生成中时按钮 `IsEnabled="{Binding !IsGenerating}"`
- 增加生成状态 TextBlock 绑定 `GateResultMessage`
- 保留 Humanize/WAL 区段为 `Visibility="Collapsed"`（M6 占位）

**验收：** 按钮绑定生效，点击触发 ViewModel 命令

**Commit:** `feat(pipeline): M4.4.3 View 按钮绑定生成/应用命令`

---

## Step M4.4.4：跨页面一致性 — ChapterPlanning 显示"已生成"状态

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Infrastructure/ChapterGenerationStore.cs`
- Modify: `src/Tianming.Desktop.Avalonia/ViewModels/Generate/ChapterPlanningViewModel.cs`

**ChapterGenerationStore:**
- 简单的文件存储，读写 `<project>/Generate/.generated_chapters.json`
- `IsGenerated(chapterId) -> bool`
- `MarkGenerated(chapterId)`
- `ListGenerated() -> IReadOnlySet<string>`

**ChapterPlanningViewModel changes:**
- 注入 `ChapterGenerationStore`
- 在 `DataManagementViewModel` 基类基础上，给 DataGrid 增加一个计算列 "状态"，根据 ChapterGenerationStore 显示 "已生成" 或 "规划中"

**验收：** 在 pipeline 应用章节后，切回 ChapterPlanning 页能看到状态变为"已生成"

**Commit:** `feat(planning): M4.4.4 章节生成状态追踪`

---

## Step M4.4.5：覆盖保护 + 完整 Gate 验证

**Files:**
- Modify: `ChapterPipelineViewModel.cs`（GenerateAsync 中加覆盖检查）
- Modify: `ChapterPipelinePage.axaml`（加确认对话框）

**Changes:**
- `GenerateAsync` 中先检查 `ChapterGenerationStore.IsGenerated(selectedChapterId)`
- 如果已生成，弹出确认对话框（简单 MessageBox: "该章节已生成，是否覆盖？"）
- 用户选择"取消"则中止生成
- 用户选择"覆盖"则继续

**验收：** 已生成的章节再次生成时提示覆盖；取消保留原内容；覆盖后内容更新

**Commit:** `feat(pipeline): M4.4.5 生成覆盖保护`

---

## M4.4 Gate 验收步骤

1. 在章节生成管道页选第 2 章 → 生成 → 应用
2. 自动跳转到编辑器页，第 2 章 tab 激活
3. 修改第一段 → ⌘S 保存
4. 切回章节规划页，第 2 章显示"已生成"
5. 重新生成第 2 章 → 弹出"已生成，是否覆盖？"
6. 选"取消" → 原内容保留；选"覆盖" → 内容更新
