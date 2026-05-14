# Tianming M7 Lane R Regressions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the three Round 7 Computer Use regressions that block visible macOS Avalonia workflows: BookPipeline navigation, AI provider selection, and `@ch` reference suggestions.

**Architecture:** Keep fixes inside the desktop Avalonia lane. Treat the UI symptom as view-model/nav-source state first, and add tests around the same public view-model surfaces used by the shell. Provider defaults live in the AI management view-model layer so empty local model-library files still leave the UI configurable.

**Tech Stack:** .NET 8, Avalonia 11, xUnit, CommunityToolkit.Mvvm, existing `FileAIConfigurationStore`, existing `ModuleDataAdapter` project-data fixtures.

---

## File Structure

- Create: `Scripts/build-dev-bundle.sh` - builds Debug output and wraps it as `/tmp/TianmingDev.app` with bundle id `dev.tianming.avalonia.manualtest`.
- Create: `Docs/macOS迁移/manual-test-howto.md` - records the Computer Use attach flow for the dev bundle.
- Modify: `Docs/macOS迁移/M5-ComputerUse-功能巡检-2026-05-14.md` - marks R1/R2/R3 as fixed or reclassified with commit evidence.
- Modify: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/ThreeColumnLayoutViewModelTests.cs` - locks the shell center-content navigation regression.
- Modify: `src/Tianming.Desktop.Avalonia/Infrastructure/NavigationBreadcrumbSource.cs` - adds labels for shipped pages so visible breadcrumb text is not raw ids.
- Modify: `src/Tianming.Desktop.Avalonia/ViewModels/AI/ModelManagementViewModel.cs` - exposes provider choices even when the on-disk library is empty.
- Modify: `src/Tianming.Desktop.Avalonia/ViewModels/AI/ApiKeysViewModel.cs` - exposes provider groups even when no user configurations exist.
- Modify: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/AI/ModelManagementViewModelTests.cs` - provider fallback tests.
- Create: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/AI/ApiKeysViewModelTests.cs` - provider fallback tests for API keys.
- Modify: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Conversation/ReferenceSuggestionSourceTests.cs` - fixture test for `SuggestAsync("ch")`.
- Modify if required by the red test: `src/Tianming.Desktop.Avalonia/ViewModels/Conversation/ReferenceSuggestionSource.cs`.

## Task 0: Commit the macOS dev-bundle manual-test flow

**Files:**
- Create: `Scripts/build-dev-bundle.sh`
- Create: `Docs/macOS迁移/manual-test-howto.md`

- [ ] **Step 1: Write the tool and how-to**

Create `Scripts/build-dev-bundle.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PROJECT="$ROOT/src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj"
CONFIGURATION="${CONFIGURATION:-Debug}"
FRAMEWORK="${FRAMEWORK:-net8.0}"
APP_DIR="${APP_DIR:-/tmp/TianmingDev.app}"
BUNDLE_ID="${BUNDLE_ID:-dev.tianming.avalonia.manualtest}"
PUBLISH_DIR="$ROOT/src/Tianming.Desktop.Avalonia/bin/$CONFIGURATION/$FRAMEWORK"

dotnet build "$PROJECT" -c "$CONFIGURATION" --no-restore
rm -rf "$APP_DIR"
mkdir -p "$APP_DIR/Contents/MacOS" "$APP_DIR/Contents/Resources"
cp -R "$PUBLISH_DIR/"* "$APP_DIR/Contents/MacOS/"
chmod +x "$APP_DIR/Contents/MacOS/Tianming.Desktop.Avalonia"
cat > "$APP_DIR/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleExecutable</key><string>Tianming.Desktop.Avalonia</string>
  <key>CFBundleIdentifier</key><string>$BUNDLE_ID</string>
  <key>CFBundleName</key><string>TianmingDev</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleVersion</key><string>1</string>
  <key>CFBundleShortVersionString</key><string>0.0.0-dev</string>
</dict>
</plist>
PLIST
echo "$APP_DIR"
```

Create `Docs/macOS迁移/manual-test-howto.md` with:

