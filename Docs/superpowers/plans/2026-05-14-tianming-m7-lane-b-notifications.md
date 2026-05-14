# Round 7 Lane B — 通知 / 声音 / 勿扰 / Toast / 系统集成 设置 page

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans / subagent-driven-development

**Goal:** 在 Avalonia Settings Shell 加 "通知" 子导航项，建一个 NotificationsPage 含 7 个 SectionCard 覆盖矩阵 D #12-#18 全部 7 项框架能力。

**Architecture:** 沿用 Lane A 的 PageRegistry + SettingsShell 子导航模式。1 个 Page + 1 个 VM 含 7 个 SectionCard，每个 Card 按 lib API 能力做 完整控制 / read-only / data 编辑。Lane 0 已注册的链路（dispatcher / sink / sound player / speech）直接复用。

**Tech Stack:** Avalonia 11, CommunityToolkit.Mvvm, .NET 8, xUnit.

**Worktree:** `.claude/worktrees/m7-lane-b-notifications` (branch `worktree-m7-lane-b-notifications`)

---

## Lib API 探索结论

| 矩阵 D | 关键 lib | DI 状态 | 实施级别 |
|---|---|---|---|
| #12 Toast 样式 | `PortableToastStyleData` (data class) | ❌ 未 DI | 补 DI → read/write data |
| #13 系统通知 | `IPortableNotificationSink` (MacOSNotificationSink) | ✅ Lane 0 | read-only 显示 sink 类型 + 测试发通知按钮 |
| #14 系统集成 | `PortableSystemIntegrationSettings` (data class) | ❌ 未 DI | 补 DI → read/write 主要字段 |
| #15 通知历史 | `FileNotificationHistoryStore` | ✅ Lane 0 | 完整 list + 清空 + 标记已读 |
| #16 勿扰 | `PortableDoNotDisturbController` (含在 DoNotDisturbPolicy.cs) | ❌ 未 DI | 补 DI → Toggle + StatusText |
| #17 声音方案 | `PortableNotificationSoundOptions.SoundScheme` | ✅ Lane 0 | read-only 显示当前方案名 + 声音库 deferred |
| #18 语音播报 | `PortableNotificationSoundOptions.VoiceBroadcast` | ✅ Lane 0 | read-only 显示当前开关 + Speed/Volume/Pitch |

---

## File Structure

### 新增

```
src/Tianming.Desktop.Avalonia/
  ViewModels/Settings/NotificationsViewModel.cs       # 主 VM 含 7 个 SectionCard data
  Views/Settings/NotificationsPage.axaml(.cs)         # 7 个 SectionCard

tests/Tianming.Desktop.Avalonia.Tests/
  ViewModels/Settings/NotificationsViewModelTests.cs  # 覆盖 history / send test / toggle DND
```

### 修改

```
src/Tianming.Desktop.Avalonia/
  Navigation/PageKeys.cs                              # +1 SettingsNotifications
  ViewModels/Settings/SettingsShellViewModel.cs       # 子导航加 "通知" 项
  AvaloniaShellServiceCollectionExtensions.cs         # 补 4 个未注册 lib DI + Page Register + AddTransient VM
  App.axaml                                           # +1 DataTemplate
```

---

## Task 1: 补 lib DI 注册（4 个未注册类）

**Files:** `AvaloniaShellServiceCollectionExtensions.cs` (notifications DI 段，~165 行附近)

补充注册：

```csharp
// Lane B: 通知设置相关 data / controller，按 Singleton 持有用户配置
s.AddSingleton(_ => DoNotDisturbSettingsData.CreateDefault());
s.AddSingleton<PortableDoNotDisturbController>(sp =>
    new PortableDoNotDisturbController(sp.GetRequiredService<DoNotDisturbSettingsData>()));
s.AddSingleton(_ => new PortableToastStyleData());
s.AddSingleton(_ => new PortableSystemIntegrationSettings());
```

- [ ] Step 1.1: 在 ShellExt.cs ~line 195（FileNotificationHistoryStore 注册之后）插入上述 4 行
- [ ] Step 1.2: build 确认 0 W / 0 E（DI 阶段不需要测试）
- [ ] Step 1.3: commit `Lane B: register DND / Toast / SystemIntegration / Sound data for UI access`

---

## Task 2: NotificationsPage 骨架 + SettingsShell 子导航条目

