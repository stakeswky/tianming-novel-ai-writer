# Tianming M7 Lane 0 Sinks Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire the four macOS platform sinks into the Avalonia DI container and close the four Lane R review leftovers that block later Round 7 manual validation.

**Architecture:** Keep platform wiring in `AvaloniaShellServiceCollectionExtensions` and startup orchestration in `AppLifecycle`. Use existing framework abstractions (`IPortableSystemAppearanceMonitor`, `IPortableNotificationSink`, `IPortableSpeechOutput`, `IPortableSystemMonitorProbe`) instead of new platform APIs. Lane R cleanup centralizes page display names in `PageRegistry`, exposes provider display names in the AI model VM, and hardens the dev `.app` bundle so Computer Use can attach by bundle id.

**Tech Stack:** .NET 8, Avalonia 11, Microsoft.Extensions.DependencyInjection, xUnit, macOS `codesign`, existing Tianming Framework portable services.

---

## File Structure

- Modify: `src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs` - register macOS appearance, notification, speech, and system monitor services; move page display names into `PageRegistry`.
- Modify: `src/Tianming.Desktop.Avalonia/Infrastructure/AppLifecycle.cs` - start/stop system appearance runtime.
- Modify: `src/Tianming.Desktop.Avalonia/Theme/ThemeBridge.cs` - apply Dark/Light variants from `PortableThemeApplicationRequest`.
- Modify: `src/Tianming.Desktop.Avalonia/Navigation/PageRegistry.cs` - store display names with page registrations.
- Modify: `src/Tianming.Desktop.Avalonia/Infrastructure/NavigationBreadcrumbSource.cs` - read labels from `PageRegistry`.
- Modify: `src/Tianming.Desktop.Avalonia/ViewModels/Shell/LeftNavViewModel.cs` - build nav labels from `PageRegistry`.
- Modify: `src/Tianming.Desktop.Avalonia/ViewModels/AI/DefaultAIProviders.cs` - expose display-name property.
- Modify: `src/Tianming.Desktop.Avalonia/ViewModels/AI/ModelManagementViewModel.cs` - expose `Providers` objects and preserve `ProviderIds`.
- Modify: `src/Tianming.Desktop.Avalonia/Views/AI/ModelManagementPage.axaml` - bind provider ComboBox to display names.
- Modify: `Scripts/build-dev-bundle.sh` - add required Info.plist metadata and ad-hoc sign the app bundle.
- Modify: `Docs/macOS迁移/manual-test-howto.md` - document required plist fields, signing, and attach command.
- Modify: `Docs/macOS迁移/M5-ComputerUse-功能巡检-2026-05-14.md` - add independent R3 IME known-limit note.
- Modify/Create tests under `tests/Tianming.Desktop.Avalonia.Tests/`.

## Step 0: Commit the Lane 0 plan

- [x] **Step 0.1: Save this plan and scan for incomplete placeholder wording**

Run: `printf '%s\n' 'T''BD' 'TO''DO' > /tmp/lane0-placeholder-patterns && rg -n -f /tmp/lane0-placeholder-patterns Docs/superpowers/plans/2026-05-14-tianming-m7-lane0-sinks.md`

Expected: no matches.

- [x] **Step 0.2: Commit**

```bash
git add Docs/superpowers/plans/2026-05-14-tianming-m7-lane0-sinks.md
git commit -m "Plan the Round 7 macOS sink wiring lane

Lane 0 needs a step-level artifact before wiring platform sinks and closing Lane R validation leftovers.

Constraint: Round 7 prompt requires superpowers:writing-plans before steps 1-8
Confidence: high
Scope-risk: narrow
Tested: printf '%s\n' 'T''BD' 'TO''DO' > /tmp/lane0-placeholder-patterns && rg -n -f /tmp/lane0-placeholder-patterns Docs/superpowers/plans/2026-05-14-tianming-m7-lane0-sinks.md
Not-tested: Implementation starts after this plan"
```

