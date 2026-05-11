using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Validation;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class FileValidationReportStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        private readonly string _reportsDirectory;

        public FileValidationReportStore(string reportsDirectory)
        {
            if (string.IsNullOrWhiteSpace(reportsDirectory))
                throw new ArgumentException("校验报告目录不能为空", nameof(reportsDirectory));

            _reportsDirectory = reportsDirectory;
            Directory.CreateDirectory(_reportsDirectory);
        }

        public async Task SaveReportAsync(ValidationReport report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));
            if (string.IsNullOrWhiteSpace(report.ChapterId))
                throw new ArgumentException("章节ID不能为空", nameof(report));

            if (string.IsNullOrWhiteSpace(report.Id))
                report.Id = Guid.NewGuid().ToString("N");

            var chapterDir = Path.Combine(_reportsDirectory, report.ChapterId);
            Directory.CreateDirectory(chapterDir);

            var fileName = $"{report.ValidatedTime:yyyyMMdd_HHmmss}_{report.Id}.json";
            var filePath = Path.Combine(chapterDir, fileName);
            var tmp = filePath + ".tmp";
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(report, JsonOptions)).ConfigureAwait(false);
            File.Move(tmp, filePath, overwrite: true);
        }

        public async Task<List<ValidationReport>> GetReportsAsync(string chapterId)
        {
            if (string.IsNullOrWhiteSpace(chapterId))
                return new List<ValidationReport>();

            var chapterDir = Path.Combine(_reportsDirectory, chapterId);
            if (!Directory.Exists(chapterDir))
                return new List<ValidationReport>();

            var reports = new List<ValidationReport>();
            foreach (var file in Directory.GetFiles(chapterDir, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file).ConfigureAwait(false);
                    var report = JsonSerializer.Deserialize<ValidationReport>(json, JsonOptions);
                    if (report != null)
                        reports.Add(report);
                }
                catch (JsonException)
                {
                }
                catch (IOException)
                {
                }
            }

            return reports
                .OrderByDescending(report => report.ValidatedTime)
                .ThenByDescending(report => report.Id, StringComparer.Ordinal)
                .ToList();
        }

        public async Task<ValidationReport?> GetLatestReportAsync(string chapterId)
        {
            return (await GetReportsAsync(chapterId).ConfigureAwait(false)).FirstOrDefault();
        }

        public Task DeleteReportAsync(string reportId)
        {
            if (string.IsNullOrWhiteSpace(reportId) || !Directory.Exists(_reportsDirectory))
                return Task.CompletedTask;

            foreach (var file in Directory.GetFiles(_reportsDirectory, $"*_{reportId}.json", SearchOption.AllDirectories))
                File.Delete(file);

            return Task.CompletedTask;
        }

        public async Task<Dictionary<string, ValidationResult>> GetAllChapterStatusAsync()
        {
            var status = new Dictionary<string, ValidationResult>(StringComparer.Ordinal);
            if (!Directory.Exists(_reportsDirectory))
                return status;

            foreach (var chapterDir in Directory.GetDirectories(_reportsDirectory))
            {
                var chapterId = Path.GetFileName(chapterDir);
                var latest = await GetLatestReportAsync(chapterId).ConfigureAwait(false);
                if (latest != null)
                    status[chapterId] = latest.Result;
            }

            return status;
        }
    }
}
