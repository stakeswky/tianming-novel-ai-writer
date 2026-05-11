using TM.Framework.Notifications;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableSystemIntegrationSettingsTests
{
    [Fact]
    public void Default_settings_match_original_system_integration_defaults()
    {
        var settings = PortableSystemIntegrationSettings.CreateDefault();

        Assert.False(settings.EnableSystemNotification);
        Assert.True(settings.NotificationSound);
        Assert.Equal("Normal", settings.NotificationPriority);
        Assert.True(settings.ShowTrayIcon);
        Assert.Equal(PortableSystemIntegrationClickBehavior.Toggle, settings.SingleClickBehavior);
        Assert.Equal(PortableSystemIntegrationClickBehavior.ShowWindow, settings.DoubleClickBehavior);
        Assert.False(settings.CloseToTray);
        Assert.False(settings.AutoStartup);
        Assert.Equal(PortableSystemIntegrationStartupMode.Normal, settings.StartupMode);
        Assert.Equal(0, settings.StartupDelay);
        Assert.False(settings.RegisterUrlProtocol);
        Assert.False(settings.AssociateFileType);
        Assert.False(settings.AddToContextMenu);
        Assert.False(settings.AddToSendToMenu);
    }

    [Fact]
    public async Task Store_round_trips_atomically_and_recovers_defaults()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "Framework", "Notifications", "SystemNotifications", "SystemIntegration", "system_integration.json");
        var store = new FileSystemIntegrationSettingsStore(path);
        var settings = PortableSystemIntegrationSettings.CreateDefault();
        settings.EnableSystemNotification = true;
        settings.NotificationPriority = "High";
        settings.ShowTrayIcon = false;
        settings.AutoStartup = true;
        settings.StartupMode = PortableSystemIntegrationStartupMode.Minimized;
        settings.StartupDelay = 12;
        settings.RegisterUrlProtocol = true;

        await store.SaveAsync(settings);
        var reloaded = await new FileSystemIntegrationSettingsStore(path).LoadAsync();

        Assert.False(File.Exists(path + ".tmp"));
        Assert.True(reloaded.EnableSystemNotification);
        Assert.Equal("High", reloaded.NotificationPriority);
        Assert.False(reloaded.ShowTrayIcon);
        Assert.True(reloaded.AutoStartup);
        Assert.Equal(PortableSystemIntegrationStartupMode.Minimized, reloaded.StartupMode);
        Assert.Equal(12, reloaded.StartupDelay);
        Assert.True(reloaded.RegisterUrlProtocol);

        await File.WriteAllTextAsync(path, "{ invalid json");
        var recovered = await new FileSystemIntegrationSettingsStore(path).LoadAsync();

        Assert.False(recovered.EnableSystemNotification);
        Assert.True(recovered.NotificationSound);
        Assert.Equal(PortableSystemIntegrationClickBehavior.Toggle, recovered.SingleClickBehavior);
    }

    [Fact]
    public void Controller_resets_and_updates_notification_options()
    {
        var settings = PortableSystemIntegrationSettings.CreateDefault();
        var controller = new PortableSystemIntegrationController(settings);

        controller.UpdateNotificationOptions(enableSystemNotification: true, notificationSound: false, priority: "High");

        Assert.True(settings.EnableSystemNotification);
        Assert.False(settings.NotificationSound);
        Assert.Equal("High", settings.NotificationPriority);

        controller.ResetToDefaults();

        Assert.False(settings.EnableSystemNotification);
        Assert.True(settings.NotificationSound);
        Assert.Equal("Normal", settings.NotificationPriority);
    }

    [Fact]
    public void BuildApplyPlan_marks_macos_side_effects_as_manual_integration_boundaries()
    {
        var settings = PortableSystemIntegrationSettings.CreateDefault();
        settings.EnableSystemNotification = true;
        settings.AutoStartup = true;
        settings.RegisterUrlProtocol = true;
        settings.AssociateFileType = true;
        settings.AddToContextMenu = true;
        settings.AddToSendToMenu = true;

        var plan = PortableSystemIntegrationController.BuildApplyPlan(settings, PortableDesktopPlatform.MacOS);

        Assert.Contains(plan.Items, item =>
            item.Feature == PortableSystemIntegrationFeature.SystemNotification &&
            item.Status == PortableSystemIntegrationApplyStatus.Ready);
        Assert.Contains(plan.Items, item =>
            item.Feature == PortableSystemIntegrationFeature.AutoStartup &&
            item.Status == PortableSystemIntegrationApplyStatus.RequiresPlatformImplementation &&
            item.Message.Contains("LaunchAgent"));
        Assert.Contains(plan.Items, item =>
            item.Feature == PortableSystemIntegrationFeature.UrlProtocol &&
            item.Message.Contains("Info.plist"));
        Assert.Contains(plan.Items, item =>
            item.Feature == PortableSystemIntegrationFeature.FileTypeAssociation &&
            item.Message.Contains("UTI"));
        Assert.Contains(plan.Items, item =>
            item.Feature == PortableSystemIntegrationFeature.ContextMenu &&
            item.Status == PortableSystemIntegrationApplyStatus.UnsupportedOnMacOS);
        Assert.Contains(plan.Items, item =>
            item.Feature == PortableSystemIntegrationFeature.SendToMenu &&
            item.Status == PortableSystemIntegrationApplyStatus.UnsupportedOnMacOS);
    }
}
