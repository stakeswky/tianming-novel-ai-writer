namespace TM.Framework.Proxy;

public enum PortableProxyChainStrategy
{
    Sequential,
    Failover,
    LoadBalance
}

public sealed class PortableProxyChainSettings
{
    public string ActiveChainId { get; init; } = string.Empty;

    public IReadOnlyList<PortableProxyChainConfig> Chains { get; init; } = [];

    public IReadOnlyList<PortableProxyChainHistory> History { get; init; } = [];

    public IReadOnlyList<PortableProxyChainPerformance> Performance { get; init; } = [];
}

public sealed class PortableProxyChainConfig
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public PortableProxyChainStrategy Strategy { get; init; } = PortableProxyChainStrategy.Sequential;

    public bool AutoFailover { get; init; } = true;

    public int HealthCheckInterval { get; init; } = 60;

    public bool Enabled { get; init; } = true;

    public IReadOnlyList<PortableProxyNode> Nodes { get; init; } = [];
}

public sealed class PortableProxyNode
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public PortableProxyConfig Config { get; init; } = new();

    public int Order { get; init; }

    public bool Enabled { get; init; } = true;

    public int Latency { get; init; }

    public bool IsAvailable { get; init; } = true;
}

public sealed class PortableProxyChainHistory
{
    public string ChainId { get; init; } = string.Empty;

    public string ChainName { get; init; } = string.Empty;

    public DateTime StartTime { get; init; } = DateTime.Now;

    public DateTime EndTime { get; init; }

    public TimeSpan Duration => EndTime - StartTime;

    public bool Success { get; init; }

    public string ErrorMessage { get; init; } = string.Empty;

    public int TotalNodes { get; init; }

    public int SuccessfulNodes { get; init; }

    public double AverageLatency { get; init; }
}

public sealed class PortableProxyChainPerformance
{
    public string ChainId { get; init; } = string.Empty;

    public string ChainName { get; init; } = string.Empty;

    public int TotalUses { get; init; }

    public int SuccessfulUses { get; init; }

    public int FailedUses { get; init; }

    public double SuccessRate => TotalUses > 0 ? SuccessfulUses * 100.0 / TotalUses : 0;

    public double AverageTotalLatency { get; init; }

    public IReadOnlyList<PortableProxyNodePerformance> NodePerformances { get; init; } = [];

    public DateTime FirstUsed { get; init; }

    public DateTime LastUsed { get; init; }
}

public sealed class PortableProxyNodePerformance
{
    public string NodeId { get; init; } = string.Empty;

    public string NodeName { get; init; } = string.Empty;

    public double AverageLatency { get; init; }

    public int SuccessCount { get; init; }

    public int FailCount { get; init; }

    public double SuccessRate => (SuccessCount + FailCount) > 0
        ? SuccessCount * 100.0 / (SuccessCount + FailCount)
        : 0;
}

public sealed class PortableProxyChainComparison
{
    public DateTime ComparisonTime { get; init; } = DateTime.Now;

    public IReadOnlyList<PortableProxyChainComparisonItem> Items { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public string BestChainId { get; init; } = string.Empty;

    public string BestChainName { get; init; } = string.Empty;
}

public sealed class PortableProxyChainComparisonItem
{
    public string ChainId { get; init; } = string.Empty;

    public string ChainName { get; init; } = string.Empty;

    public double SuccessRate { get; init; }

    public double AverageLatency { get; init; }

    public int TotalUses { get; init; }

    public int Rank { get; init; }

    public string PerformanceGrade { get; init; } = string.Empty;
}

public sealed class PortableProxyChainOptimization
{
    public string ChainId { get; init; } = string.Empty;

    public string ChainName { get; init; } = string.Empty;

    public IReadOnlyList<string> Suggestions { get; init; } = [];

    public IReadOnlyList<PortableProxyNodeOptimization> NodeOptimizations { get; init; } = [];

    public PortableProxyChainOptimizationPriority Priority { get; init; }

    public string Summary { get; init; } = string.Empty;
}

public sealed class PortableProxyNodeOptimization
{
    public string NodeId { get; init; } = string.Empty;

