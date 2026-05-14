using System;
using System.Collections.Generic;
using System.Linq;

namespace TM.Services.Framework.AI.Core.Routing;

public sealed class DefaultAIModelRouter : IAIModelRouter
{
    private readonly Func<IReadOnlyList<UserConfiguration>> _configsProvider;

    public DefaultAIModelRouter(Func<IReadOnlyList<UserConfiguration>> configsProvider)
    {
        _configsProvider = configsProvider ?? throw new ArgumentNullException(nameof(configsProvider));
    }

    public UserConfiguration Resolve(AITaskPurpose purpose)
    {
        var configs = _configsProvider() ?? Array.Empty<UserConfiguration>();
        var enabled = configs.Where(config => config.IsEnabled).ToList();

        var purposeMatch = enabled.FirstOrDefault(config =>
            string.Equals(config.Purpose, purpose.ToString(), StringComparison.OrdinalIgnoreCase));
        if (purposeMatch != null)
            return purposeMatch;

        var active = enabled.FirstOrDefault(config => config.IsActive);
        if (active != null)
            return active;

        throw new InvalidOperationException(
            $"No UserConfiguration matches purpose {purpose} and no active default found.");
    }
}
