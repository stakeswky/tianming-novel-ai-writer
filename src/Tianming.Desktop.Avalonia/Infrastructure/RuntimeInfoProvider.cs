using System.Runtime.InteropServices;

namespace Tianming.Desktop.Avalonia.Infrastructure;

public sealed class RuntimeInfoProvider : IRuntimeInfoProvider
{
    public string FrameworkDescription { get; } = RuntimeInformation.FrameworkDescription;
    public bool IsLocalMode => true; // 自用 mode 锁定
}
