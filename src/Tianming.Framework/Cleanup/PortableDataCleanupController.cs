namespace TM.Framework.Cleanup;

public interface IPortableCleanupServiceRefresher
{
    Task RefreshAsync(string serviceName, CancellationToken cancellationToken = default);
}

public sealed class PortableCleanupExecutionResult
{
    public required PortableCleanupResult CleanupResult { get; init; }

    public IReadOnlyList<string> RefreshedServices { get; init; } = [];
}

public sealed class PortableDataCleanupController
{
    private readonly PortableDataCleanupService _cleanupService;
    private readonly IPortableCleanupServiceRefresher _serviceRefresher;

    public PortableDataCleanupController(
        PortableDataCleanupService cleanupService,
        IPortableCleanupServiceRefresher serviceRefresher)
    {
        _cleanupService = cleanupService ?? throw new ArgumentNullException(nameof(cleanupService));
        _serviceRefresher = serviceRefresher ?? throw new ArgumentNullException(nameof(serviceRefresher));
    }

    public async Task<PortableCleanupExecutionResult> CleanupAsync(
        IEnumerable<PortableCleanupItem> items,
        CancellationToken cancellationToken = default)
    {
        var itemList = items.ToList();
        var cleanupResult = _cleanupService.Cleanup(itemList);
        var servicesToRefresh = PortableCleanupServiceRefreshPlanner.GetServicesToRefresh(itemList);

        foreach (var serviceName in servicesToRefresh)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _serviceRefresher.RefreshAsync(serviceName, cancellationToken).ConfigureAwait(false);
        }

        return new PortableCleanupExecutionResult
        {
            CleanupResult = cleanupResult,
            RefreshedServices = servicesToRefresh
        };
    }
}

public static class PortableCleanupServiceRefreshPlanner
{
    public static IReadOnlyList<string> GetServicesToRefresh(IEnumerable<PortableCleanupItem> items)
    {
        var services = new List<string>();

        foreach (var item in items)
        {
            var normalizedPath = item.RelativePath.Replace('\\', '/');

            if ((normalizedPath.Contains("Services/AI/Library", StringComparison.OrdinalIgnoreCase) ||
                 normalizedPath.Contains("Services/AI/Configurations", StringComparison.OrdinalIgnoreCase)) &&
                !services.Contains("AIService"))
            {
                services.Add("AIService");
            }

            if (normalizedPath.Contains("Projects/Sessions", StringComparison.OrdinalIgnoreCase) &&
                !services.Contains("SessionManager"))
            {
                services.Add("SessionManager");
            }
        }

        return services;
    }
}