## Step 1: Wire MacOSSystemAppearanceMonitor to ThemeBridge

- [x] **Step 1.1: Write red tests**

Add tests in `tests/Tianming.Desktop.Avalonia.Tests/Theme/MacOSAppearanceBridgeTests.cs`:

```csharp
[AvaloniaFact]
public async Task System_appearance_dark_event_switches_application_to_dark()
{
    var previousApp = Application.Current;
    var app = Application.Current ?? new TestApp();
    var bridge = new ThemeBridge(NullLogger<ThemeBridge>.Instance);
    var state = new PortableThemeState();
    var controller = new PortableThemeStateController(state, bridge.ApplyAsync);
    var monitor = new FakeAppearanceMonitor();
    var runtime = new PortableSystemFollowRuntime(
        new PortableSystemFollowSettings { Enabled = true, AutoStart = true, DelaySeconds = 0, EnableMinInterval = false },
        monitor,
        async (snapshot, ct) =>
        {
            var theme = snapshot.IsLightTheme ? PortableThemeType.Light : PortableThemeType.Dark;
            await controller.SwitchThemeAsync(theme, snapshot, ct);
            return new PortableSystemFollowDecision(PortableSystemFollowDecisionStatus.Switch, theme);
        });
    await runtime.InitializeAsync();

    monitor.Raise(new PortableSystemThemeSnapshot(true, false, null), new PortableSystemThemeSnapshot(false, false, null));
    await Task.Delay(50);

    Assert.Equal(ThemeVariant.Dark, app.RequestedThemeVariant);
}
```

Add an AppHost DI test in `tests/Tianming.Desktop.Avalonia.Tests/DI/AppHostTests.cs`:

```csharp
Assert.NotNull(sp.GetRequiredService<IPortableSystemAppearanceMonitor>());
Assert.NotNull(sp.GetRequiredService<PortableSystemFollowRuntime>());
```

- [x] **Step 1.2: Run red**

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter "System_appearance_dark_event_switches_application_to_dark|Build_ResolvesMacOSPlatformSinks"`

Expected: FAIL because `ThemeBridge` forces Light and DI does not expose the monitor/runtime.

- [x] **Step 1.3: Implement**

Register `IPortableSystemAppearanceProbe`, `IPortableSystemAppearanceMonitor`, `PortableSystemFollowSettings`, `PortableSystemFollowController`, and `PortableSystemFollowRuntime`. Call `PortableSystemFollowRuntime.InitializeAsync()` in `AppLifecycle.OnStartupAsync()` and dispose it on shutdown. Change `ThemeBridge.ApplyCore` and `InitializeAsync` to set `ThemeVariant.Dark` when the applied theme plan color mode is dark.

- [x] **Step 1.4: Verify and commit**

Run the focused test filter above, then `dotnet build Tianming.MacMigration.sln`.

Commit with title: `Wire macOS appearance monitor into theme runtime`.

## Step 2: Wire MacOSNotificationSink to PortableNotificationDispatcher

- [x] **Step 2.1: Write red tests**

Extend `Build_ResolvesMacOSPlatformSinks` to assert:

```csharp
Assert.IsType<MacOSNotificationSink>(sp.GetRequiredService<IPortableNotificationSink>());
Assert.NotNull(sp.GetRequiredService<PortableNotificationDispatcher>());
```

Add a unit test in a desktop DI/platform test that constructs `PortableNotificationDispatcher` with a `RecordingNotificationSink`, dispatches with `EnableSystemNotification = true`, and asserts `RecordingNotificationSink.Delivered.Count == 1`.

- [x] **Step 2.2: Run red**

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter "Build_ResolvesMacOSPlatformSinks|Dispatcher_delivers_to_registered_sink"`

Expected: DI assertion fails before registration.

- [x] **Step 2.3: Implement**

