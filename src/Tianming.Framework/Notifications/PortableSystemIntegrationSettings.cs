using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.Notifications;

public enum PortableSystemIntegrationClickBehavior
{
    ShowWindow,
    HideWindow,
    Toggle,
    DoNothing
}

public enum PortableSystemIntegrationStartupMode
{
    Normal,
    Minimized
}

public enum PortableDesktopPlatform
{
    Windows,
    MacOS,
    Linux
}

public enum PortableSystemIntegrationFeature
{
    SystemNotification,
    TrayIcon,
    AutoStartup,
    UrlProtocol,
    FileTypeAssociation,
    ContextMenu,
    SendToMenu
}

public enum PortableSystemIntegrationApplyStatus
{
    Ready,
    Disabled,
    RequiresPlatformImplementation,
    UnsupportedOnMacOS
}

public sealed class PortableSystemIntegrationSettings
{
    [JsonPropertyName("EnableWindowsNotification")]
    public bool EnableSystemNotification { get; set; }

    [JsonPropertyName("NotificationSound")]
    public bool NotificationSound { get; set; } = true;

    [JsonPropertyName("NotificationPriority")]
    public string NotificationPriority { get; set; } = "Normal";

    [JsonPropertyName("ShowTrayIcon")]
    public bool ShowTrayIcon { get; set; } = true;

    [JsonPropertyName("SingleClickBehavior")]
    public PortableSystemIntegrationClickBehavior SingleClickBehavior { get; set; } =
        PortableSystemIntegrationClickBehavior.Toggle;

    [JsonPropertyName("DoubleClickBehavior")]
    public PortableSystemIntegrationClickBehavior DoubleClickBehavior { get; set; } =
        PortableSystemIntegrationClickBehavior.ShowWindow;

    [JsonPropertyName("CloseToTray")]
    public bool CloseToTray { get; set; }

    [JsonPropertyName("AutoStartup")]
    public bool AutoStartup { get; set; }

    [JsonPropertyName("StartupMode")]
    public PortableSystemIntegrationStartupMode StartupMode { get; set; } =
        PortableSystemIntegrationStartupMode.Normal;

    [JsonPropertyName("StartupDelay")]
    public int StartupDelay { get; set; }

    [JsonPropertyName("RegisterUrlProtocol")]
    public bool RegisterUrlProtocol { get; set; }

    [JsonPropertyName("AssociateFileType")]
    public bool AssociateFileType { get; set; }

    [JsonPropertyName("AddToContextMenu")]
    public bool AddToContextMenu { get; set; }

    [JsonPropertyName("AddToSendToMenu")]
    public bool AddToSendToMenu { get; set; }

    public static PortableSystemIntegrationSettings CreateDefault()
    {
        return new PortableSystemIntegrationSettings
        {
            EnableSystemNotification = false,
            NotificationSound = true,
            NotificationPriority = "Normal",
            ShowTrayIcon = true,
            SingleClickBehavior = PortableSystemIntegrationClickBehavior.Toggle,
            DoubleClickBehavior = PortableSystemIntegrationClickBehavior.ShowWindow,
            CloseToTray = false,
            AutoStartup = false,
            StartupMode = PortableSystemIntegrationStartupMode.Normal,
            StartupDelay = 0,
            RegisterUrlProtocol = false,
            AssociateFileType = false,
            AddToContextMenu = false,
            AddToSendToMenu = false
        };
    }

    public PortableSystemIntegrationSettings Clone()
    {
        return new PortableSystemIntegrationSettings
        {
            EnableSystemNotification = EnableSystemNotification,
            NotificationSound = NotificationSound,
            NotificationPriority = NotificationPriority,
            ShowTrayIcon = ShowTrayIcon,
            SingleClickBehavior = SingleClickBehavior,
            DoubleClickBehavior = DoubleClickBehavior,
            CloseToTray = CloseToTray,
            AutoStartup = AutoStartup,
            StartupMode = StartupMode,
            StartupDelay = StartupDelay,
            RegisterUrlProtocol = RegisterUrlProtocol,
            AssociateFileType = AssociateFileType,
            AddToContextMenu = AddToContextMenu,
            AddToSendToMenu = AddToSendToMenu
        };
    }

