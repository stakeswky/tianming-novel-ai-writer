using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Interfaces;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class ChapterValidationPromptSources
    {
        public ChapterValidationDesignContextSource? DesignContextSource { get; init; }
        public ChapterValidationPromptContextSource? PromptContextSource { get; init; }
    }

    public sealed class ChapterValidationAIWorkflow
    {
        private readonly Func<ChapterValidationResult, string, IReadOnlyList<ValidationIssue>?, CancellationToken, Task<ChapterValidationPromptSources>> _loadPromptSourcesAsync;
        private readonly ChapterValidationPromptInputBuilder _promptInputBuilder;
        private readonly ChapterAIValidationExecutor _executor;

        public ChapterValidationAIWorkflow(
            Func<ChapterValidationResult, string, IReadOnlyList<ValidationIssue>?, CancellationToken, Task<ChapterValidationPromptSources>> loadPromptSourcesAsync,
            Func<string, CancellationToken, Task<ChapterAIValidationResponse>> generateAsync)
            : this(loadPromptSourcesAsync, generateAsync, new ChapterValidationPromptInputBuilder())
        {
        }

        public ChapterValidationAIWorkflow(
            Func<ChapterValidationResult, string, IReadOnlyList<ValidationIssue>?, CancellationToken, Task<ChapterValidationPromptSources>> loadPromptSourcesAsync,
            Func<string, CancellationToken, Task<ChapterAIValidationResponse>> generateAsync,
            ChapterValidationPromptInputBuilder promptInputBuilder)
        {
            _loadPromptSourcesAsync = loadPromptSourcesAsync;
            _promptInputBuilder = promptInputBuilder;
            _executor = new ChapterAIValidationExecutor(BuildPromptAsync, generateAsync);
        }

        public Task ExecuteAsync(
            ChapterValidationResult result,
            string chapterContent,
            CancellationToken cancellationToken)
        {
            return _executor.ExecuteAsync(result, chapterContent, cancellationToken);
        }

        private async Task<string> BuildPromptAsync(
            ChapterValidationResult result,
            string chapterContent,
            IReadOnlyList<ValidationIssue>? knownStructuralIssues,
            CancellationToken cancellationToken)
        {
            var sources = await _loadPromptSourcesAsync(result, chapterContent, knownStructuralIssues, cancellationToken)
                .ConfigureAwait(false);

            return _promptInputBuilder.BuildPrompt(new ChapterValidationPromptInputBuildRequest
            {
                ChapterId = result.ChapterId,
                ChapterTitle = result.ChapterTitle,
                VolumeNumber = result.VolumeNumber,
                ChapterNumber = result.ChapterNumber,
                VolumeName = result.VolumeName,
                ChapterContent = chapterContent,
                DesignContextSource = sources.DesignContextSource,
                PromptContextSource = sources.PromptContextSource,
                KnownStructuralIssues = knownStructuralIssues
            });
        }
    }
}
