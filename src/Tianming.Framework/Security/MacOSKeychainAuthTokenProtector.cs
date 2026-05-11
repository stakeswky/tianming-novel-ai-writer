using System.Diagnostics;

namespace TM.Framework.Security;

public sealed class MacOSKeychainAuthTokenProtector : IPortableAuthTokenProtector
{
    private const string SecurityToolPath = "/usr/bin/security";
    private const string MarkerPrefix = "keychain:";

    private readonly IPortableKeychainCommandRunner _runner;
    private readonly string _serviceName;
    private readonly string _accountName;

    public MacOSKeychainAuthTokenProtector()
        : this(new ProcessPortableKeychainCommandRunner(), "tianming-novel-ai-writer.auth-token", "auth-token")
    {
    }

    public MacOSKeychainAuthTokenProtector(
        IPortableKeychainCommandRunner runner,
        string serviceName = "tianming-novel-ai-writer.auth-token",
        string accountName = "auth-token")
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _serviceName = RequireValue(serviceName, nameof(serviceName));
        _accountName = RequireValue(accountName, nameof(accountName));
    }

    public string Protect(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var payload = Convert.ToBase64String(data);
        var result = _runner.Run(SecurityToolPath,
        [
            "add-generic-password",
            "-s", _serviceName,
            "-a", _accountName,
            "-w", payload,
            "-U"
        ]);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(BuildCommandError("save auth token", result));
        }

        return MarkerPrefix + _accountName;
    }

    public byte[] Unprotect(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new InvalidOperationException("Auth token payload is empty.");
        }

        var account = payload.StartsWith(MarkerPrefix, StringComparison.Ordinal)
            ? payload[MarkerPrefix.Length..]
            : _accountName;
        account = RequireValue(account, nameof(payload));

        var result = _runner.Run(SecurityToolPath,
        [
            "find-generic-password",
            "-s", _serviceName,
            "-a", account,
            "-w"
        ]);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(BuildCommandError("read auth token", result));
        }

        return Convert.FromBase64String(result.StandardOutput.TrimEnd('\r', '\n'));
    }

    public void Delete()
    {
        _runner.Run(SecurityToolPath,
        [
            "delete-generic-password",
            "-s", _serviceName,
            "-a", _accountName
        ]);
    }

    private static string RequireValue(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty.", name);
        }

        return value;
    }

    private static string BuildCommandError(string action, PortableKeychainCommandResult result)
    {
        var message = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
        return $"Failed to {action} in macOS Keychain: {message}".Trim();
    }
}

public interface IPortableKeychainCommandRunner
{
    PortableKeychainCommandResult Run(string fileName, IReadOnlyList<string> arguments);
}

public sealed record PortableKeychainCommandResult(int ExitCode, string StandardOutput, string StandardError);

public sealed record PortableKeychainCommandInvocation(string FileName, IReadOnlyList<string> Arguments);

public sealed class ProcessPortableKeychainCommandRunner : IPortableKeychainCommandRunner
{
    public PortableKeychainCommandResult Run(string fileName, IReadOnlyList<string> arguments)
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
        return new PortableKeychainCommandResult(process.ExitCode, output, error);
    }
}
