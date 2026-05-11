using System;
using System.Collections.Generic;
using System.Text;
using TM.Framework.Common.Helpers;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Helpers;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Models;

namespace TM.Services.Framework.AI.SemanticKernel.Conversation.Parsing
{
    public static class PlanStepNormalizer
    {
        public static IReadOnlyList<PlanStep> Normalize(
            string userInput,
            string rawContent,
            IReadOnlyList<PlanStep> parsedSteps)
        {
            if (parsedSteps == null || parsedSteps.Count == 0)
                return parsedSteps ?? Array.Empty<PlanStep>();

            if (SingleChapterTaskDetector.IsSingleChapterTask(userInput))
                return MergeToSingleStep(parsedSteps);

            var (startChapter, endChapter) = ExtractChapterRange(userInput);
            if (startChapter <= 0 || endChapter <= startChapter)
                (startChapter, endChapter) = ExtractChapterRange(rawContent);

            if (startChapter > 0 && endChapter > startChapter)
                return SplitByChapterRange(startChapter, endChapter, parsedSteps);

            var distinctChapters = ExtractDistinctChapters(parsedSteps);
            if (distinctChapters.Count >= 2)
                return parsedSteps;

            return MergeToSingleStep(parsedSteps);
        }

        public static (int start, int end) ExtractChapterRange(string content)
        {
            var range = ChapterParserHelper.ParseChapterRange(content);
            return range ?? (0, 0);
        }

        private static IReadOnlyList<PlanStep> MergeToSingleStep(IReadOnlyList<PlanStep> steps)
        {
            if (steps.Count <= 1)
                return steps;

            var chapterTitle = "生成章节";

            foreach (var step in steps)
            {
                if (!ChapterParserHelper.IsChapterTitle(step.Title))
                    continue;

                var (number, name) = ChapterParserHelper.ExtractChapterParts(step.Title);
                if (number.HasValue)
                    chapterTitle = string.IsNullOrEmpty(name) ? $"第{number.Value}章" : $"第{number.Value}章：{name}";
                else
                    chapterTitle = step.Title.Trim();

                break;
            }

            var detail = new StringBuilder();
            detail.AppendLine("AI 原始计划：");
            foreach (var step in steps)
            {
                detail.AppendLine($"{step.Index}. {step.Title}");
                if (!string.IsNullOrWhiteSpace(step.Detail))
                    detail.AppendLine(step.Detail);
                detail.AppendLine();
            }

            return new List<PlanStep>
            {
                new()
                {
                    Index = 1,
                    Title = chapterTitle,
                    Detail = detail.ToString().Trim()
                }
            };
        }

        private static IReadOnlyList<PlanStep> SplitByChapterRange(
            int startChapter,
            int endChapter,
            IReadOnlyList<PlanStep> originalSteps)
        {
            var chapterCount = endChapter - startChapter + 1;
            var newSteps = new List<PlanStep>();

            var originalContent = new StringBuilder();
            foreach (var step in originalSteps)
            {
                originalContent.AppendLine($"{step.Index}. {step.Title}");
                if (!string.IsNullOrWhiteSpace(step.Detail))
                    originalContent.AppendLine(step.Detail);
            }
            var sharedDetail = originalContent.ToString().Trim();

            for (var i = 0; i < chapterCount; i++)
            {
                var chapterNumber = startChapter + i;
                newSteps.Add(new PlanStep
                {
                    Index = i + 1,
                    Title = $"第{chapterNumber}章",
                    ChapterNumber = chapterNumber,
                    Detail = i == 0
                        ? $"创作计划概要：\n{sharedDetail}"
                        : $"根据前文和大纲，生成第{chapterNumber}章内容。"
                });
            }

            return newSteps;
        }

        private static HashSet<string> ExtractDistinctChapters(IReadOnlyList<PlanStep> steps)
        {
            var distinctChapters = new HashSet<string>();

            foreach (var step in steps)
            {
                if (!ChapterParserHelper.IsChapterTitle(step.Title))
                    continue;

                var (number, _) = ChapterParserHelper.ExtractChapterParts(step.Title);
                if (number.HasValue)
                    distinctChapters.Add($"第{number.Value}章");
            }

            return distinctChapters;
        }
    }
}
