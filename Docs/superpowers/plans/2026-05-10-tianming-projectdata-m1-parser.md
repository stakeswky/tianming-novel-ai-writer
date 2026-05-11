# Tianming ProjectData M1 Parser Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create the first macOS-buildable ProjectData class library and prove `AIOutputParser` works under `net8.0` without WPF.

**Architecture:** Start with a narrow linked-source extraction rather than moving files. The new class library compiles selected existing ProjectData and Framework model/helper files as links, preserving namespaces and avoiding behavior drift; tests exercise parser behavior through the public API.

**Tech Stack:** .NET 8, xUnit, linked C# source files, existing `System.Text.Json` parser code.

**Progress note, 2026-05-10:** This lane has expanded from parser proof to the first macOS-ready ProjectData support surface. Project management, generic module categories/data storage, file-backed version tracking, `AIOutputParser`, CHANGES protocol parsing, ledger consistency checking, content entity/description validation, gate result models, a cross-platform `GenerationGate` orchestrator, content preparation, chapter file storage/navigation, tracking dispatch, file-backed Guide persistence, file-backed keyword indexing, a WPF-free writing navigation catalog, file-backed module enablement and change detection, default package mapping derived from that catalog, package JSON assembly plus all/default and single-module publish orchestration, manifest/history metadata with restore overwrite confirmation preflight/protection, published package statistics, generated-chapter rewrite/delete cascade, a minimal generation save pipeline, and generation statistics now build under `src/Tianming.ProjectData` with 89 passing xUnit tests.

---

## Scope Check

This plan implements only the first ProjectData extraction lane. It does not migrate Avalonia UI, GenerationGate, AI chat, platform services, or storage abstractions. Those remain later M1/M2 tasks after this first cross-platform build/test proof exists.

## Files

- Create: `src/Tianming.ProjectData/Tianming.ProjectData.csproj`
  - Cross-platform `net8.0` class library using linked source files.
- Create: `tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj`
  - xUnit test project.
- Create: `tests/Tianming.ProjectData.Tests/AIOutputParserTests.cs`
  - Parser behavior tests.
- Modify: `Docs/macOS迁移/M0-环境与阻塞基线.md`
  - Add the M1 parser proof after tests pass.

## Task 1: Create Cross-Platform ProjectData Library

**Files:**
- Create: `src/Tianming.ProjectData/Tianming.ProjectData.csproj`

- [x] **Step 1: Create the library project file**

Create `src/Tianming.ProjectData/Tianming.ProjectData.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="../../Framework/Common/Helpers/Id/ShortIdGenerator.cs" Link="Framework/Common/Helpers/Id/ShortIdGenerator.cs" />
    <Compile Include="../../Framework/Common/Models/IEnableable.cs" Link="Framework/Common/Models/IEnableable.cs" />
    <Compile Include="../../Framework/Common/Models/IDataItem.cs" Link="Framework/Common/Models/IDataItem.cs" />
    <Compile Include="../../Framework/Common/Models/IDependencyTracked.cs" Link="Framework/Common/Models/IDependencyTracked.cs" />
    <Compile Include="../../Services/Modules/ProjectData/Models/Validate/ValidationSummary/ModuleValidationResult.cs" Link="Services/Modules/ProjectData/Models/Validate/ValidationSummary/ModuleValidationResult.cs" />
    <Compile Include="../../Services/Modules/ProjectData/Models/Validate/ValidationSummary/ValidationRules.cs" Link="Services/Modules/ProjectData/Models/Validate/ValidationSummary/ValidationRules.cs" />
    <Compile Include="../../Services/Modules/ProjectData/Models/Validate/ValidationSummary/ValidationSummaryData.cs" Link="Services/Modules/ProjectData/Models/Validate/ValidationSummary/ValidationSummaryData.cs" />
    <Compile Include="../../Services/Modules/ProjectData/Implementations/Generation/AIOutputParser.cs" Link="Services/Modules/ProjectData/Implementations/Generation/AIOutputParser.cs" />
  </ItemGroup>
</Project>
```

- [x] **Step 2: Build the library**

Run:

```bash
dotnet build src/Tianming.ProjectData/Tianming.ProjectData.csproj -v minimal
```

Expected:

```text
Build succeeded.
```

## Task 2: Add Parser Tests First

**Files:**
- Create: `tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj`
- Create: `tests/Tianming.ProjectData.Tests/AIOutputParserTests.cs`

- [x] **Step 1: Create the test project**

Create `tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/Tianming.ProjectData/Tianming.ProjectData.csproj" />
  </ItemGroup>
</Project>
```

- [x] **Step 2: Write parser behavior tests**

Create `tests/Tianming.ProjectData.Tests/AIOutputParserTests.cs`:

