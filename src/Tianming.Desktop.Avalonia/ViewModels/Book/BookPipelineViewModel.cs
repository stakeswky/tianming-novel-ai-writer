using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TM.Services.Modules.ProjectData.BookPipeline;
using Tianming.Desktop.Avalonia.Infrastructure;

namespace Tianming.Desktop.Avalonia.ViewModels.Book;

public sealed partial class BookPipelineViewModel : ObservableObject
{
    private static readonly string[] OrderedStepNames =
    {
        BookPipelineStepName.Design,
        BookPipelineStepName.Outline,
        BookPipelineStepName.Volume,
        BookPipelineStepName.ChapterPlanning,
        BookPipelineStepName.Blueprint,
        BookPipelineStepName.Generate,
        BookPipelineStepName.Humanize,
        BookPipelineStepName.Gate,
        BookPipelineStepName.Save,
        BookPipelineStepName.Index,
    };

    private readonly BookGenerationOrchestrator _orchestrator;
    private readonly IBookGenerationJournal _journal;
    private readonly ICurrentProjectService _currentProjectService;
    private CancellationTokenSource? _cts;

    public BookPipelineViewModel(
        BookGenerationOrchestrator orchestrator,
        IBookGenerationJournal journal,
        ICurrentProjectService currentProjectService)
    {
        _orchestrator = orchestrator;
        _journal = journal;
        _currentProjectService = currentProjectService;

        foreach (var name in OrderedStepNames)
            Steps.Add(new BookStepVm { Name = name });
    }

    public string PageTitle => "一键成书";

    public ObservableCollection<BookStepVm> Steps { get; } = new();

    [ObservableProperty]
    private bool isRunning;

    [ObservableProperty]
    private string? statusMessage;

    public async Task LoadAsync()
    {
        await RefreshStatusesAsync().ConfigureAwait(false);
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        IsRunning = true;
        StatusMessage = "运行中…";
        _cts = new CancellationTokenSource();

        var nextPending = Steps.FirstOrDefault(step => step.Status is "Pending" or "Failed");
        if (nextPending != null)
            nextPending.Status = "Running";

        try
        {
            var result = await _orchestrator.RunAsync(new BookPipelineContext
            {
                ProjectRoot = _currentProjectService.ProjectRoot,
            }, _cts.Token).ConfigureAwait(false);

            StatusMessage = result.Success
                ? "完成"
                : $"在 {result.FailedStepName} 失败：{result.ErrorMessage}";

            await RefreshStatusesAsync(result.Success ? null : result.FailedStepName).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "已暂停";
            await RefreshStatusesAsync().ConfigureAwait(false);
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            IsRunning = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanPause))]
    private void Pause()
    {
        _cts?.Cancel();
    }

    [RelayCommand]
    private async Task SkipStepAsync(string? stepName)
    {
        if (string.IsNullOrWhiteSpace(stepName))
            return;

        await _journal.MarkSkippedAsync(stepName).ConfigureAwait(false);
        await RefreshStatusesAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task ResetStepAsync(string? stepName)
    {
        if (string.IsNullOrWhiteSpace(stepName))
            return;

        await _journal.ResetAsync(stepName).ConfigureAwait(false);
        await RefreshStatusesAsync().ConfigureAwait(false);
    }

    partial void OnIsRunningChanged(bool value)
    {
        StartCommand.NotifyCanExecuteChanged();
        PauseCommand.NotifyCanExecuteChanged();
    }

    private bool CanStart() => !IsRunning;

    private bool CanPause() => IsRunning;

    private async Task RefreshStatusesAsync(string? failedStepName = null)
    {
        foreach (var step in Steps)
        {
            if (!string.IsNullOrWhiteSpace(failedStepName) && step.Name == failedStepName)
            {
                step.Status = "Failed";
                continue;
            }

            if (await _journal.IsCompletedAsync(step.Name).ConfigureAwait(false))
            {
                step.Status = "Completed";
                continue;
            }

            if (await _journal.IsSkippedAsync(step.Name).ConfigureAwait(false))
            {
                step.Status = "Skipped";
                continue;
            }

            step.Status = "Pending";
        }
    }
}

public sealed partial class BookStepVm : ObservableObject
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string status = "Pending";
}
