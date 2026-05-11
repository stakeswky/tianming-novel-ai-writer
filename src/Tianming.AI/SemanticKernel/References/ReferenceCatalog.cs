using System;
using System.Collections.Generic;
using System.Linq;

namespace TM.Services.Framework.AI.SemanticKernel.References;

public sealed class ReferenceTypeInfo
{
    public string Type { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public sealed class ReferenceItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public static class ReferenceCatalog
{
    public static IReadOnlyList<ReferenceTypeInfo> GetAvailableTypes() =>
    [
        new() { Type = "续写", Icon = "\U0001F4D6", Description = "注入章节上下文" },
        new() { Type = "重写", Icon = "\u267B\uFE0F", Description = "重写指定章节" },
        new() { Type = "仿写", Icon = "\u270D\uFE0F", Description = "引用爬取内容" }
    ];

    public static string BuildReferenceToken(string selectedType, ReferenceItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var value = string.Equals(selectedType, "仿写", StringComparison.Ordinal)
            ? item.Name
            : item.Id;
        return $"@{selectedType}:{value}";
    }

    public static IReadOnlyList<ReferenceItem> FilterItems(IEnumerable<ReferenceItem> items, string? keyword)
    {
        var source = items?.ToList() ?? [];
        var trimmed = keyword?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return source;
        }

        return source
            .Where(item =>
                (!string.IsNullOrEmpty(item.Name) &&
                 item.Name.Contains(trimmed, StringComparison.CurrentCultureIgnoreCase)) ||
                (!string.IsNullOrEmpty(item.Id) &&
                 item.Id.Contains(trimmed, StringComparison.CurrentCultureIgnoreCase)))
            .ToList();
    }
}
