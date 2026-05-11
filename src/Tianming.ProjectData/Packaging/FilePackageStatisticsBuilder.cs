using System;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Publishing;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class FilePackageStatisticsBuilder
    {
        private static readonly Regex MarkdownSyntaxRegex = new(@"[#*_`\[\]()]+", RegexOptions.Compiled);

        private readonly string _chaptersDirectory;
        private readonly string _publishedRoot;

        public FilePackageStatisticsBuilder(string chaptersDirectory, string publishedRoot)
        {
            if (string.IsNullOrWhiteSpace(chaptersDirectory))
                throw new ArgumentException("章节目录不能为空", nameof(chaptersDirectory));
            if (string.IsNullOrWhiteSpace(publishedRoot))
                throw new ArgumentException("发布目录不能为空", nameof(publishedRoot));

            _chaptersDirectory = chaptersDirectory;
            _publishedRoot = publishedRoot;
        }

        public async Task<StatisticsInfo> BuildStatisticsAsync()
        {
            return new StatisticsInfo
            {
                TotalChapters = CountChapters(),
                TotalWords = await CountWordsAsync().ConfigureAwait(false),
                TotalCharacters = await CountPublishedArrayItemsAsync("characterrules").ConfigureAwait(false),
                TotalLocations = await CountPublishedArrayItemsAsync("locationrules").ConfigureAwait(false)
            };
        }

        private int CountChapters()
        {
            if (!Directory.Exists(_chaptersDirectory))
                return 0;

            return Directory.GetFiles(_chaptersDirectory, "*.md", SearchOption.TopDirectoryOnly).Length;
        }

        private async Task<long> CountWordsAsync()
        {
            if (!Directory.Exists(_chaptersDirectory))
                return 0;

            long totalWords = 0;
            foreach (var file in Directory.GetFiles(_chaptersDirectory, "*.md", SearchOption.TopDirectoryOnly))
            {
                var content = await File.ReadAllTextAsync(file).ConfigureAwait(false);
                totalWords += CountChineseWords(content);
            }

            return totalWords;
        }

        private static int CountChineseWords(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            var cleaned = MarkdownSyntaxRegex.Replace(text, "");
            var count = 0;
            foreach (var c in cleaned)
            {
                if ((c >= 0x4E00 && c <= 0x9FFF)
                    || (c >= 0x3400 && c <= 0x4DBF)
                    || (c >= 0xF900 && c <= 0xFAFF)
                    || (c >= 0x2E80 && c <= 0x2EFF)
                    || (c >= 0x2F00 && c <= 0x2FDF))
                    count++;
            }

            return count;
        }

        private async Task<int> CountPublishedArrayItemsAsync(string ruleName)
        {
            var elementsFile = Path.Combine(_publishedRoot, "Design", "elements.json");
            if (!File.Exists(elementsFile))
                return 0;

            try
            {
                await using var stream = File.OpenRead(elementsFile);
                using var doc = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
                if (!doc.RootElement.TryGetProperty("data", out var data)
                    || !data.TryGetProperty(ruleName, out var rules)
                    || rules.ValueKind != JsonValueKind.Object)
                    return 0;

                var count = 0;
                foreach (var property in rules.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Array)
                        count += property.Value.GetArrayLength();
                }

                return count;
            }
            catch (JsonException)
            {
                return 0;
            }
            catch (IOException)
            {
                return 0;
            }
        }
    }
}