Register `IPortableNotificationSink` as `MacOSNotificationSink`, `FileNotificationHistoryStore`, `PortableNotificationDispatcherOptions`, and `PortableNotificationDispatcher`.

- [x] **Step 2.4: Verify and commit**

Run the focused filter and `dotnet build Tianming.MacMigration.sln`.

Commit with title: `Wire macOS notification sink into dispatcher`.

## Step 3: Wire MacOSSpeechOutput to PortableNotificationSoundPlayer

- [x] **Step 3.1: Write red tests**

Extend DI assertions:

```csharp
Assert.IsType<MacOSSpeechOutput>(sp.GetRequiredService<IPortableSpeechOutput>());
Assert.NotNull(sp.GetRequiredService<IPortableNotificationSoundPlayer>());
```

Add a sound-player unit test with a `RecordingSpeechOutput`, `PortableNotificationSoundOptions` where `VoiceBroadcast.IsEnabled = true`, call `BroadcastNotificationAsync`, and assert the speech output receives the text.

- [x] **Step 3.2: Run red**

Run focused tests: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter "Build_ResolvesMacOSPlatformSinks|Sound_player_broadcast_uses_registered_speech_output"`

Expected: DI assertion fails before registration.

- [x] **Step 3.3: Implement**

Register `IPortableSoundOutput` as `MacOSNotificationSoundOutput`, `IPortableSpeechOutput` as `MacOSSpeechOutput`, `PortableNotificationSoundOptions`, and `IPortableNotificationSoundPlayer` as `PortableNotificationSoundPlayer`.

- [x] **Step 3.4: Verify and commit**

Run the focused filter and `dotnet build Tianming.MacMigration.sln`.

Commit with title: `Wire macOS speech output into notification sound player`.

## Step 4: Wire MacOSSystemMonitorProbe to PortableSystemMonitorService

- [x] **Step 4.1: Write red tests**

Extend DI assertions:

```csharp
Assert.IsType<MacOSSystemMonitorProbe>(sp.GetRequiredService<IPortableSystemMonitorProbe>());
Assert.NotNull(sp.GetRequiredService<PortableSystemMonitorService>());
```

Add a unit test with a `CountingSystemMonitorProbe`, construct `PortableSystemMonitorService`, call `CaptureSnapshot`, and assert the fake probe was read.

- [x] **Step 4.2: Run red**

Run focused tests: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter "Build_ResolvesMacOSPlatformSinks|System_monitor_service_reads_registered_probe"`

Expected: DI assertion fails before registration.

- [x] **Step 4.3: Implement**

Register `IPortableSystemMonitorProbe` as `MacOSSystemMonitorProbe` and `PortableSystemMonitorService` from the registered probe.

- [x] **Step 4.4: Verify and commit**

Run the focused filter and `dotnet build Tianming.MacMigration.sln`.

Commit with title: `Wire macOS system monitor probe into monitor service`.

## Step 5: Close Computer Use attach loop

- [x] **Step 5.1: Diagnose current bundle**

Run:

```bash
Scripts/build-dev-bundle.sh
plutil -p /tmp/TianmingDev.app/Contents/Info.plist
codesign -dvv /tmp/TianmingDev.app 2>&1 || true
```

Expected before fix: `Info.plist=not bound` or missing common bundle metadata.

- [x] **Step 5.2: Implement bundle metadata and signing**

Add at least `CFBundleDisplayName`, `CFBundleDevelopmentRegion`, `CFBundleSignature`, `LSMinimumSystemVersion`, `NSHighResolutionCapable`, `NSPrincipalClass`, and `NSHumanReadableCopyright` to the generated Info.plist. After writing the plist, run:

```bash
codesign --force --deep -s - "$APP_DIR"
```

- [x] **Step 5.3: Update how-to**

Document required plist fields, `codesign -dvv`, and the expected `get_app_state("dev.tianming.avalonia.manualtest")` result in `Docs/macOS迁移/manual-test-howto.md`.

- [x] **Step 5.4: Verify with Computer Use**

