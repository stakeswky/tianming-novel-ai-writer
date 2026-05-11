using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableGenerationHistoryStoreTests
{
    [Fact]
    public async Task AddOrUpdateAsync_assigns_id_and_returns_records_newest_first()
    {
        using var workspace = new TempDirectory();
        var store = new FileGenerationHistoryStore(
            Path.Combine(workspace.Path, "generation_history.json"),
            () => "D-fixed");

        await store.AddOrUpdateAsync(new PortableGenerationHistoryRecord
        {
            Type = "图片取色",
            Name = "旧方案",
            Timestamp = new DateTime(2026, 5, 10, 9, 0, 0),
            PrimaryColor = "#112233"
        });
        await store.AddOrUpdateAsync(new PortableGenerationHistoryRecord
        {
            Id = "existing",
            Type = "AI配色",
            Name = "新方案",
            Timestamp = new DateTime(2026, 5, 10, 10, 0, 0),
            PrimaryColor = "#445566"
        });

        var records = await store.GetAllAsync();

        Assert.Equal(new[] { "existing", "D-fixed" }, records.Select(record => record.Id));
        Assert.Equal("新方案", records[0].Name);
    }

    [Fact]
    public async Task AddOrUpdateAsync_replaces_existing_record_with_same_id()
    {
        using var workspace = new TempDirectory();
        var store = new FileGenerationHistoryStore(Path.Combine(workspace.Path, "generation_history.json"));

        await store.AddOrUpdateAsync(new PortableGenerationHistoryRecord { Id = "r1", Name = "before" });
        await store.AddOrUpdateAsync(new PortableGenerationHistoryRecord { Id = "r1", Name = "after", IsFavorite = true });
        var records = await store.GetAllAsync();

        Assert.Single(records);
        Assert.Equal("after", records[0].Name);
        Assert.True(records[0].IsFavorite);
    }

    [Fact]
    public async Task Query_methods_filter_by_favorite_type_date_range_and_keyword()
    {
        using var workspace = new TempDirectory();
        var store = new FileGenerationHistoryStore(Path.Combine(workspace.Path, "generation_history.json"));
        await store.AddOrUpdateAsync(new PortableGenerationHistoryRecord
        {
            Id = "image",
            Type = "图片取色",
            Name = "森林主题",
            Keywords = "自然 绿色",
            Timestamp = new DateTime(2026, 5, 9),
            IsFavorite = true
        });
        await store.AddOrUpdateAsync(new PortableGenerationHistoryRecord
        {
            Id = "ai",
            Type = "AI配色",
            Name = "赛博主题",
            Keywords = "霓虹",
            Timestamp = new DateTime(2026, 5, 11)
        });

        Assert.Equal("image", (await store.GetFavoriteRecordsAsync()).Single().Id);
        Assert.Equal("ai", (await store.GetRecordsByTypeAsync("AI配色")).Single().Id);
        Assert.Equal("image", (await store.GetRecordsByDateRangeAsync(new DateTime(2026, 5, 8), new DateTime(2026, 5, 9, 23, 59, 59))).Single().Id);
        Assert.Equal("image", (await store.SearchRecordsAsync("绿色")).Single().Id);
    }

    [Fact]
    public async Task UpdateFavorite_delete_and_clear_mutate_persisted_records()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "generation_history.json");
        var store = new FileGenerationHistoryStore(path);
        await store.AddOrUpdateAsync(new PortableGenerationHistoryRecord { Id = "a", Name = "A" });
        await store.AddOrUpdateAsync(new PortableGenerationHistoryRecord { Id = "b", Name = "B" });

        await store.UpdateFavoriteAsync("a", true);
        await store.DeleteRecordAsync("b");
        var reloaded = new FileGenerationHistoryStore(path);

        Assert.Equal("a", (await reloaded.GetFavoriteRecordsAsync()).Single().Id);
        Assert.Equal("a", (await reloaded.GetAllAsync()).Single().Id);

        await reloaded.ClearAllAsync();
        Assert.Empty(await store.GetAllAsync());
    }

    [Fact]
    public async Task GetStatisticsAsync_matches_original_counts_relative_to_clock()
    {
        using var workspace = new TempDirectory();
        var now = new DateTime(2026, 5, 13, 12, 0, 0);
        var store = new FileGenerationHistoryStore(Path.Combine(workspace.Path, "generation_history.json"), clock: () => now);
        await store.AddOrUpdateAsync(new PortableGenerationHistoryRecord
        {
            Id = "today",
            Type = "图片取色",
            Timestamp = now,
            IsFavorite = true
        });
        await store.AddOrUpdateAsync(new PortableGenerationHistoryRecord
        {
            Id = "week",
            Type = "AI配色",
            Timestamp = new DateTime(2026, 5, 11)
        });
        await store.AddOrUpdateAsync(new PortableGenerationHistoryRecord
        {
            Id = "old",
            Type = "AI配色",
            Timestamp = new DateTime(2026, 4, 30)
        });

        var statistics = await store.GetStatisticsAsync();

        Assert.Equal(3, statistics.TotalCount);
        Assert.Equal(1, statistics.FavoriteCount);
        Assert.Equal(1, statistics.ImagePickerCount);
        Assert.Equal(2, statistics.AIGeneratedCount);
        Assert.Equal(1, statistics.TodayCount);
        Assert.Equal(2, statistics.ThisWeekCount);
        Assert.Equal(2, statistics.ThisMonthCount);
    }

    [Fact]
    public async Task Load_recovers_from_missing_or_invalid_json()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "generation_history.json");
        var missingStore = new FileGenerationHistoryStore(path);

        Assert.Empty(await missingStore.GetAllAsync());

        await File.WriteAllTextAsync(path, "{ invalid json");
        var invalidStore = new FileGenerationHistoryStore(path);

        Assert.Empty(await invalidStore.GetAllAsync());
    }
}
