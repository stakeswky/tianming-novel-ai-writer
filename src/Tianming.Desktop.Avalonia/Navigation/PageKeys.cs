namespace Tianming.Desktop.Avalonia.Navigation;

public readonly record struct PageKey(string Id);

public static class PageKeys
{
    public static readonly PageKey Welcome   = new("welcome");
    public static readonly PageKey Dashboard = new("dashboard");
    public static readonly PageKey Settings  = new("settings");
    // M4 扩展更多
}
