using System.Text.Json;
using TM.Services.Modules.ProjectData.Implementations;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class PortableConsistencyReconcilerTests
{
    [Fact]
    public async Task ReconcileAsync_recovers_newer_staging_chapter_and_deletes_stale_staging()
    {
        using var workspace = new TempDirectory();
        var chapters = Path.Combine(workspace.Path, "Generated");
        var staging = Path.Combine(chapters, ".staging");
        Directory.CreateDirectory(staging);
        var finalPath = Path.Combine(chapters, "vol1_ch1.md");
        var stagedPath = Path.Combine(staging, "vol1_ch1.md");
        var staleFinalPath = Path.Combine(chapters, "vol1_ch2.md");
        var staleStagedPath = Path.Combine(staging, "vol1_ch2.md");
        await File.WriteAllTextAsync(finalPath, "旧正文");
        await File.WriteAllTextAsync(stagedPath, "新正文");
        await File.WriteAllTextAsync(staleFinalPath, "保留正文");
        await File.WriteAllTextAsync(staleStagedPath, "过期正文");
        File.SetLastWriteTimeUtc(finalPath, DateTime.UtcNow.AddMinutes(-10));
        File.SetLastWriteTimeUtc(stagedPath, DateTime.UtcNow);
        File.SetLastWriteTimeUtc(staleFinalPath, DateTime.UtcNow);
        File.SetLastWriteTimeUtc(staleStagedPath, DateTime.UtcNow.AddMinutes(-10));

        var result = await new PortableConsistencyReconciler(workspace.Path).ReconcileAsync();

        Assert.Equal(2, result.StagingCleaned);
        Assert.Equal("新正文", await File.ReadAllTextAsync(finalPath));
        Assert.Equal("保留正文", await File.ReadAllTextAsync(staleFinalPath));
        Assert.False(File.Exists(stagedPath));
        Assert.False(File.Exists(staleStagedPath));
        Assert.False(Directory.Exists(staging));
    }

    [Fact]
    public async Task ReconcileAsync_restores_missing_chapter_from_backup_and_deletes_redundant_backup()
    {
        using var workspace = new TempDirectory();
        var chapters = Path.Combine(workspace.Path, "Generated");
        Directory.CreateDirectory(chapters);
        var missingBackup = Path.Combine(chapters, "vol1_ch3.md.bak");
        var redundantFinal = Path.Combine(chapters, "vol1_ch4.md");
        var redundantBackup = Path.Combine(chapters, "vol1_ch4.md.bak");
        await File.WriteAllTextAsync(missingBackup, "从备份恢复");
        await File.WriteAllTextAsync(redundantFinal, "已有正文");
        await File.WriteAllTextAsync(redundantBackup, "多余备份");

        var result = await new PortableConsistencyReconciler(workspace.Path).ReconcileAsync();

        Assert.Equal(2, result.BakCleaned);
        Assert.Equal("从备份恢复", await File.ReadAllTextAsync(Path.Combine(chapters, "vol1_ch3.md")));
        Assert.Equal("已有正文", await File.ReadAllTextAsync(redundantFinal));
        Assert.False(File.Exists(missingBackup));
        Assert.False(File.Exists(redundantBackup));
    }

    [Fact]
    public async Task ReconcileAsync_removes_temporary_files_and_orphan_backup_directories()
    {
        using var workspace = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(workspace.Path, "Config", "nested"));
        Directory.CreateDirectory(Path.Combine(workspace.Path, "Modules", "Design"));
        Directory.CreateDirectory(Path.Combine(workspace.Path, "_backup_restore"));
        await File.WriteAllTextAsync(Path.Combine(workspace.Path, "Config", "nested", "data.tmp"), "tmp");
        await File.WriteAllTextAsync(Path.Combine(workspace.Path, "Modules", "Design", "module.tmp"), "tmp");
        await File.WriteAllTextAsync(Path.Combine(workspace.Path, "manifest.json.tmp"), "tmp");

        var result = await new PortableConsistencyReconciler(workspace.Path).ReconcileAsync();

        Assert.Equal(4, result.StagingCleaned);
        Assert.False(File.Exists(Path.Combine(workspace.Path, "Config", "nested", "data.tmp")));
        Assert.False(File.Exists(Path.Combine(workspace.Path, "Modules", "Design", "module.tmp")));
        Assert.False(File.Exists(Path.Combine(workspace.Path, "manifest.json.tmp")));
        Assert.False(Directory.Exists(Path.Combine(workspace.Path, "_backup_restore")));
    }

    [Fact]
    public async Task ReconcileAsync_restores_corrupted_content_guide_from_valid_backup_and_reports_unrecoverable_content_guide()
    {
        using var workspace = new TempDirectory();
        var guides = Path.Combine(workspace.Path, "Config", "guides");
        Directory.CreateDirectory(guides);
        var recoverable = Path.Combine(guides, "content_guide_vol1.json");
        var unrecoverable = Path.Combine(guides, "content_guide_vol2.json");
        await File.WriteAllTextAsync(recoverable, "{ not-json");
        await File.WriteAllTextAsync(recoverable + ".bak", """{ "ok": true }""");
        await File.WriteAllTextAsync(unrecoverable, "{ still-not-json");

        var result = await new PortableConsistencyReconciler(workspace.Path).ReconcileAsync();

        using var restored = JsonDocument.Parse(await File.ReadAllTextAsync(recoverable));
        Assert.True(restored.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(["content_guide_vol2.json"], result.CorruptedGuides);
        Assert.Contains(result.Errors, error => error.Contains("content_guide 分片 [content_guide_vol2.json] 已损坏", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReconcileAsync_removes_orphan_summaries_and_rebuilds_affected_milestones()
    {
        using var workspace = new TempDirectory();
        var chapters = Path.Combine(workspace.Path, "Generated");
        Directory.CreateDirectory(chapters);
        await File.WriteAllTextAsync(Path.Combine(chapters, "vol1_ch1.md"), "第一章正文");
        var summaries = new InMemorySummaryRepairStore(new Dictionary<string, string>
        {
            ["vol1_ch1"] = "保留摘要",
            ["vol1_ch2"] = "孤立摘要"
        });

        var result = await new PortableConsistencyReconciler(workspace.Path, summaries).ReconcileAsync();

        Assert.Equal(1, result.SummariesRepaired);
        Assert.False(summaries.AllSummaries.ContainsKey("vol1_ch2"));
        Assert.Equal(["vol1"], summaries.RebuiltMilestones);
        Assert.Equal("保留摘要", summaries.RebuiltSnapshot[1]["vol1_ch1"]);
    }

    [Fact]
    public async Task ReconcileAsync_builds_missing_summary_from_chapter_head_and_rebuilds_volume_milestone()
    {
        using var workspace = new TempDirectory();
        var chapters = Path.Combine(workspace.Path, "Generated");
        Directory.CreateDirectory(chapters);
        var longBody = new string('命', 510) + "后续内容";
        await File.WriteAllTextAsync(Path.Combine(chapters, "vol2_ch3.md"), "第一句醒来。第二句转折！" + longBody);
        var summaries = new InMemorySummaryRepairStore();

        var result = await new PortableConsistencyReconciler(workspace.Path, summaries).ReconcileAsync();

        Assert.Equal(1, result.SummariesRepaired);
        Assert.StartsWith("第一句醒来。第二句转折！", summaries.AllSummaries["vol2_ch3"]);
        Assert.EndsWith("……", summaries.AllSummaries["vol2_ch3"]);
        Assert.Equal(502, summaries.AllSummaries["vol2_ch3"].Length);
        Assert.Equal(["vol2"], summaries.RebuiltMilestones);
        Assert.Equal(summaries.AllSummaries["vol2_ch3"], summaries.RebuiltSnapshot[2]["vol2_ch3"]);
    }

    [Fact]
    public async Task ReconcileAsync_repairs_missing_keyword_index_entries_from_known_names_in_summaries()
    {
        using var workspace = new TempDirectory();
        var chapters = Path.Combine(workspace.Path, "Generated");
        Directory.CreateDirectory(chapters);
        await File.WriteAllTextAsync(Path.Combine(chapters, "vol1_ch1.md"), "第一章正文");
        await File.WriteAllTextAsync(Path.Combine(chapters, "vol1_ch2.md"), "第二章正文");
        var summaries = new InMemorySummaryRepairStore(new Dictionary<string, string>
        {
            ["vol1_ch1"] = "沈天命在青岚宗点燃命火",
            ["vol1_ch2"] = "无关章节"
        });
        var keywords = new InMemoryKeywordIndexRepairStore(
            indexedChapterIds: ["vol1_ch2"],
            knownNames: ["沈天命", "青岚宗", "命火"]);

        var result = await new PortableConsistencyReconciler(workspace.Path, summaries, keywords).ReconcileAsync();

        Assert.Equal(1, result.KeywordIndexRepaired);
        Assert.Equal(["沈天命", "青岚宗", "命火"], keywords.IndexedKeywords["vol1_ch1"]);
        Assert.False(keywords.IndexedKeywords.ContainsKey("vol1_ch2"));
    }

    [Fact]
    public async Task ReconcileAsync_skips_keyword_index_repair_when_summary_has_no_known_names()
    {
        using var workspace = new TempDirectory();
        var chapters = Path.Combine(workspace.Path, "Generated");
        Directory.CreateDirectory(chapters);
        await File.WriteAllTextAsync(Path.Combine(chapters, "vol2_ch1.md"), "正文");
        var summaries = new InMemorySummaryRepairStore(new Dictionary<string, string>
        {
            ["vol2_ch1"] = "山雨欲来"
        });
        var keywords = new InMemoryKeywordIndexRepairStore(
            indexedChapterIds: [],
            knownNames: ["沈天命"]);

        var result = await new PortableConsistencyReconciler(workspace.Path, summaries, keywords).ReconcileAsync();

        Assert.Equal(0, result.KeywordIndexRepaired);
        Assert.Empty(keywords.IndexedKeywords);
    }

    [Fact]
    public async Task ReconcileAsync_clears_vector_degraded_flag_when_vector_index_returns_embedding_mode()
    {
        using var workspace = new TempDirectory();
        var flagPath = Path.Combine(workspace.Path, "vector_degraded.flag");
        await File.WriteAllTextAsync(flagPath, "1");
        var vector = new InMemoryVectorIndexRepairStore(VectorIndexRepairMode.LocalEmbedding);

        var result = await new PortableConsistencyReconciler(workspace.Path, vectorIndexRepairStore: vector).ReconcileAsync();

        Assert.True(vector.InitializeCalled);
        Assert.Equal(1, result.VectorReindexed);
        Assert.False(File.Exists(flagPath));
    }

    [Fact]
    public async Task ReconcileAsync_keeps_vector_degraded_flag_when_vector_index_stays_keyword_mode()
    {
        using var workspace = new TempDirectory();
        var flagPath = Path.Combine(workspace.Path, "vector_degraded.flag");
        await File.WriteAllTextAsync(flagPath, "1");
        var vector = new InMemoryVectorIndexRepairStore(VectorIndexRepairMode.Keyword);

        var result = await new PortableConsistencyReconciler(workspace.Path, vectorIndexRepairStore: vector).ReconcileAsync();

        Assert.True(vector.InitializeCalled);
        Assert.Equal(0, result.VectorReindexed);
        Assert.True(File.Exists(flagPath));
    }

    [Fact]
    public async Task ReconcileAsync_reports_tracking_gaps_clears_orphans_and_repairs_gap_summaries()
    {
        using var workspace = new TempDirectory();
        var chapters = Path.Combine(workspace.Path, "Generated");
        Directory.CreateDirectory(chapters);
        await File.WriteAllTextAsync(Path.Combine(chapters, "vol1_ch1.md"), "第一章已有追踪");
        await File.WriteAllTextAsync(Path.Combine(chapters, "vol1_ch2.md"), "第二章需要补摘要。后续剧情继续推进。");
        var summaries = new InMemorySummaryRepairStore(new Dictionary<string, string>
        {
            ["vol1_ch1"] = "已有摘要"
        });
        var tracking = new InMemoryTrackingRepairStore(["vol1_ch1", "vol9_ch9"]);

        var result = await new PortableConsistencyReconciler(
            workspace.Path,
            summaryRepairStore: summaries,
            trackingRepairStore: tracking).ReconcileAsync();

        Assert.Equal(["vol1_ch2"], result.TrackingGaps);
        Assert.Equal(1, result.TrackingOrphansCleared);
        Assert.Equal(["vol9_ch9"], tracking.RemovedChapters);
        Assert.Equal(1, result.TrackingGapSummariesRepaired);
        Assert.Equal("第二章需要补摘要。后续剧情继续推进。", summaries.AllSummaries["vol1_ch2"]);
    }

    [Fact]
    public async Task ReconcileAsync_repairs_missing_fact_archive_for_completed_volumes_only()
    {
        using var workspace = new TempDirectory();
        var summaries = new InMemorySummaryRepairStore(new Dictionary<string, string>
        {
            ["vol1_ch1"] = "第一卷第一章",
            ["vol1_ch2"] = "第一卷终章",
            ["vol2_ch1"] = "第二卷未完成"
        });
        var archive = new InMemoryFactArchiveRepairStore(
            archivedVolumes: [],
            configuredEndChapters: new Dictionary<int, int>
            {
                [1] = 2,
                [2] = 3
            });

        var result = await new PortableConsistencyReconciler(
            workspace.Path,
            summaryRepairStore: summaries,
            factArchiveRepairStore: archive).ReconcileAsync();

        Assert.Equal(1, result.FactArchivesRepaired);
        Assert.Equal([(1, "vol1_ch2")], archive.ArchivedVolumes);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-reconcile-{Guid.NewGuid():N}");

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }

    private sealed class InMemorySummaryRepairStore : IChapterSummaryRepairStore
    {
        public Dictionary<string, string> AllSummaries { get; }
        public List<string> RebuiltMilestones { get; } = [];
        public Dictionary<int, Dictionary<string, string>> RebuiltSnapshot { get; } = [];

        public InMemorySummaryRepairStore(Dictionary<string, string>? summaries = null)
        {
            AllSummaries = summaries == null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(summaries, StringComparer.OrdinalIgnoreCase);
        }

        public Task<Dictionary<string, string>> GetAllSummariesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new Dictionary<string, string>(AllSummaries, StringComparer.OrdinalIgnoreCase));
        }

        public Task RemoveSummaryAsync(string chapterId, CancellationToken cancellationToken = default)
        {
            AllSummaries.Remove(chapterId);
            return Task.CompletedTask;
        }

        public Task SetSummaryAsync(string chapterId, string summary, CancellationToken cancellationToken = default)
        {
            AllSummaries[chapterId] = summary;
            return Task.CompletedTask;
        }

        public Task<Dictionary<string, string>> GetVolumeSummariesAsync(int volumeNumber, CancellationToken cancellationToken = default)
        {
            var prefix = $"vol{volumeNumber}_";
            return Task.FromResult(AllSummaries
                .Where(pair => pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase));
        }

        public Task RebuildVolumeMilestoneAsync(
            int volumeNumber,
            Dictionary<string, string> volumeSummaries,
            CancellationToken cancellationToken = default)
        {
            RebuiltMilestones.Add($"vol{volumeNumber}");
            RebuiltSnapshot[volumeNumber] = new Dictionary<string, string>(volumeSummaries, StringComparer.OrdinalIgnoreCase);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryKeywordIndexRepairStore : IChapterKeywordIndexRepairStore
    {
        private readonly HashSet<string> _indexedChapterIds;
        private readonly List<string> _knownNames;
        public Dictionary<string, List<string>> IndexedKeywords { get; } = new(StringComparer.OrdinalIgnoreCase);

        public InMemoryKeywordIndexRepairStore(IEnumerable<string> indexedChapterIds, IEnumerable<string> knownNames)
        {
            _indexedChapterIds = new HashSet<string>(indexedChapterIds, StringComparer.OrdinalIgnoreCase);
            _knownNames = knownNames.ToList();
        }

        public Task<HashSet<string>> GetIndexedChapterIdsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HashSet<string>(_indexedChapterIds, StringComparer.OrdinalIgnoreCase));
        }

        public Task<IReadOnlyCollection<string>> GetKnownEntityNamesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyCollection<string>>(_knownNames);
        }

        public Task IndexChapterFromKeywordsAsync(
            string chapterId,
            IReadOnlyList<string> keywords,
            CancellationToken cancellationToken = default)
        {
            IndexedKeywords[chapterId] = keywords.ToList();
            _indexedChapterIds.Add(chapterId);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryVectorIndexRepairStore : IChapterVectorIndexRepairStore
    {
        private readonly VectorIndexRepairMode _mode;
        public bool InitializeCalled { get; private set; }

        public InMemoryVectorIndexRepairStore(VectorIndexRepairMode mode)
        {
            _mode = mode;
        }

        public Task<VectorIndexRepairMode> InitializeAsync(CancellationToken cancellationToken = default)
        {
            InitializeCalled = true;
            return Task.FromResult(_mode);
        }
    }

    private sealed class InMemoryTrackingRepairStore : IChapterTrackingRepairStore
    {
        private readonly HashSet<string> _trackedChapterIds;
        public List<string> RemovedChapters { get; } = [];

        public InMemoryTrackingRepairStore(IEnumerable<string> trackedChapterIds)
        {
            _trackedChapterIds = new HashSet<string>(trackedChapterIds, StringComparer.OrdinalIgnoreCase);
        }

        public Task<HashSet<string>> GetTrackedChapterIdsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HashSet<string>(_trackedChapterIds, StringComparer.OrdinalIgnoreCase));
        }

        public Task RemoveChapterTrackingAsync(string chapterId, CancellationToken cancellationToken = default)
        {
            RemovedChapters.Add(chapterId);
            _trackedChapterIds.Remove(chapterId);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryFactArchiveRepairStore : IVolumeFactArchiveRepairStore
    {
        private readonly HashSet<int> _archivedVolumes;
        private readonly IReadOnlyDictionary<int, int> _configuredEndChapters;
        public List<(int VolumeNumber, string LastChapterId)> ArchivedVolumes { get; } = [];

        public InMemoryFactArchiveRepairStore(
            IEnumerable<int> archivedVolumes,
            IReadOnlyDictionary<int, int>? configuredEndChapters = null)
        {
            _archivedVolumes = new HashSet<int>(archivedVolumes);
            _configuredEndChapters = configuredEndChapters ?? new Dictionary<int, int>();
        }

        public Task<HashSet<int>> GetArchivedVolumeNumbersAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HashSet<int>(_archivedVolumes));
        }

        public Task<IReadOnlyDictionary<int, int>> GetConfiguredEndChaptersAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_configuredEndChapters);
        }

        public Task ArchiveCompletedVolumeAsync(
            int volumeNumber,
            string lastChapterId,
            CancellationToken cancellationToken = default)
        {
            ArchivedVolumes.Add((volumeNumber, lastChapterId));
            _archivedVolumes.Add(volumeNumber);
            return Task.CompletedTask;
        }
    }
}
