using TM.Framework.Notifications;
using Xunit;

namespace Tianming.Framework.Tests;

public class NotificationHistoryStoreTests
{
    [Fact]
    public async Task AddRecordAsync_prepends_records_trims_to_max_and_persists()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "notification_history.json");
        var clock = new SequenceClock(new DateTime(2026, 5, 10, 8, 0, 0));
        var ids = new SequenceIdFactory();
        var store = new FileNotificationHistoryStore(path, clock.Next, ids.Next);

        for (var i = 1; i <= 12; i++)
        {
            await store.AddRecordAsync($"标题{i}", $"内容{i}", "Info", wasBlocked: i % 2 == 0);
        }

        var records = await store.GetRecordsAsync();
        var reloaded = await new FileNotificationHistoryStore(path).GetRecordsAsync();

        Assert.Equal(10, records.Count);
        Assert.Equal("标题12", records[0].Title);
        Assert.Equal("标题3", records[^1].Title);
        Assert.True(records[0].WasBlocked);
        Assert.False(records[0].IsRead);
        Assert.Equal(records.Select(r => r.Title), reloaded.Select(r => r.Title));
    }

    [Fact]
    public async Task MarkDeleteAndClear_update_persisted_history()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "notification_history.json");
        var store = new FileNotificationHistoryStore(path, () => DateTime.UnixEpoch, new SequenceIdFactory().Next);

        var first = await store.AddRecordAsync("第一条", "内容", "Info");
        var second = await store.AddRecordAsync("第二条", "内容", "Warning");

        Assert.True(await store.MarkAsReadAsync(first.Id));
        Assert.True(await store.DeleteRecordAsync(second.Id));

        var records = await new FileNotificationHistoryStore(path).GetRecordsAsync();
        var remaining = Assert.Single(records);
        Assert.Equal(first.Id, remaining.Id);
        Assert.True(remaining.IsRead);

        await store.ClearAllAsync();

        Assert.Empty(await new FileNotificationHistoryStore(path).GetRecordsAsync());
    }

    [Fact]
    public async Task GetRecordsAsync_recovers_from_missing_or_bad_json()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "notification_history.json");

        Assert.Empty(await new FileNotificationHistoryStore(path).GetRecordsAsync());

        await File.WriteAllTextAsync(path, "{bad json}");

        Assert.Empty(await new FileNotificationHistoryStore(path).GetRecordsAsync());
    }

    [Fact]
    public async Task Controller_loads_records_with_statistics_and_original_time_display()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "notification_history.json");
        var store = new FileNotificationHistoryStore(
            path,
            () => new DateTime(2026, 5, 10, 8, 30, 0),
            new SequenceIdFactory().Next);
        await store.AddRecordAsync("第一条", "内容", "信息");
        await store.AddRecordAsync("第二条", "内容", "错误", wasBlocked: true);
        await store.MarkAsReadAsync("D001");

        var controller = new PortableNotificationHistoryController(store);
        var snapshot = await controller.LoadAsync();

        Assert.Equal(2, snapshot.TotalCount);
        Assert.Equal(1, snapshot.UnreadCount);
        Assert.Equal("第二条", snapshot.Records[0].Title);
        Assert.True(snapshot.Records[0].WasBlocked);
        Assert.Equal("2026-05-10 08:30", snapshot.Records[0].TimeDisplay);
    }

    [Fact]
    public async Task Controller_updates_records_and_returns_original_user_messages()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "notification_history.json");
        var store = new FileNotificationHistoryStore(path, () => DateTime.UnixEpoch, new SequenceIdFactory().Next);
        var first = await store.AddRecordAsync("第一条", "内容", "信息");
        var second = await store.AddRecordAsync("第二条", "内容", "错误");
        var controller = new PortableNotificationHistoryController(store);
        await controller.LoadAsync();

        Assert.True(await controller.MarkAsReadAsync(first.Id));
        var deleteResult = await controller.DeleteRecordAsync(second.Id);
        var snapshot = await controller.GetSnapshotAsync();

        Assert.Equal("已删除该条通知记录", deleteResult.Message);
        var remaining = Assert.Single(snapshot.Records);
        Assert.Equal(first.Id, remaining.Id);
        Assert.True(remaining.IsRead);
        Assert.Equal(0, snapshot.UnreadCount);

        var clearResult = await controller.ClearAllAsync(confirm: true);

        Assert.Equal("已清空所有通知历史", clearResult.Message);
        Assert.Empty((await controller.GetSnapshotAsync()).Records);
    }

    [Fact]
    public async Task Controller_clear_all_respects_confirmation()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "notification_history.json");
        var store = new FileNotificationHistoryStore(path, () => DateTime.UnixEpoch, new SequenceIdFactory().Next);
        await store.AddRecordAsync("第一条", "内容", "信息");
        var controller = new PortableNotificationHistoryController(store);
        await controller.LoadAsync();

        var result = await controller.ClearAllAsync(confirm: false);

        Assert.False(result.Changed);
        Assert.Single((await controller.GetSnapshotAsync()).Records);
    }

    private sealed class SequenceClock
    {
        private DateTime _next;

        public SequenceClock(DateTime start) => _next = start;

        public DateTime Next()
        {
            var value = _next;
            _next = _next.AddMinutes(1);
            return value;
        }
    }

    private sealed class SequenceIdFactory
    {
        private int _next;

        public string Next() => $"D{++_next:000}";
    }
}