    public string NodeName { get; init; } = string.Empty;

    public PortableProxyNodeAction RecommendedAction { get; init; }

    public string Reason { get; init; } = string.Empty;
}

public enum PortableProxyNodeAction
{
    Keep,
    MoveUp,
    MoveDown,
    Remove,
    Replace
}

public enum PortableProxyChainOptimizationPriority
{
    Low,
    Medium,
    High,
    Critical
}

public sealed class PortableProxyChainReport
{
    public DateTime GeneratedTime { get; init; } = DateTime.Now;

    public int TotalChains { get; init; }

    public int ActiveChains { get; init; }

    public IReadOnlyList<PortableProxyChainPerformance> TopPerformers { get; init; } = [];

    public IReadOnlyList<PortableProxyChainPerformance> PoorPerformers { get; init; } = [];

    public PortableProxyChainComparison Comparison { get; init; } = new();

    public IReadOnlyList<PortableProxyChainOptimization> Optimizations { get; init; } = [];

    public string Summary { get; init; } = string.Empty;

    public int HealthScore { get; init; }
}

public sealed class PortableProxyChainSelection
{
    public string ChainId { get; init; } = string.Empty;

    public string NodeId { get; init; } = string.Empty;

    public PortableProxyConfig Config { get; init; } = new();
}

public sealed record PortableProxyNodeHealthResult(bool IsAvailable, int Latency, string Error);

public interface IPortableProxyNodeHealthTester
{
    Task<PortableProxyNodeHealthResult> TestAsync(
        PortableProxyNode node,
        CancellationToken cancellationToken = default);
}

public enum PortableProxyChainHealthStatus
{
    Skipped,
    Healthy,
    Degraded,
    FailedOver,
    Failed
}

public sealed class PortableProxyChainHealthCheckResult
{
    public PortableProxyChainSettings Settings { get; init; } = new();

    public PortableProxyChainHealthStatus Status { get; init; }

    public bool ActiveChainChanged { get; init; }

    public string Message { get; init; } = string.Empty;
}

public static class PortableProxyChainSelector
{
    public static PortableProxyChainSelection? SelectActiveProxy(PortableProxyChainSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ActiveChainId))
        {
            return null;
        }

        var chain = settings.Chains.FirstOrDefault(candidate =>
            candidate.Enabled &&
            string.Equals(candidate.Id, settings.ActiveChainId, StringComparison.Ordinal));

        if (chain is null)
        {
            return null;
        }

        var availableCandidates = chain.Nodes
            .Where(node => node.Enabled && node.IsAvailable)
            .ToList();

        var candidates = availableCandidates.Count > 0
            ? availableCandidates
            : chain.Nodes.Where(node => node.Enabled).ToList();

        var selectedNode = chain.Strategy == PortableProxyChainStrategy.LoadBalance
            ? candidates.OrderBy(node => node.Latency).ThenBy(node => node.Order).FirstOrDefault()
            : candidates.OrderBy(node => node.Order).FirstOrDefault();

        if (selectedNode is null || !IsSupportedManagedHttpProxy(selectedNode.Config))
        {
            return null;
        }

        return new PortableProxyChainSelection
        {
            ChainId = chain.Id,
            NodeId = selectedNode.Id,
            Config = selectedNode.Config
        };
    }

    private static bool IsSupportedManagedHttpProxy(PortableProxyConfig config)
    {
        return config.Type is PortableProxyType.Http or PortableProxyType.Https &&
               !string.IsNullOrWhiteSpace(config.Server) &&
               config.Port > 0;
    }
}

public sealed class PortableProxyChainHealthController
{
    private readonly IPortableProxyNodeHealthTester _tester;
    private readonly Func<DateTime> _clock;

    public PortableProxyChainHealthController(
        IPortableProxyNodeHealthTester tester,
        Func<DateTime>? clock = null)
    {
        _tester = tester ?? throw new ArgumentNullException(nameof(tester));
        _clock = clock ?? (() => DateTime.Now);
    }

