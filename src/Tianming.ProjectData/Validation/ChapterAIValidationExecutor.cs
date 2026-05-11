using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Interfaces;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class ChapterAIValidationExecutor
    {
        private const string StructuralModuleName = "StructuralConsistency";
        private const string SystemModuleName = "System";

        private readonly Func<ChapterValidationResult, string, IReadOnlyList<ValidationIssue>?, CancellationToken, Task<string>> _buildPromptAsync;
        private readonly Func<string, CancellationToken, Task<ChapterAIValidationResponse>> _generateAsync;

        public ChapterAIValidationExecutor(
            Func<ChapterValidationResult, string, IReadOnlyList<ValidationIssue>?, CancellationToken, Task<string>> buildPromptAsync,
            Func<string, CancellationToken, Task<ChapterAIValidationResponse>> generateAsync)
        {
            _buildPromptAsync = buildPromptAsync;
            _generateAsync = generateAsync;
        }

        public async Task ExecuteAsync(
            ChapterValidationResult result,
            string chapterContent,
            CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                result.IssuesByModule.TryGetValue(StructuralModuleName, out var knownStructuralIssues);
                var prompt = await _buildPromptAsync(result, chapterContent, knownStructuralIssues, cancellationToken);
                var aiResult = await _generateAsync(prompt, cancellationToken);

                if (aiResult.IsSuccess && !string.IsNullOrEmpty(aiResult.Content))
                {
                    ChapterValidationResultParser.ApplyAIContent(result, aiResult.Content);
                }
                else
                {
                    result.IssuesByModule[SystemModuleName] =
                    [
                        new ValidationIssue
                        {
                            Type = "AIValidationFailed",
                            Severity = "Warning",
                            Message = "AI校验失败，未执行校验。"
                        }
                    ];
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result.IssuesByModule[SystemModuleName] =
                [
                    new ValidationIssue
                    {
                        Type = "AIValidationException",
                        Severity = "Warning",
                        Message = $"AI校验异常：{ex.Message}，未执行校验。"
                    }
                ];
            }
        }
    }

    public sealed record ChapterAIValidationResponse(bool IsSuccess, string? Content, string? ErrorMessage)
    {
        public static ChapterAIValidationResponse Success(string content)
        {
            return new ChapterAIValidationResponse(true, content, null);
        }

        public static ChapterAIValidationResponse Failure(string? errorMessage)
        {
            return new ChapterAIValidationResponse(false, null, errorMessage);
        }
    }
}
