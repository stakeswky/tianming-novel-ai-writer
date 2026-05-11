using TM.Framework.Notifications;
using Xunit;

namespace Tianming.Framework.Tests;

public class MacOSNotificationOutputTests
{
    [Fact]
    public async Task DeliverAsync_uses_osascript_display_notification_with_escaped_text()
    {
        var runner = new RecordingMacOSCommandRunner();
        var sink = new MacOSNotificationSink(runner);

        await sink.DeliverAsync(new PortableNotificationRequest
        {
            Title = "生成 \"完成\"",
            Message = "章节路径 C:\\book\\chapter.md",
            Type = PortableNotificationType.Success
        });

        var invocation = Assert.Single(runner.Invocations);
        Assert.Equal("/usr/bin/osascript", invocation.FileName);
        Assert.Equal("-e", invocation.Arguments[0]);
        Assert.Contains("display notification \"章节路径 C:\\\\book\\\\chapter.md\"", invocation.Arguments[1]);
        Assert.Contains("with title \"生成 \\\"完成\\\"\"", invocation.Arguments[1]);
        Assert.Contains("subtitle \"成功\"", invocation.Arguments[1]);
    }

    [Fact]
    public async Task DeliverAsync_surfaces_command_failure()
    {
        var runner = new RecordingMacOSCommandRunner(
            new MacOSNotificationCommandResult(1, "", "notifications denied"));
        var sink = new MacOSNotificationSink(runner);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sink.DeliverAsync(new PortableNotificationRequest
            {
                Title = "失败",
                Message = "无法投递"
            }));

        Assert.Contains("notifications denied", ex.Message);
    }

    [Fact]
    public async Task PlayFileAsync_uses_afplay_with_clamped_volume()
    {
        using var workspace = new TempDirectory();
        var filePath = Path.Combine(workspace.Path, "ding.aiff");
        await File.WriteAllTextAsync(filePath, "fake sound");
        var runner = new RecordingMacOSCommandRunner();
        var output = new MacOSNotificationSoundOutput(runner);

        await output.PlayFileAsync(filePath, 1.8);

        var invocation = Assert.Single(runner.Invocations);
        Assert.Equal("/usr/bin/afplay", invocation.FileName);
        Assert.Equal(["-v", "1", filePath], invocation.Arguments);
    }

    [Theory]
    [InlineData(PortableSystemSound.Beep, "/System/Library/Sounds/Glass.aiff")]
    [InlineData(PortableSystemSound.Asterisk, "/System/Library/Sounds/Ping.aiff")]
    [InlineData(PortableSystemSound.Exclamation, "/System/Library/Sounds/Hero.aiff")]
    [InlineData(PortableSystemSound.Hand, "/System/Library/Sounds/Basso.aiff")]
    public async Task PlaySystemSoundAsync_maps_portable_system_sounds_to_macos_sound_files(
        PortableSystemSound sound,
        string expectedPath)
    {
        var runner = new RecordingMacOSCommandRunner();
        var output = new MacOSNotificationSoundOutput(runner);

        await output.PlaySystemSoundAsync(sound, 0.25);

        var invocation = Assert.Single(runner.Invocations);
        Assert.Equal("/usr/bin/afplay", invocation.FileName);
        Assert.Equal(["-v", "0.25", expectedPath], invocation.Arguments);
    }

    [Theory]
    [InlineData(-4, "80")]
    [InlineData(0, "200")]
    [InlineData(4, "320")]
    [InlineData(9, "360")]
    public async Task SpeakAsync_uses_say_with_bounded_rate(int speed, string expectedRate)
    {
        var runner = new RecordingMacOSCommandRunner();
        var output = new MacOSSpeechOutput(runner);

        await output.SpeakAsync("生成失败，请检查 API Key", speed, 75, 2);

        var invocation = Assert.Single(runner.Invocations);
        Assert.Equal("/usr/bin/say", invocation.FileName);
        Assert.Equal(["-r", expectedRate, "生成失败，请检查 API Key"], invocation.Arguments);
    }

    [Fact]
    public async Task SpeakAsync_uses_selected_voice_when_configured()
    {
        var runner = new RecordingMacOSCommandRunner();
        var output = new MacOSSpeechOutput(runner, "Ting-Ting");

        await output.SpeakAsync("章节已保存", 0, 100, 0);

        var invocation = Assert.Single(runner.Invocations);
        Assert.Equal("/usr/bin/say", invocation.FileName);
        Assert.Equal(["-v", "Ting-Ting", "-r", "200", "章节已保存"], invocation.Arguments);
    }

    [Fact]
    public void GetVoices_parses_say_voice_catalog_and_ignores_malformed_lines()
    {
        const string sayOutput = """
            Agnes               en_US    # Isn't it nice to have a computer that will talk to you?
            Ting-Ting            zh_CN    # 您好，我叫婷婷。
            Good News            en_US    # Good news, everyone!
            malformed line without locale
            """;
        var runner = new RecordingMacOSCommandRunner(
            new MacOSNotificationCommandResult(0, sayOutput, ""));
        var catalog = new MacOSSpeechVoiceCatalog(runner);

        var voices = catalog.GetVoices();

        Assert.Collection(
            voices,
            voice =>
            {
                Assert.Equal("Agnes", voice.Name);
                Assert.Equal("en_US", voice.Culture);
                Assert.Equal("Isn't it nice to have a computer that will talk to you?", voice.SampleText);
            },
            voice =>
            {
                Assert.Equal("Ting-Ting", voice.Name);
                Assert.Equal("zh_CN", voice.Culture);
                Assert.Equal("您好，我叫婷婷。", voice.SampleText);
            },
            voice =>
            {
                Assert.Equal("Good News", voice.Name);
                Assert.Equal("en_US", voice.Culture);
                Assert.Equal("Good news, everyone!", voice.SampleText);
            });

        var invocation = Assert.Single(runner.Invocations);
        Assert.Equal("/usr/bin/say", invocation.FileName);
        Assert.Equal(["-v", "?"], invocation.Arguments);
    }

    [Fact]
    public void GetVoices_surfaces_say_command_failure()
    {
        var runner = new RecordingMacOSCommandRunner(
            new MacOSNotificationCommandResult(1, "", "say failed"));
        var catalog = new MacOSSpeechVoiceCatalog(runner);

        var ex = Assert.Throws<InvalidOperationException>(() => catalog.GetVoices());

        Assert.Contains("say failed", ex.Message);
    }

    private sealed class RecordingMacOSCommandRunner : IMacOSNotificationCommandRunner
    {
        private readonly MacOSNotificationCommandResult _result;

        public RecordingMacOSCommandRunner()
            : this(new MacOSNotificationCommandResult(0, "", ""))
        {
        }

        public RecordingMacOSCommandRunner(MacOSNotificationCommandResult result)
        {
            _result = result;
        }

        public List<MacOSNotificationCommandInvocation> Invocations { get; } = [];

        public MacOSNotificationCommandResult Run(string fileName, IReadOnlyList<string> arguments)
        {
            Invocations.Add(new MacOSNotificationCommandInvocation(fileName, arguments.ToArray()));
            return _result;
        }
    }
}
