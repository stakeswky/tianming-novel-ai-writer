using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TM.Services.Modules.ProjectData.Interfaces;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public static class BatchChapterValidationResultParser
    {
        public static void ApplyAIContent(IList<ChapterValidationResult> results, string aiContent)
        {
            ArgumentNullException.ThrowIfNull(results);

            try
            {
                var arrayStart = aiContent.IndexOf('[');
                var arrayEnd = aiContent.LastIndexOf(']');
                if (arrayStart < 0 || arrayEnd <= arrayStart)
                {
                    AddErrorToAll(results, "批量校验AI返回格式错误");
                    return;
                }

                var json = aiContent.Substring(arrayStart, arrayEnd - arrayStart + 1);
                using var doc = JsonDocument.Parse(json);
                var elements = doc.RootElement.EnumerateArray().ToList();

                for (var i = 0; i < results.Count; i++)
                {
                    if (i >= elements.Count)
                    {
                        ChapterValidationResultParser.AddProtocolErrorIssue(results[i], "批量校验AI返回数组长度不足");
                        continue;
                    }

                    var element = elements[i];
                    if (element.TryGetProperty("moduleResults", out var moduleResultsArray))
                    {
                        ChapterValidationResultParser.ApplyModuleResults(results[i], moduleResultsArray);
                    }
                    else
                    {
                        ChapterValidationResultParser.AddProtocolErrorIssue(results[i], "批量校验结果缺少moduleResults");
                    }
                }
            }
            catch (Exception ex)
            {
                AddErrorToAll(results, $"批量解析失败: {ex.Message}");
            }
        }

        private static void AddErrorToAll(IEnumerable<ChapterValidationResult> results, string message)
        {
            foreach (var result in results)
            {
                ChapterValidationResultParser.AddProtocolErrorIssue(result, message);
            }
        }
    }
}
