using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using TM.Services.Modules.ProjectData.Humanize;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Infrastructure;

public class HumanizePipelineRegistrationTests
{
    [Fact]
    public void AddAvaloniaShell_registers_humanize_pipeline_and_builtin_rules()
    {
        var services = new ServiceCollection();
        services.AddAvaloniaShell();

        using var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<HumanizePipeline>();
        var rules = provider.GetServices<IHumanizeRule>().ToList();

        Assert.NotNull(pipeline);
        Assert.Equal(3, rules.Count);
    }
}
