using System.Text.Json;
using TM.Services.Framework.AI.Core;
using Xunit;

namespace Tianming.AI.Tests.Core;

public class AITaskPurposeTests
{
    [Fact]
    public void All_five_purposes_defined()
    {
        Assert.True(System.Enum.IsDefined(typeof(AITaskPurpose), AITaskPurpose.Chat));
        Assert.True(System.Enum.IsDefined(typeof(AITaskPurpose), AITaskPurpose.Writing));
        Assert.True(System.Enum.IsDefined(typeof(AITaskPurpose), AITaskPurpose.Polish));
        Assert.True(System.Enum.IsDefined(typeof(AITaskPurpose), AITaskPurpose.Validation));
        Assert.True(System.Enum.IsDefined(typeof(AITaskPurpose), AITaskPurpose.Default));
    }

    [Fact]
    public void UserConfiguration_carries_purpose()
    {
        var cfg = new UserConfiguration { Purpose = "Writing" };

        var json = JsonSerializer.Serialize(cfg);
        var back = JsonSerializer.Deserialize<UserConfiguration>(json);

        Assert.Contains("Writing", json);
        Assert.Equal("Writing", back!.Purpose);
    }

    [Fact]
    public void UserConfiguration_defaults_purpose_for_legacy_json()
    {
        const string legacyJson = """{"Id":"cfg-1","Name":"Legacy"}""";

        var back = JsonSerializer.Deserialize<UserConfiguration>(legacyJson);

        Assert.Equal("Default", back!.Purpose);
    }
}
