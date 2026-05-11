using TM.Framework.Logging;
using Xunit;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;

namespace Tianming.Framework.Tests;

public class PortableLogOutputPipelineTests
{
    [Fact]
    public async Task DispatchAsync_writes_enabled_targets_by_priority_and_records_failures()
    {
        var targets = new[]
        {
            new PortableLogOutputTarget
            {
                Name = "file",
                Type = PortableLogOutputTargetType.File,
                IsEnabled = true,
                Priority = 20
            },
            new PortableLogOutputTarget
            {
                Name = "console",
                Type = PortableLogOutputTargetType.Console,
                IsEnabled = true,
                Priority = 5
            },
            new PortableLogOutputTarget
            {
                Name = "remote",
                Type = PortableLogOutputTargetType.RemoteHttp,
                IsEnabled = false,
                Priority = 1
            }
        };
        var consoleSink = new RecordingLogOutputSink(PortableLogOutputTargetType.Console);
        var fileSink = new RecordingLogOutputSink(
            PortableLogOutputTargetType.File,
            _ => throw new InvalidOperationException("disk full"));
        var dispatcher = new PortableLogOutputDispatcher([fileSink, consoleSink]);

        var result = await dispatcher.DispatchAsync(targets, "line");

        Assert.False(result.AllSucceeded);
        Assert.Equal(["console", "file"], result.Attempts.Select(attempt => attempt.TargetName));
        Assert.Equal(["console:line"], consoleSink.Writes);
        Assert.Empty(fileSink.Writes);
        var failure = Assert.Single(result.Failures);
        Assert.Equal("file", failure.TargetName);
        Assert.Equal("disk full", failure.ErrorMessage);
        Assert.Equal("line", failure.LogContent);
    }

    [Fact]
    public async Task AsyncQueue_flushes_high_priority_before_low_priority_and_reports_drops()
    {
        var sink = new RecordingLogOutputSink(PortableLogOutputTargetType.Console);
        var queue = new PortableAsyncLogOutputQueue(
            new PortableAsyncLogOutputQueueOptions
            {
                LowPriorityBufferSize = 1,
                Clock = () => new DateTime(2026, 5, 10, 9, 30, 0)
            },
            new PortableLogOutputDispatcher([sink]));
        var targets = new[]
        {
            new PortableLogOutputTarget
            {
                Name = "console",
                Type = PortableLogOutputTargetType.Console,
                IsEnabled = true
            }
        };

        Assert.True(queue.Enqueue(LogSeverity.Debug, "low-1"));
        Assert.False(queue.Enqueue(LogSeverity.Debug, "low-2"));
        Assert.True(queue.Enqueue(LogSeverity.Error, "high-1"));

        var result = await queue.FlushAsync(targets);

        Assert.True(result.AllSucceeded);
        Assert.Equal(1, result.DroppedLowPriorityCount);
        Assert.Equal("console:high-1", sink.Writes[0]);
        Assert.Equal("console:low-1", sink.Writes[1]);
        Assert.Contains("已丢弃 1 条日志", sink.Writes[2]);
    }

    [Fact]
    public async Task AsyncQueue_flushes_each_target_failure_without_stopping_other_targets()
    {
        var goodSink = new RecordingLogOutputSink(PortableLogOutputTargetType.Console);
        var badSink = new RecordingLogOutputSink(
            PortableLogOutputTargetType.File,
            _ => throw new IOException("readonly"));
        var queue = new PortableAsyncLogOutputQueue(
            new PortableAsyncLogOutputQueueOptions(),
            new PortableLogOutputDispatcher([badSink, goodSink]));
        var targets = new[]
        {
            new PortableLogOutputTarget { Name = "file", Type = PortableLogOutputTargetType.File, IsEnabled = true },
            new PortableLogOutputTarget { Name = "console", Type = PortableLogOutputTargetType.Console, IsEnabled = true }
        };

        Assert.True(queue.Enqueue(LogSeverity.Warning, "warn"));

        var result = await queue.FlushAsync(targets);

        Assert.False(result.AllSucceeded);
        Assert.Equal(["console:warn"], goodSink.Writes);
        var failure = Assert.Single(result.Failures);
        Assert.Equal("file", failure.TargetName);
        Assert.Equal("readonly", failure.ErrorMessage);
    }

    [Fact]
    public async Task FileSink_writes_relative_target_path_under_storage_root()
    {
        using var workspace = new TempDirectory();
        var sink = new PortableFileLogOutputSink(workspace.Path);
        var target = new PortableLogOutputTarget
        {
            Name = "daily-file",
            Type = PortableLogOutputTargetType.File,
            Settings = { ["Path"] = "Logs/current.log" }
        };

        await sink.WriteAsync(target, "first");
        await sink.WriteAsync(target, "second");

        var content = await File.ReadAllTextAsync(Path.Combine(workspace.Path, "Logs", "current.log"));
        Assert.Equal($"first{Environment.NewLine}second{Environment.NewLine}", content);
    }

    [Fact]
    public async Task TextWriterSink_writes_console_lines_to_injected_writer()
    {
        await using var writer = new StringWriter();
        var sink = new PortableTextWriterLogOutputSink(writer);
        var target = new PortableLogOutputTarget
        {
            Name = "console",
            Type = PortableLogOutputTargetType.Console
        };

        await sink.WriteAsync(target, "hello");

        Assert.Equal($"hello{Environment.NewLine}", writer.ToString());
    }

