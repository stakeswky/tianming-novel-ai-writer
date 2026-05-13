using Tianming.Desktop.Avalonia.Infrastructure;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Infrastructure;

public class RuntimeInfoProviderTests
{
    [Fact]
    public void FrameworkDescription_StartsWith_DotNet()
    {
        var p = new RuntimeInfoProvider();
        Assert.StartsWith(".NET", p.FrameworkDescription);
    }

    [Fact]
    public void IsLocalMode_AlwaysTrue()
    {
        var p = new RuntimeInfoProvider();
        Assert.True(p.IsLocalMode);
    }
}