Run `Scripts/build-dev-bundle.sh`, `open /tmp/TianmingDev.app`, then call Computer Use `get_app_state` for `dev.tianming.avalonia.manualtest`. Expected: a window tree, not `appNotFound`.

- [x] **Step 5.5: Commit**

Commit with title: `Make the dev bundle attachable by Computer Use`.

## Step 6: Move display names into PageRegistry

- [x] **Step 6.1: Write red tests**

Update `NavigationBreadcrumbSourceTests` to register a fake page with `displayName: "测试页面"` and assert breadcrumb uses it. Add an AppHost/LeftNav test that `PageRegistry.GetDisplayName(PageKeys.BookPipeline)` equals the left-nav item label for `PageKeys.BookPipeline`.

- [x] **Step 6.2: Run red**

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter "DisplayName|BookPipeline"`

Expected: FAIL because `PageRegistry` has no display-name API yet.

- [x] **Step 6.3: Implement**

Extend `PageRegistry.Register<TViewModel,TView>(PageKey key, string displayName)` while preserving existing callers with optional/default display names. Update `RegisterPages` with every shipped page display name. Change `NavigationBreadcrumbSource` to take `PageRegistry` and read `GetDisplayName`. Change `LeftNavViewModel` constructor to accept `PageRegistry` and build `NavRailItem` labels from it.

- [x] **Step 6.4: Verify and commit**

Run focused tests and `dotnet build Tianming.MacMigration.sln`.

Commit with title: `Centralize page display names in PageRegistry`.

## Step 7: Show provider display names in ModelManagementPage

- [x] **Step 7.1: Write red test**

Add to `ModelManagementViewModelTests`:

```csharp
Assert.Contains(vm.Providers, provider => provider.Id == "openai" && provider.DisplayName == "OpenAI");
Assert.DoesNotContain(vm.Providers, provider => provider.DisplayName == provider.Id);
```

- [x] **Step 7.2: Run red**

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter Providers_include_display_names`

Expected: FAIL because the VM exposes only `ProviderIds`.

- [x] **Step 7.3: Implement**

Make `DefaultAIProviderOption` expose `DisplayName`; add `ObservableCollection<DefaultAIProviderOption> Providers` to `ModelManagementViewModel`; keep `ProviderIds` for existing edit rows. Update `ModelManagementPage.axaml` ComboBox to bind `Providers`, selected value to `NewProviderId`, and render `DisplayName`.

- [x] **Step 7.4: Verify and commit**

Run the focused test and `dotnet build Tianming.MacMigration.sln`.

Commit with title: `Show AI provider display names in model setup`.

## Step 8: Document R3 IME known limitation

- [x] **Step 8.1: Update巡检文档**

Under R3 in `Docs/macOS迁移/M5-ComputerUse-功能巡检-2026-05-14.md`, add a subheading `平台已知限制：macOS 中文输入法候选条` with symptom, workaround, and long-term check.

- [x] **Step 8.2: Verify and commit**

Run: `rg -n "平台已知限制：macOS 中文输入法候选条|英文输入|IME composition" Docs/macOS迁移/M5-ComputerUse-功能巡检-2026-05-14.md`

Commit with title: `Document the macOS IME limitation for at-references`.

## Final Verification

- [ ] Run `dotnet build Tianming.MacMigration.sln` and confirm 0 warnings / 0 errors.
- [ ] Run `dotnet test Tianming.MacMigration.sln` and confirm all tests pass.
- [ ] Run `Scripts/build-dev-bundle.sh`, `codesign -dvv /tmp/TianmingDev.app`, and `plutil -p /tmp/TianmingDev.app/Contents/Info.plist`.
- [ ] Launch `/tmp/TianmingDev.app` and verify Computer Use `get_app_state("dev.tianming.avalonia.manualtest")` returns a window tree.
- [ ] Capture startup log evidence that appearance monitoring initialized or note if no explicit log exists.
