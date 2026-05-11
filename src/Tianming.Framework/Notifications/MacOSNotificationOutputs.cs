using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;

namespace TM.Framework.Notifications;

public sealed class MacOSNotificationSink : IPortableNotificationSink
{
    private const string OsascriptToolPath = "/usr/bin/osascript";

    private readonly IMacOSNotificationCommandRunner _runner;

    public MacOSNotificationSink()
        : this(new ProcessMacOSNotificationCommandRunner())
    {
    }

    public MacOSNotificationSink(IMacOSNotificationCommandRunner runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    public Task DeliverAsync(PortableNotificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var script = BuildDisplayNotificationScript(request);
        var result = _runner.Run(OsascriptToolPath, ["-e", script]);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(BuildCommandError("deliver macOS notification", result));
        }

        return Task.CompletedTask;
    }

    private static string BuildDisplayNotificationScript(PortableNotificationRequest request)
    {
        return "display notification "
            + Quote(request.Message)
            + " with title "
            + Quote(request.Title)
            + " subtitle "
            + Quote(PortableNotificationDispatcher.ToHistoryType(request.Type));
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static string BuildCommandError(string action, MacOSNotificationCommandResult result)
    {
        var message = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
        return $"Failed to {action}: {message}".Trim();
    }
}

public sealed class MacOSNotificationSoundOutput : IPortableSoundOutput
{
    private const string AfplayToolPath = "/usr/bin/afplay";

    private readonly IMacOSNotificationCommandRunner _runner;

    public MacOSNotificationSoundOutput()
        : this(new ProcessMacOSNotificationCommandRunner())
    {
    }

    public MacOSNotificationSoundOutput(IMacOSNotificationCommandRunner runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    public Task PlayFileAsync(string filePath, double volume, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Sound file path cannot be empty.", nameof(filePath));
        }

        cancellationToken.ThrowIfCancellationRequested();
        RunAfplay(filePath, volume);
        return Task.CompletedTask;
    }

    public Task PlaySystemSoundAsync(
        PortableSystemSound systemSound,
        double volume,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RunAfplay(GetMacOSSystemSoundPath(systemSound), volume);
        return Task.CompletedTask;
    }

    private void RunAfplay(string filePath, double volume)
    {
        var result = _runner.Run(AfplayToolPath, ["-v", FormatVolume(volume), filePath]);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(BuildCommandError("play macOS notification sound", result));
        }
    }

    private static string GetMacOSSystemSoundPath(PortableSystemSound systemSound)
    {
        return systemSound switch
        {
            PortableSystemSound.Asterisk => "/System/Library/Sounds/Ping.aiff",
            PortableSystemSound.Exclamation => "/System/Library/Sounds/Hero.aiff",
            PortableSystemSound.Hand => "/System/Library/Sounds/Basso.aiff",
            _ => "/System/Library/Sounds/Glass.aiff"
        };
    }

    private static string FormatVolume(double volume)
    {
        return Math.Clamp(volume, 0, 1).ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string BuildCommandError(string action, MacOSNotificationCommandResult result)
    {
        var message = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
        return $"Failed to {action}: {message}".Trim();
    }
}

public sealed class MacOSSpeechOutput : IPortableSpeechOutput
{
    private const string SayToolPath = "/usr/bin/say";

    private readonly IMacOSNotificationCommandRunner _runner;
    private readonly string? _voiceName;

    public MacOSSpeechOutput()
        : this(new ProcessMacOSNotificationCommandRunner())
    {
    }

    public MacOSSpeechOutput(IMacOSNotificationCommandRunner runner, string? voiceName = null)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _voiceName = string.IsNullOrWhiteSpace(voiceName) ? null : voiceName.Trim();
    }

    public Task SpeakAsync(
        string text,
        int speed,
        int volume,
        int pitch,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.CompletedTask;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var arguments = new List<string>();
        if (_voiceName is not null)
        {
            arguments.Add("-v");
            arguments.Add(_voiceName);
        }

        arguments.Add("-r");
        arguments.Add(ToMacOSRate(speed).ToString(CultureInfo.InvariantCulture));
        arguments.Add(text);

        var result = _runner.Run(SayToolPath, arguments);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(BuildCommandError("speak macOS notification text", result));
        }

        return Task.CompletedTask;
    }

    private static int ToMacOSRate(int speed)
    {
        return Math.Clamp(200 + (speed * 30), 80, 360);
    }

    private static string BuildCommandError(string action, MacOSNotificationCommandResult result)
    {
        var message = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
        return $"Failed to {action}: {message}".Trim();
    }
}

public sealed record MacOSSpeechVoice(string Name, string Culture, string SampleText);

public sealed class MacOSSpeechVoiceCatalog
{
    private const string SayToolPath = "/usr/bin/say";
    private static readonly Regex VoiceLineRegex = new(
        @"^(?<name>.+?)\s{2,}(?<culture>[A-Za-z]{2}_[A-Za-z]{2})\s+#\s*(?<sample>.*)$",
        RegexOptions.Compiled);

    private readonly IMacOSNotificationCommandRunner _runner;

    public MacOSSpeechVoiceCatalog()
        : this(new ProcessMacOSNotificationCommandRunner())
    {
    }

    public MacOSSpeechVoiceCatalog(IMacOSNotificationCommandRunner runner)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
    }

    public IReadOnlyList<MacOSSpeechVoice> GetVoices()
    {
        var result = _runner.Run(SayToolPath, ["-v", "?"]);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(BuildCommandError("list macOS speech voices", result));
        }

        return ParseVoices(result.StandardOutput);
    }

    public static IReadOnlyList<MacOSSpeechVoice> ParseVoices(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return [];
        }

        var voices = new List<MacOSSpeechVoice>();
        using var reader = new StringReader(output);
        while (reader.ReadLine() is { } line)
        {
            var match = VoiceLineRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            voices.Add(new MacOSSpeechVoice(
                match.Groups["name"].Value.Trim(),
                match.Groups["culture"].Value.Trim(),
                match.Groups["sample"].Value.Trim()));
        }

        return voices;
    }

    private static string BuildCommandError(string action, MacOSNotificationCommandResult result)
    {
        var message = string.IsNullOrWhiteSpace(result.StandardError)
            ? result.StandardOutput
            : result.StandardError;
        return $"Failed to {action}: {message}".Trim();
    }
}

public interface IMacOSNotificationCommandRunner
{
    MacOSNotificationCommandResult Run(string fileName, IReadOnlyList<string> arguments);
}

public sealed record MacOSNotificationCommandResult(int ExitCode, string StandardOutput, string StandardError);

public sealed record MacOSNotificationCommandInvocation(string FileName, IReadOnlyList<string> Arguments);

public sealed class ProcessMacOSNotificationCommandRunner : IMacOSNotificationCommandRunner
{
    public MacOSNotificationCommandResult Run(string fileName, IReadOnlyList<string> arguments)
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
        return new MacOSNotificationCommandResult(process.ExitCode, output, error);
    }
}
