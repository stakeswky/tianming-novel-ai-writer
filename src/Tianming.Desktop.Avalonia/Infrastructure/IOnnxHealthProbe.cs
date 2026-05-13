using System.Threading;
using System.Threading.Tasks;
using Tianming.Desktop.Avalonia.Shell;

namespace Tianming.Desktop.Avalonia.Infrastructure;

public interface IOnnxHealthProbe
{
    Task<StatusIndicator> ProbeAsync(CancellationToken ct = default);
}
