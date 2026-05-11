using System;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Interfaces;
using TM.Services.Modules.ProjectData.Models.Contexts;
using TM.Services.Modules.ProjectData.Models.Guides;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class ValidationContextAIWorkflow
    {
        private readonly Func<string, CancellationToken, Task<ValidationContext?>> _loadValidationContextAsync;
        private readonly Func<string, CancellationToken, Task<ContextIdCollection?>> _loadContextIdsAsync;
        private readonly ChapterValidationAIWorkflow _workflow;

        public ValidationContextAIWorkflow(
            Func<string, CancellationToken, Task<ValidationContext?>> loadValidationContextAsync,
            Func<string, CancellationToken, Task<ContextIdCollection?>> loadContextIdsAsync,
            Func<string, CancellationToken, Task<ChapterAIValidationResponse>> generateAsync)
        {
            _loadValidationContextAsync = loadValidationContextAsync;
            _loadContextIdsAsync = loadContextIdsAsync;
            _workflow = new ChapterValidationAIWorkflow(LoadPromptSourcesAsync, generateAsync);
        }

        public Task ExecuteAsync(
            ChapterValidationResult result,
            string chapterContent,
            CancellationToken cancellationToken)
        {
            return _workflow.ExecuteAsync(result, chapterContent, cancellationToken);
        }

        private async Task<ChapterValidationPromptSources> LoadPromptSourcesAsync(
            ChapterValidationResult result,
            string chapterContent,
            System.Collections.Generic.IReadOnlyList<ValidationIssue>? knownStructuralIssues,
            CancellationToken cancellationToken)
        {
            var context = await _loadValidationContextAsync(result.ChapterId, cancellationToken)
                .ConfigureAwait(false);
            var contextIds = await _loadContextIdsAsync(result.ChapterId, cancellationToken)
                .ConfigureAwait(false);

            return ValidationContextPromptSourceMapper.Map(context, contextIds);
        }
    }
}
