using System;
using TM.Framework.UI.Workspace.RightPanel.Modes;

namespace TM.Services.Framework.AI.SemanticKernel;

public enum ExecutionEventType
{
    RunStarted,
    RunCompleted,
    RunFailed,
    UserMessage,
    AssistantMessage,
    ToolCallStarted,
    ToolCallCompleted,
    ToolCallFailed,
    PlanStepStarted,
    PlanStepCompleted,
    Info
}

public sealed class ExecutionEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RunId { get; set; }
    public ChatMode Mode { get; set; } = ChatMode.Ask;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public ExecutionEventType EventType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? PluginName { get; set; }
    public string? FunctionName { get; set; }
    public int? StepIndex { get; set; }
    public bool? Succeeded { get; set; }
}
