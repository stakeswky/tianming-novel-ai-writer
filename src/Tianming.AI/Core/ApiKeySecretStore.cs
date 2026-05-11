using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace TM.Services.Framework.AI.Core;

public interface IApiKeySecretStore
{
    string? GetSecret(string configId);
    void SaveSecret(string configId, string apiKey);
    void DeleteSecret(string configId);
}

public sealed class MacOSKeychainApiKeySecretStore : IApiKeySecretStore
{
    private const string SecurityToolPath = "/usr/bin/security";

    private readonly ISecurityCommandRunner _runner;
    private readonly string _serviceName;

    public MacOSKeychainApiKeySecretStore()
        : this(new ProcessSecurityCommandRunner(), "tianming-novel-ai-writer")
    {
    }

    public MacOSKeychainApiKeySecretStore(ISecurityCommandRunner runner, string serviceName = "tianming-novel-ai-writer")
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new ArgumentException("Keychain service name cannot be empty.", nameof(serviceName));

        _serviceName = serviceName;
    }

    public string? GetSecret(string configId)
    {
        if (string.IsNullOrWhiteSpace(configId))
            return null;

        var result = _runner.Run(SecurityToolPath,
        [
            "find-generic-password",
            "-s", _serviceName,
            "-a", configId,
            "-w"
        ]);

        return result.ExitCode == 0 ? result.StandardOutput.TrimEnd('\r', '\n') : null;
    }

    public void SaveSecret(string configId, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(configId) || string.IsNullOrWhiteSpace(apiKey))
            return;

        var result = _runner.Run(SecurityToolPath,
        [
            "add-generic-password",
            "-s", _serviceName,
            "-a", configId,
            "-w", apiKey,
            "-U"
        ]);

        if (result.ExitCode != 0)
            throw new InvalidOperationException(BuildCommandError("save API key", result));
    }

    public void DeleteSecret(string configId)
    {
        if (string.IsNullOrWhiteSpace(configId))
            return;

        _runner.Run(SecurityToolPath,
        [
            "delete-generic-password",
            "-s", _serviceName,
            "-a", configId
        ]);
    }

    private static string BuildCommandError(string action, SecurityCommandResult result)
    {
        var message = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
        return $"Failed to {action} in macOS Keychain: {message}".Trim();
    }
}

public interface ISecurityCommandRunner
{
    SecurityCommandResult Run(string fileName, IReadOnlyList<string> arguments);
}

public sealed record SecurityCommandResult(int ExitCode, string StandardOutput, string StandardError);

public sealed class ProcessSecurityCommandRunner : ISecurityCommandRunner
{
    public SecurityCommandResult Run(string fileName, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start command: {fileName}");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new SecurityCommandResult(process.ExitCode, output, error);
    }
}
