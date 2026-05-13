namespace Tianming.Desktop.Avalonia.Navigation;

public readonly record struct PageKey(string Id);

public static class PageKeys
{
    public static readonly PageKey Welcome   = new("welcome");
    public static readonly PageKey Dashboard = new("dashboard");
    public static readonly PageKey Settings  = new("settings");

    // M4.1 设计模块（6 页）
    public static readonly PageKey DesignWorld     = new("design.world");
    public static readonly PageKey DesignCharacter = new("design.character");
    public static readonly PageKey DesignFaction   = new("design.faction");
    public static readonly PageKey DesignLocation  = new("design.location");
    public static readonly PageKey DesignPlot      = new("design.plot");
    public static readonly PageKey DesignMaterials = new("design.materials");
}