    public void CopyFrom(PortableSystemIntegrationSettings source)
    {
        ArgumentNullException.ThrowIfNull(source);

        EnableSystemNotification = source.EnableSystemNotification;
        NotificationSound = source.NotificationSound;
        NotificationPriority = source.NotificationPriority;
        ShowTrayIcon = source.ShowTrayIcon;
        SingleClickBehavior = source.SingleClickBehavior;
        DoubleClickBehavior = source.DoubleClickBehavior;
        CloseToTray = source.CloseToTray;
        AutoStartup = source.AutoStartup;
        StartupMode = source.StartupMode;
        StartupDelay = source.StartupDelay;
        RegisterUrlProtocol = source.RegisterUrlProtocol;
        AssociateFileType = source.AssociateFileType;
        AddToContextMenu = source.AddToContextMenu;
        AddToSendToMenu = source.AddToSendToMenu;
    }
}

public sealed class FileSystemIntegrationSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public FileSystemIntegrationSettingsStore(string filePath)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("System integration settings file path is required.", nameof(filePath))
            : filePath;
    }

    public async Task<PortableSystemIntegrationSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return PortableSystemIntegrationSettings.CreateDefault();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<PortableSystemIntegrationSettings>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false) ?? PortableSystemIntegrationSettings.CreateDefault();
        }
        catch (JsonException)
        {
            return PortableSystemIntegrationSettings.CreateDefault();
        }
        catch (IOException)
        {
            return PortableSystemIntegrationSettings.CreateDefault();
        }
    }

    public async Task SaveAsync(
        PortableSystemIntegrationSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, settings.Clone(), JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }
}

public sealed record PortableSystemIntegrationApplyItem(
    PortableSystemIntegrationFeature Feature,
    PortableSystemIntegrationApplyStatus Status,
    string Message);

public sealed class PortableSystemIntegrationApplyPlan
{
    public IReadOnlyList<PortableSystemIntegrationApplyItem> Items { get; init; } =
        Array.Empty<PortableSystemIntegrationApplyItem>();
}

public sealed class PortableSystemIntegrationController
{
    private readonly PortableSystemIntegrationSettings _settings;