    [Fact]
    public async Task Dispatcher_can_write_to_real_file_and_console_sinks()
    {
        using var workspace = new TempDirectory();
        await using var writer = new StringWriter();
        var dispatcher = new PortableLogOutputDispatcher(
            [
                new PortableFileLogOutputSink(workspace.Path),
                new PortableTextWriterLogOutputSink(writer)
            ]);
        var targets = new[]
        {
            new PortableLogOutputTarget
            {
                Name = "file",
                Type = PortableLogOutputTargetType.File,
                Priority = 10,
                Settings = { ["Path"] = "Logs/app.log" }
            },
            new PortableLogOutputTarget
            {
                Name = "console",
                Type = PortableLogOutputTargetType.Console,
                Priority = 20
            }
        };

        var result = await dispatcher.DispatchAsync(targets, "line");

        Assert.True(result.AllSucceeded);
        Assert.Equal($"line{Environment.NewLine}", writer.ToString());
        Assert.Equal($"line{Environment.NewLine}", await File.ReadAllTextAsync(Path.Combine(workspace.Path, "Logs", "app.log")));
    }

    [Fact]
    public async Task HttpSink_posts_log_content_to_configured_endpoint()
    {
        var handler = new CapturingHttpMessageHandler();
        var sink = new PortableHttpLogOutputSink(new HttpClient(handler));
        var target = new PortableLogOutputTarget
        {
            Name = "http",
            Type = PortableLogOutputTargetType.RemoteHttp,
            Settings =
            {
                ["Address"] = "https://example.test/logs",
                ["ContentType"] = "application/json"
            }
        };

        await sink.WriteAsync(target, """{"message":"hello"}""");

        Assert.NotNull(handler.Request);
        Assert.Equal(HttpMethod.Post, handler.Request.Method);
        Assert.Equal("https://example.test/logs", handler.Request.RequestUri?.ToString());
        Assert.Equal("application/json", handler.ContentType);
        Assert.Equal("""{"message":"hello"}""", handler.Body);
    }

    [Fact]
    public async Task TcpSink_writes_utf8_payload_to_configured_endpoint()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var acceptTask = listener.AcceptTcpClientAsync();
        var sink = new PortableTcpLogOutputSink();
        var target = new PortableLogOutputTarget
        {
            Name = "tcp",
            Type = PortableLogOutputTargetType.RemoteTcp,
            Settings = { ["Address"] = $"tcp://127.0.0.1:{port}" }
        };

        await sink.WriteAsync(target, "tcp-line");
        using var accepted = await acceptTask.WaitAsync(TimeSpan.FromSeconds(5));
        var buffer = new byte[64];
        var count = await accepted.GetStream().ReadAsync(buffer).AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("tcp-line", Encoding.UTF8.GetString(buffer, 0, count));
    }

    [Fact]
    public async Task MacOSLoggerSink_writes_logger_command_with_target_tag_and_content()
    {
        var runner = new RecordingLoggerCommandRunner(new PortableLogCommandResult(0, "", ""));
        var sink = new MacOSLoggerLogOutputSink(runner);
        var target = new PortableLogOutputTarget
        {
            Name = "event",
            Type = PortableLogOutputTargetType.EventLog,
            Settings = { ["Tag"] = "TianmingWriter" }
        };

        await sink.WriteAsync(target, "story generated");

        var invocation = Assert.Single(runner.Invocations);
        Assert.Equal("/usr/bin/logger", invocation.FileName);
        Assert.Equal(["-t", "TianmingWriter", "--", "story generated"], invocation.Arguments);
    }

    [Fact]
    public async Task MacOSLoggerSink_surfaces_logger_command_failure()
    {
        var runner = new RecordingLoggerCommandRunner(new PortableLogCommandResult(1, "", "logger denied"));
        var sink = new MacOSLoggerLogOutputSink(runner);
        var target = new PortableLogOutputTarget
        {
            Name = "event",
            Type = PortableLogOutputTargetType.EventLog
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sink.WriteAsync(target, "story generated"));

        Assert.Contains("logger denied", ex.Message);
    }

    private sealed class RecordingLogOutputSink : IPortableLogOutputSink
    {
        private readonly Func<string, Task>? _writeAsync;

        public RecordingLogOutputSink(
            PortableLogOutputTargetType targetType,
            Func<string, Task>? writeAsync = null)
        {
            TargetType = targetType;
            _writeAsync = writeAsync;
        }

        public PortableLogOutputTargetType TargetType { get; }

        public List<string> Writes { get; } = new();

        public async Task WriteAsync(
            PortableLogOutputTarget target,
            string content,
            CancellationToken cancellationToken = default)
        {
            if (_writeAsync is not null)
            {
                await _writeAsync(content);
                return;
            }

            Writes.Add($"{target.Name}:{content}");
        }
    }

    private sealed class CapturingHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        public string? ContentType { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            ContentType = request.Content?.Headers.ContentType?.MediaType;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class RecordingLoggerCommandRunner : IPortableLogCommandRunner
    {
        private readonly PortableLogCommandResult _result;

        public RecordingLoggerCommandRunner(PortableLogCommandResult result)
        {
            _result = result;
        }

        public List<PortableLogCommandInvocation> Invocations { get; } = new();

        public PortableLogCommandResult Run(string fileName, IReadOnlyList<string> arguments)
        {
            Invocations.Add(new PortableLogCommandInvocation(fileName, arguments.ToArray()));
            return _result;
        }
    }
}
