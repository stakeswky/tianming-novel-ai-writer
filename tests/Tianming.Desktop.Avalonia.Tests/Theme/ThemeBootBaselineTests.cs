using System.IO;
using System.Xml.Linq;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Theme;

public class ThemeBootBaselineTests
{
    [Fact]
    public void App_axaml_does_not_hardcode_RequestedThemeVariant()
    {
        // 断言 App.axaml 根元素不硬编码 RequestedThemeVariant。
        // Lane 0 让 ThemeBridge 真切 ThemeVariant，但 App.axaml:22 仍 RequestedThemeVariant="Light"
        // 会导致冷启动到 AppLifecycle.InitializeAsync 之间窗口先 Light 再切 Dark，视觉闪烁。
        // 删除 axaml 上的硬编码后，启动主题完全由 ThemeBridge 控制。
        var appAxamlPath = Path.Combine(
            FindRepoRoot(),
            "src", "Tianming.Desktop.Avalonia", "App.axaml");
        Assert.True(File.Exists(appAxamlPath), $"App.axaml not found at {appAxamlPath}");

        var doc = XDocument.Load(appAxamlPath);
        var root = doc.Root!;
        var attr = root.Attribute("RequestedThemeVariant");

        Assert.True(
            attr is null,
            $"App.axaml 根元素仍写死 RequestedThemeVariant=\"{attr?.Value}\"，会导致冷启动 Light 闪烁");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Tianming.MacMigration.sln")))
            dir = dir.Parent;
        if (dir is null)
            throw new DirectoryNotFoundException("无法找到包含 Tianming.MacMigration.sln 的仓库根目录");
        return dir.FullName;
    }
}
