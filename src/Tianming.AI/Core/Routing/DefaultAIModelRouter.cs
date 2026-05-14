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

        var defaultConfig = enabled.FirstOrDefault(config =>
            string.Equals(config.Purpose, AITaskPurpose.Default.ToString(), StringComparison.OrdinalIgnoreCase)
            && config.IsActive)
            ?? enabled.FirstOrDefault(config =>
                string.Equals(config.Purpose, AITaskPurpose.Default.ToString(), StringComparison.OrdinalIgnoreCase));
        if (defaultConfig != null)
            return defaultConfig;

        throw new InvalidOperationException(
            $"No enabled UserConfiguration matches purpose {purpose} and no enabled default-purpose configuration was found.");
    }
}