**Files:**
- Modify: `Navigation/PageKeys.cs` — `+SettingsNotifications = new("settings.notifications")`
- Modify: `ViewModels/Settings/SettingsShellViewModel.cs` — SubNavItems 加 `new(SettingsNotifications, "通知", "🔔")`
- Create: `ViewModels/Settings/NotificationsViewModel.cs`
- Create: `Views/Settings/NotificationsPage.axaml(.cs)`
- Modify: `App.axaml` — +1 DataTemplate
- Modify: `AvaloniaShellServiceCollectionExtensions.cs` — reg.Register + AddTransient

**NotificationsViewModel 大致结构:**

```csharp
public partial class NotificationsViewModel : ObservableObject
{
    private readonly FileNotificationHistoryStore _historyStore;
    private readonly PortableNotificationDispatcher _dispatcher;
    private readonly PortableDoNotDisturbController _dnd;
    private readonly DoNotDisturbSettingsData _dndSettings;
    private readonly PortableNotificationSoundOptions _soundOptions;
    private readonly PortableToastStyleData _toastStyle;
    private readonly PortableSystemIntegrationSettings _sysIntegration;
    private readonly IPortableNotificationSink _sink;

    // #15 通知历史
    public ObservableCollection<NotificationRecordData> History { get; } = new();
    [ObservableProperty] private bool _hasNoHistory;

    // #13 系统通知 + 发测试通知
    [ObservableProperty] private string _sinkName;
    [RelayCommand] private async Task SendTestNotificationAsync() { ... }

    // #16 勿扰
    [ObservableProperty] private string _dndStatusText;
    [RelayCommand] private void ToggleDoNotDisturb() { _dnd.Toggle(); DndStatusText = _dnd.StatusText; }

    // #17 声音方案
    [ObservableProperty] private string _soundSchemeName;

    // #18 语音播报
    [ObservableProperty] private bool _voiceBroadcastEnabled;
    [ObservableProperty] private double _voiceSpeed;

    // #12 Toast 样式
    [ObservableProperty] private double _toastCornerRadius;
    [ObservableProperty] private string _toastScreenPosition;

    // #14 系统集成
    [ObservableProperty] private bool _showTrayIcon;
    [ObservableProperty] private bool _autoStartup;

    [RelayCommand] private async Task RefreshHistoryAsync() { ... }
    [RelayCommand] private async Task ClearHistoryAsync() { ... }
}
```

axaml 用 7 个 controls:SectionCard，每个 Card 内对应数据 binding。

---

## Task 3: 测试覆盖

**Files:** `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Settings/NotificationsViewModelTests.cs`

至少 5 条：
- `Ctor_loads_initial_data_from_lib`（验证 SoundSchemeName / SinkName / DndStatusText 都不空）
- `RefreshHistoryAsync_populates_collection`（fixture history store + 写入几条记录，验证 ListView 数据）
- `SendTestNotificationAsync_invokes_dispatcher`（mock dispatcher 或注入 spy sink，验证发送）
- `ToggleDoNotDisturb_flips_status`（toggle 后 DndStatusText 变化）
- `ClearHistoryAsync_empties_collection`

---

## 收尾

- [ ] dotnet build 0 W / 0 E
- [ ] dotnet test 全绿（新增 ≥5 条）
- [ ] commit + 主线程 merge --no-ff 入 main + push
- [ ] 清 worktree + 分支

---

## Scope 明确不做（用户选 "完整尝试" 已知 deferred）

- Toast 样式的 Animation 编辑器（type / duration / easing 多 enum）— Card 内只暴露 CornerRadius + ScreenPosition
- 系统集成的 AutoStartup macOS LaunchAgent 真实安装（plist 写入）— Card 内只 toggle data，apply 留 TODO
- 声音库（PortableSoundLibrary）的 import/delete UI — read-only 显示库目录路径
- 勿扰的 schedule 编辑器（StartTime/EndTime/Weekdays）— Card 仅 Toggle + StatusText

---

## Self-Review

- Spec coverage: D #12-#18 7 项全部以 Card 形式覆盖（5 完整 + 2 read-only），符合 Lane B 派单要求
- Placeholder scan: 无 TODO / TBD 关键字；deferred 项明确标 scope
- Type consistency: lib 类名 / API 已 grep 确认（DoNotDisturbController / SoundOptions / ToastStyleData / SystemIntegrationSettings / HistoryStore 全 ✓）
- Step granularity: Task 1 (3) + Task 2 (~6 step 内含 axaml/VM/DI/测试) + Task 3 (~5 step)。合理可控