    public async Task<PortableProxyChainHealthCheckResult> CheckActiveChainAsync(
        PortableProxyChainSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.ActiveChainId))
        {
            return new PortableProxyChainHealthCheckResult
            {
                Settings = settings,
                Status = PortableProxyChainHealthStatus.Skipped,
                Message = "未配置生效代理链"
            };
        }

        var activeChain = settings.Chains.FirstOrDefault(chain =>
            chain.Enabled &&
            string.Equals(chain.Id, settings.ActiveChainId, StringComparison.Ordinal));
        if (activeChain is null)
        {
            return new PortableProxyChainHealthCheckResult
            {
                Settings = settings,
                Status = PortableProxyChainHealthStatus.Skipped,
                Message = "生效代理链不存在或已禁用"
            };
        }

        if (!activeChain.AutoFailover)
        {
            return new PortableProxyChainHealthCheckResult
            {
                Settings = settings,
                Status = PortableProxyChainHealthStatus.Skipped,
                Message = "生效代理链未启用自动健康检查"
            };
        }

        var checkedActive = await CheckChainAsync(activeChain, cancellationToken).ConfigureAwait(false);
        var chains = settings.Chains
            .Select(chain => string.Equals(chain.Id, activeChain.Id, StringComparison.Ordinal) ? checkedActive.Chain : chain)
            .ToList();
        var history = settings.History.Append(checkedActive.History).ToList();
        var activeHasAvailableNode = checkedActive.Chain.Nodes.Any(node => node.Enabled && node.IsAvailable);

        if (activeHasAvailableNode)
        {
            return new PortableProxyChainHealthCheckResult
            {
                Settings = CopySettings(settings, settings.ActiveChainId, chains, history),
                Status = checkedActive.History.SuccessfulNodes == checkedActive.History.TotalNodes
                    ? PortableProxyChainHealthStatus.Healthy
                    : PortableProxyChainHealthStatus.Degraded,
                Message = checkedActive.History.SuccessfulNodes == checkedActive.History.TotalNodes
                    ? "生效代理链健康"
                    : "生效代理链部分节点不可用"
            };
        }

        foreach (var candidate in chains.Where(chain =>
                     chain.Enabled &&
                     !string.Equals(chain.Id, activeChain.Id, StringComparison.Ordinal)))
        {
            var checkedCandidate = await CheckChainAsync(candidate, cancellationToken).ConfigureAwait(false);
            var candidateIndex = chains.FindIndex(chain => string.Equals(chain.Id, candidate.Id, StringComparison.Ordinal));
            chains[candidateIndex] = checkedCandidate.Chain;
            history.Add(checkedCandidate.History);

            if (checkedCandidate.Chain.Nodes.Any(node => node.Enabled && node.IsAvailable))
            {
                return new PortableProxyChainHealthCheckResult
                {
                    Settings = CopySettings(settings, checkedCandidate.Chain.Id, chains, history),
                    Status = PortableProxyChainHealthStatus.FailedOver,
                    ActiveChainChanged = true,
                    Message = $"已切换到可用代理链: {checkedCandidate.Chain.Id}"
                };
            }
        }

        return new PortableProxyChainHealthCheckResult
        {
            Settings = CopySettings(settings, settings.ActiveChainId, chains, history),
            Status = PortableProxyChainHealthStatus.Failed,
            Message = "所有代理链均不可用"
        };
    }

    private async Task<CheckedChain> CheckChainAsync(
        PortableProxyChainConfig chain,
        CancellationToken cancellationToken)
    {
        var start = _clock();
        var nodes = new List<PortableProxyNode>();
        var successfulNodes = 0;
        double totalLatency = 0;

        foreach (var node in chain.Nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!node.Enabled)
            {
                nodes.Add(CopyNode(node));
                continue;
            }

            var result = await _tester.TestAsync(node, cancellationToken).ConfigureAwait(false);
            nodes.Add(CopyNode(node, result.IsAvailable, result.Latency));
            if (result.IsAvailable)
            {
                successfulNodes++;
                totalLatency += result.Latency;
            }
        }

        var checkedChain = CopyChain(chain, nodes);
        var history = new PortableProxyChainHistory
        {
            ChainId = chain.Id,
            ChainName = chain.Name,
            StartTime = start,
            EndTime = _clock(),
            Success = successfulNodes > 0,
            TotalNodes = chain.Nodes.Count(node => node.Enabled),
            SuccessfulNodes = successfulNodes,
            AverageLatency = successfulNodes > 0 ? totalLatency / successfulNodes : 0
        };
        return new CheckedChain(checkedChain, history);
    }

    private static PortableProxyChainSettings CopySettings(
        PortableProxyChainSettings settings,
        string activeChainId,
        IReadOnlyList<PortableProxyChainConfig> chains,
        IReadOnlyList<PortableProxyChainHistory> history)
    {
        return new PortableProxyChainSettings
        {
            ActiveChainId = activeChainId,
            Chains = chains,
            History = history,
            Performance = settings.Performance
        };
    }

    private static PortableProxyChainConfig CopyChain(
        PortableProxyChainConfig chain,
        IReadOnlyList<PortableProxyNode> nodes)
    {
        return new PortableProxyChainConfig
        {
            Id = chain.Id,
            Name = chain.Name,
            Strategy = chain.Strategy,
            AutoFailover = chain.AutoFailover,
            HealthCheckInterval = chain.HealthCheckInterval,
            Enabled = chain.Enabled,
            Nodes = nodes
        };
    }

    private static PortableProxyNode CopyNode(
        PortableProxyNode node,
        bool? isAvailable = null,
        int? latency = null)
    {
        return new PortableProxyNode
        {
            Id = node.Id,
            Name = node.Name,
            Config = node.Config,
            Order = node.Order,
            Enabled = node.Enabled,
            Latency = latency ?? node.Latency,
            IsAvailable = isAvailable ?? node.IsAvailable
        };
    }

    private sealed record CheckedChain(PortableProxyChainConfig Chain, PortableProxyChainHistory History);
}

