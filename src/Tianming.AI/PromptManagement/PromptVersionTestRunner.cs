using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using TM.Framework.Common.Helpers.AI;
using TM.Modules.AIAssistant.PromptTools.PromptManagement.Models;
using TM.Modules.AIAssistant.PromptTools.VersionTesting.Models;

namespace TM.Modules.AIAssistant.PromptTools.VersionTesting.Services;

public sealed class PromptVersionTestRunner
{
    private readonly IPromptVersionStore _store;
    private readonly IPromptTextGenerator _generator;

    public PromptVersionTestRunner(IPromptVersionStore store, IPromptTextGenerator generator)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
    }

    public async Task<PromptVersionTestRunResult> RunAsync(string versionId)
    {
        if (string.IsNullOrWhiteSpace(versionId))
            return PromptVersionTestRunResult.Failed("测试版本不存在");

        var version = _store.GetAllVersions()
            .FirstOrDefault(item => string.Equals(item.Id, versionId, StringComparison.Ordinal));

        if (version == null)
            return PromptVersionTestRunResult.Failed("测试版本不存在");

        return await RunAsync(version);
    }

    public async Task<PromptVersionTestRunResult> RunAsync(TestVersionData version)
    {
        if (version == null)
            throw new ArgumentNullException(nameof(version));

        if (string.IsNullOrWhiteSpace(version.TestInput))
            return PromptVersionTestRunResult.Failed("测试输入为空");

        var stopwatch = Stopwatch.StartNew();
        var aiResult = await _generator.GenerateAsync(version.TestInput);
        stopwatch.Stop();

        var updated = Clone(version);
        updated.TestTime = DateTime.Now;

        if (!aiResult.Success || string.IsNullOrWhiteSpace(aiResult.Content))
        {
            var message = string.IsNullOrWhiteSpace(aiResult.ErrorMessage)
                ? "AI未返回有效内容，请检查模型配置后重试"
                : aiResult.ErrorMessage;

            updated.TestStatus = "测试失败";
            updated.TestNotes = message;
            _store.UpdateVersion(updated);

            return PromptVersionTestRunResult.Failed(message, "测试失败", stopwatch.Elapsed);
        }

        updated.ActualOutput = aiResult.Content;
        updated.TestStatus = "已完成";
        updated.TestNotes = string.Empty;
        _store.UpdateVersion(updated);

        return PromptVersionTestRunResult.Completed(aiResult.Content, stopwatch.Elapsed);
    }

    public async Task<PromptVersionComparisonResult> CompareAsync(
        PromptTemplateData left,
        PromptTemplateData right,
        string input)
    {
        if (left == null)
            throw new ArgumentNullException(nameof(left));
        if (right == null)
            throw new ArgumentNullException(nameof(right));
        if (string.IsNullOrWhiteSpace(input))
        {
            var failed = PromptTemplateTestRunResult.Failed("测试输入为空");
            return new PromptVersionComparisonResult(false, failed, failed);
        }

        var leftResult = await RunTemplateAsync(left, input);
        var rightResult = await RunTemplateAsync(right, input);

        return new PromptVersionComparisonResult(
            leftResult.Success && rightResult.Success,
            leftResult,
            rightResult);
    }

    private async Task<PromptTemplateTestRunResult> RunTemplateAsync(PromptTemplateData template, string input)
    {
        var prompt = BuildPrompt(template, input);
        var stopwatch = Stopwatch.StartNew();
        var aiResult = await _generator.GenerateAsync(prompt);
        stopwatch.Stop();

        if (!aiResult.Success || string.IsNullOrWhiteSpace(aiResult.Content))
        {
            var message = string.IsNullOrWhiteSpace(aiResult.ErrorMessage)
                ? "AI未返回有效内容，请检查模型配置后重试"
                : aiResult.ErrorMessage;
            return PromptTemplateTestRunResult.Failed(message, template.Id, template.Name, stopwatch.Elapsed);
        }

        return PromptTemplateTestRunResult.Completed(
            template.Id,
            template.Name,
            aiResult.Content,
            stopwatch.Elapsed);
    }

    private static string BuildPrompt(PromptTemplateData template, string input)
    {
        var userPrompt = string.IsNullOrWhiteSpace(template.UserTemplate)
            ? input
            : template.UserTemplate
                .Replace("{input}", input, StringComparison.Ordinal)
                .Replace("{{input}}", input, StringComparison.Ordinal)
                .Replace("{输入}", input, StringComparison.Ordinal)
                .Replace("{{输入}}", input, StringComparison.Ordinal);

        if (string.IsNullOrWhiteSpace(template.SystemPrompt))
            return userPrompt;

        return $"系统提示词:\n{template.SystemPrompt.Trim()}\n\n测试输入:\n{userPrompt.Trim()}";
    }

    private static TestVersionData Clone(TestVersionData version)
    {
        return new TestVersionData
        {
            Id = version.Id,
            Name = version.Name,
            Category = version.Category,
            CategoryId = version.CategoryId,
            IsEnabled = version.IsEnabled,
            PromptId = version.PromptId,
            VersionNumber = version.VersionNumber,
            Description = version.Description,
            TestInput = version.TestInput,
            ExpectedOutput = version.ExpectedOutput,
            TestScenario = version.TestScenario,
            ActualOutput = version.ActualOutput,
            Rating = version.Rating,
            TestNotes = version.TestNotes,
            TestStatus = version.TestStatus,
            TestTime = version.TestTime,
            CreatedTime = version.CreatedTime,
            ModifiedTime = version.ModifiedTime
        };
    }
}

public sealed record PromptVersionTestRunResult(
    bool Success,
    string Status,
    string Output,
    string? ErrorMessage,
    TimeSpan Duration)
{
    public static PromptVersionTestRunResult Completed(string output, TimeSpan duration)
    {
        return new PromptVersionTestRunResult(true, "已完成", output, null, duration);
    }

    public static PromptVersionTestRunResult Failed(
        string errorMessage,
        string status = "测试失败",
        TimeSpan duration = default)
    {
        return new PromptVersionTestRunResult(false, status, string.Empty, errorMessage, duration);
    }
}

public sealed record PromptTemplateTestRunResult(
    bool Success,
    string PromptId,
    string PromptName,
    string Output,
    string? ErrorMessage,
    TimeSpan Duration)
{
    public static PromptTemplateTestRunResult Completed(
        string promptId,
        string promptName,
        string output,
        TimeSpan duration)
    {
        return new PromptTemplateTestRunResult(true, promptId, promptName, output, null, duration);
    }

    public static PromptTemplateTestRunResult Failed(
        string errorMessage,
        string promptId = "",
        string promptName = "",
        TimeSpan duration = default)
    {
        return new PromptTemplateTestRunResult(false, promptId, promptName, string.Empty, errorMessage, duration);
    }
}

public sealed record PromptVersionComparisonResult(
    bool Success,
    PromptTemplateTestRunResult Left,
    PromptTemplateTestRunResult Right);
