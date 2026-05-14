using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.BookPipeline;
using TM.Services.Modules.ProjectData.BookPipeline.Steps;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.ViewModels.Book;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Book;

public class BookPipelineViewModelTests
{
    [Fact]
    public async Task Load_and_start_expose_ten_steps_and_mark_them_completed()
    {
        var vm = CreateViewModel();

        await vm.LoadAsync();
        await vm.StartCommand.ExecuteAsync(null);

        Assert.Equal(10, vm.Steps.Count);
        Assert.Equal(BookPipelineStepName.Design, vm.Steps[0].Name);
        Assert.Equal(BookPipelineStepName.Index, vm.Steps[^1].Name);
        Assert.All(vm.Steps, step => Assert.Equal("Completed", step.Status));
        Assert.Equal("完成", vm.StatusMessage);
    }

    [Fact]
    public async Task Skip_and_reset_update_step_statuses()
    {
        var vm = CreateViewModel();

        await vm.LoadAsync();
        await vm.SkipStepCommand.ExecuteAsync(BookPipelineStepName.Humanize);
        Assert.Equal("Skipped", vm.Steps.Single(step => step.Name == BookPipelineStepName.Humanize).Status);

        await vm.ResetStepCommand.ExecuteAsync(BookPipelineStepName.Humanize);
        Assert.Equal("Pending", vm.Steps.Single(step => step.Name == BookPipelineStepName.Humanize).Status);
    }

    private static BookPipelineViewModel CreateViewModel()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), $"tm-book-pipeline-{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectRoot);
        var journal = new FileBookGenerationJournal(projectRoot);
        var orchestrator = new BookGenerationOrchestrator(
            new IBookPipelineStep[]
            {
                new DesignStep(),
                new OutlineStep(),
                new VolumeStep(),
                new ChapterPlanningStep(),
                new BlueprintStep(),
                new GenerateStep(),
                new HumanizeStep(),
                new GateStep(),
                new SaveStep(),
                new IndexStep(),
            },
            journal);

        return new BookPipelineViewModel(orchestrator, journal, new StubCurrentProjectService(projectRoot));
    }

    private sealed class StubCurrentProjectService(string projectRoot) : ICurrentProjectService
    {
        public string ProjectRoot { get; } = projectRoot;
    }
}