public static class PortableProxyChainAnalyzer
{
    public static IReadOnlyList<PortableProxyChainPerformance> AnalyzePerformance(PortableProxyChainSettings settings)
    {
        var performance = new List<PortableProxyChainPerformance>();
        foreach (var chain in settings.Chains)
        {
            var chainHistory = settings.History
                .Where(history => string.Equals(history.ChainId, chain.Id, StringComparison.Ordinal))
                .ToList();
            if (chainHistory.Count == 0)
            {
                continue;
            }

            performance.Add(new PortableProxyChainPerformance
            {
                ChainId = chain.Id,
                ChainName = string.IsNullOrWhiteSpace(chain.Name) ? chainHistory[0].ChainName : chain.Name,
                TotalUses = chainHistory.Count,
                SuccessfulUses = chainHistory.Count(history => history.Success),
                FailedUses = chainHistory.Count(history => !history.Success),
                AverageTotalLatency = chainHistory.Average(history => history.AverageLatency),
                FirstUsed = chainHistory.Min(history => history.StartTime),
                LastUsed = chainHistory.Max(history => history.StartTime)
            });
        }

        return performance;
    }

    public static PortableProxyChainComparison Compare(
        IReadOnlyList<PortableProxyChainPerformance> performance,
        DateTime? comparisonTime = null)
    {
        var items = new List<PortableProxyChainComparisonItem>();
        foreach (var item in performance.OrderByDescending(item => item.SuccessRate))
        {
            var comparisonItem = new PortableProxyChainComparisonItem
            {
                ChainId = item.ChainId,
                ChainName = item.ChainName,
                SuccessRate = item.SuccessRate,
                AverageLatency = item.AverageTotalLatency,
                TotalUses = item.TotalUses,
                Rank = items.Count + 1,
                PerformanceGrade = GetPerformanceGrade(item.SuccessRate, item.AverageTotalLatency)
            };
            items.Add(comparisonItem);
        }

        if (items.Count == 0)
        {
            return new PortableProxyChainComparison
            {
                ComparisonTime = comparisonTime ?? DateTime.Now,
                Items = items
            };
        }

        var best = items[0];
        return new PortableProxyChainComparison
        {
            ComparisonTime = comparisonTime ?? DateTime.Now,
            Items = items,
            BestChainId = best.ChainId,
            BestChainName = best.ChainName,
            Summary = $"最佳代理链: {best.ChainName} (评级: {best.PerformanceGrade})"
        };
    }

