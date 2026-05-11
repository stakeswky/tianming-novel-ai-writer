using System.Diagnostics;

namespace TM.Framework.Security;

public sealed class PortableOAuthBrowserOpenResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class PortableOAuthBrowserLauncher
{
    private const string MacOSOpenToolPath = "/usr/bin/open";

    private readonly IPortableBrowserCommandRunner _runner;

    public PortableOAuthBrowserLauncher()
        : this(new ProcessPortableBrowserCommandRunner())
    {
    }

    public PortableOAuthBrowserLauncher(IPortableBrowserCommandRunner runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    public PortableOAuthBrowserOpenResult OpenAuthorizationUrl(string authorizationUrl)
    {
        if (!IsHttpUrl(authorizationUrl))
        {
            return new PortableOAuthBrowserOpenResult
            {
                Success = false,
                ErrorMessage = "OAuth 授权地址无效"
            };
        }

        var result = _runner.Run(MacOSOpenToolPath, [authorizationUrl]);
        if (result.ExitCode == 0)
        {
            return new PortableOAuthBrowserOpenResult { Success = true };
        }

        return new PortableOAuthBrowserOpenResult
        {
            Success = false,
            ErrorMessage = BuildOpenError(result)
        };
    }

    private static bool IsHttpUrl(string authorizationUrl)
    {
        return Uri.TryCreate(authorizationUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }

    private static string BuildOpenError(PortableBrowserCommandResult result)
    {
        var message = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
        return $"无法打开 OAuth 授权浏览器: {message}".Trim();
    }
}

public interface IPortableBrowserCommandRunner
{
    PortableBrowserCommandResult Run(string fileName, IReadOnlyList<string> arguments);
}

public sealed record PortableBrowserCommandResult(int ExitCode, string StandardOutput, string StandardError);

public sealed record PortableBrowserCommandInvocation(string FileName, IReadOnlyList<string> Arguments);

public sealed class ProcessPortableBrowserCommandRunner : IPortableBrowserCommandRunner
{
    public PortableBrowserCommandResult Run(string fileName, IReadOnlyList<string> arguments)
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
        return new PortableBrowserCommandResult(process.ExitCode, output, error);
    }
}
