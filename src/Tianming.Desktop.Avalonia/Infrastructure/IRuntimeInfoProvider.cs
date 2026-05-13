namespace Tianming.Desktop.Avalonia.Infrastructure;

public interface IRuntimeInfoProvider
{
    string FrameworkDescription { get; }
    bool IsLocalMode { get; }
}
