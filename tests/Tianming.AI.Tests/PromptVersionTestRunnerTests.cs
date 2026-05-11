using TM.Framework.Common.Helpers.AI;
using TM.Modules.AIAssistant.PromptTools.PromptManagement.Models;
using TM.Modules.AIAssistant.PromptTools.VersionTesting.Models;
using TM.Modules.AIAssistant.PromptTools.VersionTesting.Services;
using Xunit;

namespace Tianming.AI.Tests;

public class PromptVersionTestRunnerTests
{
    [Fact]
    public async Task RunAsync_generates_output_and_persists_completed_test_version()
    {
        using var workspace = new TempDirectory();
        var store = new FilePromptVersionStore(Path.Combine(workspace.Path, "test_versions.json"));
        store.AddVersion(new TestVersionData
        {
            Id = "case-1",
            Name = "章节续写测试",
            Category = "业务提示词",
            PromptId = "prompt-1",
            VersionNumber = "1.0",
            TestInput = "写一个雨夜重逢场景",
            ExpectedOutput = "包含雨夜和重逢"
        });
        var generator = new CapturingGenerator(new PromptGenerationAiResult(true, "雨夜里，两人终于重逢。"));
        var runner = new PromptVersionTestRunner(store, generator);

        PromptVersionTestRunResult result = await runner.RunAsync("case-1");

        Assert.True(result.Success);
        Assert.Equal("已完成", result.Status);
        Assert.Equal("雨夜里，两人终于重逢。", result.Output);
        Assert.Single(generator.Requests);
        Assert.Equal("写一个雨夜重逢场景", generator.Requests[0]);

        var persisted = Assert.Single(new FilePromptVersionStore(Path.Combine(workspace.Path, "test_versions.json")).GetAllVersions());
        Assert.Equal("雨夜里，两人终于重逢。", persisted.ActualOutput);
        Assert.Equal("已完成", persisted.TestStatus);
        Assert.NotNull(persisted.TestTime);
    }

    [Fact]
    public async Task RunAsync_maps_generator_failure_to_failed_status_without_overwriting_existing_output()
    {
        using var workspace = new TempDirectory();
        var store = new FilePromptVersionStore(Path.Combine(workspace.Path, "test_versions.json"));
        store.AddVersion(new TestVersionData
        {
            Id = "case-2",
            Name = "失败保留输出",
            TestInput = "生成冲突场景",
            ActualOutput = "旧输出",
            TestStatus = "已完成"
        });
        var runner = new PromptVersionTestRunner(
            store,
            new CapturingGenerator(new PromptGenerationAiResult(false, string.Empty, "模型限流")));

        PromptVersionTestRunResult result = await runner.RunAsync("case-2");

        Assert.False(result.Success);
        Assert.Equal("测试失败", result.Status);
        Assert.Equal("模型限流", result.ErrorMessage);

        var persisted = Assert.Single(new FilePromptVersionStore(Path.Combine(workspace.Path, "test_versions.json")).GetAllVersions());
        Assert.Equal("旧输出", persisted.ActualOutput);
        Assert.Equal("测试失败", persisted.TestStatus);
        Assert.Equal("模型限流", persisted.TestNotes);
        Assert.NotNull(persisted.TestTime);
    }

    [Fact]
    public async Task CompareAsync_runs_two_prompt_templates_against_the_same_input()
    {
        var generator = new QueueGenerator(
            new PromptGenerationAiResult(true, "A版本输出"),
            new PromptGenerationAiResult(true, "B版本输出"));
        var runner = new PromptVersionTestRunner(new InMemoryPromptVersionStore(), generator);

        PromptVersionComparisonResult result = await runner.CompareAsync(
            new PromptTemplateData
            {
                Id = "a",
                Name = "细腻版",
                SystemPrompt = "你擅长细腻情绪描写",
                UserTemplate = "请扩写：{input}"
            },
            new PromptTemplateData
            {
                Id = "b",
                Name = "紧凑版",
                SystemPrompt = "你擅长快节奏冲突",
                UserTemplate = "请改写：{input}"
            },
            "主角推门进入旧书店");

        Assert.True(result.Success);
        Assert.Equal("A版本输出", result.Left.Output);
        Assert.Equal("B版本输出", result.Right.Output);
        Assert.Equal(2, generator.Requests.Count);
        Assert.Contains("你擅长细腻情绪描写", generator.Requests[0]);
        Assert.Contains("请扩写：主角推门进入旧书店", generator.Requests[0]);
        Assert.Contains("你擅长快节奏冲突", generator.Requests[1]);
        Assert.Contains("请改写：主角推门进入旧书店", generator.Requests[1]);
    }

    private sealed class CapturingGenerator : IPromptTextGenerator
    {
        private readonly PromptGenerationAiResult _result;

        public List<string> Requests { get; } = new();

        public CapturingGenerator(PromptGenerationAiResult result)
        {
            _result = result;
        }

        public Task<PromptGenerationAiResult> GenerateAsync(string prompt)
        {
            Requests.Add(prompt);
            return Task.FromResult(_result);
        }
    }

    private sealed class QueueGenerator : IPromptTextGenerator
    {
        private readonly Queue<PromptGenerationAiResult> _results;

        public List<string> Requests { get; } = new();

        public QueueGenerator(params PromptGenerationAiResult[] results)
        {
            _results = new Queue<PromptGenerationAiResult>(results);
        }

        public Task<PromptGenerationAiResult> GenerateAsync(string prompt)
        {
            Requests.Add(prompt);
            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class InMemoryPromptVersionStore : IPromptVersionStore
    {
        public List<TestVersionData> Versions { get; } = new();

        public IReadOnlyList<TestVersionData> GetAllVersions()
        {
            return Versions.ToList();
        }

        public void AddVersion(TestVersionData version)
        {
            Versions.RemoveAll(item => item.Id == version.Id);
            Versions.Add(version);
        }

        public void UpdateVersion(TestVersionData version)
        {
            Versions.RemoveAll(item => item.Id == version.Id);
            Versions.Add(version);
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-prompt-runner-{Guid.NewGuid():N}");

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
