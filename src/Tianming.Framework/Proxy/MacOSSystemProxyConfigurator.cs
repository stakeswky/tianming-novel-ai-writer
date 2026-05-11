using System.Diagnostics;

namespace TM.Framework.Proxy;

public sealed class MacOSSystemProxyOperationResult
{
    public bool Success { get; init; }

    public List<string> Errors { get; init; } = [];
}

public sealed class MacOSSystemProxyConfigurator
{
    private const string NetworkSetupToolPath = "/usr/sbin/networksetup";

    private readonly IMacOSSystemProxyCommandRunner _runner;

    public MacOSSystemProxyConfigurator()
        : this(new ProcessMacOSSystemProxyCommandRunner())
    {
    }

    public MacOSSystemProxyConfigurator(IMacOSSystemProxyCommandRunner runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    public async Task<MacOSSystemProxyOperationResult> ApplyAsync(
        string networkService,
        PortableProxyConfig config,
        CancellationToken cancellationToken = default)
    {
        ValidateNetworkService(networkService);
        if (string.IsNullOrWhiteSpace(config.Server) || config.Port <= 0)
        {
            return new MacOSSystemProxyOperationResult
            {
                Success = false,
                Errors = ["Proxy server and port are required."]
            };
        }

        var operations = new List<IReadOnlyList<string>>();
        switch (config.Type)
        {
            case PortableProxyType.Http:
                operations.Add(["-setwebproxy", networkService, config.Server, config.Port.ToString()]);
                operations.Add(["-setsecurewebproxy", networkService, config.Server, config.Port.ToString()]);
                break;
            case PortableProxyType.Https:
                operations.Add(["-setsecurewebproxy", networkService, config.Server, config.Port.ToString()]);
                break;
            case PortableProxyType.Socks4:
            case PortableProxyType.Socks5:
                operations.Add(["-setsocksfirewallproxy", networkService, config.Server, config.Port.ToString()]);
                break;
            default:
                return new MacOSSystemProxyOperationResult
                {
                    Success = false,
                    Errors = [$"Unsupported proxy type: {config.Type}"]
                };
        }

        var bypassList = config.BypassList
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
        if (bypassList.Length > 0)
        {
            operations.Add(["-setproxybypassdomains", networkService, .. bypassList]);
        }

        return await RunOperationsAsync(operations, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MacOSSystemProxyOperationResult> ApplyPacAsync(
        string networkService,
        Uri pacUrl,
        CancellationToken cancellationToken = default)
    {
        ValidateNetworkService(networkService);
        if (pacUrl.Scheme is not ("http" or "https"))
        {
            return new MacOSSystemProxyOperationResult
            {
                Success = false,
                Errors = ["PAC URL must be HTTP or HTTPS."]
            };
        }

        return await RunOperationsAsync(
            [
                ["-setautoproxyurl", networkService, pacUrl.ToString()],
                ["-setautoproxystate", networkService, "on"]
            ],
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<MacOSSystemProxyOperationResult> DisableAsync(
        string networkService,
        CancellationToken cancellationToken = default)
    {
        ValidateNetworkService(networkService);
        return await RunOperationsAsync(
            [
                ["-setwebproxystate", networkService, "off"],
                ["-setsecurewebproxystate", networkService, "off"],
                ["-setsocksfirewallproxystate", networkService, "off"],
                ["-setautoproxystate", networkService, "off"]
            ],
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<MacOSSystemProxyOperationResult> RunOperationsAsync(
        IReadOnlyList<IReadOnlyList<string>> operations,
        CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        foreach (var operation in operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await _runner.RunAsync(NetworkSetupToolPath, operation, cancellationToken).ConfigureAwait(false);
            if (result.ExitCode != 0)
            {
                var error = string.IsNullOrWhiteSpace(result.StandardError)
                    ? result.StandardOutput
                    : result.StandardError;
                errors.Add(error.Trim());
            }
        }

        return new MacOSSystemProxyOperationResult
        {
            Success = errors.Count == 0,
            Errors = errors
        };
    }

    private static void ValidateNetworkService(string networkService)
    {
        if (string.IsNullOrWhiteSpace(networkService))
        {
            throw new ArgumentException("Network service is required.", nameof(networkService));
        }
    }
}

public interface IMacOSSystemProxyCommandRunner
{
    Task<MacOSSystemProxyCommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);
}

public sealed record MacOSSystemProxyCommandResult(int ExitCode, string StandardOutput, string StandardError);

public sealed class MacOSSystemProxyCommandInvocation : IEquatable<MacOSSystemProxyCommandInvocation>
{
    public MacOSSystemProxyCommandInvocation(string fileName, IReadOnlyList<string> arguments)
    {
        FileName = fileName;
        Arguments = arguments.ToArray();
    }

    public string FileName { get; }

    public IReadOnlyList<string> Arguments { get; }

    public bool Equals(MacOSSystemProxyCommandInvocation? other)
    {
        return other is not null &&
               string.Equals(FileName, other.FileName, StringComparison.Ordinal) &&
               Arguments.SequenceEqual(other.Arguments, StringComparer.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as MacOSSystemProxyCommandInvocation);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(FileName, StringComparer.Ordinal);
        foreach (var argument in Arguments)
        {
            hash.Add(argument, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}

public sealed class ProcessMacOSSystemProxyCommandRunner : IMacOSSystemProxyCommandRunner
{
    public async Task<MacOSSystemProxyCommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start command: {fileName}");
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return new MacOSSystemProxyCommandResult(process.ExitCode, output, error);
    }
}
