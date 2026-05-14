using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Context;

public interface IDesignContextService
{
    Task<IReadOnlyList<DesignReference>> ListByCategoryAsync(string category, CancellationToken ct = default);

    Task<IReadOnlyList<DesignReference>> SearchAsync(string query, CancellationToken ct = default);

    Task<DesignReference?> GetByIdAsync(string id, CancellationToken ct = default);
}

public sealed class DesignReference
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string RawJson { get; set; } = string.Empty;
}
