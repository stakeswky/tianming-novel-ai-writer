using TM.Framework.Notifications;
using Xunit;

namespace Tianming.Framework.Tests;

public class MacOSAudioDeviceCatalogTests
{
    [Fact]
    public void GetDevices_uses_system_profiler_audio_data_type()
    {
        var runner = new RecordingMacOSAudioCommandRunner(new MacOSAudioCommandResult(0, "", ""));
        var catalog = new MacOSAudioDeviceCatalog(runner);

        _ = catalog.GetDevices();

        var invocation = Assert.Single(runner.Invocations);
        Assert.Equal("/usr/sbin/system_profiler", invocation.FileName);
        Assert.Equal(["SPAudioDataType"], invocation.Arguments);
    }

    [Fact]
    public void ParseDevices_maps_system_profiler_output_to_input_and_output_devices()
    {
        const string profilerOutput = """
            Audio:

                Devices:

                    MacBook Pro Speakers:

                      Default Output Device: Yes
                      Default System Output Device: Yes
                      Manufacturer: Apple Inc.
                      Output Channels: 2
                      Current SampleRate: 48 kHz
                      Transport: Built-in

                    Studio Display Microphone:

                      Default Input Device: Yes
                      Manufacturer: Apple Inc.
                      Input Channels: 1
                      Current SampleRate: 48 kHz
                      Transport: USB

                    HDMI:

                      Output Channels: 8
                      Current SampleRate: 48 kHz
                      Transport: HDMI
            """;

        var devices = MacOSAudioDeviceCatalog.ParseDevices(profilerOutput);

        Assert.Collection(
            devices,
            device =>
            {
                Assert.Equal("macos-output-macbook-pro-speakers", device.DeviceId);
                Assert.Equal("MacBook Pro Speakers", device.DeviceName);
                Assert.Equal(PortableAudioDeviceType.Output, device.DeviceType);
                Assert.True(device.IsDefault);
                Assert.Equal("Built-in", device.Transport);
                Assert.Equal("已连接", device.Status);
            },
            device =>
            {
                Assert.Equal("macos-input-studio-display-microphone", device.DeviceId);
                Assert.Equal("Studio Display Microphone", device.DeviceName);
                Assert.Equal(PortableAudioDeviceType.Input, device.DeviceType);
                Assert.True(device.IsDefault);
                Assert.Equal("USB", device.Transport);
            },
            device =>
            {
                Assert.Equal("macos-output-hdmi", device.DeviceId);
                Assert.Equal("HDMI", device.DeviceName);
                Assert.Equal(PortableAudioDeviceType.Output, device.DeviceType);
                Assert.False(device.IsDefault);
                Assert.Equal("HDMI", device.Transport);
            });
    }

    [Fact]
    public void ParseDevices_ignores_sections_without_input_or_output_channels()
    {
        const string profilerOutput = """
            Audio:

                Devices:

                    Audio Engine:

                      Manufacturer: Example
                      Transport: Virtual
            """;

        var devices = MacOSAudioDeviceCatalog.ParseDevices(profilerOutput);

        Assert.Empty(devices);
    }

    [Fact]
    public void GetDevices_surfaces_system_profiler_failure()
    {
        var runner = new RecordingMacOSAudioCommandRunner(
            new MacOSAudioCommandResult(1, "", "profiler failed"));
        var catalog = new MacOSAudioDeviceCatalog(runner);

        var ex = Assert.Throws<InvalidOperationException>(() => catalog.GetDevices());

        Assert.Contains("profiler failed", ex.Message);
    }

    [Fact]
    public void SystemVolume_gets_master_volume_from_osascript()
    {
        var runner = new RecordingMacOSAudioCommandRunner(new MacOSAudioCommandResult(0, "67\n", ""));
        var controller = new MacOSSystemVolumeController(runner);

        var volume = controller.GetMasterVolume();

        Assert.Equal(67, volume);
        var invocation = Assert.Single(runner.Invocations);
        Assert.Equal("/usr/bin/osascript", invocation.FileName);
        Assert.Equal(["-e", "output volume of (get volume settings)"], invocation.Arguments);
    }

    [Fact]
    public void SystemVolume_sets_clamped_master_volume_and_mute_state()
    {
        var runner = new RecordingMacOSAudioCommandRunner(new MacOSAudioCommandResult(0, "", ""));
        var controller = new MacOSSystemVolumeController(runner);

        Assert.True(controller.SetMasterVolume(125));
        Assert.True(controller.SetMute(true));

        Assert.Collection(
            runner.Invocations,
            invocation => Assert.Equal(["-e", "set volume output volume 100"], invocation.Arguments),
            invocation => Assert.Equal(["-e", "set volume output muted true"], invocation.Arguments));
    }

    [Fact]
    public void SystemVolume_reads_mute_state_and_toggles_it()
    {
        var runner = new QueueMacOSAudioCommandRunner(
            new MacOSAudioCommandResult(0, "false\n", ""),
            new MacOSAudioCommandResult(0, "", ""));
        var controller = new MacOSSystemVolumeController(runner);

        Assert.True(controller.ToggleMute());

        Assert.Collection(
            runner.Invocations,
            invocation => Assert.Equal(["-e", "output muted of (get volume settings)"], invocation.Arguments),
            invocation => Assert.Equal(["-e", "set volume output muted true"], invocation.Arguments));
    }

    private sealed class RecordingMacOSAudioCommandRunner : IMacOSAudioCommandRunner
    {
        private readonly MacOSAudioCommandResult _result;

        public RecordingMacOSAudioCommandRunner(MacOSAudioCommandResult result)
        {
            _result = result;
        }

        public List<MacOSAudioCommandInvocation> Invocations { get; } = [];

        public MacOSAudioCommandResult Run(string fileName, IReadOnlyList<string> arguments)
        {
            Invocations.Add(new MacOSAudioCommandInvocation(fileName, arguments.ToArray()));
            return _result;
        }
    }

    private sealed class QueueMacOSAudioCommandRunner : IMacOSAudioCommandRunner
    {
        private readonly Queue<MacOSAudioCommandResult> _results;

        public QueueMacOSAudioCommandRunner(params MacOSAudioCommandResult[] results)
        {
            _results = new Queue<MacOSAudioCommandResult>(results);
        }

        public List<MacOSAudioCommandInvocation> Invocations { get; } = [];

        public MacOSAudioCommandResult Run(string fileName, IReadOnlyList<string> arguments)
        {
            Invocations.Add(new MacOSAudioCommandInvocation(fileName, arguments.ToArray()));
            return _results.Dequeue();
        }
    }
}
