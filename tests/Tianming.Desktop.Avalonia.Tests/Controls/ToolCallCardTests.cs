using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Controls;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class ToolCallCardTests
{
    [AvaloniaFact]
    public void Defaults_StatePending_ToolNameEmpty()
    {
        var c = new ToolCallCard();
        Assert.Equal(string.Empty, c.ToolName);
        Assert.Null(c.ArgumentsPreview);
        Assert.Equal(ToolCallState.Pending, c.State);
        Assert.Null(c.ApproveCommand);
        Assert.Null(c.RejectCommand);
    }

    [AvaloniaFact]
    public void SetToolNameAndArgs_Persists()
    {
        var c = new ToolCallCard
        {
            ToolName = "DataEditPlugin.UpdateCharacter",
            ArgumentsPreview = "{\"id\":\"C001\",\"name\":\"白起\"}"
        };
        Assert.Equal("DataEditPlugin.UpdateCharacter", c.ToolName);
        Assert.Contains("C001", c.ArgumentsPreview);
    }

    [AvaloniaFact]
    public void StateTransitions_AndCommands_Persist()
    {
        var approve = new RelayCommand(() => { });
        var reject = new RelayCommand(() => { });
        var c = new ToolCallCard { ApproveCommand = approve, RejectCommand = reject };
        c.State = ToolCallState.Applied;
        Assert.Equal(ToolCallState.Applied, c.State);
        Assert.Same(approve, c.ApproveCommand);
        Assert.Same(reject, c.RejectCommand);
    }
}
