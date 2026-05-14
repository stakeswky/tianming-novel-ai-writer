using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.BookPipeline;
using Xunit;

namespace Tianming.ProjectData.Tests.BookPipeline;

public class BookGenerationOrchestratorTests
{
    [Fact]
    public async Task Runs_all_uncompleted_steps()
    {
        var steps = new[] { new FakeStep("S1"), new FakeStep("S2"), new FakeStep("S3") };
        var journal = new InMemoryJournal();
        var orchestrator = new BookGenerationOrchestrator(steps, journal);

        var result = await orchestrator.RunAsync(new BookPipelineContext());

        Assert.True(result.Success);
        Assert.All(steps, step => Assert.True(step.Called));
    }

    [Fact]
    public async Task Skips_completed_steps()
    {
        var steps = new[] { new FakeStep("S1"), new FakeStep("S2") };
        var journal = new InMemoryJournal();
        journal.Completed.Add("S1");
        var orchestrator = new BookGenerationOrchestrator(steps, journal);

        await orchestrator.RunAsync(new BookPipelineContext());

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
        var orchestrator = new BookGenerationOrchestrator(steps, journal);

        var result = await orchestrator.RunAsync(new BookPipelineContext());

        Assert.False(result.Success);
        Assert.Equal("S2", result.FailedStepName);
    }

    private sealed class FakeStep : IBookPipelineStep
    {
        public FakeStep(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public bool Called { get; private set; }

        public Task<BookStepResult> ExecuteAsync(BookPipelineContext context, CancellationToken ct = default)
        {
            Called = true;
            return Task.FromResult(new BookStepResult { Success = true });
        }
    }

    private sealed class FailingStep : IBookPipelineStep
    {
        public FailingStep(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public Task<BookStepResult> ExecuteAsync(BookPipelineContext context, CancellationToken ct = default)
            => Task.FromResult(new BookStepResult { Success = false, ErrorMessage = "boom" });
    }

    private sealed class InMemoryJournal : IBookGenerationJournal
    {
        public HashSet<string> Completed { get; } = new();

        public HashSet<string> Skipped { get; } = new();

        public Task<bool> IsCompletedAsync(string stepName, CancellationToken ct = default)
            => Task.FromResult(Completed.Contains(stepName));

        public Task<bool> IsSkippedAsync(string stepName, CancellationToken ct = default)
            => Task.FromResult(Skipped.Contains(stepName));

        public Task RecordCompletedAsync(string stepName, CancellationToken ct = default)
        {
            Completed.Add(stepName);
            return Task.CompletedTask;
        }

        public Task MarkSkippedAsync(string stepName, CancellationToken ct = default)
        {
            Skipped.Add(stepName);
            return Task.CompletedTask;
        }

        public Task ResetAsync(string stepName, CancellationToken ct = default)
        {
            Completed.Remove(stepName);
            Skipped.Remove(stepName);
            return Task.CompletedTask;
        }
    }
}