    public static PortableProxyChainOptimization Optimize(
        PortableProxyChainConfig chain,
        PortableProxyChainPerformance? performance)
    {
        if (performance is null)
        {
            return new PortableProxyChainOptimization
            {
                ChainId = chain.Id,
                ChainName = chain.Name,
                Suggestions = ["该代理链暂无性能数据"],
                Priority = PortableProxyChainOptimizationPriority.Low,
                Summary = "该代理链暂无性能数据"
            };
        }

        var suggestions = new List<string>();
        var priority = PortableProxyChainOptimizationPriority.Low;
        if (performance.SuccessRate < 80)
        {
            suggestions.Add("成功率较低，建议检查节点配置");
            priority = PortableProxyChainOptimizationPriority.High;
        }

        if (performance.AverageTotalLatency > 500)
        {
            suggestions.Add("延迟较高，建议优化节点顺序或更换节点");
            priority = PortableProxyChainOptimizationPriority.Medium;
        }

        if (suggestions.Count == 0)
        {
            suggestions.Add("代理链运行良好，无需优化");
            priority = PortableProxyChainOptimizationPriority.Low;
        }

        return new PortableProxyChainOptimization
        {
            ChainId = chain.Id,
            ChainName = chain.Name,
            Suggestions = suggestions,
            Priority = priority,
            Summary = string.Join("; ", suggestions)
        };
    }

    public static PortableProxyChainReport BuildReport(
        PortableProxyChainSettings settings,
        IReadOnlyList<PortableProxyChainOptimization>? optimizations = null,
        DateTime? generatedTime = null)
    {
        var time = generatedTime ?? DateTime.Now;
        var performance = AnalyzePerformance(settings);
        var comparison = Compare(performance, time);
        var reportOptimizations = optimizations ?? settings.Chains
            .Select(chain => Optimize(chain, performance.FirstOrDefault(item => item.ChainId == chain.Id)))
            .ToList();

        return new PortableProxyChainReport
        {
            GeneratedTime = time,
            TotalChains = settings.Chains.Count,
            ActiveChains = settings.Chains.Count(chain => chain.Enabled),
            TopPerformers = performance.OrderByDescending(item => item.SuccessRate).Take(3).ToList(),
            PoorPerformers = performance.OrderBy(item => item.SuccessRate).Take(3).ToList(),
            Comparison = comparison,
            Optimizations = reportOptimizations,
            Summary = $"代理链报告 - 生成于 {time:yyyy-MM-dd HH:mm:ss}",
            HealthScore = CalculateHealthScore(performance)
        };
    }

    private static string GetPerformanceGrade(double successRate, double averageLatency)
    {
        if (successRate > 90 && averageLatency < 200)
        {
            return "A";
        }

        if (successRate > 75 && averageLatency < 500)
        {
            return "B";
        }

        return successRate > 50 ? "C" : "D";
    }

    private static int CalculateHealthScore(IReadOnlyList<PortableProxyChainPerformance> performance)
    {
        if (performance.Count == 0)
        {
            return 50;
        }

        var score = 0;
        foreach (var item in performance)
        {
            if (item.SuccessRate > 90)
            {
                score += 30;
            }
            else if (item.SuccessRate > 75)
            {
                score += 20;
            }
            else if (item.SuccessRate > 50)
            {
                score += 10;
            }

            if (item.AverageTotalLatency < 200)
            {
                score += 20;
            }
            else if (item.AverageTotalLatency < 500)
            {
                score += 10;
            }
        }

        return Math.Min(100, score / Math.Max(1, performance.Count));
    }
}