```markdown
# macOS Avalonia Manual Test How-To

## Computer Use attach flow

1. Run `Scripts/build-dev-bundle.sh`.
2. Launch `/tmp/TianmingDev.app`.
3. Attach Computer Use to bundle id `dev.tianming.avalonia.manualtest`.
4. Verify the visible window title is `天命`.

This wrapper exists because `dotnet run --project src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj` starts a visible process without a stable `CFBundleIdentifier`, which prevents Computer Use from attaching reliably on macOS.
```

- [ ] **Step 2: Verify the script is syntactically valid**

Run: `bash -n Scripts/build-dev-bundle.sh`
Expected: exit 0.

- [ ] **Step 3: Run the script**

Run: `Scripts/build-dev-bundle.sh`
Expected: prints `/tmp/TianmingDev.app`; `/tmp/TianmingDev.app/Contents/Info.plist` contains `dev.tianming.avalonia.manualtest`.

- [ ] **Step 4: Commit**

```bash
git add Scripts/build-dev-bundle.sh Docs/macOS迁移/manual-test-howto.md
git commit -m "Preserve the macOS manual-test bundle path

Round 7 Computer Use verification needs a stable bundle id so the dev app can be attached as a real macOS application.

Constraint: Direct dotnet run lacks a visible CFBundleIdentifier for Computer Use attach
Confidence: high
Scope-risk: narrow
Tested: bash -n Scripts/build-dev-bundle.sh; Scripts/build-dev-bundle.sh
Not-tested: Codesigned release packaging"
```

## Task 1: Fix and lock BookPipeline center-content navigation

**Files:**
- Modify: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/ThreeColumnLayoutViewModelTests.cs`
- Modify: `src/Tianming.Desktop.Avalonia/Infrastructure/NavigationBreadcrumbSource.cs`
- Modify if required: `src/Tianming.Desktop.Avalonia/ViewModels/ThreeColumnLayoutViewModel.cs`

- [ ] **Step 1: Write the failing regression test**

Add a test that builds the real `AppHost`, navigates from `PageKeys.AIUsage` to `PageKeys.BookPipeline`, and asserts the layout center changes to `BookPipelineViewModel`:

```csharp
[Fact]
public async Task Navigate_to_book_pipeline_replaces_previous_center_page()
{
    using var sp = (ServiceProvider)AppHost.Build();
    var layout = sp.GetRequiredService<ThreeColumnLayoutViewModel>();
    var nav = sp.GetRequiredService<INavigationService>();

    await nav.NavigateAsync(PageKeys.AIUsage);
    Assert.IsType<UsageStatisticsViewModel>(layout.Center);

    await nav.NavigateAsync(PageKeys.BookPipeline);

    Assert.IsType<BookPipelineViewModel>(layout.Center);
}
```

- [ ] **Step 2: Run the focused test and confirm red**

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter Navigate_to_book_pipeline_replaces_previous_center_page`
Expected before fix: FAIL because the shell center does not switch to `BookPipelineViewModel`, or because the test exposes the current hidden cause.

- [ ] **Step 3: Implement the smallest fix**

If the red failure is missing label-only evidence, update `NavigationBreadcrumbSource.KnownLabels` to include:

```csharp
[PageKeys.AIUsage] = "使用统计",
[PageKeys.BookPipeline] = "一键成书",
```

If the red failure proves `ThreeColumnLayoutViewModel.Center` is not refreshed, update `OnNavigated` to assign the latest `_nav.CurrentViewModel` on every navigation event.

- [ ] **Step 4: Verify green and build**

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter Navigate_to_book_pipeline_replaces_previous_center_page`
Expected: PASS.

Run: `dotnet build Tianming.MacMigration.sln`
Expected: 0 warnings, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add tests/Tianming.Desktop.Avalonia.Tests/ViewModels/ThreeColumnLayoutViewModelTests.cs src/Tianming.Desktop.Avalonia/Infrastructure/NavigationBreadcrumbSource.cs src/Tianming.Desktop.Avalonia/ViewModels/ThreeColumnLayoutViewModel.cs
git commit -m "Keep BookPipeline navigation visible in the shell

The Computer Use repro showed the breadcrumb advancing while the center page stayed on usage statistics, so the regression test now locks the user-visible center page transition.

Constraint: Lane R is limited to the shipped Avalonia navigation path
Rejected: Replace the navigation service | broader than the observed regression
Confidence: medium
Scope-risk: narrow
Tested: dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter Navigate_to_book_pipeline_replaces_previous_center_page; dotnet build Tianming.MacMigration.sln
Not-tested: Manual Computer Use rerun until final bundle verification"
```

