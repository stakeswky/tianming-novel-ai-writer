using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Tianming.Desktop.Avalonia.Tests.TestApp))]

namespace Tianming.Desktop.Avalonia.Tests;

public class TestApp : Application
{
    public override void Initialize()
    {
        // 不加载 axaml，避免依赖系统层 NativeMenu / FluentTheme。
        // primitive 测试只验证 StyledProperty 默认值 / setter / PropertyChanged，
        // 不依赖 ControlTheme 的实际渲染。
    }
}

public static class TestAppEntryPoint
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<TestApp>()
                     .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
