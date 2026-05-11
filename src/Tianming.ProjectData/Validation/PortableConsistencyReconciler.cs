using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public interface IChapterSummaryRepairStore
    {
        Task<Dictionary<string, string>> GetAllSummariesAsync(CancellationToken cancellationToken = default);

        Task RemoveSummaryAsync(string chapterId, CancellationToken cancellationToken = default);

        Task SetSummaryAsync(string chapterId, string summary, CancellationToken cancellationToken = default);

        Task<Dictionary<string, string>> GetVolumeSummariesAsync(int volumeNumber, CancellationToken cancellationToken = default);

        Task RebuildVolumeMilestoneAsync(
            int volumeNumber,
            Dictionary<string, string> volumeSummaries,
            CancellationToken cancellationToken = default);
    }

    public interface IChapterKeywordIndexRepairStore
    {
        Task<HashSet<string>> GetIndexedChapterIdsAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyCollection<string>> GetKnownEntityNamesAsync(CancellationToken cancellationToken = default);

        Task IndexChapterFromKeywordsAsync(
            string chapterId,
            IReadOnlyList<string> keywords,
            CancellationToken cancellationToken = default);
    }

    public enum VectorIndexRepairMode
    {
        None,
        Keyword,
        LocalEmbedding
    }

    public interface IChapterVectorIndexRepairStore
    {
        Task<VectorIndexRepairMode> InitializeAsync(CancellationToken cancellationToken = default);
    }

    public interface IChapterTrackingRepairStore
    {
        Task<HashSet<string>> GetTrackedChapterIdsAsync(CancellationToken cancellationToken = default);

        Task RemoveChapterTrackingAsync(string chapterId, CancellationToken cancellationToken = default);
    }

    public interface IVolumeFactArchiveRepairStore
    {
        Task<HashSet<int>> GetArchivedVolumeNumbersAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyDictionary<int, int>> GetConfiguredEndChaptersAsync(CancellationToken cancellationToken = default);

        Task ArchiveCompletedVolumeAsync(
            int volumeNumber,
            string lastChapterId,
            CancellationToken cancellationToken = default);
    }

    public sealed class PortableConsistencyReconcileResult
    {
        public int StagingCleaned { get; set; }
        public int BakCleaned { get; set; }
        public int SummariesRepaired { get; set; }
        public int VectorReindexed { get; set; }
        public List<string> CorruptedGuides { get; set; } = [];
        public List<string> TrackingGaps { get; set; } = [];
        public int TrackingGapSummariesRepaired { get; set; }
        public int KeywordIndexRepaired { get; set; }
        public int FactArchivesRepaired { get; set; }
        public int TrackingOrphansCleared { get; set; }
        public List<string> Errors { get; set; } = [];

        public bool HasRepairs =>
            StagingCleaned > 0 ||
            BakCleaned > 0 ||
            SummariesRepaired > 0 ||
            VectorReindexed > 0 ||
            CorruptedGuides.Count > 0 ||
            TrackingGapSummariesRepaired > 0 ||
            KeywordIndexRepaired > 0 ||
            FactArchivesRepaired > 0 ||
            TrackingOrphansCleared > 0;
    }

    public sealed class PortableConsistencyReconciler
    {
        private readonly string _projectRoot;
        private readonly string _chaptersDirectory;
        private readonly string _configDirectory;
        private readonly string _modulesDirectory;
        private readonly IChapterSummaryRepairStore? _summaryRepairStore;
        private readonly IChapterKeywordIndexRepairStore? _keywordIndexRepairStore;
        private readonly IChapterVectorIndexRepairStore? _vectorIndexRepairStore;
        private readonly IChapterTrackingRepairStore? _trackingRepairStore;
        private readonly IVolumeFactArchiveRepairStore? _factArchiveRepairStore;

        public PortableConsistencyReconciler(string projectRoot)
            : this(
                projectRoot,
                Path.Combine(projectRoot, "Generated"),
                Path.Combine(projectRoot, "Config"),
                Path.Combine(projectRoot, "Modules"),
                summaryRepairStore: null,
                keywordIndexRepairStore: null,
                vectorIndexRepairStore: null,
                trackingRepairStore: null,
                factArchiveRepairStore: null)
        {
        }

        public PortableConsistencyReconciler(
            string projectRoot,
            IChapterVectorIndexRepairStore? vectorIndexRepairStore)
            : this(
                projectRoot,
                summaryRepairStore: null,
                keywordIndexRepairStore: null,
                vectorIndexRepairStore,
                trackingRepairStore: null,
                factArchiveRepairStore: null)
        {
        }

        public PortableConsistencyReconciler(
            string projectRoot,
            IChapterSummaryRepairStore? summaryRepairStore)
            : this(
                projectRoot,
                summaryRepairStore,
                keywordIndexRepairStore: null,
                vectorIndexRepairStore: null,
                trackingRepairStore: null,
                factArchiveRepairStore: null)
        {
        }

        public PortableConsistencyReconciler(
            string projectRoot,
            IChapterSummaryRepairStore? summaryRepairStore,
            IChapterKeywordIndexRepairStore? keywordIndexRepairStore = null,
            IChapterVectorIndexRepairStore? vectorIndexRepairStore = null,
            IChapterTrackingRepairStore? trackingRepairStore = null,
            IVolumeFactArchiveRepairStore? factArchiveRepairStore = null)
            : this(
                projectRoot,
                Path.Combine(projectRoot, "Generated"),
                Path.Combine(projectRoot, "Config"),
                Path.Combine(projectRoot, "Modules"),
                summaryRepairStore,
                keywordIndexRepairStore,
                vectorIndexRepairStore,
                trackingRepairStore,
                factArchiveRepairStore)
        {
        }

        public PortableConsistencyReconciler(
            string projectRoot,
            string chaptersDirectory,
            string configDirectory,
            string modulesDirectory,
            IChapterSummaryRepairStore? summaryRepairStore = null,
            IChapterKeywordIndexRepairStore? keywordIndexRepairStore = null,
            IChapterVectorIndexRepairStore? vectorIndexRepairStore = null,
            IChapterTrackingRepairStore? trackingRepairStore = null,
            IVolumeFactArchiveRepairStore? factArchiveRepairStore = null)
        {
            if (string.IsNullOrWhiteSpace(projectRoot))
                throw new ArgumentException("项目目录不能为空", nameof(projectRoot));
            if (string.IsNullOrWhiteSpace(chaptersDirectory))
                throw new ArgumentException("章节目录不能为空", nameof(chaptersDirectory));
            if (string.IsNullOrWhiteSpace(configDirectory))
                throw new ArgumentException("配置目录不能为空", nameof(configDirectory));
            if (string.IsNullOrWhiteSpace(modulesDirectory))
                throw new ArgumentException("模块目录不能为空", nameof(modulesDirectory));

            _projectRoot = projectRoot;
            _chaptersDirectory = chaptersDirectory;
            _configDirectory = configDirectory;
            _modulesDirectory = modulesDirectory;
            _summaryRepairStore = summaryRepairStore;
            _keywordIndexRepairStore = keywordIndexRepairStore;
            _vectorIndexRepairStore = vectorIndexRepairStore;
            _trackingRepairStore = trackingRepairStore;
            _factArchiveRepairStore = factArchiveRepairStore;
        }

        public async Task<PortableConsistencyReconcileResult> ReconcileAsync(CancellationToken cancellationToken = default)
        {
            var result = new PortableConsistencyReconcileResult();
            CleanStagingAndBackups(result);
            cancellationToken.ThrowIfCancellationRequested();
            await ValidateGuidesIntegrityAsync(result, cancellationToken).ConfigureAwait(false);
            await DetectTrackingGapsAsync(result, cancellationToken).ConfigureAwait(false);
            await ReconcileSummariesAsync(result, cancellationToken).ConfigureAwait(false);
            await ReconcileVectorIndexAsync(result, cancellationToken).ConfigureAwait(false);
            await ReconcileKeywordIndexAsync(result, cancellationToken).ConfigureAwait(false);
            await ReconcileFactArchivesAsync(result, cancellationToken).ConfigureAwait(false);
            CleanGuideBackups();
            return result;
        }

        private void CleanStagingAndBackups(PortableConsistencyReconcileResult result)
        {
            if (Directory.Exists(_chaptersDirectory))
            {
                CleanChapterStaging(result);
                CleanChapterBackups(result);
            }

            DeleteTemporaryFiles(_configDirectory, result);
            DeleteFileIfExists(Path.Combine(_projectRoot, "manifest.json.tmp"), result);
            DeleteTemporaryFiles(_modulesDirectory, result);
            DeleteOrphanBackupDirectories(result);
        }

        private void CleanChapterStaging(PortableConsistencyReconcileResult result)
        {
            var stagingDirectory = Path.Combine(_chaptersDirectory, ".staging");
            if (!Directory.Exists(stagingDirectory))
                return;

            foreach (var file in Directory.GetFiles(stagingDirectory, "*.md"))
            {
                try
                {
                    var chapterId = Path.GetFileNameWithoutExtension(file);
                    var finalFile = Path.Combine(_chaptersDirectory, $"{chapterId}.md");
                    if (!File.Exists(finalFile) ||
                        File.GetLastWriteTimeUtc(file) > File.GetLastWriteTimeUtc(finalFile))
                    {
                        File.Move(file, finalFile, overwrite: true);
                    }
                    else
                    {
                        File.Delete(file);
                    }

                    result.StagingCleaned++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"清理staging失败 {file}: {ex.Message}");
                }
            }

            try
            {
                if (!Directory.EnumerateFileSystemEntries(stagingDirectory).Any())
                    Directory.Delete(stagingDirectory);
            }
            catch
            {
            }
        }

        private void CleanChapterBackups(PortableConsistencyReconcileResult result)
        {
            foreach (var file in Directory.GetFiles(_chaptersDirectory, "*.bak"))
            {
                try
                {
                    var finalFile = Path.Combine(_chaptersDirectory, Path.GetFileNameWithoutExtension(file));
                    if (!File.Exists(finalFile))
                    {
                        File.Move(file, finalFile);
                    }
                    else
                    {
                        File.Delete(file);
                    }

                    result.BakCleaned++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"清理bak失败 {file}: {ex.Message}");
                }
            }
        }

        private static void DeleteTemporaryFiles(string directory, PortableConsistencyReconcileResult result)
        {
            if (!Directory.Exists(directory))
                return;

            foreach (var file in Directory.GetFiles(directory, "*.tmp", SearchOption.AllDirectories))
                DeleteFileIfExists(file, result);
        }

        private static void DeleteFileIfExists(string path, PortableConsistencyReconcileResult result)
        {
            if (!File.Exists(path))
                return;

            try
            {
                File.Delete(path);
                result.StagingCleaned++;
            }
            catch
            {
            }
        }

        private void CleanGuideBackups()
        {
            var guidesDirectory = Path.Combine(_configDirectory, "guides");
            if (!Directory.Exists(guidesDirectory))
                return;

            foreach (var backupFile in Directory.GetFiles(guidesDirectory, "*.bak"))
            {
                try
                {
                    var originalFile = backupFile[..^4];
                    if (!File.Exists(originalFile))
                    {
                        File.Move(backupFile, originalFile);
                    }
                    else
                    {
                        File.Delete(backupFile);
                    }
                }
                catch
                {
                }
            }
        }

        private void DeleteOrphanBackupDirectories(PortableConsistencyReconcileResult result)
        {
            if (!Directory.Exists(_projectRoot))
                return;

            foreach (var directory in Directory.GetDirectories(_projectRoot, "_backup_*"))
            {
                try
                {
                    Directory.Delete(directory, recursive: true);
                    result.StagingCleaned++;
                }
                catch
                {
                }
            }
        }

        private async Task ValidateGuidesIntegrityAsync(
            PortableConsistencyReconcileResult result,
            CancellationToken cancellationToken)
        {
            var guidesDirectory = Path.Combine(_configDirectory, "guides");
            if (!Directory.Exists(guidesDirectory))
                return;

            foreach (var file in Directory.GetFiles(guidesDirectory, "*.json", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var json = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                    using var document = JsonDocument.Parse(json);
                }
                catch
                {
                    await TryRestoreGuideAsync(file, result, cancellationToken).ConfigureAwait(false);
                }
            }

            foreach (var file in Directory.GetFiles(guidesDirectory, "*.txt", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    _ = await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    result.CorruptedGuides.Add(Path.GetFileName(file));
                    result.Errors.Add($"里程碑文件读取失败 {Path.GetFileName(file)}: {ex.Message}");
                }
            }
        }

        private async Task ReconcileSummariesAsync(
            PortableConsistencyReconcileResult result,
            CancellationToken cancellationToken)
        {
            if (_summaryRepairStore == null || !Directory.Exists(_chaptersDirectory))
                return;

            var chapterFiles = Directory.GetFiles(_chaptersDirectory, "*.md", SearchOption.TopDirectoryOnly);
            if (chapterFiles.Length == 0)
                return;

            Dictionary<string, string> existingSummaries;
            try
            {
                existingSummaries = await _summaryRepairStore.GetAllSummariesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                existingSummaries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var chapterIds = new HashSet<string>(
                chapterFiles.Select(Path.GetFileNameWithoutExtension).Where(id => !string.IsNullOrWhiteSpace(id))!,
                StringComparer.OrdinalIgnoreCase);

            var orphanAffectedVolumes = new HashSet<int>();
            foreach (var orphanId in existingSummaries.Keys.Where(id => !chapterIds.Contains(id)).ToList())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await _summaryRepairStore.RemoveSummaryAsync(orphanId, cancellationToken).ConfigureAwait(false);
                    result.SummariesRepaired++;
                    orphanAffectedVolumes.Add(ParseVolumeNumber(orphanId));
                }
                catch
                {
                }
            }

            foreach (var volume in orphanAffectedVolumes)
                await RebuildMilestoneAsync(volume, result, cancellationToken).ConfigureAwait(false);

            var repairedVolumes = new HashSet<int>();
            foreach (var chapterFile in chapterFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var chapterId = Path.GetFileNameWithoutExtension(chapterFile);
                if (string.IsNullOrWhiteSpace(chapterId) || existingSummaries.ContainsKey(chapterId))
                    continue;

                try
                {
                    var content = await ReadHeadAsync(chapterFile, 2000, cancellationToken).ConfigureAwait(false);
                    var summary = ExtractSummaryFromHead(content);
                    if (string.IsNullOrWhiteSpace(summary))
                        continue;

                    await _summaryRepairStore.SetSummaryAsync(chapterId, summary, cancellationToken).ConfigureAwait(false);
                    result.SummariesRepaired++;
                    repairedVolumes.Add(ParseVolumeNumber(chapterId));
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"补建摘要失败 {chapterId}: {ex.Message}");
                }
            }

            foreach (var volume in repairedVolumes)
                await RebuildMilestoneAsync(volume, result, cancellationToken).ConfigureAwait(false);
        }

        private async Task RebuildMilestoneAsync(
            int volume,
            PortableConsistencyReconcileResult result,
            CancellationToken cancellationToken)
        {
            try
            {
                var summaries = await _summaryRepairStore!.GetVolumeSummariesAsync(volume, cancellationToken).ConfigureAwait(false);
                await _summaryRepairStore.RebuildVolumeMilestoneAsync(volume, summaries, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"重建里程碑失败 vol{volume}: {ex.Message}");
            }
        }

        private async Task ReconcileKeywordIndexAsync(
            PortableConsistencyReconcileResult result,
            CancellationToken cancellationToken)
        {
            if (_summaryRepairStore == null || _keywordIndexRepairStore == null || !Directory.Exists(_chaptersDirectory))
                return;

            var chapterFiles = Directory.GetFiles(_chaptersDirectory, "*.md", SearchOption.TopDirectoryOnly);
            if (chapterFiles.Length == 0)
                return;

            try
            {
                var indexedChapterIds = await _keywordIndexRepairStore.GetIndexedChapterIdsAsync(cancellationToken).ConfigureAwait(false);
                var missingChapterIds = chapterFiles
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(id => !string.IsNullOrWhiteSpace(id) && !indexedChapterIds.Contains(id))
                    .Cast<string>()
                    .ToList();
                if (missingChapterIds.Count == 0)
                    return;

                var summaries = await _summaryRepairStore.GetAllSummariesAsync(cancellationToken).ConfigureAwait(false);
                var knownNames = (await _keywordIndexRepairStore.GetKnownEntityNamesAsync(cancellationToken).ConfigureAwait(false))
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (knownNames.Count == 0)
                    return;

                foreach (var chapterId in missingChapterIds)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!summaries.TryGetValue(chapterId, out var summary) || string.IsNullOrWhiteSpace(summary))
                        continue;

                    var matched = knownNames
                        .Where(name => summary.Contains(name, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (matched.Count == 0)
                        continue;

                    await _keywordIndexRepairStore.IndexChapterFromKeywordsAsync(chapterId, matched, cancellationToken).ConfigureAwait(false);
                    result.KeywordIndexRepaired++;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"关键词索引对账失败: {ex.Message}");
            }
        }

        private async Task DetectTrackingGapsAsync(
            PortableConsistencyReconcileResult result,
            CancellationToken cancellationToken)
        {
            if (_trackingRepairStore == null || !Directory.Exists(_chaptersDirectory))
                return;

            var chapterIds = Directory.GetFiles(_chaptersDirectory, "*.md", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Cast<string>()
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (chapterIds.Count <= 1)
                return;

            try
            {
                var trackedChapterIds = await _trackingRepairStore
                    .GetTrackedChapterIdsAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (trackedChapterIds.Count == 0)
                    return;

                foreach (var chapterId in chapterIds.OrderBy(id => ParseVolumeNumber(id)).ThenBy(ParseChapterNumber))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!trackedChapterIds.Contains(chapterId))
                        result.TrackingGaps.Add(chapterId);
                }

                foreach (var orphanId in trackedChapterIds.Where(id => !chapterIds.Contains(id)).ToList())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        await _trackingRepairStore.RemoveChapterTrackingAsync(orphanId, cancellationToken).ConfigureAwait(false);
                        result.TrackingOrphansCleared++;
                    }
                    catch
                    {
                    }
                }

                if (result.TrackingGaps.Count == 0 || _summaryRepairStore == null)
                    return;

                Dictionary<string, string> summaries;
                try
                {
                    summaries = await _summaryRepairStore.GetAllSummariesAsync(cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    summaries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }

                foreach (var gapId in result.TrackingGaps)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (summaries.TryGetValue(gapId, out var existing) && !string.IsNullOrWhiteSpace(existing))
                        continue;

                    var chapterPath = Path.Combine(_chaptersDirectory, $"{gapId}.md");
                    if (!File.Exists(chapterPath))
                        continue;

                    try
                    {
                        var head = await ReadHeadAsync(chapterPath, 2000, cancellationToken).ConfigureAwait(false);
                        var summary = ExtractSummaryFromHead(head);
                        if (string.IsNullOrWhiteSpace(summary))
                            continue;

                        await _summaryRepairStore.SetSummaryAsync(gapId, summary, cancellationToken).ConfigureAwait(false);
                        result.TrackingGapSummariesRepaired++;
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"追踪缺章检测失败: {ex.Message}");
            }
        }

        private async Task ReconcileFactArchivesAsync(
            PortableConsistencyReconcileResult result,
            CancellationToken cancellationToken)
        {
            if (_summaryRepairStore == null || _factArchiveRepairStore == null)
                return;

            try
            {
                var summaries = await _summaryRepairStore.GetAllSummariesAsync(cancellationToken).ConfigureAwait(false);
                if (summaries.Count == 0)
                    return;

                var volumeChapters = summaries.Keys
                    .Select(id => new ParsedChapter(id, ParseVolumeNumber(id), ParseChapterNumber(id)))
                    .Where(item => item.Volume > 0 && item.Chapter > 0)
                    .GroupBy(item => item.Volume)
                    .ToDictionary(
                        group => group.Key,
                        group => group.OrderBy(item => item.Chapter).ToList());
                if (volumeChapters.Count == 0)
                    return;

                var archivedVolumes = await _factArchiveRepairStore
                    .GetArchivedVolumeNumbersAsync(cancellationToken)
                    .ConfigureAwait(false);
                var configuredEndChapters = await _factArchiveRepairStore
                    .GetConfiguredEndChaptersAsync(cancellationToken)
                    .ConfigureAwait(false);
                var maxVolume = volumeChapters.Keys.Max();

                foreach (var (volume, chapters) in volumeChapters.OrderBy(pair => pair.Key))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (archivedVolumes.Contains(volume) || !IsVolumeCompleted(volume, chapters, configuredEndChapters, maxVolume))
                        continue;

                    var lastChapterId = chapters.Last().ChapterId;
                    await _factArchiveRepairStore
                        .ArchiveCompletedVolumeAsync(volume, lastChapterId, cancellationToken)
                        .ConfigureAwait(false);
                    result.FactArchivesRepaired++;
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"fact_archive对账失败: {ex.Message}");
            }
        }

        private async Task ReconcileVectorIndexAsync(
            PortableConsistencyReconcileResult result,
            CancellationToken cancellationToken)
        {
            if (_vectorIndexRepairStore == null)
                return;

            var flagPath = Path.Combine(_projectRoot, "vector_degraded.flag");
            var wasDegraded = File.Exists(flagPath);

            try
            {
                var mode = await _vectorIndexRepairStore.InitializeAsync(cancellationToken).ConfigureAwait(false);
                if (wasDegraded && File.Exists(flagPath) && mode != VectorIndexRepairMode.Keyword)
                {
                    try
                    {
                        File.Delete(flagPath);
                        result.VectorReindexed++;
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add($"向量索引对账失败: {ex.Message}");
            }
        }

        private static async Task TryRestoreGuideAsync(
            string file,
            PortableConsistencyReconcileResult result,
            CancellationToken cancellationToken)
        {
            var fileName = Path.GetFileName(file);
            var backupFile = file + ".bak";
            if (File.Exists(backupFile))
            {
                try
                {
                    var backupJson = await File.ReadAllTextAsync(backupFile, cancellationToken).ConfigureAwait(false);
                    using var document = JsonDocument.Parse(backupJson);
                    File.Copy(backupFile, file, overwrite: true);
                    return;
                }
                catch
                {
                }
            }

            result.CorruptedGuides.Add(fileName);
            if (fileName.StartsWith("content_guide", StringComparison.OrdinalIgnoreCase))
            {
                var message = $"content_guide 分片 [{fileName}] 已损坏且无法自动恢复，请重新执行【全量打包】以重建指导文件。";
                if (!result.Errors.Contains(message))
                    result.Errors.Add(message);
            }
        }

        private static async Task<string> ReadHeadAsync(
            string filePath,
            int maxChars,
            CancellationToken cancellationToken)
        {
            var bufferSize = maxChars * 3;
            var buffer = new byte[bufferSize];
            int bytesRead;

            await using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
            }

            if (bytesRead == 0)
                return string.Empty;

            var text = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
            return text.Length > maxChars ? text[..maxChars] : text;
        }

        private static string ExtractSummaryFromHead(string content)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;

            var cleaned = content.Replace("\r\n", " ").Replace("\n", " ").Trim();
            if (cleaned.Length <= 500)
                return cleaned;

            var cutRegion = cleaned[..500];
            var lastSentenceEnd = cutRegion.LastIndexOfAny(['。', '！', '？', '…', '"']);
            if (lastSentenceEnd > 200)
                return cutRegion[..(lastSentenceEnd + 1)] + "……";

            return cutRegion + "……";
        }

        private static int ParseVolumeNumber(string chapterId)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                chapterId ?? string.Empty,
                @"(?:vol|v)(\d+)|^(\d+)_",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success)
                return 1;

            var value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            return int.TryParse(value, out var volume) && volume > 0 ? volume : 1;
        }

        private static int ParseChapterNumber(string chapterId)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                chapterId ?? string.Empty,
                @"(?:ch|chapter)(\d+)|_(\d+)$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success)
                return 0;

            var value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            return int.TryParse(value, out var chapter) && chapter > 0 ? chapter : 0;
        }

        private static bool IsVolumeCompleted(
            int volume,
            IReadOnlyList<ParsedChapter> chapters,
            IReadOnlyDictionary<int, int> configuredEndChapters,
            int maxVolume)
        {
            if (configuredEndChapters.TryGetValue(volume, out var endChapter) && endChapter > 0)
                return chapters.Any(chapter => chapter.Chapter == endChapter);

            var isMaxVolume = volume == maxVolume;
            return !isMaxVolume || chapters.Count >= 7;
        }

        private readonly record struct ParsedChapter(string ChapterId, int Volume, int Chapter);
    }
}
