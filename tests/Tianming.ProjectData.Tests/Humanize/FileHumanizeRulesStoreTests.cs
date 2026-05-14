using System.Collections.Generic;
using System.IO;
using TM.Services.Modules.ProjectData.Humanize;
using Xunit;

namespace Tianming.ProjectData.Tests.Humanize;

public class FileHumanizeRulesStoreTests
{
    [Fact]
    public void Load_returns_default_when_file_missing()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-hum-{System.Guid.NewGuid():N}");
        var store = new FileHumanizeRulesStore(dir);
        var cfg = store.Load();
        Assert.NotNull(cfg);
        Assert.NotEmpty(cfg.PhraseReplacements);
    }

    [Fact]
    public void Save_then_Load_round_trip()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-hum-{System.Guid.NewGuid():N}");
        var store = new FileHumanizeRulesStore(dir);
        var cfg = new HumanizeRulesConfig
        {
            PhraseReplacements = new Dictionary<string, string> { ["xxx"] = string.Empty },
            SentenceLongThreshold = 50,
        };
        store.Save(cfg);
        var back = store.Load();
        Assert.True(back.PhraseReplacements.ContainsKey("xxx"));
        Assert.Equal(50, back.SentenceLongThreshold);
    }

    [Fact]
    public void Load_returns_default_when_json_is_malformed()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-hum-{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "humanize_rules.json"), "{ malformed");

        var store = new FileHumanizeRulesStore(dir);
        var cfg = store.Load();

        Assert.NotNull(cfg);
        Assert.NotEmpty(cfg.PhraseReplacements);
        Assert.True(cfg.EnablePunctuation);
        Assert.Equal(40, cfg.SentenceLongThreshold);
    }
}
