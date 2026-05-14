using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Tianming.Desktop.Avalonia.ViewModels.Conversation;

public interface IReferenceSuggestionSource
{
    Task<IReadOnlyList<ReferenceItemVm>> SuggestAsync(string query, CancellationToken ct = default);
}