```csharp
using System;
using System.Linq;
using System.Text.Json;
using TM.Services.Modules.ProjectData.Implementations;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class AIOutputParserTests
{
    [Fact]
    public void ParseAIOutput_accepts_json_wrapped_in_model_text()
    {
        var parser = new AIOutputParser();

        var result = parser.ParseAIOutput("前置说明\n" + ValidPayload() + "\n后置说明", 3);

        Assert.Equal(3, result.TargetVolumeNumber);
        Assert.Equal("第三卷", result.TargetVolumeName);
        Assert.Equal("通过", result.OverallResult);
        Assert.Equal(10, result.ModuleResults.Count);
        Assert.All(result.ModuleResults, module => Assert.Equal("通过", module.Result));
        Assert.StartsWith("D", result.Id);
    }

    [Fact]
    public void ParseAIOutput_rejects_missing_module_results()
    {
        var parser = new AIOutputParser();
        var payload = """
        {
          "Volume": { "VolumeNumber": 1 },
          "OverallResult": "失败"
        }
        """;

        var ex = Assert.Throws<InvalidOperationException>(() => parser.ParseAIOutput(payload, 1));

        Assert.Contains("moduleResults", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseAIOutput_rejects_missing_extended_data_fields()
    {
        var parser = new AIOutputParser();
        var payload = ValidPayload().Replace("\"styleHint\":\"节奏快\"", "\"styleHint_missing\":\"节奏快\"");

        var ex = Assert.Throws<JsonException>(() => parser.ParseAIOutput(payload, 1));

        Assert.Contains("extendedData缺少字段", ex.Message);
    }

    private static string ValidPayload()
    {
        return """
        {
          "Volume": {
            "VolumeNumber": 3,
            "VolumeName": "第三卷",
            "SampledChapterCount": 2,
            "SampledChapterIds": ["C1", "C2"]
          },
          "OverallResult": "通过",
          "DependencyModuleVersions": { "Outline": 2 },
          "ModuleResults": [
            { "ModuleName": "StyleConsistency", "Result": "通过", "ExtendedData": { "templateName":"热血", "genre":"玄幻", "overallIdea":"升级", "styleHint":"节奏快" } },
            { "ModuleName": "WorldviewConsistency", "Result": "通过", "ExtendedData": { "worldRuleName":"灵气", "hardRules":"不可复活", "powerSystem":"修炼", "specialLaws":"天罚" } },
            { "ModuleName": "CharacterConsistency", "Result": "通过", "ExtendedData": { "characterName":"林衡", "identity":"弟子", "coreTraits":"谨慎", "arcGoal":"破境" } },
            { "ModuleName": "FactionConsistency", "Result": "通过", "ExtendedData": { "factionName":"青岚宗", "factionType":"宗门", "goal":"守山", "leader":"宗主" } },
            { "ModuleName": "LocationConsistency", "Result": "通过", "ExtendedData": { "locationName":"寒潭", "locationType":"秘境", "description":"深冷", "terrain":"山谷" } },
            { "ModuleName": "PlotConsistency", "Result": "通过", "ExtendedData": { "plotName":"试炼", "storyPhase":"开端", "goal":"取火", "conflict":"争夺", "result":"胜出" } },
            { "ModuleName": "OutlineConsistency", "Result": "通过", "ExtendedData": { "oneLineOutline":"少年入山", "coreConflict":"身份", "theme":"成长", "endingState":"入门" } },
            { "ModuleName": "ChapterPlanConsistency", "Result": "通过", "ExtendedData": { "chapterTitle":"入山", "mainGoal":"拜师", "keyTurn":"被拒", "hook":"异象", "foreshadowing":"玉佩" } },
            { "ModuleName": "BlueprintConsistency", "Result": "通过", "ExtendedData": { "chapterId":"C1", "oneLineStructure":"起承转合", "pacingCurve":"快慢快", "cast":"林衡", "locations":"山门" } },
            { "ModuleName": "VolumeDesignConsistency", "Result": "通过", "ExtendedData": { "volumeTitle":"山门卷", "volumeTheme":"入世", "stageGoal":"立足", "mainConflict":"门规", "keyEvents":"试炼" } }
          ]
        }
        """;
    }
}
```

- [x] **Step 3: Run tests and verify they pass**

Run:

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj -v minimal
```

Expected:

```text
Passed!
```

## Task 3: Record M1 Parser Proof

**Files:**
- Modify: `Docs/macOS迁移/M0-环境与阻塞基线.md`

- [x] **Step 1: Add M1 proof section**

Append this section to `Docs/macOS迁移/M0-环境与阻塞基线.md`:

```markdown
## M1 首个跨平台验证点

- 新增类库：`src/Tianming.ProjectData/Tianming.ProjectData.csproj`
- 新增测试：`tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj`
- 验证对象：`AIOutputParser`
- 验证结果：`dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj -v minimal` 通过。
- 意义：ProjectData 的第一条业务链路已经可以在 macOS `net8.0` 下构建和测试，不依赖 WPF。
```

- [x] **Step 2: Run final verification**

Run:

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj -v minimal
Scripts/check-windows-bindings.sh > /tmp/tianming-windows-bindings.txt
git status --short
```

Expected:

```text
Build succeeded.
Passed!
```

`git status --short` should show the new M1 library/test files, migration docs, scanner script, and `global.json`.

## Spec Coverage Self-Review

- M1 parser proof advances the macOS conversion by creating the first `net8.0` ProjectData build target.
- It preserves original behavior by linking existing source files instead of copying or rewriting parser code.
- It verifies parser parity with passing tests for successful payload parsing, missing module results, and missing extended-data fields.
- It does not complete the full macOS app conversion; UI and platform services remain later phases.

## Execution Handoff

Proceed with inline execution. The scope is small enough for one session and follows TDD through parser behavior tests.
