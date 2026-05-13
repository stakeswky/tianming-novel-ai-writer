# M4.6 Step-Level Plan: AI 管理

> 模型 / Key / 提示词 / 用量 四页，管理页配置被 M4.5 对话消费
> 工作量：1.5 天 | 与 M4.5 并行

---

## Step M4.6.1：PageKeys + LeftNav 更新

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/Navigation/PageKeys.cs`
- Modify: `src/Tianming.Desktop.Avalonia/ViewModels/Shell/LeftNavViewModel.cs`

**新增 PageKeys:**
```csharp
public static readonly PageKey AIModels;
public static readonly PageKey AIPrompts;
public static readonly PageKey AIUsage;
```

**LeftNavViewModel 新增:**
- "模型与密钥" → `NavigateCommand.Execute(PageKeys.AIModels)`
- "提示词" → `NavigateCommand.Execute(PageKeys.AIPrompts)`
- "使用统计" → `NavigateCommand.Execute(PageKeys.AIUsage)`

**Commit:** `feat(nav): M4.6.1 AI 管理页导航`

---

## Step M4.6.2：ModelManagementPage

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/AI/ModelManagementViewModel.cs`
- Create: `src/Tianming.Desktop.Avalonia/Views/AI/ModelManagementPage.axaml` + `.axaml.cs`

**ModelManagementViewModel:**
- 注入 `FileAIConfigurationStore`
- `ObservableCollection<ModelConfigItem> Models` — 列出所有 provider+model 配置
- `ModelConfigItem` — `Id`, `ProviderId`, `ModelId`, `Endpoint`, `Temperature`, `IsActive`, `DisplayName`
- `[RelayCommand] async Task AddModelAsync()` — 新建配置
- `[RelayCommand] async Task SetActiveAsync(string configId)` — 设为默认
- `[RelayCommand] async Task DeleteModelAsync(string configId)` — 删除
- `[RelayCommand] async Task SaveModelAsync(ModelConfigItem item)` — 保存编辑

**ModelManagementPage.axaml:**
- DataGrid 列出模型（Provider / Model / Endpoint / Temperature / 状态 / 操作）
- "添加模型" 按钮 → 弹出编辑表单
- 每行"设为默认" / "删除" 按钮

**Commit:** `feat(ai): M4.6.2 ModelManagementPage`

---

## Step M4.6.3：ApiKeysPage

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/AI/ApiKeysViewModel.cs`
- Create: `src/Tianming.Desktop.Avalonia/Views/AI/ApiKeysPage.axaml` + `.axaml.cs`

**ApiKeysViewModel:**
- 注入 `FileAIConfigurationStore`, `IApiKeySecretStore`
- `ObservableCollection<ProviderKeyGroup> Providers` — 每个 provider 下 1+ key 条目
- `ProviderKeyGroup` — `ProviderId`, `ProviderName`, `ObservableCollection<ApiKeyItem> Keys`
- `ApiKeyItem` — `Id`, `Remark`, `IsEnabled`, `CreatedAt`, `string MaskedKey`（****前缀遮蔽）
- `[ObservableProperty] string _newKey` — 新增 key 输入
- `[RelayCommand] async Task SaveKeyAsync(string providerId)` — 存 key 到 IApiKeySecretStore（`Task.Run` 包装）
- `[RelayCommand] async Task ToggleKeyAsync(string keyId)` — 启用/禁用
- `[RelayCommand] async Task DeleteKeyAsync(string keyId)` — 删除

**ApiKeysPage.axaml:**
- 按 provider 分组，每组展开显示 key 列表
- PasswordBox（可切换明文显示）
- "新增 Key" 按钮 + 输入框

**Commit:** `feat(ai): M4.6.3 ApiKeysPage`

---

## Step M4.6.4：PromptManagementPage

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/AI/PromptManagementViewModel.cs`
- Create: `src/Tianming.Desktop.Avalonia/Views/AI/PromptManagementPage.axaml` + `.axaml.cs`

**PromptManagementViewModel:**
- 注入 `FilePromptTemplateStore`
- `ObservableCollection<PromptTemplateItem> Templates`
- `PromptTemplateItem` — `Id`, `Name`, `Description`, `Template`, `Variables`（逗号分隔）, `Category`
- `[RelayCommand] async Task AddTemplateAsync()` — 新建
- `[RelayCommand] async Task SaveTemplateAsync(PromptTemplateItem item)` — 保存
- `[RelayCommand] async Task DeleteTemplateAsync(string id)` — 删除

