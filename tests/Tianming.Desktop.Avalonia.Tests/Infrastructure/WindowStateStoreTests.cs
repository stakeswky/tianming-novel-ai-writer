using System;
using System.IO;
using Tianming.Desktop.Avalonia.Infrastructure;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Infrastructure;

public class WindowStateStoreTests
{
    private static string TempRoot() => Path.Combine(Path.GetTempPath(), $"tianming-{Guid.NewGuid():N}");

    [Fact]
    public void Load_WhenFileMissing_ReturnsDefault()
    {
        var dir = TempRoot();
        Directory.CreateDirectory(dir);
        try
        {
            var store = new WindowStateStore(Path.Combine(dir, "window_state.json"));
            var state = store.Load();
            Assert.Equal(1200.0, state.Width);
            Assert.Equal(800.0,  state.Height);
            Assert.Equal(240.0,  state.LeftColumnWidth);
            Assert.Equal(360.0,  state.RightColumnWidth);
            Assert.False(state.IsMaximized);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void SaveThenLoad_RoundTrip()
    {
        var dir = TempRoot();
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "window_state.json");
            var store = new WindowStateStore(path);
            var saved = new WindowState(X: 100, Y: 200, Width: 1400, Height: 900,
                LeftColumnWidth: 300, RightColumnWidth: 400, IsMaximized: true);
            store.Save(saved);
            var loaded = store.Load();
            Assert.Equal(saved, loaded);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Load_WhenFileCorrupted_ReturnsDefault()
    {
        var dir = TempRoot();
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "window_state.json");
            File.WriteAllText(path, "{ not valid json");
            var store = new WindowStateStore(path);
            var state = store.Load();
            Assert.Equal(1200.0, state.Width);
        }
        finally { Directory.Delete(dir, true); }
    }
}
