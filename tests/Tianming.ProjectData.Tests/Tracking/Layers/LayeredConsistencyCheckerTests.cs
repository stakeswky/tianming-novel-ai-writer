using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Rules;
using TM.Services.Modules.ProjectData.Models.Tracking;
using TM.Services.Modules.ProjectData.Tracking.Layers;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking.Layers;

public class LayeredConsistencyCheckerTests
{
    [Fact]
    public async Task Runs_all_5_layers_and_groups_issues_by_layer()
    {
        var checker = new LayeredConsistencyChecker(
        [
            new StructuralLayer(),
            new EntityLayer(),
            new ForeshadowLayer(),
            new TimelineLayer(),
            new RelationshipLayer()
        ]);

        var result = await checker.CheckAsync(new ChapterChanges(), new FactSnapshot(), LedgerRuleSet.CreateUniversalDefault());

        Assert.Equal(5, result.LayerNames.Count);
        Assert.True(result.IssuesByLayer.ContainsKey("Structural"));
        Assert.True(result.IssuesByLayer.ContainsKey("Entity"));
        Assert.True(result.IssuesByLayer.ContainsKey("Foreshadow"));
        Assert.True(result.IssuesByLayer.ContainsKey("Timeline"));
        Assert.True(result.IssuesByLayer.ContainsKey("Relationship"));
    }

    [Fact]
    public async Task AllIssues_concatenates_all_layer_issues()
    {
        var checker = new LayeredConsistencyChecker([new StructuralLayer()]);

        var result = await checker.CheckAsync(null!, new FactSnapshot(), LedgerRuleSet.CreateUniversalDefault());

        Assert.Contains(result.AllIssues, issue => issue.IssueType == "NullChanges");
    }
}
