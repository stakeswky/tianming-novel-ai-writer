using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tianming.Desktop.Avalonia.Navigation;

public interface INavigationService
{
    PageKey? CurrentKey { get; }
    object?  CurrentViewModel { get; }
    object?  LastParameter { get; }
    bool     CanGoBack { get; }

    Task NavigateAsync(PageKey key, object? parameter = null, CancellationToken ct = default);
    Task GoBackAsync(CancellationToken ct = default);

    event EventHandler<PageKey>? CurrentKeyChanged;
}