## Task 2: Fix AI provider dropdown sources

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/ViewModels/AI/ModelManagementViewModel.cs`
- Modify: `src/Tianming.Desktop.Avalonia/ViewModels/AI/ApiKeysViewModel.cs`
- Modify: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/AI/ModelManagementViewModelTests.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/AI/ApiKeysViewModelTests.cs`

- [ ] **Step 1: Write failing provider-source tests**

In `ModelManagementViewModelTests`, add:

```csharp
[Fact]
public void Empty_library_still_exposes_default_provider_ids()
{
    using var workspace = new TempDirectory();
    var store = new FileAIConfigurationStore(Path.Combine(workspace.Path, "Library"), Path.Combine(workspace.Path, "Configurations"));

    var vm = new ModelManagementViewModel(store);

    Assert.Contains("openai", vm.ProviderIds);
    Assert.All(vm.ProviderIds, providerId => Assert.False(string.IsNullOrWhiteSpace(providerId)));
}
```

Create `ApiKeysViewModelTests` with an in-memory `IApiKeySecretStore`, then assert:

```csharp
[Fact]
public void Empty_library_still_exposes_default_provider_groups()
{
    using var workspace = new TempDirectory();
    var store = new FileAIConfigurationStore(Path.Combine(workspace.Path, "Library"), Path.Combine(workspace.Path, "Configurations"));

    var vm = new ApiKeysViewModel(store, new InMemorySecretStore());

    Assert.Contains(vm.Providers, provider => provider.ProviderId == "openai" && !string.IsNullOrWhiteSpace(provider.ProviderName));
}
```

- [ ] **Step 2: Run focused tests and confirm red**

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter "Empty_library_still_exposes_default"`
Expected before fix: FAIL because empty on-disk libraries produce no provider options.

- [ ] **Step 3: Implement provider fallback**

Add a private default provider list shared by both view-models or duplicated locally if that is the smaller diff:

```csharp
private static readonly (string Id, string Name)[] DefaultProviders =
[
    ("openai", "OpenAI"),
    ("anthropic", "Anthropic"),
    ("google", "Google Gemini"),
    ("azure-openai", "Azure OpenAI"),
    ("deepseek", "DeepSeek"),
    ("cherry-studio", "Cherry Studio"),
];
```

Use `_store.GetAllProviders()` when present; otherwise use the fallback list. `ModelManagementViewModel.ProviderIds` receives ids. `ApiKeysViewModel.Providers` receives empty `ProviderKeyGroup` entries so the ComboBox and grouped list both have visible options before user configurations exist.

- [ ] **Step 4: Verify green and build**

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter "Empty_library_still_exposes_default"`
Expected: PASS.

Run: `dotnet build Tianming.MacMigration.sln`
Expected: 0 warnings, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/Tianming.Desktop.Avalonia/ViewModels/AI/ModelManagementViewModel.cs src/Tianming.Desktop.Avalonia/ViewModels/AI/ApiKeysViewModel.cs tests/Tianming.Desktop.Avalonia.Tests/ViewModels/AI/ModelManagementViewModelTests.cs tests/Tianming.Desktop.Avalonia.Tests/ViewModels/AI/ApiKeysViewModelTests.cs
git commit -m "Keep AI provider dropdowns populated without local library data

The macOS UI must allow first-run model and key setup even when the model library directory has not been seeded yet.

Constraint: FileAIConfigurationStore legitimately returns an empty provider list for an empty library path
Rejected: Require users to create provider JSON before opening settings | first-run UI would remain unusable
Confidence: high
Scope-risk: narrow
Tested: dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter Empty_library_still_exposes_default; dotnet build Tianming.MacMigration.sln
Not-tested: Real provider API calls"
```

## Task 3: Reproduce and fix `@ch` reference suggestions with fixture data

**Files:**
- Modify: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Conversation/ReferenceSuggestionSourceTests.cs`
- Modify if required: `src/Tianming.Desktop.Avalonia/ViewModels/Conversation/ReferenceSuggestionSource.cs`
- Modify: `Docs/macOS迁移/M5-ComputerUse-功能巡检-2026-05-14.md`

