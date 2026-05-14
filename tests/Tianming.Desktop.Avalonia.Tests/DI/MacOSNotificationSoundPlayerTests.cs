using TM.Framework.Notifications;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.DI;

public class MacOSNotificationSoundPlayerTests
{
    [Fact]
    public async Task Sound_player_broadcast_uses_registered_speech_output()
    {
        var speech = new RecordingSpeechOutput();
        var player = new PortableNotificationSoundPlayer(
            new PortableNotificationSoundOptions
            {
                VoiceBroadcast = new PortableVoiceBroadcastData
                {
                    IsEnabled = true,
                    BroadcastOnNotification = true,
                    Speed = 1,
                    Volume = 70,
                    Pitch = -1
                }
            },
            new RecordingSoundOutput(),
            speech);

        await player.BroadcastNotificationAsync(new PortableNotificationRequest
        {
            Title = "模型完成",
            Message = "批处理已结束",
            Type = PortableNotificationType.Info
        });

        var spoken = Assert.Single(speech.Spoken);
        Assert.Equal("模型完成。批处理已结束", spoken.Text);
        Assert.Equal(1, spoken.Speed);
        Assert.Equal(70, spoken.Volume);
        Assert.Equal(-1, spoken.Pitch);
    }

    private sealed class RecordingSoundOutput : IPortableSoundOutput
    {
        public Task PlayFileAsync(string filePath, double volume, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task PlaySystemSoundAsync(
            PortableSystemSound systemSound,
            double volume,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class RecordingSpeechOutput : IPortableSpeechOutput
    {
        public List<(string Text, int Speed, int Volume, int Pitch)> Spoken { get; } = [];

        public Task SpeakAsync(
            string text,
            int speed,
            int volume,
            int pitch,
            CancellationToken cancellationToken = default)
        {
            Spoken.Add((text, speed, volume, pitch));
            return Task.CompletedTask;
        }
    }
}
