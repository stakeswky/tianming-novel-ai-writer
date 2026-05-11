using System.Diagnostics;
using System.Net.Sockets;
using System.Text;

namespace TM.Framework.Logging;

public sealed class PortableFileLogOutputSink : IPortableLogOutputSink
{
    private readonly string _storageRoot;

    public PortableFileLogOutputSink(string storageRoot)
    {
        _storageRoot = string.IsNullOrWhiteSpace(storageRoot)
            ? Directory.GetCurrentDirectory()
            : storageRoot;
    }

    public PortableLogOutputTargetType TargetType => PortableLogOutputTargetType.File;

    public async Task WriteAsync(
        PortableLogOutputTarget target,
        string content,
        CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(target);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.AppendAllTextAsync(path, content + Environment.NewLine, cancellationToken)
            .ConfigureAwait(false);
    }

    private string ResolvePath(PortableLogOutputTarget target)
    {
        var configuredPath = GetSetting(target, "Path")
            ?? GetSetting(target, "FilePath")
            ?? "Logs/application.log";
        configuredPath = configuredPath.Trim();

        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        var root = Path.GetFullPath(_storageRoot);
        var resolved = Path.GetFullPath(Path.Combine(root, configuredPath));
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!resolved.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, comparison)
            && !string.Equals(resolved, root, comparison))
        {
            throw new InvalidOperationException("Log output path must stay under the storage root.");
        }

        return resolved;
    }

    private static string? GetSetting(PortableLogOutputTarget target, string key)
    {
        return target.Settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }
}

public sealed class PortableTextWriterLogOutputSink : IPortableLogOutputSink
{
    private readonly TextWriter _writer;

    public PortableTextWriterLogOutputSink(TextWriter writer)
    {
        _writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    public PortableLogOutputTargetType TargetType => PortableLogOutputTargetType.Console;

    public async Task WriteAsync(
        PortableLogOutputTarget target,
        string content,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _writer.WriteLineAsync(content.AsMemory(), cancellationToken).ConfigureAwait(false);
    }
}

public sealed class PortableHttpLogOutputSink : IPortableLogOutputSink
{
    private readonly HttpClient _httpClient;

    public PortableHttpLogOutputSink(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public PortableLogOutputTargetType TargetType => PortableLogOutputTargetType.RemoteHttp;

    public async Task WriteAsync(
        PortableLogOutputTarget target,
        string content,
        CancellationToken cancellationToken = default)
    {
        var address = GetSetting(target, "Address")
            ?? GetSetting(target, "Url")
            ?? GetSetting(target, "RemoteAddress")
            ?? throw new InvalidOperationException("HTTP log output address is required.");

        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("HTTP log output address must be an absolute HTTP or HTTPS URL.");
        }

        var contentType = GetSetting(target, "ContentType") ?? "text/plain";
        using var requestContent = new StringContent(content, Encoding.UTF8, contentType);
        using var response = await _httpClient.PostAsync(uri, requestContent, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"HTTP log output failed with status {(int)response.StatusCode}.");
        }
    }

    private static string? GetSetting(PortableLogOutputTarget target, string key)
    {
        return target.Settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }
}

public sealed class PortableTcpLogOutputSink : IPortableLogOutputSink
{
    public PortableLogOutputTargetType TargetType => PortableLogOutputTargetType.RemoteTcp;

    public async Task WriteAsync(
        PortableLogOutputTarget target,
        string content,
        CancellationToken cancellationToken = default)
    {
        var (host, port) = ResolveEndpoint(target);
        using var client = new TcpClient();
        await client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);

        var bytes = Encoding.UTF8.GetBytes(content);
        await client.GetStream().WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private static (string Host, int Port) ResolveEndpoint(PortableLogOutputTarget target)
    {
        var address = GetSetting(target, "Address")
            ?? GetSetting(target, "RemoteAddress")
            ?? throw new InvalidOperationException("TCP log output address is required.");

        if (!address.Contains("://", StringComparison.Ordinal))
        {
            address = "tcp://" + address;
        }

        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)
            || !string.Equals(uri.Scheme, "tcp", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new InvalidOperationException("TCP log output address must be tcp://host:port or host:port.");
        }

        return (uri.Host, uri.Port > 0 ? uri.Port : 514);
    }

    private static string? GetSetting(PortableLogOutputTarget target, string key)
    {
        return target.Settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }
}

public sealed class MacOSLoggerLogOutputSink : IPortableLogOutputSink
{
    private const string LoggerToolPath = "/usr/bin/logger";

    private readonly IPortableLogCommandRunner _runner;

    public MacOSLoggerLogOutputSink()
        : this(new ProcessPortableLogCommandRunner())
    {
    }

    public MacOSLoggerLogOutputSink(IPortableLogCommandRunner runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    public PortableLogOutputTargetType TargetType => PortableLogOutputTargetType.EventLog;

    public Task WriteAsync(
        PortableLogOutputTarget target,
        string content,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tag = GetSetting(target, "Tag")
            ?? GetSetting(target, "Source")
            ?? GetSetting(target, "EventLogSource")
            ?? target.Name;

        if (string.IsNullOrWhiteSpace(tag))
        {
            tag = "TianmingWriter";
        }

        var result = _runner.Run(LoggerToolPath, ["-t", tag.Trim(), "--", content]);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(BuildCommandError(result));
        }

        return Task.CompletedTask;
    }

    private static string? GetSetting(PortableLogOutputTarget target, string key)
    {
        return target.Settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    private static string BuildCommandError(PortableLogCommandResult result)
    {
        var message = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
        return $"Failed to write macOS event log: {message}".Trim();
    }
}

public interface IPortableLogCommandRunner
{
    PortableLogCommandResult Run(string fileName, IReadOnlyList<string> arguments);
}

public sealed record PortableLogCommandResult(int ExitCode, string StandardOutput, string StandardError);

public sealed record PortableLogCommandInvocation(string FileName, IReadOnlyList<string> Arguments);

public sealed class ProcessPortableLogCommandRunner : IPortableLogCommandRunner
{
    public PortableLogCommandResult Run(string fileName, IReadOnlyList<string> arguments)
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
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new PortableLogCommandResult(process.ExitCode, output, error);
    }
}