- [ ] **Step 1: Write the fixture regression test**

Add:

```csharp
[Fact]
public async Task SuggestAsync_ch_returns_matching_chapter_fixture()
{
    using var workspace = new TempDirectory();
    var adapter = new ModuleDataAdapter<ChapterCategory, ChapterData>(new ChapterPlanningSchema(), workspace.Path);
    await adapter.LoadAsync();
    await adapter.AddCategoryAsync(new ChapterCategory { Id = "chapter-cat", Name = "章节", IsEnabled = true });
    await adapter.AddAsync(new ChapterData
    {
        Id = "chapter-ch001",
        Category = "章节",
        Name = "ch001 破局",
        ChapterTitle = "ch001 破局",
        IsEnabled = true,
    });
    var source = new ReferenceSuggestionSource(new StubCurrentProjectService(workspace.Path));

    var results = await source.SuggestAsync("ch");

    Assert.Contains(results, item => item.Category == "Chapter" && item.Name.Contains("ch001", StringComparison.OrdinalIgnoreCase));
}
```

- [ ] **Step 2: Run the focused test and confirm red or existing green**

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter SuggestAsync_ch_returns_matching_chapter_fixture`
Expected: FAIL if the data source cannot match the fixture; PASS means R3 is not a data-source bug and is likely fixture/IME/manual-state dependent.

- [ ] **Step 3: Implement only if the test is red**

If red, update `ReferenceSuggestionSource.GetChapterDisplayName` or filtering so `ChapterData.Name`, numbered title, and bare `ChapterTitle` are all searchable. If green, do not change production code; document that code-level data-source verification passed and final manual rerun must use a seeded fixture project.

- [ ] **Step 4: Update巡检 evidence and verify**

Update the R1/R2/R3 sections in `Docs/macOS迁移/M5-ComputerUse-功能巡检-2026-05-14.md` with `已修复` or `代码层已排除数据源问题，需 fixture 实机复测`, plus the relevant short commit ids after commits exist.

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter "SuggestAsync_ch_returns_matching_chapter_fixture|Navigate_to_book_pipeline_replaces_previous_center_page|Empty_library_still_exposes_default"`
Expected: PASS.

Run: `dotnet build Tianming.MacMigration.sln`
Expected: 0 warnings, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Conversation/ReferenceSuggestionSourceTests.cs src/Tianming.Desktop.Avalonia/ViewModels/Conversation/ReferenceSuggestionSource.cs Docs/macOS迁移/M5-ComputerUse-功能巡检-2026-05-14.md
git commit -m "Classify at-reference suggestions with fixture coverage

The Computer Use repro used @ch without a known matching project fixture, so this locks the data-source behavior before deciding whether the remaining symptom is manual-state or IME related.

Constraint: R3 root cause was inconclusive in the original巡检 evidence
Rejected: Patch the popup blindly | data-source and invocation paths need separate proof
Confidence: medium
Scope-risk: narrow
Tested: dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter SuggestAsync_ch_returns_matching_chapter_fixture; dotnet build Tianming.MacMigration.sln
Not-tested: Final Computer Use fixture rerun until bundle verification"
```

## Final Verification

- [ ] Run `dotnet build Tianming.MacMigration.sln` and confirm 0 warnings / 0 errors.
- [ ] Run `dotnet test Tianming.MacMigration.sln` and confirm all tests pass with at least 4 new tests.
- [ ] Run `bash -n Scripts/build-dev-bundle.sh`.
- [ ] Run `Scripts/build-dev-bundle.sh` and confirm `/tmp/TianmingDev.app` exists with bundle id `dev.tianming.avalonia.manualtest`.
- [ ] Launch `/tmp/TianmingDev.app` and rerun R1/R2/R3 through Computer Use when available in the current environment.
- [ ] Report all commits, Task 0-3 status, final test evidence, and any remaining manual verification gaps. Do not push.
