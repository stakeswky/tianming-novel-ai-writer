using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TM.Framework.Appearance;
using TM.Framework.Preferences;
using TM.Framework.SystemInfo;
using TM.Framework.SystemMonitor;

namespace Tianming.Desktop.Avalonia.ViewModels.Settings;

/// <summary>
/// M7 Lane C "系统" 设置 page VM — 11 个 SectionCard 覆盖矩阵 D #10/#11/#19-#27。
/// 多数 Card 直接 read/write Singleton settings data class（VM 改 → data class 改 →
/// 与持有同一 Singleton 实例的任何 service 共享）。系统信息 / 运行环境 / 诊断信息
/// 三项用 System.Environment + GC API 自组装（避免接复杂 probe）。
/// 数据清理 / 日志 / 代理三项展示状态文本，完整编辑器后续提供。
/// </summary>
public partial class SystemSettingsViewModel : ObservableObject
{
    private readonly PortableUIResolutionSettings _uiResolution;
    private readonly PortableLoadingAnimationSettings _loadingAnimation;
    private readonly PortableDisplaySettings _displaySettings;
    private readonly PortableLocaleSettings _localeSettings;
    private readonly PortableRuntimeEnvironmentSettings _runtimeEnvSettings;
    private readonly PortableSystemMonitorService _systemMonitorService;

    // #10 UI 分辨率
    [ObservableProperty] private int _windowWidth;
    [ObservableProperty] private int _windowHeight;
    [ObservableProperty] private int _scalePercent;

    // #11 加载动画
    [ObservableProperty] private string _loadingAnimationStatus = string.Empty;

    // #19 代理
    [ObservableProperty] private string _proxyStatusText = string.Empty;

    // #20 日志
    [ObservableProperty] private string _logStatusText = string.Empty;

    // #21 数据清理
    [ObservableProperty] private string _dataCleanupScanResult = string.Empty;

    // #22 系统信息
    [ObservableProperty] private string _osDescription = string.Empty;
    [ObservableProperty] private string _machineName = string.Empty;
    [ObservableProperty] private int _processorCount;

    // #23 运行环境
    [ObservableProperty] private string _runtimeVersion = string.Empty;
    [ObservableProperty] private string _frameworkDescription = string.Empty;
    [ObservableProperty] private string _runtimeIdentifier = string.Empty;

    // #24 诊断信息
    [ObservableProperty] private long _processWorkingSetMb;
    [ObservableProperty] private int _threadCount;
    [ObservableProperty] private int _gen0CollectionCount;

    // #25 系统监控（Lane 0 已 DI service）
    [ObservableProperty] private string _systemMonitorStatusText = string.Empty;

    // #26 显示偏好
    [ObservableProperty] private bool _showFunctionBar;
    [ObservableProperty] private string _listDensity = string.Empty;

    // #27 语言区域
    [ObservableProperty] private string _language = string.Empty;
    [ObservableProperty] private string _timeZoneId = string.Empty;
    [ObservableProperty] private string _dateFormat = string.Empty;
    [ObservableProperty] private bool _use24HourFormat;

    public SystemSettingsViewModel(
        PortableUIResolutionSettings uiResolution,
        PortableLoadingAnimationSettings loadingAnimation,
        PortableDisplaySettings displaySettings,
        PortableLocaleSettings localeSettings,
        PortableRuntimeEnvironmentSettings runtimeEnvSettings,
        PortableSystemMonitorService systemMonitorService)
    {
        _uiResolution = uiResolution;
        _loadingAnimation = loadingAnimation;
        _displaySettings = displaySettings;
        _localeSettings = localeSettings;
        _runtimeEnvSettings = runtimeEnvSettings;
        _systemMonitorService = systemMonitorService;

        LoadInitialState();
    }

    private void LoadInitialState()
    {
        // #10
        WindowWidth = _uiResolution.WindowWidth;
        WindowHeight = _uiResolution.WindowHeight;
        ScalePercent = _uiResolution.ScalePercent;

        // #11
        LoadingAnimationStatus = $"已就绪（默认）";

        // #19 / #20
        ProxyStatusText = "代理路由已通过 Lane 0 装到 HttpClient；完整代理链编辑器后续提供";
        LogStatusText = "日志写入 ~/Library/Application Support/Tianming/Logs/；级别 / 输出目标编辑器后续提供";

        // #21
        DataCleanupScanResult = "尚未扫描。点击 '扫描' 查看可清理项。";

        // #22-#24
        RefreshSystemInfo();
        RefreshDiagnostics();

        // #25
        RefreshSystemMonitorStatus();

        // #26
        ShowFunctionBar = _displaySettings.ShowFunctionBar;
        ListDensity = _displaySettings.ListDensity.ToString();

        // #27
        Language = _localeSettings.Language;
        TimeZoneId = _localeSettings.TimeZoneId;
        DateFormat = _localeSettings.DateFormat;
        Use24HourFormat = _localeSettings.Use24HourFormat;
    }

    private void RefreshSystemInfo()
    {
        OsDescription = RuntimeInformation.OSDescription;
        MachineName = Environment.MachineName;
        ProcessorCount = Environment.ProcessorCount;
    }

    private void RefreshDiagnostics()
    {
        var proc = Process.GetCurrentProcess();
        ProcessWorkingSetMb = proc.WorkingSet64 / 1024 / 1024;
        ThreadCount = proc.Threads.Count;
        Gen0CollectionCount = GC.CollectionCount(0);

        RuntimeVersion = Environment.Version.ToString();
        FrameworkDescription = RuntimeInformation.FrameworkDescription;
        RuntimeIdentifier = RuntimeInformation.RuntimeIdentifier;
    }

    private void RefreshSystemMonitorStatus()
    {
        // PortableSystemMonitorService 接口暂未在 Lane C 接 RefreshAsync；
        // 仅展示 service 已注册的状态文案，详细 CPU/内存/磁盘 view 后续 milestone
        SystemMonitorStatusText = $"系统监控服务已就绪：{_systemMonitorService.GetType().Name}（实时数据展示后续提供）";
    }

    [RelayCommand]
    public void RefreshSystemInfoNow() => RefreshSystemInfo();

    [RelayCommand]
    public void RefreshDiagnosticsNow() => RefreshDiagnostics();

    [RelayCommand]
    public void ScanDataCleanup()
    {
        // 占位实现：真正的 PortableDataCleanupController.CleanupAsync 是 destructive
        // 操作，需用户多次确认 + 清理类别选择，留 deferred。
        DataCleanupScanResult = "扫描完成：未检测到可清理项目（占位，真实扫描需要完整 UI 后续提供）。";
    }

    // 数据写回（用户改 UI ↔ data class）
    partial void OnWindowWidthChanged(int value) => _uiResolution.WindowWidth = value;
    partial void OnWindowHeightChanged(int value) => _uiResolution.WindowHeight = value;
    partial void OnScalePercentChanged(int value) => _uiResolution.ScalePercent = value;
    partial void OnShowFunctionBarChanged(bool value) => _displaySettings.ShowFunctionBar = value;
    partial void OnLanguageChanged(string value) => _localeSettings.Language = value;
    partial void OnTimeZoneIdChanged(string value) => _localeSettings.TimeZoneId = value;
    partial void OnDateFormatChanged(string value) => _localeSettings.DateFormat = value;
    partial void OnUse24HourFormatChanged(bool value) => _localeSettings.Use24HourFormat = value;
}
