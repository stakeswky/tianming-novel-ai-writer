using System.IO;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Context;
using Xunit;

namespace Tianming.ProjectData.Tests.Context;

public class DesignContextServiceTests
{
    [Fact]
    public async Task ListByCategory_returns_empty_for_unknown_category()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-dc-{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var svc = new DesignContextService(root);

        var list = await svc.ListByCategoryAsync("UnknownCategory");

        Assert.Empty(list);
    }

    [Fact]
    public async Task Search_finds_match_in_any_category()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-dc-{System.Guid.NewGuid():N}");
        var charDir = Path.Combine(root, "Design", "Elements", "Characters");
        Directory.CreateDirectory(charDir);
        await File.WriteAllTextAsync(
            Path.Combine(charDir, "char-001.json"),
            "{\"Id\":\"char-001\",\"Name\":\"沈砚\",\"Summary\":\"剑客\"}");

        var svc = new DesignContextService(root);

        var results = await svc.SearchAsync("沈砚");

        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Name == "沈砚");
    }

    [Fact]
    public async Task GetById_returns_matching_reference()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-dc-{System.Guid.NewGuid():N}");
        var plotDir = Path.Combine(root, "Design", "Elements", "Plot");
        Directory.CreateDirectory(plotDir);
        await File.WriteAllTextAsync(
            Path.Combine(plotDir, "plot-001.json"),
            "{\"Id\":\"plot-001\",\"Name\":\"命火试炼\",\"Description\":\"试炼主线\"}");

        var svc = new DesignContextService(root);

        var reference = await svc.GetByIdAsync("plot-001");

        Assert.NotNull(reference);
        Assert.Equal("命火试炼", reference!.Name);
        Assert.Equal("Plot", reference.Category);
    }
}
