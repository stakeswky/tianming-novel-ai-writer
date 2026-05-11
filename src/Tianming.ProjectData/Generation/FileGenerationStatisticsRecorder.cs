using System;
using System.IO;
using System.Text.Json;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class FileGenerationStatisticsRecorder : GenerationStatisticsRecorder
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        private readonly string _statisticsFilePath;

        public FileGenerationStatisticsRecorder(string statisticsFilePath)
            : base(LoadStatistics(statisticsFilePath))
        {
            if (string.IsNullOrWhiteSpace(statisticsFilePath))
                throw new ArgumentException("生成统计文件路径不能为空", nameof(statisticsFilePath));

            _statisticsFilePath = statisticsFilePath;
        }

        public override void RecordGeneration(GenerationResult result)
        {
            base.RecordGeneration(result);
            SaveStatistics();
        }

        public override void RecordConsistencyIssue(string issueType)
        {
            base.RecordConsistencyIssue(issueType);
            SaveStatistics();
        }

        public override void ResetStatistics()
        {
            base.ResetStatistics();
            SaveStatistics();
        }

        private void SaveStatistics()
        {
            var directory = Path.GetDirectoryName(_statisticsFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var tmp = _statisticsFilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(GetStatistics(), JsonOptions));
            File.Move(tmp, _statisticsFilePath, overwrite: true);
        }

        private static GenerationStatistics LoadStatistics(string statisticsFilePath)
        {
            if (string.IsNullOrWhiteSpace(statisticsFilePath) || !File.Exists(statisticsFilePath))
                return new GenerationStatistics();

            try
            {
                var json = File.ReadAllText(statisticsFilePath);
                return JsonSerializer.Deserialize<GenerationStatistics>(json, JsonOptions)
                       ?? new GenerationStatistics();
            }
            catch (JsonException)
            {
                return new GenerationStatistics();
            }
            catch (IOException)
            {
                return new GenerationStatistics();
            }
        }
    }
}