    public PortableSystemIntegrationController(PortableSystemIntegrationSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public void ResetToDefaults()
    {
        _settings.CopyFrom(PortableSystemIntegrationSettings.CreateDefault());
    }

    public void UpdateNotificationOptions(
        bool enableSystemNotification,
        bool notificationSound,
        string priority)
    {
        _settings.EnableSystemNotification = enableSystemNotification;
        _settings.NotificationSound = notificationSound;
        _settings.NotificationPriority = string.IsNullOrWhiteSpace(priority) ? "Normal" : priority;
    }

    public static PortableSystemIntegrationApplyPlan BuildApplyPlan(
        PortableSystemIntegrationSettings settings,
        PortableDesktopPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new PortableSystemIntegrationApplyPlan
        {
            Items =
            [
                BuildSystemNotificationPlan(settings),
                BuildTrayIconPlan(settings),
                BuildAutoStartupPlan(settings, platform),
                BuildUrlProtocolPlan(settings, platform),
                BuildFileTypeAssociationPlan(settings, platform),
                BuildContextMenuPlan(settings, platform),
                BuildSendToMenuPlan(settings, platform)
            ]
        };
    }

    private static PortableSystemIntegrationApplyItem BuildSystemNotificationPlan(
        PortableSystemIntegrationSettings settings)
    {
        return settings.EnableSystemNotification
            ? new PortableSystemIntegrationApplyItem(
                PortableSystemIntegrationFeature.SystemNotification,
                PortableSystemIntegrationApplyStatus.Ready,
                "系统通知已启用，可由平台通知 sink 接线。")
            : Disabled(PortableSystemIntegrationFeature.SystemNotification);
    }

    private static PortableSystemIntegrationApplyItem BuildTrayIconPlan(
        PortableSystemIntegrationSettings settings)
    {
        return settings.ShowTrayIcon
            ? new PortableSystemIntegrationApplyItem(
                PortableSystemIntegrationFeature.TrayIcon,
                PortableSystemIntegrationApplyStatus.RequiresPlatformImplementation,
                "托盘/菜单栏图标需要 macOS menu bar status item 接线。")
            : Disabled(PortableSystemIntegrationFeature.TrayIcon);
    }

    private static PortableSystemIntegrationApplyItem BuildAutoStartupPlan(
        PortableSystemIntegrationSettings settings,
        PortableDesktopPlatform platform)
    {
        if (!settings.AutoStartup)
        {
            return Disabled(PortableSystemIntegrationFeature.AutoStartup);
        }

        return platform == PortableDesktopPlatform.MacOS
            ? new PortableSystemIntegrationApplyItem(
                PortableSystemIntegrationFeature.AutoStartup,
                PortableSystemIntegrationApplyStatus.RequiresPlatformImplementation,
                "macOS 开机自启动需要 LaunchAgent 或 SMAppService 接线。")
            : RequiresPlatform(PortableSystemIntegrationFeature.AutoStartup);
    }

    private static PortableSystemIntegrationApplyItem BuildUrlProtocolPlan(
        PortableSystemIntegrationSettings settings,
        PortableDesktopPlatform platform)
    {
        if (!settings.RegisterUrlProtocol)
        {
            return Disabled(PortableSystemIntegrationFeature.UrlProtocol);
        }

        return platform == PortableDesktopPlatform.MacOS
            ? new PortableSystemIntegrationApplyItem(
                PortableSystemIntegrationFeature.UrlProtocol,
                PortableSystemIntegrationApplyStatus.RequiresPlatformImplementation,
                "macOS URL 协议需要 Info.plist CFBundleURLTypes 接线。")
            : RequiresPlatform(PortableSystemIntegrationFeature.UrlProtocol);
    }

    private static PortableSystemIntegrationApplyItem BuildFileTypeAssociationPlan(
        PortableSystemIntegrationSettings settings,
        PortableDesktopPlatform platform)
    {
        if (!settings.AssociateFileType)
        {
            return Disabled(PortableSystemIntegrationFeature.FileTypeAssociation);
        }

        return platform == PortableDesktopPlatform.MacOS
            ? new PortableSystemIntegrationApplyItem(
                PortableSystemIntegrationFeature.FileTypeAssociation,
                PortableSystemIntegrationApplyStatus.RequiresPlatformImplementation,
                "macOS 文件关联需要 UTI/Document Types 接线。")
            : RequiresPlatform(PortableSystemIntegrationFeature.FileTypeAssociation);
    }

    private static PortableSystemIntegrationApplyItem BuildContextMenuPlan(
        PortableSystemIntegrationSettings settings,
        PortableDesktopPlatform platform)
    {
        if (!settings.AddToContextMenu)
        {
            return Disabled(PortableSystemIntegrationFeature.ContextMenu);
        }

        return platform == PortableDesktopPlatform.MacOS
            ? new PortableSystemIntegrationApplyItem(
                PortableSystemIntegrationFeature.ContextMenu,
                PortableSystemIntegrationApplyStatus.UnsupportedOnMacOS,
                "Windows 右键菜单注册表集成在 macOS 不适用。")
            : RequiresPlatform(PortableSystemIntegrationFeature.ContextMenu);
    }

    private static PortableSystemIntegrationApplyItem BuildSendToMenuPlan(
        PortableSystemIntegrationSettings settings,
        PortableDesktopPlatform platform)
    {
        if (!settings.AddToSendToMenu)
        {
            return Disabled(PortableSystemIntegrationFeature.SendToMenu);
        }

        return platform == PortableDesktopPlatform.MacOS
            ? new PortableSystemIntegrationApplyItem(
                PortableSystemIntegrationFeature.SendToMenu,
                PortableSystemIntegrationApplyStatus.UnsupportedOnMacOS,
                "Windows 发送到菜单在 macOS 不适用。")
            : RequiresPlatform(PortableSystemIntegrationFeature.SendToMenu);
    }

    private static PortableSystemIntegrationApplyItem Disabled(PortableSystemIntegrationFeature feature)
    {
        return new PortableSystemIntegrationApplyItem(
            feature,
            PortableSystemIntegrationApplyStatus.Disabled,
            "未启用。");
    }

    private static PortableSystemIntegrationApplyItem RequiresPlatform(PortableSystemIntegrationFeature feature)
    {
        return new PortableSystemIntegrationApplyItem(
            feature,
            PortableSystemIntegrationApplyStatus.RequiresPlatformImplementation,
            "需要平台实现接线。");
    }
}