**PromptManagementPage.axaml:**
- DataGrid 列出提示词（名称 / 描述 / 类别 / 操作）
- 详情编辑区：名称、描述、模板内容（TextBox MultiLine）、变量、类别

**Commit:** `feat(ai): M4.6.4 PromptManagementPage`

---

## Step M4.6.5：UsageStatisticsPage

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/AI/UsageStatisticsViewModel.cs`
- Create: `src/Tianming.Desktop.Avalonia/Views/AI/UsageStatisticsPage.axaml` + `.axaml.cs`

**UsageStatisticsViewModel:**
- 注入 `FileUsageStatisticsService`
- `StatisticsSummary TodayStats` — 今日统计（请求数/Tokens/成功率/费用估算）
- `IReadOnlyList<DailyStatistics> DailyStats` — 近 7 天趋势
- `IReadOnlyDictionary<string, StatisticsSummary> ModelStats` — 按 model 聚合

**费用估算：** 简单按 `$0.001/1k input + $0.003/1k output` 粗估（后续 M6.6 按 model 精确）

**UsageStatisticsPage.axaml:**
- 顶部 4 个 StatsCard（请求数 / Tokens / 费用 / 成功率）
- 下方 DataGrid 按日期列出 DailyStatistics
- 可选按 model 分组的汇总表

**Commit:** `feat(ai): M4.6.5 UsageStatisticsPage`

---

## Step M4.6.6：DI 注册 + App.axaml DataTemplate

**Files:**
- Modify: `AvaloniaShellServiceCollectionExtensions.cs`
- Modify: `App.axaml`

**DI 注册:**
```csharp
// M4.6 AI 管理
s.AddSingleton<FileAIConfigurationStore>(sp =>
    new FileAIConfigurationStore(Path.Combine(sp.GetRequiredService<AppPaths>().AppSupportDirectory, "AI")));
s.AddSingleton<IApiKeySecretStore>(_ => new MacOSKeychainApiKeySecretStore(new ProcessSecurityCommandRunner()));
s.AddSingleton<FileUsageStatisticsService>(sp =>
    new FileUsageStatisticsService(Path.Combine(sp.GetRequiredService<AppPaths>().AppSupportDirectory, "Usage")));
s.AddTransient<ModelManagementViewModel>();
s.AddTransient<ApiKeysViewModel>();
s.AddTransient<PromptManagementViewModel>();
s.AddTransient<UsageStatisticsViewModel>();
```

**App.axaml DataTemplate:**
```xml
<DataTemplate DataType="vm:ModelManagementViewModel"><vg:ModelManagementPage/></DataTemplate>
<DataTemplate DataType="vm:ApiKeysViewModel"><vg:ApiKeysPage/></DataTemplate>
<DataTemplate DataType="vm:PromptManagementViewModel"><vg:PromptManagementPage/></DataTemplate>
<DataTemplate DataType="vm:UsageStatisticsViewModel"><vg:UsageStatisticsPage/></DataTemplate>
```

**Commit:** `feat(ai): M4.6.6 DI 注册 + DataTemplate`

---

## Step M4.6.7：M4.5 ↔ M4.6 集成验证

**Files:**
- Modify: `ConversationOrchestrator` 或 `ConversationPanelViewModel` — 使用 `FileAIConfigurationStore.GetActiveConfiguration()` 构建 chat client

**集成点：**
1. `OpenAICompatibleChatClient` 初始化时从 `FileAIConfigurationStore` 读取 active config（endpoint/model/apiKey）
2. 切换 ModelManagementPage 中的默认配置后，对话面板下次发消息走新配置
3. API Key 通过 `IApiKeySecretStore` 存取，重启后不丢失
4. 对话完成后调用 `FileUsageStatisticsService.RecordCall()` 记录用量

**Commit:** `feat(ai): M4.6.7 管理页与对话面板集成`

---

## M4.6 Gate 验收步骤

1. ModelManagement — 新增 DeepSeek provider → 标记默认 → 对话面板发消息走 DeepSeek 端点
2. ApiKeys — 填 Key → 关闭重开 → Key 仍在
3. PromptManagement — 创建"历史顾问"提示词 → 对话中可选用 → AI 回复风格改变
4. UsageStatistics — 经过前述操作，看到至少 1 条今日记录
5. 切换另一 provider（OpenAI）→ 同样流程走 OpenAI 端点
